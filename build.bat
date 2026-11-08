@echo off
REM Baut Wumms.exe lokal. Braucht das .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
cd /d "%~dp0"
if not exist Wumms\wumms.ico powershell -NoProfile -Command "[IO.File]::WriteAllBytes('Wumms\wumms.ico', [Convert]::FromBase64String((Get-Content Wumms\wumms.ico.b64 -Raw).Trim()))"
dotnet publish Wumms\Wumms.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
if errorlevel 1 (
  echo.
  echo Fehler beim Bauen.
  pause
  exit /b 1
)
echo.
echo Fertig! Deine App liegt hier: %~dp0publish\Wumms.exe
pause
