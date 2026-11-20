@echo off
rem Builds WinUltra.exe: one file with the icon inside. It runs from wherever it is kept and shows a
rem setup window on first run, where each part (start-up, shortcut, clipboard history, notification
rem pop-ups, frosted glass, Caps Lock pop-up) is switched on or off by the person using it.
rem Needs only what ships with Windows 11 plus the Windows SDK metadata for the WinRT APIs.
cd /d "%~dp0"
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set WPF=%FW%\WPF
set WINMD=C:\Program Files (x86)\Windows Kits\10\UnionMetadata\10.0.26100.0\Windows.winmd
if not exist WinUltra.ico powershell -NoProfile -File MakeIcon.ps1
"%FW%\csc.exe" /nologo /optimize+ /target:winexe /platform:x64 /define:FRIENDS /out:WinUltra.exe /win32icon:WinUltra.ico /win32manifest:WinUltra.manifest ^
 /r:"%WPF%\PresentationFramework.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\WindowsBase.dll" /r:"%FW%\System.Xaml.dll" ^
 /r:"%FW%\System.Windows.Forms.dll" /r:"%FW%\System.Drawing.dll" ^
 /r:"%FW%\System.Management.dll" /r:"%FW%\System.Web.Extensions.dll" /r:"%FW%\System.IO.Compression.dll" /r:"%FW%\System.IO.Compression.FileSystem.dll" ^
 /r:"%FW%\System.Runtime.dll" /r:"%FW%\System.Runtime.WindowsRuntime.dll" /r:"%FW%\System.Runtime.InteropServices.WindowsRuntime.dll" ^
 /r:"%WINMD%" ^
 /resource:WinUltra.ico,WinUltra.ico ^
 src\WinUltra.cs src\Island.cs src\Setup.cs
