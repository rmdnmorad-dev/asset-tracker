@echo off
title Timecard - Nexus helper
cd /d "%~dp0"

where node >nul 2>nul
if errorlevel 1 goto nonode

REM ---- find the helper sitting next to this .bat ----------------------
REM Browsers rename downloads, so accept the usual manglings as well:
REM   nexus-helper (1).js    nexus-helper.js.txt    nexus-helper.txt
set "HELPER="
if exist "nexus-helper.js" set "HELPER=nexus-helper.js"
if not defined HELPER call :pick nexus-helper*.js
if not defined HELPER call :pick nexus-helper*.txt
if not defined HELPER goto nohelper
if /i not "%HELPER%"=="nexus-helper.js" echo   using "%HELPER%"

REM Nothing to install - this helper uses only what ships with Node.js.
REM It drives Opera first. If your Opera refuses to open its debugging port
REM it falls back to Chrome / Edge / Brave / Vivaldi on its own.
REM To force one browser, un-comment the next line and fix the path:
REM set "BROWSER_PATH=%LOCALAPPDATA%\Programs\Opera\opera.exe"

node "%HELPER%"

echo.
echo   The helper stopped. Press any key to close.
pause >nul
exit /b

:pick
if exist "%~1" for %%F in (%~1) do if not defined HELPER set "HELPER=%%~nxF"
exit /b

:nonode
echo.
echo   Node.js is not installed on this PC.
echo   Install it from https://nodejs.org (the LTS button), then run this again.
echo.
pause
exit /b 1

:nohelper
echo.
echo   I cannot find nexus-helper.js in this folder:
echo      %~dp0
echo.
echo   What is actually in there:
dir /b
echo.
echo   Save nexus-helper.js into that same folder, right next to this .bat
echo   file. Both files have to sit together - the .bat only starts the .js.
echo.
echo   If Explorer hides file endings, a file that looks like "nexus-helper"
echo   may really be "nexus-helper.js.txt". Turn on View - File name
echo   extensions to see the real name.
echo.
pause
exit /b 1
