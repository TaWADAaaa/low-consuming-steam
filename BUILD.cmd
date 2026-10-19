@echo off
powershell.exe -NoProfile -File "%~dp0scripts\Build.ps1" -Product Steam
if errorlevel 1 pause
