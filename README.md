# Ciallo

Ciallo～(∠・ω< )⌒★! Anime computer graphics.

## Introduction


The name "Ciallo" is the combination of the Italian "Ciao" and English "Hello", which comes from the video game *Sabbat of the Witch* developed by *Yuzusoft*.

Ciallo is greatly inspired by [Blender Grease Pencil](https://docs.blender.org/manual/en/latest/grease_pencil/introduction.html). It's a excellently toolset based on polylines in 3D space. Here are some successful artworks drawn with polyline vector lines (in Blender): [GPencil open project](https://cloud.blender.org/p/gallery/5b642e25bf419c1042056fc6).

Shen Ciao will present Ciallo at SIGGRAPH Conference on Thursday, 10 August 2023, [presentation link](https://s2023.siggraph.org/presentation/?id=gensub_185&sess=sess176). The draft and distributed version of the paper (two-page abstract) is available [here](./paper).

## Core Features

### Vector Bucket Fill

Bucket fill in real-time. Hundreds of times faster than the antique flood-fill algorithm.

Benefiting from this massive amount of performance gain, Ciallo can "vectorize" the position where users fill color, which is recorded and rendered as a regular stroke. We call it labeling stroke temporarily and it needs a better name.

The image below shows translating a labeling stroke (the blue line), the regions to fill are updated automatically in real-time.

In the future, users can copy labeling strokes from one frame to another in an animation project. It can significantly improve their coloring efficiency since drawing in frames are usually highly correlated to each other. Artists can just do easy translation and deformation for the vast majority of frames.

![vectorFillDemo](./articles/vector_bucket_fill_demo.gif)

### GPU-rendered Brushes

Fully GPU-powered brush engine. (At least) a thousand of times faster to render than CPU brushes. Potentially be able to replicate the vast majority of brushes in other paint programs.

You may have encountered paint software advertising their brushes as GPU-accelerated, [like this one](https://www.youtube.com/watch?v=v7RF0etZWwQ). In contrast, Ciallo's brush engine is fully built upon GPU (the difference would be a literally technical topic). And more importantly, we would like to share this technique with everybody.

<img src=".\articles\six.gif" alt="naiive brush engine" style="zoom:100%;" />



![brush_airbrush](./articles/brush_vanilla.png)

![brush_pencil](./articles/brush_splatter.png)

![brush_splatter](./articles/brush_pencil.png)

![brush_vanilla](./articles/brush_airbrush.png)

![monkey](./articles/monkey.png)

The figure above shows the monkey suzanne rendered with the pencil brush. It has 15 strokes and 516 vertices. 

### Curve binding

Polylines are hard to deform, so Ciallo allows users to "bind" a polyline upon a bezier curve. User can deform a polyline as if it was a bezier curve.

This feature is inspired by Blender Grease Pencil's curve editing system and the research _StrokeStrip: Joint Parameterization and Fitting of Stroke Clusters_

![binding](./articles/binding_demo.gif)

## Trinity!

Each invi

![trinity](./articles/trinity.gif)

## About the Future

Ciallo began with a clownish plan. Shen thought he could turn himself into a GPT model which can write thousands of lines of code per day, several months later, he would get a medium-large paint program that can produce serious 2D content. But it quickly turns out the actual pace is two orders of magnitude slower than the initial plan.

Now this project has accumulated too many historical engineering issues. A major rework is necessary for further development. However, it cannot be handled by Shen since his inexperience in large project management (he graduated in Psychology).

You may have already found several novel features that we never mentioned in Ciallo's research paper. They might be published in the future.

Shen needs a job or a Ph.D. degree at USA. Check out his resume and contact him if you are interested in upgrading your paint software or letting him be your student.

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
