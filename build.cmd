@echo off
chcp 65001 >nul
cd /d "%~dp0"
dotnet publish "src\AefAccount\AefAccount.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "src\AefAccount\publish"
if errorlevel 1 (
  echo Build failed.
  exit /b 1
)
echo Done: src\AefAccount\publish\AkEfLedger.exe
pause
