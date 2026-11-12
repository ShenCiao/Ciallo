$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$scratch = Join-Path $repoRoot 'Ciallo/.fennara/tmp/media'
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'desktop_capture.gd') -Destination $scratch
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'settings_pen_pressure.gd') -Destination $scratch
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'run.gd') -Destination $scratch
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'image_sequence.gd') -Destination $scratch
Write-Output 'Ready: res://.fennara/tmp/media/run.gd'
