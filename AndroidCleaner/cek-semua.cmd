@echo off
rem Cek semua: satu klik untuk memeriksa HP (read-only). Bisa di-double-click dari Explorer.
setlocal
cd /d "%~dp0"
where python >nul 2>nul || (echo Python belum terpasang. Install dari https://www.python.org/downloads/ & pause & exit /b 1)
if not exist ".tools\platform-tools\adb.exe" (
  where adb >nul 2>nul || (
    echo ADB belum ada, mengunduh Android platform-tools...
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts\install_platform_tools.ps1 || (pause & exit /b 1)
  )
)
python -m androidcleaner devices
python -m androidcleaner checkup %*
set rc=%errorlevel%
echo.
if "%~1"=="" pause
exit /b %rc%
