@echo off
echo ====================================
echo REVIT ADDIN QUICK TEST
echo ====================================
echo.

if "%~1"=="" (
    echo Su dung: QuickTest.bat "duong_dan_file.addin"
    echo Vi du: QuickTest.bat "C:\RevitTools\MyAddin.addin"
    pause
    exit /b
)

set "ADDIN_FILE=%~1"
echo Kiem tra file: %ADDIN_FILE%
echo.

:: Check if file exists
if not exist "%ADDIN_FILE%" (
    echo [ERROR] File khong ton tai!
    pause
    exit /b
)

echo [OK] File ton tai

:: Check if it's XML file
findstr /C:"<?xml" "%ADDIN_FILE%" >nul
if errorlevel 1 (
    echo [ERROR] Khong phai file XML!
    pause
    exit /b
)

echo [OK] File XML hop le

:: Check AddIn tag
findstr /C:"<AddIn" "%ADDIN_FILE%" >nul
if errorlevel 1 (
    echo [ERROR] Khong co tag AddIn!
    pause
    exit /b
)

echo [OK] Co tag AddIn

:: Extract assembly path
for /f "tokens=2 delims=<>" %%a in ('findstr /C:"<Assembly>" "%ADDIN_FILE%"') do set "ASSEMBLY_PATH=%%a"

echo Assembly: %ASSEMBLY_PATH%

:: Check if assembly path is relative or absolute
echo %ASSEMBLY_PATH% | findstr /C:":" >nul
if errorlevel 1 (
    echo [INFO] Duong dan tuong doi
    set "FULL_ASSEMBLY=%~dp1%ASSEMBLY_PATH%"
) else (
    echo [INFO] Duong dan tuyet doi  
    set "FULL_ASSEMBLY=%ASSEMBLY_PATH%"
)

echo Duong dan day du: %FULL_ASSEMBLY%

:: Check if assembly exists
if exist "%FULL_ASSEMBLY%" (
    echo [OK] Assembly ton tai
) else (
    echo [ERROR] Assembly khong ton tai!
)

echo.
echo ====================================
echo HOAN THANH KIEM TRA
echo ====================================
pause
