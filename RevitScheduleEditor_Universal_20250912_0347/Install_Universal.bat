@echo off
echo ================================================
echo   UNIVERSAL REVIT SCHEDULE EDITOR INSTALLER
echo   Compatible with Revit 2020-2026
echo ================================================
echo.

echo Detecting Revit installations...
set FOUND_VERSIONS=

REM Check for each Revit version
for %%v in (2026 2025 2024 2023 2022 2021 2020) do (
    if exist "%%ProgramFiles%%\Autodesk\Revit %%v" (
        echo Found Revit %%v
        set FOUND_VERSIONS= %%v
    )
)

if "%%FOUND_VERSIONS%%"=="" (
    echo ERROR: No Revit installations found
    echo Please install Revit 2020-2026 first.
    pause
    exit /b 1
)

echo Installing for versions: %%FOUND_VERSIONS%%
echo.

REM Install for each found version
for %%v in (%%FOUND_VERSIONS%%) do (
    echo Installing for Revit %%v...
    set "ADDIN_DIR=%%APPDATA%%\Autodesk\Revit\Addins\%%v"
ECHO is off.
    REM Create directories
    if not exist "" mkdir ""
    if not exist "\RevitScheduleEditor" mkdir "\RevitScheduleEditor"
ECHO is off.
    REM Copy files
    copy "RevitScheduleEditor_Universal.dll" "\RevitScheduleEditor\" >nul
    copy "RevitScheduleEditor_Universal.pdb" "\RevitScheduleEditor\" >nul
    copy "RevitScheduleEditor_Universal.addin" "\" >nul
ECHO is off.
    REM Copy dependencies
    if exist "Dependencies\*.dll" copy "Dependencies\*.dll" "\RevitScheduleEditor\" >nul
ECHO is off.
    echo   - Installed to 
)

echo ================================================
echo Installation completed successfully
echo.
echo The Universal Schedule Editor will appear in the
echo Add-Ins tab when you start any Revit version.
echo ================================================
pause
