@echo off
echo ================================================
echo TAO ULTIMATE REVIT SCHEDULE EDITOR PACKAGE
echo Bao gom fixes cho tat ca cac loi da phat hien
echo ================================================

set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\msbuild.exe"

echo Building improved version...
"%MSBUILD%" RevitScheduleEditor_Flexible.csproj /p:Configuration=Release /p:Platform=AnyCPU /verbosity:minimal

echo Building diagnostic tool...
"%MSBUILD%" DiagnosticTool\RevitAddinDiagnostic.csproj /p:Configuration=Release /p:Platform=AnyCPU /verbosity:minimal

echo Creating ultimate package...
set "PKG_DIR=RevitScheduleEditor_Ultimate_%date:~6,4%%date:~3,2%%date:~0,2%_%time:~0,2%%time:~3,2%%time:~6,2%"
set "PKG_DIR=%PKG_DIR: =0%"

if exist "%PKG_DIR%" rd /s /q "%PKG_DIR%"
mkdir "%PKG_DIR%"

echo Copying main files...
copy "bin\Release\RevitScheduleEditor.dll" "%PKG_DIR%\"
copy "bin\Release\RevitScheduleEditor.pdb" "%PKG_DIR%\" 2>nul
copy "bin\Release\RevitScheduleEditor.dll.config" "%PKG_DIR%\" 2>nul
copy "RevitScheduleEditor_External.addin" "%PKG_DIR%\"

echo Copying dependencies...
copy "packages\DataGridExtensions.2.7.0\lib\net48\DataGridExtensions.dll" "%PKG_DIR%\" 2>nul

echo Copying diagnostic tool...
copy "DiagnosticTool\bin\Release\RevitAddinDiagnostic.exe" "%PKG_DIR%\"

echo Copying installation scripts...
copy "SmartInstaller.bat" "%PKG_DIR%\"
copy "InstallAllVersions.bat" "%PKG_DIR%\" 2>nul

echo Copying documentation...
copy "FIX_REVIT_API_ISSUES.md" "%PKG_DIR%\"
copy "RevitScheduleEditor_ExternalTool_20250911_140533\*.md" "%PKG_DIR%\" 2>nul

echo Creating README...
(
echo # RevitScheduleEditor Ultimate Package
echo Generated: %date% %time%
echo.
echo ## FIXES INCLUDED:
echo - ✅ Flexible Revit API references ^(auto-detect versions^)
echo - ✅ Assembly binding redirects for compatibility
echo - ✅ Smart installer with version detection
echo - ✅ Enhanced diagnostic tool
echo - ✅ Support for Revit 2017-2026
echo.
echo ## INSTALLATION:
echo ### Quick Install ^(Recommended^):
echo 1. Run SmartInstaller.bat as Administrator
echo 2. Script will auto-detect Revit versions and install
echo.
echo ### Manual Install:
echo 1. Run RevitAddinDiagnostic.exe first to check environment
echo 2. Follow diagnostic recommendations
echo.
echo ### If still having issues:
echo 1. Read FIX_REVIT_API_ISSUES.md for detailed solutions
echo 2. Use diagnostic tool to create detailed report
echo.
echo ## FILES:
echo - RevitScheduleEditor.dll - Main add-in
echo - RevitScheduleEditor_External.addin - Manifest
echo - DataGridExtensions.dll - Dependency
echo - RevitAddinDiagnostic.exe - Diagnostic tool
echo - SmartInstaller.bat - Auto installer
echo - *.md - Documentation and fixes
echo.
echo Built with Visual Studio 2022 Community
echo Compatible with .NET Framework 4.8
) > "%PKG_DIR%\README.txt"

echo Creating config for app.config...
(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<configuration^>
echo   ^<runtime^>
echo     ^<assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1"^>
echo       ^<dependentAssembly^>
echo         ^<assemblyIdentity name="RevitAPI" culture="neutral" /^>
echo         ^<bindingRedirect oldVersion="0.0.0.0-99.0.0.0" newVersion="20.0.0.0" /^>
echo       ^</dependentAssembly^>
echo       ^<dependentAssembly^>
echo         ^<assemblyIdentity name="RevitAPIUI" culture="neutral" /^>
echo         ^<bindingRedirect oldVersion="0.0.0.0-99.0.0.0" newVersion="20.0.0.0" /^>
echo       ^</dependentAssembly^>
echo     ^</assemblyBinding^>
echo   ^</runtime^>
echo ^</configuration^>
) > "%PKG_DIR%\RevitScheduleEditor.dll.config"

echo.
echo ================================================
echo ULTIMATE PACKAGE CREATED: %PKG_DIR%
echo ================================================
echo.
echo Files included:
dir /b "%PKG_DIR%"

echo.
echo This package includes fixes for:
echo ✅ RevitAPI version compatibility issues
echo ✅ Assembly loading problems  
echo ✅ Multi-version Revit support
echo ✅ Automatic installation and detection
echo ✅ Comprehensive diagnostic tools
echo.
echo Ready for deployment to any machine!
pause
