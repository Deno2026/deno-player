# vpk으로 Velopack release package 생성. 사용자가 다음 launch 시 자동 검출 + opt-in 적용.
# GitHub Actions는 tag push 시 이 흐름을 자동 수행한다.

param(
    [string]$Version = "",
    [string]$Channel = "win-x64"  # 추후 channel 다양화 위해
)
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Version)) {
    # csproj에서 version 자동 추출
    [xml]$csproj = Get-Content DenoVideoPlayer.csproj
    $Version = ($csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
}
if ([string]::IsNullOrWhiteSpace($Version)) { throw "Version unresolved" }

# vpk 설치 확인
if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Host "Installing vpk (dotnet tool)..." -ForegroundColor Yellow
    dotnet tool install --global vpk
}

Write-Host ">>> Publishing self-contained build for v$Version" -ForegroundColor Cyan
$pubDir = "publish-sc\DenoVideoPlayer-win-x64"
if (Test-Path $pubDir) { Remove-Item -Recurse -Force $pubDir }
dotnet publish DenoVideoPlayer.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishReadyToRun=true -p:DebugType=none -p:DebugSymbols=false `
    -o $pubDir

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

Write-Host ""
Write-Host ">>> Done. Output: $outDir\" -ForegroundColor Green
Write-Host "  자동 update가 가능하려면 $outDir 안 파일들을 public GitHub Release에" -ForegroundColor Yellow
Write-Host "  publish해야 합니다. 권장: github.com/Deno2026/deno-player/releases/new v$Version" -ForegroundColor Yellow
Write-Host "  업로드할 파일: $outDir\DenoVideoPlayer-win-Setup.exe, *.nupkg, RELEASES" -ForegroundColor Yellow
