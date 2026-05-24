@echo off
title AI2U Ultimate Fix - Configurator
echo ============================================
echo   AI2U Ultimate Fix - Configurator
echo ============================================
echo.

:: Check for Python
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Python is not installed or not in PATH.
    echo Please install Python 3.8+ from https://www.python.org/downloads/
    echo Make sure to check "Add Python to PATH" during installation.
    echo.
    pause
    exit /b 1
)

:: Run the configurator
echo Starting configurator...
python "%~dp0AI2U_Configurator.py"

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Configurator encountered an error.
    pause
)
