@echo off
setlocal
rem Setup.exe is produced by Inno Setup after a Unity Windows player build.
rem Do not compile the old C# self-extracting stub — Defender flags it as malware.

set "ISCC="
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
where ISCC.exe >nul 2>&1 && for /f "delims=" %%I in ('where ISCC.exe') do set "ISCC=%%I"

if not defined ISCC (
  echo Inno Setup compiler ^(ISCC.exe^) was not found.
  echo Install Inno Setup 6.5 or later from https://jrsoftware.org/isdl.php
  echo Then build from Unity: A-Math / Windows / Build Player and Setup.exe
  exit /b 1
)

set "SOURCE=%~dp0..\Build\Windows"
if not exist "%SOURCE%\A-Math.exe" (
  echo No Windows player at %SOURCE%\A-Math.exe
  echo Build from Unity: A-Math / Windows / Build Player and Setup.exe
  exit /b 1
)

set "APP_VERSION="
for /f "tokens=2" %%V in ('findstr /c:"bundleVersion:" "%~dp0..\ProjectSettings\ProjectSettings.asset"') do set "APP_VERSION=%%V"
if not defined APP_VERSION (
  echo Could not read bundleVersion from ProjectSettings\ProjectSettings.asset.
  exit /b 1
)

if exist "%SOURCE%\Setup.exe" del /f /q "%SOURCE%\Setup.exe"
if exist "%SOURCE%\Setup.exe.sha256" del /f /q "%SOURCE%\Setup.exe.sha256"
if exist "%SOURCE%\Setup.exe" exit /b 1
if exist "%SOURCE%\Setup.exe.sha256" exit /b 1

set "OUT=%TEMP%\amath-inno-out"
if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%"

set "SOURCE_FWD=%SOURCE:\=/%"
set "OUT_FWD=%OUT:\=/%"

"%ISCC%" "/DSourceDir=%SOURCE_FWD%" "/DOutputDir=%OUT_FWD%" "/DAppVersion=%APP_VERSION%" "%~dp0A-Math.iss"
if errorlevel 1 exit /b 1

copy /y "%OUT%\Setup.exe" "%SOURCE%\Setup.exe" >nul
if errorlevel 1 (
  echo Could not copy Setup.exe to %SOURCE%.
  exit /b 1
)
set "SHA256="
set "SETUP_PATH=%SOURCE%\Setup.exe"
for /f "delims=" %%H in ('powershell.exe -NoProfile -Command "(Get-FileHash -LiteralPath $env:SETUP_PATH -Algorithm SHA256).Hash.ToLowerInvariant()"') do set "SHA256=%%H"
if not defined SHA256 (
  echo Could not hash Setup.exe.
  del /f /q "%SOURCE%\Setup.exe"
  exit /b 1
)
>"%SOURCE%\Setup.exe.sha256" echo %SHA256%  Setup.exe
if not exist "%SOURCE%\Setup.exe.sha256" exit /b 1
echo Built:
echo   %SOURCE%\Setup.exe
endlocal
