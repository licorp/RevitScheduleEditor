@echo off
setlocal enabledelayedexpansion
echo =====================================
echo SMART REVIT SCHEDULE EDITOR INSTALLER
echo Phat hien va cai dat tu dong
echo =====================================
echo.

set "SOURCE_DIR=%~dp0"
set "INSTALLED_COUNT=0"

echo Kiem tra cac phien ban Revit da cai dat...
echo.

:: Check each Revit version from 2017 to 2026
for %%v in (2017 2018 2019 2020 2021 2022 2023 2024 2025 2026) do (
    set "REVIT_DIR=C:\Program Files\Autodesk\Revit %%v"
    
    if exist "!REVIT_DIR!" (
        echo [FOUND] Revit %%v: !REVIT_DIR!
        
        :: Check if RevitAPI.dll exists
        if exist "!REVIT_DIR!\RevitAPI.dll" (
            echo         RevitAPI.dll: OK
            
            :: Prepare addins directory
            set "ADDINS_DIR=%APPDATA%\Autodesk\Revit\Addins\%%v"
            if not exist "!ADDINS_DIR!" (
                mkdir "!ADDINS_DIR!"
                echo         Created addins directory
            )
            
            :: Copy main files
            copy "!SOURCE_DIR!RevitScheduleEditor.dll" "!ADDINS_DIR!\" >nul 2>&1
            copy "!SOURCE_DIR!RevitScheduleEditor_External.addin" "!ADDINS_DIR!\RevitScheduleEditor.addin" >nul 2>&1
            
            :: Copy dependencies
            copy "!SOURCE_DIR!DataGridExtensions.dll" "!ADDINS_DIR!\" >nul 2>&1
            copy "!SOURCE_DIR!RevitScheduleEditor.pdb" "!ADDINS_DIR!\" >nul 2>&1
            
            :: Verify installation
            if exist "!ADDINS_DIR!\RevitScheduleEditor.addin" (
                echo         [OK] Cai dat thanh cong cho Revit %%v
                set /a INSTALLED_COUNT+=1
            ) else (
                echo         [ERROR] Cai dat that bai cho Revit %%v
            )
        ) else (
            echo         [WARNING] Khong tim thay RevitAPI.dll
        )
        echo.
    )
)

echo =====================================
echo KET QUA CAI DAT
echo =====================================
echo Tong so phien ban da cai dat: !INSTALLED_COUNT!
echo.

if !INSTALLED_COUNT! gtr 0 (
    echo [SUCCESS] Add-in da duoc cai dat thanh cong!
    echo.
    echo Huong dan su dung:
    echo 1. Mo Revit
    echo 2. Vao Add-Ins tab trong ribbon
    echo 3. Tim "Schedule Editor" va click de su dung
    echo.
    echo Neu van gap loi, chay RevitAddinDiagnostic.exe de kiem tra.
) else (
    echo [ERROR] Khong tim thay Revit nao tren may!
    echo.
    echo Vui long:
    echo 1. Kiem tra Revit da duoc cai dat chua
    echo 2. Chay script voi quyen Administrator
    echo 3. Su dung RevitAddinDiagnostic.exe de kiem tra chi tiet
)

echo.
echo Nhan phim bat ky de dong...
pause >nul
