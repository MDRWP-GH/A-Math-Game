@echo off
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Could not find csc.exe. Install .NET Framework 4.x developer pack, or build from a Windows machine.
  exit /b 1
)

pushd "%~dp0"

"%CSC%" /nologo /target:winexe /optimize+ /out:uninstall.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Uninstall.cs AppInfo.cs
if errorlevel 1 exit /b 1

"%CSC%" /nologo /target:winexe /optimize+ /out:Setup.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Setup.cs AppInfo.cs
if errorlevel 1 exit /b 1

echo Built:
echo   %CD%\uninstall.exe
echo   %CD%\Setup.exe

if exist "%~dp0..\Build\Windows\A-Math.exe" (
  copy /Y uninstall.exe "%~dp0..\Build\Windows\uninstall.exe" > nul
  copy /Y Setup.exe "%~dp0..\Build\Windows\Setup.exe" > nul
  echo Copied into Build\Windows
)

popd
endlocal
