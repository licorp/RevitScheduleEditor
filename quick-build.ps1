# Quick build script
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

Write-Host "Building RevitScheduleEditor..." -ForegroundColor Green
& $msbuild "RevitScheduleEditor.csproj" "/p:Configuration=Release" "/p:Platform=AnyCPU" "/v:q"

if ($LASTEXITCODE -eq 0) {
    $dll = "bin\Release\RevitScheduleEditor.dll"
    if (Test-Path $dll) {
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $newName = "RevitScheduleEditor_$timestamp.dll"
        Rename-Item $dll $newName
        Write-Host "✅ Build thành công: bin\Release\$newName" -ForegroundColor Cyan
        
        # Rename PDB too
        $pdb = "bin\Release\RevitScheduleEditor.pdb"
        if (Test-Path $pdb) {
            $newPdbName = "RevitScheduleEditor_$timestamp.pdb"
            Rename-Item $pdb $newPdbName
        }
    }
} else {
    Write-Host "❌ Build thất bại!" -ForegroundColor Red
}
