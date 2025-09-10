using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Grid = System.Windows.Controls.Grid;
using Rectangle = System.Windows.Shapes.Rectangle;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;

namespace RevitScheduleEditor
{
    public class ColumnHeaderInfo
    {
        public string ParameterType { get; set; }
        public string ParameterGroup { get; set; }
    }


    public partial class ScheduleEditorWindow : Window
    {
        private readonly ScheduleEditorViewModel _viewModel;
        private Dictionary<string, HashSet<string>> _columnFilters = new Dictionary<string, HashSet<string>>();
        private Dictionary<string, List<string>> _columnValues = new Dictionary<string, List<string>>();

        // Debug logging methods
        private void DebugLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string fullMessage = $"[ScheduleEditorWindow] {timestamp} - {message}";
            OutputDebugStringA(fullMessage + "\r\n");
            Debug.WriteLine(fullMessage);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
        private static extern void OutputDebugStringA(string lpOutputString);

        public ScheduleEditorWindow(Document doc)
        {
            DebugLog("=== ScheduleEditorWindow Constructor Started ===");
            
            try
            {
                InitializeComponent();
                DebugLog("InitializeComponent completed");
                
                _viewModel = new ScheduleEditorViewModel(doc);
                DebugLog("ScheduleEditorViewModel created");
                
                this.DataContext = _viewModel;
                DebugLog("DataContext set to ViewModel");
                
                // Setup Excel-like behaviors
                SetupExcelLikeBehaviors();
                DebugLog("Excel-like behaviors setup completed");
                
                _viewModel.PropertyChanged += (sender, args) =>
                {
                    DebugLog($"ViewModel PropertyChanged: {args.PropertyName}");
                    
                    if (args.PropertyName == nameof(_viewModel.SelectedSchedule))
                    {
                        DebugLog("SelectedSchedule changed - waiting for Preview/Edit button");
                        // Don't auto-generate columns anymore - wait for Preview/Edit button
                    }
                    else if (args.PropertyName == nameof(_viewModel.ScheduleData))
                    {
                        DebugLog("ScheduleData changed - regenerating DataGrid columns");
                        // Data updated - regenerate columns only when data is loaded
                        GenerateDataGridColumns();
                    }
                };
                
                // Gọi ngay lập tức nếu đã có SelectedSchedule
                this.Loaded += (sender, args) =>
                {
                    DebugLog("Window Loaded event fired");
                    // Don't auto-load data anymore - user needs to click Preview/Edit
                    
                    // Setup Excel-like enhanced autofill
                    SetupEnhancedAutofill();
                    DebugLog("Enhanced autofill setup completed");
                };
                
                DebugLog("ScheduleEditorWindow constructor completed successfully");
            }
            catch (Exception ex)
            {
                DebugLog($"ERROR in ScheduleEditorWindow constructor: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        private void SetupExcelLikeBehaviors()
        {
            var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
            if (dataGrid == null) return;

            // Enable double-click to edit
            dataGrid.BeginningEdit += (sender, e) =>
            {
                // Save current state for undo
                _viewModel.SaveStateForUndo();
            };

            // Handle cell editing
            dataGrid.CellEditEnding += (sender, e) =>
            {
                if (e.EditAction == DataGridEditAction.Commit)
                {
                    DebugLog($"CellEditEnding - Committing edit for column: {e.Column?.Header}");
                    
                    // Update will be handled by binding automatically
                    // Refresh Update Model button state after edit
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        DebugLog("CellEditEnding - Refreshing Update Model button state");
                        _viewModel.RefreshUpdateButtonState();
                        
                        // Also log current modified count for debugging
                        var modifiedCount = _viewModel.ScheduleData?.Count(row => row.IsModified) ?? 0;
                        DebugLog($"CellEditEnding - Modified rows count: {modifiedCount}");
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                else
                {
                    DebugLog($"CellEditEnding - Edit cancelled for column: {e.Column?.Header}");
                }
            };

            // Add more debug events
            dataGrid.BeginningEdit += (sender, e) =>
            {
                DebugLog($"BeginningEdit - Starting edit for column: {e.Column?.Header}, Row: {dataGrid.Items.IndexOf(e.Row.Item)}");
            };

            dataGrid.PreparingCellForEdit += (sender, e) =>
            {
                DebugLog($"PreparingCellForEdit - Column: {e.Column?.Header}");
            };

            // Add selection debugging
            dataGrid.SelectionChanged += (sender, e) =>
            {
                var selectedCellsCount = dataGrid.SelectedCells?.Count ?? 0;
                var selectedItemsCount = dataGrid.SelectedItems?.Count ?? 0;
                DebugLog($"SelectionChanged - SelectedCells: {selectedCellsCount}, SelectedItems: {selectedItemsCount}");
            };

            dataGrid.CurrentCellChanged += (sender, e) =>
            {
                DebugLog($"CurrentCellChanged - Current cell: {dataGrid.CurrentCell.Column?.Header}");
            };

            // Handle Enter key to move to next cell (like Excel)
            dataGrid.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    
                    // Move to next row, same column
                    var currentCell = dataGrid.CurrentCell;
                    var currentRowIndex = dataGrid.Items.IndexOf(currentCell.Item);
                    var currentColumnIndex = currentCell.Column.DisplayIndex;
                    
                    if (currentRowIndex < dataGrid.Items.Count - 1)
                    {
                        dataGrid.CurrentCell = new DataGridCellInfo(
                            dataGrid.Items[currentRowIndex + 1],
                            dataGrid.Columns[currentColumnIndex]);
                        dataGrid.BeginEdit();
                    }
                }
                else if (e.Key == Key.Tab)
                {
                    e.Handled = true;
                    
                    // Move to next column, same row (or next row if at end)
                    var currentCell = dataGrid.CurrentCell;
                    var currentRowIndex = dataGrid.Items.IndexOf(currentCell.Item);
                    var currentColumnIndex = currentCell.Column.DisplayIndex;
                    
                    if (currentColumnIndex < dataGrid.Columns.Count - 1)
                    {
                        dataGrid.CurrentCell = new DataGridCellInfo(
                            currentCell.Item,
                            dataGrid.Columns[currentColumnIndex + 1]);
                    }
                    else if (currentRowIndex < dataGrid.Items.Count - 1)
                    {
                        dataGrid.CurrentCell = new DataGridCellInfo(
                            dataGrid.Items[currentRowIndex + 1],
                            dataGrid.Columns[0]);
                    }
                    dataGrid.BeginEdit();
                }
                else if (e.Key == Key.F2)
                {
                    // F2 to edit current cell (like Excel)
                    dataGrid.BeginEdit();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    // Escape to cancel edit
                    dataGrid.CancelEdit();
                    e.Handled = true;
                }
            };

            // Handle direct typing to start editing (like Excel)
            dataGrid.PreviewTextInput += (sender, e) =>
            {
                if (!dataGrid.IsReadOnly && dataGrid.CurrentCell.IsValid)
                {
                    dataGrid.BeginEdit();
                }
            };
        }

        private void GenerateDataGridColumns()
        {
            var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
            if (dataGrid == null) return;
            
            dataGrid.Columns.Clear();
            if (_viewModel.SelectedSchedule == null) return;

            var visibleFields = _viewModel.SelectedSchedule.Definition.GetFieldOrder()
                .Select(id => _viewModel.SelectedSchedule.Definition.GetField(id))
                .Where(f => !f.IsHidden).ToList();

            // Get the custom styles
            var excelCellStyle = this.FindResource("ExcelLikeCellStyle") as Style;
            var filterHeaderStyle = this.FindResource("FilterColumnHeaderStyle") as Style;

            // Add Element ID column first
            var elementIdColumn = new DataGridTextColumn
            {
                Header = "Element ID",
                Binding = new System.Windows.Data.Binding("Id") { Mode = BindingMode.OneWay },
                Width = new DataGridLength(100),
                IsReadOnly = true,
                CellStyle = excelCellStyle,
                HeaderStyle = filterHeaderStyle
            };
            dataGrid.Columns.Add(elementIdColumn);

            // Add regular schedule fields
            foreach (var field in visibleFields)
            {
                string fieldName = field.GetName();
                var column = new DataGridTextColumn
                {
                    Header = fieldName,
                    Binding = new System.Windows.Data.Binding($"[{fieldName}]") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                    CellStyle = excelCellStyle,  // Apply Excel-like style with fill handle
                    HeaderStyle = filterHeaderStyle  // Apply integrated filter header style
                };
                dataGrid.Columns.Add(column);
            }
            
            // Setup fill handle interactions
            SetupFillHandleInteractions(dataGrid);
        }

        // Event handler for integrated filter buttons in column headers
        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            DebugLog("FilterButton_Click - Started");
            
            // Cancel any active edit operations first
            var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
            if (dataGrid != null)
            {
                try
                {
                    dataGrid.CancelEdit();
                    dataGrid.CommitEdit();
                }
                catch
                {
                    // Ignore edit cancellation errors
                }
            }

            var button = sender as Button;
            if (button == null) 
            {
                DebugLog("FilterButton_Click - Button is null, returning");
                return;
            }

            // Find the column header and get column name
            var header = FindParent<DataGridColumnHeader>(button);
            if (header?.Content == null) 
            {
                DebugLog("FilterButton_Click - Header or Header.Content is null, returning");
                return;
            }

            string columnName = header.Content.ToString();
            DebugLog($"FilterButton_Click - Column name: {columnName}");
            
            // Create and show filter popup
            ShowFilterPopup(button, columnName);
        }

        private void ShowFilterPopup(Button button, string columnName)
        {
            DebugLog($"ShowFilterPopup - Started for column: {columnName}");
            
            if (_viewModel.ScheduleData.Count == 0) 
            {
                DebugLog("ShowFilterPopup - ScheduleData is empty, returning");
                return;
            }

            // Get unique values for this column
            var uniqueValues = new List<string>();
            
            if (columnName == "Element ID")
            {
                // Special handling for Element ID column
                uniqueValues = _viewModel.ScheduleData
                    .Select(row => row.GetElement().Id.IntegerValue.ToString())
                    .Distinct()
                    .OrderBy(v => long.Parse(v))
                    .ToList();
            }
            else
            {
                // Regular column values - normalize them for consistent filtering
                uniqueValues = _viewModel.ScheduleData
                    .Select(row => row.Values.ContainsKey(columnName) ? row.Values[columnName] : "")
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Select(v => (v ?? "").Trim()) // Normalize by trimming whitespace
                    .Distinct(StringComparer.OrdinalIgnoreCase) // Case-insensitive distinct
                    .OrderBy(v => v, StringComparer.OrdinalIgnoreCase) // Case-insensitive sort
                    .ToList();
            }

            DebugLog($"ShowFilterPopup - Found {uniqueValues.Count} unique values");

            if (uniqueValues.Count == 0) 
            {
                DebugLog("ShowFilterPopup - No unique values found, returning");
                return;
            }

            // Get current filter values for this column
            var currentFilters = _columnFilters.ContainsKey(columnName) 
                ? _columnFilters[columnName].ToList() 
                : uniqueValues;

            DebugLog($"ShowFilterPopup - Current filters count: {currentFilters.Count}");

            // Show Text Filters Window
            try 
            {
                DebugLog("ShowFilterPopup - Creating TextFiltersWindow");
                var filterWindow = new TextFiltersWindow()
                {
                    Owner = this
                };
                
                // Set filter data using the new method
                filterWindow.SetFilterData(uniqueValues, _columnFilters.ContainsKey(columnName) ? _columnFilters[columnName] : null);

                DebugLog("ShowFilterPopup - Showing TextFiltersWindow dialog");
                if (filterWindow.ShowDialog() == true)
                {
                    DebugLog("ShowFilterPopup - Dialog returned True, applying filters");
                    // Apply the selected filters
                    var selectedValues = filterWindow.SelectedValues;
                    
                    DebugLog($"ShowFilterPopup - Selected {selectedValues.Count} out of {uniqueValues.Count} total values");
                    
                    if (selectedValues.Count == 0)
                    {
                        // No items selected - hide all (create empty filter)
                        _columnFilters[columnName] = new HashSet<string>(); // Empty set = hide all
                        DebugLog($"ShowFilterPopup - No items selected, created empty filter for {columnName} (hide all)");
                    }
                    else if (selectedValues.Count == uniqueValues.Count)
                    {
                        // All items selected - show all (remove filter)  
                        _columnFilters.Remove(columnName);
                        DebugLog($"ShowFilterPopup - All items selected, removed filter for {columnName}");
                    }
                    else
                    {
                        // Some items selected - apply filter
                        _columnFilters[columnName] = new HashSet<string>(selectedValues);
                        DebugLog($"ShowFilterPopup - Applied filter for {columnName} with {selectedValues.Count} items");
                        
                        // Log first few selected values for debugging
                        DebugLog($"ShowFilterPopup - Selected values: {string.Join(", ", selectedValues.Take(5))}{(selectedValues.Count > 5 ? "..." : "")}");
                    }

                    // Update the column header visual to show filter status
                    DebugLog($"ShowFilterPopup - Updating column header status for {columnName}");
                    UpdateColumnHeaderFilterStatus(columnName, _columnFilters.ContainsKey(columnName));
                    
                    // Apply all filters to refresh the view
                    DebugLog("ShowFilterPopup - Calling ApplyFilters()");
                    ApplyFilters();
                    DebugLog("ShowFilterPopup - ApplyFilters() completed");
                }
                else
                {
                    DebugLog("ShowFilterPopup - Dialog was cancelled");
                }
            }
            catch (Exception ex)
            {
                DebugLog($"ShowFilterPopup - Exception: {ex.Message}");
                DebugLog($"ShowFilterPopup - Exception StackTrace: {ex.StackTrace}");
            }
        }

        private void ApplyColumnFilter(string columnName, ListBox listBox)
        {
            try
            {
                // Cancel any current edit operations before applying filter
                var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
                if (dataGrid != null)
                {
                    dataGrid.CancelEdit();
                    dataGrid.CommitEdit();
                }

                // Get selected values (skip "Select All" checkbox and separator)
                var selectedValues = listBox.Items.OfType<CheckBox>()
                    .Where(cb => cb.Content.ToString() != "(Select All)")
                    .Where(cb => cb.IsChecked == true)
                    .Select(cb => cb.Content.ToString())
                    .ToHashSet();

                // Get all possible values for comparison (excluding "Select All")
                var allValues = listBox.Items.OfType<CheckBox>()
                    .Where(cb => cb.Content.ToString() != "(Select All)")
                    .Select(cb => cb.Content.ToString())
                    .ToHashSet();

                bool hasFilter = selectedValues.Count != allValues.Count;

                // Debug info
                System.Diagnostics.Debug.WriteLine($"Filter for '{columnName}': Selected {selectedValues.Count} of {allValues.Count} values");

                if (selectedValues.Count == 0)
                {
                    // If nothing selected, show all by removing filter
                    if (_columnFilters.ContainsKey(columnName))
                        _columnFilters.Remove(columnName);
                    hasFilter = false;
                }
                else if (selectedValues.Count == allValues.Count)
                {
                    // If all selected, remove filter (show all)
                    if (_columnFilters.ContainsKey(columnName))
                        _columnFilters.Remove(columnName);
                    hasFilter = false;
                }
                else
                {
                    // Update column filters
                    _columnFilters[columnName] = selectedValues;
                    System.Diagnostics.Debug.WriteLine($"Applied filter values: {string.Join(", ", selectedValues)}");
                }

                // Update column header appearance
                UpdateColumnHeaderAppearance(columnName, hasFilter);

                // Apply all filters with proper synchronization
                var collectionView = CollectionViewSource.GetDefaultView(_viewModel.ScheduleData);
                
                // Use Dispatcher to ensure UI is not in edit mode
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        collectionView.Filter = FilterPredicate;
                        collectionView.Refresh();
                        System.Diagnostics.Debug.WriteLine($"After filter: {collectionView.Cast<object>().Count()} items visible");
                    }
                    catch (Exception filterEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Filter application error: {filterEx.Message}");
                        // If filter fails, clear all filters and show all data
                        collectionView.Filter = null;
                        collectionView.Refresh();
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying filter: {ex.Message}", "Filter Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateColumnHeaderAppearance(string columnName, bool hasFilter)
        {
            var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
            if (dataGrid == null) return;

            // Force the visual tree to be generated
            dataGrid.UpdateLayout();

            foreach (DataGridColumn column in dataGrid.Columns)
            {
                if (column.Header.ToString() == columnName)
                {
                    // Set Tag on the column to trigger visual state
                    if (hasFilter)
                    {
                        column.HeaderStyle = this.FindResource("FilterColumnHeaderStyleActive") as Style ?? column.HeaderStyle;
                    }
                    else
                    {
                        column.HeaderStyle = this.FindResource("FilterColumnHeaderStyle") as Style;
                    }
                    break;
                }
            }
        }

        private DataGridColumnHeader GetColumnHeader(DataGrid dataGrid, DataGridColumn column)
        {
            // Get the column header presenter
            var presenter = GetVisualChild<DataGridColumnHeadersPresenter>(dataGrid);
            if (presenter != null)
            {
                for (int i = 0; i < presenter.Items.Count; i++)
                {
                    var header = presenter.ItemContainerGenerator.ContainerFromIndex(i) as DataGridColumnHeader;
                    if (header != null && header.Column == column)
                    {
                        return header;
                    }
                }
            }
            return null;
        }

        private T GetVisualChild<T>(DependencyObject parent) where T : Visual
        {
            T child = default(T);
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null)
                {
                    child = GetVisualChild<T>(v);
                }
                if (child != null)
                {
                    break;
                }
            }
            return child;
        }

        private bool FilterPredicate(object item)
        {
            if (item is ScheduleRow row)
            {
                // Global search filter
                if (!string.IsNullOrEmpty(_viewModel.SearchText))
                {
                    bool globalMatch = row.Values.Values.Any(val => 
                        val.ToLower().Contains(_viewModel.SearchText.ToLower()));
                    if (!globalMatch) return false;
                }

                // Column-specific filters
                foreach (var filter in _columnFilters)
                {
                    string cellValue;
                    
                    // Special handling for Element ID column
                    if (filter.Key == "Element ID")
                    {
                        cellValue = row.GetElement().Id.IntegerValue.ToString();
                    }
                    else
                    {
                        cellValue = row[filter.Key] ?? string.Empty;
                    }
                    
                    // Debug logging
                    System.Diagnostics.Debug.WriteLine($"Filter '{filter.Key}': checking '{cellValue}' against {filter.Value.Count} selected values");
                    
                    // If filter has selected values and cell value is not in the selected set, exclude
                    if (filter.Value.Count > 0 && !filter.Value.Contains(cellValue))
                    {
                        System.Diagnostics.Debug.WriteLine($"Excluding row because '{cellValue}' not in selected values");
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        private void SetupFillHandleInteractions(DataGrid dataGrid)
        {
            // Variables để track fill handle dragging
            bool isDragging = false;
            DataGridCell startCell = null;
            Point startPoint;

            dataGrid.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                // Check if mouse is over a fill handle
                var element = e.OriginalSource as FrameworkElement;
                if (element?.Name == "FillHandle")
                {
                    isDragging = true;
                    startCell = FindParent<DataGridCell>(element);
                    startPoint = e.GetPosition(dataGrid);
                    dataGrid.CaptureMouse();
                    e.Handled = true;
                }
            };

            dataGrid.PreviewMouseMove += (sender, e) =>
            {
                if (isDragging && startCell != null)
                {
                    var currentPosition = e.GetPosition(dataGrid);
                    
                    // Visual feedback during drag
                    var endCell = GetCellFromPoint(dataGrid, currentPosition);
                    if (endCell != null)
                    {
                        // Highlight selection range
                        HighlightFillRange(dataGrid, startCell, endCell);
                    }
                }
            };

            dataGrid.PreviewMouseLeftButtonUp += (sender, e) =>
            {
                if (isDragging && startCell != null)
                {
                    var endPosition = e.GetPosition(dataGrid);
                    var endCell = GetCellFromPoint(dataGrid, endPosition);
                    
                    if (endCell != null && endCell != startCell)
                    {
                        // Perform autofill operation
                        PerformFillHandleAutofill(startCell, endCell);
                    }
                    
                    isDragging = false;
                    startCell = null;
                    dataGrid.ReleaseMouseCapture();
                    ClearFillHighlight(dataGrid);
                }
            };
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private DataGridCell GetCellFromPoint(DataGrid dataGrid, Point point)
        {
            var hitTest = VisualTreeHelper.HitTest(dataGrid, point);
            if (hitTest?.VisualHit != null)
            {
                return FindParent<DataGridCell>(hitTest.VisualHit);
            }
            return null;
        }

        private void HighlightFillRange(DataGrid dataGrid, DataGridCell startCell, DataGridCell endCell)
        {
            // Clear previous highlight
            ClearFillHighlight(dataGrid);
            
            // Get start and end positions
            var startRow = dataGrid.Items.IndexOf(startCell.DataContext);
            var startCol = startCell.Column.DisplayIndex;
            var endRow = dataGrid.Items.IndexOf(endCell.DataContext);
            var endCol = endCell.Column.DisplayIndex;
            
            // Ensure proper order
            var minRow = Math.Min(startRow, endRow);
            var maxRow = Math.Max(startRow, endRow);
            var minCol = Math.Min(startCol, endCol);
            var maxCol = Math.Max(startCol, endCol);
            
            // Highlight range
            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    var cell = GetCell(dataGrid, row, col);
                    if (cell != null)
                    {
                        cell.Background = new SolidColorBrush(Color.FromArgb(100, 0, 120, 212));
                    }
                }
            }
        }

        private void ClearFillHighlight(DataGrid dataGrid)
        {
            foreach (var item in dataGrid.Items)
            {
                for (int col = 0; col < dataGrid.Columns.Count; col++)
                {
                    var cell = GetCell(dataGrid, dataGrid.Items.IndexOf(item), col);
                    if (cell != null && !cell.IsSelected)
                    {
                        cell.Background = Brushes.Transparent;
                    }
                }
            }
        }

        private DataGridCell GetCell(DataGrid dataGrid, int row, int column)
        {
            if (row >= dataGrid.Items.Count || column >= dataGrid.Columns.Count)
                return null;
                
            var rowContainer = dataGrid.ItemContainerGenerator.ContainerFromIndex(row) as DataGridRow;
            if (rowContainer == null) return null;
            
            var presenter = FindVisualChild<DataGridCellsPresenter>(rowContainer);
            if (presenter == null) return null;
            
            return presenter.ItemContainerGenerator.ContainerFromIndex(column) as DataGridCell;
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;
                    
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        private void PerformFillHandleAutofill(DataGridCell startCell, DataGridCell endCell)
        {
            // Save state for undo
            _viewModel.SaveStateForUndo();
            
            var dataGrid = FindParent<DataGrid>(startCell);
            var startRow = dataGrid.Items.IndexOf(startCell.DataContext);
            var startCol = startCell.Column.DisplayIndex;
            var endRow = dataGrid.Items.IndexOf(endCell.DataContext);
            var endCol = endCell.Column.DisplayIndex;
            
            // Get start value
            var startRowData = startCell.DataContext as ScheduleRow;
            var columnName = startCell.Column.Header.ToString();
            var startValue = startRowData?.Values[columnName] ?? "";
            
            // Determine fill direction and perform autofill
            if (startRow == endRow) // Horizontal fill
            {
                var minCol = Math.Min(startCol, endCol);
                var maxCol = Math.Max(startCol, endCol);
                
                for (int col = minCol + 1; col <= maxCol; col++)
                {
                    var targetColumnName = dataGrid.Columns[col].Header.ToString();
                    var targetRowData = dataGrid.Items[startRow] as ScheduleRow;
                    if (targetRowData != null)
                    {
                        targetRowData[targetColumnName] = startValue;  // Use indexer instead of Values
                    }
                }
            }
            else if (startCol == endCol) // Vertical fill
            {
                var minRow = Math.Min(startRow, endRow);
                var maxRow = Math.Max(startRow, endRow);
                
                // Detect pattern for smart fill
                var values = new List<string>();
                for (int row = minRow; row <= Math.Min(minRow + 2, maxRow); row++)
                {
                    var rowData = dataGrid.Items[row] as ScheduleRow;
                    if (rowData?.Values.ContainsKey(columnName) == true)
                    {
                        values.Add(rowData.Values[columnName]);
                    }
                }
                
                // Perform smart fill with pattern detection
                for (int row = minRow + 1; row <= maxRow; row++)
                {
                    var targetRowData = dataGrid.Items[row] as ScheduleRow;
                    if (targetRowData != null)
                    {
                        var fillValue = GetSmartFillValue(values, row - minRow);
                        targetRowData[columnName] = fillValue;  // Use indexer instead of Values
                    }
                }
            }
            
            // Refresh the view
            dataGrid.Items.Refresh();
        }

        private string GetSmartFillValue(List<string> seedValues, int position)
        {
            if (seedValues.Count == 0) return "";
            if (seedValues.Count == 1) return seedValues[0];
            
            // Try numeric pattern
            if (seedValues.Count >= 2)
            {
                if (double.TryParse(seedValues[0], out double val1) && 
                    double.TryParse(seedValues[1], out double val2))
                {
                    double increment = val2 - val1;
                    double result = val1 + (increment * position);
                    return result.ToString();
                }
                
                // Try text with number pattern
                var match1 = System.Text.RegularExpressions.Regex.Match(seedValues[0], @"^(.+?)(\d+)(.*)$");
                var match2 = System.Text.RegularExpressions.Regex.Match(seedValues[1], @"^(.+?)(\d+)(.*)$");
                
                if (match1.Success && match2.Success && 
                    match1.Groups[1].Value == match2.Groups[1].Value &&
                    match1.Groups[3].Value == match2.Groups[3].Value)
                {
                    if (int.TryParse(match1.Groups[2].Value, out int num1) &&
                        int.TryParse(match2.Groups[2].Value, out int num2))
                    {
                        int increment = num2 - num1;
                        int result = num1 + (increment * position);
                        return $"{match1.Groups[1].Value}{result}{match1.Groups[3].Value}";
                    }
                }
            }
            
            // Default to repeating first value
            return seedValues[0];
        }

        private void SetupEnhancedAutofill()
        {
            var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
            if (dataGrid == null) return;

            // Double-click behavior: Edit mode for single cell, autofill for multiple cells
            dataGrid.MouseDoubleClick += (sender, e) =>
            {
                DebugLog($"MouseDoubleClick - SelectedCells count: {dataGrid.SelectedCells.Count}");
                
                if (dataGrid.SelectedCells.Count > 1)
                {
                    DebugLog("MouseDoubleClick - Multiple cells selected, triggering autofill");
                    _viewModel.AutofillCommand.Execute(dataGrid.SelectedCells);
                }
                else if (dataGrid.SelectedCells.Count == 1)
                {
                    DebugLog("MouseDoubleClick - Single cell selected, entering edit mode");
                    // For single cell, enter edit mode
                    try
                    {
                        dataGrid.BeginEdit();
                        DebugLog("MouseDoubleClick - BeginEdit() called successfully");
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"MouseDoubleClick - Error calling BeginEdit(): {ex.Message}");
                    }
                }
                else
                {
                    DebugLog("MouseDoubleClick - No cells selected");
                }
            };

            // Enhanced autofill on Enter key
            dataGrid.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (dataGrid.SelectedCells.Count > 1)
                    {
                        _viewModel.AutofillCommand.Execute(dataGrid.SelectedCells);
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    // Smart autofill with pattern detection
                    ExecuteSmartAutofill((System.Collections.IList)dataGrid.SelectedCells);
                    e.Handled = true;
                }
            };
        }

        private void ExecuteSmartAutofill(System.Collections.IList selectedCells)
        {
            if (selectedCells == null || selectedCells.Count < 2) return;

            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            
            // Group by column for vertical autofill
            var columnGroups = cellInfos.GroupBy(c => c.Column).ToList();
            
            foreach (var columnGroup in columnGroups)
            {
                var cellsInColumn = columnGroup.OrderBy(c => _viewModel.ScheduleData.IndexOf(c.Item as ScheduleRow)).ToList();
                if (cellsInColumn.Count < 2) continue;

                var column = cellsInColumn.First().Column as DataGridBoundColumn;
                if (column == null) continue;

                var bindingPath = (column.Binding as System.Windows.Data.Binding)?.Path.Path;
                if (string.IsNullOrEmpty(bindingPath)) continue;

                string columnName = bindingPath.Trim('[', ']');

                // Analyze pattern từ first 2 cells
                var firstRow = cellsInColumn[0].Item as ScheduleRow;
                var secondRow = cellsInColumn[1].Item as ScheduleRow;
                
                if (firstRow == null || secondRow == null) continue;

                string firstValue = firstRow[columnName];
                string secondValue = secondRow[columnName];

                // Detect pattern type
                var pattern = DetectPattern(firstValue, secondValue);
                
                // Apply pattern to remaining cells
                for (int i = 2; i < cellsInColumn.Count; i++)
                {
                    var targetRow = cellsInColumn[i].Item as ScheduleRow;
                    if (targetRow == null) continue;

                    string newValue = GenerateNextValue(firstValue, secondValue, i, pattern);
                    targetRow[columnName] = newValue;
                }
            }
        }

        private PatternType DetectPattern(string value1, string value2)
        {
            if (string.IsNullOrEmpty(value1) || string.IsNullOrEmpty(value2))
                return PatternType.Copy;

            // Try to detect numeric pattern
            if (double.TryParse(value1, out double num1) && double.TryParse(value2, out double num2))
            {
                return PatternType.Numeric;
            }

            // Try to detect text with number pattern (e.g., "Item 1", "Item 2")
            if (ExtractNumber(value1, out string text1, out int number1) && 
                ExtractNumber(value2, out string text2, out int number2) &&
                text1 == text2)
            {
                return PatternType.TextWithNumber;
            }

            // Default to copy
            return PatternType.Copy;
        }

        private string GenerateNextValue(string value1, string value2, int index, PatternType pattern)
        {
            switch (pattern)
            {
                case PatternType.Numeric:
                    if (double.TryParse(value1, out double num1) && double.TryParse(value2, out double num2))
                    {
                        double increment = num2 - num1;
                        double result = num1 + (increment * index);
                        return result.ToString();
                    }
                    break;

                case PatternType.TextWithNumber:
                    if (ExtractNumber(value1, out string text1, out int number1) && 
                        ExtractNumber(value2, out string text2, out int number2))
                    {
                        int increment = number2 - number1;
                        int newNumber = number1 + (increment * index);
                        return text1 + newNumber;
                    }
                    break;

                case PatternType.Copy:
                default:
                    return value1; // Just copy first value
            }

            return value1;
        }

        private bool ExtractNumber(string input, out string textPart, out int numberPart)
        {
            textPart = "";
            numberPart = 0;

            if (string.IsNullOrEmpty(input)) return false;

            // Find last number in string
            var match = System.Text.RegularExpressions.Regex.Match(input, @"^(.*?)(\d+)$");
            if (match.Success)
            {
                textPart = match.Groups[1].Value;
                return int.TryParse(match.Groups[2].Value, out numberPart);
            }

            return false;
        }

        private enum PatternType
        {
            Copy,
            Numeric,
            TextWithNumber
        }

        // Method to update column header visual state for filters
        private void UpdateColumnHeaderFilterStatus(string columnName, bool hasFilter)
        {
            var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
            if (dataGrid == null) return;

            var column = dataGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == columnName);
            if (column == null) return;

            // Update the column header's Tag property to indicate filter status
            // This will trigger the DataTrigger in XAML to change visual appearance
            if (column.HeaderTemplate?.LoadContent() is FrameworkElement headerContent)
            {
                headerContent.Tag = hasFilter ? "HasFilter" : null;
            }
        }

        // Method to apply all active filters to the data
        private void ApplyFilters()
        {
            DebugLog($"ApplyFilters - Started, active filters: {_columnFilters.Count}");
            
            if (_viewModel?.ScheduleData == null) 
            {
                DebugLog("ApplyFilters - ScheduleData is null, returning");
                return;
            }

            try
            {
                var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
                if (dataGrid == null) 
                {
                    DebugLog("ApplyFilters - DataGrid not found, returning");
                    return;
                }

                DebugLog($"ApplyFilters - Original data count: {_viewModel.ScheduleData.Count}");

                // If no filters, show all data
                if (_columnFilters.Count == 0)
                {
                    DebugLog("ApplyFilters - No filters active, showing all data");
                    dataGrid.ItemsSource = _viewModel.ScheduleData;
                    return;
                }

                // Create filtered collection
                var filteredData = _viewModel.ScheduleData.AsEnumerable();
                int debugRowCount = 0; // Counter for debug logging
                
                foreach (var filter in _columnFilters)
                {
                    string columnName = filter.Key;
                    var allowedValues = filter.Value;
                    
                    DebugLog($"ApplyFilters - Applying filter for column '{columnName}' with {allowedValues.Count} allowed values");

                    filteredData = filteredData.Where(row =>
                    {
                        string cellValue = "";
                        
                        if (columnName == "Element ID")
                        {
                            cellValue = row.GetElement().Id.IntegerValue.ToString();
                        }
                        else if (row.Values.ContainsKey(columnName))
                        {
                            cellValue = row.Values[columnName];
                        }

                        // Normalize values for comparison (trim whitespace, handle null/empty)
                        string normalizedCellValue = (cellValue ?? "").Trim();
                        
                        // Check if the normalized cell value is in the allowed values
                        // Use case-insensitive comparison and also check trimmed versions
                        bool isIncluded = allowedValues.Any(allowed => 
                            string.Equals((allowed ?? "").Trim(), normalizedCellValue, StringComparison.OrdinalIgnoreCase));
                        
                        // Debug log for first few rows to see what's happening
                        if (debugRowCount < 5)
                        {
                            DebugLog($"ApplyFilters - Row {debugRowCount}: Column='{columnName}'");
                            DebugLog($"ApplyFilters - Original CellValue='{cellValue}', Normalized='{normalizedCellValue}', IsIncluded={isIncluded}");
                            DebugLog($"ApplyFilters - AllowedValues: [{string.Join(", ", allowedValues.Select(v => $"'{v}'").Take(5))}{(allowedValues.Count > 5 ? "..." : "")}]");
                            debugRowCount++;
                        }
                        
                        return isIncluded;
                    });
                }

                var resultList = filteredData.ToList();
                DebugLog($"ApplyFilters - Filtered data count: {resultList.Count}");

                // Update DataGrid's ItemsSource
                dataGrid.ItemsSource = resultList;
                
                DebugLog("ApplyFilters - DataGrid ItemsSource updated successfully");
            }
            catch (Exception ex)
            {
                DebugLog($"ApplyFilters - Error applying filters: {ex.Message}");
                DebugLog($"ApplyFilters - StackTrace: {ex.StackTrace}");
                
                // On error, show all data
                var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
                if (dataGrid != null)
                {
                    dataGrid.ItemsSource = _viewModel.ScheduleData;
                    DebugLog("ApplyFilters - Restored original data due to error");
                }
            }
        }
    }
}
