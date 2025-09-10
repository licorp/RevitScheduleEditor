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
    /// Interaction logic for TextFiltersWindow.xaml - Excel-like text filter dialog
    /// </summary>
    public partial class TextFiltersWindow : Window, INotifyPropertyChanged
    {
        private ObservableCollection<FilterItem> _allItems;
        private ObservableCollection<FilterItem> _filteredItems;
        private string _searchText = "";

        /// <summary>
        /// Gets the list of selected values
        /// </summary>
        public List<string> SelectedValues { get; private set; }

        public TextFiltersWindow()
        {
            InitializeComponent();
            DataContext = this;
            SelectedValues = new List<string>();
        }

        /// <summary>
        /// Initialize the filter window with unique values and current filter
        /// </summary>
        public void SetFilterData(List<string> uniqueValues, HashSet<string> currentFilter = null)
        {
            // Create FilterItem objects for all unique values
            _allItems = new ObservableCollection<FilterItem>();
            
            // Logic for default selection:
            // - If currentFilter exists, use it (restore previous filter state)
            // - If no currentFilter, start with all UNCHECKED for clearer filtering workflow
            // User can use "Select All" button if they want to check everything
            bool defaultSelection = false; // Start with nothing selected for clearer workflow
            
            // Process values and sort with "(Blanks)" first like Excel
            var processedValues = uniqueValues.Select(value => new 
            {
                Original = value,
                Display = string.IsNullOrWhiteSpace(value) ? "(Blanks)" : value,
                IsBlank = string.IsNullOrWhiteSpace(value)
            })
            .OrderBy(x => x.IsBlank ? 0 : 1)  // Blanks first
            .ThenBy(x => x.Display);          // Then alphabetical
            
            foreach (var valueInfo in processedValues)
            {
                var item = new FilterItem
                {
                    Value = valueInfo.Display,
                    ActualValue = valueInfo.Original, // Store original value for filtering
                    IsSelected = currentFilter?.Contains(valueInfo.Original) ?? defaultSelection
                };
                
                item.PropertyChanged += FilterItem_PropertyChanged;
                _allItems.Add(item);
            }

            // Initialize filtered items (initially all items)
            FilteredItems = new ObservableCollection<FilterItem>(_allItems);
            
            UpdateSelectAllCheckbox();
            OnPropertyChanged(nameof(StatusText));
        }

        private void FilterItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilterItem.IsSelected))
            {
                UpdateSelectAllCheckbox();
                OnPropertyChanged(nameof(StatusText));
            }
        }

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

        public string StatusText
        {
            get
            {
                if (FilteredItems == null) return "";
                
                var selectedCount = FilteredItems.Count(item => item.IsSelected);
                var totalCount = FilteredItems.Count;
                var allItemsCount = _allItems?.Count ?? 0;
                
                if (selectedCount == allItemsCount)
                {
                    return $"All {allItemsCount} items selected (no filter will be applied)";
                }
                else if (selectedCount == 0)
                {
                    return $"No items selected (all will be hidden)";
                }
                else
                {
                    return $"{selectedCount} of {allItemsCount} items selected ({allItemsCount - selectedCount} will be hidden)";
                }
            }
        }

        private void FilterItems()
        {
            if (_allItems == null) return;

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredItems = new ObservableCollection<FilterItem>(_allItems);
            }
            else
            {
                var filtered = _allItems.Where(item => 
                {
                    // Search in both display value (e.g., "(Blanks)") and actual value
                    var displayMatch = item.Value?.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
                    var actualMatch = item.ActualValue?.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
                    return displayMatch || actualMatch;
                });
                FilteredItems = new ObservableCollection<FilterItem>(filtered);
            }
            
            UpdateSelectAllCheckbox();
            OnPropertyChanged(nameof(StatusText));
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

        private void InvertSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            // Temporarily unsubscribe from events to avoid triggering updates
            foreach (var item in FilteredItems)
            {
                item.PropertyChanged -= FilterItem_PropertyChanged;
                item.IsSelected = !item.IsSelected;
                item.PropertyChanged += FilterItem_PropertyChanged;
            }
            
            // Update the underlying collection as well
            foreach (var item in _allItems.Where(item => FilteredItems.Contains(item)))
            {
                item.IsSelected = !item.IsSelected;
            }
            
            UpdateSelectAllState();
        }

        private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            // Temporarily unsubscribe from events to avoid triggering updates
            foreach (var item in FilteredItems)
            {
                item.PropertyChanged -= FilterItem_PropertyChanged;
                item.IsSelected = false;
                item.PropertyChanged += FilterItem_PropertyChanged;
            }
            
            // Update the underlying collection as well
            foreach (var item in _allItems.Where(item => FilteredItems.Contains(item)))
            {
                item.IsSelected = false;
            }
            
            UpdateSelectAllState();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // SearchText binding will handle the filtering
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedCount = _allItems.Count(item => item.IsSelected);
            var totalCount = _allItems?.Count ?? 0;
            
            // Note: Removed intrusive warning dialog that was interrupting user workflow
            // Users can apply "no filter" (all selected) if they want to see all data
            
            // Get selected values from all items (not just filtered) - use ActualValue for filtering
            SelectedValues = _allItems.Where(item => item.IsSelected)
                                    .Select(item => item.ActualValue ?? item.Value) // Fallback to Value if ActualValue is null
                                    .ToList();
            
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateSelectAllState()
        {
            if (SelectAllCheckBox == null || _allItems == null) return;
            
            var selectedCount = _allItems.Count(item => item.IsSelected);
            var totalCount = _allItems.Count;
            
            if (selectedCount == 0)
            {
                SelectAllCheckBox.IsChecked = false;
            }
            else if (selectedCount == totalCount)
            {
                SelectAllCheckBox.IsChecked = true;
            }
            else
            {
                SelectAllCheckBox.IsChecked = null; // Indeterminate state
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// FilterItem class to represent each item in the filter list
    /// </summary>
    public class FilterItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _value;
        private string _actualValue;

        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        /// <summary>
        /// The actual value used for filtering (original value before display transformation)
        /// </summary>
        public string ActualValue
        {
            get => _actualValue;
            set
            {
                _actualValue = value;
                OnPropertyChanged(nameof(ActualValue));
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
}
