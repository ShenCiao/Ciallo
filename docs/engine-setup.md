# Engine management

`Ciallo/global.json` is the authoritative published editor and Godot SDK version. See the
[Contributing Guide](../.github/CONTRIBUTING.md#how-to-build) for initial setup.

`./engine.sh setup` installs pinned gdvm and jq releases into `.ciallo/tools`,
checks their hashes against `tools/engine/dependencies.lock`, prepares Fennara, and enables the Git hooks.
Developers need Git, .NET 10, and a shell with curl; Windows uses Git Bash.
FFmpeg is optional for normal builds and development runs. PNG export and application
packaging use the separate `./engine.sh ffmpeg` command; see
[Bundled FFmpeg](../Ciallo/ExternalData/ffmpeg/README.md) for requirements and setup.

## Published engines and C#

Setup generates `.ciallo/gdvm/gdvm.toml` from `Ciallo/global.json` and uses gdvm to install
that exact custom C# build. Each engine Release contains its own gdvm registry
files (`registry.json`, `index.json`, `release.json`) and three platform bundles.
Each editor bundle includes the editor, GodotSharp, four NuGet packages, and a
small `export-templates.json` download manifest. Matching export templates are
separate platform archives. The `godot.repository` entry in `tools/engine/dependencies.lock`
selects the GitHub repository; `Ciallo/global.json` remains the only Godot version pin.

gdvm handles downloads, SHA-512 verification, extraction, and version storage in
`~/.gdvm`. Its custom-registry trust prompt is accepted by the project setup command
for the configured Ciallo release source. The four bundled Godot packages are copied
to `.ciallo/nuget`; the tracked `NuGet.Config` maps them to that local source.
The project's explicit SDK imports resolve `Godot.NET.Sdk` using `Ciallo/global.json` and NuGet.
Other dependencies restore from nuget.org.

Git hooks prepare the required version after checkout, merge, rebase, or commits
that change engine requirements. For a manual version edit or interrupted setup:

```sh
./engine.sh sync
./engine.sh status
```

Open the generated `Open Ciallo` shortcut or run `./engine.sh open`. The entry
prepares the selected SDK before launching the editor with the Ciallo project.
Reload the C# project in an already-open IDE after changing engine modes. Hooks complete after Git
has changed the checkout; resolve any reported setup failure before building.

## Local engine development

Build the engine and its managed assemblies using the Godot repository's VS Code
`Build: Windows Debug Gen Glue` task. Its `Build .NET Assemblies` step uses
`--local-development` to emit `GodotSharp/Tools/LocalDevelopment/Sdk`, the API
assemblies, and the source generator. Enable that editor in Ciallo:

```sh
./engine.sh local on /path/to/compiled/editor
dotnet build Ciallo/Ciallo.sln
```

The explicit `engine.local` switch and editor path persist in the ignored
`.ciallo/config`. `engine.sh` writes `.ciallo/local-engine.props` with the SDK path.
`Ciallo.csproj` reads it before importing `Sdk.props`, then loads the local SDK's
props and targets and its matching API and generator directly from the editor output.
The SDK's version is diagnostic metadata; its fixed directory selects the build.

Regular `dotnet build`, Godot builds, and VS Code build tasks use the same project
imports. After rebuilding the engine's managed assemblies in place, the next build
reads the updated files. Reload the C# language service if it retains loaded API or
generator assemblies. Switching modes and rebuilding keep tracked configuration intact.

Use the saved path again or return to the team release:

```sh
./engine.sh local off
./engine.sh local on
./engine.sh status
```

Turning local mode off retains the editor path and removes the generated local import.
`sync`, `open`, and Git hooks honor the switch. CI runs `sync --ci`, which prepares
the published SDK and removes local imports while preserving the saved preference.
Run `sync` afterward to restore that preference in a reused local checkout.

Editors must preserve explicit Godot SDK imports during project upgrades. Use a
release containing that support, or the local editor built with this implementation.

## Templates and cleanup

Normal setup, sync, and open download only the editor bundle. To prepare exports:

```sh
./engine.sh sync --templates
```

This downloads the matching template archive for the current platform, verifies
its SHA-512 checksum and version, and installs it into Godot's versioned user
template directory. Template extraction requires `unzip` (included with Git Bash
on Windows). A matching completed installation is reused without downloading it
again. Local engine mode uses templates built and configured by the engine developer.
The shared CI setup action also defaults to editor-only setup; export jobs pass
`templates: 'true'`.

Setup and sync finish with `gdvm prune`; `./engine.sh clean` runs it explicitly.
Unused installations and archives
become eligible after 30 days by default. gdvm preserves its default installation
and tracked live links. Run cleanup while editors are closed. Tools and project
configuration under `.ciallo` are ignored by Git; engine storage is shared by gdvm.

## Publishing a dependency

1. Run the engine repository's `Ciallo Godot Release` workflow. It publishes
   `v<SDK version>` with editor bundles, separate export template archives, and
   the gdvm registry files.
2. Update `Ciallo/global.json` with that version in the Ciallo feature change.
3. Run `./engine.sh local off --templates` and require the Ciallo export checks
   before merging. The Release must be available before the dependency is adopted.

The Git-config-format `tools/engine/dependencies.lock` pins gdvm, jq, Fennara, and
each platform's FFmpeg build. It also selects the Godot release repository. Git reads
this file before jq is available. To update gdvm or jq, replace its version, platform
download URLs, and SHA-256 values from the upstream release. FFmpeg's update procedure
is in its [bundling guide](../Ciallo/ExternalData/ffmpeg/README.md).

The provisioning integration check is `pwsh -File tools/engine/test.ps1`; CI runs it
on Windows, Linux, and macOS. It uses the real gdvm binary, a temporary loopback
registry, and the product's SDK imports. It checks published restore, direct local
builds and rebuilds, mode switching, and preservation of tracked configuration.
`CIALLO_GDVM_REGISTRY` overrides the release registry URL for such testing.

## Fennara

`setup`, `sync`, `open`, and the Git hooks prepare the Fennara version selected by
`dependencies.lock`. The plugin is restored before Godot starts because `project.godot`
references its runtime helper. To prepare only Fennara or inspect the local installation:

```sh
./engine.sh fennara sync
./engine.sh fennara status
```

Close all Fennara-enabled Godot editors and finish runtime sessions before changing
versions. Upgrade to the latest published stable release, or choose an exact version:

```sh
./engine.sh fennara update
./engine.sh fennara update 0.4.3
```

Review and commit the resulting `dependencies.lock` change. Other checkouts restore
that version on their next sync. To revert, use an older exact version with `update`,
or restore the earlier lock file and run `sync`. Reopen Godot and reconnect the MCP
client after an upgrade or rollback; an existing MCP process retains its loaded runtime.

**Do not use the official installation scripts, `fennara install`, `fennara update`,
or the Godot dock's setup/update buttons.** Ciallo installs official release payloads
directly. It verifies the locked release manifest and archive SHA-256 hashes, excludes
`addons/fennara/ai/`, and never creates or rewrites project `AGENTS.md` files. Replacing
the addon removes old packaged guidance. The official entry points do not follow this
policy. Instructions embedded in the upstream MCP tool descriptions are unchanged.

The manifest URL and SHA-256 are locked alongside the Fennara version. Its verified
contents supply platform-specific archive hashes; manifests and downloads are cached
under `.ciallo/fennara/`. `sync` never discovers a newer release. `update` resolves one
stable release, checks its manifest format and the three supported platform entries,
and updates the lock only as part of successful installation. Only the host platform
is installed. Unknown installation formats fail until the project installer is updated.

The addon stays in ignored `Ciallo/addons/fennara/`. Matching CLI, daemon, MCP launchers,
and versioned runtimes use Fennara's standard per-user data directory: `%LOCALAPPDATA%/Fennara`
on Windows, `~/Library/Application Support/Fennara` on macOS, and
`${XDG_DATA_HOME:-~/.local/share}/fennara` on Linux. Linux also installs the release's CEF
runtime. Fennara shares one active runtime version across projects; incompatible versions
cannot run together. Setup checks the authenticated daemon before changing files and
refuses when editors are connected. It stops an idle daemon, but never kills editors.
Locked files cause installation to fail; close the process using them and retry.

Downloads and extraction finish before activation. Replacements preserve the previous
files until activation completes. A failed switch rolls them back; a journal permits
the next sync to recover an interrupted switch. Simultaneous project-script installations
are serialized by checkout and per-user locks. Cached downloads and older version folders
are retained for reuse. Windows may keep an old executable loaded after a successful
switch; its backup cleanup is deferred until a later sync after the MCP client reconnects.

Development sync generates an ignored `.codex/config.toml` block named
`mcp_servers.Fennara`, using the local launcher path and an explicit `--project-path`
to this checkout's `Ciallo/` directory. Other config entries are preserved. An existing
unmanaged Fennara block in that file must be removed before setup can own it. Open a
new trusted project session so the client loads the configuration. Other MCP clients
can use the same launcher and project-path argument. Verify `fennara_status` reports
`bound`, the expected project directory, and a connected editor.

`sync --ci` restores only the project addon, without installing a shared runtime or
writing MCP client configuration. This lets Godot resolve the autoload and load the
plugin's export guard. CI that needs live Fennara tools must use a development sync.
Exports must exclude Fennara's plugin and runtime helper; the upstream export guard
handles this while the editor plugin is loaded.

The Fennara integration check is `pwsh -File tools/engine/test-fennara.ps1`.
Its loopback release fixtures cover upgrades, rollback, interrupted activation, rejected
payloads, and preservation of project instructions. `CIALLO_FENNARA_HOME` redirects
only the project installer for isolated tests; it does not redirect the Godot plugin.
`CIALLO_FENNARA_RELEASES_URL` supplies a fixture GitHub release API and
`CIALLO_FENNARA_DAEMON_URL` isolates tests from the user's real loopback daemon.
