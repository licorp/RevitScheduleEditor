using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Data;
using System.Collections;
using System.Windows.Controls;

namespace RevitScheduleEditor
{
    public class ScheduleEditorViewModel : BaseViewModel
    {
        private readonly Document _doc;
        private ViewSchedule _selectedSchedule;
        public ObservableCollection<ViewSchedule> Schedules { get; set; }
        public ObservableCollection<ScheduleRow> ScheduleData { get; set; }

        // Filtering
        private List<ScheduleRow> _allScheduleData;
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterScheduleData();
            }
        }

        // Autofill
        public ICommand AutofillCommand { get; }

        public ViewSchedule SelectedSchedule
        {
            get => _selectedSchedule;
            set
            {
                _selectedSchedule = value;
                OnPropertyChanged();
                LoadScheduleDataCommand.Execute(null);
            }
        }

        public ICommand LoadScheduleDataCommand { get; }
        public ICommand UpdateModelCommand { get; }

        public ScheduleEditorViewModel(Document doc)
        {
            _doc = doc;
            _allScheduleData = new List<ScheduleRow>();
            Schedules = new ObservableCollection<ViewSchedule>();
            ScheduleData = new ObservableCollection<ScheduleRow>();
            LoadScheduleDataCommand = new RelayCommand(LoadScheduleData, CanLoadScheduleData);
            UpdateModelCommand = new RelayCommand(UpdateModel, CanUpdateModel);
            AutofillCommand = new RelayCommand(ExecuteAutofill, CanExecuteAutofill);
            // Enable filtering
            CollectionViewSource.GetDefaultView(ScheduleData).Filter = FilterPredicate;
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

        private bool CanLoadScheduleData(object obj) => SelectedSchedule != null;

        private void LoadScheduleData(object obj)
        {
            if (!CanLoadScheduleData(null)) return;
            _allScheduleData.Clear();
            ScheduleData.Clear();
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
                    string value = param?.AsValueString() ?? param?.AsString() ?? string.Empty;
                    scheduleRow.AddValue(field.GetName(), value);
                }
                _allScheduleData.Add(scheduleRow);
            }
            foreach (var row in _allScheduleData)
            {
                ScheduleData.Add(row);
            }
            FilterScheduleData();
        }

        private void FilterScheduleData()
        {
            CollectionViewSource.GetDefaultView(ScheduleData).Refresh();
        }

        private bool FilterPredicate(object item)
        {
            if (string.IsNullOrEmpty(SearchText))
                return true;
            if (item is ScheduleRow row)
            {
                // Kiểm tra tất cả các giá trị của row
                foreach (var key in row.GetModifiedValues().Keys)
                {
                    if (row[key]?.ToLower().Contains(SearchText.ToLower()) ?? false)
                        return true;
                }
            }
            return false;
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
        
        private bool CanUpdateModel(object obj) => ScheduleData.Any(row => row.IsModified);
        
        private void UpdateModel(object obj)
        {
            using (var trans = new Transaction(_doc, "Update from Schedule Editor"))
            {
                trans.Start();
                int updatedCount = 0;
                var changedRows = ScheduleData.Where(row => row.IsModified).ToList();
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
                                param.Set(modifiedPair.Value); // Nếu cần, có thể thêm xử lý kiểu dữ liệu
                                updatedCount++;
                            }
                            catch (Exception ex)
                            {
                                // Logging...
                            }
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
            if (field.ParameterId.Value < 0)
            {
                return elem.get_Parameter((BuiltInParameter)field.ParameterId.Value);
            }
            return elem.LookupParameter(field.GetName());
        }
    }
}
