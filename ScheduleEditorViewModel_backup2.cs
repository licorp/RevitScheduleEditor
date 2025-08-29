using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        private readonly UIApplication _uiApp;
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

        // Async loading manager - Cải tiến hiệu năng như SheetLink
        private AsyncScheduleDataManager _asyncDataManager;
        
        // Progress tracking properties
        private bool _isLoadingData;
        private string _loadingStatus;
        private double _loadingProgress;
        private int _totalElements;
        private int _loadedElements;

        // Debug logging methods
        private void DebugLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string fullMessage = $"[ScheduleEditorViewModel] {timestamp} - {message}";
            OutputDebugStringA(fullMessage + "\r\n");
            Debug.WriteLine(fullMessage);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
        private static extern void OutputDebugStringA(string lpOutputString);

        // Progress properties for data binding
        public bool IsLoadingData
        {
            get => _isLoadingData;
            set
            {
                _isLoadingData = value;
                OnPropertyChanged(nameof(IsLoadingData));
            }
        }

        public string LoadingStatus
        {
            get => _loadingStatus;
            set
            {
                _loadingStatus = value;
                OnPropertyChanged(nameof(LoadingStatus));
            }
        }

        public double LoadingProgress
        {
            get => _loadingProgress;
            set
            {
                _loadingProgress = value;
                OnPropertyChanged(nameof(LoadingProgress));
            }
        }

        public int TotalElements
        {
            get => _totalElements;
            set
            {
                _totalElements = value;
                OnPropertyChanged(nameof(TotalElements));
            }
        }

        public int LoadedElements
        {
            get => _loadedElements;
            set
            {
                _loadedElements = value;
                OnPropertyChanged(nameof(LoadedElements));
            }
        }
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
        
        // New commands for the buttons
        public ICommand PreviewEditCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand CancelLoadingCommand { get; }
        
        public ViewSchedule SelectedSchedule
        {
            get => _selectedSchedule;
            set
            {
                _selectedSchedule = value;
                OnPropertyChanged();
                // Don't auto-load data anymore - user needs to click Preview/Edit
            }
        }
        
        public ICommand UpdateModelCommand { get; }

        public ScheduleEditorViewModel(Document doc)
        {
            DebugLog("=== ScheduleEditorViewModel Constructor Started ===");
            
            try
            {
                _doc = doc;
                DebugLog($"Document assigned: {doc.Title}");
                
                // Tạo UIApplication để truyền cho AsyncScheduleDataManager
                _uiApp = new UIApplication(doc.Application);
                
                _allScheduleData = new List<ScheduleRow>();
                Schedules = new ObservableCollection<ViewSchedule>();
                ScheduleData = new ObservableCollection<ScheduleRow>();
                DebugLog("Collections initialized");
                
                // Khởi tạo Async Data Manager cho hiệu năng cao
                _asyncDataManager = new AsyncScheduleDataManager(_uiApp);
                _asyncDataManager.PropertyChanged += OnAsyncDataManagerPropertyChanged;
                _asyncDataManager.BatchLoaded += OnBatchLoaded;
                _asyncDataManager.LoadingCompleted += OnLoadingCompleted;
                DebugLog("AsyncScheduleDataManager initialized");
                
                // Initialize history
                _undoHistory = new List<Dictionary<string, string>>();
                _redoHistory = new List<Dictionary<string, string>>();
                DebugLog("Undo/Redo history initialized");
                
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
                
                // New commands
                PreviewEditCommand = new RelayCommand(ExecutePreviewEdit, CanExecutePreviewEdit);
                ImportCommand = new RelayCommand(ExecuteImport, CanExecuteImport);
                ExportCommand = new RelayCommand(ExecuteExport, CanExecuteExport);
                
                // Add cancel loading command
                CancelLoadingCommand = new RelayCommand(ExecuteCancelLoading, CanExecuteCancelLoading);
                DebugLog("All commands initialized");

                LoadSchedules();
                DebugLog("ScheduleEditorViewModel constructor completed successfully");
            }
            catch (Exception ex)
            {
                DebugLog($"ERROR in ScheduleEditorViewModel constructor: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        private void LoadSchedules()
        {
            DebugLog("Loading schedules from document...");
            
            try
            {
                var schedules = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .Where(s => !s.IsTemplate && s.ViewType == ViewType.Schedule)
                    .OrderBy(s => s.Name)
                    .ToList();
                
                DebugLog($"Found {schedules.Count} schedules in document");
                
                Schedules.Clear();
                foreach (var s in schedules)
                {
                    Schedules.Add(s);
                    DebugLog($"Added schedule: {s.Name}");
                }
                
                DebugLog("LoadSchedules completed successfully");
            }
            catch (Exception ex)
            {
                DebugLog($"ERROR in LoadSchedules: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        // Event handlers cho AsyncScheduleDataManager
        private void OnAsyncDataManagerPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(AsyncScheduleDataManager.IsLoading):
                    IsLoadingData = _asyncDataManager.IsLoading;
                    break;
                case nameof(AsyncScheduleDataManager.LoadingStatus):
                    LoadingStatus = _asyncDataManager.LoadingStatus;
                    break;
                case nameof(AsyncScheduleDataManager.LoadingProgress):
                    LoadingProgress = _asyncDataManager.LoadingProgress;
                    break;
                case nameof(AsyncScheduleDataManager.TotalElements):
                    TotalElements = _asyncDataManager.TotalElements;
                    break;
                case nameof(AsyncScheduleDataManager.LoadedElements):
                    LoadedElements = _asyncDataManager.LoadedElements;
                    break;
            }
        }

        private void OnBatchLoaded(object sender, BatchLoadedEventArgs e)
        {
            DebugLog($"Batch loaded: {e.NewRows.Count} new rows, Total: {e.TotalLoadedElements}/{e.TotalElements}");
            
            // Thêm rows mới vào ScheduleData (có thể cần dispatch về UI thread)
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var row in e.NewRows)
                {
                    ScheduleData.Add(row);
                    _allScheduleData.Add(row);
                }
                
                // Notify UI
                OnPropertyChanged(nameof(ScheduleData));
            }));
        }

        private void OnLoadingCompleted(object sender, EventArgs e)
        {
            DebugLog("All data loading completed");
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                LoadingStatus = $"Completed! Loaded {LoadedElements} elements";
                // Có thể thêm logic khác khi hoàn thành tải dữ liệu
            }));
        }

        // Command methods
        private void ExecutePreviewEdit(object obj)
        {
            DebugLog("ExecutePreviewEdit called - Using async loading for better performance");
            
            if (SelectedSchedule == null)
            {
                MessageBox.Show("Please select a schedule first.", "No Schedule Selected");
                return;
            }

            // Clear current data
            ScheduleData.Clear();
            _allScheduleData.Clear();
            
            // Start async loading like SheetLink
            _asyncDataManager.LoadScheduleAsync(SelectedSchedule);
        }

        private bool CanExecutePreviewEdit(object obj)
        {
            return SelectedSchedule != null && !IsLoadingData;
        }

        private void ExecuteCancelLoading(object obj)
        {
            DebugLog("User cancelled data loading");
            _asyncDataManager.CancelLoading();
        }

        private bool CanExecuteCancelLoading(object obj)
        {
            return IsLoadingData;
        }

        private void LoadScheduleData(object obj)
        {
            DebugLog("=== LoadScheduleData Started ===");
            
            if (SelectedSchedule == null) 
            {
                DebugLog("SelectedSchedule is null, returning");
                return;
            }

            DebugLog($"Loading data for schedule: {SelectedSchedule.Name}");
            
            try
            {
                _allScheduleData.Clear();
                var collector = new FilteredElementCollector(_doc, SelectedSchedule.Id).WhereElementIsNotElementType();
                var elements = collector.ToElements();
                DebugLog($"Found {elements.Count} elements in schedule");
                
                var visibleFields = SelectedSchedule.Definition.GetFieldOrder()
                    .Select(id => SelectedSchedule.Definition.GetField(id))
                    .Where(f => !f.IsHidden).ToList();
                DebugLog($"Found {visibleFields.Count} visible fields");

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

                DebugLog($"Loaded {_allScheduleData.Count} rows of schedule data");

                ScheduleData.Clear();
                foreach (var row in _allScheduleData)
                {
                    ScheduleData.Add(row);
                }
                
                // Notify that data has changed so UI can regenerate filters
                OnPropertyChanged(nameof(ScheduleData));
                DebugLog("LoadScheduleData completed successfully");
            }
            catch (Exception ex)
            {
                DebugLog($"ERROR in LoadScheduleData: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
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
            DebugLog("=== UpdateModel Started ===");
            
            try
            {
                using (var trans = new Transaction(_doc, "Update from Schedule Editor"))
                {
                    trans.Start();
                    DebugLog("Transaction started");
                    
                    int updatedCount = 0;
                    var changedRows = _allScheduleData.Where(row => row.IsModified).ToList();
                    DebugLog($"Found {changedRows.Count} rows with modifications");
                    
                    foreach (var row in changedRows)
                    {
                        Element elem = row.GetElement();
                        var modifiedValues = row.GetModifiedValues();
                        DebugLog($"Processing Element ID {elem.Id}: {modifiedValues.Count} modified parameters");
                        
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
                                            if (int.TryParse(modifiedPair.Value, out int intValue)) 
                                            {
                                                param.Set(intValue);
                                                DebugLog($"  Set Integer parameter '{modifiedPair.Key}' = {intValue}");
                                            }
                                            break;
                                        case StorageType.Double:
                                            // Dùng SetValueString để Revit tự xử lý đơn vị
                                            param.SetValueString(modifiedPair.Value);
                                            DebugLog($"  Set Double parameter '{modifiedPair.Key}' = {modifiedPair.Value}");
                                            break;
                                        case StorageType.String:
                                            param.Set(modifiedPair.Value);
                                            DebugLog($"  Set String parameter '{modifiedPair.Key}' = {modifiedPair.Value}");
                                            break;
                                        case StorageType.ElementId:
                                            // Xử lý cho tham số ElementId nếu cần
                                            DebugLog($"  Skipped ElementId parameter '{modifiedPair.Key}'");
                                            break;
                                    }
                                    updatedCount++;
                                } 
                                catch (Exception paramEx) 
                                {
                                    DebugLog($"  ERROR setting parameter '{modifiedPair.Key}': {paramEx.Message}");
                                }
                            }
                            else
                            {
                                DebugLog($"  Skipped parameter '{modifiedPair.Key}' (null or read-only)");
                            }
                        }
                        row.AcceptChanges();
                    }
                    
                    trans.Commit();
                    DebugLog($"Transaction committed. Updated {updatedCount} parameters.");
                    MessageBox.Show($"Updated {updatedCount} parameters.", "Success");
                }
                
                DebugLog("UpdateModel completed successfully");
            }
            catch (Exception ex)
            {
                DebugLog($"ERROR in UpdateModel: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error updating model: {ex.Message}", "Error");
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

        #region New Commands Implementation

        // Preview/Edit Command
        private void ExecutePreviewEdit(object parameter)
        {
            if (SelectedSchedule == null)
            {
                MessageBox.Show("Please select a schedule first.", "No Schedule Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                LoadScheduleData(null);
                MessageBox.Show($"Successfully loaded {ScheduleData.Count} items from schedule '{SelectedSchedule.Name}'.", "Data Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading schedule data: {ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecutePreviewEdit(object parameter)
        {
            return SelectedSchedule != null;
        }

        // Import Command
        private void ExecuteImport(object parameter)
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    Title = "Select CSV or Excel file to import"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    ImportFromExcel(openFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing data: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteImport(object parameter)
        {
            return SelectedSchedule != null && ScheduleData.Count > 0;
        }

        // Export Command
        private void ExecuteExport(object parameter)
        {
            try
            {
                if (ScheduleData.Count == 0)
                {
                    MessageBox.Show("No data to export.", "No Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx",
                    Title = "Save Excel file",
                    FileName = $"{SelectedSchedule?.Name ?? "Schedule"}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportToExcel(saveFileDialog.FileName);
                    MessageBox.Show("Data exported successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting data: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteExport(object parameter)
        {
            return ScheduleData.Count > 0;
        }

        #endregion

        #region Excel Import/Export Implementation

        private void ImportFromExcel(string filePath)
        {
            try
            {
                // Simple CSV import implementation
                var lines = System.IO.File.ReadAllLines(filePath);
                if (lines.Length < 2)
                {
                    MessageBox.Show("File must contain header and at least one data row.", "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var headers = lines[0].Split(',');
                var elementIdIndex = Array.FindIndex(headers, h => h.Trim().Equals("Element ID", StringComparison.OrdinalIgnoreCase));
                
                if (elementIdIndex == -1)
                {
                    MessageBox.Show("File must contain 'Element ID' column.", "Missing Element ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int updatedCount = 0;
                SaveStateForUndo(); // Save state before import

                for (int i = 1; i < lines.Length; i++)
                {
                    var values = lines[i].Split(',');
                    if (values.Length <= elementIdIndex) continue;

                    var elementIdStr = values[elementIdIndex].Trim();
                    if (!long.TryParse(elementIdStr, out long elementId)) continue;

                    // Find matching row
                    var scheduleRow = ScheduleData.FirstOrDefault(row => row.GetElement().Id.IntegerValue == elementId);
                    if (scheduleRow == null) continue;

                    // Update values
                    for (int j = 0; j < headers.Length && j < values.Length; j++)
                    {
                        if (j == elementIdIndex) continue; // Skip Element ID column

                        var columnName = headers[j].Trim();
                        var newValue = values[j].Trim();
                        
                        if (scheduleRow.Values.ContainsKey(columnName))
                        {
                            scheduleRow[columnName] = newValue;
                        }
                    }
                    updatedCount++;
                }

                MessageBox.Show($"Successfully imported data for {updatedCount} elements.", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading file: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToExcel(string filePath)
        {
            try
            {
                var lines = new List<string>();
                
                // Get visible fields for headers
                var visibleFields = SelectedSchedule.Definition.GetFieldOrder()
                    .Select(id => SelectedSchedule.Definition.GetField(id))
                    .Where(f => !f.IsHidden).ToList();

                // Create header line
                var headers = new List<string> { "Element ID" };
                headers.AddRange(visibleFields.Select(f => f.GetName()));
                lines.Add(string.Join(",", headers.Select(h => $"\"{h}\"")));

                // Create data lines
                foreach (var row in ScheduleData)
                {
                    var values = new List<string> { row.GetElement().Id.IntegerValue.ToString() };
                    
                    foreach (var field in visibleFields)
                    {
                        var fieldName = field.GetName();
                        var value = row.Values.ContainsKey(fieldName) ? row.Values[fieldName] : "";
                        values.Add($"\"{value}\"");
                    }
                    
                    lines.Add(string.Join(",", values));
                }

                // Write to file (CSV format for simplicity)
                var csvPath = System.IO.Path.ChangeExtension(filePath, ".csv");
                System.IO.File.WriteAllLines(csvPath, lines);
                
                MessageBox.Show($"Data exported to: {csvPath}\n\nNote: File saved as CSV format for compatibility.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error writing file: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #endregion
    }
}
