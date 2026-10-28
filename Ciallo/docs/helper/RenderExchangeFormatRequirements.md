# Ciallo Render Exchange Format

## Status and goal

`.glb2dim` is the draft display format used by the WebGPU gallery in
`ShenCiao/brush-rendering-tutorial`. Its container is RIFF; its JSON description
and binary resource organization are inspired by GLB. It is independent of
Khronos glTF/GLB. File identification and compatibility use the signature,
version, and profile, rather than the filename extension.

The target is visual agreement with Ciallo at the same viewing conditions:
no apparent difference at first glance. Bit-identical pixels across GPUs are
not required. Geometry, brush evaluation, paint order, transparency, and
compositing must preserve the artwork's appearance.

The format describes rendering semantics. GPU buffers, shader programs,
framebuffers, passes, and stencil operations are renderer implementation details.

## Supported export

Use **File → Export current frame for web** in Ciallo. The exporter reads the
working document, including unsaved changes, resolves the current frame and
visible layer tree, and writes one complete display snapshot. The source project
is unchanged. Data is prepared before writing and the output is replaced only
after a complete temporary file has been written.

Version 1, profile `ciallo-airbrush-1`, supports:

- Variable-radius Airbrush polylines with pressure-controlled flow, color,
  density, falloff, and Normal or Erase brush blending.
- Solid-color polygon fills triangulated with Ciallo's existing
  `Arrangement2D.RepairAndTriangulate` implementation.
- Shape layers and folders, whole-group opacity, Normal/Add/Multiply layer
  blending, and resolved sibling clipping stacks.
- The selected cel exposure in a cel folder. Only the current frame is exported;
  this version does not encode an animation timeline.

Other brush models, dashed strokes, missing brushes, image layers, and other
unsupported drawing types fail export with an error. Display-affecting data is
required; the loader does not substitute black or skip required effects.

Hidden layers are omitted after resolving clipping relationships. A hidden or
absent clipping base yields zero coverage for its clipped siblings. Editor
selection, lock state, timeline UI state, and layer-panel state are not exported.

## Container and encoding

All integer lengths and binary numeric arrays use little-endian encoding.
The outer file is a standard RIFF form:

```text
RIFF  uint32(file_length - 8)  C2DI
  JSON  uint32(payload_length)  UTF-8 description  [zero pad if odd]
  BIN0  uint32(payload_length)  binary resources   [zero pad if odd]
```

There is exactly one `JSON` and one `BIN0` chunk. A chunk length excludes its
8-byte header and optional pad. Unknown chunks may be skipped by length; new
required rendering semantics must change the supported version/profile. The
classic RIFF 32-bit lengths apply. The format has no compression or checksum.

Every resource range is `{ "offset": byte_offset, "count": scalar_count }`,
relative to the start of the `BIN0` payload. Offsets are multiples of four.
Positions and tilts are interleaved float32 pairs; radii, pressures, and curves
are float32 arrays. Triangle indices are uint32. `count` counts scalar values,
so N positions occupy 2N floats. RIFF's two-byte chunk alignment does not imply
that BIN0's absolute file offset is four-byte aligned; loaders must account for
that when creating typed array views.

## Description and coordinates

The JSON root contains:

- `version`: integer `1`.
- `profile`: `"ciallo-airbrush-1"`.
- `colorSpace`: `"linear-srgb"`.
- `name`: artwork display name.
- `width`, `height`: positive reference canvas dimensions.
- `frame`: the source frame number, for identification only.
- `brushes`: shared brush records.
- `nodes`: ordered paint nodes.

Coordinates use reference-canvas units, origin at the canvas center, positive X
right and positive Y down. Content is clipped to the reference canvas. Browser
layout and camera zoom are independent of reference dimensions. Later siblings
paint over earlier siblings. Colors are straight linear-sRGB RGBA in the file;
compositing operates on premultiplied linear-sRGB RGBA.

## Draw records

A stroke has `kind: "stroke"`, `brush` (index into `brushes`), and ranges for
`positions`, `radii`, `pressures`, and `tilts`. Radii are final geometric radii;
pressure-to-radius authoring has already been applied. The renderer must not
apply it a second time. The current brush profile does not consume tilt.

A fill has `kind: "fill"`, `color`, and ranges for `positions` and `indices`.
Every three indices describe a triangle. Editable polygon rings and editor fill
markers are not needed for playback.

A brush record has `color`, `density`, `pressureFlow`, `erase`, and ranges
`pressure` and `falloff`. Each curve has 512 samples, baked by
`StrokeBrushMaterial.BakeCurve` at x = i/512. Sampling matches normalized
level-zero linear texture sampling with clamped endpoints: interpolate at
`clamp(t * 512 - 0.5, 0, 511)`.

The profile's brush model follows Ciallo's Gaussian Airbrush evaluation in
`Rendering/Stroke.gdshader`: a 0.6-render-target-pixel Gaussian with two-pixel
support, variable-radius segment integration, filtered transmittance, and a
0.01-pixel diameter cull threshold. A one-point stroke uses the same very short
segment representation as `StrokeView`. Normal outputs premultiplied color;
Erase attenuates destination RGB and alpha by the same keep factor.

## Groups and clipping stacks

A group has `kind: "group"`, optional display `name`, `opacity`, `blend`, and
ordered `children`. Blend values are `0` Normal, `1` Add, `2` Multiply. Group
opacity is applied after its children are composited. Ordinary source-over
subtrees may be drawn directly when that is equivalent; erasing and
non-source-over child operations require the same isolation boundaries as
Godot's automatic `Layer2D` compositor.

A clipping stack has `kind: "clip"`, `base` (a group), and ordered `clips`
(groups). First composite the base's children and apply its opacity. Then apply
each clipped group's completed image and opacity using the clipped equations
below. Finally composite the stack into its parent using the base's blend mode.
The base's blend mode is not applied when establishing the internal stack.
Clipped groups never increase the stack's alpha.

For premultiplied source S (after opacity) and destination D:

| Mode | Ordinary RGB | Clipped RGB |
| --- | --- | --- |
| Normal | S.rgb + D.rgb × (1 − S.a) | S.rgb × D.a + D.rgb × (1 − S.a) |
| Add | S.rgb + D.rgb | S.rgb × D.a + D.rgb |
| Multiply | S.rgb × (1 − D.a) + D.rgb × (S.rgb + 1 − S.a) | D.rgb × (S.rgb + 1 − S.a) |

Ordinary alpha is S.a + D.a × (1 − S.a). Clipped alpha is D.a. Values above one
from additive compositing remain available until final display conversion.
These equations match the project's custom Godot `Layer2D` pipeline, including
its transparent-backdrop and clipped-Multiply behavior.

## Verification and extension

The first complete asset is `Inaba Meguru`: its current visible snapshot is
loaded by the gallery and compared with the supplied PNG. GPU regression cases
exercise overlapping group opacity, clipped half-transparent bases, and
Multiply over transparency. Browser interaction checks cover left/middle drag,
pointer-centered wheel zoom, keyboard navigation, resizing, and page navigation.

Future format revisions can add brush models and animation. Until a revision is
implemented and verified by both exporter and loader, the current profile's
supported surface above is authoritative. File references are resource indices,
not stable editor object identities.
