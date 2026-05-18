@echo off
REM Wrapper para o Seed-DemoData.ps1 — permite correr a partir do cmd.exe sem
REM ter de invocar PowerShell manualmente.
REM
REM Uso (no cmd):  .\scripts\Seed-DemoData.bat

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Seed-DemoData.ps1" %*
