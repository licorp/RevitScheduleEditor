using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace RevitScheduleEditor
{
    /// <summary>
    /// FilterItem class to represent each item in the filter list
    /// </summary>
    public class FilterItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _value;

        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Text Filters Window - similar to Excel's text filter dialog
    /// </summary>
    public partial class TextFiltersWindow : Window, INotifyPropertyChanged
    {
        private ObservableCollection<FilterItem> _allItems;
        private ObservableCollection<FilterItem> _filteredItems;
        private string _searchText = "";

        public ObservableCollection<FilterItem> FilteredItems
        {
            get => _filteredItems;
            set
            {
                _filteredItems = value;
                OnPropertyChanged(nameof(FilteredItems));
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                FilterItems();
            }
        }

        public List<string> SelectedValues { get; private set; }
        public string ColumnName { get; private set; }

        public TextFiltersWindow(string columnName, List<string> allValues, List<string> selectedValues = null)
        {
            InitializeComponent();
            DataContext = this;
            
            ColumnName = columnName;
            Title = $"Text Filters - {columnName}";
            
            // Debug logging
            System.Diagnostics.Debug.WriteLine($"TextFiltersWindow - Column: {columnName}");
            System.Diagnostics.Debug.WriteLine($"TextFiltersWindow - Values count: {allValues?.Count ?? 0}");
            
            // Initialize items
            _allItems = new ObservableCollection<FilterItem>();
            
            // Add unique values, sorted
            var uniqueValues = allValues?.Where(v => !string.IsNullOrEmpty(v))
                                      .Distinct()
                                      .OrderBy(v => v)
                                      .ToList() ?? new List<string>();
            
            System.Diagnostics.Debug.WriteLine($"TextFiltersWindow - Unique values count: {uniqueValues.Count}");
            
            foreach (var value in uniqueValues)
            {
                var item = new FilterItem 
                { 
                    Value = value, 
                    IsSelected = selectedValues?.Contains(value) ?? true 
                };
                
                System.Diagnostics.Debug.WriteLine($"TextFiltersWindow - Adding item: {value}");
                
                // Subscribe to property changes to update Select All checkbox
                item.PropertyChanged += FilterItem_PropertyChanged;
                _allItems.Add(item);
            }
            
            FilteredItems = new ObservableCollection<FilterItem>(_allItems);
            UpdateSelectAllCheckbox();
        }

        private void FilterItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilterItem.IsSelected))
            {
                UpdateSelectAllCheckbox();
            }
        }

        private void FilterItems()
        {
            if (string.IsNullOrEmpty(SearchText))
            {
                FilteredItems = new ObservableCollection<FilterItem>(_allItems);
            }
            else
            {
                var filtered = _allItems.Where(item => 
                    item.Value.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);
                FilteredItems = new ObservableCollection<FilterItem>(filtered);
            }
            
            UpdateSelectAllCheckbox();
        }

        private void UpdateSelectAllCheckbox()
        {
            if (FilteredItems == null || !FilteredItems.Any())
            {
                SelectAllCheckBox.IsChecked = false;
                return;
            }

            var selectedCount = FilteredItems.Count(item => item.IsSelected);
            
            if (selectedCount == 0)
            {
                SelectAllCheckBox.IsChecked = false;
            }
            else if (selectedCount == FilteredItems.Count)
            {
                SelectAllCheckBox.IsChecked = true;
            }
            else
            {
                SelectAllCheckBox.IsChecked = null; // Indeterminate state
            }
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            bool isChecked = checkBox.IsChecked == true;
            
            // Temporarily unsubscribe from events to avoid triggering updates
            foreach (var item in FilteredItems)
            {
                item.PropertyChanged -= FilterItem_PropertyChanged;
                item.IsSelected = isChecked;
                item.PropertyChanged += FilterItem_PropertyChanged;
            }
            
            // Update the underlying collection as well
            foreach (var item in _allItems.Where(item => FilteredItems.Contains(item)))
            {
                item.IsSelected = isChecked;
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // SearchText binding will handle the filtering
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Get selected values from all items (not just filtered)
            SelectedValues = _allItems.Where(item => item.IsSelected)
                                    .Select(item => item.Value)
                                    .ToList();
            
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
