# Ciallo: Next-generation digital paint/animation software

Ciallo～(∠・ω< )⌒★! Anime computer graphics.

## Introduction

Ciallo aims for improving 2D artists' work efficiency with modern GPU and computational geometry. 

The name "Ciallo" is the combination of the Italian "Ciao" and English "Hello", and comes from the video game *Sabbat of the Witch* developed by *Yuzusoft*.

Different from traditional pixel image or bezier curve based painting software, polylines/polygons are the first citizens in Ciallo. They are rendered with GPU and edited based on computational geometry algorithms. 

Ciallo is greatly inspired by [Blender Grease Pencil](https://docs.blender.org/manual/en/latest/grease_pencil/introduction.html). It's a wonderful toolset based on polylines in 3D space inside Blender. Here are some successful artworks drawn with the polyline method (in blender): [GPencil open project](https://cloud.blender.org/p/gallery/5b642e25bf419c1042056fc6). Ciallo fixes some fatal rendering drawbacks in blender grease pencil and tries to provide more innovative tools.

For now, Ciallo is more of a personal research project. But I wish it could shine as free open-source software. Artists would love to paint/animate their artworks and researchers could easily develop and test their 2D geometry/rendering algorithm in a real production environment. Help me out by staring on this project and follow up the evolution of 2D computer graphics.

## Feature Previews

- Bucket fill with vector strokes
- Brush engines powered by GPU
- Curve binding for stroke editing and animating

### Bucket fill with vector strokes

<figure>
    <p> <img src="./articles/autoColoringFromShriobako.jpg" width = 640/></p>
</figure>

*Fig. - Screenshot from SHIROBAKO. It's an anime about anime making.*

Artists cannot bucket fill in a vector graphics software like Adobe Illustrator or Inkscape. Ciallo will solve the problem.

In animation industry, artists use labeling strokes to indicate how to fill colors. For example, on the upper part of the picture, blue strokes inside the character indicate fill in shadows, red strokes indicate highlights, green or yellow indicate contours need to be filled in combination with other strokes. 

<img src=".\articles\AnimeMakingwholeProcess.gif" alt="AnimeMakingwholeProcess"  />

Ciallo will utilize these labeling strokes for coloring. In computational geometry, [the point-location problem](https://en.wikipedia.org/wiki/Point_location) is well-researched and widely used in GIS and motion planning. Color filling in 2D artworks is exactly a point-location problem. 

In Ciallo, the color filling is separated into three steps. First, artists paint on canvas and meanwhile arrange the polyline data. Second, query with labeling curves' and fetch desired face data (faces are polygons generated from artworks). Third, send face data to GPU for rendering a colored polygon. 

Compared to flood fill on high-resolution pixelmap, it can be 100~1000 times faster and the final artwork can be freely modified. You may be interested in [statistics on some existing artworks](./articles/miscellaneous.md#statistics-on-existing-artworks), the number of polylines and points are critical to the performance of 2D arrangements.

A similar feature offered by Krita is called "[colorize mask](https://docs.krita.org/en/reference_manual/tools/colorize_mask.html)". But it seems not fast enough for production and hard to edit the content on canvas since it's based on pixelmap. Take a look at [this video](https://www.youtube.com/watch?v=HQdx6H9BIGs) if you are interested in the problem of colorize mask.

In combination with line binding, users can animate their artworks with great ease. After modifying the labeling strokes and strokes enclosing them, faces generated from strokes are updated automatically.

### Brush engines powered by GPU

<img src=".\articles\six.gif" alt="naiive brush engine" style="zoom:100%;" />

*Fig. - Triangle directed upward is drawn by Articulated Line, and downward is by Equidistant Dot.*

Polyline/polygon in 2D space is the counterpart of mesh in 3D space. Real-time rendering on polyline could potentially save GPU from fetching GBs of texture or video. It'll make large-scale animation and real-time lighting in 2D games possible. 

Though inspired by the blender grease pencil, the rendering method in Ciallo is quite different from the grease pencil. The new method fixes some fatal drawbacks and aims for flexibility instead of performance.

https://user-images.githubusercontent.com/24319509/182907394-b014c70b-33b3-4f8d-91f2-60aa095a5d49.mp4

*Fig. -  Attributes are editable at per vertex level.*

In July 2022, two engines are made, named *Equidistant Dot* and *Articulated Line*. As the names imply, *equidistant dots* evenly place texture quad along the polyline, *articulated line* renders a polyline as if an articulated arm. Here are some comparisons between them:

| Features                | Equidistant Dot                                              | Articulated Line                                             |
| ----------------------- | ------------------------------------------------------------ | ------------------------------------------------------------ |
| Great performance       | Yes, better than methods on CPU.                             | Yes, better than Equidistant Dot in theory.                  |
| Customization by users  | Easy. A procedural texture is not mandatory.                 | Hard. Procedural texture or seamlessly tiled texture is mandatory. Need experience in shader development or shader graph node system. |
| Robustness to ill cases | One ill case would be pretty common (unevenly distributed vertices) and it might hit hard on rendering performance. Need user to avoid it. | Better than Equidistant Dot. Pretty few ill cases I've found and they rarely happen in practice. |
| Limitations on vertices | Total amount of vertices input is limited by the maximum local workgroup size (1024) in compute shader. The total amount of dots generated is limited by the buffer size set by developers. | No limits on regular usage.                                  |



Their computations in geometry are pretty straightforward except for the airbrush generated from the articulated line. It needs calculus to clarify the whole idea of solving "the joint problem". I've made [a draft to explain the brush](./articles/ContinuousAirbrushStrokeRendering.pdf) in Jan. 2022 but it's too messy to read. Unless you are ultra interested in rendering an airbrush stroke on a bezier curve which takes [8 steps in illustrator](https://www.techwalla.com/articles/how-to-airbrush-in-illustrator), I do not recommend reading it.

### Line binding for editing and animating

Polylines are hard to edit. So Ciallo allows users to "bind" a polyline upon a bezier curve. It'll act like this:

<figure>
    <p> <img src=".\articles\strokeManipulation.gif"/></p>
</figure>


*Fig. - Screenshot from the paper StrokeStrip: Joint Parameterization and Fitting of Stroke Clusters.*

The polyline keeps noise information, meanwhile bezier curve helps to edit the polyline's overall shapes. When it comes to animating, bezier curve can make animation like bones in 3D after binding, which lets 2D artists reuse their data within stroke level. A lot of geometry tools may be able to apply to polyline editing potentially.

## Trinity

Each feature above doesn't make too much sense for end users: 

>- Label to fill? Clicking around with the paint bucket tool is convenient enough;
>- GPU brush engines? Why bother with very few brushes? There are so many seasoned brushes in Photoshop, Clip Studio Paint, Affinity, or Krita.
>- Curve binding? Just use a bezier curve vector software like Illustrator or Inkscape.

Only the integration of the three features above can make a brand-new experience for users. Star on Ciallo and see more of this amazing toolset in the future.

## How to Compile

### Windows

- Pull vcpkg and integrate.
- Pull the codebase and run Ciallo.sln with visual studio.

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

## Inspired by Blender Grease Pencil

Ciallo is inspired by [blender grease pencil](https://docs.blender.org/manual/en/latest/grease_pencil/introduction.html) (gpencil). I got a lot of help from @Clément Foucault and @Falk David when learning the code of grease pencil system. The grease pencil system let artists draw polyline strokes in 3D space which are rendered by OpenGL. It's a great amazing tool haven't been widely accepted by artists.

Ciallo discards one dimension, which makes it possible to utilize some powerful geometry tools only available on curves in 2D space like 2D arrangements, 2D generalized winding numbers, and 2D Envelopes.

Ciallo will try to implement everything you could expect from grease pencil in the future, especially for curve edit/sculpt tools.

