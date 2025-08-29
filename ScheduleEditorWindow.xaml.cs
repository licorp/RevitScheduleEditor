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
                    GenerateDataGridColumns();
                    GenerateFilterButtons();
                }
                else if (args.PropertyName == nameof(_viewModel.ScheduleData))
                {
                    GenerateFilterButtons();
                }
            };
            
            // Gọi ngay lập tức nếu đã có SelectedSchedule
            this.Loaded += (sender, args) =>
            {
                if (_viewModel.SelectedSchedule != null)
                {
                    GenerateDataGridColumns();
                    GenerateFilterButtons();
                }
                
                // Force generate filters with delay để đảm bảo UI đã loaded
                this.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Loaded,
                    new System.Action(() =>
                    {
                        if (_viewModel.SelectedSchedule != null && _viewModel.ScheduleData.Count > 0)
                        {
                            GenerateFilterButtons();
                        }
                    })
                );
                
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

            // Get the Excel-like cell style
            var excelCellStyle = this.FindResource("ExcelLikeCellStyle") as Style;

            foreach (var field in visibleFields)
            {
                string fieldName = field.GetName();
                var column = new DataGridTextColumn
                {
                    Header = fieldName,
                    Binding = new System.Windows.Data.Binding($"[{fieldName}]") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                    CellStyle = excelCellStyle  // Apply Excel-like style with fill handle
                };
                dataGrid.Columns.Add(column);
            }
            
            // Setup fill handle interactions
            SetupFillHandleInteractions(dataGrid);
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

        private void GenerateFilterButtons()
        {
            var filterPanel = this.FindName("FilterPanel") as StackPanel;
            if (filterPanel == null) return;
            
            filterPanel.Children.Clear();
            _columnFilters.Clear();
            _columnValues.Clear();

            if (_viewModel.SelectedSchedule == null || _viewModel.ScheduleData.Count == 0) return;

            var visibleFields = _viewModel.SelectedSchedule.Definition.GetFieldOrder()
                .Select(id => _viewModel.SelectedSchedule.Definition.GetField(id))
                .Where(f => !f.IsHidden).ToList();

            foreach (var field in visibleFields)
            {
                string fieldName = field.GetName();
                
                // Collect all unique values for this column (including empty values)
                var uniqueValues = _viewModel.ScheduleData
                    .Select(row => row[fieldName] ?? string.Empty)
                    .Distinct()
                    .OrderBy(v => v)
                    .ToList();

                _columnValues[fieldName] = uniqueValues;
                _columnFilters[fieldName] = new HashSet<string>(uniqueValues); // All selected by default

                var filterButton = new Button
                {
                    Width = 120,
                    Height = 25,
                    Margin = new Thickness(2),
                    Content = $"▼ {fieldName}",
                    Tag = fieldName,
                    Background = System.Windows.Media.Brushes.LightGray,
                    BorderBrush = System.Windows.Media.Brushes.Gray,
                    BorderThickness = new Thickness(1)
                };

                filterButton.Click += FilterButton_Click;
                filterPanel.Children.Add(filterButton);
            }
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            string fieldName = button.Tag.ToString();
            var uniqueValues = _columnValues[fieldName];
            var selectedValues = _columnFilters[fieldName];

            // Create popup window với checkbox list
            var filterWindow = new Window
            {
                Title = $"Filter {fieldName}",
                Width = 300,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });

            // Search box
            var searchPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            searchPanel.Children.Add(new TextBlock { Text = "Search:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });
            var searchBox = new TextBox { Width = 200 };
            searchPanel.Children.Add(searchBox);
            Grid.SetRow(searchPanel, 0);
            mainGrid.Children.Add(searchPanel);

            // Checkbox list
            var scrollViewer = new ScrollViewer { Margin = new Thickness(5) };
            var checkBoxPanel = new StackPanel();

            // Select All / Clear All buttons
            var selectAllPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            var selectAllCheckBox = new CheckBox { Content = "(Select All)", IsChecked = selectedValues.Count == uniqueValues.Count };
            selectAllPanel.Children.Add(selectAllCheckBox);
            checkBoxPanel.Children.Add(selectAllPanel);

            // Individual checkboxes
            var checkBoxes = new List<CheckBox>();
            foreach (var value in uniqueValues)
            {
                var checkBox = new CheckBox
                {
                    Content = value,
                    IsChecked = selectedValues.Contains(value),
                    Margin = new Thickness(0, 2, 0, 2)
                };
                checkBoxes.Add(checkBox);
                checkBoxPanel.Children.Add(checkBox);
            }

            selectAllCheckBox.Checked += (s, args) =>
            {
                foreach (var cb in checkBoxes) cb.IsChecked = true;
            };
            selectAllCheckBox.Unchecked += (s, args) =>
            {
                foreach (var cb in checkBoxes) cb.IsChecked = false;
            };

            // Search functionality
            searchBox.TextChanged += (s, args) =>
            {
                string searchText = searchBox.Text.ToLower();
                foreach (var cb in checkBoxes)
                {
                    cb.Visibility = string.IsNullOrEmpty(searchText) || 
                                   cb.Content.ToString().ToLower().Contains(searchText) 
                                   ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                }
            };

            scrollViewer.Content = checkBoxPanel;
            Grid.SetRow(scrollViewer, 1);
            mainGrid.Children.Add(scrollViewer);

            // OK/Cancel buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(5) };
            var okButton = new Button { Content = "OK", Width = 60, Height = 25, Margin = new Thickness(0, 0, 5, 0) };
            var cancelButton = new Button { Content = "Cancel", Width = 60, Height = 25 };

            okButton.Click += (s, args) =>
            {
                selectedValues.Clear();
                foreach (var cb in checkBoxes)
                {
                    if (cb.IsChecked == true && cb.Visibility == System.Windows.Visibility.Visible)
                    {
                        selectedValues.Add(cb.Content.ToString());
                    }
                }
                
                // Update button appearance
                button.Content = selectedValues.Count == uniqueValues.Count ? 
                                $"▼ {fieldName}" : 
                                $"▼ {fieldName} ({selectedValues.Count})";
                
                button.Background = selectedValues.Count == uniqueValues.Count ? 
                                   System.Windows.Media.Brushes.LightGray : 
                                   System.Windows.Media.Brushes.LightBlue;

                ApplyFilters();
                filterWindow.Close();
            };

            cancelButton.Click += (s, args) => filterWindow.Close();

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);
            mainGrid.Children.Add(buttonPanel);

            filterWindow.Content = mainGrid;
            filterWindow.ShowDialog();
        }

        private void ApplyFilters()
        {
            var view = CollectionViewSource.GetDefaultView(_viewModel.ScheduleData);
            view.Filter = FilterPredicate;
            view.Refresh();
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
                    string cellValue = row[filter.Key] ?? string.Empty;
                    
                    // If filter has selected values and cell value is not in the selected set, exclude
                    if (filter.Value.Count > 0 && !filter.Value.Contains(cellValue))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
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
