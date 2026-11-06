@echo off
rem Double-click to put the new version in place: asks for administrator rights (UAC) and runs update.ps1
rem regardless of the machine's PowerShell execution policy (it is Restricted on the NUC).
net session >nul 2>&1
if errorlevel 1 (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0update.ps1"
echo.
pause
