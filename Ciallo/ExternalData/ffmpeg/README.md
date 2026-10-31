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

Supported targets are `win-x64`, `linux-x64`, and `osx-arm64`:

| Target | Binary supplier | Pinned version |
| --- | --- | --- |
| Windows x64, Linux x64 | [BtbN monthly release](https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-09-30-13-08) | `n8.1.3-9-g29e619e767`, static LGPL build |
| macOS ARM64 | [OSXExperts](https://www.osxexperts.net/ffmpeg81arm.zip) | FFmpeg 8.1 |

BtbN retains the last build of each month for two years. Update these pinned
downloads before September 2028; they are not permanent archives. OSXExperts uses
a versioned URL but does not publish a retention guarantee. Linux requires
glibc 2.28 and kernel 4.18 or newer.

`manifest.json` records the download URLs, versions, licenses, archive formats and
entries, executable sizes, and SHA-256 hashes of both archives and executables.
Only the manifest, license notices, and setup code are committed; executables are
ignored by Git.

The command verifies the installed executable and reuses it when it matches.
Otherwise it downloads the archive into `.ciallo/ffmpeg-downloads/`, verifies
its SHA-256 hash, extracts only the executable, and verifies the executable hash
before installing it with execution permissions using an atomic rename. Verified
cached archives are reused. A failed download or verification leaves the previously
installed executable intact. ZIP extraction requires `unzip`; Linux archives
require `tar` with xz support.

The Windows and Linux executables use LGPL-3.0-or-later; the macOS executable uses
GPL-2.0-or-later. Each platform's `NOTICE.txt` identifies its supplier, source,
and build information; `LICENSE.txt` contains the corresponding license text.

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

To update the binaries, select a retained BtbN month-end release for Windows and
Linux and a versioned OSXExperts release for macOS. Verify the archives against
upstream checksums where provided, calculate each executable's hash, update
`manifest.json` and the platform's notices, run `./engine.sh ffmpeg <target>`, and run
`dotnet test Ciallo/Ciallo.csproj --filter FullyQualifiedName~ExportPngTests`.
The release workflow also runs the executable from its exported location and
verifies tagged RGBA16 PNG output on every supported platform. The build must
include the PNG encoder with sRGB metadata support and the Targa encoder with
BGRA and RLE support.
