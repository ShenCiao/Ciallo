# Sourced by engine.sh. Only bootstraps pinned community tools; gdvm installs Godot.
case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*) os=windows ;;
    Darwin) os=macos; [ "$(uname -m)" = arm64 ] || fail 'macOS requires Apple Silicon.' ;;
    Linux) os=linux; [ "$(uname -m)" = x86_64 ] || fail 'Linux requires x86_64.' ;;
    *) fail 'Supported platforms: Windows x64, Linux x64, macOS arm64.' ;;
esac
case "$os" in windows) rid=win-x64 ;; linux) rid=linux-x64 ;; macos) rid=osx-arm64 ;; esac
dependencies="$script_dir/dependencies.lock"
dependency() { git config --file "$dependencies" --get "$1"; }
mkdir -p "$state/tools"
for tool in gdvm jq; do
    version=$(dependency "$tool.version")
    url=$(dependency "$tool.$rid.url")
    checksum=$(dependency "$tool.$rid.sha256")
    asset=${url##*/}
    executable="$state/tools/$tool-$version-$asset"
    if [ ! -f "$executable" ]; then
        printf 'Installing %s %s\n' "$tool" "$version" >&2
        if ! curl --fail --location --show-error --retry 2 --connect-timeout 15 --max-time 300 \
            --proto '=https' --proto-redir '=https' --output "$executable.tmp-$$" "$url"; then
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
done
gdvm() { "$gdvm_bin" "$@"; }
jq() { "$jq_bin" "$@"; }
native_path() { if [ "$os" = windows ]; then cygpath -m "$1"; else printf '%s\n' "$1"; fi; }
