@echo off
setlocal enabledelayedexpansion

echo ========================================
echo   UNIVERSAL REVIT SCHEDULE EDITOR
echo   Compatible with Revit 2020-2026
echo ========================================
echo.

for /f "tokens=1-4 delims=/ " %%a in ('date /t') do set mydate=%%d%%b%%c
for /f "tokens=1-2 delims=: " %%a in ('time /t') do set mytime=%%a%%b
set TIMESTAMP=%mydate%_%mytime%

set SOURCE_DIR=%~dp0
set PACKAGE_DIR=%~dp0RevitScheduleEditor_Universal_%TIMESTAMP%
set DLL_NAME=RevitScheduleEditor_Universal_20250911_173835.dll
set PDB_NAME=RevitScheduleEditor_Universal_20250911_173835.pdb

echo Creating Universal package directory...
mkdir "%PACKAGE_DIR%"

echo.
echo === Copying Universal files ===
copy "%~dp0bin\Release\%DLL_NAME%" "%PACKAGE_DIR%\RevitScheduleEditor_Universal.dll"
copy "%~dp0bin\Release\%PDB_NAME%" "%PACKAGE_DIR%\RevitScheduleEditor_Universal.pdb"
copy "%~dp0RevitScheduleEditor_Universal.addin" "%PACKAGE_DIR%\"

echo.
echo === Copying Dependencies ===
mkdir "%PACKAGE_DIR%\Dependencies"
if exist "%~dp0bin\Release\DataGridExtensions.dll" copy "%~dp0bin\Release\DataGridExtensions.dll" "%PACKAGE_DIR%\Dependencies\"

echo.
echo === Creating Smart Universal Installer ===
(
echo @echo off
echo echo ================================================
echo echo   UNIVERSAL REVIT SCHEDULE EDITOR INSTALLER
echo echo   Compatible with Revit 2020-2026
echo echo ================================================
echo echo.
echo.
echo echo Detecting Revit installations...
echo set FOUND_VERSIONS=
echo.
echo REM Check for each Revit version
echo for %%%%v in (2026 2025 2024 2023 2022 2021 2020^) do (
echo     if exist "%%%%ProgramFiles%%%%\Autodesk\Revit %%%%v" (
echo         echo Found Revit %%%%v
echo         set FOUND_VERSIONS=!FOUND_VERSIONS! %%%%v
echo     ^)
echo ^)
echo.
echo if "%%%%FOUND_VERSIONS%%%%"=="" (
echo     echo ERROR: No Revit installations found!
echo     echo Please install Revit 2020-2026 first.
echo     pause
echo     exit /b 1
echo ^)
echo.
echo echo Installing for versions: %%%%FOUND_VERSIONS%%%%
echo echo.
echo.
echo REM Install for each found version
echo for %%%%v in (%%%%FOUND_VERSIONS%%%%^) do (
echo     echo Installing for Revit %%%%v...
echo     set "ADDIN_DIR=%%%%APPDATA%%%%\Autodesk\Revit\Addins\%%%%v"
echo     
echo     REM Create directories
echo     if not exist "!ADDIN_DIR!" mkdir "!ADDIN_DIR!"
echo     if not exist "!ADDIN_DIR!\RevitScheduleEditor" mkdir "!ADDIN_DIR!\RevitScheduleEditor"
echo     
echo     REM Copy files
echo     copy "RevitScheduleEditor_Universal.dll" "!ADDIN_DIR!\RevitScheduleEditor\" ^>nul
echo     copy "RevitScheduleEditor_Universal.pdb" "!ADDIN_DIR!\RevitScheduleEditor\" ^>nul
echo     copy "RevitScheduleEditor_Universal.addin" "!ADDIN_DIR!\" ^>nul
echo     
echo     REM Copy dependencies
echo     if exist "Dependencies\*.dll" copy "Dependencies\*.dll" "!ADDIN_DIR!\RevitScheduleEditor\" ^>nul
echo     
echo     echo   - Installed to !ADDIN_DIR!
echo ^)
echo.
echo echo ================================================
echo echo Installation completed successfully!
echo echo.
echo echo The Universal Schedule Editor will appear in the
echo echo Add-Ins tab when you start any Revit version.
echo echo ================================================
echo pause
) > "%PACKAGE_DIR%\Install_Universal.bat"

echo.
echo === Creating README ===
(
echo UNIVERSAL REVIT SCHEDULE EDITOR
echo ================================
echo.
echo COMPATIBILITY:
echo - Revit 2020, 2021, 2022, 2023, 2024, 2025, 2026
echo - Windows 10/11
echo - .NET Framework 4.8
echo.
echo INSTALLATION:
echo 1. Run "Install_Universal.bat" as Administrator
echo 2. The installer will detect all installed Revit versions
echo 3. Add-in will be installed for each found version
echo 4. Restart Revit to see the new command
echo.
echo Created: %date% %time%
) > "%PACKAGE_DIR%\README_Universal.txt"

echo.
echo ================================================
echo Universal package created successfully!
echo.
echo Location: %PACKAGE_DIR%
echo ================================================
echo.
pause
