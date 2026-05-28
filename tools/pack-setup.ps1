# Pack publish folder into a clean portable zip in user's Downloads.
# Excludes mpv/ffmpeg binaries — app first launch or START_HERE.bat prepares them.
param(
    [string]$SrcDir = "publish\DenoVideoPlayer-win-x64",
    [string]$DestZip = ""
)
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($DestZip)) {
    $DestZip = Join-Path $env:USERPROFILE "Downloads\DenoVideoPlayer-Portable.zip"
}

if (-not (Test-Path $SrcDir)) {
    throw "Publish folder not found: $SrcDir (run 'dotnet publish' first)"
}

$tmp = Join-Path $env:TEMP ("denovideoplayer-pack-" + [Guid]::NewGuid().ToString("N").Substring(0,8))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null

Copy-Item -Recurse -Path "$SrcDir\*" -Destination $tmp

# External runtime binaries are excluded from public packages.
$mpvExe = Join-Path $tmp "runtime\mpv\mpv.exe"
if (Test-Path $mpvExe) { Remove-Item -Force $mpvExe }
$ffmpegDir = Join-Path $tmp "runtime\ffmpeg"
if (Test-Path $ffmpegDir) { Remove-Item -Recurse -Force $ffmpegDir }

if (Test-Path $DestZip) { Remove-Item -Force $DestZip }
Compress-Archive -Path (Join-Path $tmp "*") -DestinationPath $DestZip -CompressionLevel Optimal

Remove-Item -Recurse -Force $tmp

$size = [math]::Round((Get-Item $DestZip).Length / 1MB, 2)
Write-Host "  packed: $DestZip ($size MB)" -ForegroundColor Green
