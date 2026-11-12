param(
    [Parameter(Mandatory)][string]$Frames,
    [Parameter(Mandatory)][string]$Output,
    [ValidateRange(1, 60)][int]$FrameRate = 15,
    [string]$Ffmpeg = 'ffmpeg'
)
$ErrorActionPreference = 'Stop'
$frameDirectory = (Resolve-Path -LiteralPath $Frames).Path
if (Test-Path -LiteralPath $Output) { throw "Output already exists: $Output" }
& $Ffmpeg -hide_banner -loglevel error -framerate $FrameRate -i (Join-Path $frameDirectory 'frame_%04d.png') -c:v libwebp_anim -lossless 1 -compression_level 6 -loop 0 $Output
if ($LASTEXITCODE -ne 0) { throw "FFmpeg failed with exit code $LASTEXITCODE" }
