$src = Join-Path $PSScriptRoot 'bin/Release/RevitScheduleEditor.dll'
if (Test-Path $src) {
	$dt = Get-Date -Format 'yyyyMMdd_HHmmss'
	$dst = Join-Path $PSScriptRoot ("bin/Release/RevitScheduleEditor_$dt.dll")
	Rename-Item -Path $src -NewName $dst
	Write-Host "Renamed to $dst"
} else {
	Write-Host "Source DLL not found: $src"
}
