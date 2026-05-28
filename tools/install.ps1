#requires -Version 5.1
<#
.SYNOPSIS
  Registers Deno Video Player with Windows so the user can:
    - Right-click any media file -> Open with -> Deno Video Player
    - Set Deno Video Player as the default app for chosen extensions
    - Launch from Desktop shortcut and Start Menu

  All registration is HKCU-only (per-user). No UAC, no admin needed.

.DESCRIPTION
  Run once after `dotnet publish` (or after `dotnet build`). The script:
    1. Resolves the path to DenoVideoPlayer.exe (-ExePath optional override)
    2. Registers HKCU\Software\Classes\Applications\DenoVideoPlayer.exe
       with SupportedTypes for video/audio extensions so the EXE
       shows up under Explorer's "Open with" menu
    3. Adds Deno Video Player to video/audio HKCU\...\OpenWithProgids so
       Windows offers it in the "Open with" dialog. Images stay opt-in from
       the app Settings screen.
    4. Creates a Desktop shortcut
    5. Creates a Start Menu shortcut

.PARAMETER ExePath
  Absolute path to DenoVideoPlayer.exe. If omitted, auto-detected.

.PARAMETER NoDesktop
  Skip Desktop shortcut.

.PARAMETER NoStartMenu
  Skip Start Menu shortcut.

.PARAMETER Uninstall
  Remove the registrations and shortcuts.

.EXAMPLE
  pwsh -ExecutionPolicy Bypass -File .\tools\install.ps1
  pwsh -ExecutionPolicy Bypass -File .\tools\install.ps1 -Uninstall
#>

[CmdletBinding()]
param(
    [string]$ExePath,
    [switch]$NoDesktop,
    [switch]$NoStartMenu,
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

# Resolve script root robustly.
if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
} else {
    $scriptRoot = $PSScriptRoot
}
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")

$VideoExt = @('.mp4','.mkv','.mov','.webm','.avi','.m4v','.ts','.mts','.m2ts','.wmv','.flv','.3gp')
$AudioExt = @('.mp3','.wav','.flac','.aac','.m4a','.ogg','.opus','.wma','.alac')
$ImageExt = @('.jpg','.jpeg','.png','.webp','.bmp','.gif')
$DefaultExt = $VideoExt + $AudioExt
$AllExt   = $VideoExt + $AudioExt + $ImageExt
$DefaultExtLookup = @{}
foreach ($e in $DefaultExt) { $DefaultExtLookup[$e.ToLowerInvariant()] = $true }

$AppRegKey  = 'HKCU:\Software\Classes\Applications\DenoVideoPlayer.exe'
$LegacyAppRegKey = 'HKCU:\Software\Classes\Applications\DenoPlayer.exe'
$DesktopLnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Deno Video Player.lnk'
$StartMenuLnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'Deno Video Player.lnk'
$LegacyDesktopLnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Deno Player.lnk'
$LegacyStartMenuLnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'Deno Player.lnk'

function Find-Exe {
    if ($ExePath) {
        if (-not (Test-Path $ExePath)) { throw "ExePath not found: $ExePath" }
        return (Resolve-Path $ExePath).Path
    }
    $candidates = @(
        (Join-Path $repoRoot 'publish\DenoVideoPlayer-win-x64\DenoVideoPlayer.exe'),
        (Join-Path $repoRoot 'publish\DenoVideoPlayer-win-x64-fxdep\DenoVideoPlayer.exe'),
        (Join-Path $repoRoot 'bin\Release\net8.0-windows\win-x64\publish\DenoVideoPlayer.exe'),
        (Join-Path $repoRoot 'bin\Release\net8.0-windows\publish\DenoVideoPlayer.exe'),
        (Join-Path $repoRoot 'bin\Release\net8.0-windows\DenoVideoPlayer.exe'),
        (Join-Path $repoRoot 'bin\Debug\net8.0-windows\DenoVideoPlayer.exe'),
        (Join-Path $repoRoot 'DenoVideoPlayer.exe')
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return (Resolve-Path $c).Path }
    }
    throw "DenoVideoPlayer.exe not found. Build first: `dotnet build -c Release`. Or pass -ExePath."
}

function New-Shortcut([string]$target, [string]$lnk, [string]$workdir, [string]$desc) {
    $wsh = New-Object -ComObject WScript.Shell
    $sc = $wsh.CreateShortcut($lnk)
    $sc.TargetPath       = $target
    $sc.WorkingDirectory = $workdir
    $sc.Description      = $desc
    $sc.IconLocation     = "$target,0"
    $sc.Save()
}

function Install-DenoVideoPlayer {
    $exe = Find-Exe
    $work = Split-Path $exe -Parent
    Write-Host ">>> Registering Deno Video Player" -ForegroundColor Cyan
    Write-Host "    exe: $exe" -ForegroundColor DarkGray

    $hkcu = [Microsoft.Win32.Registry]::CurrentUser
    $progId        = 'DenoVideoPlayer.Media'
    $openWithProg  = 'Applications\DenoVideoPlayer.exe'
    $capabilities  = 'Software\DenoVideoPlayer\Capabilities'
    $legacyProgId = 'DenoPlayer.Media'
    $legacyOpenWithProg = 'Applications\DenoPlayer.exe'
    $legacyCapabilities = 'Software\DenoPlayer\Capabilities'

    Remove-Item -Path $LegacyAppRegKey -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "HKCU:\Software\Classes\$legacyProgId" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "HKCU:\$legacyCapabilities" -Recurse -Force -ErrorAction SilentlyContinue

    # 1) HKCU Application key — "Open with" menu / SupportedTypes
    New-Item -Path $AppRegKey -Force | Out-Null
    Set-ItemProperty -Path $AppRegKey -Name 'FriendlyAppName' -Value 'Deno Video Player' -Force
    New-Item -Path "$AppRegKey\shell\open\command" -Force | Out-Null
    Set-ItemProperty -Path "$AppRegKey\shell\open\command" -Name '(Default)' `
        -Value ('"' + $exe + '" "%1"') -Force
    Remove-Item -Path "$AppRegKey\SupportedTypes" -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -Path "$AppRegKey\SupportedTypes" -Force | Out-Null
    foreach ($e in $DefaultExt) {
        Set-ItemProperty -Path "$AppRegKey\SupportedTypes" -Name $e -Value '' -Force
    }

    # 2) ProgID — required to be selectable as a real default app
    New-Item -Path "HKCU:\Software\Classes\$progId" -Force | Out-Null
    Set-ItemProperty -Path "HKCU:\Software\Classes\$progId" `
        -Name '(Default)' -Value 'Deno Video Player Media File' -Force
    Set-ItemProperty -Path "HKCU:\Software\Classes\$progId" `
        -Name 'FriendlyTypeName' -Value 'Deno Video Player Media' -Force
    New-Item -Path "HKCU:\Software\Classes\$progId\DefaultIcon" -Force | Out-Null
    Set-ItemProperty -Path "HKCU:\Software\Classes\$progId\DefaultIcon" `
        -Name '(Default)' -Value ('"' + $exe + '",0') -Force
    New-Item -Path "HKCU:\Software\Classes\$progId\shell\open\command" -Force | Out-Null
    Set-ItemProperty -Path "HKCU:\Software\Classes\$progId\shell\open\command" `
        -Name '(Default)' -Value ('"' + $exe + '" "%1"') -Force

    # 3) Capabilities + RegisteredApplications — Settings "기본 앱"에 등장
    New-Item -Path "HKCU:\$capabilities" -Force | Out-Null
    Set-ItemProperty -Path "HKCU:\$capabilities" `
        -Name 'ApplicationName' -Value 'Deno Video Player' -Force
    Set-ItemProperty -Path "HKCU:\$capabilities" `
        -Name 'ApplicationDescription' `
        -Value 'Lightweight local media shell player (mpv backend)' -Force
    Set-ItemProperty -Path "HKCU:\$capabilities" `
        -Name 'ApplicationIcon' -Value ('"' + $exe + '",0') -Force

    Remove-Item -Path "HKCU:\$capabilities\FileAssociations" -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -Path "HKCU:\$capabilities\FileAssociations" -Force | Out-Null
    foreach ($e in $DefaultExt) {
        Set-ItemProperty -Path "HKCU:\$capabilities\FileAssociations" `
            -Name $e -Value $progId -Force
    }
    New-Item -Path 'HKCU:\Software\RegisteredApplications' -Force | Out-Null
    Remove-ItemProperty -Path 'HKCU:\Software\RegisteredApplications' `
        -Name 'Deno Player' -ErrorAction SilentlyContinue
    Set-ItemProperty -Path 'HKCU:\Software\RegisteredApplications' `
        -Name 'Deno Video Player' -Value $capabilities -Force

    # 4) Per-extension OpenWithProgids — Set-ItemProperty가 backslash 포함 값 이름을
    #    silently drop하므로 .NET API 직접 사용. ProgID + Applications\... 둘 다 추가
    foreach ($e in $AllExt) {
        $sub = $hkcu.CreateSubKey("Software\Classes\$e\OpenWithProgids", $true)
        try {
            $sub.DeleteValue($legacyProgId, $false)
            $sub.DeleteValue($legacyOpenWithProg, $false)
            if ($DefaultExtLookup.ContainsKey($e.ToLowerInvariant())) {
                $sub.SetValue($progId, [byte[]]@(),
                              [Microsoft.Win32.RegistryValueKind]::None)
                $sub.SetValue($openWithProg, [byte[]]@(),
                              [Microsoft.Win32.RegistryValueKind]::None)
            } else {
                $sub.DeleteValue($progId, $false)
                $sub.DeleteValue($openWithProg, $false)
            }
        } finally { $sub.Close() }
    }

    # 5) Shortcuts
    if (-not $NoDesktop) {
        if (Test-Path $LegacyDesktopLnk) { Remove-Item $LegacyDesktopLnk -Force }
        New-Shortcut -target $exe -lnk $DesktopLnk -workdir $work -desc 'Deno Video Player — local media shell'
        Write-Host "    desktop:    $DesktopLnk" -ForegroundColor DarkGray
    }
    if (-not $NoStartMenu) {
        if (Test-Path $LegacyStartMenuLnk) { Remove-Item $LegacyStartMenuLnk -Force }
        New-Shortcut -target $exe -lnk $StartMenuLnk -workdir $work -desc 'Deno Video Player — local media shell'
        Write-Host "    start menu: $StartMenuLnk" -ForegroundColor DarkGray
    }

    Write-Host ">>> Done." -ForegroundColor Green
    Write-Host ""
    Write-Host "How to use:" -ForegroundColor Yellow
    Write-Host "  1) Double-click 'Deno Video Player' on the Desktop, or"
    Write-Host "  2) In Explorer, right-click a media file -> 'Open with' -> 'Deno Video Player'"
    Write-Host "  3) To make Deno Video Player the default for an extension, use Windows"
    Write-Host "     Settings -> Apps -> Default apps, or in 'Open with' dialog tick"
    Write-Host "     'Always use this app'."
}

function Uninstall-DenoVideoPlayer {
    Write-Host ">>> Removing Deno Video Player registrations" -ForegroundColor Cyan
    $progId = 'DenoVideoPlayer.Media'
    $legacyProgId = 'DenoPlayer.Media'
    $legacyOpenWithProg = 'Applications\DenoPlayer.exe'
    if (Test-Path $AppRegKey) {
        Remove-Item -Path $AppRegKey -Recurse -Force
        Write-Host "    removed $AppRegKey" -ForegroundColor DarkGray
    }
    if (Test-Path $LegacyAppRegKey) {
        Remove-Item -Path $LegacyAppRegKey -Recurse -Force
        Write-Host "    removed $LegacyAppRegKey" -ForegroundColor DarkGray
    }
    if (Test-Path "HKCU:\Software\Classes\$progId") {
        Remove-Item -Path "HKCU:\Software\Classes\$progId" -Recurse -Force
        Write-Host "    removed ProgID $progId" -ForegroundColor DarkGray
    }
    if (Test-Path "HKCU:\Software\Classes\$legacyProgId") {
        Remove-Item -Path "HKCU:\Software\Classes\$legacyProgId" -Recurse -Force
        Write-Host "    removed legacy ProgID $legacyProgId" -ForegroundColor DarkGray
    }
    if (Test-Path 'HKCU:\Software\DenoVideoPlayer') {
        Remove-Item -Path 'HKCU:\Software\DenoVideoPlayer' -Recurse -Force
        Write-Host "    removed Capabilities" -ForegroundColor DarkGray
    }
    if (Test-Path 'HKCU:\Software\DenoPlayer') {
        Remove-Item -Path 'HKCU:\Software\DenoPlayer' -Recurse -Force
        Write-Host "    removed legacy Capabilities" -ForegroundColor DarkGray
    }
    if (Test-Path 'HKCU:\Software\RegisteredApplications') {
        Remove-ItemProperty -Path 'HKCU:\Software\RegisteredApplications' `
            -Name 'Deno Video Player' -ErrorAction SilentlyContinue
        Remove-ItemProperty -Path 'HKCU:\Software\RegisteredApplications' `
            -Name 'Deno Player' -ErrorAction SilentlyContinue
    }
    foreach ($e in $AllExt) {
        $extKey = "HKCU:\Software\Classes\$e\OpenWithProgids"
        if (Test-Path $extKey) {
            Remove-ItemProperty -Path $extKey -Name 'Applications\DenoVideoPlayer.exe' -ErrorAction SilentlyContinue
            Remove-ItemProperty -Path $extKey -Name $progId -ErrorAction SilentlyContinue
            Remove-ItemProperty -Path $extKey -Name $legacyOpenWithProg -ErrorAction SilentlyContinue
            Remove-ItemProperty -Path $extKey -Name $legacyProgId -ErrorAction SilentlyContinue
        }
    }
    if (Test-Path $DesktopLnk)   { Remove-Item $DesktopLnk -Force }
    if (Test-Path $StartMenuLnk) { Remove-Item $StartMenuLnk -Force }
    if (Test-Path $LegacyDesktopLnk)   { Remove-Item $LegacyDesktopLnk -Force }
    if (Test-Path $LegacyStartMenuLnk) { Remove-Item $LegacyStartMenuLnk -Force }
    Write-Host ">>> Done." -ForegroundColor Green
    Write-Host "Note: file associations that were explicitly set as 'Always use this app'"
    Write-Host "      live under HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\<ext>\\UserChoice"
    Write-Host "      and Windows protects them. Change them via Settings -> Apps -> Default apps."
}

if ($Uninstall) { Uninstall-DenoVideoPlayer } else { Install-DenoVideoPlayer }
