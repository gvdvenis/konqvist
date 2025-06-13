@echo off

REM ========================================================================
REM Run this script as administrator
REM ========================================================================

powershell -ExecutionPolicy Bypass -File "%~dp0.dev-utils\setup-devcert.ps1"

if %errorlevel% equ 1 (
    REM exit if not in administrator mode
    exit /b 1
)

if %errorlevel% neq 0 (
    echo.
    echo There was an unexpected error running the PowerShell script.
    exit /b %errorlevel%
)

pause
