# Sourced by engine.sh. Templates are downloaded only for an explicit --templates request.
install_templates() (
    $local_mode && fail 'Build and configure export templates separately in local engine mode.'
    local manifest target version checksum url scratch actual entry protocols
    manifest="$(dirname "$editor")/export-templates.json"
    [ -f "$manifest" ] || fail 'This engine release has no separate template manifest; select a release with optional templates.'
    jq -e --arg required "$required" '.version == $required' "$manifest" >/dev/null || fail 'Template manifest does not match the editor.'
    checksum=$(jq -er '.sha512 | select(test("^[0-9a-f]{128}$"))' "$manifest")
    url=$(jq -er '.url | select(type == "string" and length > 0)' "$manifest")
    version="${required%%-*}.${required#*-}.mono"
    case "$os" in
        windows) target="$(cygpath -u "$APPDATA")/Godot/export_templates/$version" ;;
        macos) target="$HOME/Library/Application Support/Godot/export_templates/$version" ;;
        linux) target="${XDG_DATA_HOME:-$HOME/.local/share}/godot/export_templates/$version" ;;
    esac
    if [ -f "$target/.ciallo-sha512" ] && [ "$(cat "$target/.ciallo-sha512")" = "$checksum" ] &&
        [ -f "$target/version.txt" ] && [ "$(tr -d '\r\n' < "$target/version.txt")" = "$version" ]; then
        return
    fi
    command -v unzip >/dev/null || fail 'Install unzip to prepare export templates.'
    protocols='=https'
    [ "${GDVM_ALLOW_INSECURE_URLS:-}" != 1 ] || protocols='=https,http'
    scratch=$(mktemp -d "$state/templates.XXXXXX")
    trap 'rm -rf -- "$scratch"' EXIT
    printf 'Downloading export templates for %s\n' "$os"
    curl --fail --location --show-error --retry 2 --connect-timeout 15 --max-time 600 \
        --proto "$protocols" --proto-redir "$protocols" --output "$scratch/templates.zip" "$url"
    if command -v sha512sum >/dev/null 2>&1; then actual=$(sha512sum "$scratch/templates.zip");
    else actual=$(shasum -a 512 "$scratch/templates.zip"); fi
    [ "${actual%% *}" = "$checksum" ] || fail 'Export template checksum mismatch.'
    unzip -Z1 "$scratch/templates.zip" > "$scratch/entries"
    while IFS= read -r entry; do
        case "$entry" in
            templates/*) ;;
            *) fail "Invalid template archive entry: $entry" ;;
        esac
        case "/${entry%/}/" in
            */../*|*/./*|*\\*|*:*|*//*) fail "Invalid template archive entry: $entry" ;;
        esac
    done < "$scratch/entries"
    unzip -q "$scratch/templates.zip" -d "$scratch/unpacked"
    [ "$(tr -d '\r\n' < "$scratch/unpacked/templates/version.txt")" = "$version" ] || fail 'Export templates do not match the editor.'
    mkdir -p "$target"
    cp -R "$scratch/unpacked/templates/." "$target/"
    printf '%s\n' "$checksum" > "$target/.ciallo-sha512"
)
