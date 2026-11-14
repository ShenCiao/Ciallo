> 内容由AI书写，建议交给AI查看。

# Engine management

Requires Git with LFS, .NET 10, and a shell with curl. Use standalone Git Bash on
Windows; PowerShell 7 is preferred for shortcut generation. See the
[Contributing Guide](../.github/CONTRIBUTING.md#how-to-build) for cloning and building.

## Setup and daily use

Run from the repository root:

```sh
./engine.sh setup              # Install dependencies, Git hooks, and shortcut
./engine.sh sync               # Restore selected versions after changes or failure
./engine.sh status             # Check prepared engine selection
./engine.sh open               # Sync, then launch the project editor
./engine.sh sync --templates   # Also install this platform's export templates
./engine.sh ffmpeg             # Prepare PNG export and packaging dependency
./engine.sh clean              # Prune unused gdvm installations and archives
```

The generated `Open Ciallo` shortcut also syncs before opening. Git hooks run after
checkout, merge, rebase, and relevant commits; a failed hook does not undo the Git
operation. Fix setup errors before building. Reload the IDE's C# project after
switching engines.

Normal setup omits export templates and FFmpeg. Templates require `unzip` (included
with Git Bash); local-engine mode uses developer-provided templates. See
[Bundled FFmpeg](../Ciallo/ExternalData/ffmpeg/README.md) for its platform and export requirements.
Setup and sync also prune gdvm storage: unused entries become eligible after
30 days by default; default installations and live links are retained. Close editors
before cleanup.

## Versions and storage

| Source | Purpose |
| --- | --- |
| `Ciallo/global.json` | Sole published Godot editor/SDK version pin |
| `tools/engine/dependencies.lock` | Godot repository; gdvm, jq, Fennara, and FFmpeg versions, URLs, and hashes |
| `.ciallo/` | Ignored local tools, caches, configuration, and Godot NuGet feed |
| `~/.gdvm` | Shared editor installations managed by gdvm |

Setup verifies downloaded tools and release payloads. The editor's four bundled
Godot NuGet packages go to `.ciallo/nuget`; `NuGet.Config` maps Godot packages there
and other packages to nuget.org. Keep the explicit SDK imports in `Ciallo.csproj`;
use a custom editor that preserves them during project upgrades.

## Local engine development

Build the engine's managed assemblies with `--local-development` (the Windows
`Build: Windows Debug Gen Glue` task includes this), producing
`GodotSharp/Tools/LocalDevelopment/Sdk` and its matching APIs and generator.

```sh
./engine.sh local on /path/to/compiled/editor
dotnet build Ciallo/Ciallo.sln
./engine.sh local off          # Return to the pinned release; retain saved path
./engine.sh local on           # Reuse the saved local editor
```

Selection lives in `.ciallo/config`; `.ciallo/local-engine.props` supplies direct
SDK imports without changing tracked files. Builds use the current local engine
output. Reload the C# language service if it retains old assemblies.

`sync --ci` selects the published engine and removes local imports without changing
the saved preference. Run `sync` afterward to restore local mode in a reused checkout.

## Fennara

`setup`, `sync`, `open`, and Git hooks restore the locked Fennara version before
Godot loads its autoload helper. Use only the project commands:

```sh
./engine.sh fennara sync             # Restore the lock; never discover upgrades
./engine.sh fennara status           # Check addon, runtime, and MCP config presence
./engine.sh fennara update           # Select the latest stable release
./engine.sh fennara update 0.4.3      # Select an exact version, including downgrades
```

Close Fennara-enabled editors and runtime sessions before changing versions. Review
and commit the updated lock; reopen Godot and reconnect MCP clients afterward.
Restoring an older lock and running `sync` also rolls back. Failed activation rolls
back automatically; rerun `sync` after an interrupted installation. If files remain
locked, close the process holding them and retry. Old loaded executables may defer
backup cleanup until a later sync.

**Do not use upstream installers, `fennara install/update`, or editor setup/update
buttons.** The project installer checks release hashes, excludes `addons/fennara/ai/`,
and preserves project `AGENTS.md` files. Upstream MCP tool descriptions remain unchanged.
Unsupported release formats require an installer update.

The ignored addon lives in `Ciallo/addons/fennara/`. Shared runtimes use
`%LOCALAPPDATA%/Fennara` on Windows, `~/Library/Application Support/Fennara` on macOS,
and `${XDG_DATA_HOME:-~/.local/share}/fennara` on Linux (including CEF).
Only one shared runtime version can be active across projects. Installation refuses
changes while editors are connected and may stop an idle daemon.

Development sync writes the managed `mcp_servers.Fennara` block in ignored
`.codex/config.toml`, preserving other settings and binding `--project-path` to
this checkout's `Ciallo/`. Remove any unmanaged Fennara block before setup takes
ownership. Open a new trusted project session to load the config; verify
`fennara_status` reports `bound`, the correct project, and a connected editor.

`sync --ci` installs only the addon, without shared runtimes or MCP configuration.
Live MCP checks require development sync. Exports must exclude the plugin and its
helper; keep the editor export guards loaded.

### Known issues

- **Windows application-data redirection:** a packaged app can redirect writes to
  its own `LocalCache/Local/Fennara` while logs still show `%LOCALAPPDATA%/Fennara`.
  This directory may contain the installed executables, not just disposable caches.
  Run initial sync and status checks from a standalone terminal. For a redirected
  installation, close editors/MCP clients and stop the idle daemon, then move it
  to the standard user directory from outside the packaged app and rerun `sync`.
  Inspect both directories first if the standard destination already exists.
- **Fennara 0.4.3 shader diagnostics:** project scans can reject valid `#define` and
  `#include` directives in `FillHint.gdshader` and `PolygonFill.gdshader`. Both render
  with the pinned engine's D3D12 backend. Verify reports through Godot resource
  loading and rendering before changing shaders; C# compilation alone is insufficient.

## Publishing a dependency

1. Publish the engine repository's `Ciallo Godot Release` workflow artifacts first.
2. Update `Ciallo/global.json` to the published SDK version.
3. Run `./engine.sh local off --templates` and pass the Ciallo export checks before merging.

For gdvm/jq updates, change their versions, platform URLs, and SHA-256 values in
`dependencies.lock`. FFmpeg updates follow its [bundling guide](../Ciallo/ExternalData/ffmpeg/README.md).

## Verification

```sh
pwsh -File tools/engine/test.ps1
pwsh -File tools/engine/test-fennara.ps1
```

CI runs these on Windows, Linux, and macOS. They exercise engine selection, builds,
templates, Fennara upgrades/rollback, interrupted activation, checksums, and instruction
preservation using isolated loopback fixtures. CI also restores the locked upstream addon.

Fixture overrides: `CIALLO_GDVM_REGISTRY`, `CIALLO_FENNARA_RELEASES_URL`,
`CIALLO_FENNARA_DAEMON_URL`, and `CIALLO_FENNARA_HOME`. The last changes only the
project installer's destination; it does not redirect the Godot plugin.
