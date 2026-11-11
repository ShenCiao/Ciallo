# Media tools

Reusable media helpers and Ciallo capture workflows, executed through Fennara
0.4.3 `runtime_script`. Currently supports Windows desktop screenshots and box
annotations; `settings_pen_pressure.gd` is the first concrete capture workflow.
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
System cursor pixels are not added. This tool currently produces static PNGs;
continuous recording and animated WebP export are not part of this worker.
