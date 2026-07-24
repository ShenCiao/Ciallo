# Roadmap

# Ciallo Contributing Guide

## Introduction

This guide is not yet complete.
The current version is a note for AI to read, but it aims to be a comprehensive guide for humans interested in developing Ciallo.

After getting a basic idea on Ciallo's code architecture here, you can check AI wiki [deepwiki](https://deepwiki.com/ShenCiao/Ciallo) to get in-depth details on how each system is implemented.

## Basic setup

### How to build

Ciallo is built on Godot. Building the core part of Ciallo is similar to building a standard Godot C# project:

- Install Git LFS, the .NET 10 SDK, and the latest release of [our custom Godot editor](https://github.com/ShenCiao/godot/releases). You can follow a [video guide](https://www.youtube.com/watch?v=7nExKQn1CAw).
- The provided editor supports Windows and Linux x86_64, and macOS arm64.
- Restore and build from the repository root:

```sh
git lfs install
git lfs pull
dotnet restore Ciallo/Ciallo.csproj --configfile NuGet.Config
dotnet build Ciallo/Ciallo.csproj --no-restore
```

- Open `Ciallo/project.godot` with the custom Godot editor, then run the project.
  - If Godot was opened before the first build, temporary `script ... is not compiling` autoload errors are expected. Autoload errors remaining after a successful build are real errors.
- Enable the "Embedded game size stretches..." option in the game run window.

![](/.github/EnableStretch.png)

### No need to build the Godot editor

You do not need to build the Godot editor from source. Use the provided custom editor release instead. (And wish you would never be tortured by C++.)

### How to export

Local development only needs the editor. To export packages, also install the export templates from the latest custom Godot release, then use the presets in `Ciallo/export_presets.cfg`.

Official Windows, Linux, and macOS packages are built by CI. Signing and notarization are only configured there, so local exports are development builds.

## Code architecture and third-party libraries

Designing professional-level software architectures often takes decades of experience, so my implementations may seem noob trying hard.
Please contact me if you have suggestions for improvement.

### Godot 2D

Ciallo uses many Godot 2D features, especially GUI control nodes.
So every piece of experience you have in 2D game development is helpful, and skills you learn from Ciallo can also be applied to your future game development.

You can find all the ui scenes and custom gui nodes in the `GuiControl` or `Widget` folders.

### Component pattern and Frent library

Ciallo heavily uses the [frent](https://github.com/itsBuggingMe/Frent) library for realizing component pattern in almost every piece of code.
Make sure you understand the component pattern theory [(tutorial)](https://gameprogrammingpatterns.com/component.html),
and the first page of the frent library [documentation](https://itsbuggingme.github.io/Frent/docs/ecf.html).

I assume you already know about Entity and Component. In Ciallo, for those editable objects like strokes, layers, we create an entity for each object and add necessary components to the entity.
e.g. add `PolylineGeometry` compoent and `StrokeSetting` compoent to a stroke entity, `PolylineLayerSetting` component to a Polyline layer entity. You can find examples in `Ciallo/Command/New*Cmd.cs` files.

Also see the `AppDocumentManager` class. Each user document is stored and managed by a `World` object.
Each `World` object creates an entity that stores "document-level singletons" data.
e.g. `DocumentSetting` for canvas settings, `LayerTreeNode` for layer node, `CommandManager` for undo redo stack, etc.

These data should be one per document, so I call them "document-level singletons" and name the entity as `Document`.
You can find code like `Document.Get<LayerTreeNode>()` to visit the document's layer tree root.

<details>
<summary>Why using an ECS library?</summary>

When I developed my research project, I found Inkscape and Krita both use an integer id value to manage editable objects.
So to imitate them, I tried to find a 3rd party library can do the two things:

- Generate unique ids.
- Manage objects lifecycle with these id values.

An ECS library is a very nice fit after ignoring the "s" part (cache-friendly system coding).
In fact, if you search C# libraries supporting component pattern, those ECS libraries value too much in cache-friendly performance, which is unnecessary to Ciallo.
Frent is the only C# ECS library that values more about code architecture design rather than performance.

I started using EnTT for my C++ project (undoubtedly overdesigned in that project).
Later I have learned more about software architecture and realised probably I happened to make a nice choice.
Check [this](https://youtu.be/wo84LFzx5nI?si=YPJa9tF5mult5ulA&t=3987) lecture OOP programming history which mentions Sketchpad.
Ciallo as a descendant of Sketchpad may has the same sense of good code design.

</details>

### Two-way binding and R3 library

Ciallo heavily uses [R3](https://github.com/Cysharp/R3) library's `ReactiveProperty` to replace traditional reflection and implement two-way binding between data and UI.
You can find code like `colorButton.BindColor(ReactiveProperty<Color> color)` in UI code to intimate WPF's xaml binding behavior.

R3's document is not written for beginners. I put a lot of effort only to take a very basic grasp. Luckily, you don't have to learn too much about R3/ReactiveProgramming to start.
Just google for what is ReactiveProperty, two-way binding, or MVVM pattern.
Then you understand most of the R3 usage in Ciallo.

If you have to understand how I handle dragging mouse input with R3 (reactive programming) in the Layers panel, here is my learning path:

### Data serialization and DuckDB

Project data is stored in a DuckDB-backed `.ciallo` file.
The mechanism is combined with the component system:

- When an entity is tagged with `ToSerializeTag`, it will be persisted to `.ciallo` files, indexed by an id.
- The document entity is always persisted as id `0`.
- Component classes attributed with `[ToSerialize]` are mapped to DuckDB tables. Members marked with `[ProjectField]` are mapped to table columns.
- Scalar values use DuckDB scalar types, and creative value types such as `Color`, `Vector2`, `Transform2D`, and Bezier data use DuckDB `STRUCT` columns. Structured and scalar arrays use DuckDB array columns, and entity maps use `MAP(INTEGER, INTEGER)`.
- Binary media such as textures and images stays in `BLOB` columns.
- An entity without `ToSerializeTag` but with `[ToSerialize]` components won't be persisted, including the entity itself and its components, unless it is the document entity.


## Release

Ciallo uses `dev` for daily integration and `main` for formal releases.
Create feature branches from `dev` using `<your-name>/<feature-topic>`, for example `alice/brush-preview`.

Publishing is done by running a workflow from the Actions page.

### Feature test builds

Use a feature test build when a branch needs downloadable builds for testers before
merging to `dev`. It produces short-lived GitHub Actions artifacts only — no GitHub
Release, no Steam upload.

Run the **Ciallo Feature Test** workflow from the Actions page and pick the branch in
the "Run workflow" ref dropdown. The workflow locks that branch's current commit SHA,
exports all three platforms, and retains the Actions artifacts for two days.

### Development builds

Run the **Ciallo Development Build** workflow from the Actions page. It locks the current
tip of `origin/dev`, exports all three platforms, and sets the build live on Steam's
**development** branch. Development builds do not create tags or GitHub Releases.

### Final releases (managed by owner)

Run the **Ciallo Publish Release** workflow from the Actions page. It locks the current
tip of `origin/main`, exports and verifies all three platforms, then pauses for a required
reviewer in the `production` environment. Approval creates the immutable
`v<config/version>` tag on the verified commit and publishes the artifacts already built
in that run.

Final builds create normal GitHub Releases and are uploaded to Steam's **default** branch,
but are NOT set live automatically — a project owner must promote the build by hand in
Steamworks App Admin > Builds. A failed publish may reuse a tag only when it still points
to the same verified commit. If the source changes, increase `config/version`; release
tags are never moved.

### Hotfix releases

Hotfix releases aim to publish bug fix within 5-10min. 
Use a hotfix when the latest stable release needs an urgent public fix. 
Hotfixes are temporary prereleases under the already published stable version:

```text
v0.1.0
v0.1.0-hotfix.1
v0.1.0-hotfix.2
v0.1.1
```

Hotfixes are allowed only for the latest stable GitHub Release.

Create hotfix branches from `main` using the `hotfix/` prefix:

```bash
git checkout main
git pull --ff-only
git checkout -b hotfix/fix-export-crash
```

Open a PR from `hotfix/fix-export-crash` to `main`.
Hotfix PRs must be squash merged.
After the PR is merged, the Ciallo Hotfix Release workflow automatically:

- exports and verifies the merged `main` commit;
- creates the next immutable `vX.Y.Z-hotfix.N` tag after the export succeeds;
- publishes the tag as a GitHub Prerelease with exported Windows, Linux, and macOS artifacts;
- creates a backport PR from `backport/hotfix-<PR>-to-dev` into `dev`.

If the automatic cherry-pick to `dev` conflicts, the workflow fails and the fix must be backported manually.
Do not manually bump `project.godot` for hotfixes; bump it for the next stable release.
