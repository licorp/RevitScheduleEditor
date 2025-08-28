using Autodesk.Revit.DB;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace RevitScheduleEditor
{
    public class ScheduleEditorViewModel : BaseViewModel
    {
        private readonly Document _doc;
        private ViewSchedule _selectedSchedule;
        public ObservableCollection<ViewSchedule> Schedules { get; set; }
        public ObservableCollection<ScheduleRow> ScheduleData { get; set; }
        
        private List<ScheduleRow> _allScheduleData;
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                // Filter sẽ được handle bởi Window
            }
        }

        public ICommand AutofillCommand { get; }
        public ViewSchedule SelectedSchedule
        {
            get => _selectedSchedule;
            set
            {
                _selectedSchedule = value;
                OnPropertyChanged();
                LoadScheduleData(null);
            }
        }
        
        public ICommand UpdateModelCommand { get; }

        public ScheduleEditorViewModel(Document doc)
        {
            _doc = doc;
            _allScheduleData = new List<ScheduleRow>();
            Schedules = new ObservableCollection<ViewSchedule>();
            ScheduleData = new ObservableCollection<ScheduleRow>();
            
            UpdateModelCommand = new RelayCommand(UpdateModel, CanUpdateModel);
            AutofillCommand = new RelayCommand(ExecuteAutofill, CanExecuteAutofill);

            LoadSchedules();
        }

        private void LoadSchedules()
        {
            var schedules = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(s => !s.IsTemplate && s.ViewType == ViewType.Schedule)
                .OrderBy(s => s.Name)
                .ToList();
            Schedules.Clear();
            foreach (var s in schedules)
            {
                Schedules.Add(s);
            }
        }

        private void LoadScheduleData(object obj)
        {
            if (SelectedSchedule == null) return;

            _allScheduleData.Clear();
            var collector = new FilteredElementCollector(_doc, SelectedSchedule.Id).WhereElementIsNotElementType();
            var elements = collector.ToElements();
            var visibleFields = SelectedSchedule.Definition.GetFieldOrder()
                .Select(id => SelectedSchedule.Definition.GetField(id))
                .Where(f => !f.IsHidden).ToList();

            foreach (Element elem in elements)
            {
                var scheduleRow = new ScheduleRow(elem);
                foreach (var field in visibleFields)
                {
                    Parameter param = GetParameterFromField(elem, field);
                    string value = param != null ? param.AsValueString() ?? param.AsString() ?? string.Empty : string.Empty;
                    scheduleRow.AddValue(field.GetName(), value);
                }
                _allScheduleData.Add(scheduleRow);
            }

            ScheduleData.Clear();
            foreach (var row in _allScheduleData)
            {
                ScheduleData.Add(row);
            }
            
            // Notify that data has changed so UI can regenerate filters
            OnPropertyChanged(nameof(ScheduleData));
        }

        private void ExecuteAutofill(object parameter)
        {
            var selectedCells = parameter as IList;
            if (selectedCells == null) return;
            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            if (cellInfos.Count < 2) return;
            var firstCell = cellInfos.First();
            var firstCellColumn = firstCell.Column as DataGridBoundColumn;
            if (firstCellColumn == null) return;
            var bindingPath = (firstCellColumn.Binding as System.Windows.Data.Binding).Path.Path;
            string columnName = bindingPath.Trim('[', ']');
            var firstRow = firstCell.Item as ScheduleRow;
            if (firstRow == null) return;
            var valueToFill = firstRow[columnName];
            foreach (var cellInfo in cellInfos.Skip(1))
            {
                if (cellInfo.Item is ScheduleRow rowToFill)
                {
                    if (cellInfo.Column == firstCell.Column)
                    {
                        rowToFill[columnName] = valueToFill;
                    }
                }
            }
        }

        private bool CanExecuteAutofill(object parameter)
        {
            var selectedCells = parameter as IList;
            if (selectedCells == null || selectedCells.Count < 2)
                return false;
            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            var firstColumn = cellInfos.First().Column;
            return cellInfos.All(c => c.Column == firstColumn);
        }

        private bool CanUpdateModel(object obj) => _allScheduleData.Any(row => row.IsModified);
        
        private void UpdateModel(object obj)
        {
            using (var trans = new Transaction(_doc, "Update from Schedule Editor"))
            {
                trans.Start();
                int updatedCount = 0;
                var changedRows = _allScheduleData.Where(row => row.IsModified).ToList();
                
                foreach (var row in changedRows)
                {
                    Element elem = row.GetElement();
                    var modifiedValues = row.GetModifiedValues();
                    foreach (var modifiedPair in modifiedValues)
                    {
                        Parameter param = elem.LookupParameter(modifiedPair.Key);
                        if (param != null && !param.IsReadOnly)
                        {
                            try
                            {
                                switch (param.StorageType)
                                {
                                    case StorageType.Integer:
                                        if (int.TryParse(modifiedPair.Value, out int intValue)) param.Set(intValue);
                                        break;
                                    case StorageType.Double:
                                        // Dùng SetValueString để Revit tự xử lý đơn vị
                                        param.SetValueString(modifiedPair.Value);
                                        break;
                                    case StorageType.String:
                                        param.Set(modifiedPair.Value);
                                        break;
                                    case StorageType.ElementId:
                                        // Xử lý cho tham số ElementId nếu cần
                                        break;
                                }
                                updatedCount++;
                            } catch {}
                        }
                    }
                    row.AcceptChanges();
                }
                trans.Commit();
                MessageBox.Show($"Updated {updatedCount} parameters.", "Success");
            }
        }
        
        private Parameter GetParameterFromField(Element elem, ScheduleField field)
        {
            if (elem == null || field == null) return null;
            if (field.ParameterId.IntegerValue < 0)
            {
                return elem.get_Parameter((BuiltInParameter)field.ParameterId.IntegerValue);
            }
            return elem.LookupParameter(field.GetName());
        }
    }
}
