@echo off
chcp 65001 > nul
setlocal
cd /d "%~dp0"

echo.
echo  Deno Video Player 등록 해제 (바로가기 + 탐색기 메뉴 제거)
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\install.ps1" -Uninstall
if errorlevel 1 (
    echo  X 해제 실패.
    pause
    exit /b 1
)

echo.
echo  완료. 폴더 자체는 그대로 둬도 되고 삭제해도 됩니다.
echo.
pause
