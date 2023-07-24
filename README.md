# Ciallo ～(∠・ω< )⌒★!

[Paper](./pape) | [Shaders](./Ciallo/shaders) | [Live Demo](https://youtu.be/gqTrD8-nlh0) | [Talk](https://s2023.siggraph.org/presentation/?id=gensub_185&sess=sess176) (in working) | Blender Implementation (in working)

![Ciallo](https://github.com/ShenCiao/Ciallo/assets/24319509/455de8e7-06ac-49ca-bcd7-854b40102d2d)

## Introduction

Rendering 2D art stroke by stroke with GPU!

The name "Ciallo" is the combination of the Italian "Ciao" and English "Hello", comes from the video game *Sabbat of the Witch* developed by *Yuzusoft*.

The project is greatly inspired by [Blender Grease Pencil](https://docs.blender.org/manual/en/latest/grease_pencil/introduction.html). Shen Ciao will integrate the stroke rendering methods into it. Here are some successful artworks drawn in the grease pencil: [GPencil open project](https://cloud.blender.org/p/gallery/5b642e25bf419c1042056fc6).

This project will be presented at the SIGGRAPH 2023 Conference on Thursday, 10 August 2023, [presentation link](https://s2023.siggraph.org/presentation/?id=gensub_185&sess=sess176). The final draft version of the paper (two-page abstract) is available [here](./paper).

## Core Features

### Vector Fill

![vectorFillDemo](./articles/vector_bucket_fill_demo.gif)

labels and lines|labels, lines and fills|lines and fills
:-------------------------:|:-------------------------:|:-------------------------:
![](./articles/dango_label.png) | ![](./articles/dango_both.png) | ![](./articles/dango_final.png)

### GPU-rendered Brushes

<img src=".\articles\six.gif" alt="naiive brush engine" style="zoom:100%;" />

Vanilla|Airbrush
:-------------------------:|:-------------------------:
![](./articles/brush_vanilla.png)| ![](./articles/brush_airbrush.png)

Pencil|Splatter
:-------------------------:|:-------------------------:
![](./articles/brush_pencil.png)| ![](./articles/brush_splatter.png)

### Curve binding

![binding](./articles/binding_demo.gif)

## Trinity!

Each feature individually do not make a big change, until we combine them together!

![trinity](./articles/trinity.gif)

## About the Future

Ciallo began with a clownish plan. Shen thought he could turn himself into a GPT model which can write thousands of lines of code per day, several months later, he would get a medium-large paint program that can produce serious 2D content. But it quickly turns out the actual pace is two orders of magnitude slower than the initial plan.

Shen will integrate the techniques into the Blender Grease Pencil to provide a production-ready program for artists.

If this project get enough concerns by computer graphics community or someone is willing to sponsor, Shen will make a series of tutorial videos about the stroke rendering.

You may have already found several novel features that we never mentioned in Ciallo's research paper. They might be published in the future.

## How to Compile
### Windows

- Pull vcpkg and integrate.
- Pull the codebase and run `Ciallo/Ciallo.sln` with visual studio.

### External Dependencies

- Rendering
  - OpenGL
- GUI
  - Dear ImGui
  - ImPlot
- Coding Patterns
  - Entt - ECS and event system
- Geometry and algebra
  - CGAL
  - GLM
  - dlib - For curve fitting
