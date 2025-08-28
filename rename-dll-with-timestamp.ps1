$src = "./bin/Release/net8.0-windows/RevitScheduleEditor.dll"
$dt = Get-Date -Format "yyyyMMdd_HHmmss"
$dst = "./bin/Release/net8.0-windows/RevitScheduleEditor_$dt.dll"
$src = Join-Path $PSScriptRoot 'bin/Release/net8.0-windows/RevitScheduleEditor.dll'
if (Test-Path $src) {
	$dt = Get-Date -Format 'yyyyMMdd_HHmmss'
	$dst = Join-Path $PSScriptRoot ("bin/Release/net8.0-windows/RevitScheduleEditor_$dt.dll")
	Rename-Item -Path $src -NewName $dst
	Write-Host "Renamed to $dst"
} else {
	Write-Host "Source DLL not found: $src"
}
