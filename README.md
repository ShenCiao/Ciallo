⚠️ This page and features are under construction. Navigate to the [Contributing Tab] for the development plan.

[![godot-ci export](https://github.com/ShenCiao/Ciallo/actions/workflows/godot-ci.yml/badge.svg?branch=main)](https://github.com/ShenCiao/Ciallo/actions/workflows/godot-ci.yml)

Want to know how these features are (or will be) implemented?
Leave a star to this project and check my [research project], [paper](https://dl.acm.org/doi/10.1145/3641519.3657418) or [tutorial](https://shenciao.github.io/brush-rendering-tutorial/Introduction/).

---
# Ciallo ～(∠・ω< )⌒★!

Ciallo is an open-source graphics program for professional digital painting.
It aims to compete with traditional raster painting software like Photoshop and Clip Studio Paint, while offering the following unique features:

## Unique features
### Vectorized Photoshop-like brushes

Resolution-independent, Photoshop-like brushes: The brushes are entirely drawn (rendered) on your graphics card GPU. This technique is researched by the developer ([Shen Ciao]).

### Vector fill

Bucket fill in vector form: The positions to fill are tracked with "fill markers", the shape $\odot$ in the image.

We can manipulate the positions, colors, or any other filling properties by editing these markers.

(Under development) We will be able to copy and paste these markers between animation frames, greatly reducing manual work.”

### Export to game engines

Export your artwork into the Godot game engine (as a .scn/.tscn file), and it keeps resolution independent.
Like those old days when Adobe Flash was alive.

Ciallo also exports the original layers as nodes to facilitate creating CG variations.

Other game engines will be supported in the future.

## Other features
The following features may be in your favor:

### No generative AI
<img align="left" width="192" height="192" src="https://upload.wikimedia.org/wikipedia/commons/f/f1/No_AI_art.svg">

Ciallo uses a custom vector format that is hard to generate by AI.

Unlike [the big company](https://www.youtube.com/watch?v=DoM3nUD-1Ro), Ciallo will never change the terms of service silently to steal your artwork stocks for training AI.
We also plan to build a sharing platform while protecting your artworks from unethical AI training.

In the invisible-far future, Ciallo may offer AI-powered features --- but always designed for professional artists.

### Line binding

### Lasso tool on vector stroke (like CSP)

### Infinite canvas

(@Shen Ciao: "I never see a professional artist needs an infinite canvas when drawing illustrators, mangas or animes.
Do you really need this feature? Tell me how to add more support to this.")

## Download (Free on all platforms)

Steam | Itch.io

System requirements (A system can run large-sized 2D games.):

- OS: Windows 10 or higher
- Memory: 6GB or more
- Graphics card: Minimum NV GTX 1650 or AMD Radeon RX 6500 XT.
- Monitor: Recommend refresh rate greater than 100fps to get the best drawing experience.

> About MacOS: I literally wish but cannot afford to buy a MacBook Pro (which cost me half a year’s living budget). Consider patreon me, really lacking of money currently.

## Development philosophy

Ciallo is in an early stage of development; its version number will be labeled as EA (early access).

During the EA stage, we mainly R&D traditional paint software features with modern shaders and GPU APIs. After finishing major paint features, we will open Steam Workshop, marking the end of the EA stage.

Overall, Ciallo will be a DCC program focusing on creating 2D game assets, including illustrations, 2D animations and hand-drawn textures in 3D.

#### Feature requests

The developer basically knows the most needed features during the EA stage. These are the YouTube channels he learns painting: [Dong Chang](https://www.youtube.com/@DongChang) | [Aaron's Painter Tutorials](https://www.youtube.com/@AaronsPainterTutorials) | [saitonaoki](https://www.youtube.com/@saitonaoki2).

If you eagerly need a feature to be deployed within a week/month, consider contacting the developer and sponsoring this project.

## Sponsor my research and your future
It's Ciallo's mission to bring your dream 2D art techniques to life.
I researched those techniques driven by the passion to Anime, and need your support to keep them free and accessible to everyone.

Moreover, in this age of AI, we all face challenges by the surging AI techniques.
Hope Ciallo will be the tool helping you shine in this era --- the tool liberating your creativity, not replacing it with AI.
Sponsor Ciallo to shape the future of your painting, and keep the creativity alive in the future AI-driven world.

## Credits
### Project name
The name "Ciallo" is a combination of the Italian "Ciao" and English "Hello", and comes from the galgames developed by [Yuzusoft](https://www.yuzu-soft.com/). We won't shame this name.

### Coding frameworks/libraries

- [Godot C#](https://godotengine.org/): [Why godot?](#tech-faq)
- [Massive-ecs](https://github.com/nilpunch/massive-ecs): Unity-like (gameobject/entity) component pattern (ECS library without need for the 'S').
- [CGAL](https://www.cgal.org/): Complex geometry operations.
- [R3](https://github.com/Cysharp/R3): Signal on steroids and reactive programming.
- [GdUnit4](https://github.com/MikeSchulze/gdUnit4): Unit test framework.
- [MessagePack](https://github.com/MessagePack-CSharp/MessagePack-CSharp): Binary serialization.
- [Newtonsoft.Json](https://www.newtonsoft.com/json): JSON serialization.
- [GodotSharp.SourceGenerators](https://github.com/Cat-Lips/GodotSharp.SourceGenerators): Godot auxiliary.
- [Stateless](https://github.com/dotnet-state-machine/stateless): Managing complex interactive states (tool system).

## Build Guide
Ciallo is built on Godot. Building the core part of Ciallo is the same as building a standard Godot C# project:

- Set up Godot 4.5 with .Net9. You can follow an arbitrary [video guide](https://www.youtube.com/watch?v=7nExKQn1CAw), but pay attention to the version.
- Open the `Ciallo/project.godot` file with your Godot editor, then build and run.

Go to [Contributing Tab](https://github.com/ShenCiao/Ciallo?tab=contributing-ov-file#how-to-build) for a more complete guide.

## Tech FAQ

Shen answers the following questions:

### Why develop on a game engine?

**TL;DR**: I cannot handle the complexity of Vulkan.

My first attempt to develop Ciallo was with raw Vulkan, [project link](https://github.com/ShenCiao/CialloVulkan), motivated by the rather naive reason: “my next-generation technology should be built on next-generation technical foundations.”

I soon realize the complexity of Vulkan: it isn’t designed for one-person projects, and even highly experienced graphics engineers can make simple yet catastrophic mistakes, see [Cherno’s Vulkan story](https://youtu.be/bUUZ1iD9_e4?si=vVCUxXU-dScgcZx5&t=1438). Only large teams can truly afford the mental burden of Vulkan.

So, I decided to sacrifice the freedom of controlling graphics in exchange for productivity. Under this idea, one of the game engines is the best choice.

You can consider Ciallo as a building/RTS game, e.g., _City Skylines_, _Warcraft III_. Players build strokes (polylines) and place color blocks (polygons) in the game world (canvas).

### Why Godot, not Unity or Unreal?

Godot has the best 2D rendering infrastructure and GUI widgets.

For example, as of June 2025, Godot is the only engine that supports rendering a filled polygon directly from a list of points (without manual tessellation).
Moreover, Godot’s `ColorPicker` control convinced me to choose it over Unity.

On the other side, Godot's convenience brings potential limitations on rendering.
I'm far away from being familiar with the low-level [`RenderingDevice`](https://docs.godotengine.org/en/stable/classes/class_renderingdevice.html#class-renderingdevice) api of Godot.
If we have to give up using Godot for whatever reason someday, we may consider using [defold](https://defold.com/) or [flax](https://flaxengine.com/).

### Why not GDScript?
Dynamically-typed languages cannot support our project (or, IMHO, any projects that need more than 5 custom types). C# is a better choice, also for its larger community.

But I do like the GDScript language itself. In my wet dream, Godot will discard GDScript for engine scripting and turn it into a high-level shading language for graphics.

> The graphics community lacks an advanced programming language for shading. Compared to C#, GDScript lacks many features, but compared to GLSL/HLSL, GDScript is already very feature-rich.

[Contributing Tab]:https://github.com/ShenCiao/Ciallo?tab=contributing-ov-file
[research project]:https://github.com/ShenCiao/CialloResearch
[Shen Ciao]:https://github.com/ShenCiao