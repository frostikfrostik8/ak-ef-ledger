@echo off
cd /d "%~dp0"
if exist "src\AefAccount\publish\AkEfLedger.exe" (
  start "" "src\AefAccount\publish\AkEfLedger.exe"
  exit /b 0
)
echo Build first: build.cmd
pause
