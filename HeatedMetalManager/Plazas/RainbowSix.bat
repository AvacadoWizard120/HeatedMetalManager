@echo off
echo Starting..
start "" RainbowSix.exe /belaunch
echo Press any button to close the game...
pause >nul
echo Killing...
TASKKILL.EXE /IM RainbowSix.exe /F
TASKKILL.EXE /IM RainbowSixGame.exe /F
TASKKILL.EXE /IM RainbowSix_Vulkan.exe /F
pause
echo.