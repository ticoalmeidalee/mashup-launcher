@echo off
rem Builds ..\MashupLauncher.exe from every .cs here (Roslyn; .NET Framework 4.8 ships with Windows 10/11).
rem CSC overrides the compiler (CI sets it); default: VS 2022 Build Tools.
setlocal
set FW=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
if not defined CSC set "CSC=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
"%CSC%" /nologo /noconfig /target:winexe /optimize+ /out:"%~dp0..\MashupLauncher.exe" /r:%FW%\mscorlib.dll /r:%FW%\System.dll /r:%FW%\System.Core.dll /r:%FW%\System.Drawing.dll /r:%FW%\System.Windows.Forms.dll /r:%FW%\System.Web.Extensions.dll /r:%FW%\System.IO.Compression.dll /r:%FW%\System.IO.Compression.FileSystem.dll /recurse:"%~dp0*.cs"
