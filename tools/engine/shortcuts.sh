# Generate local desktop entry files. Git ignores these outputs.
create_shortcut() {
    local destination command
    case "$os" in
        windows)
            powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass \
                -File "$(native_path "$script_dir/shortcut.ps1")" \
                -Editor "$(native_path "$editor")" -Project "$(native_path "$project")" \
                -Destination "$(native_path "$root/Open Ciallo.lnk")" \
                -Bash "$(native_path "$BASH")" -Entry "$(native_path "$root/engine.sh")" ;;
        macos)
            destination="$root/Open Ciallo.command"
            printf '#!/bin/bash\nexec sh %q open\n' "$root/engine.sh" | write_file "$destination"
            chmod +x "$destination" ;;
        linux)
            destination="$root/Open Ciallo.desktop"
            command=$(printf '%s' "$root/engine.sh" | sed 's/\\/\\\\/g;s/"/\\"/g;s/\$/\\$/g;s/`/\\`/g;s/%/%%/g')
            printf '[Desktop Entry]\nType=Application\nName=Ciallo Editor\nExec=sh "%s" open\nTerminal=false\n' "$command" | write_file "$destination"
            chmod +x "$destination" ;;
    esac
}
