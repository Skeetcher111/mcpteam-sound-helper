@echo off
rem Builds MCPteamSoundHelper.exe with the C# compiler that ships with every Windows 10/11 (.NET Framework 4.x)
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /platform:anycpu /win32icon:src\soundhelper.ico /out:MCPteamSoundHelper.exe src\SoundHelper.cs
