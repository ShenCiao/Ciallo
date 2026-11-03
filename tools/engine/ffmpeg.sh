# Sourced by engine.sh. Native tools are prepared explicitly, independently of Godot.
ffmpeg_sha256() {
    local actual
    if command -v sha256sum >/dev/null 2>&1; then actual=$(sha256sum "$1");
    else actual=$(shasum -a 256 "$1"); fi
    printf '%s\n' "${actual%% *}"
}

prepare_ffmpeg() (
    local rid=$1 metadata version url archive_hash format entry executable binary_hash
    local target cache archive download='' staged=''
    case "$rid" in
        win-x64|linux-x64|osx-arm64) ;;
        *) fail "Unsupported FFmpeg platform: $rid. Use win-x64, linux-x64, or osx-arm64." ;;
    esac
    metadata=$(jq -er --arg rid "$rid" \
        '.[$rid] | [.version, .url, .archive_sha256, .archive_format, .archive_entry, .executable, .sha256] | @tsv' \
        "$project/ExternalData/ffmpeg/manifest.json")
    IFS=$'\t' read -r version url archive_hash format entry executable binary_hash <<< "$metadata"
    target="$project/ExternalData/ffmpeg/$rid/$executable"
    if [ -f "$target" ] && [ "$(ffmpeg_sha256 "$target")" = "$binary_hash" ]; then
        chmod +x "$target"
        printf 'FFmpeg ready: %s (%s)\n' "$version" "$rid"
        return
    fi

    case "$format" in
        zip) command -v unzip >/dev/null || fail 'Install unzip to prepare FFmpeg.' ;;
        tar.xz) command -v tar >/dev/null || fail 'Install tar with xz support to prepare FFmpeg.' ;;
        *) fail "Unsupported FFmpeg archive format: $format" ;;
    esac
    cache="$state/ffmpeg-downloads"
    mkdir -p "$cache" "$(dirname "$target")"
    archive="$cache/$archive_hash.$format"
    trap 'rm -f -- "$download" "$staged"' EXIT
    if [ ! -f "$archive" ] || [ "$(ffmpeg_sha256 "$archive")" != "$archive_hash" ]; then
        download=$(mktemp "$cache/download.XXXXXX")
        printf 'Downloading FFmpeg %s for %s\n' "$version" "$rid"
        curl --fail --location --show-error --retry 2 --connect-timeout 15 --max-time 1200 \
            --proto '=https' --proto-redir '=https' --output "$download" "$url"
        [ "$(ffmpeg_sha256 "$download")" = "$archive_hash" ] || fail 'FFmpeg archive checksum mismatch.'
        mv -f -- "$download" "$archive"
        download=''
    fi

    # Extract just the selected executable to stdout, never archive paths to disk.
    # Keep the installed executable until both archive and binary hashes pass.
    staged=$(mktemp "$(dirname "$target")/.ffmpeg-XXXXXX")
    case "$format" in
        zip) unzip -p "$archive" "$entry" > "$staged" ;;
        tar.xz) tar -xJOf "$archive" "$entry" > "$staged" ;;
    esac
    [ "$(ffmpeg_sha256 "$staged")" = "$binary_hash" ] || fail 'FFmpeg executable checksum mismatch.'
    chmod +x "$staged"
    mv -f -- "$staged" "$target"
    staged=''
    printf 'FFmpeg ready: %s (%s)\n' "$version" "$rid"
)
