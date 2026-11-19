@echo off
rem Builds the version to share with friends into the "WinUltra for Friends" folder on your desktop.
rem Same island and clock, but everything is the person's choice: a setup window on first run,
rem no self-copying, no keyboard watching or key typing, no floating-taskbar code.
rem Your own build (Build.cmd -> WinUltra.exe) is not touched.
cd /d "%~dp0"
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set WPF=%FW%\WPF
set WINMD=C:\Program Files (x86)\Windows Kits\10\UnionMetadata\10.0.26100.0\Windows.winmd
rem Find the Desktop folder (it may have been moved, for example into OneDrive).
powershell -NoProfile -Command "[Environment]::GetFolderPath('DesktopDirectory')" > "%TEMP%\winultra-desktop.txt"
set /p DESK=<"%TEMP%\winultra-desktop.txt"
del "%TEMP%\winultra-desktop.txt"
set OUT=%DESK%\WinUltra for Friends
if not exist "%OUT%" mkdir "%OUT%"
if not exist WinUltra.ico powershell -NoProfile -File MakeIcon.ps1
"%FW%\csc.exe" /nologo /optimize+ /target:winexe /platform:x64 /define:NO_TASKBAR;FRIENDS /out:"%OUT%\WinUltra.exe" /win32icon:WinUltra.ico /win32manifest:friends.manifest ^
 /r:"%WPF%\PresentationFramework.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\WindowsBase.dll" /r:"%FW%\System.Xaml.dll" ^
 /r:"%FW%\System.Windows.Forms.dll" /r:"%FW%\System.Drawing.dll" ^
 /r:"%FW%\System.Management.dll" /r:"%FW%\System.Web.Extensions.dll" /r:"%FW%\System.IO.Compression.dll" /r:"%FW%\System.IO.Compression.FileSystem.dll" ^
 /r:"%FW%\System.Runtime.dll" /r:"%FW%\System.Runtime.WindowsRuntime.dll" /r:"%FW%\System.Runtime.InteropServices.WindowsRuntime.dll" ^
 /r:"%WINMD%" ^
 /resource:fonts\Anurati-Regular.otf,WinUltra.Anurati.otf /resource:fonts\Montserrat-Light.ttf,WinUltra.Montserrat.ttf /resource:WinUltra.ico,WinUltra.ico ^
 src\WinUltra.cs src\Island.cs src\Clock.cs src\Friends.cs
