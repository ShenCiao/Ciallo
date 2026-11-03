# Drawing adapter

The running Ciallo application's `/root/AutoloadData` node exposes a small C# drawing
API to Fennara `runtime_script` and other Godot callers. Every method returns a JSON
string. Check `ok` before using the result. Expected input errors return
`{"ok":false,"error":"..."}`; internal application failures are not suppressed.

## Session and document operations

| Method | Behavior |
| --- | --- |
| `DrawingNewDocument(name, Vector2(width, height))` | Create an empty, unsaved document with a white background. |
| `DrawingOpenDocument(absolutePath)` | Load an existing `.ciallo` file through the normal document loader. |
| `DrawingGetState(id, includeGeometry)` | Read document metadata and objects; an empty ID includes all objects. |
| `DrawingApplyBatch(json)` | Validate and commit a batch as one undoable action. |
| `DrawingHistory(session, version, redo)` | Undo, or redo when `redo` is true. |
| `DrawingSaveDocument(session, version, absolutePath)` | Save to an absolute `.ciallo` path; changing the path uses Save As. |

New/open refuse to replace a modified document. Finish active gestures and stop
playback before mutations. Calls run on the Godot main thread. Godot callers must
supply every argument; use `DrawingGetState("", false)` for a compact full-state
query and `false` for undo.

A state response contains `session`, `version`, `name`, `path`, `size`, `modified`,
`canUndo`, `canRedo`, `selection` (layer IDs and working brush IDs), and `objects`.
With no document, state returns
`{"ok":true,"document":null}`. New documents have no layers or brushes until a batch
creates them. `path` is the configured destination, not proof that a file exists.

Each object has an `id` and `kind`. Tree objects also expose `parent`, `index`, and
ordered `children`; layers expose `name`, `visible`, and `opacity`; brushes expose
`color`; shapes expose `brush` and `pointCount`. Geometry queries include `points`
and `radii`. Unsupported object kinds are listed as `other` and cannot be edited.

Object IDs are stable for one loaded document session, including undo/redo. Caller
IDs are unique within that session and remain reserved after deletion or undo.
`root` denotes the document; IDs beginning with `@` are assigned by inspection.
Opening/replacing a document generates a new session and fresh inspection IDs.
IDs are not stored in the project file. After reopening, query state and locate
layers by their saved names and shapes by their ordered child lists.

Pass the last returned `session` and `version` for batches, history, and save. The
version is the document's committed persistence epoch encoded as an opaque string;
preserve it unchanged across JSON calls. A stale token is rejected.
After a timeout, query state before retrying; compare the epoch and object IDs to
determine whether the operation committed. Undo advances the epoch too.

## Batches

The envelope is `{"session":"...","version":"0","name":"Action label","commands":[]}`.
Commands execute in array order. Referenced layers/brushes must already exist or
be created earlier in the same batch. Each object may be targeted only once per
batch. All input is validated before allocating entities or executing commands.

| `op` | Fields besides required `id` | Effect |
| --- | --- | --- |
| `layer` | `name`, `index` | Create a root shape layer; name defaults to ID. |
| `stroke_brush` | `color`, `name` | Create a solid Vanilla stroke brush. |
| `fill_brush` | `color` | Create a flat fill brush. |
| `stroke` | `parent`, `brush`, `points`, `radius` or `radii`, `index` | Create a native stroke in a shape layer. |
| `polygon` | `parent`, `brush`, `points`, `index` | Create a native filled polygon in a shape layer. |
| `geometry` | `points`, `radius` or `radii` | Replace a shape's complete sampled geometry. |
| `brush` | `brush` | Assign a compatible brush to a shape. |
| `color` | `color` | Change a brush's color, affecting all shapes that use it. |
| `layer_settings` | `name`, `visible`, `opacity` | Change supplied properties of a shape layer. |
| `move` | `parent`, `index` | Move/reorder a shape; index is measured after removal. |
| `delete` | — | Remove a shape. |

`index` defaults to `-1` (append). Later siblings draw above earlier siblings; create
the flat-color layer first and the line layer second. The first created layer is
selected when the document previously had no layer selection. The first created
stroke/fill brushes become working brushes when the corresponding selection was empty.

Coordinates are document/world units: origin at the canvas center, X right, Y down.
For a reference image of the same size, subtract half its width and height from
pixel coordinates. Viewport zoom and pan do not change these coordinates.

`points` is an array of finite `[x,y]` pairs. Strokes require at least two distinct
points, polygons three. Consecutive duplicate points are rejected. Polygon rings
are closed automatically; polygons use the existing repair/triangulation path.
The adapter accepts sampled polylines; sample Bézier curves before submission.
`radius` defaults to 1 document unit (a diameter of 2). `radii`, when supplied for
a stroke, must contain one positive radius per point. Geometry writes use pressure
1 and tilt zero. Colors are HTML hex strings such as `#302832` or `#f59d8aff`.

## Fennara example

Run this inside an active Fennara runtime session for `res://Main.tscn`:

```gdscript
extends RefCounted

func run(ctx: Variant) -> void:
    var api: Node = ctx.get_scene_root().get_node("/root/AutoloadData")
    var state: Dictionary = JSON.parse_string(api.DrawingNewDocument("Drawing", Vector2(512, 512)))
    assert(state.ok, str(state))
    var commands: Array = [
        {"op": "layer", "id": "flat", "name": "Flat color"},
        {"op": "layer", "id": "ink", "name": "Line art"},
        {"op": "fill_brush", "id": "peach", "color": "#f59d8a"},
        {"op": "stroke_brush", "id": "outline", "color": "#302832"},
        {"op": "polygon", "id": "color.1", "parent": "flat", "brush": "peach",
         "points": [[-80, 60], [0, -80], [80, 60]]},
        {"op": "stroke", "id": "line.1", "parent": "ink", "brush": "outline",
         "points": [[-80, 60], [0, -80], [80, 60]], "radii": [1.0, 2.0, 1.0]},
    ]
    state = JSON.parse_string(api.DrawingApplyBatch(JSON.stringify({
        "session": state.session, "version": state.version,
        "name": "Draw two layers", "commands": commands,
    })))
    assert(state.ok, str(state))
    state = JSON.parse_string(api.DrawingSaveDocument(
        state.session, state.version, "C:/drawings/example.ciallo"))
    assert(state.ok, str(state))
    ctx.log("Saved drawing", state)
    await ctx.capture("Drawing")
```

A successful batch confirms a document commit. Stroke rendering and topology have
frame/worker updates; capture a subsequent rendered frame through Fennara for visual
verification. PNG export remains available through Ciallo's existing export dialog.
