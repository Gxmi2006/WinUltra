@echo off
rem Builds WinUltra.exe, one file with everything inside (fonts and icon included).
rem Needs only what ships with Windows 11 plus the Windows SDK metadata for the WinRT APIs.
cd /d "%~dp0"
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set WPF=%FW%\WPF
set WINMD=C:\Program Files (x86)\Windows Kits\10\UnionMetadata\10.0.26100.0\Windows.winmd
if not exist WinUltra.ico powershell -NoProfile -File MakeIcon.ps1
"%FW%\csc.exe" /nologo /optimize+ /target:winexe /platform:x64 /out:WinUltra.exe /win32icon:WinUltra.ico ^
 /r:"%WPF%\PresentationFramework.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\WindowsBase.dll" /r:"%FW%\System.Xaml.dll" ^
 /r:"%FW%\System.Windows.Forms.dll" /r:"%FW%\System.Drawing.dll" ^
 /r:"%FW%\System.Management.dll" /r:"%FW%\System.Web.Extensions.dll" /r:"%FW%\System.IO.Compression.dll" /r:"%FW%\System.IO.Compression.FileSystem.dll" ^
 /r:"%FW%\System.Runtime.dll" /r:"%FW%\System.Runtime.WindowsRuntime.dll" /r:"%FW%\System.Runtime.InteropServices.WindowsRuntime.dll" ^
 /r:"%WINMD%" ^
 /resource:fonts\Anurati-Regular.otf,WinUltra.Anurati.otf /resource:fonts\Montserrat-Light.ttf,WinUltra.Montserrat.ttf /resource:WinUltra.ico,WinUltra.ico ^
 src\WinUltra.cs src\Island.cs
