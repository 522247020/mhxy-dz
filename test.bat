@echo off
setlocal
cd /d "%~dp0"

REM 编译并运行逻辑自检：倒计时状态机、环形进度轮廓、快捷键换算、配置读写。
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" goto :nocsc

if not exist "build" mkdir "build"

"%CSC%" /nologo /target:exe /optimize+ /warn:4 /out:"build\SelfTest.exe" /win32manifest:"src\app.manifest" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "src\Countdown.cs" "src\OverlaySettings.cs" "src\HotkeyBinding.cs" "tests\SelfTest.cs"
if errorlevel 1 goto :failed

echo.
"build\SelfTest.exe"
if errorlevel 1 goto :failed
exit /b 0

:nocsc
echo 没有找到 .NET Framework 自带的 csc.exe，请确认系统里装了 .NET Framework 4.x。
pause
exit /b 1

:failed
echo 测试没有通过。
pause
exit /b 1
