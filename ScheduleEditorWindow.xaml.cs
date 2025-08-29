using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
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

        public ScheduleEditorWindow(Document doc)
        {
            InitializeComponent();
            _viewModel = new ScheduleEditorViewModel(doc);
            this.DataContext = _viewModel;
            
            // Setup Excel-like behaviors
            SetupExcelLikeBehaviors();
            
            _viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(_viewModel.SelectedSchedule))
                {
                    // Don't auto-generate columns anymore - wait for Preview/Edit button
                }
                else if (args.PropertyName == nameof(_viewModel.ScheduleData))
                {
                    // Data updated - regenerate columns only when data is loaded
                    GenerateDataGridColumns();
                }
            };
            
            // Gọi ngay lập tức nếu đã có SelectedSchedule
            this.Loaded += (sender, args) =>
            {
                // Don't auto-load data anymore - user needs to click Preview/Edit
                
                // Setup Excel-like enhanced autofill
                SetupEnhancedAutofill();
            };
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
                    // Update will be handled by binding automatically
                }
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
                    Binding = new System.Windows.Data.Binding($"[{fieldName}]") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
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
            var button = sender as Button;
            if (button == null) return;

            // Find the column header and get column name
            var header = FindParent<DataGridColumnHeader>(button);
            if (header?.Content == null) return;

            string columnName = header.Content.ToString();
            
            // Create and show filter popup
            ShowFilterPopup(button, columnName);
        }

        private void ShowFilterPopup(Button button, string columnName)
        {
            if (_viewModel.ScheduleData.Count == 0) return;

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
                // Regular column values
                uniqueValues = _viewModel.ScheduleData
                    .Select(row => row.Values.ContainsKey(columnName) ? row.Values[columnName] : "")
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Distinct()
                    .OrderBy(v => v)
                    .ToList();
            }

            if (uniqueValues.Count == 0) return;

            // Create popup with checkboxes
            var popup = new System.Windows.Controls.Primitives.Popup();
            var listBox = new ListBox
            {
                Width = 200,
                MaxHeight = 300,
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };

            // Add "Select All" option
            var selectAllItem = new CheckBox
            {
                Content = "(Select All)",
                IsChecked = true,
                Margin = new Thickness(2)
            };
            
            selectAllItem.Checked += (s, e) => 
            {
                foreach (CheckBox cb in listBox.Items.OfType<CheckBox>().Skip(1))
                    cb.IsChecked = true;
            };
            
            selectAllItem.Unchecked += (s, e) => 
            {
                foreach (CheckBox cb in listBox.Items.OfType<CheckBox>().Skip(1))
                    cb.IsChecked = false;
            };

            listBox.Items.Add(selectAllItem);

            // Add separator
            listBox.Items.Add(new Separator());

            // Add value checkboxes
            foreach (var value in uniqueValues)
            {
                var checkBox = new CheckBox
                {
                    Content = value,
                    IsChecked = true,
                    Margin = new Thickness(2)
                };
                listBox.Items.Add(checkBox);
            }

            // Add OK/Cancel buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(5)
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 50,
                Margin = new Thickness(2),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 50,
                Margin = new Thickness(2),
                IsCancel = true
            };

            okButton.Click += (s, e) =>
            {
                ApplyColumnFilter(columnName, listBox);
                popup.IsOpen = false;
            };

            cancelButton.Click += (s, e) =>
            {
                popup.IsOpen = false;
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            var stackPanel = new StackPanel();
            stackPanel.Children.Add(listBox);
            stackPanel.Children.Add(buttonPanel);

            popup.Child = stackPanel;
            popup.PlacementTarget = button;
            popup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            popup.StaysOpen = false;
            popup.IsOpen = true;
        }

        private void ApplyColumnFilter(string columnName, ListBox listBox)
        {
            try
            {
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

                // Apply all filters
                var collectionView = CollectionViewSource.GetDefaultView(_viewModel.ScheduleData);
                collectionView.Filter = FilterPredicate;
                collectionView.Refresh();
                
                System.Diagnostics.Debug.WriteLine($"After filter: {collectionView.Cast<object>().Count()} items visible");
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

            // Double-click autofill 
            dataGrid.MouseDoubleClick += (sender, e) =>
            {
                if (dataGrid.SelectedCells.Count > 1)
                {
                    _viewModel.AutofillCommand.Execute(dataGrid.SelectedCells);
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
    }
}
