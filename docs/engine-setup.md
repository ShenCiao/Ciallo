# Engine management

`Ciallo/global.json` is the authoritative published editor and Godot SDK version. See the
[Contributing Guide](../.github/CONTRIBUTING.md#how-to-build) for initial setup.

`./engine.sh setup` installs pinned gdvm and jq releases into `.ciallo/tools`,
checks their hashes against `tools/engine/tools.lock`, and enables the Git hooks.
Developers need Git, .NET 10, and a shell with curl; Windows uses Git Bash.

## Published engines and C#

Setup generates `.ciallo/gdvm/gdvm.toml` from `Ciallo/global.json` and uses gdvm to install
that exact custom C# build. Each engine Release contains its own gdvm registry
files (`registry.json`, `index.json`, `release.json`) and three platform bundles.
Each editor bundle includes the editor, GodotSharp, four NuGet packages, and a
small `export-templates.json` download manifest. Matching export templates are
separate platform archives. `tools/engine/distribution.json` selects the GitHub repository.

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

The setup tools are pinned in `tools/engine/tools.lock`. To update one, replace its
version, platform asset names, and SHA-256 values from the upstream release.

The provisioning integration check is `pwsh -File tools/engine/test.ps1`; CI runs it
on Windows, Linux, and macOS. It uses the real gdvm binary, a temporary loopback
registry, and the product's SDK imports. It checks published restore, direct local
builds and rebuilds, mode switching, and preservation of tracked configuration.
`CIALLO_GDVM_REGISTRY` overrides the release registry URL for such testing.
