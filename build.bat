@echo off
setlocal
cd /d "%~dp0"

REM 只用 Windows 自带的 .NET Framework 编译器，不需要 .NET SDK / Visual Studio。
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" goto :nocsc

if not exist "dist" mkdir "dist"

"%CSC%" /nologo /target:winexe /optimize+ /warn:4 /out:"dist\MeditationTimer.exe" /win32manifest:"src\app.manifest" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "src\Countdown.cs" "src\OverlaySettings.cs" "src\HotkeyBinding.cs" "src\Dialogs.cs" "src\DarkMenuRenderer.cs" "src\TrayIcon.cs" "src\OverlayForm.cs" "src\AssemblyInfo.cs" "src\Program.cs"
if errorlevel 1 goto :failed

echo.
for %%f in ("dist\MeditationTimer.exe") do echo 已生成 %%~ff
for %%f in ("dist\MeditationTimer.exe") do echo 体积 %%~zf 字节
exit /b 0

:nocsc
echo 没有找到 .NET Framework 自带的 csc.exe，请确认系统里装了 .NET Framework 4.x。
pause
exit /b 1

:failed
echo 编译失败。
pause
exit /b 1

