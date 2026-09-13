# Command line

Ciallo opens one `.ciallo` document at startup. Application options follow Godot's `--` separator:

```powershell
./Ciallo.exe -- "C:/drawings/example.ciallo" --user-data-dir "C:/ciallo-tests/profile" --debug-info
```

When running from source, put engine options before the separator:

```powershell
& $env:GODOT_BIN --path Ciallo -- --open "C:/drawings/example.ciallo" --factory-startup
```

Godot 4.6 also preserves bare `.ciallo` filenames before the separator, so `Ciallo.exe "example.ciallo"` works for file associations and dragging a document onto the executable. Keep application switches after `--`. Paths are resolved against the running process's current directory; `res://` and `user://` paths use Godot's normal path resolution.

| Argument | Behavior |
| --- | --- |
| `FILE` or `--open FILE` | Open one existing document once the main scene is ready. A failed load prints an error and shows the normal open-document error dialog. |
| `--user-data-dir DIR` | Override Godot's `user://` root for subsequent access. Create the directory if needed. Defaults to Godot's normal user directory. |
| `--factory-startup` | Use built-in default configuration for this run. Existing user configuration is neither loaded nor automatically overwritten. Does not change `user://`. |
| `--debug-info` | Print application/build, engine, OS, CPU/GPU, renderer and effective data/configuration paths at startup, then continue running normally. The same system information appears in copied bug reports. |

Path options support both `--option value` and `--option=value`. One document is supported; supplying multiple filenames or combining a filename with `--open` is an error. Repeating `--user-data-dir`, unknown application options, empty paths and missing option values are errors. Argument errors exit with code 2 before the other application autoloads initialize. A second `--` inside the application arguments allows a filename beginning with `-`.

## User data and factory startup

The selected `user://` directory contains `Preference.json`, `Brush/`, `Marker/`, `DockableLayout.tres`, `config.ini`, `shortcut_profiles/` and `DocumentDurability/v1/` (including recovery snapshots, outbox and Steam OAuth credentials). Product code and addons keep using `user://` paths.

Factory startup skips loading and automatic saving of preferences, brushes, user marker textures, dock layout and shortcut configuration. It starts with English and built-in defaults; configuration edits remain usable in memory for the current session. It does not reset document recovery data or Steam authorization: these remain in the selected user directory. Explicit document saves and shortcut-profile exports still write the paths chosen by the user.

The flags compose: `--user-data-dir DIR --factory-startup` keeps `user://` at `DIR` and uses default configuration without overwriting the configuration already there. No temporary profile or directory cleanup is involved.

The first autoload configures Godot's built-in custom user directory settings in memory. On Windows it sets the process-local `APPDATA` to the requested directory's parent; on Linux/BSD it sets `XDG_DATA_HOME`. Godot appends the requested directory name. These changes affect the running process and children it launches, not the editor, shell or machine settings. Other platforms can select a subdirectory of `OS.GetDataDir()`; paths outside that directory are rejected. Filesystem roots and directory names changed by Godot's sanitization (for example a final component containing `..`) are rejected instead of silently using another directory. No runtime override is saved to `project.godot`.

`AutoloadCommandLine` stays first in the autoload order. Its `_EnterTree()` initializes `AppCommandLineOptions` and applies the directory override before other autoloads load configuration. Its `_Ready()` configures Keychain before Keychain's `_ready()`, then waits for the root window's one-shot `Ready` signal to open the document and print diagnostics after the entire initial scene tree is ready.

Application code accesses the parsed values directly through the global static class, for example `AppCommandLineOptions.FactoryStartup` and `AppCommandLineOptions.DocumentPath`. Its properties have private setters; the autoload initializes them explicitly rather than parsing in a static constructor.

Files opened by the engine before autoloads run keep their original location, including the engine log. Ciallo captures that log path before overriding `user://`, so debug information and copied bug reports refer to the actual log. Fennara's daemon-selected absolute artifact paths also keep working. Subsequent `FileAccess`, `DirAccess`, resource operations and `ProjectSettings.GlobalizePath("user://...")` use the override.

## Fennara

Fennara 0.4.3 adds the engine separator. Pass each argument as one array element, without additional shell quotes:

```json
{
  "action": "start",
  "scene_path": "res://Main.tscn",
  "user_args": [
    "C:/drawings/example.ciallo",
    "--user-data-dir", "C:/ciallo-tests/profile",
    "--factory-startup",
    "--debug-info"
  ]
}
```

Godot strips whitespace at the edges of each argument and replaces literal `%20` with spaces before Ciallo receives it. Embedded spaces and Unicode paths work; pass actual spaces instead of URL-encoding file paths.
