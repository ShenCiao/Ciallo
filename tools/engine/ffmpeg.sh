# Sourced by engine.sh. Native tools are prepared explicitly, independently of Godot.
ffmpeg_sha256() {
    local actual
    if command -v sha256sum >/dev/null 2>&1; then actual=$(sha256sum "$1");
    else actual=$(shasum -a 256 "$1"); fi
    printf '%s\n' "${actual%% *}"
}

prepare_ffmpeg() (
    local rid=$1 metadata version url executable binary_hash
    local target cache cached_binary download='' staged=''
    case "$rid" in
        win-x64|linux-x64|osx-arm64) ;;
        *) fail "Unsupported FFmpeg platform: $rid. Use win-x64, linux-x64, or osx-arm64." ;;
    esac
    metadata=$(jq -er --arg rid "$rid" \
        '.[$rid] | [.version, .url, .executable, .sha256] | @tsv' \
        "$project/ExternalData/ffmpeg/manifest.json")
    IFS=$'\t' read -r version url executable binary_hash <<< "$metadata"
    target="$project/ExternalData/ffmpeg/$rid/$executable"
    if [ -f "$target" ] && [ "$(ffmpeg_sha256 "$target")" = "$binary_hash" ]; then
        chmod +x "$target"
        printf 'FFmpeg ready: %s (%s)\n' "$version" "$rid"
        return
    fi

    cache="$state/ffmpeg-downloads"
    mkdir -p "$cache" "$(dirname "$target")"
    cached_binary="$cache/$binary_hash"
    trap 'rm -f -- "$download" "$staged"' EXIT
    if [ ! -f "$cached_binary" ] || [ "$(ffmpeg_sha256 "$cached_binary")" != "$binary_hash" ]; then
        download=$(mktemp "$cache/download.XXXXXX")
        printf 'Downloading FFmpeg %s for %s\n' "$version" "$rid"
        curl --fail --location --show-error --retry 2 --connect-timeout 15 --max-time 1200 \
            --proto '=https' --proto-redir '=https' --output "$download" "$url"
        [ "$(ffmpeg_sha256 "$download")" = "$binary_hash" ] || fail 'FFmpeg executable checksum mismatch.'
        mv -f -- "$download" "$cached_binary"
        download=''
    fi

    # Stage beside the target so installation is an atomic rename.
    staged=$(mktemp "$(dirname "$target")/.ffmpeg-XXXXXX")
    cp -- "$cached_binary" "$staged"
    chmod +x "$staged"
    mv -f -- "$staged" "$target"
    staged=''
    printf 'FFmpeg ready: %s (%s)\n' "$version" "$rid"
)
