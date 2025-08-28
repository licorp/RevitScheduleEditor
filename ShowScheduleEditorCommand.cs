using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitScheduleEditor
{
    [Transaction(TransactionMode.Manual)]
    public class ShowScheduleEditorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            var window = new ScheduleEditorWindow(doc);
            
            // Đảm bảo cửa sổ được hiển thị như một hộp thoại của Revit
            System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(window);
            helper.Owner = commandData.Application.MainWindowHandle;
            
            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}