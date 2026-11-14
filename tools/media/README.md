# Media tools

Reusable media helpers and Ciallo capture workflows, executed through Fennara
0.4.3 `runtime_script`. Supports Windows desktop screenshots, box annotations,
image sequences, and animated WebP export. `settings_pen_pressure.gd` captures
the Settings menu entry.
Requires a connected Ciallo editor and a visible desktop. These scripts are tool
infrastructure, not application scripts or autoloads.

## Run

1. Run `./tools/media/prepare.ps1` from the repository root. It stages
   the workers under the ignored `Ciallo/.fennara/tmp/media/` directory.
   Edit the authoritative files in this directory and stage again before rerunning.
2. Start `runtime_session` with `scene_path: "res://Main.tscn"` and `user_args`:
   `['--user-data-dir', '<repo>/.ciallo/media/profile', '--factory-startup']`.
   Replace `<repo>` with the absolute checkout path. Factory startup uses English
   and UI scale 1. The separate profile holds capture documents and preferences.
3. Create a blank document in that profile directory, or open a disposable copy of
   the document needed for the capture. Arrange the desired background before capture.
4. Run `runtime_script` with the returned session ID and
   `script_path: "res://.fennara/tmp/media/run.gd"`.
5. Inspect the output, then stop the owned runtime session.

The current recipe clicks Settings, focuses Global Pen Pressure without activating
it, and captures a 720×210 region at the main window's client origin. It saves:

- `.ciallo/media/output/settings-global-pen-pressure.raw.png`
- `.ciallo/media/output/settings-global-pen-pressure.png`
- `.ciallo/media/output/settings-global-pen-pressure.json`

Rerunning replaces these three outputs. The JSON records the desktop crop, image
dimensions, highlight rectangles in output pixels, and outline color/thickness.
Fennara also receives the annotated image as a normal image artifact.
The runner and recipe explicitly reload their scripts without using Godot's resource
cache, so staging changes takes effect in the same running session. Restart the
runtime session if changing the runner itself.

## Capture constraints

`desktop_capture.gd` captures the actual desktop region, including native popups.
Other windows can occlude it. The recipe temporarily pins the main window above
ordinary windows, then opens its transient menu; after capture it hides the menu
and restores the main window's previous pinning state. On interruption, stop the
owned runtime session. Do not interact with the capture window during the run.

The recipe targets the English, left-to-right, UI-scale-1 layout. Its popup row
measurement supports the current equal-height, single-line Settings items without
icons or separators. Recheck its geometry when that menu or display scaling changes.
The capture helper accounts for a difference between requested region size and
returned pixel dimensions, but mixed-DPI displays have not been validated.

Annotations are applied to a copy of the captured image. They do not alter the UI.
System cursor pixels are not captured. The sequence helper can add a coral pointer
marker at supplied desktop coordinates after capture.

## Animated WebP

Stage with `prepare.ps1`, then load `image_sequence.gd` using
`ResourceLoader.CACHE_MODE_IGNORE`. After each real UI change and render wait, call
`save_frame(region, directory, index, highlights, pointer)`; the last two arguments
are optional. Coordinates use desktop pixels, as in `desktop_capture.gd`.
Use a fresh sequence directory and zero-based consecutive indices. Repeated
unchanged frames provide pauses at the beginning and end.

Encode with FFmpeg (requires the `libwebp_anim` encoder):

```powershell
./tools/media/encode-webp.ps1 -Frames .ciallo/media/output/example-frames -Output .ciallo/media/output/example.webp -FrameRate 15 -Ffmpeg C:/ffmpeg/bin/ffmpeg.exe
```

The output loops, uses lossless compression, and refuses to overwrite an existing
file. The frame rate controls playback timing; this is a sequence of rendered
states, not a real-time performance recording. Keep web assets in the site's
public directory so its image pipeline preserves animation. Inspect the encoded
animation, its first and last frames, and the page at its actual display size.

Native dialog dragging requires events addressed to that window's `window_id`.
Warp its mouse position along with each `InputEventMouseMotion` so Godot's native
hit testing and drag/drop share the same coordinates. Set the held button mask,
release it at the target, and verify the resulting list change. `push_input()`
alone does not update all native-window mouse state. Allow a render wait after
raising the main window before opening its transient dialog; never pin a transient
dialog itself.
