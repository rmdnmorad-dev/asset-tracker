@echo off
title Timecard - Nexus helper
cd /d "%~dp0"

where node >nul 2>nul
if errorlevel 1 goto nonode

REM ---- find the helper sitting next to this .bat ----------------------
REM Browsers rename downloads, so take the name loosely: nexus-helper.js,
REM nexus-helper (1).js, NexusHelper.js, nexus-helper.js.txt - and if there
REM is only one .js file in the folder at all, that must be it.
set "HELPER="
if exist "nexus-helper.js" set "HELPER=nexus-helper.js"
if not defined HELPER call :pick nexus-helper*.js
if not defined HELPER call :pick *nexus*.js
if not defined HELPER call :pick *helper*.js
if not defined HELPER call :only
if not defined HELPER call :pick *nexus*.txt
if not defined HELPER goto nohelper
if /i not "%HELPER%"=="nexus-helper.js" echo   using "%HELPER%"

REM Nothing to install - this helper uses only what ships with Node.js.
REM It drives whatever browser Windows has set as your default. If that one
REM cannot be automated (Firefox) or will not open a debugging port, it
REM falls back to Chrome / Edge / Opera / Brave / Vivaldi on its own.
REM To force one browser, un-comment the next line and fix the path:
REM set "BROWSER_PATH=%LOCALAPPDATA%\Programs\Opera\opera.exe"

REM The browser this helper starts is the one the rockets open tabs in, so
REM your Timecard should live in it too. Keep Timecard*.html next to this
REM .bat (or on your Desktop) and it opens automatically as the first tab.
REM To point at one exact file, un-comment and fix this:
REM set "TIMECARD=%USERPROFILE%\Desktop\Timecard_3.0.html"

node "%HELPER%"

echo.
echo   The helper stopped. Press any key to close.
pause >nul
exit /b

:pick
if exist "%~1" for %%F in (%~1) do if not defined HELPER set "HELPER=%%~nxF"
exit /b

REM If there is exactly one .js file in the folder, it can only be the helper.
:only
set /a JSN=0
for %%F in (*.js) do (set /a JSN+=1 & set "JS1=%%~nxF")
if %JSN%==1 set "HELPER=%JS1%"
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
