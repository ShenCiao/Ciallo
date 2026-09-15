#!/usr/bin/env pwsh
# Integration tests for the real gdvm release. PowerShell is available on all CI runners.
$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('ciallo-gdvm-' + [guid]::NewGuid().ToString('N'))
$checkout = Join-Path $fixture 'checkout with spaces'
$registry = Join-Path $fixture 'registry'
$server = $null
$gdvm = $null
$previousRoot = $env:CIALLO_ENGINE_ROOT
$previousRegistry = $env:CIALLO_GDVM_REGISTRY
$previousInsecure = $env:GDVM_ALLOW_INSECURE_URLS
$previousPackages = $env:NUGET_PACKAGES
$registries = @()
$templateTargets = @()
$succeeded = $false
function Run([string[]]$Arguments, [switch]$Fails) {
    & $script:bash (Join-Path $checkout 'engine.sh') @Arguments
    if ($Fails) { if ($LASTEXITCODE -eq 0) { throw 'Expected setup failure.' } }
    elseif ($LASTEXITCODE -ne 0) { throw "Engine command failed: $Arguments" }
}
function Write-Json($Path, $Value) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 20))
}
function Check-Sdk([string]$Expected, [switch]$Build) {
    $selected = & dotnet msbuild "$checkout/Ciallo/Probe.csproj" -nologo -getProperty:ProbeSdkSource
    if ($LASTEXITCODE -ne 0 -or $selected -ne $Expected) { throw "Expected SDK '$Expected'; got '$selected'." }
    if ($Build) {
        & dotnet build "$checkout/Ciallo/Probe.csproj" --nologo -v:q
        if ($LASTEXITCODE -ne 0) { throw "Build failed for SDK '$Expected'." }
    }
}
try {
    if ($IsWindows) {
        $git = (Get-Command git.exe).Source
        $script:bash = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName($git)) '../bin/bash.exe'))
        $platform = 'windows-x86_64'; $binary = 'Godot.exe'
    } elseif ($IsMacOS) { $script:bash = '/bin/bash'; $platform = 'macos-arm64'; $binary = 'Godot_Ciallo' }
    else { $script:bash = '/bin/bash'; $platform = 'linux-x86_64'; $binary = 'Godot_vCiallo.x86_64' }
    [IO.Directory]::CreateDirectory("$checkout/Ciallo") | Out-Null
    [IO.Directory]::CreateDirectory("$checkout/tools") | Out-Null
    Copy-Item "$sourceRoot/tools/engine" "$checkout/tools/engine" -Recurse
    Copy-Item "$sourceRoot/engine.sh" "$checkout/engine.sh"
    Copy-Item "$sourceRoot/NuGet.Config" "$checkout/NuGet.Config"
    # Exercise the product's exact SDK import order with a minimal C# payload.
    [xml]$product = Get-Content "$sourceRoot/Ciallo/Ciallo.csproj"
    $imports = @($product.Project.Import)
    $probe = '<Project>' + (($imports | Select-Object -First 3 | ForEach-Object OuterXml) -join "`n") +
        '<PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>' +
        (($imports | Select-Object -Skip 3 | ForEach-Object OuterXml) -join "`n") + '</Project>'
    [IO.File]::WriteAllText("$checkout/Ciallo/Probe.csproj", $probe)
    [IO.File]::WriteAllText("$checkout/Ciallo/Probe.cs", 'public class Probe {}')
    # Reuse only verified tool binaries; all installs and SDK packages below are fresh.
    if (Test-Path "$sourceRoot/.ciallo/tools") {
        [IO.Directory]::CreateDirectory("$checkout/.ciallo") | Out-Null
        Copy-Item "$sourceRoot/.ciallo/tools" "$checkout/.ciallo/tools" -Recurse
    }
    & git init -q $checkout
    if ($LASTEXITCODE -ne 0) { throw 'Fixture git init failed.' }
    $env:CIALLO_ENGINE_ROOT = $checkout
    $env:NUGET_PACKAGES = Join-Path $fixture 'nuget-cache'
    $env:GDVM_ALLOW_INSECURE_URLS = '1' # Loopback fixture server only.
    $portProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $portProbe.Start(); $port = $portProbe.LocalEndpoint.Port; $portProbe.Stop()
    $base = "http://localhost:$port/"
    $versions = @(('4.6.2-ciallo.g' + [guid]::NewGuid().ToString('N').Substring(0,9)), ('4.6.2-ciallo.g' + [guid]::NewGuid().ToString('N').Substring(0,9)))
    foreach ($version in $versions) {
        $content = Join-Path $fixture $version
        $feed = "$content/GodotSharp/Tools/nupkgs"
        [IO.Directory]::CreateDirectory($feed) | Out-Null
        [IO.File]::WriteAllText("$content/$binary", 'fixture editor')
        [IO.File]::WriteAllText("$content/GodotSharp/sdk.version", $version)
        foreach ($package in @('Godot.NET.Sdk', 'GodotSharp', 'GodotSharpEditor', 'Godot.SourceGenerators')) {
            $packagePath = "$content/package-$package"
            [IO.Directory]::CreateDirectory($packagePath) | Out-Null
            [IO.File]::WriteAllText("$packagePath/$package.nuspec", "<package><metadata><id>$package</id><version>$version</version><authors>Test</authors><description>Test</description></metadata></package>")
            if ($package -eq 'Godot.NET.Sdk') {
                [IO.Directory]::CreateDirectory("$packagePath/Sdk") | Out-Null
                [IO.File]::WriteAllText("$packagePath/Sdk/Sdk.props", "<Project><Import Project=`"Sdk.props`" Sdk=`"Microsoft.NET.Sdk`"/><PropertyGroup><ProbeSdkSource>$version</ProbeSdkSource></PropertyGroup></Project>")
                [IO.File]::WriteAllText("$packagePath/Sdk/Sdk.targets", '<Project><Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk"/></Project>')
            }
            [IO.Compression.ZipFile]::CreateFromDirectory($packagePath, "$feed/$package.$version.nupkg")
        }
        $localTools = "$content/GodotSharp/Tools/LocalDevelopment"
        [IO.Directory]::CreateDirectory($localTools) | Out-Null
        Copy-Item "$content/package-Godot.NET.Sdk/Sdk" "$localTools/Sdk" -Recurse
        [IO.File]::WriteAllText("$localTools/Godot.LocalDevelopment.props", '<Project><PropertyGroup><GodotLocalDevelopment>true</GodotLocalDevelopment></PropertyGroup></Project>')
        $release = "$registry/$version"
        [IO.Directory]::CreateDirectory($release) | Out-Null
        $templateName = "templates-$platform.zip"
        $templateVersion = $version.Replace('-', '.') + '.mono'
        $templateContent = "$fixture/templates-$version/templates"
        [IO.Directory]::CreateDirectory($templateContent) | Out-Null
        [IO.File]::WriteAllText("$templateContent/version.txt", $templateVersion)
        [IO.File]::WriteAllText("$templateContent/template", "template-$version")
        [IO.Compression.ZipFile]::CreateFromDirectory("$fixture/templates-$version", "$release/$templateName")
        Write-Json "$content/export-templates.json" @{
            version=$version
            sha512=(Get-FileHash "$release/$templateName" -Algorithm SHA512).Hash.ToLowerInvariant()
            url="$base$version/$templateName"
        }
        # Editor bundles carry only template download metadata, not the template binaries.
        $zip = [IO.Compression.ZipFile]::Open("$release/bundle.zip", [IO.Compression.ZipArchiveMode]::Create)
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,"$content/$binary",$binary) | Out-Null
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,"$content/export-templates.json",'export-templates.json') | Out-Null
        Get-ChildItem "$content/GodotSharp" -Recurse -File | ForEach-Object {
            $name = [IO.Path]::GetRelativePath($content, $_.FullName).Replace('\','/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$_.FullName,$name) | Out-Null
        }
        $zip.Dispose()
        Write-Json "$release/registry.json" @{schema=2;name='Ciallo integration fixture'}
        Write-Json "$release/index.json" @{schema=2;releases=@(@{version=$version;path='release.json';variants=@{csharp=@($platform)}})}
        $artifact = @{sha512=(Get-FileHash "$release/bundle.zip" -Algorithm SHA512).Hash.ToLowerInvariant();urls=@("$base$version/bundle.zip")}
        Write-Json "$release/release.json" @{schema=2;version=$version;variants=@{csharp=@{$platform=$artifact}}}
    }
    $server = Start-Job -ArgumentList $base,$registry -ScriptBlock {
        param($base,$registry)
        $listener=[Net.HttpListener]::new(); $listener.Prefixes.Add($base); $listener.Start()
        try {
            while ($listener.IsListening) {
                # Keep Stop-Job responsive when cleanup begins without another HTTP request.
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
    foreach ($version in $versions) {
        Write-Json "$checkout/Ciallo/global.json" @{'msbuild-sdks'=@{'Godot.NET.Sdk'=$version}}
        $env:CIALLO_GDVM_REGISTRY = "$base$version/"
        $registries += $env:CIALLO_GDVM_REGISTRY.TrimEnd('/')
        Run -Arguments @('setup','--no-shortcut')
        $ready=Get-Content "$checkout/.ciallo/ready.json" -Raw | ConvertFrom-Json
        if ($ready.sdk -ne $version) { throw 'Wrong selected SDK.' }
        $templateName = "templates-$platform.zip"
        $templateRequest = "/$version/$templateName"
        if (@(Get-Content "$registry/requests.log").Contains($templateRequest)) { throw 'Default setup downloaded export templates.' }
        $templateVersion = $version.Replace('-', '.') + '.mono'
        $templateRoot = if ($IsWindows) { "$env:APPDATA/Godot/export_templates" }
            elseif ($IsMacOS) { "$HOME/Library/Application Support/Godot/export_templates" }
            elseif ($env:XDG_DATA_HOME) { "$env:XDG_DATA_HOME/godot/export_templates" }
            else { "$HOME/.local/share/godot/export_templates" }
        $templateTarget = Join-Path $templateRoot $templateVersion
        $templateTargets += $templateTarget
        if (Test-Path $templateTarget) { throw 'Default setup installed export templates.' }
        Run -Arguments @('sync','--templates','--no-shortcut')
        if ([IO.File]::ReadAllText("$templateTarget/template") -ne "template-$version") { throw 'Wrong templates installed.' }
        Run -Arguments @('sync','--templates','--no-shortcut')
        if (@(Get-Content "$registry/requests.log" | Where-Object { $_ -eq $templateRequest }).Count -ne 1) { throw 'Templates were not downloaded exactly once.' }
        $manifestPath = Join-Path ([IO.Path]::GetDirectoryName($ready.editor)) 'export-templates.json'
        $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
        $checksum = $manifest.sha512
        $manifest.sha512 = '0' * 128
        Write-Json $manifestPath $manifest
        Run -Arguments @('sync','--templates','--no-shortcut') -Fails
        if ([IO.File]::ReadAllText("$templateTarget/.ciallo-sha512").Trim() -ne $checksum) { throw 'Failed verification changed installed templates.' }
        $manifest.sha512 = $checksum
        Write-Json $manifestPath $manifest
        Check-Sdk $version -Build
    }
    $teamConfig = [IO.File]::ReadAllText("$checkout/Ciallo/global.json")
    $localEditor = "$fixture/$($versions[0])/$binary"
    # A stored path alone must leave published mode selected.
    & git config --file "$checkout/.ciallo/config" engine.editor $localEditor
    Run -Arguments @('sync','--no-shortcut')
    Check-Sdk $versions[1]
    Run -Arguments @('local','on','--no-shortcut')
    Run -Arguments @('sync','--templates','--no-shortcut') -Fails
    $env:NUGET_PACKAGES = "$fixture/local-nuget-cache"
    # No local NuGet package feed or SDK version metadata is needed to build.
    Move-Item "$fixture/$($versions[0])/GodotSharp/Tools/nupkgs" "$fixture/local-packages-unused"
    Check-Sdk $versions[0] -Build
    $localSdkProps = "$fixture/$($versions[0])/GodotSharp/Tools/LocalDevelopment/Sdk/Sdk.props"
    [IO.File]::WriteAllText($localSdkProps, [IO.File]::ReadAllText($localSdkProps).Replace($versions[0], 'rebuilt-local-sdk'))
    Check-Sdk 'rebuilt-local-sdk' -Build
    if (Test-Path "$env:NUGET_PACKAGES/godot.net.sdk") { throw 'Local mode resolved a cached NuGet SDK.' }
    $env:NUGET_PACKAGES = "$fixture/nuget-cache"
    Run -Arguments @('sync','--ci')
    Check-Sdk $versions[1]
    if ((& git config --file "$checkout/.ciallo/config" --get engine.local) -ne 'true') { throw 'CI changed the local preference.' }
    Run -Arguments @('sync','--no-shortcut')
    Check-Sdk 'rebuilt-local-sdk'
    Run -Arguments @('local','off','--no-shortcut')
    if (Test-Path "$checkout/.ciallo/local-engine.props") { throw 'Local imports remain enabled.' }
    Check-Sdk $versions[1] -Build
    Run -Arguments @('local','on',$localEditor,'--no-shortcut')
    Check-Sdk 'rebuilt-local-sdk'
    Run -Arguments @('local','off','--no-shortcut')
    if ([IO.File]::ReadAllText("$checkout/Ciallo/global.json") -cne $teamConfig) { throw 'Engine switching modified the team version.' }
    if ([IO.File]::ReadAllText("$checkout/Ciallo/Probe.csproj") -cne $probe) { throw 'Engine switching modified the project.' }
    Run -Arguments @('sync') # Exercise native shortcut creation.
    Run -Arguments @('status')
    'gdvm installation, templates, published restore, direct local SDK builds, rebuilds, CI selection, and mode switching passed.'
    $succeeded = $true
} finally {
    if ($server) { Stop-Job $server; Remove-Job $server }
    foreach ($target in $templateTargets) {
        $resolved = [IO.Path]::GetFullPath($target)
        if ([IO.Path]::GetDirectoryName($resolved) -ne [IO.Path]::GetFullPath($templateRoot) -or
            -not ($versions | Where-Object { $_.Replace('-', '.') + '.mono' -eq [IO.Path]::GetFileName($resolved) })) {
            throw 'Unexpected template cleanup path.'
        }
        if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
    }
    # Remove only the fixture versions through gdvm's own lifecycle commands.
    $gdvm = Get-ChildItem "$checkout/.ciallo/tools" -Filter 'gdvm-*' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($gdvm -and (Test-Path "$checkout/.ciallo/gdvm")) {
        foreach ($index in 0..($registries.Count-1)) {
            if ($index -lt 0 -or $registries.Count -eq 0) { break }
            [IO.File]::WriteAllText("$checkout/.ciallo/gdvm/gdvm.toml", "[registries.ciallo]`nurl = '$($registries[$index])'`n")
            Push-Location "$checkout/.ciallo/gdvm"
            & $gdvm.FullName remove "ciallo/csharp:$($versions[$index])" -y
            Pop-Location
        }
    }
    $gdvmConfig = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.gdvm/config.toml'
    if (Test-Path $gdvmConfig) {
        $configText = [IO.File]::ReadAllText($gdvmConfig)
        foreach ($url in $registries) {
            $literal = '"' + $url + '"'
            $configText = $configText.Replace($literal + ', ', '').Replace(', ' + $literal, '').Replace($literal, '')
        }
        [IO.File]::WriteAllText($gdvmConfig, $configText)
    }
    $env:CIALLO_ENGINE_ROOT=$previousRoot; $env:CIALLO_GDVM_REGISTRY=$previousRegistry
    $env:GDVM_ALLOW_INSECURE_URLS=$previousInsecure; $env:NUGET_PACKAGES=$previousPackages
    if ($succeeded) {
        $expectedParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
        if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($fixture)) -ne $expectedParent -or
            -not [IO.Path]::GetFileName($fixture).StartsWith('ciallo-gdvm-')) { throw 'Unexpected fixture cleanup path.' }
        [IO.Directory]::Delete($fixture, $true)
    } else { Write-Host "Failed fixture retained: $fixture" }
}
