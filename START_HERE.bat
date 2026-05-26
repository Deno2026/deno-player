@echo off
chcp 65001 > nul
setlocal
cd /d "%~dp0"

echo.
echo  ┌──────────────────────────────────────────────────────┐
echo  │   Deno Player 설치를 시작합니다                       │
echo  └──────────────────────────────────────────────────────┘
echo.
echo  [1/3] mpv 미디어 엔진 받는 중... (한 번만, 약 30 MB)
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\fetch-mpv.ps1" -SkipIfExists
if errorlevel 1 (
    echo.
    echo  X mpv 다운로드 실패. 인터넷 연결을 확인하고 다시 실행하세요.
    echo.
    pause
    exit /b 1
)

echo.
echo  [2/3] ffmpeg 받는 중 (자르기 기능용, 한 번만, 약 30 MB)...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\fetch-ffmpeg.ps1" -SkipIfExists
if errorlevel 1 (
    echo.
    echo  ! ffmpeg 다운로드 실패 - 자르기 기능만 비활성. 재생은 정상.
    echo.
)

echo.
echo  [3/3] 바탕화면 바로가기 + 탐색기 우클릭 메뉴 등록 중...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\install.ps1"
if errorlevel 1 (
    echo.
    echo  X 바로가기 등록 실패.
    echo.
    pause
    exit /b 1
)

echo.
echo  ┌──────────────────────────────────────────────────────┐
echo  │   설치 완료!                                          │
echo  │                                                       │
echo  │   바탕화면의 'Deno Player' 아이콘을 더블클릭하거나     │
echo  │   탐색기에서 영상 파일 우클릭 → 연결 프로그램         │
echo  │   → Deno Player 로 열 수 있습니다.                    │
echo  └──────────────────────────────────────────────────────┘
echo.
pause
