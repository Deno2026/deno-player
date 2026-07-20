#requires -Version 5.1
<#
.SYNOPSIS
  Fetches ffmpeg.exe into runtime\ffmpeg\ for Deno Video Player's trim feature.

.DESCRIPTION
  Downloads a static Windows x64 ffmpeg build from BtbN/FFmpeg-Builds
  (LGPL/GPL static binaries; download size varies and can be hundreds of MB). Used by the in-app
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

function Install-FileAtomically([string]$Source, [string]$Destination) {
    $dir = Split-Path $Destination -Parent
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $temp = Join-Path $dir ('.' + [IO.Path]::GetFileName($Destination) + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    $backup = $temp + '.bak'
    try {
        Copy-Item -LiteralPath $Source -Destination $temp -Force
        if (Test-Path -LiteralPath $Destination) {
            [IO.File]::Replace($temp, $Destination, $backup, $true)
            Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue
        } else {
            Move-Item -LiteralPath $temp -Destination $Destination
        }
    } finally {
        Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue
    }
}

function Assert-GitHubAssetDigest($Asset, [string]$Path) {
    $digest = [string]$Asset.digest
    if ([string]::IsNullOrWhiteSpace($digest) -or -not $digest.StartsWith('sha256:')) {
        throw "GitHub did not provide a SHA-256 digest for $($Asset.name). Existing runtime was left unchanged."
    }
    $expected = $digest.Substring('sha256:'.Length).ToUpperInvariant()
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actual -ne $expected) {
        throw "SHA-256 verification failed for $($Asset.name). Existing runtime was left unchanged."
    }
    Write-Host ">>> SHA-256 verified." -ForegroundColor DarkGreen
}

function Test-FfmpegExecutable([string]$Path) {
    $process = New-Object System.Diagnostics.Process
    try {
        $process.StartInfo = New-Object System.Diagnostics.ProcessStartInfo
        $process.StartInfo.FileName = $Path
        $process.StartInfo.Arguments = '-version'
        $process.StartInfo.UseShellExecute = $false
        $process.StartInfo.CreateNoWindow = $true
        $process.StartInfo.RedirectStandardOutput = $true
        $process.StartInfo.RedirectStandardError = $true
        if (-not $process.Start()) { return $false }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(5000)) {
            try { $process.Kill() } catch {}
            return $false
        }
        $text = ([string]$stdout.Result + [string]$stderr.Result).Trim()
        if ($text.Length -gt 0) { Write-Host (($text -split '\r?\n')[0]) }
        return $process.ExitCode -eq 0
    } catch {
        return $false
    } finally {
        $process.Dispose()
    }
}

$destParent = Split-Path $Dest -Parent
if (-not (Test-Path $destParent)) { New-Item -ItemType Directory -Path $destParent -Force | Out-Null }
$Dest = (Resolve-Path $destParent).Path + "\" + (Split-Path $Dest -Leaf)
New-Item -ItemType Directory -Path $Dest -Force | Out-Null

$existing = Join-Path $Dest "ffmpeg.exe"
if ($SkipIfExists -and (Test-Path $existing)) {
    if (Test-FfmpegExecutable $existing) {
        Write-Host ">>> Valid ffmpeg.exe already at $existing - skipping download." -ForegroundColor Yellow
        exit 0
    }
    Write-Warning "Existing ffmpeg.exe failed validation. Downloading a clean replacement."
}

Write-Host ">>> Resolving latest ffmpeg release from $Owner/$Repo ..." -ForegroundColor Cyan
$headers = @{ "User-Agent" = "DenoVideoPlayer-ffmpeg-fetch" }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$rel = Invoke-RestMethod -Headers $headers -Uri "https://api.github.com/repos/$Owner/$Repo/releases/latest"

$asset = $rel.assets | Where-Object { $_.name -match $Match } | Select-Object -First 1
if (-not $asset) {
    Write-Error "No matching asset (pattern: $Match). Grab ffmpeg manually from https://ffmpeg.org/download.html and place ffmpeg.exe under $Dest"
    exit 1
}

$tmpZip = Join-Path $env:TEMP (([Guid]::NewGuid().ToString("N")) + "-" + $asset.name)
$extractDir = $null
try {
Write-Host ">>> Downloading $($asset.name) (large download; size varies by build) ..." -ForegroundColor Cyan
Invoke-WebRequest -Headers $headers -Uri $asset.browser_download_url -OutFile $tmpZip
Assert-GitHubAssetDigest $asset $tmpZip

$extractDir = Join-Path $env:TEMP ("ffmpeg-extract-" + [Guid]::NewGuid().ToString("N").Substring(0,8))
New-Item -ItemType Directory -Path $extractDir -Force | Out-Null
Write-Host ">>> Extracting ..." -ForegroundColor Cyan
Expand-Archive -Path $tmpZip -DestinationPath $extractDir -Force

$exe = Get-ChildItem -Path $extractDir -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
if (-not $exe) {
    throw "ffmpeg.exe not found inside extracted archive. Existing runtime was left unchanged."
}

if (-not (Test-FfmpegExecutable $exe.FullName)) {
    throw "Downloaded ffmpeg.exe failed its version check. Existing runtime was left unchanged."
}

# ffprobe를 먼저 교체하고 ffmpeg.exe를 마지막 ready marker로 교체한다.
$probe = Get-ChildItem -Path $extractDir -Recurse -Filter "ffprobe.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($probe) {
    Install-FileAtomically $probe.FullName (Join-Path $Dest "ffprobe.exe")
}
Install-FileAtomically $exe.FullName (Join-Path $Dest "ffmpeg.exe")

Write-Host ""
Write-Host ">>> Done. Installed at: $Dest\ffmpeg.exe" -ForegroundColor Green
if (-not (Test-FfmpegExecutable (Join-Path $Dest "ffmpeg.exe"))) {
    throw "Installed ffmpeg.exe failed its version check."
}
}
finally {
    Remove-Item -LiteralPath $tmpZip -Force -ErrorAction SilentlyContinue
    if ($extractDir) {
        Remove-Item -LiteralPath $extractDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
