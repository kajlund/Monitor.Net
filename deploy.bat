@echo off
powershell.exe -NoProfile -File "%~dp0deploy.ps1"
if errorlevel 1 pause