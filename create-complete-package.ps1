# Script tạo package hoàn chỉnh với Diagnostic Tool
param(
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release"
)

function Write-ColorOutput($ForegroundColor, $Message) {
    Write-Host $Message -ForegroundColor $ForegroundColor
}

Write-ColorOutput Yellow "=== TẠO PACKAGE VỚI DIAGNOSTIC TOOL ==="
Write-Output ""

# Build RevitScheduleEditor
Write-ColorOutput Cyan "Bước 1: Build RevitScheduleEditor..."
$msbuildPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\msbuild.exe"
& $msbuildPath "RevitScheduleEditor.csproj" /p:Configuration=$Configuration /p:Platform=AnyCPU /verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    Write-ColorOutput Red "Build RevitScheduleEditor thất bại!"
    exit 1
}
Write-ColorOutput Green "✓ Build RevitScheduleEditor thành công"

# Build Diagnostic Tool
Write-ColorOutput Cyan "Bước 2: Build Diagnostic Tool..."
& $msbuildPath "DiagnosticTool\RevitAddinDiagnostic.csproj" /p:Configuration=$Configuration /p:Platform=AnyCPU /verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    Write-ColorOutput Red "Build Diagnostic Tool thất bại!"
    exit 1
}
Write-ColorOutput Green "✓ Build Diagnostic Tool thành công"

# Tạo package directory
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$packageDir = "RevitScheduleEditor_Complete_$timestamp"

Write-ColorOutput Cyan "Bước 3: Tạo package directory..."
if (Test-Path $packageDir) {
    Remove-Item $packageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $packageDir | Out-Null
Write-ColorOutput Green "✓ Tạo thư mục: $packageDir"

# Copy RevitScheduleEditor files
Write-ColorOutput Cyan "Bước 4: Copy RevitScheduleEditor files..."
Copy-Item "bin\$Configuration\RevitScheduleEditor.dll" -Destination $packageDir
Copy-Item "bin\$Configuration\RevitScheduleEditor.pdb" -Destination $packageDir
Copy-Item "RevitScheduleEditor_External.addin" -Destination $packageDir

# Copy dependencies
$depsPath = "packages\DataGridExtensions.2.7.0\lib\net48\DataGridExtensions.dll"
if (Test-Path $depsPath) {
    Copy-Item $depsPath -Destination $packageDir
    Write-ColorOutput Green "✓ Copied: DataGridExtensions.dll"
}

Write-ColorOutput Green "✓ Copied RevitScheduleEditor files"

# Copy Diagnostic Tool
Write-ColorOutput Cyan "Bước 5: Copy Diagnostic Tool..."
Copy-Item "DiagnosticTool\bin\$Configuration\RevitAddinDiagnostic.exe" -Destination $packageDir
Write-ColorOutput Green "✓ Copied: RevitAddinDiagnostic.exe"

# Tạo các file hướng dẫn và scripts
Write-ColorOutput Cyan "Bước 6: Tạo documentation và scripts..."

# Main README
$mainReadme = @'
# RevitScheduleEditor Complete Package

## Nội dung package:
- **RevitScheduleEditor**: Add-in chính cho Revit
- **Diagnostic Tool**: Tool chẩn đoán lỗi và debug
- **Installation Scripts**: Scripts cài đặt tự động
- **Documentation**: Hướng dẫn chi tiết

## Quick Start:

### 1. Kiểm tra môi trường trước (Khuyến nghị)
```
RevitAddinDiagnostic.exe
```
Click "Kiểm tra Môi trường" và "Kiểm tra Revit"

### 2. Cài đặt Add-in
**Cách 1: Tự động (Dễ nhất)**
```
InstallAllVersions.bat
```

**Cách 2: External Tool (Cho test)**
```
Install.bat
```

### 3. Nếu có lỗi
1. Chạy `RevitAddinDiagnostic.exe`
2. Click "Kiểm tra Add-in"
3. Chọn file `.addin` để phân tích
4. Lưu report và gửi cho support

## Files:
- `RevitScheduleEditor.dll` - Main add-in
- `RevitScheduleEditor_External.addin` - Add-in manifest
- `DataGridExtensions.dll` - Dependency
- `RevitAddinDiagnostic.exe` - Diagnostic tool
- `Install*.bat` - Installation scripts
- `QuickTest.bat` - Quick .addin file test
- `*.md` - Documentation

## Hỗ trợ:
- Đọc `DIAGNOSTIC_TOOL_GUIDE.md` để debug lỗi
- Đọc `HUONG_DAN_CAI_DAT.md` để cài đặt chi tiết
'@

$mainReadme | Out-File -FilePath "$packageDir\README.md" -Encoding UTF8
Write-ColorOutput Green "✓ Tạo README.md"

# Installation scripts
$installAllBat = @'
@echo off
echo ===================================
echo REVIT SCHEDULE EDITOR INSTALLER
echo Cai dat cho nhieu phien ban Revit
echo ===================================
echo.

set "CURRENT_DIR=%~dp0"

echo Kiem tra Diagnostic Tool...
if exist "%CURRENT_DIR%RevitAddinDiagnostic.exe" (
    echo [INFO] Co Diagnostic Tool - chay truoc khi cai dat de kiem tra moi truong
    echo.
)

echo Dang cai dat cho cac phien ban Revit...
echo.

:: Revit versions array
set "versions=2020 2021 2022 2023 2024 2025"

for %%v in (%versions%) do (
    set "REVIT_DIR=%APPDATA%\Autodesk\Revit\Addins\%%v"
    if not exist "!REVIT_DIR!" mkdir "!REVIT_DIR!" 2>nul
    
    copy "%CURRENT_DIR%RevitScheduleEditor_External.addin" "!REVIT_DIR!\RevitScheduleEditor.addin" >nul 2>&1
    copy "%CURRENT_DIR%RevitScheduleEditor.dll" "!REVIT_DIR!\" >nul 2>&1
    copy "%CURRENT_DIR%RevitScheduleEditor.pdb" "!REVIT_DIR!\" >nul 2>&1
    copy "%CURRENT_DIR%DataGridExtensions.dll" "!REVIT_DIR!\" >nul 2>&1
    
    if exist "!REVIT_DIR!\RevitScheduleEditor.addin" echo [OK] Revit %%v
)

echo.
echo ===================================
echo CAI DAT HOAN TAT!
echo ===================================
echo.
echo Add-in da duoc cai dat cho tat ca cac phien ban Revit.
echo Khoi dong lai Revit de thay Schedule Editor trong menu Add-Ins.
echo.
echo Neu co loi, chay RevitAddinDiagnostic.exe de kiem tra.
echo.
pause
'@

$installAllBat | Out-File -FilePath "$packageDir\InstallAllVersions.bat" -Encoding ASCII
Write-ColorOutput Green "✓ Tạo InstallAllVersions.bat"

# External tool installer
$installExternalBat = @'
@echo off
echo ===================================
echo EXTERNAL TOOL INSTALLER
echo ===================================
echo.

set "CURRENT_DIR=%~dp0"
set "TARGET_BASE=C:\RevitTools\ScheduleEditor"

echo Creating target directory...
if not exist "%TARGET_BASE%" mkdir "%TARGET_BASE%"

echo Copying files...
xcopy "%CURRENT_DIR%*" "%TARGET_BASE%\" /Y /Q

echo.
echo ===================================
echo Installation completed!
echo ===================================
echo.
echo Files copied to: %TARGET_BASE%
echo.
echo Next steps:
echo 1. Open Revit
echo 2. Go to External Tools ^> Configure External Tools
echo 3. Add new tool:
echo    - Title: Schedule Editor
echo    - Command: %TARGET_BASE%\RevitScheduleEditor_External.addin
echo    - Initial Directory: %TARGET_BASE%
echo.
echo Debug tool available at: %TARGET_BASE%\RevitAddinDiagnostic.exe
echo.
pause
'@

$installExternalBat | Out-File -FilePath "$packageDir\InstallAsExternalTool.bat" -Encoding ASCII
Write-ColorOutput Green "✓ Tạo InstallAsExternalTool.bat"

# Quick test script
$quickTestBat = @'
@echo off
echo ====================================
echo REVIT ADDIN QUICK TEST
echo ====================================
echo.

if "%~1"=="" (
    echo Test file .addin hien tai:
    set "ADDIN_FILE=%~dp0RevitScheduleEditor_External.addin"
) else (
    set "ADDIN_FILE=%~1"
)

echo Kiem tra file: %ADDIN_FILE%
echo.

if not exist "%ADDIN_FILE%" (
    echo [ERROR] File khong ton tai!
    pause
    exit /b
)

echo [OK] File ton tai

findstr /C:"<?xml" "%ADDIN_FILE%" >nul
if errorlevel 1 (
    echo [ERROR] Khong phai file XML!
) else (
    echo [OK] File XML hop le
)

findstr /C:"<AddIn" "%ADDIN_FILE%" >nul
if errorlevel 1 (
    echo [ERROR] Khong co tag AddIn!
) else (
    echo [OK] Co tag AddIn
)

for /f "tokens=2 delims=<>" %%a in ('findstr /C:"<Assembly>" "%ADDIN_FILE%" 2^>nul') do set "ASSEMBLY_PATH=%%a"

if defined ASSEMBLY_PATH (
    echo Assembly: %ASSEMBLY_PATH%
    
    echo %ASSEMBLY_PATH% | findstr /C:":" >nul
    if errorlevel 1 (
        set "FULL_ASSEMBLY=%~dp0%ASSEMBLY_PATH%"
    ) else (
        set "FULL_ASSEMBLY=%ASSEMBLY_PATH%"
    )
    
    if exist "%FULL_ASSEMBLY%" (
        echo [OK] Assembly ton tai: %FULL_ASSEMBLY%
    ) else (
        echo [ERROR] Assembly khong ton tai: %FULL_ASSEMBLY%
    )
) else (
    echo [ERROR] Khong tim thay Assembly path!
)

echo.
echo De kiem tra chi tiet hon, chay: RevitAddinDiagnostic.exe
echo.
pause
'@

$quickTestBat | Out-File -FilePath "$packageDir\QuickTest.bat" -Encoding ASCII
Write-ColorOutput Green "✓ Tạo QuickTest.bat"

# Copy existing documentation
if (Test-Path "RevitScheduleEditor_ExternalTool_20250911_140533\HUONG_DAN_CAI_DAT.md") {
    Copy-Item "RevitScheduleEditor_ExternalTool_20250911_140533\HUONG_DAN_CAI_DAT.md" -Destination $packageDir
    Write-ColorOutput Green "✓ Copied HUONG_DAN_CAI_DAT.md"
}

if (Test-Path "RevitScheduleEditor_ExternalTool_20250911_140533\DIAGNOSTIC_TOOL_GUIDE.md") {
    Copy-Item "RevitScheduleEditor_ExternalTool_20250911_140533\DIAGNOSTIC_TOOL_GUIDE.md" -Destination $packageDir
    Write-ColorOutput Green "✓ Copied DIAGNOSTIC_TOOL_GUIDE.md"
}

# Tạo ZIP package
Write-ColorOutput Cyan "Bước 7: Tạo ZIP package..."
try {
    $zipPath = "$packageDir.zip"
    Compress-Archive -Path "$packageDir\*" -DestinationPath $zipPath -Force
    Write-ColorOutput Green "✓ Tạo file ZIP: $zipPath"
} catch {
    Write-ColorOutput Yellow "! Không thể tạo ZIP: $($_.Exception.Message)"
}

# Summary
Write-Output ""
Write-ColorOutput Green "=== HOÀN THÀNH ==="
Write-ColorOutput White "Complete package: $packageDir"

$size = (Get-ChildItem $packageDir -Recurse | Measure-Object -Property Length -Sum).Sum
Write-ColorOutput White "Tổng kích thước: $([math]::Round($size / 1MB, 2)) MB"

Write-Output ""
Write-ColorOutput Cyan "Files trong package:"
Get-ChildItem $packageDir | Sort-Object Name | ForEach-Object {
    $fileSize = [math]::Round($_.Length / 1KB, 2)
    Write-ColorOutput White "  - $($_.Name) ($fileSize KB)"
}

Write-Output ""
Write-ColorOutput Yellow "Hướng dẫn sử dụng:"
Write-ColorOutput White "1. Giải nén package ra máy cần cài đặt"
Write-ColorOutput White "2. Chạy RevitAddinDiagnostic.exe để kiểm tra môi trường"
Write-ColorOutput White "3. Chạy InstallAllVersions.bat để cài đặt"
Write-ColorOutput White "4. Nếu có lỗi, dùng Diagnostic Tool để debug"
