@echo off
echo ===================================
echo REVIT SCHEDULE EDITOR INSTALLER
echo ===================================
echo.

set "CURRENT_DIR=%~dp0"
set "TARGET_BASE=C:\RevitTools\ScheduleEditor"

echo Creating target directory...
if not exist "%TARGET_BASE%" mkdir "%TARGET_BASE%"

echo Copying files...
xcopy "%CURRENT_DIR%*" "%TARGET_BASE%\" /Y /Q

echo.
echo Installation completed!
echo Files copied to: %TARGET_BASE%
echo.
pause
