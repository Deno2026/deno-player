#requires -Version 5.1
<#
.SYNOPSIS
  Fetches mpv.exe into runtime\mpv\ for Deno Player.

.DESCRIPTION
  mpv does not host an "official" Windows static build directly.
  This script grabs the latest x86_64 build from a community release
  (zhongfly/mpv-winbuild by default), extracts it, and keeps mpv.exe
  (and any sibling DLLs) under runtime\mpv\.

  mpv itself is GPLv2+ / LGPLv2.1+. This repository does NOT redistribute
  mpv binaries; this script merely automates the download the user
  would otherwise perform manually.

.NOTES
  Run from PowerShell:
      pwsh -ExecutionPolicy Bypass -File .\tools\fetch-mpv.ps1
  or:
      powershell -ExecutionPolicy Bypass -File .\tools\fetch-mpv.ps1
#>

[CmdletBinding()]
param(
    [string]$Owner = "zhongfly",
    [string]$Repo  = "mpv-winbuild",
    [string]$Dest  = "",
    [string]$Match = "mpv-x86_64-(?!.*v3-).*\.7z$"
)

$ErrorActionPreference = "Stop"

# $PSScriptRoot is empty when this file is dot-sourced before -File runs.
# Resolve it ourselves so default Dest works in every shell.
if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
} else {
    $scriptRoot = $PSScriptRoot
}
if ([string]::IsNullOrWhiteSpace($Dest)) {
    $Dest = Join-Path $scriptRoot "..\runtime\mpv"
}

# Make Dest absolute.
$destParent = Split-Path $Dest -Parent
if (-not (Test-Path $destParent)) { New-Item -ItemType Directory -Path $destParent -Force | Out-Null }
$Dest = (Resolve-Path $destParent).Path + "\" + (Split-Path $Dest -Leaf)
New-Item -ItemType Directory -Path $Dest -Force | Out-Null

function Find-7Zip {
    $candidates = @(
        "$env:ProgramFiles\7-Zip\7z.exe",
        "${env:ProgramFiles(x86)}\7-Zip\7z.exe",
        (Get-Command 7z -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
    ) | Where-Object { $_ -and (Test-Path $_) }
    return $candidates | Select-Object -First 1
}

Write-Host ">>> Resolving latest mpv release from $Owner/$Repo ..." -ForegroundColor Cyan
$headers = @{ "User-Agent" = "DenoPlayer-mpv-fetch" }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
$rel = Invoke-RestMethod -Headers $headers -Uri "https://api.github.com/repos/$Owner/$Repo/releases/latest"

$asset = $rel.assets | Where-Object { $_.name -match $Match } | Select-Object -First 1
if (-not $asset) {
    Write-Error "No matching asset (pattern: $Match). Grab mpv manually from https://mpv.io/installation/ and place mpv.exe under $Dest"
    exit 1
}

$tmp7z = Join-Path $env:TEMP $asset.name
Write-Host ">>> Downloading $($asset.name) ..." -ForegroundColor Cyan
Invoke-WebRequest -Headers $headers -Uri $asset.browser_download_url -OutFile $tmp7z

$sz = Find-7Zip
if (-not $sz) {
    Write-Warning "7-Zip not found. Extract the archive manually and copy mpv.exe to: $Dest"
    Write-Host    "   Downloaded archive: $tmp7z" -ForegroundColor Yellow
    Write-Host    "   (To install 7-Zip: winget install 7zip.7zip)" -ForegroundColor Yellow
    exit 0
}

$extractDir = Join-Path $env:TEMP ("mpv-extract-" + [Guid]::NewGuid().ToString("N").Substring(0,8))
New-Item -ItemType Directory -Path $extractDir -Force | Out-Null
Write-Host ">>> Extracting with 7-Zip ..." -ForegroundColor Cyan
& $sz x -y "-o$extractDir" $tmp7z | Out-Null

$exe = Get-ChildItem -Path $extractDir -Recurse -Filter "mpv.exe" | Select-Object -First 1
if (-not $exe) {
    Write-Error "mpv.exe not found inside extracted archive: $extractDir"
    exit 1
}

Copy-Item -Path $exe.FullName -Destination (Join-Path $Dest "mpv.exe") -Force

# Sibling DLLs / locale resources if present.
$exeDir = Split-Path $exe.FullName -Parent
Get-ChildItem $exeDir -Filter "*.dll" -ErrorAction SilentlyContinue | ForEach-Object {
    Copy-Item $_.FullName -Destination $Dest -Force
}

Remove-Item $tmp7z -Force -ErrorAction SilentlyContinue
Remove-Item $extractDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host ">>> Done. Installed at: $Dest\mpv.exe" -ForegroundColor Green
& (Join-Path $Dest "mpv.exe") --version | Select-Object -First 1
