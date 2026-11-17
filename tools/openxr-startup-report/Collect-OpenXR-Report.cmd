@echo off
powershell.exe -NoLogo -NoProfile -File "%~dp0Collect-OpenXR-Report.ps1"
if errorlevel 1 echo Report collection failed. Copy the error shown above.
pause
