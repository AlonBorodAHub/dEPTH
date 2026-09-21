@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0HubSessionSetup.ps1" -Mode Remove
pause
