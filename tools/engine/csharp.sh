# Sourced by engine.sh after selecting the editor.
configure_csharp() {
    local package source target sdk_dir
    if $local_mode; then
        sdk_dir="$sharp/Tools/LocalDevelopment/Sdk"
        for source in "$sdk_dir/Sdk.props" "$sdk_dir/Sdk.targets" "$sdk_dir/../Godot.LocalDevelopment.props"; do
            [ -f "$source" ] || fail "Missing local SDK file: $source. Build Godot's .NET assemblies with --local-development."
        done
        printf '<Project><PropertyGroup><GodotLocalSdkDir>%s</GodotLocalSdkDir></PropertyGroup></Project>\n' \
            "$(native_path "$sdk_dir" | jq -Rr '@html')" | write_file "$state/local-engine.props"
    else
        mkdir -p "$state/nuget"
        for package in Godot.NET.Sdk Godot.SourceGenerators GodotSharp GodotSharpEditor; do
            source="$sharp/Tools/nupkgs/$package.$sdk.nupkg"
            target="$state/nuget/$package.$sdk.nupkg"
            [ -f "$source" ] || fail "Missing bundled package: $source"
            if [ ! -f "$target" ] || ! cmp -s "$source" "$target"; then
                cp "$source" "$target.tmp-$$"; mv -f "$target.tmp-$$" "$target"
            fi
        done
        for target in "$state/nuget/"*.nupkg; do
            case "$target" in *".$sdk.nupkg") ;; *) rm -f "$target" ;; esac
        done
        rm -f "$state/local-engine.props"
    fi
}
