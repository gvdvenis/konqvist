@echo off
REM Run this script as administrator
powershell -ExecutionPolicy Bypass -File "%~dp0DevUtils\setup-devcert.ps1"
if %errorlevel% equ 1 (
    REM The PowerShell script already explained the admin requirement.
    exit /b 1
)
if %errorlevel% neq 0 (
    echo.
    echo There was an unexpected error running the PowerShell script.
    exit /b %errorlevel%
)
pause
