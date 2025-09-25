# Deploy Licorp Schedule Editor voi icon
Write-Host "=== DEPLOYING LICORP SCHEDULE EDITOR ===" -ForegroundColor Green

$targetPath = "C:\ProgramData\Autodesk\Revit\Addins\2020"

# Copy DLL
Write-Host "Copying DLL..." -ForegroundColor Yellow
Copy-Item "bin\Release\LicorpScheduleEditor.dll" $targetPath -Force

# Copy PDB  
Write-Host "Copying PDB..." -ForegroundColor Yellow
Copy-Item "bin\Release\LicorpScheduleEditor.pdb" $targetPath -Force

# Copy ADDIN
Write-Host "Copying ADDIN..." -ForegroundColor Yellow
Copy-Item "LicorpScheduleEditor.addin" $targetPath -Force

# Copy ICON
Write-Host "Copying ICON..." -ForegroundColor Yellow
if (Test-Path "icons8-edit-property-windows-10.png") {
    Copy-Item "icons8-edit-property-windows-10.png" $targetPath -Force
} else {
    Write-Warning "Icon file not found!"
}

# Update addin file path
Write-Host "Updating addin file paths..." -ForegroundColor Yellow
$addinFile = Join-Path $targetPath "LicorpScheduleEditor.addin"
$content = Get-Content $addinFile
$content = $content -replace "<Assembly>LicorpScheduleEditor.dll</Assembly>", "<Assembly>$targetPath\LicorpScheduleEditor.dll</Assembly>"
$content | Set-Content $addinFile

Write-Host "=== DEPLOYMENT COMPLETE ===" -ForegroundColor Green
Write-Host "Files deployed to: $targetPath" -ForegroundColor Cyan

# List deployed files
Write-Host "`nDeployed files:" -ForegroundColor Yellow
Get-ChildItem "$targetPath\Licorp*" | Select-Object Name, Length, LastWriteTime
Get-ChildItem "$targetPath\icons8-edit-property-windows-10.png" -ErrorAction SilentlyContinue | Select-Object Name, Length, LastWriteTime