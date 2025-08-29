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
        
        // Excel-like features
        private string[,] _clipboardData;
        private List<Dictionary<string, string>> _undoHistory;
        private List<Dictionary<string, string>> _redoHistory;
        private const int MaxHistorySize = 50;
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
        public ICommand CopyCommand { get; }
        public ICommand PasteCommand { get; }
        public ICommand CutCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand FillDownCommand { get; }
        public ICommand FillRightCommand { get; }
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
            
            // Initialize history
            _undoHistory = new List<Dictionary<string, string>>();
            _redoHistory = new List<Dictionary<string, string>>();
            
            // Commands
            UpdateModelCommand = new RelayCommand(UpdateModel, CanUpdateModel);
            AutofillCommand = new RelayCommand(ExecuteAutofill, CanExecuteAutofill);
            CopyCommand = new RelayCommand(ExecuteCopy, CanExecuteCopy);
            PasteCommand = new RelayCommand(ExecutePaste, CanExecutePaste);
            CutCommand = new RelayCommand(ExecuteCut, CanExecuteCut);
            UndoCommand = new RelayCommand(ExecuteUndo, CanExecuteUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, CanExecuteRedo);
            FillDownCommand = new RelayCommand(ExecuteFillDown, CanExecuteFillDown);
            FillRightCommand = new RelayCommand(ExecuteFillRight, CanExecuteFillRight);

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

        #region Excel-like Features

        // Copy functionality
        private void ExecuteCopy(object parameter)
        {
            var selectedCells = parameter as IList;
            if (selectedCells == null) return;
            
            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            if (cellInfos.Count == 0) return;

            // Group cells by row and column to create 2D array
            var rowGroups = cellInfos.GroupBy(c => c.Item).ToList();
            var colGroups = cellInfos.GroupBy(c => c.Column).ToList();
            
            _clipboardData = new string[rowGroups.Count, colGroups.Count];
            
            var rowIndex = 0;
            foreach (var rowGroup in rowGroups)
            {
                var colIndex = 0;
                foreach (var colGroup in colGroups)
                {
                    var cell = cellInfos.FirstOrDefault(c => c.Item == rowGroup.Key && c.Column == colGroup.Key);
                    if (cell.Item is ScheduleRow row && cell.Column is DataGridBoundColumn boundCol)
                    {
                        var bindingPath = (boundCol.Binding as System.Windows.Data.Binding)?.Path.Path;
                        if (!string.IsNullOrEmpty(bindingPath))
                        {
                            string columnName = bindingPath.Trim('[', ']');
                            _clipboardData[rowIndex, colIndex] = row[columnName] ?? string.Empty;
                        }
                    }
                    colIndex++;
                }
                rowIndex++;
            }
        }

        private bool CanExecuteCopy(object parameter)
        {
            var selectedCells = parameter as IList;
            return selectedCells != null && selectedCells.Count > 0;
        }

        // Paste functionality
        private void ExecutePaste(object parameter)
        {
            var selectedCells = parameter as IList;
            if (selectedCells == null || _clipboardData == null) return;
            
            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            if (cellInfos.Count == 0) return;

            SaveStateForUndo();

            var startCell = cellInfos.First();
            if (!(startCell.Item is ScheduleRow startRow)) return;

            var startRowIndex = ScheduleData.IndexOf(startRow);
            var startColIndex = GetColumnIndex(startCell.Column);
            
            if (startRowIndex < 0 || startColIndex < 0) return;

            var clipRows = _clipboardData.GetLength(0);
            var clipCols = _clipboardData.GetLength(1);

            for (int r = 0; r < clipRows && startRowIndex + r < ScheduleData.Count; r++)
            {
                for (int c = 0; c < clipCols; c++)
                {
                    var targetRow = ScheduleData[startRowIndex + r];
                    var targetColName = GetColumnNameByIndex(startColIndex + c);
                    
                    if (!string.IsNullOrEmpty(targetColName))
                    {
                        targetRow[targetColName] = _clipboardData[r, c];
                    }
                }
            }
        }

        private bool CanExecutePaste(object parameter)
        {
            var selectedCells = parameter as IList;
            return selectedCells != null && selectedCells.Count > 0 && _clipboardData != null;
        }

        // Cut functionality
        private void ExecuteCut(object parameter)
        {
            ExecuteCopy(parameter);
            
            var selectedCells = parameter as IList;
            if (selectedCells == null) return;
            
            SaveStateForUndo();
            
            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            foreach (var cellInfo in cellInfos)
            {
                if (cellInfo.Item is ScheduleRow row && cellInfo.Column is DataGridBoundColumn boundCol)
                {
                    var bindingPath = (boundCol.Binding as System.Windows.Data.Binding)?.Path.Path;
                    if (!string.IsNullOrEmpty(bindingPath))
                    {
                        string columnName = bindingPath.Trim('[', ']');
                        row[columnName] = string.Empty;
                    }
                }
            }
        }

        private bool CanExecuteCut(object parameter)
        {
            return CanExecuteCopy(parameter);
        }

        // Fill Down functionality
        private void ExecuteFillDown(object parameter)
        {
            var selectedCells = parameter as IList;
            if (selectedCells == null) return;
            
            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            if (cellInfos.Count < 2) return;

            SaveStateForUndo();

            var columnGroups = cellInfos.GroupBy(c => c.Column);
            foreach (var columnGroup in columnGroups)
            {
                var cellsInColumn = columnGroup.OrderBy(c => ScheduleData.IndexOf(c.Item as ScheduleRow)).ToList();
                var firstCell = cellsInColumn.First();
                
                if (!(firstCell.Item is ScheduleRow firstRow) || !(firstCell.Column is DataGridBoundColumn boundCol)) 
                    continue;

                var bindingPath = (boundCol.Binding as System.Windows.Data.Binding)?.Path.Path;
                if (string.IsNullOrEmpty(bindingPath)) continue;

                string columnName = bindingPath.Trim('[', ']');
                string valueToFill = firstRow[columnName];

                foreach (var cell in cellsInColumn.Skip(1))
                {
                    if (cell.Item is ScheduleRow targetRow)
                    {
                        targetRow[columnName] = valueToFill;
                    }
                }
            }
        }

        private bool CanExecuteFillDown(object parameter)
        {
            var selectedCells = parameter as IList;
            return selectedCells != null && selectedCells.Count > 1;
        }

        // Fill Right functionality
        private void ExecuteFillRight(object parameter)
        {
            var selectedCells = parameter as IList;
            if (selectedCells == null) return;
            
            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            if (cellInfos.Count < 2) return;

            SaveStateForUndo();

            var rowGroups = cellInfos.GroupBy(c => c.Item);
            foreach (var rowGroup in rowGroups)
            {
                var cellsInRow = rowGroup.OrderBy(c => GetColumnIndex(c.Column)).ToList();
                var firstCell = cellsInRow.First();
                
                if (!(firstCell.Item is ScheduleRow row) || !(firstCell.Column is DataGridBoundColumn boundCol)) 
                    continue;

                var bindingPath = (boundCol.Binding as System.Windows.Data.Binding)?.Path.Path;
                if (string.IsNullOrEmpty(bindingPath)) continue;

                string firstColumnName = bindingPath.Trim('[', ']');
                string valueToFill = row[firstColumnName];

                foreach (var cell in cellsInRow.Skip(1))
                {
                    if (cell.Column is DataGridBoundColumn targetBoundCol)
                    {
                        var targetBindingPath = (targetBoundCol.Binding as System.Windows.Data.Binding)?.Path.Path;
                        if (!string.IsNullOrEmpty(targetBindingPath))
                        {
                            string targetColumnName = targetBindingPath.Trim('[', ']');
                            row[targetColumnName] = valueToFill;
                        }
                    }
                }
            }
        }

        private bool CanExecuteFillRight(object parameter)
        {
            var selectedCells = parameter as IList;
            return selectedCells != null && selectedCells.Count > 1;
        }

        // Undo/Redo functionality
        public void SaveStateForUndo()
        {
            var state = new Dictionary<string, string>();
            for (int i = 0; i < ScheduleData.Count; i++)
            {
                var row = ScheduleData[i];
                foreach (var kvp in row.Values)
                {
                    state[$"{i}_{kvp.Key}"] = kvp.Value;
                }
            }
            
            _undoHistory.Add(state);
            if (_undoHistory.Count > MaxHistorySize)
            {
                _undoHistory.RemoveAt(0);
            }
            
            _redoHistory.Clear(); // Clear redo history when new action is performed
        }

        private void ExecuteUndo(object parameter)
        {
            if (_undoHistory.Count == 0) return;

            var currentState = new Dictionary<string, string>();
            for (int i = 0; i < ScheduleData.Count; i++)
            {
                var row = ScheduleData[i];
                foreach (var kvp in row.Values)
                {
                    currentState[$"{i}_{kvp.Key}"] = kvp.Value;
                }
            }
            _redoHistory.Add(currentState);

            var previousState = _undoHistory.Last();
            _undoHistory.RemoveAt(_undoHistory.Count - 1);

            RestoreState(previousState);
        }

        private bool CanExecuteUndo(object parameter)
        {
            return _undoHistory.Count > 0;
        }

        private void ExecuteRedo(object parameter)
        {
            if (_redoHistory.Count == 0) return;

            SaveStateForUndo();

            var redoState = _redoHistory.Last();
            _redoHistory.RemoveAt(_redoHistory.Count - 1);

            RestoreState(redoState);
        }

        private bool CanExecuteRedo(object parameter)
        {
            return _redoHistory.Count > 0;
        }

        private void RestoreState(Dictionary<string, string> state)
        {
            foreach (var kvp in state)
            {
                var parts = kvp.Key.Split('_');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int rowIndex))
                {
                    if (rowIndex < ScheduleData.Count)
                    {
                        string columnName = string.Join("_", parts.Skip(1));
                        ScheduleData[rowIndex][columnName] = kvp.Value;
                    }
                }
            }
        }

        // Helper methods
        private int GetColumnIndex(DataGridColumn column)
        {
            if (SelectedSchedule == null) return -1;
            
            var visibleFields = SelectedSchedule.Definition.GetFieldOrder()
                .Select(id => SelectedSchedule.Definition.GetField(id))
                .Where(f => !f.IsHidden).ToList();

            if (column is DataGridBoundColumn boundCol)
            {
                var bindingPath = (boundCol.Binding as System.Windows.Data.Binding)?.Path.Path;
                if (!string.IsNullOrEmpty(bindingPath))
                {
                    string columnName = bindingPath.Trim('[', ']');
                    for (int i = 0; i < visibleFields.Count; i++)
                    {
                        if (visibleFields[i].GetName() == columnName)
                            return i;
                    }
                }
            }
            return -1;
        }

        private string GetColumnNameByIndex(int columnIndex)
        {
            if (SelectedSchedule == null) return null;
            
            var visibleFields = SelectedSchedule.Definition.GetFieldOrder()
                .Select(id => SelectedSchedule.Definition.GetField(id))
                .Where(f => !f.IsHidden).ToList();

            if (columnIndex >= 0 && columnIndex < visibleFields.Count)
            {
                return visibleFields[columnIndex].GetName();
            }
            return null;
        }

        #endregion
    }
}
