# Pack publish folder into a clean DenoVideoPlayer-Setup.zip in user's Downloads.
# Excludes runtime/mpv/mpv.exe (GPL — let START_HERE.bat fetch fresh on user's PC).
param(
    [string]$SrcDir = "publish\DenoVideoPlayer-win-x64",
    [string]$DestZip = ""
)
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($DestZip)) {
    $DestZip = Join-Path $env:USERPROFILE "Downloads\DenoVideoPlayer-Setup.zip"
}

if (-not (Test-Path $SrcDir)) {
    throw "Publish folder not found: $SrcDir (run 'dotnet publish' first)"
}

$tmp = Join-Path $env:TEMP ("denovideoplayer-pack-" + [Guid]::NewGuid().ToString("N").Substring(0,8))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null

Copy-Item -Recurse -Path "$SrcDir\*" -Destination $tmp

# mpv binary excluded — START_HERE.bat downloads it on first run (GPL boundary)
$mpvExe = Join-Path $tmp "runtime\mpv\mpv.exe"
if (Test-Path $mpvExe) { Remove-Item -Force $mpvExe }

if (Test-Path $DestZip) { Remove-Item -Force $DestZip }
Compress-Archive -Path (Join-Path $tmp "*") -DestinationPath $DestZip -CompressionLevel Optimal

Remove-Item -Recurse -Force $tmp

$size = [math]::Round((Get-Item $DestZip).Length / 1MB, 2)
Write-Host "  packed: $DestZip ($size MB)" -ForegroundColor Green
