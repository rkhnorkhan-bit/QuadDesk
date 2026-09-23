@echo off
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1"
if errorlevel 1 (
  echo.
  echo Release build failed. Read BUILD.md.
  pause
  exit /b 1
)
echo.
echo Release artifacts are in artifacts\release
pause
