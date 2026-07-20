# vpk으로 Velopack release package 생성. 사용자가 다음 launch 시 자동 검출 + opt-in 적용.
# GitHub Actions는 tag push 시 이 흐름을 자동 수행한다.

param([string]$Version = "")
$ErrorActionPreference = "Stop"
$requiredVpkVersion = "1.2.0"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))

Push-Location $repoRoot
try {

if ([string]::IsNullOrWhiteSpace($Version)) {
    # csproj에서 version 자동 추출
    [xml]$csproj = Get-Content DenoVideoPlayer.csproj
    $Version = ($csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
}
if ([string]::IsNullOrWhiteSpace($Version)) { throw "Version unresolved" }

# 공개 패키징 도구는 자동 설치하지 않는다. 승인된 버전을 운영자가 명시적으로 준비해야 한다.
if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    throw "vpk $requiredVpkVersion is required. Install it explicitly with: dotnet tool install --global vpk --version $requiredVpkVersion"
}
$vpkHelp = (& vpk --help 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw "vpk --help failed with exit code $LASTEXITCODE" }
$versionMatch = [regex]::Match($vpkHelp, 'Velopack CLI\s+(\d+\.\d+\.\d+)')
if (-not $versionMatch.Success) { throw "Could not read the installed vpk version from its help output." }
$actualVpkVersion = $versionMatch.Groups[1].Value
if ($actualVpkVersion -ne $requiredVpkVersion) {
    throw "vpk version mismatch. Required $requiredVpkVersion, found: $actualVpkVersion"
}

Write-Host ">>> Publishing self-contained build for v$Version" -ForegroundColor Cyan
$fileVersion = "$Version.0"
$pubDir = "publish-sc\DenoVideoPlayer-win-x64"
$fullPubDir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $pubDir))
if (-not $fullPubDir.StartsWith($repoRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe publish directory: $fullPubDir"
}
if (Test-Path -LiteralPath $fullPubDir) { Remove-Item -LiteralPath $fullPubDir -Recurse -Force }
dotnet publish DenoVideoPlayer.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishReadyToRun=true -p:DebugType=none -p:DebugSymbols=false `
    -p:Version=$Version -p:FileVersion=$fileVersion -p:InformationalVersion=$Version `
    -o $pubDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

# 외부 runtime binary 제외 — 첫 launch 시 앱이 필요한 backend를 준비함.
Remove-Item -Force "$pubDir\runtime\mpv\mpv.exe" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$pubDir\runtime\ffmpeg" -ErrorAction SilentlyContinue

Write-Host ">>> vpk pack" -ForegroundColor Cyan
$outDir = "Releases"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
vpk pack `
    --packId DenoVideoPlayer `
    --packVersion $Version `
    --packDir $pubDir `
    --mainExe DenoVideoPlayer.exe `
    --packTitle "Deno Video Player" `
    --packAuthors "DENO" `
    --icon icon.ico `
    --outputDir $outDir `
    --yes `
    --skip-updates
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed with exit code $LASTEXITCODE" }

Write-Host ""
Write-Host (">>> Done. Output: {0}" -f $outDir) -ForegroundColor Green
Write-Host "  Upload these files to the public GitHub Release for automatic updates:" -ForegroundColor Yellow
Write-Host ("  {0}\DenoVideoPlayer-win-Setup.exe, *.nupkg, RELEASES, releases.win.json" -f $outDir) -ForegroundColor Yellow
Write-Host "  Recommended release page: https://github.com/Deno2026/deno-video-player/releases/new" -ForegroundColor Yellow
}
finally {
    Pop-Location
}
