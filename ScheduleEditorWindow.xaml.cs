using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Grid = System.Windows.Controls.Grid;

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

            foreach (var field in visibleFields)
            {
                string fieldName = field.GetName();
                var column = new DataGridTextColumn
                {
                    Header = fieldName,
                    Binding = new System.Windows.Data.Binding($"[{fieldName}]") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }
                };
                dataGrid.Columns.Add(column);
            }
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
    }
}
