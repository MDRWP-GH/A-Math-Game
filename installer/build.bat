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

"%CSC%" /nologo /target:winexe /optimize+ /out:Setup.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll Setup.cs PackedPayload.cs AppInfo.cs
if errorlevel 1 exit /b 1

echo Built stubs:
echo   %CD%\uninstall.exe
echo   %CD%\Setup.exe
echo Unity Windows player builds pack the game into Setup.exe automatically.

popd
endlocal
