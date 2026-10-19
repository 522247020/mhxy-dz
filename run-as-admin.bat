@echo off
setlocal
cd /d "%~dp0"

REM 游戏一般是管理员权限运行的，Windows 不允许普通进程接收高权限窗口的按键。
REM 想让全局快捷键在游戏窗口聚焦时也生效，小窗就要以管理员身份运行。
net session >nul 2>&1
if not errorlevel 1 goto :elevated

echo 正在申请管理员权限…
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
echo 如果 UAC 被拒绝，请右键本文件选“以管理员身份运行”。
pause
exit /b 1

:elevated
if not exist "dist\MeditationTimer.exe" call "%~dp0build.bat"
if not exist "dist\MeditationTimer.exe" exit /b 1
start "" "dist\MeditationTimer.exe"
exit /b 0
