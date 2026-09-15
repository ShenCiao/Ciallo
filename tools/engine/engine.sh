#!/usr/bin/env bash
# Project integration only: gdvm owns engine downloads, installation, and cleanup.
set -euo pipefail
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)
root=${CIALLO_ENGINE_ROOT:-$(CDPATH= cd "$script_dir/../.." && pwd -P)}
root=$(CDPATH= cd "$root" && pwd -P)
project="$root/Ciallo"
state="$root/.ciallo"
command_name=${1:-help}
[ "$#" -eq 0 ] || shift
templates=false; shortcut=true; ci=false; arguments=()
for arg in "$@"; do
    case "$arg" in
        --templates) templates=true ;;
        --no-shortcut) shortcut=false ;;
        --ci) ci=true; shortcut=false ;;
        --*) printf 'Unknown option: %s\n' "$arg" >&2; exit 1 ;;
        *) arguments+=("$arg") ;;
    esac
done
fail() { printf 'Ciallo engine: %s\n' "$*" >&2; exit 1; }
setting() { git config --file "$state/config" --get "$1" 2>/dev/null || true; }
write_file() {
    mkdir -p "$(dirname "$1")"
    cat > "$1.tmp-$$"
    if [ -f "$1" ] && cmp -s "$1.tmp-$$" "$1"; then rm -f "$1.tmp-$$";
    else mv -f "$1.tmp-$$" "$1"; fi
}
source "$script_dir/bootstrap.sh"
source "$script_dir/csharp.sh"
source "$script_dir/shortcuts.sh"
source "$script_dir/templates.sh"
required_version() {
    jq -er '."msbuild-sdks"."Godot.NET.Sdk" | select(test("^[0-9]+\\.[0-9]+\\.[0-9]+-ciallo\\.g[0-9a-f]{9,40}$"))' "$project/global.json"
}

# gdvm installs the release selected by Ciallo/global.json.
prepare_registry() {
    local repository registry
    repository=$(jq -er '.repository | select(test("^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))' "$script_dir/distribution.json")
    registry=${CIALLO_GDVM_REGISTRY:-https://github.com/$repository/releases/download/v$required/}
    selection="ciallo/csharp:$required"
    mkdir -p "$state/gdvm"
    printf '[godot]\nversion = %s\n[registries.ciallo]\nurl = %s\n' \
        "$(printf '%s' "$selection" | jq -Rs .)" "$(printf '%s' "$registry" | jq -Rs .)" | write_file "$state/gdvm/gdvm.toml"
}
select_editor() {
    local_mode=false
    if ! $ci && [ "$(setting engine.local)" = true ]; then
        local_mode=true; editor=$(setting engine.editor)
    else
        prepare_registry
        editor=$(cd "$state/gdvm" && gdvm show "$selection" -y --format json 2>/dev/null | jq -er .path) || {
            (cd "$state/gdvm" && gdvm install "$selection" -y)
            editor=$(cd "$state/gdvm" && gdvm show "$selection" -y --format json | jq -er .path)
        }
    fi
    [ "$os" != windows ] || editor=$(cygpath -u "$editor")
    [ -f "$editor" ] || fail "Editor not found: $editor"
    sharp="$(dirname "$editor")/GodotSharp"
    if [ ! -d "$sharp" ]; then sharp="$(dirname "$editor")/../Resources/GodotSharp"; fi
    sdk=$(tr -d '\r\n' < "$sharp/sdk.version")
    $local_mode || [ "$sdk" = "$required" ] || fail 'Downloaded bundle does not match Ciallo/global.json.'
}
sync_engine() {
    command -v dotnet >/dev/null || fail 'Install the .NET 10 SDK.'
    required=$(required_version)
    select_editor
    [ "$required" = "$(required_version)" ] || fail 'Version changed during installation; rerun setup.'
    configure_csharp
    if $templates; then install_templates; fi
    if $shortcut; then create_shortcut; fi
    jq -n --arg required "$required" --arg sdk "$sdk" --arg editor "$(native_path "$editor")" \
        --argjson local "$local_mode" '{required:$required,sdk:$sdk,editor:$editor,local:$local}' | write_file "$state/ready.json"
    if [ -n "${GITHUB_OUTPUT:-}" ]; then printf 'editor_path=%s\n' "$(native_path "$editor")" >> "$GITHUB_OUTPUT"; fi
    printf 'Ciallo engine ready: %s\n' "$sdk"
    gdvm prune
}
case "$command_name" in
    setup)
        git -C "$root" config --local core.hooksPath Ciallo/.githooks
        sync_engine ;;
    sync) sync_engine ;;
    local)
        $ci && fail 'Use sync --ci for CI builds.'
        case "${arguments[0]:-}" in
            on)
                [ "${#arguments[@]}" -le 2 ] || fail 'Usage: ./engine.sh local on [editor]'
                editor_path=${arguments[1]:-$(setting engine.editor)}
                [ -n "$editor_path" ] || fail 'Select an editor with ./engine.sh local on <editor>.'
                [ "$os" != windows ] || editor_path=$(cygpath -u "$editor_path")
                [ -f "$editor_path" ] || fail "Editor not found: $editor_path"
                editor_path="$(CDPATH= cd "$(dirname "$editor_path")" && pwd -P)/$(basename "$editor_path")"
                git config --file "$state/config" engine.editor "$editor_path"
                git config --file "$state/config" engine.local true ;;
            off)
                [ "${#arguments[@]}" -eq 1 ] || fail 'Usage: ./engine.sh local off'
                git config --file "$state/config" engine.local false ;;
            *) fail 'Usage: ./engine.sh local on [editor] | local off' ;;
        esac
        sync_engine ;;
    open)
        shortcut=false
        sync_engine
        if $local_mode; then exec "$editor" --editor --path "$project";
        else cd "$state/gdvm"; gdvm run "$selection" -y -- --editor --path "$(native_path "$project")"; fi ;;
    status|check)
        required=$(required_version)
        local_mode=false
        if ! $ci && [ "$(setting engine.local)" = true ]; then local_mode=true; fi
        jq -e --arg required "$required" --argjson local "$local_mode" \
            '.required == $required and .local == $local' "$state/ready.json" >/dev/null || fail 'Run ./engine.sh sync.'
        [ -f "$(jq -r '.editor' "$state/ready.json")" ] || fail 'Prepared editor is missing; run ./engine.sh sync.'
        jq . "$state/ready.json" ;;
    clean) gdvm prune ;;
    version) required_version ;;
    help) printf 'Usage: ./engine.sh setup|sync|local on [editor]|local off|open|status|clean [--templates] [--no-shortcut]\n' ;;
    *) fail "Unknown command: $command_name" ;;
esac
