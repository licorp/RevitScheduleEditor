# Script cài đặt RevitScheduleEditor cho nhiều phiên bản Revit
# Tác giả: LICORP

param(
    [Parameter(Mandatory=$false)]
    [string[]]$RevitVersions = @("2020", "2021", "2022", "2023", "2024", "2025"),
    
    [Parameter(Mandatory=$false)]
    [string]$SourcePath = ""
)

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

Write-ColorOutput Yellow "=== CÀI ĐẶT REVIT SCHEDULE EDITOR CHO NHIỀU PHIÊN BẢN ==="

# Tự động xác định source path nếu không được cung cấp
if ([string]::IsNullOrEmpty($SourcePath)) {
    $SourcePath = Split-Path -Parent $MyInvocation.MyCommand.Path
}

Write-ColorOutput White "Source Path: $SourcePath"
Write-ColorOutput White "Revit Versions: $($RevitVersions -join ', ')"
Write-Output ""

# Kiểm tra files cần thiết
$requiredFiles = @(
    "RevitScheduleEditor.dll",
    "RevitScheduleEditor_External.addin"
)

foreach ($file in $requiredFiles) {
    $filePath = Join-Path $SourcePath $file
    if (-not (Test-Path $filePath)) {
        Write-ColorOutput Red "✗ Không tìm thấy file: $file"
        Write-ColorOutput Red "Vui lòng chạy package-external-tool.ps1 trước"
        exit 1
    }
}

Write-ColorOutput Green "✓ Tất cả files cần thiết đều có sẵn"
Write-Output ""

# Cài đặt cho từng phiên bản Revit
foreach ($version in $RevitVersions) {
    Write-ColorOutput Cyan "Đang cài đặt cho Revit $version..."
    
    $addinPath = "$env:APPDATA\Autodesk\Revit\Addins\$version"
    
    # Tạo thư mục nếu chưa có
    if (-not (Test-Path $addinPath)) {
        try {
            New-Item -ItemType Directory -Path $addinPath -Force | Out-Null
            Write-ColorOutput Yellow "  Tạo thư mục: $addinPath"
        } catch {
            Write-ColorOutput Red "  ✗ Không thể tạo thư mục: $addinPath"
            continue
        }
    }
    
    # Copy file .addin
    try {
        $sourceAddin = Join-Path $SourcePath "RevitScheduleEditor_External.addin"
        $targetAddin = Join-Path $addinPath "RevitScheduleEditor.addin"
        Copy-Item $sourceAddin $targetAddin -Force
        Write-ColorOutput Green "  ✓ Copied .addin file"
    } catch {
        Write-ColorOutput Red "  ✗ Lỗi copy .addin file: $($_.Exception.Message)"
        continue
    }
    
    # Copy DLL files
    $dllFiles = @("RevitScheduleEditor.dll", "DataGridExtensions.dll")
    foreach ($dll in $dllFiles) {
        $sourceDll = Join-Path $SourcePath $dll
        if (Test-Path $sourceDll) {
            try {
                $targetDll = Join-Path $addinPath $dll
                Copy-Item $sourceDll $targetDll -Force
                Write-ColorOutput Green "  ✓ Copied $dll"
            } catch {
                Write-ColorOutput Red "  ✗ Lỗi copy $dll : $($_.Exception.Message)"
            }
        } else {
            Write-ColorOutput Yellow "  ! Không tìm thấy $dll"
        }
    }
    
    Write-ColorOutput Green "✓ Hoàn thành cài đặt cho Revit $version"
    Write-Output ""
}

Write-ColorOutput Green "=== CÀI ĐẶT HOÀN TẤT ==="
Write-ColorOutput White "Add-in đã được cài đặt cho các phiên bản Revit được chọn."
Write-ColorOutput White "Khởi động lại Revit để thấy Schedule Editor trong menu Add-Ins."
Write-Output ""
Write-ColorOutput Cyan "Vị trí cài đặt:"
foreach ($version in $RevitVersions) {
    $addinPath = "$env:APPDATA\Autodesk\Revit\Addins\$version"
    if (Test-Path $addinPath) {
        Write-ColorOutput White "  Revit $version : $addinPath"
    }
}
