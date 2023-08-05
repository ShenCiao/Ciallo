---
layout: inner
title: About
permalink: /
---

{::nomarkdown}

<script async src="https://unpkg.com/es-module-shims@1.6.3/dist/es-module-shims.js"></script>
<script type="importmap">
{
    "imports": {
    "three": "https://unpkg.com/three@0.154.0/build/three.module.js",
    "three/addons/": "https://unpkg.com/three@0.154.0/examples/jsm/"
    }
}
</script>
<script id="vertexShader" type="x-shader/x-vertex">
	{% capture vertex_shader %}
  	{% include_relative /shaders/articulatedLine.vert %}
	{% endcapture %}
  {{ vertex_shader | remove_first: "#version 300 es" }}
</script>

<script id="fragmentShader" type="x-shader/x-fragment">
	{% capture fragment_shader %}
  	{% include_relative /shaders/articulatedLine.frag %}
	{% endcapture %}
  {{ fragment_shader | remove_first: "#version 300 es" }}
</script>
{:/}


# Ciallo ～(∠・ω< )⌒★!

## Stroke Demo
{::nomarkdown}
<style>
  canvas { width: inherit; position: relative; top: 0;}
</style>
<div id="stats-holder" style="display: none;"></div>
<div id='canvas-holder' style="position: relative;">
  <div id="dat-gui-holder" style="position: absolute; top: 0em; left: 0em; z-index: 1;"></div>
</div>
{:/}
---
### Instructions

**Pan**: Left-click and drag the mouse.

**Zoom**: Scroll or drag the mouse wheel.

**Settings**: Click the `Open Controls` button.

### Intro

"This is a GPU-rendered 2D stroke demo built upon Three.js, showcasing methods derived from a research project called Ciallo. The project focuses on GPU-based rendering of 2D paint strokes."

---

**Strokes Categories:** _Vanilla_, _Stamp_ and _Airbrush_.

The _Vanilla_ is the default stroke and self-explanatory.

The _Stamp_ includes the **`Splatter`**, **`Pencil`** and **`Dot`** strokes. 
The option **`Equidistant`** means the interval between the textures are equal,
the option **`Ratiodistant`** means the interval between the textures are propotional to the stroke radius.

The _Airbrush_ has two control points, which are the two middle control points of a cubic bezier curve starts from (0.0, 1.0) ends to (1.0, 0.0).

**Models:**

**`Sine Wave`** is the default model.

**`Monkey`** is the mascot of Blender Grease Pencil.

**`Bear`** is created by Renato Roldan and Daniel Martinez Lara (Pepeland). We run a performance test with it and show the test result in our talk.

**`Totoro`** is the character from the anime movie _My Neighbor Totoro_. I like the character so put it here. This version is created by Francisco Antonio Peinado (Currobot). 

---

### Have Fun!

Shader code [link](./shaders). 

If you like this demo and want to learn more about 2D paint stroke rendering, please check out for the project main page and star the project.

{::nomarkdown}
<script type="module" src="js/main.js"></script>
<a href="https://github.com/ShenCiao/Ciallo"><button class="btn btn-default btn-lg"><i class="fa fa-github fa-lg"></i>GitHub</button></a>
{:/}

