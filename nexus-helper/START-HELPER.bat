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
node nexus-helper.js

echo.
echo   The helper stopped. Press any key to close.
pause >nul
