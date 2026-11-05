#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/fennara-fixture.ps1"
$sourceRoot = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('ciallo-fennara-' + [guid]::NewGuid().ToString('N'))
$checkout = "$fixture/checkout with spaces"
$registry = "$fixture/registry"
$previousHome = $env:CIALLO_FENNARA_HOME
$previousApi = $env:CIALLO_FENNARA_RELEASES_URL
$previousRoot = $env:CIALLO_ENGINE_ROOT
$previousDaemon = $env:CIALLO_FENNARA_DAEMON_URL
$server = $null
$succeeded = $false
function Run([string[]]$Arguments, [switch]$Fails) {
    & $script:bash "$checkout/engine.sh" fennara @Arguments
    if ($Fails) { if ($LASTEXITCODE -eq 0) { throw "Expected failure: $Arguments" } }
    elseif ($LASTEXITCODE -ne 0) { throw "Fennara failed: $Arguments" }
}
function Assert-Installed([string]$Version) {
    if ([IO.File]::ReadAllText("$checkout/Ciallo/addons/fennara/VERSION").Trim() -ne $Version) { throw 'Wrong addon version.' }
    if ((Get-Content -Raw "$env:CIALLO_FENNARA_HOME/current.json" | ConvertFrom-Json).version -ne $Version) { throw 'Wrong shared runtime.' }
    if (Test-Path "$checkout/Ciallo/addons/fennara/ai") { throw 'Guidance was deployed.' }
    if (Test-Path "$env:CIALLO_FENNARA_HOME/versions/$Version/addon/addons/fennara/ai") { throw 'Cached addon includes guidance.' }
    if ([IO.File]::ReadAllText("$checkout/AGENTS.md") -cne 'root instructions' -or
        [IO.File]::ReadAllText("$checkout/Ciallo/AGENTS.md") -cne 'project instructions') { throw 'Instructions changed.' }
    if ([IO.File]::ReadAllText("$checkout/Ciallo/project.godot") -cne 'config_version=5') { throw 'Project settings changed.' }
}
try {
    $script:bash = if ($IsWindows) {
        [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName((Get-Command git.exe).Source)) '../bin/bash.exe'))
    } else { '/bin/bash' }
    [IO.Directory]::CreateDirectory("$checkout/Ciallo") | Out-Null
    [IO.Directory]::CreateDirectory("$checkout/tools") | Out-Null
    Copy-Item "$sourceRoot/tools/engine" "$checkout/tools/engine" -Recurse
    Copy-Item "$sourceRoot/engine.sh" "$checkout/engine.sh"
    Copy-Item "$sourceRoot/NuGet.Config" "$checkout/NuGet.Config"
    Copy-Item "$sourceRoot/.gitignore" "$checkout/.gitignore"
    [IO.File]::WriteAllText("$checkout/Ciallo/project.godot", 'config_version=5')
    [IO.File]::WriteAllText("$checkout/AGENTS.md", 'root instructions')
    [IO.File]::WriteAllText("$checkout/Ciallo/AGENTS.md", 'project instructions')
    [IO.Directory]::CreateDirectory("$checkout/.codex") | Out-Null
    [IO.File]::WriteAllText("$checkout/.codex/config.toml", "model = 'existing-model'`n")
    if (Test-Path "$sourceRoot/.ciallo/tools") {
        [IO.Directory]::CreateDirectory("$checkout/.ciallo") | Out-Null
        Copy-Item "$sourceRoot/.ciallo/tools" "$checkout/.ciallo/tools" -Recurse
    }
    & git init -q $checkout
    $env:CIALLO_ENGINE_ROOT = $checkout
    $env:CIALLO_FENNARA_HOME = "$fixture/fennara user"
    $probe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
    $probe.Start(); $port=$probe.LocalEndpoint.Port; $probe.Stop()
    $base = "http://localhost:$port/"
    $daemonProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $daemonProbe.Start(); $env:CIALLO_FENNARA_DAEMON_URL = "http://127.0.0.1:$($daemonProbe.LocalEndpoint.Port)"; $daemonProbe.Stop()
    $env:CIALLO_FENNARA_RELEASES_URL = "${base}releases"
    $first = New-FennaraFixture $registry $base '0.4.3'
    $second = New-FennaraFixture $registry $base '0.4.4'
    $bad = New-FennaraFixture $registry $base '0.4.5' -BadGuidance
    Copy-Item "$registry/releases/tags/v0.4.4" "$registry/releases/latest" -Force
    Set-FennaraFixtureLock $checkout $first
    $server = Start-Job -ArgumentList $base,$registry -ScriptBlock {
        param($base,$registry)
        $listener=[Net.HttpListener]::new(); $listener.Prefixes.Add($base); $listener.Start()
        try {
            while ($listener.IsListening) {
                $pending=$listener.GetContextAsync()
                while (-not $pending.Wait(250)) { }
                $context=$pending.GetAwaiter().GetResult()
                [IO.File]::AppendAllText("$registry/requests.log", $context.Request.Url.AbsolutePath + "`n")
                $file=Join-Path $registry ([Uri]::UnescapeDataString($context.Request.Url.AbsolutePath).TrimStart('/'))
                if ([IO.File]::Exists($file)) {
                    $bytes=[IO.File]::ReadAllBytes($file); $context.Response.ContentLength64=$bytes.Length
                    $context.Response.OutputStream.Write($bytes,0,$bytes.Length)
                } else { $context.Response.StatusCode=404 }
                $context.Response.Close()
            }
        } finally { $listener.Close() }
    }
    Run @('sync','--ci')
    if (Test-Path $env:CIALLO_FENNARA_HOME) { throw 'CI touched the shared user installation.' }
    if ([IO.File]::ReadAllText("$checkout/.codex/config.toml") -cne "model = 'existing-model'`n") { throw 'CI changed MCP config.' }
    # Existing upstream guidance is removed on adoption; both instruction files survive.
    [IO.Directory]::CreateDirectory("$checkout/Ciallo/addons/fennara/ai") | Out-Null
    [IO.File]::WriteAllText("$checkout/Ciallo/addons/fennara/ai/guidelines.md", 'old upstream instructions')
    Run @('sync')
    Assert-Installed '0.4.3'
    $config = [IO.File]::ReadAllText("$checkout/.codex/config.toml")
    if (!$config.Contains("model = 'existing-model'") -or !$config.Contains('--project-path') -or
        !$config.Contains($checkout.Replace('\','/') + '/Ciallo')) { throw 'MCP binding or preserved config missing.' }
    $initialLock = [IO.File]::ReadAllText("$checkout/tools/engine/dependencies.lock")
    $initialRequests = @(Get-Content "$registry/requests.log").Count
    [IO.File]::WriteAllText("$checkout/Ciallo/addons/fennara/runtime/game_capture_helper.gd.uid", 'uid://fixture')
    [IO.File]::WriteAllText("$checkout/Ciallo/addons/fennara/bin/~libfennara.windows.editor.x86_64.dll", 'Godot live library copy')
    Run @('sync')
    if (!(Test-Path "$checkout/Ciallo/addons/fennara/bin/~libfennara.windows.editor.x86_64.dll")) { throw 'Repeat sync replaced a live addon.' }
    if (@(Get-Content "$registry/requests.log").Count -ne $initialRequests) { throw 'Repeat sync downloaded files.' }
    if ([IO.File]::ReadAllText("$checkout/tools/engine/dependencies.lock") -cne $initialLock) { throw 'Sync rewrote lock.' }
    Run @('update')
    Assert-Installed '0.4.4'
    if ([IO.File]::ReadAllText("$checkout/Ciallo/addons/fennara/runtime/game_capture_helper.gd.uid") -cne 'uid://fixture') { throw 'Godot script UID was lost on upgrade.' }
    Run @('update','0.4.3')
    Assert-Installed '0.4.3'
    # Broken payload and checksum must fail without changing the active installation or lock.
    $before = [IO.File]::ReadAllText("$checkout/tools/engine/dependencies.lock")
    Run @('update','0.4.5') -Fails
    Assert-Installed '0.4.3'
    if ([IO.File]::ReadAllText("$checkout/tools/engine/dependencies.lock") -cne $before) { throw 'Rejected update changed lock.' }
    $metadataPath = "$registry/releases/tags/v0.4.4"
    $metadata = Get-Content -Raw $metadataPath | ConvertFrom-Json
    $digest = $metadata.assets[0].digest
    $metadata.assets[0].digest = 'sha256:' + ('0' * 64)
    [IO.File]::WriteAllText($metadataPath, ($metadata | ConvertTo-Json -Depth 5))
    Run @('update','0.4.4') -Fails
    Assert-Installed '0.4.3'
    $metadata.assets[0].digest = $digest
    [IO.File]::WriteAllText($metadataPath, ($metadata | ConvertTo-Json -Depth 5))
    if ($IsWindows) {
        # Fail the final rename after addon/runtime replacements: rollback must restore every earlier entry.
        $held = [IO.File]::Open("$checkout/tools/engine/dependencies.lock", [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try { Run @('update','0.4.4') -Fails } finally { $held.Dispose() }
        Assert-Installed '0.4.3'
        if ([IO.File]::ReadAllText("$checkout/tools/engine/dependencies.lock") -cne $before) { throw 'Failed activation changed lock.' }
    }
    # Reproduce a process interrupted after moving the previous addon out of the way.
    $target = "$checkout/Ciallo/addons/fennara"
    $backup = "$target.ciallo-previous-fixture"
    [IO.Directory]::Move($target,$backup)
    $journal = @{Committed=$false;Entries=@(@{Target=$target;Next="$target.ciallo-next-fixture";Backup=$backup;IsDirectory=$true;HadOriginal=$true})}
    [IO.File]::WriteAllText("$env:CIALLO_FENNARA_HOME/.ciallo-transaction.json", ($journal | ConvertTo-Json -Depth 5))
    Run @('sync')
    Assert-Installed '0.4.3'
    if (Test-Path $backup) { throw 'Interrupted activation was not cleaned up.' }
    Run @('status')
    # Exercise the Linux package layout on every host; no fixture executables are launched.
    $env:CIALLO_FENNARA_HOME = "$fixture/linux-fennara-user"
    & dotnet run --project "$checkout/tools/engine/fennara/Fennara.csproj" --configuration Release --no-launch-profile -- $checkout linux-x64 sync
    if ($LASTEXITCODE -ne 0) { throw 'Linux Fennara provisioning failed.' }
    $cefRoot = "$env:CIALLO_FENNARA_HOME/webview/cef/linux-x64"
    $marker = Get-Content -Raw "$cefRoot/0.4.3/fennara-cef-runtime.json" | ConvertFrom-Json
    $current = Get-Content -Raw "$cefRoot/current.json" | ConvertFrom-Json
    if ($marker.runtime -ne 'cef' -or $marker.platform_arch -ne 'linux-x64' -or
        $marker.version -ne '0.4.3' -or $current.dir -ne '0.4.3' -or !$marker.archive.sha256) {
        throw 'Linux CEF runtime markers are incomplete.'
    }
    & git -C $checkout check-ignore AGENTS.md
    if ($LASTEXITCODE -eq 0) { throw 'AGENTS.md is ignored.' }
    & git -C $checkout check-ignore Ciallo/addons/fennara/VERSION .codex/config.toml
    if ($LASTEXITCODE -ne 0) { throw 'Generated installation is not ignored.' }
    'Fennara CI bootstrap, adoption, upgrades, rollback, interrupted activation, checksums and instruction preservation passed.'
    $succeeded = $true
} finally {
    if ($server) { Stop-Job $server; Remove-Job $server }
    $env:CIALLO_FENNARA_HOME=$previousHome; $env:CIALLO_FENNARA_RELEASES_URL=$previousApi; $env:CIALLO_ENGINE_ROOT=$previousRoot
    $env:CIALLO_FENNARA_DAEMON_URL=$previousDaemon
    if ($succeeded) {
        $resolved=[IO.Path]::GetFullPath($fixture)
        if ([IO.Path]::GetDirectoryName($resolved) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) -or
            ![IO.Path]::GetFileName($resolved).StartsWith('ciallo-fennara-')) { throw 'Unexpected fixture cleanup path.' }
        [IO.Directory]::Delete($resolved,$true)
    } else { Write-Host "Failed fixture retained: $fixture" }
}
