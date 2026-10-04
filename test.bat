@echo off
rem Builds the launcher and runs its self-tests. Exit code 0 = all passed.
call "%~dp0launcher\build.bat" || exit /b 1
"%~dp0MashupLauncher.exe" --test > "%TEMP%\mashup-launcher-tests.txt" 2>&1
set RC=%ERRORLEVEL%
type "%TEMP%\mashup-launcher-tests.txt"
exit /b %RC%
