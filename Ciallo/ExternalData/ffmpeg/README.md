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

Supported targets are `win-x64`, `linux-x64`, and `osx-arm64`. Windows and Linux
use the pinned BtbN build; macOS uses the pinned OSXExperts build. `manifest.json`
records upstream archives, versions, extraction entries, licenses, and SHA-256
hashes of both archives and installed executables. Only the manifest, notices,
and setup code are committed; executables are ignored by Git.

The command verifies the installed executable and reuses it when it matches.
Otherwise it downloads an archive into `.ciallo/ffmpeg-downloads/`, verifies its
hash, extracts only the selected executable, verifies that hash, and installs it
with execution permissions. Verified archives are reused. A failed download or
verification leaves the previously installed executable intact. ZIP extraction
requires `unzip`; Linux archives require `tar` with xz support.

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

To update a binary, verify the supplier archive and executable hashes, update
`manifest.json` and the platform's notices, run `./engine.sh ffmpeg <target>`, and run
`dotnet test Ciallo/Ciallo.csproj --filter FullyQualifiedName~ExportPngTests`.
The release workflow also runs the executable from its exported location and
verifies tagged RGBA16 PNG output on every supported platform. The build must
include the PNG encoder with sRGB metadata support and the Targa encoder with
BGRA and RLE support.
