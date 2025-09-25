# Script build RevitScheduleEditor khong co timestamp
param(
    [string]$Configuration = "Release"
)

# Duong dan MSBuild
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

# Kiem tra MSBuild co ton tai khong
if (-not (Test-Path $msbuild)) {
    Write-Error "MSBuild khong tim thay tai: $msbuild"
    exit 1
}

Write-Host "=== BUILDING REVIT SCHEDULE EDITOR ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Output: RevitScheduleEditor.dll (KHONG co timestamp)" -ForegroundColor Cyan

# Build project
& $msbuild "RevitScheduleEditor.csproj" "/p:Configuration=$Configuration" "/p:Platform=AnyCPU" "/verbosity:minimal"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build that bai!"
    exit $LASTEXITCODE
}

Write-Host "Build thanh cong!" -ForegroundColor Green

# Kiem tra file output
$outputPath = "bin\$Configuration"
$dllPath = Join-Path $outputPath "RevitScheduleEditor.dll"
$pdbPath = Join-Path $outputPath "RevitScheduleEditor.pdb"

if (Test-Path $dllPath) {
    Write-Host "=== BUILD HOAN THANH ===" -ForegroundColor Green
    Write-Host "File DLL: $dllPath" -ForegroundColor Yellow
    if (Test-Path $pdbPath) {
        Write-Host "File PDB: $pdbPath" -ForegroundColor Yellow
    }
    
    # Hien thi thong tin file
    $fileInfo = Get-Item $dllPath
    Write-Host "Kich thuoc: $($fileInfo.Length) bytes" -ForegroundColor Cyan
    Write-Host "Ngay tao: $($fileInfo.CreationTime)" -ForegroundColor Cyan
} else {
    Write-Warning "Khong tim thay file DLL tai: $dllPath"
}