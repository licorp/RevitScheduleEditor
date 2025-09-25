using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System;
using System.Reflection;

namespace RevitScheduleEditor
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ScheduleEditorApplication : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Tạo tab mới với tên unique
                string tabName = "Licorp";
                try 
                {
                    application.CreateRibbonTab(tabName);
                }
                catch 
                {
                    // Tab đã tồn tại, không sao
                }

                // Tạo panel với tên cố định
                string panelName = "Data Tools";
                RibbonPanel panel = application.CreateRibbonPanel(tabName, panelName);

                // Tạo button
                string assemblyPath = typeof(ScheduleEditorApplication).Assembly.Location;
                PushButtonData buttonData = new PushButtonData(
                    "ScheduleEditor", 
                    "Schedule\nEditor", 
                    assemblyPath, 
                    "RevitScheduleEditor.ShowScheduleEditorCommand"
                );

                PushButton button = panel.AddItem(buttonData) as PushButton;
                button.ToolTip = "Licorp Schedule Editor - Professional Schedule Management Tool";
                button.LongDescription = "Licorp Schedule Editor cho phép bạn chỉnh sửa và quản lý các bảng dữ liệu (schedule) trong Revit với giao diện thân thiện, tính năng filter nâng cao, và khả năng export Excel.";

                // Thêm icon đơn giản
                try
                {
                    string iconPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(assemblyPath), "icons8-edit-property-windows-10.png");
                    if (System.IO.File.Exists(iconPath))
                    {
                        button.LargeImage = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
                    }
                }
                catch
                {
                    // Icon không load được, bỏ qua
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Startup Error", "Lỗi khởi động Schedule Editor: " + ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}