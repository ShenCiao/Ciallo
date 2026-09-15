# Sourced by engine.sh. Only bootstraps pinned community tools; gdvm installs Godot.
case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*) os=windows ;;
    Darwin) os=macos; [ "$(uname -m)" = arm64 ] || fail 'macOS requires Apple Silicon.' ;;
    Linux) os=linux; [ "$(uname -m)" = x86_64 ] || fail 'Linux requires x86_64.' ;;
    *) fail 'Supported platforms: Windows x64, Linux x64, macOS arm64.' ;;
esac
mkdir -p "$state/tools"
while read -r tool version platform asset checksum; do
    [ "$platform" = "$os" ] || continue
    executable="$state/tools/$tool-$version-$asset"
    if [ ! -f "$executable" ]; then
        case "$tool" in
            gdvm) url="https://github.com/adalinesimonian/gdvm/releases/download/v$version/$asset" ;;
            jq) url="https://github.com/jqlang/jq/releases/download/jq-$version/$asset" ;;
        esac
        printf 'Installing %s %s\n' "$tool" "$version" >&2
        if ! curl --fail --location --show-error --retry 2 --connect-timeout 15 --max-time 300 \
            --output "$executable.tmp-$$" "$url"; then
            rm -f "$executable.tmp-$$"; fail "Could not download $tool."
        fi
        if command -v sha256sum >/dev/null 2>&1; then actual=$(sha256sum "$executable.tmp-$$");
        else actual=$(shasum -a 256 "$executable.tmp-$$"); fi
        if [ "${actual%% *}" != "$checksum" ]; then
            rm -f "$executable.tmp-$$"; fail "$tool checksum mismatch."
        fi
        chmod +x "$executable.tmp-$$"
        mv -f "$executable.tmp-$$" "$executable"
    fi
    case "$tool" in gdvm) gdvm_bin=$executable ;; jq) jq_bin=$executable ;; esac
done < "$script_dir/tools.lock"
gdvm() { "$gdvm_bin" "$@"; }
jq() { "$jq_bin" "$@"; }
native_path() { if [ "$os" = windows ]; then cygpath -m "$1"; else printf '%s\n' "$1"; fi; }
