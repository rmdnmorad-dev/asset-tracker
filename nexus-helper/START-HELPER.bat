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

REM ---- find the helper next to this .bat -------------------------------
REM Browsers like to rename downloads, so accept the usual manglings too:
REM   nexus-helper (1).js   nexus-helper.js.txt   nexus-helper.txt
set "HELPER="
if exist "nexus-helper.js" set "HELPER=nexus-helper.js"
if not defined HELPER for %%F in (nexus-helper*.js) do if not defined HELPER set "HELPER=%%~nxF"
if not defined HELPER for %%F in (nexus-helper*.txt) do if not defined HELPER set "HELPER=%%~nxF"

if not defined HELPER (
  echo.
  echo   I cannot find nexus-helper.js in this folder:
  echo      %~dp0
  echo.
  echo   What is actually in there:
  for %%F in (*.*) do echo      %%~nxF
  echo.
  echo   Save nexus-helper.js into that same folder, next to this .bat file,
  echo   and run this again. Both files have to sit together.
  echo.
  echo   If your browser saved it as nexus-helper.js.txt, just rename it
  echo   back to nexus-helper.js  ^(turn on "File name extensions" in the
  echo   Explorer View tab so you can see the real ending^).
  echo.
  pause
  exit /b 1
)

if /i not "%HELPER%"=="nexus-helper.js" echo   using "%HELPER%"

REM Nothing to install - this helper uses only what ships with Node.js.
REM It drives Opera first. If your Opera refuses to open its debugging port it
REM falls back to Chrome / Edge / Brave / Vivaldi on its own.
REM To force one particular browser, un-comment the next line and fix the path:
REM set "BROWSER_PATH=%LOCALAPPDATA%\Programs\Opera\opera.exe"

node "%HELPER%"

echo.
echo   The helper stopped. Press any key to close.
pause >nul
