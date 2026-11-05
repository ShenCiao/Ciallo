# Ciallo Contributing Guide

## Basic setup

### How to build

Use the .NET 10 SDK and Git with LFS already configured. The custom editor supports
Windows/Linux x86_64 and macOS arm64. Run these commands in **standalone Git Bash on Windows** or in a terminal on macOS/Linux.
Clone the `dev` branch; use your fork's URL if contributing through a fork:

```sh
git clone --branch dev --recurse-submodules https://github.com/ShenCiao/Ciallo.git Ciallo
cd Ciallo
./engine.sh setup
dotnet build Ciallo/Ciallo.csproj
```

Setup installs the required editor and dependencies and creates an `Open Ciallo` shortcut.
Open that shortcut (or run `./engine.sh open`) to launch the editor, then press **F5** to run Ciallo.

Enable the "Embedded game size stretches..." option in the game run window.

![Enable stretching in the embedded game window](EnableStretch.png)

FFmpeg is optional for development. Run `./engine.sh ffmpeg` when using PNG export or running its integration tests.
See [Bundled FFmpeg](../Ciallo/ExternalData/ffmpeg/README.md) for details.

For setup issues, local engine builds, or Fennara configuration, see [Engine setup](../docs/engine-setup.md).

### How to export locally

See [Local exports](../docs/releases.md#local-exports) to build an application package.

## Making a contribution

Ciallo uses `dev` for daily integration and `main` for formal releases.

1. Create a feature branch from `dev` using `<your-name>/<feature-topic>`, for example `alice/brush-preview`.
2. Try the changed behavior in Ciallo and run the relevant [tests](#testing).
3. Open a PR targeting `dev`. Describe what changed and how you checked it.

Urgent fixes to the latest stable release follow the [hotfix procedure](../docs/releases.md#hotfix-releases).

## Code architecture and third-party libraries

Please contact me if you have suggestions for improvement.

### Godot 2D

Ciallo uses many Godot 2D features, especially GUI control nodes.
Experience with 2D game development is helpful, and skills you learn from Ciallo can also be applied to your future game development.

You can find UI scenes and custom GUI nodes in the `Ciallo/GuiControl` or `Ciallo/Widget` folders.

### Component pattern and Frent library

Ciallo heavily uses the [Frent](https://github.com/itsBuggingMe/Frent) library to implement the component pattern.
The [component pattern tutorial](https://gameprogrammingpatterns.com/component.html)
and the first page of the [Frent documentation](https://itsbuggingme.github.io/Frent/docs/ecf.html) are useful starting points.

For editable objects like strokes and layers, we create an entity for each object and add the necessary components.
For example, a stroke has `SampledPolyline` and `StrokeSetting` components, and a shape layer has a `ShapeLayerSetting` component.
See [NewStrokeCmd](../Ciallo/Command/NewStrokeCmd.cs) and [NewShapeLayerCmd](../Ciallo/Command/NewShapeLayerCmd.cs).

Also see the `AppDocumentManager` class. Each user document is stored and managed by a `World` object.
Each `World` has an entity that stores "document-level singletons":
`DocumentSetting` for canvas settings, `LayerTreeNode` for the layer tree root, and `CommandManager` for the undo/redo stack.

These should exist once per document, so I call them "document-level singletons" and name the entity `Document`.
You can find code like `Document.Get<LayerTreeNode>()` to access the document's layer tree root.

<details>
<summary>Why use an ECS library?</summary>

We need a library that can do two things:

- Generate unique ids.
- Manage object lifetimes through those ids.

An ECS library fits these needs. Ciallo mainly uses Frent's entities and components to organize editable objects.

</details>

### Two-way binding and R3 library

Ciallo uses [R3](https://github.com/Cysharp/R3)'s `ReactiveProperty` to implement two-way binding between data and UI.
For example, `colorButton.BindColor(color)` binds a `ReactiveProperty<Color>` to a color picker, similar to WPF's XAML binding.
See [BindColorPicker](../Ciallo/GuiBinding/BindColorPicker.cs).

You don't have to learn much reactive programming to start. Understanding `ReactiveProperty` and two-way binding covers most of the R3 usage in Ciallo.

### Data serialization and DuckDB

Project data is stored in a DuckDB-backed `.ciallo` file.
The mechanism is combined with the component system:

- Entities tagged with `ToSerializeTag` are persisted. The document entity is always persisted as id `0`, even without the tag.
- Component classes attributed with `[ToSerialize]` are mapped to DuckDB tables. Members marked with `[ProjectField]` are mapped to table columns.

See `Ciallo/Data/ProjectFormat` for the storage implementation and [Document durability](../docs/document-durability.md) for recovery and cloud behavior.

For more detail on individual systems, see and ask the AI-generated [DeepWiki](https://deepwiki.com/ShenCiao/Ciallo).

## Testing

After building and opening the project in the editor once, run the regression suites from the repository root:

```sh
dotnet test Ciallo/Ciallo.csproj --no-build
```

For selecting a suite or configuring the test editor, see [Testing](../docs/testing.md).

## Sharing builds

If you've modified Ciallo and want to share a runnable version with others, use GitHub Actions (CI) to build packages.

1. Push your changes to a branch on GitHub and open a PR targeting `dev`; a draft PR is fine. This starts the build automatically.
2. From the PR's **Checks** tab, open the **Ciallo Build** workflow run and wait for it to succeed.
3. On the run's **Summary** page, download the package from **Artifacts** or send the pr to the people who will try your changes. Download it within two days, before it expires.

For official publishing, see the maintainer [Release guide](../docs/releases.md).
