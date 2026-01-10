<h1 align = "center">Ciallo ～(∠・ω< )⌒★!</h1>
<h3 align="center">The next-generation vector paint program in your dreams.</h3>
</br>
<div align="center">

[![Steam](https://img.shields.io/badge/steam-%23000000.svg?style=for-the-badge&logo=steam&logoColor=white)](https://store.steampowered.com/app/3973810)
[![Itch.io](https://img.shields.io/badge/Itch-%23FF0B34.svg?style=for-the-badge&logo=Itch.io&logoColor=white)](https://shenciao.itch.io/ciallo)
[![Patreon](https://img.shields.io/badge/Patreon-F96854?style=for-the-badge&logo=patreon&logoColor=white)](https://www.patreon.com/cw/ShenCiao)
[![Pixiv](https://img.shields.io/badge/pixiv-0096FA.svg?style=for-the-badge&logo=pixiv&logoColor=white)](https://www.pixiv.net/artworks/137703808)

[![License](https://img.shields.io/github/license/ShenCiao/Ciallo.svg)](https://github.com/ShenCiao/Ciallo/blob/main/LICENSE)
[![godot-ci export](https://github.com/ShenCiao/Ciallo/actions/workflows/godot-ci.yml/badge.svg?branch=main)](https://github.com/ShenCiao/Ciallo/actions/workflows/godot-ci.yml)
![.Net](https://img.shields.io/badge/.NET-512BD4.svg?style=for-the-badge&logo=dotnet&logoColor=white)

</div>

---

Ciallo is an open-source graphics program for professional digital painting.
It aims to compete with traditional raster painting software like Photoshop and Clip Studio Paint, while offering the following unique features researched by the developer:

## Unique features

### Vectorized Photoshop-like brushes

Vectorized stamp brushes, entirely drawn (rendered) on your graphics card (GPU).

![](/.github/Stamp.gif)

Resolution-independent airbrush, adjust the opacity falloff directly, with ultra‑fast rendering performance.

![](/.github/Airbrush.gif)

### Export to game engines

Export your artwork into the Godot game engine (as a .scn/.tscn file), and keep it resolution-independent.
Like those old days when Flash was alive.

![](/.github/Dango.png)

![](/.github/DangoExportation.gif)

Will support other game engines in the future.

### (WIP) Vector fill

Bucket fill in vector form: The positions to fill are editable and tracked with "fill markers", the shape $\odot$ in the image:

![](/.github/VectorFill.gif)

Copy and paste these markers between animation frames:

![](/.github/VectorFillFrames.gif)

## Other features

The following features may be in your favor:

### No generative AI

<img align="left" width="192" height="192" src="https://upload.wikimedia.org/wikipedia/commons/f/f1/No_AI_art.svg">

Ciallo uses a custom vector format that is aiming to be the game industry standard and hard to generate by the current AI techniques.
Unless being explicitly declared by the copyright owners, artworks from Ciallo are [forbidden to be trained](#user-created-content-copyright-and-ai-notice).

Unlike [the big company](https://www.youtube.com/watch?v=DoM3nUD-1Ro), Ciallo will never steal your artwork stocks for training AI.
We also plan to build a sharing platform while protecting your artworks from unethical AI training.

In the invisible-far future, Ciallo may offer AI-powered features --- but always designed for professional artists.

### Optimized pen feel

Ciallo aims to provide a drawing/pen feel significantly better than _Clip Studio Paint_ and _Procreate_; this will require several technical advancements in future updates.
For the current version, the feel is not worse than _Clip Studio Paint_ and certainly better than _Photoshop_, _GIMP_, or _MyPaint_. (Report a bug if the paint tool feels laggier than _Photoshop_ or _Krita_.)

### Infinite canvas

@Developer: "I have never seen a professional artist who needs an infinite canvas when drawing illustrators, mangas, or animes.
Do you really need this? Tell me how I can add better support.

## Download (Free on all platforms)

[Steam](https://store.steampowered.com/app/3973810) | [Itch.io](https://shenciao.itch.io/ciallo)

System requirements:
Requires a system capable of running small to mid-sized 3D games.
Close any other software that heavily uses your GPU before opening Ciallo (except OBS, if necessary).

- OS: Windows 10 or higher
- Memory: 6 GB or more
- GPU: Minimum NV GTX 1650 or AMD Radeon RX 6500 XT
- Monitor: A refresh rate greater than 100 Hz is recommended for the best drawing experience
- Tablet: Use your tablet at its maximum polling/reporting rate. (If you need to buy a new one, the fastest is Wacom DTC-141.)

> Ciallo is highly sensitive to your tablet polling rate. It can potentially respond to tablet/mouse input at up to 1000 Hz. However, tablets in 2025 can reach at most 360 Hz (DTC-141), and 150–200 Hz on average, which is highly insufficient for capturing small turning points and handwritten text. If a tablet with a 500–1000 Hz polling rate becomes available on the market, contact me — Ciallo will try to add support.

> About macOS: I literally wish to support macOS, but I cannot afford a MacBook Pro (it would cost me roughly half a year's living expenses). Consider sponsoring me on Patreon; really lacking of money.

## Development philosophy

**EA stage**

Ciallo is in an early stage of development; its version number will be labeled as EA (early access).
During the EA stage, Ciallo mainly focuses on R&D of traditional painting features using modern shaders and GPU APIs. After finishing those major painting features, we plan to open Steam Workshop, marking the end of the EA stage.

**Hand-drawn art**

Although Ciallo focuses on vector features, it is NOT intended to replace Inkscape or Illustrator for graphic design.
Instead, it aims to provide Clip Studio Paint or Krita vector layers on super steroids.
All features that were previously available only in raster workflows would be supported in Ciallo's new vector workflow.

**Game art first**

Except for the core painting, Ciallo prioritizes auxiliary features supporting video game development.
It aims to be a DCC program providing vector hand-drawn assets, including illustrations, 2D animations and hand-drawn textures in 3D.

## Feature requests

Hope Ciallo has already addressed something important to you. (To be frank, the large company should have provided these infrastructures a decade ago.) There's always more to add as Ciallo grows. Check the [Contributing Tab] for the development plan.

The developer basically knows the most needed features during the EA stage. These are the YouTube channels he learns painting: [Dong Chang](https://www.youtube.com/@DongChang) | [Aaron's Painter Tutorials](https://www.youtube.com/@AaronsPainterTutorials) | [saitonaoki](https://www.youtube.com/@saitonaoki2).

Instead of asking for features existing in other paint programs, you may propose creative features unconstrained in style.
The developer would love to hear new ideas about line art (especially anime-style art) and may be able to do technical research on your request.

If you urgently need a feature from other programs implemented in Ciallo within a week or month, consider contacting the developer or sponsoring the project.

## Sponsor Ciallo's research and your future

Ciallo's mission is to bring advanced hard-drawn art techniques from dream to life.
It needs your support to keep them free and accessible to everyone.

[![Become a Patron!](https://c5.patreon.com/external/logo/become_a_patron_button.png)](https://www.patreon.com/cw/ShenCiao)

Consider patreon on Ciallo development, the developer will post plans, designs and teasers about innovative features well before release, which acts as a pre-manual to understand how to use Ciallo.
Check the post [About Vector Fill](https://www.patreon.com/posts/about-vector-143858627) as an example.

Moreover, in this age of AI we all face challenges from rapidly advancing AI techniques.
Hope Ciallo will be the tool that helps you shine in this era — a tool that liberates your creativity, not one that replaces it with AI or steals it to feed AI.
Sponsor Ciallo to shape the future of your digital painting and help keep creativity alive in an AI-driven world.

## User-created content copyright and AI notice

For the purposes of this Project, "Artwork" means any brushes and drawings saved, exported, or represented in Ciallo's native format, other game engine vector formats, or raster format. The copyright owner of an Artwork (the "Owner") retains all rights in and to that Artwork. We do not claim any rights over user-created Artwork.

Unless the Owner has given explicit express, written permission, any use of Artwork to train, fine‑tune, validate, evaluate, or otherwise improve machine learning, generative AI, or other automated models or datasets is strictly prohibited. Prohibited acts include, without limitation, collection, copying, ingestion, extraction, annotation, transformation, or inclusion of such Artwork in any training, testing, or benchmarking dataset.

Explicit prohibition on "no‑copyright" labels and opt‑out schemes:
Artwork labeled or claimed as "no copyright", "public domain", or similar does not authorize AI training under this project's terms. Likewise, any purported "opt‑out" or "unless the Owner opts out" declarations (i.e., statements that artworks may be used for AI purposes unless an Owner affirmatively opts out) do not and shall not be treated as authorization to use Artwork and this project for AI training. Such uses are violations of this project's terms and may result in injunctive relief, damages, and recovery of costs and attorneys' fees under applicable law.

## License and AI notice
Unless otherwise specified, files in this repository are licensed under the AGPL-3.0 license.

### Shader code special notice

For the purposes of this Project, "Shader Code" means any GPU shader program, shader graph, material shader, or related code used to render strokes, fills, brushes, or other visual effects in this project.

1. Use in Artwork

    You are explicitly allowed to use the Shader Code render Artwork in closed‑source or commercial games, including shipping binaries and asset bundles containing compiled, optimized, or obfuscated versions of the Shader Code, without any obligation to publish your game source code or shader source code, as long as: 
    - You do not distribute the Shader Code as a general‑purpose graphics component.
    - You do not use the rendered Artwork for AI training purposes or give training permission to others.

2. Use in other software

    Parts of the Shader Code implement original rendering algorithms and techniques that are the result of Shen Ciao's own research.
    Unless you obtain the researcher Shen's explicit permission, if you integrate the Shader Code (or modified versions of it) into any other program, that program and all parts using the Shader Code must be released under the same AGPL‑3.0 license and made publicly available in source form.

    This includes, without limitation:
    - Using the Shader Code inside AI‑assisted painting tools or AI model pipelines;
    - Taking the Shader Code and shipping it in a closed‑source or proprietary drawing/painting application without releasing your source code under AGPL‑3.0;

3. No AI‑training opt‑out bypass

   Labeling Shader‑rendered assets or shaders themselves as “no copyright”, “public domain”, or similar does not grant permission for AI training under these terms. Any “opt‑out” or “unless the Owner opts out” AI policies do not override this notice. Unauthorised AI use of the Shader Code or Shader‑rendered assets is a violation of this project’s terms.

These special Shader Code terms are an integral part of the Project’s licensing and must be read together with the AGPL‑3.0 license and the "User‑created content copyright and AI notice" above.

## Credits

### Project name

The name "Ciallo" is a combination of the Italian "Ciao" and English "Hello", and means "Hello". It comes from the galgames developed by [Yuzusoft](https://www.yuzu-soft.com/). We won't shame this name.

### Citation

Check Ciallo's [research project] if you are interested in how Ciallo's unique features are implemented.

SIGGRAPH 2024 Technical Paper (Conference Track)

    @inproceedings{10.1145/3641519.3657418,
       author = {Ciao, Shen and Guan, Zhongyue and Liu, Qianxi and Wei, Li-Yi and Wang, Zeyu},
       title = {Ciallo: GPU-Accelerated Rendering of Vector Brush Strokes},
       year = {2024},
       isbn = {9798400705250},
       publisher = {Association for Computing Machinery},
       address = {New York, NY, USA},
       url = {https://doi.org/10.1145/3641519.3657418},
       doi = {10.1145/3641519.3657418},
       booktitle = {ACM SIGGRAPH 2024 Conference Papers},
       articleno = {3},
       numpages = {11},
       keywords = {brush stroke rendering, digital painting, vector graphics},
       location = {Denver, CO, USA},
       series = {SIGGRAPH '24}
    }

SIGGRAPH 2023 Talk

    @inproceedings{Ciallo,
       author = {Ciao, Shen and Wei, Li-Yi},
       title = {Ciallo: The next-Generation Vector Paint Program},
       year = {2023},
       isbn = {9798400701436},
       publisher = {Association for Computing Machinery},
       address = {New York, NY, USA},
       url = {https://doi.org/10.1145/3587421.3595418},
       doi = {10.1145/3587421.3595418},
       booktitle = {ACM SIGGRAPH 2023 Talks},
       articleno = {67},
       numpages = {2},
       keywords = {Digital painting, stylized stroke, arrangement, vector graphics. coloring, graphics processing unit (GPU)},
       location = {Los Angeles, CA, USA},
       series = {SIGGRAPH '23}
    }

### Coding frameworks/libraries

- [Godot C#](https://godotengine.org/): [Why godot?](#tech-faq)
- [Frent](https://github.com/itsBuggingMe/Frent): Unity-like (gameobject/entity) component pattern (ECS library without need for the 'S').
- [CGAL](https://www.cgal.org/): Complex geometry operations.
- [R3](https://github.com/Cysharp/R3): Signal on steroids and reactive programming.
- [GdUnit4](https://github.com/MikeSchulze/gdUnit4): Unit test framework.
- [MessagePack](https://github.com/MessagePack-CSharp/MessagePack-CSharp): Binary serialization.
- [Newtonsoft.Json](https://www.newtonsoft.com/json): JSON serialization.
- [GodotSharp.SourceGenerators](https://github.com/Cat-Lips/GodotSharp.SourceGenerators): Godot auxiliary.
- [Stateless](https://github.com/dotnet-state-machine/stateless): Managing complex interactive states (tool system).

## Build Guide

Ciallo is built on Godot. Building the core part of Ciallo is the same as building a standard Godot C# project:

- Set up Godot 4.5.1 with .Net9. You can follow an arbitrary [video guide](https://www.youtube.com/watch?v=7nExKQn1CAw), but pay attention to the version.
- Open the `Ciallo/project.godot` file with your Godot editor, then build and run.

Go to [Contributing Tab](https://github.com/ShenCiao/Ciallo?tab=contributing-ov-file#how-to-build) for a more complete guide.

## Tech FAQ

Shen answers the following questions:

### Hand-drawn art vs. Graphics design?
Ciallo is designed for hand-drawn art---illustrations and animations, rather than graphic design tasks such as logos, web layouts, or UI/UX assets.

You may wonder about the difference: think of it like game development compared with front-end web development. Both involve creating visual elements, but they require different skills and tools. With unlimited resources Ciallo could support both, like the Affinity 3 does, but most programs focus on one area for a better user experience:
Krita, Clip Studio Paint, and Procreate emphasize hand-drawn art, while Adobe Illustrator, Figma, and Inkscape specialize in graphic design.

But Ciallo's stroke-rendering technology isn't limited to hand-drawn art. Everywhere needs stylized lines/strokes could use it. Feel free to ask me for support if you'd like to use Ciallo's brushes while developing graphic-design or other software.

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
Developing a mature programming language is as hard as developing the game engine itself.
GDscript is doubling the efforts as Godot grows.

[Contributing Tab]:https://github.com/ShenCiao/Ciallo?tab=contributing-ov-file

[research project]:https://github.com/ShenCiao/CialloResearch

[Shen Ciao]:https://github.com/ShenCiao
