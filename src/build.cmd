@echo off
REM Builds SteamLinkScreenSelector.exe with the logo embedded as the application icon.
REM Requires only the .NET Framework (csc.exe), already present on Windows.

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe

"%CSC%" /nologo /target:winexe /win32icon:app.ico /out:..\SteamLinkScreenSelector.exe ^
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ^
  SteamLinkScreenSelector.cs

if %ERRORLEVEL%==0 (
  echo.
  echo Build OK -^> ..\SteamLinkScreenSelector.exe
) else (
  echo.
  echo Build FAILED
)
pause
