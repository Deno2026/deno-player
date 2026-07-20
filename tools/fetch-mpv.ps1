#requires -Version 5.1
<#
.SYNOPSIS
  Fetches mpv.exe into runtime\mpv\ for Deno Video Player.

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
    [string]$Match = "mpv-x86_64-(?!.*v3-).*\.7z$",
    [switch]$SkipIfExists  # skip download if mpv.exe already exists in Dest
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
    # 우선순위: 1) tools/7zr.exe (번들된 standalone, 사용자 7-Zip 설치 불필요)
    #         2) system 7-Zip
    $candidates = @(
        (Join-Path $scriptRoot "7zr.exe"),
        "$env:ProgramFiles\7-Zip\7z.exe",
        "${env:ProgramFiles(x86)}\7-Zip\7z.exe",
        (Get-Command 7z -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
    ) | Where-Object { $_ -and (Test-Path $_) }
    return $candidates | Select-Object -First 1
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

function Test-MpvExecutable([string]$Path) {
    $process = New-Object System.Diagnostics.Process
    try {
        $process.StartInfo = New-Object System.Diagnostics.ProcessStartInfo
        $process.StartInfo.FileName = $Path
        $process.StartInfo.Arguments = '--version'
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

$existingMpv = Join-Path $Dest "mpv.exe"
if ($SkipIfExists -and (Test-Path $existingMpv)) {
    if (Test-MpvExecutable $existingMpv) {
        Write-Host ">>> Valid mpv.exe already at $existingMpv - skipping download." -ForegroundColor Yellow
        exit 0
    }
    Write-Warning "Existing mpv.exe failed validation. Downloading a clean replacement."
}

Write-Host ">>> Resolving latest mpv release from $Owner/$Repo ..." -ForegroundColor Cyan
$headers = @{ "User-Agent" = "DenoVideoPlayer-mpv-fetch" }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$rel = Invoke-RestMethod -Headers $headers -Uri "https://api.github.com/repos/$Owner/$Repo/releases/latest"

$asset = $rel.assets | Where-Object { $_.name -match $Match } | Select-Object -First 1
if (-not $asset) {
    Write-Error "No matching asset (pattern: $Match). Grab mpv manually from https://mpv.io/installation/ and place mpv.exe under $Dest"
    exit 1
}

$tmp7z = Join-Path $env:TEMP (([Guid]::NewGuid().ToString("N")) + "-" + $asset.name)
$extractDir = $null
try {
Write-Host ">>> Downloading $($asset.name) ..." -ForegroundColor Cyan
Invoke-WebRequest -Headers $headers -Uri $asset.browser_download_url -OutFile $tmp7z
Assert-GitHubAssetDigest $asset $tmp7z

$sz = Find-7Zip
if (-not $sz) {
    throw "7-Zip not found (bundled tools\7zr.exe is missing). Existing runtime was left unchanged."
}

$extractDir = Join-Path $env:TEMP ("mpv-extract-" + [Guid]::NewGuid().ToString("N").Substring(0,8))
New-Item -ItemType Directory -Path $extractDir -Force | Out-Null
Write-Host ">>> Extracting with 7-Zip ..." -ForegroundColor Cyan
& $sz x -y "-o$extractDir" $tmp7z | Out-Null
if ($LASTEXITCODE -ne 0) { throw "7-Zip extraction failed with exit code $LASTEXITCODE" }

$exe = Get-ChildItem -Path $extractDir -Recurse -Filter "mpv.exe" | Select-Object -First 1
if (-not $exe) {
    throw "mpv.exe not found inside extracted archive. Existing runtime was left unchanged."
}

if (-not (Test-MpvExecutable $exe.FullName)) {
    throw "Downloaded mpv.exe failed its version check. Existing runtime was left unchanged."
}

# Sibling DLLs first, executable last. A valid final mpv.exe is the ready marker.
$exeDir = Split-Path $exe.FullName -Parent
Get-ChildItem $exeDir -Filter "*.dll" -ErrorAction SilentlyContinue | ForEach-Object {
    Install-FileAtomically $_.FullName (Join-Path $Dest $_.Name)
}
Install-FileAtomically $exe.FullName (Join-Path $Dest "mpv.exe")

Write-Host ""
Write-Host ">>> Done. Installed at: $Dest\mpv.exe" -ForegroundColor Green
if (-not (Test-MpvExecutable (Join-Path $Dest "mpv.exe"))) {
    throw "Installed mpv.exe failed its version check."
}
}
finally {
    Remove-Item -LiteralPath $tmp7z -Force -ErrorAction SilentlyContinue
    if ($extractDir) {
        Remove-Item -LiteralPath $extractDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
