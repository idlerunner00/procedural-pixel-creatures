@echo off
setlocal EnableExtensions DisableDelayedExpansion
title Procedural Pixel Creature Workshop
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start.ps1" %*
set "EXITCODE=%ERRORLEVEL%"
rem Keep a failed double-click launch open so the error can be read.
if not "%EXITCODE%"=="0" pause
exit /b %EXITCODE%
