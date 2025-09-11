# Script đóng gói RevitScheduleEditor External Tool
# Tác giả: LICORP
# Ngày: $(Get-Date -Format "dd/MM/yyyy")

param(
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [string]$PackageName = "RevitScheduleEditor_ExternalTool"
)

# Màu sắc cho output
function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    } else {
        $input | Write-Output
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

Write-ColorOutput Yellow "=== ĐÓNG GÓI REVIT SCHEDULE EDITOR EXTERNAL TOOL ==="
Write-ColorOutput White "Configuration: $Configuration"
Write-ColorOutput White "Package Name: $PackageName"
Write-Output ""

# Tạo timestamp
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$packageDir = "$PackageName`_$timestamp"

Write-ColorOutput Cyan "Bước 1: Tạo thư mục package..."
if (Test-Path $packageDir) {
    Remove-Item $packageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $packageDir | Out-Null
Write-ColorOutput Green "✓ Đã tạo thư mục: $packageDir"

# Build project trước
Write-ColorOutput Cyan "Bước 2: Build project..."
$buildResult = & msbuild "RevitScheduleEditor.csproj" /p:Configuration=$Configuration /p:Platform=AnyCPU /verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    Write-ColorOutput Red "✗ Build thất bại!"
    exit 1
}
Write-ColorOutput Green "✓ Build thành công"

# Copy các file cần thiết
Write-ColorOutput Cyan "Bước 3: Copy files..."

# Copy DLL chính
$sourceDll = "bin\$Configuration\RevitScheduleEditor.dll"
if (Test-Path $sourceDll) {
    Copy-Item $sourceDll -Destination $packageDir
    Write-ColorOutput Green "✓ Copied: RevitScheduleEditor.dll"
} else {
    Write-ColorOutput Red "✗ Không tìm thấy file DLL: $sourceDll"
    exit 1
}

# Copy PDB file (nếu có)
$sourcePdb = "bin\$Configuration\RevitScheduleEditor.pdb"
if (Test-Path $sourcePdb) {
    Copy-Item $sourcePdb -Destination $packageDir
    Write-ColorOutput Green "✓ Copied: RevitScheduleEditor.pdb"
}

# Copy dependencies
$depsPath = "packages\DataGridExtensions.2.7.0\lib\net48\DataGridExtensions.dll"
if (Test-Path $depsPath) {
    Copy-Item $depsPath -Destination $packageDir
    Write-ColorOutput Green "✓ Copied: DataGridExtensions.dll"
}

# Copy file .addin
Copy-Item "RevitScheduleEditor_External.addin" -Destination $packageDir
Write-ColorOutput Green "✓ Copied: RevitScheduleEditor_External.addin"

# Tạo file README cho package
$readmeContent = @"
# RevitScheduleEditor External Tool Package
Generated: $(Get-Date -Format "dd/MM/yyyy HH:mm:ss")
Configuration: $Configuration

## Cài đặt:

### Phương pháp 1: External Tool (Khuyến nghị)
1. Copy toàn bộ thư mục này vào một vị trí cố định (ví dụ: C:\RevitTools\ScheduleEditor\)
2. Trong Revit, vào External Tools > Configure External Tools
3. Thêm tool mới với thông tin:
   - Title: Schedule Editor
   - Command: [đường dẫn đến thư mục]\RevitScheduleEditor_External.addin
   - Initial Directory: [đường dẫn đến thư mục]
   - Prompt for Arguments: (để trống)

### Phương pháp 2: Manual Add-in
1. Copy file RevitScheduleEditor_External.addin vào thư mục:
   - Revit 2020: %APPDATA%\Autodesk\Revit\Addins\2020\
   - Revit 2021: %APPDATA%\Autodesk\Revit\Addins\2021\
   - Revit 2022: %APPDATA%\Autodesk\Revit\Addins\2022\
   - Revit 2023: %APPDATA%\Autodesk\Revit\Addins\2023\
   - Revit 2024: %APPDATA%\Autodesk\Revit\Addins\2024\
   - Revit 2025: %APPDATA%\Autodesk\Revit\Addins\2025\

2. Đảm bảo tất cả các file DLL ở cùng thư mục với file .addin

## Files bao gồm:
- RevitScheduleEditor.dll (Main assembly)
- RevitScheduleEditor.pdb (Debug symbols)
- DataGridExtensions.dll (Dependency)
- RevitScheduleEditor_External.addin (Add-in manifest)

## Tương thích:
- Revit 2020, 2021, 2022, 2023, 2024, 2025
- .NET Framework 4.8

## Sử dụng:
Sau khi cài đặt, tool sẽ xuất hiện trong ribbon hoặc external tools menu.
Click để mở Schedule Editor window.

## Hỗ trợ:
Liên hệ LICORP để được hỗ trợ.
"@

$readmeContent | Out-File -FilePath "$packageDir\README.txt" -Encoding UTF8
Write-ColorOutput Green "✓ Tạo README.txt"

# Tạo file batch script để cài đặt nhanh
$installScript = @'
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
pause
'@

$installScript | Out-File -FilePath "$packageDir\Install.bat" -Encoding ASCII
Write-ColorOutput Green "✓ Tạo Install.bat"

# Tạo file zip (nếu có 7zip hoặc PowerShell 5.0+)
Write-ColorOutput Cyan "Bước 4: Tạo file zip..."
try {
    $zipPath = "$packageDir.zip"
    if (Get-Command "Compress-Archive" -ErrorAction SilentlyContinue) {
        Compress-Archive -Path "$packageDir\*" -DestinationPath $zipPath -Force
        Write-ColorOutput Green "✓ Tạo file zip: $zipPath"
    } else {
        Write-ColorOutput Yellow "! PowerShell version không hỗ trợ Compress-Archive"
    }
} catch {
    Write-ColorOutput Yellow "! Không thể tạo file zip: $($_.Exception.Message)"
}

Write-Output ""
Write-ColorOutput Green "=== HOÀN THÀNH ==="
Write-ColorOutput White "Package được tạo tại: $packageDir"
Write-ColorOutput White "Kích thước package:"
$size = (Get-ChildItem $packageDir -Recurse | Measure-Object -Property Length -Sum).Sum
Write-ColorOutput White "  $('{0:N2}' -f ($size / 1MB)) MB"

Write-Output ""
Write-ColorOutput Cyan "Files trong package:"
Get-ChildItem $packageDir | ForEach-Object {
    Write-ColorOutput White "  - $($_.Name) ($('{0:N2}' -f ($_.Length / 1KB)) KB)"
}

Write-Output ""
Write-ColorOutput Yellow "Hướng dẫn sử dụng:"
Write-ColorOutput White "1. Copy thư mục $packageDir đến vị trí mong muốn"
Write-ColorOutput White "2. Chạy Install.bat để cài đặt tự động"
Write-ColorOutput White "3. Hoặc làm theo hướng dẫn trong README.txt"
