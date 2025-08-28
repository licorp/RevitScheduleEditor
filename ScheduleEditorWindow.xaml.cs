using Autodesk.Revit.DB;
using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
// using System.Windows.Data; // Bỏ 'using' này để tránh xung đột
using System.Windows.Input;

namespace RevitScheduleEditor
{
    public class ColumnHeaderInfo
    {
        public string ParameterType { get; set; }
        public string ParameterGroup { get; set; }
    }

    public partial class ScheduleEditorWindow : Window
    {
        private readonly Document _doc;
        private ViewSchedule _selectedSchedule;
        private DataTable _scheduleDataTable;
        private const string LOG_PREFIX = "[SCHEDULE_EDITOR]";

        public ScheduleEditorWindow(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            LogInfo("ScheduleEditorWindow initialized");
            LoadSchedules();
        }

        private void LoadSchedules()
        {
            LogInfo("Loading schedules...");
            try
            {
                var schedules = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .Where(s => !s.IsTemplate && s.ViewType == ViewType.Schedule)
                    .OrderBy(s => s.Name)
                    .ToList();
                ScheduleComboBox.ItemsSource = schedules;
                LogInfo($"Loaded {schedules.Count} schedules");
            }
            catch (Exception ex) { LogError("LoadSchedules", ex); }
        }

        private void ScheduleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScheduleComboBox.SelectedItem is ViewSchedule vs)
            {
                _selectedSchedule = vs;
                LogInfo($"Selected schedule: '{_selectedSchedule.Name}'");
            }
        }
        
        private void PreviewEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSchedule != null)
            {
                LogInfo($"Preview/Edit button clicked for schedule: '{_selectedSchedule.Name}'");
                PopulateDataGrid();
            }
            else
            {
                MessageBox.Show("Please select a schedule first.", "Information");
            }
        }

        private void PopulateDataGrid()
        {
            if (_selectedSchedule == null) return;
            LogInfo($"Populating DataGrid for '{_selectedSchedule.Name}'");
            try
            {
                ScheduleDataGrid.Columns.Clear();
                ScheduleDataGrid.ItemsSource = null;

                var collector = new FilteredElementCollector(_doc, _selectedSchedule.Id).WhereElementIsNotElementType();
                var elements = collector.ToElements();
                _scheduleDataTable = new DataTable();
                _scheduleDataTable.Columns.Add("ElementId", typeof(long));

                var visibleFields = _selectedSchedule.Definition.GetFieldOrder()
                    .Select(id => _selectedSchedule.Definition.GetField(id))
                    .Where(f => !f.IsHidden).ToList();

                var firstElement = elements.FirstOrDefault();
                var baseHeaderStyle = (Style)FindResource("CustomColumnHeaderStyle");

                foreach (var field in visibleFields)
                {
                    string fieldName = field.GetName();
                    _scheduleDataTable.Columns.Add(fieldName, typeof(string));

                    var column = new DataGridTextColumn
                    {
                        Header = fieldName,
                        // SỬA LỖI AMBIGUITY #1: Chỉ định rõ ràng System.Windows.Data.Binding
                        Binding = new System.Windows.Data.Binding(fieldName),
                    };

                    var headerStyle = new Style(typeof(DataGridColumnHeader));
                    foreach (var setter in baseHeaderStyle.Setters) headerStyle.Setters.Add(setter);
                    headerStyle.Resources = baseHeaderStyle.Resources;
                    headerStyle.Triggers.Clear();
                    foreach (var trigger in baseHeaderStyle.Triggers) headerStyle.Triggers.Add(trigger);

                    if (firstElement != null)
                    {
                        Parameter param = GetParameterFromField(firstElement, field);
                        if (param != null)
                        {
                            var info = new ColumnHeaderInfo
                            {
                                ParameterType = param.Definition.GetDataType().ToString(),
                                ParameterGroup = param.Definition.Name
                            };
                            headerStyle.Setters.Add(new Setter(TagProperty, info));
                        }
                    }
                    column.HeaderStyle = headerStyle;
                    ScheduleDataGrid.Columns.Add(column);
                }
                
                foreach (Element elem in elements)
                {
                    var row = _scheduleDataTable.NewRow();
                    row["ElementId"] = elem.Id.Value;
                    foreach (var field in visibleFields)
                    {
                        Parameter param = GetParameterFromField(elem, field);
                        string value = param?.AsValueString() ?? param?.AsString() ?? string.Empty;
                        row[field.GetName()] = value;
                    }
                    _scheduleDataTable.Rows.Add(row);
                }

                ScheduleDataGrid.ItemsSource = _scheduleDataTable.DefaultView;
                _scheduleDataTable.AcceptChanges(); // Đặt trạng thái ban đầu là Unchanged
                LogInfo($"Populated DataGrid with {_scheduleDataTable.Rows.Count} rows");
            }
            catch (Exception ex) { LogError("PopulateDataGrid", ex); }
        }

        private void ScheduleDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView.Row.EndEdit();
                // Đánh dấu dòng là Modified để Transaction nhận biết
                if (rowView.Row.RowState == DataRowState.Unchanged)
                {
                    rowView.Row.SetModified();
                }
            }
        }

        private Parameter GetParameterFromField(Element elem, ScheduleField field)
        {
            if (elem == null || field == null) return null;
            if (field.ParameterId.Value < 0)
            {
                return elem.get_Parameter((BuiltInParameter)field.ParameterId.Value);
            }
            return elem.LookupParameter(field.GetName());
        }

        private void UpdateModel_Click(object sender, RoutedEventArgs e)
        {
            if (_scheduleDataTable == null || _selectedSchedule == null) return;
            LogInfo("Update Model button clicked.");
            try
            {
                using (var trans = new Transaction(_doc, "Update from Schedule Editor"))
                {
                    trans.Start();
                    int updated = 0;
                    foreach (DataRowView drv in _scheduleDataTable.DefaultView)
                    {
                        if (drv.Row.RowState != DataRowState.Modified) continue;
                        
                        long elemIdVal = (long)drv.Row["ElementId"];
                        var elem = _doc.GetElement(new ElementId(elemIdVal));
                        if (elem == null) continue;

                        var visibleFields = _selectedSchedule.Definition.GetFieldOrder()
                            .Select(id => _selectedSchedule.Definition.GetField(id))
                            .Where(f => !f.IsHidden).ToList();

                        foreach (var field in visibleFields)
                        {
                            Parameter param = GetParameterFromField(elem, field);
                            if (param != null && !param.IsReadOnly)
                            {
                                string newValueStr = drv.Row[field.GetName()]?.ToString() ?? string.Empty;
                                string origValueStr = param.AsValueString() ?? param.AsString() ?? string.Empty;

                                if (origValueStr != newValueStr)
                                {
                                    try
                                    {
                                        switch (param.StorageType)
                                        {
                                            case StorageType.Integer:
                                                param.Set(int.Parse(newValueStr));
                                                break;
                                            case StorageType.Double:
                                                param.Set(double.Parse(newValueStr));
                                                break;
                                            case StorageType.String:
                                                param.Set(newValueStr);
                                                break;
                                            default:
                                                param.Set(newValueStr);
                                                break;
                                        }
                                        updated++;
                                    }
                                    catch (Exception setEx)
                                    {
                                        LogInfo($"Could not set parameter '{param.Definition.Name}' to '{newValueStr}'. Reason: {setEx.Message}");
                                    }
                                }
                            }
                        }
                    }
                    trans.Commit();
                    LogInfo($"Committed transaction. Updated {updated} parameters.");
                    MessageBox.Show($"Updated {updated} parameters.", "Success");
                }
            }
            catch (Exception ex) { LogError("UpdateModel_Click", ex); }
        }
        
        private void ScheduleDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                PasteFromClipboard();
            }
        }

        private void PasteFromClipboard()
        {
            try
            {
                var startCell = ScheduleDataGrid.SelectedCells.FirstOrDefault();
                if (startCell == null || startCell.Item == null) return;

                var startRowIndex = ScheduleDataGrid.Items.IndexOf(startCell.Item);
                var startColumnIndex = startCell.Column.DisplayIndex;

                string clipboardText = Clipboard.GetText();
                string[] lines = clipboardText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (startRowIndex + i >= ScheduleDataGrid.Items.Count) break;
                    string[] cells = lines[i].Split('\t');
                    DataRowView rowView = ScheduleDataGrid.Items[startRowIndex + i] as DataRowView;

                    for (int j = 0; j < cells.Length; j++)
                    {
                        if (startColumnIndex + j >= ScheduleDataGrid.Columns.Count) break;
                        var column = ScheduleDataGrid.Columns[startColumnIndex + j];
                        // SỬA LỖI AMBIGUITY #2: Chỉ định rõ ràng System.Windows.Data.Binding
                        var binding = (column as DataGridBoundColumn)?.Binding as System.Windows.Data.Binding;
                        if (binding == null) continue;
                        var columnName = binding.Path.Path;
                        
                        if (columnName.Equals("ElementId", StringComparison.OrdinalIgnoreCase)) continue;
                        
                        rowView.Row[columnName] = cells[j];
                    }
                }
            }
            catch (Exception ex) { LogError("PasteFromClipboard", ex); }
        }
        
        private void ScheduleDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private void LogInfo(string msg) => Debug.WriteLine($"{LOG_PREFIX} [INFO] {msg}");
        private void LogError(string ctx, Exception ex) => Debug.WriteLine($"{LOG_PREFIX} [ERROR] {ctx}: {ex.Message}\n{ex.StackTrace}");
    }
}