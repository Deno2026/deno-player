#requires -Version 5.1
<#
.SYNOPSIS
  Fetches ffmpeg.exe into runtime\ffmpeg\ for Deno Video Player's trim feature.

.DESCRIPTION
  Downloads a static Windows x64 ffmpeg build from BtbN/FFmpeg-Builds
  (LGPL/GPL static binaries, ~30 MB compressed). Used by the in-app
  trim feature (lossless stream copy, no re-encode).

  ffmpeg is LGPLv2.1+/GPLv2+. This repo does NOT redistribute ffmpeg;
  this script only automates the download the user could do manually.

.NOTES
  Run from PowerShell:
      pwsh -ExecutionPolicy Bypass -File .\tools\fetch-ffmpeg.ps1
  or:
      powershell -ExecutionPolicy Bypass -File .\tools\fetch-ffmpeg.ps1
#>

[CmdletBinding()]
param(
    [string]$Owner = "BtbN",
    [string]$Repo  = "FFmpeg-Builds",
    [string]$Dest  = "",
    # Static + LGPL (no encoder restrictions for trim use case)
    [string]$Match = "ffmpeg-master-latest-win64-lgpl\.zip$",
    [switch]$SkipIfExists
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
} else {
    $scriptRoot = $PSScriptRoot
}
if ([string]::IsNullOrWhiteSpace($Dest)) {
    $Dest = Join-Path $scriptRoot "..\runtime\ffmpeg"
}

$destParent = Split-Path $Dest -Parent
if (-not (Test-Path $destParent)) { New-Item -ItemType Directory -Path $destParent -Force | Out-Null }
$Dest = (Resolve-Path $destParent).Path + "\" + (Split-Path $Dest -Leaf)
New-Item -ItemType Directory -Path $Dest -Force | Out-Null

$existing = Join-Path $Dest "ffmpeg.exe"
if ($SkipIfExists -and (Test-Path $existing)) {
    Write-Host ">>> ffmpeg.exe already at $existing - skipping download (-SkipIfExists)." -ForegroundColor Yellow
    & $existing -version | Select-Object -First 1
    exit 0
}

Write-Host ">>> Resolving latest ffmpeg release from $Owner/$Repo ..." -ForegroundColor Cyan
$headers = @{ "User-Agent" = "DenoPlayer-ffmpeg-fetch" }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
$rel = Invoke-RestMethod -Headers $headers -Uri "https://api.github.com/repos/$Owner/$Repo/releases/latest"

$asset = $rel.assets | Where-Object { $_.name -match $Match } | Select-Object -First 1
if (-not $asset) {
    Write-Error "No matching asset (pattern: $Match). Grab ffmpeg manually from https://ffmpeg.org/download.html and place ffmpeg.exe under $Dest"
    exit 1
}

$tmpZip = Join-Path $env:TEMP $asset.name
Write-Host ">>> Downloading $($asset.name) (~30 MB) ..." -ForegroundColor Cyan
Invoke-WebRequest -Headers $headers -Uri $asset.browser_download_url -OutFile $tmpZip

$extractDir = Join-Path $env:TEMP ("ffmpeg-extract-" + [Guid]::NewGuid().ToString("N").Substring(0,8))
New-Item -ItemType Directory -Path $extractDir -Force | Out-Null
Write-Host ">>> Extracting ..." -ForegroundColor Cyan
Expand-Archive -Path $tmpZip -DestinationPath $extractDir -Force

$exe = Get-ChildItem -Path $extractDir -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
if (-not $exe) {
    Write-Error "ffmpeg.exe not found inside extracted archive: $extractDir"
    exit 1
}

Copy-Item -Path $exe.FullName -Destination (Join-Path $Dest "ffmpeg.exe") -Force

# ffprobe도 함께 (메타데이터 조회용, 향후 확장 여지)
$probe = Get-ChildItem -Path $extractDir -Recurse -Filter "ffprobe.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($probe) {
    Copy-Item -Path $probe.FullName -Destination (Join-Path $Dest "ffprobe.exe") -Force
}

Remove-Item $tmpZip -Force -ErrorAction SilentlyContinue
Remove-Item $extractDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host ">>> Done. Installed at: $Dest\ffmpeg.exe" -ForegroundColor Green
& (Join-Path $Dest "ffmpeg.exe") -version | Select-Object -First 1
