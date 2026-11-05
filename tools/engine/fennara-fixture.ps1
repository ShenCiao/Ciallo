# Loopback release fixtures shared by the provisioning integration checks.
function New-FennaraFixture([string]$Registry, [string]$BaseUrl, [string]$Version, [switch]$BadGuidance) {
    $release = Join-Path $Registry $Version
    $payload = Join-Path $Registry "payload-$Version"
    [IO.Directory]::CreateDirectory($release) | Out-Null
    $addon = "$payload/addon/addons/fennara"
    foreach ($dir in @('ai', 'runtime', 'bin/libfennara.macos.editor.framework')) {
        [IO.Directory]::CreateDirectory("$addon/$dir") | Out-Null
    }
    [IO.File]::WriteAllText("$addon/VERSION", $Version)
    [IO.File]::WriteAllText("$addon/release.json", (@{schema_version=1;track='stable';version=$Version;release_tag="v$Version"} | ConvertTo-Json))
    foreach ($file in @('fennara.gdextension','runtime/game_capture_helper.gd', 'bin/libfennara.windows.editor.x86_64.dll',
        'bin/libfennara.linux.editor.x86_64.so','bin/libfennara.macos.editor.framework/libfennara.macos.editor')) {
        [IO.File]::WriteAllText("$addon/$file", "fixture $Version")
    }
    [IO.File]::WriteAllText("$addon/ai/guidelines.md", 'Must not be installed.')
    if ($BadGuidance) { [IO.File]::WriteAllText("$addon/AGENTS.md", 'Must fail before activation.') }
    $addonName = "fennara-release-addon-v$Version.zip"
    [IO.Compression.ZipFile]::CreateFromDirectory("$payload/addon", "$release/$addonName")
    $assets = @{addon=@{name=$addonName;sha256=(Get-FileHash "$release/$addonName").Hash.ToLowerInvariant()}; local=@{}; cli=@{}}
    foreach ($platform in @('windows-x86_64','linux-x86_64','macos-arm64')) {
        $suffix = if ($platform -eq 'windows-x86_64') { '.exe' } else { '' }
        $localDir = "$payload/$platform/local"
        $cliDir = "$payload/$platform/cli"
        [IO.Directory]::CreateDirectory("$localDir/bin") | Out-Null
        [IO.Directory]::CreateDirectory("$cliDir/bin") | Out-Null
        [IO.File]::WriteAllText("$localDir/VERSION", $Version)
        [IO.File]::WriteAllText("$cliDir/VERSION", $Version)
        foreach ($name in @('fennara-mcp','fennara-daemon','fennara-mcp-runtime','fennara-daemon-runtime')) {
            [IO.File]::WriteAllText("$localDir/bin/$name$suffix", "fixture $Version $name")
        }
        [IO.File]::WriteAllText("$cliDir/bin/fennara$suffix", "fixture $Version CLI")
        foreach ($kind in @('local','cli')) {
            $name = "fennara-$kind-$platform-v$Version.zip"
            [IO.Compression.ZipFile]::CreateFromDirectory("$payload/$platform/$kind", "$release/$name")
            $assets[$kind][$platform] = @{name=$name;sha256=(Get-FileHash "$release/$name").Hash.ToLowerInvariant()}
        }
    }
    $cef = "$payload/cef"
    [IO.Directory]::CreateDirectory("$cef/locales") | Out-Null
    foreach ($file in @('libcef.so', 'fennara_cef_helper', 'locales/en-US.pak')) {
        [IO.File]::WriteAllText("$cef/$file", "CEF fixture $Version")
    }
    $cefName = "fennara-cef-$Version.zip"
    [IO.Compression.ZipFile]::CreateFromDirectory($cef, "$release/$cefName")
    $cefRuntime = @{id='linux-cef';kind='cef';runtime='cef';schema_version=1;platform='linux';arch='x86_64';
        platform_arch='linux-x64';version=$Version;enabled=$true;required_files=@('libcef.so','fennara_cef_helper','locales/en-US.pak');
        archive=@{format='zip';name=$cefName;sha256=(Get-FileHash "$release/$cefName").Hash.ToLowerInvariant()}}
    $manifestPath = "$release/fennara-release-manifest-v$Version.json"
    $manifest = @{schema_version=1;version=$Version;release=@{track='stable';version=$Version;release_tag="v$Version"};
        install_primitives=@('local-zip-v1','addon-zip-v1','shared-webview-cef-v1');assets=$assets;shared_runtimes=@($cefRuntime)}
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 12))
    $hash = (Get-FileHash $manifestPath).Hash.ToLowerInvariant()
    $url = "$BaseUrl$Version/fennara-release-manifest-v$Version.json"
    $releaseMetadata = @{tag_name="v$Version";draft=$false;prerelease=$false;assets=@(@{name="fennara-release-manifest-v$Version.json";browser_download_url=$url;digest="sha256:$hash"})}
    [IO.Directory]::CreateDirectory("$Registry/releases/tags") | Out-Null
    [IO.File]::WriteAllText("$Registry/releases/tags/v$Version", ($releaseMetadata | ConvertTo-Json -Depth 5))
    [IO.File]::WriteAllText("$Registry/releases/latest", ($releaseMetadata | ConvertTo-Json -Depth 5))
    return @{version=$Version;url=$url;hash=$hash}
}

function Set-FennaraFixtureLock([string]$Checkout, $Release) {
    & git config --file "$Checkout/tools/engine/dependencies.lock" fennara.version $Release.version
    & git config --file "$Checkout/tools/engine/dependencies.lock" fennara.manifest-url $Release.url
    & git config --file "$Checkout/tools/engine/dependencies.lock" fennara.manifest-sha256 $Release.hash
    if ($LASTEXITCODE -ne 0) { throw 'Could not set fixture dependency lock.' }
}
