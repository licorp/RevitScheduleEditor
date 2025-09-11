# Script tạo package hoàn chỉnh
param([string]$Configuration = "Release")

Write-Host "=== TẠO PACKAGE HOÀN CHỈNH ===" -ForegroundColor Yellow

# Build projects
Write-Host "Building RevitScheduleEditor..." -ForegroundColor Cyan
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\msbuild.exe"
& $msbuild "RevitScheduleEditor.csproj" /p:Configuration=$Configuration /p:Platform=AnyCPU /verbosity:minimal

Write-Host "Building Diagnostic Tool..." -ForegroundColor Cyan
& $msbuild "DiagnosticTool\RevitAddinDiagnostic.csproj" /p:Configuration=$Configuration /p:Platform=AnyCPU /verbosity:minimal

# Create package
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$packageDir = "RevitScheduleEditor_Complete_$timestamp"

Write-Host "Creating package: $packageDir" -ForegroundColor Cyan
if (Test-Path $packageDir) { Remove-Item $packageDir -Recurse -Force }
New-Item -ItemType Directory -Path $packageDir | Out-Null

# Copy files
Write-Host "Copying files..." -ForegroundColor Cyan
Copy-Item "bin\$Configuration\RevitScheduleEditor.dll" -Destination $packageDir
Copy-Item "bin\$Configuration\RevitScheduleEditor.pdb" -Destination $packageDir -ErrorAction SilentlyContinue
Copy-Item "RevitScheduleEditor_External.addin" -Destination $packageDir
Copy-Item "DiagnosticTool\bin\$Configuration\RevitAddinDiagnostic.exe" -Destination $packageDir

# Copy dependency
$depsPath = "packages\DataGridExtensions.2.7.0\lib\net48\DataGridExtensions.dll"
if (Test-Path $depsPath) {
    Copy-Item $depsPath -Destination $packageDir
}

# Copy documentation from existing package
if (Test-Path "RevitScheduleEditor_ExternalTool_20250911_140533") {
    Copy-Item "RevitScheduleEditor_ExternalTool_20250911_140533\*.md" -Destination $packageDir -ErrorAction SilentlyContinue
    Copy-Item "RevitScheduleEditor_ExternalTool_20250911_140533\InstallAllVersions.bat" -Destination $packageDir -ErrorAction SilentlyContinue
    Copy-Item "RevitScheduleEditor_ExternalTool_20250911_140533\Install.bat" -Destination $packageDir -ErrorAction SilentlyContinue
}

Write-Host "✓ Package created: $packageDir" -ForegroundColor Green

# List files
Write-Host ""
Write-Host "Files in package:" -ForegroundColor Cyan
Get-ChildItem $packageDir | ForEach-Object {
    Write-Host "  - $($_.Name)" -ForegroundColor White
}

Write-Host ""
Write-Host "=== COMPLETE ===" -ForegroundColor Green
