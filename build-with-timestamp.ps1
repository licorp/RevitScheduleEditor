# Script build RevitScheduleEditor với timestamp
param(
    [string]$Configuration = "Release"
)

# Đường dẫn MSBuild
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

# Kiểm tra MSBuild có tồn tại không
if (-not (Test-Path $msbuild)) {
    Write-Error "MSBuild không tìm thấy tại: $msbuild"
    exit 1
}

Write-Host "=== BUILDING REVIT SCHEDULE EDITOR ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow

# Build project
& $msbuild "RevitScheduleEditor.csproj" "/p:Configuration=$Configuration" "/p:Platform=AnyCPU" "/verbosity:minimal"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build thất bại!"
    exit $LASTEXITCODE
}

Write-Host "Build thành công!" -ForegroundColor Green

# Đổi tên file DLL với timestamp
$outputPath = "bin\$Configuration"
$originalDll = Join-Path $outputPath "RevitScheduleEditor.dll"
$originalPdb = Join-Path $outputPath "RevitScheduleEditor.pdb"

if (Test-Path $originalDll) {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $newDllName = "RevitScheduleEditor_$timestamp.dll"
    $newPdbName = "RevitScheduleEditor_$timestamp.pdb"
    
    $newDllPath = Join-Path $outputPath $newDllName
    $newPdbPath = Join-Path $outputPath $newPdbName
    
    # Đổi tên DLL
    Rename-Item -Path $originalDll -NewName $newDllName
    Write-Host "Đã đổi tên DLL: $newDllPath" -ForegroundColor Cyan
    
    # Đổi tên PDB nếu có
    if (Test-Path $originalPdb) {
        Rename-Item -Path $originalPdb -NewName $newPdbName
        Write-Host "Đã đổi tên PDB: $newPdbPath" -ForegroundColor Cyan
    }
    
    Write-Host "=== BUILD HOÀN THÀNH ===" -ForegroundColor Green
    Write-Host "File output: $newDllPath" -ForegroundColor Yellow
} else {
    Write-Warning "Không tìm thấy file DLL tại: $originalDll"
}
