# Bundled FFmpeg

Ciallo uses the FFMpegCore NuGet package to run FFmpeg for single-image and frame
sequence PNG export. The native executable is a separate, optional development
dependency; restoring FFMpegCore does not download it.

| Workflow | Native FFmpeg required? |
| --- | --- |
| `dotnet restore`, `dotnet build` | No; the managed FFMpegCore package restores through NuGet. |
| Open the editor, start Ciallo, develop features unrelated to export | No. |
| Export PNG images or frame sequences; run PNG export integration tests | Yes, for the current platform. |
| Export Ciallo as an application package | Yes, for the target platform; CI prepares it automatically. |

When needed, prepare the current platform's executable from the repository root
(Git Bash on Windows):

```sh
./engine.sh ffmpeg
# Prepare another export target, or every supported target:
./engine.sh ffmpeg osx-arm64
./engine.sh ffmpeg all
```

Supported targets are `win-x64`, `linux-x64`, and `osx-arm64`. All three use
FFmpeg 8.1.2 from the Shaka project's
[versioned release `n8.1.2-1`](https://github.com/shaka-project/static-ffmpeg-binaries/releases/tag/n8.1.2-1).
Linux uses the Ubuntu 22.04 build, with FFmpeg libraries linked statically and
system libraries supplied by the distribution. `manifest.json` records each release
asset's fixed URL, version, license, size, and SHA-256 hash. Only the manifest,
license notices, and setup code are committed; executables are ignored by Git.

The command verifies the installed executable and reuses it when it matches.
Otherwise it downloads the executable into `.ciallo/ffmpeg-downloads/`, verifies
its SHA-256 hash, and installs it with execution permissions using an atomic
rename. Verified cached executables are reused. A failed download or verification
leaves the previously installed executable intact. No archive extraction is needed.

The bundled FFmpeg executables use GPL-3.0-or-later. Each platform's `NOTICE.txt`
links to the upstream source, dependency versions, and build scripts; `LICENSE.txt`
contains the GPLv3 license distributed with FFmpeg.

Run the command before using PNG export, running its integration tests, or
packaging Ciallo, and rerun it when the manifest changes. `engine.sh setup`,
`sync`, `open`, and Git hooks do not prepare FFmpeg. The release workflow prepares
its host platform automatically before exporting. A developer without the native
executable can still start Ciallo; attempting PNG export reports an export error.

The Ciallo editor export plugin includes only the selected platform's executable
and license notices. Windows and Linux put it in `ffmpeg/` beside the application;
macOS puts it in `Contents/Helpers/ffmpeg/`, where Godot signs it with the app.
The executable stays outside the PCK. License notices are stored inside the PCK
at `res://ExternalData/ffmpeg/`. The application uses these bundled binaries and
does not download tools at runtime.

For a local Linux application export, set the helper's executable permissions
before running or packaging the exported directory:

```sh
chmod 755 /path/to/export/ffmpeg/ffmpeg
```

CI applies these permissions before the PNG encoding check and preserves them
in the Linux ZIP package.

To update the binaries, select a Shaka versioned release for all three platforms,
verify the downloaded executables against the release asset SHA-256 digests, update
`manifest.json` and the platform's notices, run `./engine.sh ffmpeg <target>`, and run
`dotnet test Ciallo/Ciallo.csproj --filter FullyQualifiedName~ExportPngTests`.
The release workflow also runs the executable from its exported location and
verifies tagged RGBA16 PNG output on every supported platform. The build must
include the PNG encoder with sRGB metadata support and the Targa encoder with
BGRA and RLE support.
