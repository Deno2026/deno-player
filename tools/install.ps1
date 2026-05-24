#requires -Version 5.1
<#
.SYNOPSIS
  Registers Deno Player with Windows so the user can:
    - Right-click any media file -> Open with -> Deno Player
    - Set Deno Player as the default app for chosen extensions
    - Launch from Desktop shortcut and Start Menu

  All registration is HKCU-only (per-user). No UAC, no admin needed.

.DESCRIPTION
  Run once after `dotnet publish` (or after `dotnet build`). The script:
    1. Resolves the path to DenoPlayer.exe (-ExePath optional override)
    2. Registers HKCU\Software\Classes\Applications\DenoPlayer.exe
       with SupportedTypes for video/audio/image extensions so the EXE
       shows up under Explorer's "Open with" menu
    3. Adds Deno Player to each extension's HKCU\...\OpenWithProgids so
       Windows offers it in the "Open with" dialog
    4. Creates a Desktop shortcut
    5. Creates a Start Menu shortcut

.PARAMETER ExePath
  Absolute path to DenoPlayer.exe. If omitted, auto-detected.

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
$AllExt   = $VideoExt + $AudioExt + $ImageExt

$AppRegKey  = 'HKCU:\Software\Classes\Applications\DenoPlayer.exe'
$DesktopLnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Deno Player.lnk'
$StartMenuLnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'Deno Player.lnk'

function Find-Exe {
    if ($ExePath) {
        if (-not (Test-Path $ExePath)) { throw "ExePath not found: $ExePath" }
        return (Resolve-Path $ExePath).Path
    }
    $candidates = @(
        (Join-Path $repoRoot 'publish\DenoPlayer-win-x64\DenoPlayer.exe'),
        (Join-Path $repoRoot 'publish\DenoPlayer-win-x64-fxdep\DenoPlayer.exe'),
        (Join-Path $repoRoot 'bin\Release\net8.0-windows\win-x64\publish\DenoPlayer.exe'),
        (Join-Path $repoRoot 'bin\Release\net8.0-windows\publish\DenoPlayer.exe'),
        (Join-Path $repoRoot 'bin\Release\net8.0-windows\DenoPlayer.exe'),
        (Join-Path $repoRoot 'bin\Debug\net8.0-windows\DenoPlayer.exe'),
        (Join-Path $repoRoot 'DenoPlayer.exe')
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return (Resolve-Path $c).Path }
    }
    throw "DenoPlayer.exe not found. Build first: `dotnet build -c Release`. Or pass -ExePath."
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

function Install-DenoPlayer {
    $exe = Find-Exe
    $work = Split-Path $exe -Parent
    Write-Host ">>> Registering Deno Player" -ForegroundColor Cyan
    Write-Host "    exe: $exe" -ForegroundColor DarkGray

    $hkcu = [Microsoft.Win32.Registry]::CurrentUser
    $progId        = 'DenoPlayer.Media'
    $openWithProg  = 'Applications\DenoPlayer.exe'
    $capabilities  = 'Software\DenoPlayer\Capabilities'

    # 1) HKCU Application key — "Open with" menu / SupportedTypes
    New-Item -Path $AppRegKey -Force | Out-Null
    Set-ItemProperty -Path $AppRegKey -Name 'FriendlyAppName' -Value 'Deno Player' -Force
    New-Item -Path "$AppRegKey\shell\open\command" -Force | Out-Null
    Set-ItemProperty -Path "$AppRegKey\shell\open\command" -Name '(Default)' `
        -Value ('"' + $exe + '" "%1"') -Force
    New-Item -Path "$AppRegKey\SupportedTypes" -Force | Out-Null
    foreach ($e in $AllExt) {
        Set-ItemProperty -Path "$AppRegKey\SupportedTypes" -Name $e -Value '' -Force
    }

    # 2) ProgID — required to be selectable as a real default app
    New-Item -Path "HKCU:\Software\Classes\$progId" -Force | Out-Null
    Set-ItemProperty -Path "HKCU:\Software\Classes\$progId" `
        -Name '(Default)' -Value 'Deno Player Media File' -Force
    Set-ItemProperty -Path "HKCU:\Software\Classes\$progId" `
        -Name 'FriendlyTypeName' -Value 'Deno Player Media' -Force
    New-Item -Path "HKCU:\Software\Classes\$progId\DefaultIcon" -Force | Out-Null
    Set-ItemProperty -Path "HKCU:\Software\Classes\$progId\DefaultIcon" `
        -Name '(Default)' -Value ('"' + $exe + '",0') -Force
    New-Item -Path "HKCU:\Software\Classes\$progId\shell\open\command" -Force | Out-Null
    Set-ItemProperty -Path "HKCU:\Software\Classes\$progId\shell\open\command" `
        -Name '(Default)' -Value ('"' + $exe + '" "%1"') -Force

    # 3) Capabilities + RegisteredApplications — Settings "기본 앱"에 등장
    New-Item -Path "HKCU:\$capabilities" -Force | Out-Null
    Set-ItemProperty -Path "HKCU:\$capabilities" `
        -Name 'ApplicationName' -Value 'Deno Player' -Force
    Set-ItemProperty -Path "HKCU:\$capabilities" `
        -Name 'ApplicationDescription' `
        -Value 'Lightweight local media shell player (mpv backend)' -Force
    Set-ItemProperty -Path "HKCU:\$capabilities" `
        -Name 'ApplicationIcon' -Value ('"' + $exe + '",0') -Force

    New-Item -Path "HKCU:\$capabilities\FileAssociations" -Force | Out-Null
    foreach ($e in $AllExt) {
        Set-ItemProperty -Path "HKCU:\$capabilities\FileAssociations" `
            -Name $e -Value $progId -Force
    }
    New-Item -Path 'HKCU:\Software\RegisteredApplications' -Force | Out-Null
    Set-ItemProperty -Path 'HKCU:\Software\RegisteredApplications' `
        -Name 'Deno Player' -Value $capabilities -Force

    # 4) Per-extension OpenWithProgids — Set-ItemProperty가 backslash 포함 값 이름을
    #    silently drop하므로 .NET API 직접 사용. ProgID + Applications\... 둘 다 추가
    foreach ($e in $AllExt) {
        $sub = $hkcu.CreateSubKey("Software\Classes\$e\OpenWithProgids", $true)
        try {
            $sub.SetValue($progId, [byte[]]@(),
                          [Microsoft.Win32.RegistryValueKind]::None)
            $sub.SetValue($openWithProg, [byte[]]@(),
                          [Microsoft.Win32.RegistryValueKind]::None)
        } finally { $sub.Close() }
    }

    # 5) Shortcuts
    if (-not $NoDesktop) {
        New-Shortcut -target $exe -lnk $DesktopLnk -workdir $work -desc 'Deno Player — local media shell'
        Write-Host "    desktop:    $DesktopLnk" -ForegroundColor DarkGray
    }
    if (-not $NoStartMenu) {
        New-Shortcut -target $exe -lnk $StartMenuLnk -workdir $work -desc 'Deno Player — local media shell'
        Write-Host "    start menu: $StartMenuLnk" -ForegroundColor DarkGray
    }

    Write-Host ">>> Done." -ForegroundColor Green
    Write-Host ""
    Write-Host "How to use:" -ForegroundColor Yellow
    Write-Host "  1) Double-click 'Deno Player' on the Desktop, or"
    Write-Host "  2) In Explorer, right-click a media file -> 'Open with' -> 'Deno Player'"
    Write-Host "  3) To make Deno Player the default for an extension, use Windows"
    Write-Host "     Settings -> Apps -> Default apps, or in 'Open with' dialog tick"
    Write-Host "     'Always use this app'."
}

function Uninstall-DenoPlayer {
    Write-Host ">>> Removing Deno Player registrations" -ForegroundColor Cyan
    $progId = 'DenoPlayer.Media'
    if (Test-Path $AppRegKey) {
        Remove-Item -Path $AppRegKey -Recurse -Force
        Write-Host "    removed $AppRegKey" -ForegroundColor DarkGray
    }
    if (Test-Path "HKCU:\Software\Classes\$progId") {
        Remove-Item -Path "HKCU:\Software\Classes\$progId" -Recurse -Force
        Write-Host "    removed ProgID $progId" -ForegroundColor DarkGray
    }
    if (Test-Path 'HKCU:\Software\DenoPlayer') {
        Remove-Item -Path 'HKCU:\Software\DenoPlayer' -Recurse -Force
        Write-Host "    removed Capabilities" -ForegroundColor DarkGray
    }
    if (Test-Path 'HKCU:\Software\RegisteredApplications') {
        Remove-ItemProperty -Path 'HKCU:\Software\RegisteredApplications' `
            -Name 'Deno Player' -ErrorAction SilentlyContinue
    }
    foreach ($e in $AllExt) {
        $extKey = "HKCU:\Software\Classes\$e\OpenWithProgids"
        if (Test-Path $extKey) {
            Remove-ItemProperty -Path $extKey -Name 'Applications\DenoPlayer.exe' -ErrorAction SilentlyContinue
            Remove-ItemProperty -Path $extKey -Name $progId -ErrorAction SilentlyContinue
        }
    }
    if (Test-Path $DesktopLnk)   { Remove-Item $DesktopLnk -Force }
    if (Test-Path $StartMenuLnk) { Remove-Item $StartMenuLnk -Force }
    Write-Host ">>> Done." -ForegroundColor Green
    Write-Host "Note: file associations that were explicitly set as 'Always use this app'"
    Write-Host "      live under HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\<ext>\\UserChoice"
    Write-Host "      and Windows protects them. Change them via Settings -> Apps -> Default apps."
}

if ($Uninstall) { Uninstall-DenoPlayer } else { Install-DenoPlayer }
