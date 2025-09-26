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
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
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
                
                _viewModel = new ScheduleEditorViewModel(doc, this); // Pass this window to ViewModel
                DebugLog("ScheduleEditorViewModel created with parent window reference");
                
                this.DataContext = _viewModel;
                DebugLog("DataContext set to ViewModel");
                
                // Add ComboBox event handler for debugging
                ScheduleComboBox.SelectionChanged += (sender, e) =>
                {
                    var comboBox = sender as ComboBox;
                    DebugLog($"ComboBox SelectionChanged - SelectedItem: {comboBox?.SelectedItem?.GetType().Name ?? "null"} - {(comboBox?.SelectedItem as ViewSchedule)?.Name ?? "null"}");
                    DebugLog($"ComboBox SelectedIndex: {comboBox?.SelectedIndex}");
                    DebugLog($"ViewModel SelectedSchedule after ComboBox change: {_viewModel.SelectedSchedule?.Name ?? "null"}");
                };
                
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

        // Non-blocking notification helper for window
        private void ShowNonBlockingNotification(string message, string title = "Thông báo", int autoCloseSeconds = 3)
        {
            try
            {
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var notificationWindow = new Window
                    {
                        Title = title,
                        Content = new TextBlock
                        {
                            Text = message,
                            Margin = new Thickness(20),
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 400
                        },
                        SizeToContent = SizeToContent.WidthAndHeight,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        Topmost = true,
                        ResizeMode = ResizeMode.NoResize,
                        ShowInTaskbar = false, // Notification không cần taskbar
                        // Bỏ Owner để tránh lock trong Revit add-in
                    };

                    notificationWindow.Show();

                    // Auto-close timer
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(autoCloseSeconds) };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        notificationWindow.Close();
                    };
                    timer.Start();
                }));
            }
            catch (Exception ex)
            {
                // Fallback to debug log if UI fails
                DebugLog($"ShowNonBlockingNotification failed: {ex.Message}. Original message: {message}");
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
                
                // Debug: Log first few selected cells
                if (selectedCellsCount > 0)
                {
                    var firstFew = dataGrid.SelectedCells.Take(3);
                    foreach (var cell in firstFew)
                    {
                        DebugLog($"  Selected cell: {cell.Column?.Header} in item {dataGrid.Items.IndexOf(cell.Item)}");
                    }
                }
            };

            // Add mouse event debugging
            dataGrid.MouseDown += (sender, e) =>
            {
                DebugLog($"MouseDown - Button: {e.ChangedButton}, Position: {e.GetPosition(dataGrid)}");
            };
            
            dataGrid.MouseUp += (sender, e) =>
            {
                var selectedCellsCount = dataGrid.SelectedCells?.Count ?? 0;
                DebugLog($"MouseUp - Button: {e.ChangedButton}, SelectedCells after: {selectedCellsCount}");
            };

            // Context menu is now defined in XAML - no need to create it here
            // The code-behind context menu was overriding the XAML one
            // var contextMenu = new ContextMenu();
            // var copyMenuItem = new MenuItem { Header = "Copy (Ctrl+C)" };
            // copyMenuItem.Click += (s, e) => {
            //     DebugLog("Context menu Copy clicked");
            //     CopyCells();
            // };
            // var pasteMenuItem = new MenuItem { Header = "Paste (Ctrl+V)" };
            // pasteMenuItem.Click += (s, e) => {
            //     DebugLog("Context menu Paste clicked");
            //     PasteCells();
            // };
            // contextMenu.Items.Add(copyMenuItem);
            // contextMenu.Items.Add(pasteMenuItem);
            // dataGrid.ContextMenu = contextMenu;

            dataGrid.CurrentCellChanged += (sender, e) =>
            {
                DebugLog($"CurrentCellChanged - Current cell: {dataGrid.CurrentCell.Column?.Header}");
            };

            // Handle Enter key to move to next cell (like Excel)
            dataGrid.PreviewKeyDown += (sender, e) =>
            {
                DebugLog($"PreviewKeyDown - Key: {e.Key}, Modifiers: {Keyboard.Modifiers}");
                
                if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    DebugLog($"Ctrl+C detected in PreviewKeyDown - Selected cells: {dataGrid.SelectedCells.Count}");
                    // Ctrl+C: Copy selected cells
                    CopyCells();
                    e.Handled = true;
                }
                else if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    DebugLog($"Ctrl+V detected in PreviewKeyDown - Current cell: {dataGrid.CurrentCell.Column?.Header}");
                    // Ctrl+V: Paste cells
                    PasteCells();
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter)
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
            
            // Add column header click handler for selecting entire column
            dataGrid.PreviewMouseLeftButtonDown += DataGrid_PreviewMouseLeftButtonDown;
            
            // Add Ctrl+C handler for copy functionality
            dataGrid.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    DebugLog("Ctrl+C pressed - Using native DataGrid copy");
                    try
                    {
                        // Let DataGrid handle the copy with its built-in functionality
                        ApplicationCommands.Copy.Execute(null, dataGrid);
                        e.Handled = true;
                        DebugLog("Native DataGrid copy executed successfully");
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"Native DataGrid copy failed: {ex.Message}");
                        // Fallback to custom copy
                        CopyCells();
                        e.Handled = true;
                    }
                }
            };
        }

        // Event handler for column header click - select entire column
        private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (dataGrid == null) return;
            
            // Check if click is on a column header
            var hitTest = VisualTreeHelper.HitTest(dataGrid, e.GetPosition(dataGrid));
            if (hitTest?.VisualHit == null) return;
            
            // Find if we clicked on a DataGridColumnHeader (but not on the filter button)
            var columnHeader = FindParent<DataGridColumnHeader>(hitTest.VisualHit as DependencyObject);
            if (columnHeader?.Column == null) return;
            
            // Check if we clicked on the filter button - if so, don't select column
            var filterButton = FindParent<Button>(hitTest.VisualHit as DependencyObject);
            if (filterButton != null && filterButton.Name == "FilterButton") 
            {
                DebugLog($"DataGrid_PreviewMouseLeftButtonDown - Clicked on filter button, ignoring column selection");
                return;
            }
            
            DebugLog($"DataGrid_PreviewMouseLeftButtonDown - Column header clicked: {columnHeader.Column.Header}");
            
            try
            {
                // Clear current selection first
                dataGrid.SelectedCells.Clear();
                
                // Select all cells in this column
                foreach (var item in dataGrid.Items)
                {
                    var cellInfo = new DataGridCellInfo(item, columnHeader.Column);
                    dataGrid.SelectedCells.Add(cellInfo);
                }
                
                DebugLog($"DataGrid_PreviewMouseLeftButtonDown - Selected {dataGrid.SelectedCells.Count} cells in column {columnHeader.Column.Header}");
                
                // Auto-copy column values to clipboard
                CopyColumnToClipboard(dataGrid, columnHeader.Column);
                
                // Prevent further processing to avoid other click behaviors
                e.Handled = true;
            }
            catch (Exception ex)
            {
                DebugLog($"DataGrid_PreviewMouseLeftButtonDown - Error: {ex.Message}");
            }
        }

        // Copy column values to clipboard
        private void CopyColumnToClipboard(DataGrid dataGrid, DataGridColumn column)
        {
            try
            {
                var columnValues = new List<string>();
                
                // Add column header
                string columnHeader = column.Header?.ToString() ?? "Column";
                columnValues.Add(columnHeader);
                
                // Get column binding path to extract values
                var binding = (column as DataGridBoundColumn)?.Binding as System.Windows.Data.Binding;
                if (binding?.Path?.Path == null)
                {
                    DebugLog("CopyColumnToClipboard - Could not get column binding path");
                    return;
                }
                
                string propertyPath = binding.Path.Path;
                DebugLog($"CopyColumnToClipboard - Extracting values for property: {propertyPath}");
                
                // Extract values from each row
                foreach (var item in dataGrid.Items)
                {
                    if (item == null) continue;
                    
                    try
                    {
                        // Use reflection to get property value
                        var property = item.GetType().GetProperty(propertyPath);
                        if (property != null)
                        {
                            var value = property.GetValue(item);
                            string cellValue = value?.ToString() ?? "";
                            columnValues.Add(cellValue);
                        }
                        else
                        {
                            columnValues.Add("");
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"CopyColumnToClipboard - Error getting value for row: {ex.Message}");
                        columnValues.Add("");
                    }
                }
                
                // Join all values with newlines and copy to clipboard
                string clipboardText = string.Join("\n", columnValues);
                System.Windows.Clipboard.SetText(clipboardText);
                
                DebugLog($"CopyColumnToClipboard - Copied {columnValues.Count} values to clipboard for column '{columnHeader}'");
            }
            catch (Exception ex)
            {
                DebugLog($"CopyColumnToClipboard - Error: {ex.Message}");
            }
        }

        // Context menu handler for copying column
        private void CopyColumn_Click(object sender, RoutedEventArgs e)
        {
            var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
            if (dataGrid?.SelectedCells == null || dataGrid.SelectedCells.Count == 0)
            {
                DebugLog("CopyColumn_Click - No cells selected");
                return;
            }

            // Get the column from the first selected cell
            var firstCell = dataGrid.SelectedCells[0];
            if (firstCell.Column != null)
            {
                CopyColumnToClipboard(dataGrid, firstCell.Column);
            }
            else
            {
                DebugLog("CopyColumn_Click - Could not determine column from selected cell");
            }
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
            
            // Update filter status display when columns change
            UpdateFilterStatusDisplay();
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

            // Get the DataGrid to access currently visible data (after existing filters)
            var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
            var currentData = dataGrid?.ItemsSource as IEnumerable<ScheduleRow>;
            
            // If no current data source (no filters applied yet), use all data
            if (currentData == null)
            {
                currentData = _viewModel.ScheduleData;
                DebugLog($"ShowFilterPopup - Using original data source ({_viewModel.ScheduleData.Count} rows)");
            }
            else
            {
                var currentDataList = currentData.ToList();
                DebugLog($"ShowFilterPopup - Using filtered data source ({currentDataList.Count} rows)");
                currentData = currentDataList;
            }

            // Get unique values for this column FROM CURRENTLY VISIBLE DATA ONLY
            var uniqueValues = new List<string>();
            
            if (columnName == "Element ID")
            {
                // Special handling for Element ID column
                uniqueValues = currentData
                    .Select(row => row.GetElement().Id.IntegerValue.ToString())
                    .Distinct()
                    .OrderBy(v => long.Parse(v))
                    .ToList();
            }
            else
            {
                // Regular column values - include empty values for "(Blanks)" functionality
                // IMPORTANT: Only get values from currently visible/filtered data
                uniqueValues = currentData
                    .Select(row => row.Values.ContainsKey(columnName) ? row.Values[columnName] : "")
                    .Select(v => (v ?? "").Trim()) // Normalize by trimming whitespace
                    .Distinct(StringComparer.OrdinalIgnoreCase) // Case-insensitive distinct
                    .OrderBy(v => v, StringComparer.OrdinalIgnoreCase) // Case-insensitive sort
                    .ToList();
            }

            DebugLog($"ShowFilterPopup - Found {uniqueValues.Count} unique values from currently visible data");

            if (uniqueValues.Count == 0) 
            {
                DebugLog("ShowFilterPopup - No unique values found, returning");
                return;
            }

            // Get current filter values for this column
            // If column has existing filter, use those selected values
            // If no existing filter, default to showing all available values as selected
            HashSet<string> currentFilters;
            if (_columnFilters.ContainsKey(columnName))
            {
                currentFilters = _columnFilters[columnName];
                DebugLog($"ShowFilterPopup - Column {columnName} has existing filter with {currentFilters.Count} selected values");
            }
            else
            {
                // No existing filter for this column - default to all values selected
                currentFilters = new HashSet<string>(uniqueValues, StringComparer.OrdinalIgnoreCase);
                DebugLog($"ShowFilterPopup - Column {columnName} has no existing filter, defaulting to all {uniqueValues.Count} values selected");
            }

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
                // Pass the current filters (selected values) to the window
                filterWindow.SetFilterData(uniqueValues, currentFilters);

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
                    UpdateColumnHeaderAppearance(columnName, _columnFilters.ContainsKey(columnName));
                    
                    // Apply all filters to refresh the view
                    DebugLog("ShowFilterPopup - Calling ApplyFiltersEnhanced()");
                    ApplyFiltersEnhanced();
                    DebugLog("ShowFilterPopup - ApplyFiltersEnhanced() completed");
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

                // Apply all filters with enhanced method
                ApplyFiltersEnhanced();
                
            }
            catch (Exception ex)
            {
                ShowNonBlockingNotification($"Lỗi khi áp dụng bộ lọc: {ex.Message}", "Lỗi Bộ Lọc", 5);
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
                    // Apply appropriate style based on filter status
                    if (hasFilter)
                    {
                        // Try to get active filter style, fallback to creating inline style
                        var activeStyle = this.FindResource("FilterColumnHeaderStyleActive") as Style;
                        if (activeStyle != null)
                        {
                            column.HeaderStyle = activeStyle;
                        }
                        else
                        {
                            // Create inline style for filtered columns with vibrant orange highlight
                            var style = new Style(typeof(DataGridColumnHeader));
                            style.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 107, 53)))); // Vibrant orange
                            style.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(229, 81, 0)))); // Dark orange border
                            style.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 2, 2))); // Thicker border
                            style.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
                            style.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, Brushes.White)); // White text
                            
                            // Add drop shadow effect
                            var dropShadow = new DropShadowEffect
                            {
                                Color = Color.FromRgb(255, 107, 53),
                                Opacity = 0.6,
                                ShadowDepth = 1,
                                BlurRadius = 3
                            };
                            style.Setters.Add(new Setter(UIElement.EffectProperty, dropShadow));
                            
                            column.HeaderStyle = style;
                        }
                    }
                    else
                    {
                        // Reset to default style
                        column.HeaderStyle = this.FindResource("FilterColumnHeaderStyle") as Style;
                    }
                    break;
                }
            }
        }

        // Update all column headers to reflect current filter status
        private void UpdateAllColumnHeadersAppearance()
        {
            var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
            if (dataGrid == null) return;

            foreach (DataGridColumn column in dataGrid.Columns)
            {
                string columnName = column.Header?.ToString();
                if (!string.IsNullOrEmpty(columnName))
                {
                    bool hasFilter = _columnFilters.ContainsKey(columnName);
                    UpdateColumnHeaderAppearance(columnName, hasFilter);
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
                        // Check if Ctrl is pressed for different visual feedback
                        bool isCtrlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
                        
                        // Highlight selection range with different colors based on mode
                        HighlightFillRange(dataGrid, startCell, endCell, isCtrlPressed);
                        
                        // Update cursor based on mode
                        if (isCtrlPressed)
                        {
                            dataGrid.Cursor = Cursors.Cross; // Series fill cursor
                        }
                        else
                        {
                            dataGrid.Cursor = Cursors.SizeNWSE; // Normal fill cursor
                        }
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
                    dataGrid.Cursor = Cursors.Arrow; // Reset cursor
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

        private void HighlightFillRange(DataGrid dataGrid, DataGridCell startCell, DataGridCell endCell, bool isCtrlPressed = false)
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
            
            // Choose color based on fill mode
            Color highlightColor = isCtrlPressed 
                ? Color.FromArgb(120, 76, 175, 80)   // Green for Fill Series (Ctrl)
                : Color.FromArgb(100, 0, 120, 212);  // Blue for normal fill
            
            // Highlight range
            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    var cell = GetCell(dataGrid, row, col);
                    if (cell != null)
                    {
                        cell.Background = new SolidColorBrush(highlightColor);
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
            
            // Check if Ctrl is pressed for Fill Series mode
            bool isCtrlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            
            DebugLog($"PerformFillHandleAutofill - Ctrl pressed: {isCtrlPressed}, Start value: '{startValue}'");
            
            // Determine fill direction and perform autofill
            if (startRow == endRow) // Horizontal fill
            {
                var minCol = Math.Min(startCol, endCol);
                var maxCol = Math.Max(startCol, endCol);
                
                if (isCtrlPressed)
                {
                    // Fill Series horizontally với Ctrl
                    PerformHorizontalFillSeries(dataGrid, startRow, minCol, maxCol, startValue);
                }
                else
                {
                    // Copy value horizontally (original behavior)
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
            }
            else if (startCol == endCol) // Vertical fill
            {
                var minRow = Math.Min(startRow, endRow);
                var maxRow = Math.Max(startRow, endRow);
                
                if (isCtrlPressed)
                {
                    // Fill Series vertically với Ctrl
                    PerformVerticalFillSeries(dataGrid, minRow, maxRow, columnName, startValue);
                }
                else
                {
                    // Smart fill with pattern detection (original behavior)
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
            }
            
            // Refresh the view
            dataGrid.Items.Refresh();
            
            DebugLog($"PerformFillHandleAutofill completed - Mode: {(isCtrlPressed ? "Fill Series" : "Smart Fill")}");
        }

        /// <summary>
        /// Thực hiện Fill Series theo chiều ngang (horizontal) khi giữ Ctrl
        /// </summary>
        private void PerformHorizontalFillSeries(DataGrid dataGrid, int row, int minCol, int maxCol, string startValue)
        {
            DebugLog($"PerformHorizontalFillSeries - Row: {row}, Columns: {minCol} to {maxCol}, Start value: '{startValue}'");
            
            // Parse start value to determine if it's numeric or text with number
            if (double.TryParse(startValue, out double numericStart))
            {
                // Pure number - increment by 1
                DebugLog($"Horizontal Fill Series - Pure number detected: {numericStart}");
                for (int col = minCol + 1; col <= maxCol; col++)
                {
                    var targetColumnName = dataGrid.Columns[col].Header.ToString();
                    var targetRowData = dataGrid.Items[row] as ScheduleRow;
                    if (targetRowData != null)
                    {
                        double newValue = numericStart + (col - minCol);
                        targetRowData[targetColumnName] = newValue.ToString();
                        DebugLog($"Set cell [{row}, {col}] = {newValue}");
                    }
                }
            }
            else if (ExtractNumber(startValue, out string textPart, out int numberPart))
            {
                // Text with number - increment the number part
                DebugLog($"Horizontal Fill Series - Text with number detected: '{textPart}' + {numberPart}");
                
                // Check if number is at the end (most common case)
                var match = System.Text.RegularExpressions.Regex.Match(startValue, @"^(.+?)(\d+)$");
                bool numberAtEnd = match.Success;
                
                for (int col = minCol + 1; col <= maxCol; col++)
                {
                    var targetColumnName = dataGrid.Columns[col].Header.ToString();
                    var targetRowData = dataGrid.Items[row] as ScheduleRow;
                    if (targetRowData != null)
                    {
                        int newNumber = numberPart + (col - minCol);
                        string newValue = numberAtEnd ? textPart + newNumber : newNumber + textPart;
                        targetRowData[targetColumnName] = newValue;
                        DebugLog($"Set cell [{row}, {col}] = '{newValue}'");
                    }
                }
            }
            else
            {
                // Not numeric - create a simple series by appending numbers
                DebugLog($"Horizontal Fill Series - Non-numeric text, creating numbered series");
                for (int col = minCol + 1; col <= maxCol; col++)
                {
                    var targetColumnName = dataGrid.Columns[col].Header.ToString();
                    var targetRowData = dataGrid.Items[row] as ScheduleRow;
                    if (targetRowData != null)
                    {
                        string newValue = $"{startValue} {col - minCol + 1}";
                        targetRowData[targetColumnName] = newValue;
                        DebugLog($"Set cell [{row}, {col}] = '{newValue}'");
                    }
                }
            }
        }

        /// <summary>
        /// Thực hiện Fill Series theo chiều dọc (vertical) khi giữ Ctrl
        /// </summary>
        private void PerformVerticalFillSeries(DataGrid dataGrid, int minRow, int maxRow, string columnName, string startValue)
        {
            DebugLog($"PerformVerticalFillSeries - Rows: {minRow} to {maxRow}, Column: '{columnName}', Start value: '{startValue}'");
            
            // Parse start value to determine if it's numeric or text with number
            if (double.TryParse(startValue, out double numericStart))
            {
                // Pure number - increment by 1
                DebugLog($"Vertical Fill Series - Pure number detected: {numericStart}");
                for (int row = minRow + 1; row <= maxRow; row++)
                {
                    var targetRowData = dataGrid.Items[row] as ScheduleRow;
                    if (targetRowData != null)
                    {
                        double newValue = numericStart + (row - minRow);
                        targetRowData[columnName] = newValue.ToString();
                        DebugLog($"Set cell [{row}] = {newValue}");
                    }
                }
            }
            else if (ExtractNumber(startValue, out string textPart, out int numberPart))
            {
                // Text with number - increment the number part
                DebugLog($"Vertical Fill Series - Text with number detected: '{textPart}' + {numberPart}");
                
                // Check if number is at the end (most common case)
                var match = System.Text.RegularExpressions.Regex.Match(startValue, @"^(.+?)(\d+)$");
                bool numberAtEnd = match.Success;
                
                for (int row = minRow + 1; row <= maxRow; row++)
                {
                    var targetRowData = dataGrid.Items[row] as ScheduleRow;
                    if (targetRowData != null)
                    {
                        int newNumber = numberPart + (row - minRow);
                        string newValue = numberAtEnd ? textPart + newNumber : newNumber + textPart;
                        targetRowData[columnName] = newValue;
                        DebugLog($"Set cell [{row}] = '{newValue}'");
                    }
                }
            }
            else
            {
                // Not numeric - create a simple series by appending numbers
                DebugLog($"Vertical Fill Series - Non-numeric text, creating numbered series");
                for (int row = minRow + 1; row <= maxRow; row++)
                {
                    var targetRowData = dataGrid.Items[row] as ScheduleRow;
                    if (targetRowData != null)
                    {
                        string newValue = $"{startValue} {row - minRow + 1}";
                        targetRowData[columnName] = newValue;
                        DebugLog($"Set cell [{row}] = '{newValue}'");
                    }
                }
            }
        }

        /// <summary>
        /// Phân tích text để trích xuất phần text và phần số
        /// Ví dụ: "Item 5" -> textPart = "Item ", numberPart = 5
        /// </summary>
        private bool ExtractNumber(string input, out string textPart, out int numberPart)
        {
            textPart = "";
            numberPart = 0;

            if (string.IsNullOrEmpty(input))
                return false;

            // Tìm số ở cuối string (pattern phổ biến nhất: "Item 5", "Room 101", etc.)
            var match = System.Text.RegularExpressions.Regex.Match(input, @"^(.+?)(\d+)$");
            if (match.Success && int.TryParse(match.Groups[2].Value, out numberPart))
            {
                textPart = match.Groups[1].Value;
                return true;
            }

            // Tìm số ở đầu string ("5 Items", "101 Room", etc.)
            match = System.Text.RegularExpressions.Regex.Match(input, @"^(\d+)(.*)$");
            if (match.Success && int.TryParse(match.Groups[1].Value, out numberPart))
            {
                textPart = match.Groups[2].Value;
                return true;
            }

            return false;
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

            dataGrid.KeyDown += (sender, e) =>
            {
                DebugLog($"KeyDown - Key: {e.Key}, Modifiers: {Keyboard.Modifiers}");
                
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
                else if (e.Key == Key.Enter && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                {
                    // Fill Series với Ctrl+Shift+Enter
                    if (dataGrid.SelectedCells.Count > 1)
                    {
                        ExecuteFillSeries((System.Collections.IList)dataGrid.SelectedCells);
                        e.Handled = true;
                    }
                }
            };
        }

        /// <summary>
        /// Thực hiện Fill Series cho các cell đã chọn (keyboard shortcut)
        /// </summary>
        private void ExecuteFillSeries(System.Collections.IList selectedCells)
        {
            if (selectedCells == null || selectedCells.Count < 2) 
            {
                DebugLog("ExecuteFillSeries - Not enough cells selected");
                return;
            }

            DebugLog($"ExecuteFillSeries - Processing {selectedCells.Count} selected cells");
            
            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            
            // Save state for undo
            _viewModel.SaveStateForUndo();
            
            // Group by column for vertical fill series
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

                // Get first cell value as starting point
                var firstRow = cellsInColumn[0].Item as ScheduleRow;
                if (firstRow == null) continue;

                string startValue = firstRow[columnName];
                DebugLog($"ExecuteFillSeries - Column '{columnName}', Start value: '{startValue}'");

                // Apply Fill Series to all cells in this column
                var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
                var startRowIndex = _viewModel.ScheduleData.IndexOf(firstRow);
                var endRowIndex = _viewModel.ScheduleData.IndexOf(cellsInColumn.Last().Item as ScheduleRow);
                
                // Use the PerformVerticalFillSeries method
                PerformVerticalFillSeries(dataGrid, startRowIndex, endRowIndex, columnName, startValue);
            }
            
            // Refresh the view
            var dataGridControl = this.FindName("ScheduleDataGrid") as DataGrid;
            dataGridControl?.Items.Refresh();
            
            DebugLog("ExecuteFillSeries completed");
        }

        /// <summary>
        /// Context menu click handler cho Fill Series
        /// </summary>
        private void FillSeries_Click(object sender, RoutedEventArgs e)
        {
            var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
            if (dataGrid?.SelectedCells != null && dataGrid.SelectedCells.Count > 1)
            {
                DebugLog($"FillSeries_Click - Executing Fill Series for {dataGrid.SelectedCells.Count} cells");
                ExecuteFillSeries((System.Collections.IList)dataGrid.SelectedCells);
            }
            else
            {
                DebugLog("FillSeries_Click - Not enough cells selected for Fill Series");
                ShowNonBlockingNotification("Vui lòng chọn ít nhất 2 cell để sử dụng Fill Series.", "Fill Series");
            }
        }

        /// <summary>
        /// Context menu click handler cho Select Highlighted Elements
        /// </summary>
        private void SelectHighlightedElements_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DebugLog("SelectHighlightedElements_Click - Starting");
                
                var viewModel = this.DataContext as ScheduleEditorViewModel;
                if (viewModel != null)
                {
                    // Lấy dòng tại vị trí chuột thay vì dựa vào selection
                    var rowAtMousePosition = GetRowAtMousePosition();
                    
                    // Gọi command từ ViewModel với row được detect
                    if (viewModel.SelectHighlightedElementsCommand?.CanExecute(rowAtMousePosition) == true)
                    {
                        viewModel.SelectHighlightedElementsCommand.Execute(rowAtMousePosition);
                    }
                }
                else
                {
                    DebugLog("SelectHighlightedElements_Click - ViewModel is null");
                    ShowNonBlockingNotification("Không thể truy cập ViewModel.", "Lỗi", 3);
                }
            }
            catch (Exception ex)
            {
                DebugLog($"SelectHighlightedElements_Click - Error: {ex.Message}");
                ShowNonBlockingNotification($"Lỗi khi chọn elements: {ex.Message}", "Lỗi", 5);
            }
        }

        /// <summary>
        /// Context menu click handler cho Show Highlighted Elements
        /// </summary>
        private void ShowHighlightedElements_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DebugLog("ShowHighlightedElements_Click - Starting");
                
                var viewModel = this.DataContext as ScheduleEditorViewModel;
                if (viewModel != null)
                {
                    // Lấy dòng tại vị trí chuột thay vì dựa vào selection
                    var rowAtMousePosition = GetRowAtMousePosition();
                    
                    // Gọi command từ ViewModel với row được detect
                    if (viewModel.ShowHighlightedElementsCommand?.CanExecute(rowAtMousePosition) == true)
                    {
                        viewModel.ShowHighlightedElementsCommand.Execute(rowAtMousePosition);
                    }
                }
                else
                {
                    DebugLog("ShowHighlightedElements_Click - ViewModel is null");
                    ShowNonBlockingNotification("Không thể truy cập ViewModel.", "Lỗi", 3);
                }
            }
            catch (Exception ex)
            {
                DebugLog($"ShowHighlightedElements_Click - Error: {ex.Message}");
                ShowNonBlockingNotification($"Lỗi khi hiển thị thông tin elements: {ex.Message}", "Lỗi", 5);
            }
        }

        /// <summary>
        /// Lấy ScheduleRow tại vị trí chuột khi nhấp chuột phải
        /// </summary>
        private ScheduleRow GetRowAtMousePosition()
        {
            try
            {
                var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
                if (dataGrid == null)
                {
                    DebugLog("GetRowAtMousePosition - DataGrid is null");
                    return null;
                }

                // Lấy vị trí chuột hiện tại relative to DataGrid
                var mousePosition = Mouse.GetPosition(dataGrid);
                DebugLog($"GetRowAtMousePosition - Mouse position: {mousePosition.X}, {mousePosition.Y}");

                // Tìm element tại vị trí chuột
                var hitTestResult = VisualTreeHelper.HitTest(dataGrid, mousePosition);
                if (hitTestResult?.VisualHit == null)
                {
                    DebugLog("GetRowAtMousePosition - No visual hit found");
                    return null;
                }

                // Tìm DataGridRow từ visual hit
                var dataGridRow = FindParent<DataGridRow>(hitTestResult.VisualHit);
                if (dataGridRow == null)
                {
                    DebugLog("GetRowAtMousePosition - No DataGridRow found");
                    return null;
                }

                // Lấy ScheduleRow từ DataContext của row
                var scheduleRow = dataGridRow.DataContext as ScheduleRow;
                if (scheduleRow != null)
                {
                    DebugLog($"GetRowAtMousePosition - Found ScheduleRow with ID: {scheduleRow.Id?.IntegerValue ?? -1}");
                    return scheduleRow;
                }
                else
                {
                    DebugLog("GetRowAtMousePosition - DataContext is not ScheduleRow");
                    return null;
                }
            }
            catch (Exception ex)
            {
                DebugLog($"GetRowAtMousePosition - Error: {ex.Message}");
                return null;
            }
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
                    
                    // Update all column headers to reflect no filters
                    UpdateAllColumnHeadersAppearance();
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
                
                // Update all column headers to reflect current filter status
                UpdateAllColumnHeadersAppearance();
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

        #region Copy/Paste Functionality
        
        private string _clipboardData = string.Empty;
        
        private void CopyCells()
        {
            var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
            if (dataGrid == null) 
            {
                DebugLog("CopyCells - DataGrid not found");
                return;
            }
            
            var selectedCells = dataGrid.SelectedCells;
            DebugLog($"CopyCells - DataGrid found, SelectedCells count: {selectedCells?.Count ?? 0}");
            
            if (selectedCells == null || selectedCells.Count == 0)
            {
                DebugLog("CopyCells - No cells selected");
                
                // Try to get current cell if no selection
                var currentCell = dataGrid.CurrentCell;
                if (currentCell.Item != null && currentCell.Column != null)
                {
                    DebugLog($"CopyCells - Using current cell: {currentCell.Column.Header}");
                    
                    // Create a single cell selection
                    var scheduleRow = currentCell.Item as ScheduleRow;
                    var column = currentCell.Column as DataGridBoundColumn;
                    
                    if (scheduleRow != null && column != null)
                    {
                        var binding = column.Binding as System.Windows.Data.Binding;
                        if (binding != null)
                        {
                            var fieldName = binding.Path.Path.Trim('[', ']');
                            var value = scheduleRow[fieldName] ?? "";
                            
                            _clipboardData = value;
                            Clipboard.SetText(value);
                            
                            DebugLog($"CopyCells - Copied current cell value: '{value}'");
                            return;
                        }
                    }
                }
                
                DebugLog("CopyCells - No current cell available either");
                return;
            }
            
            DebugLog($"CopyCells - Processing {selectedCells.Count} selected cells");
            
            try
            {
                // Convert selected cells to text format (tab-separated)
                var cellData = new List<string>();
                
                // Group cells by row and column for proper ordering
                var cellsByRow = selectedCells
                    .Cast<DataGridCellInfo>()
                    .Where(cell => cell.Item != null && cell.Column != null)
                    .GroupBy(cell => cell.Item)
                    .OrderBy(group => dataGrid.Items.IndexOf(group.Key))
                    .ToList();
                
                DebugLog($"CopyCells - Grouped into {cellsByRow.Count} rows");
                
                foreach (var rowGroup in cellsByRow)
                {
                    var cellsInRow = rowGroup
                        .OrderBy(cell => cell.Column.DisplayIndex)
                        .ToList();
                    
                    var rowValues = new List<string>();
                    
                    foreach (var cellInfo in cellsInRow)
                    {
                        var scheduleRow = cellInfo.Item as ScheduleRow;
                        var column = cellInfo.Column as DataGridBoundColumn;
                        
                        if (scheduleRow != null && column != null)
                        {
                            var binding = column.Binding as System.Windows.Data.Binding;
                            if (binding != null)
                            {
                                var originalPath = binding.Path.Path;
                                var fieldName = originalPath.Trim('[', ']');
                                DebugLog($"CopyCells - Original binding path: '{originalPath}', Field name: '{fieldName}'");
                                
                                var value = scheduleRow[fieldName];
                                DebugLog($"CopyCells - ScheduleRow has key '{fieldName}': {scheduleRow.Values?.ContainsKey(fieldName)}");
                                
                                if (string.IsNullOrEmpty(value))
                                {
                                    // Try alternative access methods
                                    try
                                    {
                                        var property = scheduleRow.GetType().GetProperty(fieldName);
                                        if (property != null)
                                        {
                                            value = property.GetValue(scheduleRow)?.ToString() ?? "";
                                            DebugLog($"CopyCells - Got value via reflection: '{value}'");
                                        }
                                    }
                                    catch (Exception reflEx)
                                    {
                                        DebugLog($"CopyCells - Reflection failed: {reflEx.Message}");
                                    }
                                }
                                
                                rowValues.Add(value ?? "");
                                DebugLog($"CopyCells - Cell [{fieldName}] = '{value}'");
                            }
                        }
                    }
                    
                    if (rowValues.Count > 0)
                    {
                        cellData.Add(string.Join("\t", rowValues));
                    }
                }
                
                _clipboardData = string.Join("\n", cellData);
                
                // Also copy to system clipboard
                if (!string.IsNullOrEmpty(_clipboardData))
                {
                    Clipboard.SetText(_clipboardData);
                    DebugLog($"CopyCells - Successfully copied {cellData.Count} rows to clipboard");
                }
                else
                {
                    DebugLog("CopyCells - No data to copy");
                }
            }
            catch (Exception ex)
            {
                DebugLog($"CopyCells - Error: {ex.Message}");
                DebugLog($"CopyCells - StackTrace: {ex.StackTrace}");
            }
        }
        
        private void PasteCells()
        {
            var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
            if (dataGrid == null) return;
            
            var currentCell = dataGrid.CurrentCell;
            if (currentCell.Item == null || currentCell.Column == null)
            {
                DebugLog("PasteCells - No current cell selected");
                return;
            }
            
            try
            {
                string clipboardText = _clipboardData;
                
                // Try system clipboard if internal clipboard is empty
                if (string.IsNullOrEmpty(clipboardText))
                {
                    if (Clipboard.ContainsText())
                    {
                        clipboardText = Clipboard.GetText();
                    }
                }
                
                if (string.IsNullOrEmpty(clipboardText))
                {
                    DebugLog("PasteCells - No clipboard data available");
                    return;
                }
                
                DebugLog($"PasteCells - Pasting data: {clipboardText.Replace('\n', '|').Replace('\t', ',')}");
                
                // Parse clipboard data
                var rows = clipboardText.Split('\n');
                var startRowIndex = dataGrid.Items.IndexOf(currentCell.Item);
                var startColumnIndex = currentCell.Column.DisplayIndex;
                
                DebugLog($"PasteCells - Starting at row {startRowIndex}, column {startColumnIndex}");
                
                int pastedCells = 0;
                
                for (int rowOffset = 0; rowOffset < rows.Length; rowOffset++)
                {
                    var targetRowIndex = startRowIndex + rowOffset;
                    if (targetRowIndex >= dataGrid.Items.Count) break;
                    
                    var scheduleRow = dataGrid.Items[targetRowIndex] as ScheduleRow;
                    if (scheduleRow == null) continue;
                    
                    var cellValues = rows[rowOffset].Split('\t');
                    
                    for (int colOffset = 0; colOffset < cellValues.Length; colOffset++)
                    {
                        var targetColumnIndex = startColumnIndex + colOffset;
                        if (targetColumnIndex >= dataGrid.Columns.Count) break;
                        
                        var column = dataGrid.Columns[targetColumnIndex] as DataGridBoundColumn;
                        if (column == null) continue;
                        
                        var binding = column.Binding as System.Windows.Data.Binding;
                        if (binding == null) continue;
                        
                        var fieldName = binding.Path.Path.Trim('[', ']');
                        var newValue = cellValues[colOffset];
                        
                        // Set the value using the indexer
                        scheduleRow[fieldName] = newValue;
                        pastedCells++;
                        
                        DebugLog($"PasteCells - Set [{fieldName}] = '{newValue}' in row {targetRowIndex}");
                    }
                }
                
                DebugLog($"PasteCells - Successfully pasted {pastedCells} cells");
                
                // Refresh the DataGrid and Update Model button
                dataGrid.Items.Refresh();
                _viewModel.RefreshUpdateButtonState();
            }
            catch (Exception ex)
            {
                DebugLog($"PasteCells - Error: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Multi-Column AutoFilter Enhancement
        
        // Clear All Filters Button Event Handler
        private void ClearAllFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            DebugLog("ClearAllFiltersButton_Click - Started");
            
            try
            {
                // Clear all column filters
                _columnFilters.Clear();
                DebugLog("ClearAllFiltersButton_Click - Cleared all column filters");
                
                // Update all column headers to normal appearance
                UpdateAllColumnHeadersAppearance();
                DebugLog("ClearAllFiltersButton_Click - Updated column header appearances");
                
                // Apply filters (which will show all data since no filters exist)
                ApplyFilters();
                DebugLog("ClearAllFiltersButton_Click - Applied filters (show all data)");
                
                // Update filter status display
                UpdateFilterStatusDisplay();
                DebugLog("ClearAllFiltersButton_Click - Updated filter status display");
                
                // Show success message
                ShowNonBlockingNotification("Tất cả bộ lọc đã được xóa. Hiển thị toàn bộ dữ liệu.", "Xóa Bộ Lọc");
            }
            catch (Exception ex)
            {
                DebugLog($"ClearAllFiltersButton_Click - Error: {ex.Message}");
                ShowNonBlockingNotification($"Lỗi khi xóa bộ lọc: {ex.Message}", "Lỗi", 5);
            }
        }
        
        // Update Filter Status Display
        private void UpdateFilterStatusDisplay()
        {
            try
            {
                var filterStatusPanel = this.FindName("FilterStatusPanel") as StackPanel;
                var filterStatusText = this.FindName("FilterStatusText") as TextBlock;
                var filteredRowCountText = this.FindName("FilteredRowCountText") as TextBlock;
                var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
                
                if (filterStatusPanel == null || filterStatusText == null || filteredRowCountText == null)
                {
                    DebugLog("UpdateFilterStatusDisplay - UI elements not found");
                    return;
                }
                
                if (_columnFilters.Count == 0)
                {
                    // No filters active
                    filterStatusPanel.Visibility = System.Windows.Visibility.Collapsed;
                    DebugLog("UpdateFilterStatusDisplay - No filters, hiding status panel");
                }
                else
                {
                    // Show filter status
                    filterStatusPanel.Visibility = System.Windows.Visibility.Visible;
                    
                    // Create filter summary text
                    var filterSummary = new List<string>();
                    foreach (var filter in _columnFilters)
                    {
                        var columnName = filter.Key;
                        var valueCount = filter.Value.Count;
                        filterSummary.Add($"{columnName} ({valueCount} items)");
                    }
                    
                    filterStatusText.Text = string.Join(", ", filterSummary.Take(3)) + 
                                          (filterSummary.Count > 3 ? $" +{filterSummary.Count - 3} more" : "");
                    
                    // Show filtered row count
                    if (dataGrid?.ItemsSource != null)
                    {
                        var filteredCount = (dataGrid.ItemsSource as System.Collections.IEnumerable)?.Cast<object>().Count() ?? 0;
                        var totalCount = _viewModel?.ScheduleData?.Count ?? 0;
                        filteredRowCountText.Text = $"Showing {filteredCount:N0} of {totalCount:N0} rows";
                    }
                    
                    DebugLog($"UpdateFilterStatusDisplay - Showing {_columnFilters.Count} active filters");
                }
            }
            catch (Exception ex)
            {
                DebugLog($"UpdateFilterStatusDisplay - Error: {ex.Message}");
            }
        }
        
        // Get Filter Summary for Column
        private string GetFilterSummaryForColumn(string columnName)
        {
            if (!_columnFilters.ContainsKey(columnName))
                return "All";
                
            var filter = _columnFilters[columnName];
            if (filter.Count == 0)
                return "None";
                
            if (filter.Count == 1)
                return filter.First();
                
            return $"{filter.Count} selected";
        }
        
        // Enhanced Apply Filters with better performance and logging
        private void ApplyFiltersEnhanced()
        {
            DebugLog($"ApplyFiltersEnhanced - Started, active filters: {_columnFilters.Count}");
            
            if (_viewModel?.ScheduleData == null) 
            {
                DebugLog("ApplyFiltersEnhanced - ScheduleData is null");
                return;
            }

            try
            {
                var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
                if (dataGrid == null) 
                {
                    DebugLog("ApplyFiltersEnhanced - DataGrid not found");
                    return;
                }

                var totalRows = _viewModel.ScheduleData.Count;
                DebugLog($"ApplyFiltersEnhanced - Total rows: {totalRows}");

                // If no filters, show all data
                if (_columnFilters.Count == 0)
                {
                    dataGrid.ItemsSource = _viewModel.ScheduleData;
                    DebugLog("ApplyFiltersEnhanced - No filters, showing all data");
                    UpdateFilterStatusDisplay();
                    return;
                }

                // Apply multi-column AND logic filtering
                var filteredData = _viewModel.ScheduleData.Where(row =>
                {
                    // Check all active filters - ALL must pass (AND logic)
                    foreach (var filter in _columnFilters)
                    {
                        string columnName = filter.Key;
                        var allowedValues = filter.Value;
                        
                        // If filter has no allowed values, hide this row
                        if (allowedValues.Count == 0)
                            return false;
                        
                        // Get cell value
                        string cellValue = "";
                        if (columnName == "Element ID")
                        {
                            cellValue = row.GetElement().Id.IntegerValue.ToString();
                        }
                        else if (row.Values.ContainsKey(columnName))
                        {
                            cellValue = row.Values[columnName];
                        }
                        
                        // Normalize and check if value is allowed
                        string normalizedCellValue = (cellValue ?? "").Trim();
                        bool isAllowed = allowedValues.Any(allowed => 
                            string.Equals((allowed ?? "").Trim(), normalizedCellValue, StringComparison.OrdinalIgnoreCase));
                        
                        // If this column filter fails, reject the row
                        if (!isAllowed)
                            return false;
                    }
                    
                    // All filters passed - include this row
                    return true;
                }).ToList();

                var filteredCount = filteredData.Count;
                DebugLog($"ApplyFiltersEnhanced - Filtered to {filteredCount} rows from {totalRows}");

                // Update DataGrid
                dataGrid.ItemsSource = filteredData;
                
                // Update filter status display
                UpdateFilterStatusDisplay();
                
                DebugLog("ApplyFiltersEnhanced - Completed successfully");
            }
            catch (Exception ex)
            {
                DebugLog($"ApplyFiltersEnhanced - Error: {ex.Message}");
                
                // On error, show all data
                var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
                if (dataGrid != null)
                {
                    dataGrid.ItemsSource = _viewModel.ScheduleData;
                }
            }
        }
        
        #endregion
    }
}
