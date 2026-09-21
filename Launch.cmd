@echo off
if not exist "%~dp0bin\Depth.exe" powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if exist "%~dp0bin\Depth.exe" start "" "%~dp0bin\Depth.exe"
