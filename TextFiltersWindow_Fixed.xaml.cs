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
            
            foreach (var value in uniqueValues.OrderBy(v => v))
            {
                var item = new FilterItem
                {
                    Value = value,
                    IsSelected = currentFilter?.Contains(value) ?? true // Default to selected if no current filter
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
                    item.Value.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);
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

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // SearchText binding will handle the filtering
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedCount = _allItems.Count(item => item.IsSelected);
            var totalCount = _allItems?.Count ?? 0;
            
            // Warn user if all items are selected (no filter effect)
            if (selectedCount == totalCount)
            {
                var result = MessageBox.Show(
                    "All items are selected, so no filtering will be applied.\n\n" +
                    "To filter data:\n" +
                    "• UNCHECK items you want to HIDE\n" +
                    "• Only CHECKED items will remain VISIBLE\n\n" +
                    "Continue anyway?",
                    "No Filter Applied",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                    
                if (result == MessageBoxResult.No)
                {
                    return; // Stay in dialog
                }
            }
            
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
}
