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

if not exist "node_modules" (
  echo.
  echo   First run - installing what the helper needs. This takes a few minutes...
  echo.
  call npm install
  if errorlevel 1 (
    echo.
    echo   Install failed. If your company blocks npm, tell Claude and we will
    echo   use a different approach.
    echo.
    pause
    exit /b 1
  )
)

node nexus-helper.js
echo.
echo   The helper stopped. Press any key to close.
pause >nul
