using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Diagnostics;
using System.Reflection;

namespace RevitScheduleEditor
{
    [Transaction(TransactionMode.Manual)]
    public class ShowScheduleEditorCommand : IExternalCommand
    {
        // Revit API version detection
        private static readonly Version CurrentRevitVersion = GetRevitVersion();
        
        // Version-specific feature flags
        private static readonly bool SupportsForgeTypeId = CurrentRevitVersion >= new Version(2021, 0);
        private static readonly bool SupportsModernUI = CurrentRevitVersion >= new Version(2025, 0);
        private static readonly bool SupportsAdvancedSchedules = CurrentRevitVersion >= new Version(2024, 0);
        
        private static Version GetRevitVersion()
        {
            try
            {
                // Get Revit version from the loaded assembly
                var revitApiAssembly = Assembly.GetAssembly(typeof(Document));
                if (revitApiAssembly != null)
                {
                    var version = revitApiAssembly.GetName().Version;
                    DebugLogStatic($"Detected Revit API Version: {version}");
                    return version;
                }
            }
            catch (Exception ex)
            {
                DebugLogStatic($"Error detecting Revit version: {ex.Message}");
            }
            
            // Fallback to 2020 if detection fails
            return new Version(2020, 0);
        }
        
        private static void DebugLogStatic(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string fullMessage = $"[ScheduleEditor] {timestamp} - {message}";
            OutputDebugStringA(fullMessage + "\r\n");
            Debug.WriteLine(fullMessage);
        }

        private void DebugLog(string message)
        {
            DebugLogStatic(message);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
        private static extern void OutputDebugStringA(string lpOutputString);

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            DebugLog("=== Schedule Editor Command Started ===");
            DebugLog($"Revit Version: {CurrentRevitVersion}");
            DebugLog($"Forge TypeId Support: {SupportsForgeTypeId}");
            DebugLog($"Modern UI Support: {SupportsModernUI}");
            DebugLog($"Advanced Schedules Support: {SupportsAdvancedSchedules}");
            
            try
            {
                Document doc = commandData.Application.ActiveUIDocument.Document;
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                
                DebugLog($"Document loaded: {doc.Title}");
                DebugLog($"Revit Build: {commandData.Application.Application.VersionBuild}");
                DebugLog($"Revit Version Name: {commandData.Application.Application.VersionName}");
                
                // Version-specific optimizations
                if (SupportsModernUI)
                {
                    DebugLog("Using modern UI features for Revit 2025+");
                }
                
                // Create window with version-specific enhancements
                var window = CreateScheduleEditorWindow(doc, uidoc);
                DebugLog("ScheduleEditorWindow created successfully");
                
                // Enhanced window setup for modern Revit versions
                SetupWindowForRevitVersion(window, commandData);
                
                DebugLog("Showing Schedule Editor dialog");
                var result = window.ShowDialog();
                DebugLog($"Schedule Editor dialog closed with result: {result}");
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Error in Schedule Editor: {ex.Message}";
                DebugLog($"CRITICAL ERROR: {ex.Message}\n{ex.StackTrace}");
                
                // Enhanced error reporting for modern Revit versions
                if (SupportsModernUI)
                {
                    ShowModernErrorDialog(ex, commandData);
                }
                
                return Result.Failed;
            }
        }
        
        private ScheduleEditorWindow CreateScheduleEditorWindow(Document doc, UIDocument uidoc)
        {
            if (SupportsAdvancedSchedules)
            {
                DebugLog("Creating window with advanced schedule support");
                return new ScheduleEditorWindow(doc, uidoc, CurrentRevitVersion);
            }
            else
            {
                DebugLog("Creating window with legacy schedule support");
                return new ScheduleEditorWindow(doc);
            }
        }
        
        private void SetupWindowForRevitVersion(ScheduleEditorWindow window, ExternalCommandData commandData)
        {
            // Đảm bảo cửa sổ được hiển thị như một hộp thoại của Revit
            var helper = new System.Windows.Interop.WindowInteropHelper(window);
            helper.Owner = commandData.Application.MainWindowHandle;
            DebugLog("Window owner set to Revit main window");
            
            // Modern Revit version enhancements
            if (SupportsModernUI)
            {
                // Enable modern Windows features
                window.WindowStyle = System.Windows.WindowStyle.SingleBorderWindow;
                window.ResizeMode = System.Windows.ResizeMode.CanResize;
                
                // Better DPI support for Revit 2025+
                if (CurrentRevitVersion >= new Version(2025, 0))
                {
                    window.UseLayoutRounding = true;
                    window.SnapsToDevicePixels = true;
                }
                
                DebugLog("Applied modern UI enhancements");
            }
        }
        
        private void ShowModernErrorDialog(Exception ex, ExternalCommandData commandData)
        {
            try
            {
                // Use modern Revit UI for error display
                var taskDialog = new TaskDialog("Schedule Editor Error");
                taskDialog.MainInstruction = "An error occurred in Schedule Editor";
                taskDialog.MainContent = ex.Message;
                taskDialog.ExpandedContent = $"Version: {CurrentRevitVersion}\n\nStack Trace:\n{ex.StackTrace}";
                taskDialog.CommonButtons = TaskDialogCommonButtons.Ok;
                taskDialog.DefaultButton = TaskDialogResult.Ok;
                taskDialog.Show();
            }
            catch
            {
                // Fallback to simple message if TaskDialog fails
                TaskDialog.Show("Error", ex.Message);
            }
        }
    }
}
