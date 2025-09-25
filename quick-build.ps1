# Quick build script - NO timestamp
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

Write-Host "Building RevitScheduleEditor (NO timestamp)..." -ForegroundColor Green
& $msbuild "RevitScheduleEditor.csproj" "/p:Configuration=Release" "/p:Platform=AnyCPU" "/v:q"

if ($LASTEXITCODE -eq 0) {
    $dll = "bin\Release\RevitScheduleEditor.dll"
    if (Test-Path $dll) {
        Write-Host "Build thanh cong: bin\Release\RevitScheduleEditor.dll" -ForegroundColor Cyan
        
        # Hien thi thong tin file
        $fileInfo = Get-Item $dll
        Write-Host "Kich thuoc: $($fileInfo.Length) bytes" -ForegroundColor Yellow
        Write-Host "Ngay build: $($fileInfo.LastWriteTime)" -ForegroundColor Yellow
    }
} else {
    Write-Host "Build that bai!" -ForegroundColor Red
}
