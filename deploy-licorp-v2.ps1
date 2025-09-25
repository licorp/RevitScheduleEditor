# Deploy Licorp Schedule Editor V2 with Data Tools panel
Write-Host "=== DEPLOYING LICORP SCHEDULE EDITOR V2 ===" -ForegroundColor Green

$targetPath = "C:\ProgramData\Autodesk\Revit\Addins\2020"

# Stop Revit processes first
Write-Host "Stopping Revit processes..." -ForegroundColor Yellow
Get-Process "Revit" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Copy DLL V2
Write-Host "Copying DLL V2..." -ForegroundColor Yellow
Copy-Item "bin\Release\LicorpScheduleEditorV2.dll" $targetPath -Force

# Copy PDB V2
Write-Host "Copying PDB V2..." -ForegroundColor Yellow
Copy-Item "bin\Release\LicorpScheduleEditorV2.pdb" $targetPath -Force

# Copy ADDIN V2
Write-Host "Copying ADDIN V2..." -ForegroundColor Yellow
Copy-Item "LicorpScheduleEditorV2.addin" $targetPath -Force

# Copy ICON
Write-Host "Copying ICON..." -ForegroundColor Yellow
if (Test-Path "icons8-edit-property-windows-10.png") {
    Copy-Item "icons8-edit-property-windows-10.png" $targetPath -Force
} else {
    Write-Warning "Icon file not found!"
}

Write-Host "=== DEPLOYMENT COMPLETE ===" -ForegroundColor Green
Write-Host "Tab: Licorp" -ForegroundColor Cyan
Write-Host "Panel: Data Tools" -ForegroundColor Cyan
Write-Host "Files deployed to: $targetPath" -ForegroundColor Cyan

# List deployed files
Write-Host "`nDeployed files:" -ForegroundColor Yellow
Get-ChildItem "$targetPath\LicorpScheduleEditorV2*" | Select-Object Name, Length, LastWriteTime
Get-ChildItem "$targetPath\icons8-edit-property-windows-10.png" -ErrorAction SilentlyContinue | Select-Object Name, Length, LastWriteTime

Write-Host "`n*** Ready to test in Revit 2020! ***" -ForegroundColor Green