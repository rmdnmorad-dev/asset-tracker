@echo off
title Timecard - Nexus helper
cd /d "%~dp0"

where node >nul 2>nul
if errorlevel 1 (
  echo.
  echo   Node.js is not installed on this PC.
  echo   Install it from https://nodejs.org  ^(the LTS button^), then run this file again.
  echo.
  pause
  exit /b 1
)

REM Nothing to install - this helper uses only what ships with Node.js.
REM It drives Opera first. If your Opera refuses to open its debugging port it
REM falls back to Chrome / Edge / Brave / Vivaldi on its own.
REM To force one particular browser, un-comment the next line and fix the path:
REM set "BROWSER_PATH=%LOCALAPPDATA%\Programs\Opera\opera.exe"

node nexus-helper.js

echo.
echo   The helper stopped. Press any key to close.
pause >nul
