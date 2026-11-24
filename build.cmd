@echo off
chcp 65001 >nul
cd /d "%~dp0"
dotnet publish "src\AefAccount\AefAccount.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "src\AefAccount\publish"
if errorlevel 1 (
  echo Build failed.
  exit /b 1
)
if not exist "src\AefAccount\publish\data" mkdir "src\AefAccount\publish\data"
copy /Y "data\schema.sql" "src\AefAccount\publish\data\" >nul
copy /Y "data\seed.sql" "src\AefAccount\publish\data\" >nul
if not exist "src\AefAccount\publish\scripts" mkdir "src\AefAccount\publish\scripts"
copy /Y "scripts\*.py" "src\AefAccount\publish\scripts\" >nul
echo Done: src\AefAccount\publish\AkEfLedger.exe
pause
