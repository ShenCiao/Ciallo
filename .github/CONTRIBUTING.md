# Roadmap
## v0.1 EA plan 
To get a little bit more than a minimum viable product, below are the features for finishing the Demo version.
Will make a release on steam and start version "v0.1 EA" after finish these features.

- [x] Document/world manager
- [ ] Serialization
  - [ ] Export to Godot
  - [ ] .Ciallo binary format
- [x] Command undo/redo system
- [ ] Property undo/redo
- [ ] Tool system
  - [x] Infrastructure
  - [ ] Brush tool
    - [x] Basic interaction
    - [ ] Basic brush engine
    - [ ] Paint stabilizer
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
- [ ] Layer system
  - [x] Add, delete
  - [x] Rename
  - [x] Reorder
  - [ ] Merge, split
  - [ ] Import image as a layer
- [ ] Stamp brush engine
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
- Feature-rich GPU brush engine that should be same level to [Krita](https://krita.org/en/) and [MyPaint](https://www.mypaint.app/en/) (need research)

# Ciallo Contributing Guide

## Introduction
This guide is not yet complete.
The current version seems like Shen's personal book note, but it aims to be a comprehensive guide for developing Ciallo.

## Basic setup
### How to build

Ciallo is built on Godot. Building the core part of Ciallo is the same as building a standard Godot C# project:

- Set up Godot 4.4.1 with .Net9. You can follow an arbitrary [video guide](https://www.youtube.com/watch?v=7nExKQn1CAw), but pay attention to the version.
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

### Godot

Ciallo basically uses every major feature of Godot to develop a 2D game.
So every piece of experience you have in 2D game development is helpful, and skills you learn from Ciallo can also be applied your future 2D game development.

### Component pattern and Arch library
Ciallo heavily uses the [Arch](https://github.com/genaray/Arch) library for implementing component pattern in almost every piece of code.
Make sure you understand the component pattern [(tutorial)](https://gameprogrammingpatterns.com/component.html),
and the Arch library [documentation](https://arch-ecs.gitbook.io/arch).
> __Note__: Reading Arch document's first three tabs: Concepts, World and Entity is enough to begin with.
> Ciallo uses Arch for writing clean code, but not for CPU-cache optimization (the "S" part of ECS).

See `WorldManager` class. Each user document is stored and managed by a `World` object.
Each `World` object creates an entity that stores "document-level singletons" data.
e.g. `DocumentSetting` for canvas settings, `LayerTreeManager` for layer data, `CommandManager` for undo redo stack, etc.
These data should be one per document, so I call them "document-level singletons" and simply name the entity as `Document`.

The current document user working on is globally accessible, so are those document-level singletons.
You can see self-explanatory code like `Document.Get<LayerTreeManager>()` to visit the document's layer tree.

> Note: There are several annoying issues have to bear with when coding:
> - `Entity.Add()` can lag Rider a lot.
> - Rider crashes much more often after using Arch (Tell me if you have the same feeling, rather than my own hallucination).
> - Remember to include both `Arch.Core` and `Arch.Core.Extensions`
>   - When adding component(s), `e.Add(Obj)` is in Arch.Core.Extensions namespace but `e.Add(Obj1, Obj2)` is in `Arch.Core` namespace.
>   - I have wasted a lot of time on finding this issue.

<details>
<summary>Why using an ECS framework?</summary>
When I developed my research project, I found Inkscape and Krita both use an integer id value to manage editable objects.
So to imitate them and reduce code to implement, I tried to find a library can do the two things:

- Generate unique ids.
- Manage objects lifecycle with these id values.

An ECS framework is a very nice fit when ignoring the "s" part (cache-friendly system coding).

I used EnTT for my C++ project (undoubtedly overdesigned in that project).
And when I started C#, I searched for a C# ECS framework similar to EnTT, but only found Arch.
There are other C# ECS frameworks, but they are either unity-specific or force users to use the "s" part.
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

R3's document is terrible. I put a lot of effort only to take a very basic grasp.
But luckily, you don't have to learn too much about R3 to start.
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
