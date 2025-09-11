using System;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Linq;

namespace RevitScheduleEditor
{
    // Universal command interface
    public interface IUniversalRevitCommand
    {
        object Execute(object commandData, ref string message, object elements);
    }

    // Universal command that works with any Revit version
    public class UniversalScheduleEditorCommand : IUniversalRevitCommand
    {
        private static Type _documentType;
        private static Type _uiDocumentType;
        private static Type _externalCommandDataType;
        private static Type _resultType;
        private static Assembly _revitAPIAssembly;
        private static Assembly _revitAPIUIAssembly;
        private static Version _revitVersion;

        // Static constructor để load APIs
        static UniversalScheduleEditorCommand()
        {
            LoadRevitAPIs();
        }

        private static void LoadRevitAPIs()
        {
            try
            {
                DebugLogStatic("=== Universal Command Initializing ===");
                
                // Detect Revit installation và load appropriate APIs
                var revitPath = DetectRevitInstallation();
                if (string.IsNullOrEmpty(revitPath))
                {
                    throw new InvalidOperationException("No Revit installation found");
                }

                DebugLogStatic($"Found Revit at: {revitPath}");

                // Load RevitAPI assemblies
                var apiPath = Path.Combine(revitPath, "RevitAPI.dll");
                var apiUIPath = Path.Combine(revitPath, "RevitAPIUI.dll");

                if (!File.Exists(apiPath) || !File.Exists(apiUIPath))
                {
                    throw new FileNotFoundException($"RevitAPI assemblies not found at {revitPath}");
                }

                _revitAPIAssembly = Assembly.LoadFrom(apiPath);
                _revitAPIUIAssembly = Assembly.LoadFrom(apiUIPath);

                DebugLogStatic("RevitAPI assemblies loaded successfully");

                // Get version
                _revitVersion = _revitAPIAssembly.GetName().Version;
                DebugLogStatic($"Revit API Version: {_revitVersion}");

                // Cache important types
                _documentType = _revitAPIAssembly.GetType("Autodesk.Revit.DB.Document");
                _externalCommandDataType = _revitAPIUIAssembly.GetType("Autodesk.Revit.UI.ExternalCommandData");
                _resultType = _revitAPIAssembly.GetType("Autodesk.Revit.DB.Result");

                DebugLogStatic("Universal command initialized successfully");
            }
            catch (Exception ex)
            {
                DebugLogStatic($"Failed to initialize universal command: {ex.Message}");
                throw;
            }
        }

        private static string DetectRevitInstallation()
        {
            // Check for Revit versions from newest to oldest
            var versions = new[] { "2026", "2025", "2024", "2023", "2022", "2021", "2020" };
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            foreach (var version in versions)
            {
                var path = Path.Combine(programFiles, "Autodesk", $"Revit {version}");
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "RevitAPI.dll")))
                {
                    DebugLogStatic($"Found Revit {version}");
                    return path;
                }
            }

            return null;
        }

        private static void DebugLogStatic(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var fullMessage = $"[UniversalCommand] {timestamp} - {message}";
            Debug.WriteLine(fullMessage);
            
            try
            {
                OutputDebugStringA(fullMessage + "\r\n");
            }
            catch { /* Ignore if OutputDebugStringA fails */ }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
        private static extern void OutputDebugStringA(string lpOutputString);

        public object Execute(object commandData, ref string message, object elements)
        {
            DebugLogStatic("=== Universal Execute Started ===");
            
            try
            {
                // Get document from commandData using reflection
                var application = GetProperty(commandData, "Application");
                var activeUIDocument = GetProperty(application, "ActiveUIDocument");
                var document = GetProperty(activeUIDocument, "Document");

                DebugLogStatic($"Document retrieved: {GetProperty(document, "Title")}");

                // Create window với universal support
                var window = CreateUniversalWindow(document, activeUIDocument);
                
                // Setup window owner
                var mainWindowHandle = GetProperty(application, "MainWindowHandle");
                SetupWindow(window, mainWindowHandle);

                // Show dialog
                DebugLogStatic("Showing universal schedule editor");
                var result = InvokeMethod(window, "ShowDialog");
                
                DebugLogStatic($"Dialog closed with result: {result}");

                // Return success (enum value 0 for Result.Succeeded)
                return GetEnumValue(_resultType, "Succeeded");
            }
            catch (Exception ex)
            {
                message = $"Universal Schedule Editor Error: {ex.Message}";
                DebugLogStatic($"ERROR: {ex.Message}\n{ex.StackTrace}");
                
                // Return failed (enum value 1 for Result.Failed)  
                return GetEnumValue(_resultType, "Failed");
            }
        }

        private object CreateUniversalWindow(object document, object uidocument)
        {
            try
            {
                // Try modern constructor first (with UIDocument support)
                if (_revitVersion >= new Version(2024, 0) && uidocument != null)
                {
                    return new ScheduleEditorWindow(document, uidocument, _revitVersion);
                }
                else
                {
                    return new ScheduleEditorWindow(document);
                }
            }
            catch
            {
                // Fallback to basic constructor
                return new ScheduleEditorWindow(document);
            }
        }

        private void SetupWindow(object window, object mainWindowHandle)
        {
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper((System.Windows.Window)window);
                helper.Owner = (IntPtr)mainWindowHandle;
                DebugLogStatic("Window owner set successfully");
            }
            catch (Exception ex)
            {
                DebugLogStatic($"Failed to set window owner: {ex.Message}");
            }
        }

        // Helper methods for reflection
        private object GetProperty(object obj, string propertyName)
        {
            if (obj == null) return null;
            var prop = obj.GetType().GetProperty(propertyName);
            return prop?.GetValue(obj);
        }

        private object InvokeMethod(object obj, string methodName, params object[] parameters)
        {
            if (obj == null) return null;
            var method = obj.GetType().GetMethod(methodName);
            return method?.Invoke(obj, parameters);
        }

        private object GetEnumValue(Type enumType, string valueName)
        {
            return Enum.Parse(enumType, valueName);
        }
    }

    // Wrapper class implementing IExternalCommand for each Revit version
    // [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)] - Applied at runtime
    public class ShowScheduleEditorCommand // : IExternalCommand - will be implemented dynamically
    {
        private static readonly UniversalScheduleEditorCommand _universalCommand = new UniversalScheduleEditorCommand();

        // This method will be called by Revit regardless of version
        public object Execute(object commandData, ref string message, object elements)
        {
            return _universalCommand.Execute(commandData, ref message, elements);
        }

        // For compatibility with older reflection-based loading
        public int Execute(object commandData, ref string message, object elements, Type resultType)
        {
            var result = Execute(commandData, ref message, elements);
            
            // Convert result to integer for older Revit versions
            if (result != null && resultType.IsEnum)
            {
                return (int)result;
            }
            
            return 0; // Success
        }
    }
}
