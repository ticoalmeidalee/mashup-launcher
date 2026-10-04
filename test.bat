@echo off
rem Builds the launcher, then a console copy of it in %TEMP% that runs the self-tests (a GUI exe started from a
rem batch file doesn't reliably hand back its output). Exit code 0 = all passed.
setlocal
call "%~dp0launcher\build.bat" || exit /b 1
set FW=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
if not defined CSC set "CSC=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
set OUT=%TEMP%\mashup-launcher-tests-%RANDOM%.exe
"%CSC%" /nologo /noconfig /target:exe /out:"%OUT%" /r:%FW%\mscorlib.dll /r:%FW%\System.dll /r:%FW%\System.Core.dll /r:%FW%\System.Drawing.dll /r:%FW%\System.Windows.Forms.dll /r:%FW%\System.Web.Extensions.dll /r:%FW%\System.IO.Compression.dll /r:%FW%\System.IO.Compression.FileSystem.dll /recurse:"%~dp0launcher\*.cs" || exit /b 1
"%OUT%" --test --root "%~dp0."
set RC=%ERRORLEVEL%
del "%OUT%" 2>nul
exit /b %RC%
