# Roadmap
## v0.1 EA plan 
To get a little bit more than a minimum viable product, below are the features for finishing the Demo version.
Will make a release on steam and start version "v0.1 EA" after finish these features.

- [x] Document/world manager
- [x] Export to Godot
- [x] Export to raster image
- [x] .Ciallo project file
- [x] Command undo/redo system
- [ ] Property undo/redo
- [x] Tool system infrastructure
- [x] Brush tool
  - [ ] More brushes
  - [ ] More brush parameters
  - [x] Paint stabilizer
  - [ ] Resize brush interactor
- [ ] Paint fill tool
- [ ] Vector fill tool
  - [x] CGAL C++ code
  - [ ] Integration
- [ ] Selection/move tool
  - [ ] Line binding system (Bézier curve only)
    - [x] Bézier curve geometry
  - [ ] Design
  - [x] Polyline overlay rendering
- [ ] Basic lasso tool
- [x] Layer system
  - [x] Add, delete
  - [x] Rename
  - [x] Reorder
  - [ ] Merge, split
- [ ] Import image as a layer
- [ ] Localization
  - [x] Infrastructure (ai translation)
  - [ ] Complete


## v1.0 plan
Ciallo is largely inspired by Blender Grease Pencil (GP) 3D stroke.
Before release v1.0, Ciallo will have 2D copies of every GP's major features.

Beside GP, here is a rough unique feature list:

- Animation system similar to Clip Studio Paint (CSP)
  - Vector fill integration in depth
- Lasso tool identical to Photoshop or CSP's for raster image.
- Polygon gaps detection system (built with 2D game navigation system)  
- Anime style lighting system integrated with Godot's 2D light (need research)
- Feature-rich GPU brush engine near to [Krita](https://krita.org/en/) and [MyPaint](https://www.mypaint.app/en/) (need research)

# Ciallo Contributing Guide

## Introduction
This guide is not yet complete.
The current version seems like Shen's personal book note, but it aims to be a comprehensive guide for developing Ciallo.

## Basic setup
### How to build

Ciallo is built on Godot. Building the core part of Ciallo is the same as building a standard Godot C# project:

- Set up Godot 4.5 with .Net9. You can follow an arbitrary [video guide](https://www.youtube.com/watch?v=7nExKQn1CAw), but pay attention to the version.
- Open the `Ciallo/project.godot` file with your Godot editor, then build and run.
- Enable the "Embedded game size stretches..." option in the game run window.
  
![](/.github/EnableStretch.png)

### IDE
In theory, you can use any IDE supporting C#. Follow the Godot [guide](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html#configuring-an-external-editor) to link the Godot editor with your IDE.

However, I suggest using JetBrains [Rider](https://www.jetbrains.com/rider/), which is free since 2024 and offers comprehensive productivity support for Godot scripting.

I'm pretty satisfied with Rider, but also interested in learning if Rider is the best choice.
So if you have solid experience in VS Code or Visual Studio to script Godot C#. Contact if you would rather use one of them.

## Code architecture and third-party libraries
Designing professional-grade software architectures often takes decades of experience, so my implementations may seem noob trying hard.
Please contact me if you have recommendations for improvement.

### Godot 2D features

Ciallo will use the vast majority of Godot feature for developing a 2D game, and heavily use nearly all types of GUI control node.
So every piece of experience you have in 2D game development is helpful, and skills you learn from Ciallo can also be applied your future game development.

### Component pattern and Massive ECS library
Ciallo heavily uses the [massive-ecs](https://github.com/nilpunch/massive-ecs) library for realizing component pattern in almost every piece of code.
Make sure you understand the component pattern thoery [(tutorial)](https://gameprogrammingpatterns.com/component.html),
and the Massive library [documentation](https://github.com/nilpunch/massive-ecs/wiki/Entity-Component-System).
> __Note__:
> Ciallo uses Arch for writing clean code, but not for CPU-cache optimization (the "S" part of ECS).

See the `AppWorldManager` class. Each user document is stored and managed by a `World` object.
Each `World` object creates an entity that stores "document-level singletons" data.
e.g. `DocumentSetting` for canvas settings, `LayerTreeManager` for layer data, `CommandManager` for undo redo stack, etc.

These data should be one per document, so I call them "document-level singletons" and name the entity as `Document`.
You can find self-explanatory code like `Document.Get<LayerTreeManager>()` to visit the document's layer tree.

For those editable object like strokes, layers, we will create entities and add components such as `PolylineGeometry`, `StrokeBrush`, `LayerSetting` object to the entities.

<details>
<summary>Why using an ECS library?</summary>

When I developed my research project, I found Inkscape and Krita both use an integer id value to manage editable objects.
So to imitate them, I tried to find a 3rd party library can do the two things:

- Generate unique ids.
- Manage objects lifecycle with these id values.

An ECS library is a very nice fit after ignoring the "s" part (cache-friendly system coding).
In fact, if you search for a 3rd party library for component pattern, an ECS library is the only choice. There are no dedicate libraries for component pattern.

I used EnTT for my C++ project (undoubtedly overdesigned in that project).
And when I started C#, I searched for a C# ECS library similar to EnTT, first tried Arch then switch to Massive for sparse set.
There are other C# ECS libraries, but they are either Unity-specific or force users to write the "System" code.

</details>

<!--
<details>
<summary>Why globalize the Document entity?</summary>
Though using global variables/singletons is commonly considered a bad practice, it's necessary for Ciallo.
Ciallo is an interactive graphics program, the interaction between subsystems is necessary by business.
As the business grows, it's impossible to predefine the accessibility scope of each subsystem.
So I think this design is reasonable.
</details>
-->

### Two-way binding and R3 library
Ciallo heavily uses [R3](https://github.com/Cysharp/R3) library's `ReactiveProperty` implement two-way binding between data and UI.
You can find code like `colorButton.BindColor(ReactiveProperty<Color> color)` in UI code to intimate WPF's xaml binding behavior.

R3's document is terrible. I put a lot of effort only to take a very basic grasp. Luckily, you don't have to learn too much about R3 to start.
Just google for what is ReactiveProperty, two-way binding, or MVVM pattern.
Then you understand most of the R3 usage in Ciallo.

If you have to understand how I handle dragging mouse input with R3 (reactive programming) in the Layers panel, here is my learning path:

1. Know [reactive programming](https://gist.github.com/staltz/868e7e9bc2a7b8c1f754) concept first.
2. Then read [UniRx](https://github.com/neuecc/UniRx) to know the former version of R3.
3. Reference [ReactiveX operator document](https://reactivex.io/documentation/operators.html) to choose suitable operators.
4. Make hard guess on a very unintuitive solution (and still being buggy).

### MVP pattern
For those elements (strokes, polygons) visible on the canvas. They hold complex data not suitable for two-way binding.
Ciallo separates related code into the Data(Model), Rendering(View) and Command(Presenter).
You can find corresponding folders in the project directory.

The interaction logic between them can be explained by the [MVP pattern](https://www.geeksforgeeks.org/android/mvp-model-view-presenter-architecture-pattern-in-android-with-example/).
Create command objects to change both data and view.
As the "Command" name suggests, it also implements the undo/redo system.

## Code style
See my [instruction](../Ciallo/.github/copilot-instructions.md) to copilot.
