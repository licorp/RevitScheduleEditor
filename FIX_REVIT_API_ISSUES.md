# KHẮC PHỤC LỖI REVIT API DEPENDENCIES

## Vấn đề phát hiện từ Diagnostic Reports:
```
❌ Could not load file or assembly 'RevitAPIUI, Version=20.0.0.0'
❌ Reference tới Revit 2020 API nhưng chạy trên các version khác
```

## GIẢI PHÁP 1: Tạo Multi-Version Build

### Bước 1: Sửa file .csproj để support multiple Revit versions

```xml
<PropertyGroup>
  <RevitVersion Condition="'$(RevitVersion)' == ''">2020</RevitVersion>
  <DefineConstants>$(DefineConstants);REVIT$(RevitVersion)</DefineConstants>
  <TargetFrameworkVersion Condition="'$(RevitVersion)' == '2020'">v4.8</TargetFrameworkVersion>
  <TargetFrameworkVersion Condition="'$(RevitVersion)' == '2021'">v4.8</TargetFrameworkVersion>
  <TargetFrameworkVersion Condition="'$(RevitVersion)' == '2022'">v4.8</TargetFrameworkVersion>
  <TargetFrameworkVersion Condition="'$(RevitVersion)' == '2023'">v4.8</TargetFrameworkVersion>
  <TargetFrameworkVersion Condition="'$(RevitVersion)' == '2024'">v4.8</TargetFrameworkVersion>
  <TargetFrameworkVersion Condition="'$(RevitVersion)' == '2025'">v4.8</TargetFrameworkVersion>
</PropertyGroup>

<ItemGroup>
  <Reference Include="RevitAPI">
    <HintPath>$(ProgramFiles)\Autodesk\Revit $(RevitVersion)\RevitAPI.dll</HintPath>
    <Private>False</Private>
  </Reference>
  <Reference Include="RevitAPIUI">
    <HintPath>$(ProgramFiles)\Autodesk\Revit $(RevitVersion)\RevitAPIUI.dll</HintPath>
    <Private>False</Private>
  </Reference>
</ItemGroup>
```

### Bước 2: Build script cho multiple versions

```batch
@echo off
echo Building for multiple Revit versions...

for %%v in (2020 2021 2022 2023 2024 2025) do (
    echo Building for Revit %%v...
    msbuild RevitScheduleEditor.csproj /p:RevitVersion=%%v /p:Configuration=Release /p:Platform=AnyCPU
    
    if exist "bin\Release\RevitScheduleEditor.dll" (
        mkdir "bin\Revit%%v" 2>nul
        copy "bin\Release\RevitScheduleEditor.dll" "bin\Revit%%v\"
        copy "bin\Release\RevitScheduleEditor.pdb" "bin\Revit%%v\" 2>nul
        echo [OK] Built for Revit %%v
    )
)
```

## GIẢI PHÁP 2: Assembly Binding Redirects (Khuyến nghị)

### Tạo file app.config cho add-in:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <runtime>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <!-- Redirect Revit 2020 references to any available version -->
      <dependentAssembly>
        <assemblyIdentity name="RevitAPI" culture="neutral" />
        <bindingRedirect oldVersion="20.0.0.0" newVersion="20.0.0.0-26.0.0.0" />
      </dependentAssembly>
      <dependentAssembly>
        <assemblyIdentity name="RevitAPIUI" culture="neutral" />
        <bindingRedirect oldVersion="20.0.0.0" newVersion="20.0.0.0-26.0.0.0" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
</configuration>
```

## GIẢI PHÁP 3: Late Binding (Tốt nhất cho compatibility)

### Sửa code để load RevitAPI dynamically:

```csharp
public class ShowScheduleEditorCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            // Load RevitAPI assemblies dynamically
            LoadRevitAPIs();
            
            // Your existing code here
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return Result.Failed;
        }
    }
    
    private void LoadRevitAPIs()
    {
        var revitPath = GetRevitInstallPath();
        if (!string.IsNullOrEmpty(revitPath))
        {
            var apiPath = Path.Combine(revitPath, "RevitAPI.dll");
            var apiUIPath = Path.Combine(revitPath, "RevitAPIUI.dll");
            
            if (File.Exists(apiPath))
                Assembly.LoadFrom(apiPath);
            if (File.Exists(apiUIPath))
                Assembly.LoadFrom(apiUIPath);
        }
    }
}
```

## GIẢI PHÁP 4: Deployment Script (Dễ nhất)

### Tạo smart installer detect Revit version và copy đúng files:

```batch
@echo off
echo SMART REVIT SCHEDULE EDITOR INSTALLER
echo ======================================

set "SOURCE_DIR=%~dp0"

:: Detect available Revit versions and copy appropriate DLLs
for %%v in (2020 2021 2022 2023 2024 2025 2026) do (
    set "REVIT_DIR=C:\Program Files\Autodesk\Revit %%v"
    set "ADDINS_DIR=%APPDATA%\Autodesk\Revit\Addins\%%v"
    
    if exist "!REVIT_DIR!" (
        echo Found Revit %%v
        
        :: Create addins directory
        if not exist "!ADDINS_DIR!" mkdir "!ADDINS_DIR!"
        
        :: Copy our addin files
        copy "%SOURCE_DIR%RevitScheduleEditor.dll" "!ADDINS_DIR!\"
        copy "%SOURCE_DIR%RevitScheduleEditor_External.addin" "!ADDINS_DIR!\RevitScheduleEditor.addin"
        
        :: Copy dependencies (important!)
        copy "%SOURCE_DIR%DataGridExtensions.dll" "!ADDINS_DIR!\" 2>nul
        
        echo [OK] Installed for Revit %%v
    )
)
```

## GIẢI PHÁP 5: Enhanced Diagnostic Tool Update

### Thêm chức năng fix tự động trong diagnostic tool:

```csharp
private void FixMissingDependencies(string addinPath)
{
    var addinDir = Path.GetDirectoryName(addinPath);
    var revitVersions = GetInstalledRevitVersions();
    
    foreach (var version in revitVersions)
    {
        var revitApiPath = Path.Combine(version.Value, "RevitAPI.dll");
        var revitApiUIPath = Path.Combine(version.Value, "RevitAPIUI.dll");
        
        if (File.Exists(revitApiPath) && File.Exists(revitApiUIPath))
        {
            // Create symbolic links or copy references
            CreateApiReference(addinDir, revitApiPath, revitApiUIPath);
            break;
        }
    }
}
```

## KHUYẾN NGHỊ:

### ✅ **Giải pháp nhanh nhất (cho user hiện tại):**
1. Sử dụng **GIẢI PHÁP 4** - Smart installer
2. Detect Revit version trên máy
3. Copy files vào đúng thư mục

### ✅ **Giải pháp lâu dài (cho developer):**
1. Implement **GIẢI PHÁP 2** - Binding redirects
2. Hoặc **GIẢI PHÁP 3** - Late binding
3. Build universal add-in compatible với tất cả versions

### 🔧 **Action items:**
1. Update project references để flexible
2. Tạo smart installer script
3. Test trên multiple Revit versions
4. Update diagnostic tool với auto-fix features
