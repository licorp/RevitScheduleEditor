@echo off
echo === TAO PACKAGE HOAN CHINH ===

set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\msbuild.exe"

echo Building RevitScheduleEditor...
"%MSBUILD%" RevitScheduleEditor.csproj /p:Configuration=Release /p:Platform=AnyCPU /verbosity:minimal

echo Building Diagnostic Tool...
"%MSBUILD%" DiagnosticTool\RevitAddinDiagnostic.csproj /p:Configuration=Release /p:Platform=AnyCPU /verbosity:minimal

echo Creating package...
set "PKG_DIR=RevitScheduleEditor_Complete_%date:~6,4%%date:~3,2%%date:~0,2%_%time:~0,2%%time:~3,2%%time:~6,2%"
set "PKG_DIR=%PKG_DIR: =0%"

if exist "%PKG_DIR%" rd /s /q "%PKG_DIR%"
mkdir "%PKG_DIR%"

echo Copying files...
copy "bin\Release\RevitScheduleEditor.dll" "%PKG_DIR%\"
copy "bin\Release\RevitScheduleEditor.pdb" "%PKG_DIR%\" 2>nul
copy "RevitScheduleEditor_External.addin" "%PKG_DIR%\"
copy "DiagnosticTool\bin\Release\RevitAddinDiagnostic.exe" "%PKG_DIR%\"
copy "packages\DataGridExtensions.2.7.0\lib\net48\DataGridExtensions.dll" "%PKG_DIR%\" 2>nul

if exist "RevitScheduleEditor_ExternalTool_20250911_140533" (
    copy "RevitScheduleEditor_ExternalTool_20250911_140533\*.md" "%PKG_DIR%\" 2>nul
    copy "RevitScheduleEditor_ExternalTool_20250911_140533\Install*.bat" "%PKG_DIR%\" 2>nul
)

echo.
echo Package created: %PKG_DIR%
echo.
echo Files:
dir /b "%PKG_DIR%"

echo.
echo === COMPLETE ===
pause
