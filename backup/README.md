# RevitScheduleEditor - Backup Files

## Build Information
- **Build Date**: August 30, 2025 - 04:19:54
- **Configuration**: Debug
- **Target Framework**: .NET Framework 4.8
- **Revit Version**: 2020

## Files Backed Up

### Version 1 (Original)
- `RevitScheduleEditor_20250830_041954.dll` - Main assembly
- `RevitScheduleEditor_20250830_041954.pdb` - Debug symbols

### Version 2 (With Debug Logging)
- `RevitScheduleEditor_20250830_043024.dll` - Main assembly with debug logging
- `RevitScheduleEditor_20250830_043024.pdb` - Debug symbols

## Build Process
1. Built using MSBuild from Visual Studio 2022
2. XAML files compiled successfully
3. DLL renamed with timestamp using PowerShell script
4. Files backed up to this directory

## Debug Logging Features (Version 2)
- Added comprehensive debug logging throughout the application
- Uses OutputDebugStringA() for DebugView compatibility
- Logs to both Debug.WriteLine and DebugView
- Covers:
  - Command execution
  - Window initialization
  - ViewModel operations
  - Schedule data loading
  - Model updates
  - Parameter changes
  - Error handling

## Usage with DebugView
1. Download and run DebugView from Microsoft Sysinternals
2. Enable "Capture Win32" in DebugView
3. Load the plugin in Revit
4. Monitor real-time debug output with prefix "[ScheduleEditor]"

## Notes
- This is a Revit API plugin for editing schedules
- Built successfully with no warnings or errors
- Ready for deployment to Revit 2020
- Use Version 2 for debugging and troubleshooting
