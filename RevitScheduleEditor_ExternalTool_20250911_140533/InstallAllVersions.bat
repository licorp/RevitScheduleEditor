@echo off
echo ===================================
echo REVIT SCHEDULE EDITOR INSTALLER
echo Cai dat cho nhieu phien ban Revit
echo ===================================
echo.

set "CURRENT_DIR=%~dp0"

echo Dang cai dat cho cac phien ban Revit...
echo.

:: Revit 2020
set "REVIT_2020=%APPDATA%\Autodesk\Revit\Addins\2020"
if not exist "%REVIT_2020%" mkdir "%REVIT_2020%"
copy "%CURRENT_DIR%RevitScheduleEditor_External.addin" "%REVIT_2020%\RevitScheduleEditor.addin" >nul 2>&1
copy "%CURRENT_DIR%RevitScheduleEditor.dll" "%REVIT_2020%\" >nul 2>&1
copy "%CURRENT_DIR%RevitScheduleEditor.pdb" "%REVIT_2020%\" >nul 2>&1
if exist "%REVIT_2020%\RevitScheduleEditor.addin" echo [OK] Revit 2020

:: Revit 2021
set "REVIT_2021=%APPDATA%\Autodesk\Revit\Addins\2021"
if not exist "%REVIT_2021%" mkdir "%REVIT_2021%"
copy "%CURRENT_DIR%RevitScheduleEditor_External.addin" "%REVIT_2021%\RevitScheduleEditor.addin" >nul 2>&1
copy "%CURRENT_DIR%RevitScheduleEditor.dll" "%REVIT_2021%\" >nul 2>&1
copy "%CURRENT_DIR%RevitScheduleEditor.pdb" "%REVIT_2021%\" >nul 2>&1
if exist "%REVIT_2021%\RevitScheduleEditor.addin" echo [OK] Revit 2021

:: Revit 2022
set "REVIT_2022=%APPDATA%\Autodesk\Revit\Addins\2022"
if not exist "%REVIT_2022%" mkdir "%REVIT_2022%"
copy "%CURRENT_DIR%RevitScheduleEditor_External.addin" "%REVIT_2022%\RevitScheduleEditor.addin" >nul 2>&1
copy "%CURRENT_DIR%RevitScheduleEditor.dll" "%REVIT_2022%\" >nul 2>&1
copy "%CURRENT_DIR%RevitScheduleEditor.pdb" "%REVIT_2022%\" >nul 2>&1
if exist "%REVIT_2022%\RevitScheduleEditor.addin" echo [OK] Revit 2022

:: Revit 2023
set "REVIT_2023=%APPDATA%\Autodesk\Revit\Addins\2023"
if not exist "%REVIT_2023%" mkdir "%REVIT_2023%"
copy "%CURRENT_DIR%RevitScheduleEditor_External.addin" "%REVIT_2023%\RevitScheduleEditor.addin" >nul 2>&1
copy "%CURRENT_DIR%RevitScheduleEditor.dll" "%REVIT_2023%\" >nul 2>&1
copy "%CURRENT_DIR%RevitScheduleEditor.pdb" "%REVIT_2023%\" >nul 2>&1
if exist "%REVIT_2023%\RevitScheduleEditor.addin" echo [OK] Revit 2023

:: Revit 2024
set "REVIT_2024=%APPDATA%\Autodesk\Revit\Addins\2024"
if not exist "%REVIT_2024%" mkdir "%REVIT_2024%"
copy "%CURRENT_DIR%RevitScheduleEditor_External.addin" "%REVIT_2024%\RevitScheduleEditor.addin" >nul 2>&1
copy "%CURRENT_DIR%RevitScheduleEditor.dll" "%REVIT_2024%\" >nul 2>&1
copy "%CURRENT_DIR%RevitScheduleEditor.pdb" "%REVIT_2024%\" >nul 2>&1
if exist "%REVIT_2024%\RevitScheduleEditor.addin" echo [OK] Revit 2024

:: Revit 2025
set "REVIT_2025=%APPDATA%\Autodesk\Revit\Addins\2025"
if not exist "%REVIT_2025%" mkdir "%REVIT_2025%"
copy "%CURRENT_DIR%RevitScheduleEditor_External.addin" "%REVIT_2025%\RevitScheduleEditor.addin" >nul 2>&1
copy "%CURRENT_DIR%RevitScheduleEditor.dll" "%REVIT_2025%\" >nul 2>&1
copy "%CURRENT_DIR%RevitScheduleEditor.pdb" "%REVIT_2025%\" >nul 2>&1
if exist "%REVIT_2025%\RevitScheduleEditor.addin" echo [OK] Revit 2025

echo.
echo ===================================
echo CAI DAT HOAN TAT!
echo ===================================
echo.
echo Add-in da duoc cai dat cho tat ca cac phien ban Revit.
echo Khoi dong lai Revit de thay Schedule Editor trong menu Add-Ins.
echo.
pause
