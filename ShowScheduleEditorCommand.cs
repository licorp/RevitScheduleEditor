using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Diagnostics;

namespace RevitScheduleEditor
{
    [Transaction(TransactionMode.Manual)]
    public class ShowScheduleEditorCommand : IExternalCommand
    {
        // Debug logging methods similar to SplitElement
        private void DebugLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string fullMessage = $"[ScheduleEditor] {timestamp} - {message}";
            OutputDebugStringA(fullMessage + "\r\n");
            Debug.WriteLine(fullMessage);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
        private static extern void OutputDebugStringA(string lpOutputString);

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            DebugLog("=== Schedule Editor Command Started ===");
            
            try
            {
                Document doc = commandData.Application.ActiveUIDocument.Document;
                DebugLog($"Document loaded: {doc.Title}");
                
                var window = new ScheduleEditorWindow(doc);
                DebugLog("ScheduleEditorWindow created successfully");
                
                // Đảm bảo cửa sổ được hiển thị như một hộp thoại của Revit
                System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = commandData.Application.MainWindowHandle;
                DebugLog("Window owner set to Revit main window");
                
                DebugLog("Showing Schedule Editor dialog");
                window.ShowDialog();
                DebugLog("Schedule Editor dialog closed");
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Error in Schedule Editor: {ex.Message}";
                DebugLog($"CRITICAL ERROR: {ex.Message}\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}