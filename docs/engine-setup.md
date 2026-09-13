# Engine management

The root `global.json` is the authoritative Godot SDK version. See the
[Contributing Guide](../.github/CONTRIBUTING.md#how-to-build) for initial setup.

`./engine.sh setup` installs pinned gdvm and jq releases into `.ciallo/tools`,
checks their hashes against `tools/engine/tools.lock`, and enables the Git hooks.
Developers need Git, .NET 10, and a shell with curl; Windows uses Git Bash.

## Published engines and C#

Setup generates `.ciallo/gdvm/gdvm.toml` from `global.json` and uses gdvm to install
that exact custom C# build. Each engine Release contains its own gdvm registry
files (`registry.json`, `index.json`, `release.json`) and three platform bundles.
Each editor bundle includes the editor, GodotSharp, four NuGet packages, and a
small `export-templates.json` download manifest. Matching export templates are
separate platform archives. `tools/engine/distribution.json` selects the GitHub repository.

gdvm handles downloads, SHA-512 verification, extraction, and version storage in
`~/.gdvm`. Its custom-registry trust prompt is accepted by the project setup command
for the configured Ciallo release source. The four bundled Godot packages are copied
to `.ciallo/nuget`; the tracked `NuGet.Config` maps them to that local source.
MSBuild resolves `<Project Sdk="Godot.NET.Sdk">` using `global.json` and standard NuGet.
Other dependencies restore from nuget.org.

Git hooks prepare the required version after checkout, merge, rebase, or commits
that change engine requirements. For a manual version edit or interrupted setup:

```sh
./engine.sh sync
./engine.sh status
```

Open the generated `Open Ciallo` shortcut or run `./engine.sh open`. The entry
prepares C# packages before launching the selected editor with the Ciallo project.
Reload an already-open IDE after changing SDK versions. Hooks complete after Git
has changed the checkout; resolve any reported setup failure before building.

## Local engine development

Build the engine and its managed assemblies using the Godot repository's VS Code
`Build: Windows Debug Gen Glue` task, then select the compiled editor:

```sh
./engine.sh local /path/to/compiled/editor
```

The selected path persists in `.ciallo/config`. After rebuilding managed assemblies,
run `./engine.sh sync`. An ignored `Ciallo/global.json` selects the local SDK version;
the team's root version stays pinned. Builds with `--local-development=ciallo` also
provide direct API DLL and generator references through `.godot/mono/local_sdk.props`.
Changed local packages invalidate only their matching four NuGet cache entries.
Use `NUGET_PACKAGES` consistently when choosing a custom NuGet cache directory.

Return to the root version with `./engine.sh published`. CI always uses the root
version and passes `--ci` to ignore a local editor selection.

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
2. Update `global.json` with that version in the Ciallo feature change.
3. Run `./engine.sh published --templates` and require the Ciallo export checks
   before merging. The Release must be available before the dependency is adopted.

The setup tools are pinned in `tools/engine/tools.lock`. To update one, replace its
version, platform asset names, and SHA-256 values from the upstream release.

The provisioning integration check is `pwsh -File tools/engine/test.ps1`; CI runs it
on Windows, Linux, and macOS. It uses the real gdvm binary and a temporary loopback
registry. `CIALLO_GDVM_REGISTRY` overrides the release registry URL for such testing.
