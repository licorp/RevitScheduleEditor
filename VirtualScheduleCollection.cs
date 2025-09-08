using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.DB;

namespace RevitScheduleEditor
{
    /// <summary>
    /// Simple virtualization collection that loads data on-demand for better performance with large datasets
    /// </summary>
    public class VirtualScheduleCollection : ObservableCollection<ScheduleRow>, INotifyPropertyChanged
    {
        private readonly Document _doc;
        private readonly ViewSchedule _schedule;
        private readonly List<ScheduleField> _visibleFields;
        private readonly IList<Element> _allElements;
        private readonly int _pageSize;
        private readonly Dictionary<int, bool> _loadedPages;
        private readonly object _lockObject = new object();

        // Performance tracking
        private int _totalElements;
        private int _loadedElements;
        private bool _isLoading;

        public VirtualScheduleCollection(Document doc, ViewSchedule schedule, List<ScheduleField> visibleFields, int pageSize = 50)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _visibleFields = visibleFields ?? throw new ArgumentNullException(nameof(visibleFields));
            _pageSize = pageSize;
            _loadedPages = new Dictionary<int, bool>();

            // Initialize elements
            var collector = new FilteredElementCollector(_doc, _schedule.Id);
            _allElements = collector.ToElements();
            _totalElements = _allElements.Count;

            // Pre-populate collection with placeholder items
            InitializePlaceholders();
        }

        public int TotalElements => _totalElements;
        public int LoadedElements => _loadedElements;
        public bool IsLoading => _isLoading;

        private void InitializePlaceholders()
        {
            // Check if we have elements to work with
            if (_totalElements == 0 || _allElements.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("No elements to create placeholders for");
                return;
            }

            // Get a sample element for placeholder creation
            var sampleElement = _allElements.FirstOrDefault();
            if (sampleElement == null)
            {
                System.Diagnostics.Debug.WriteLine("No sample element available for placeholders");
                return;
            }

            // Add placeholder items to enable virtualization
            for (int i = 0; i < _totalElements; i++)
            {
                try
                {
                    // Create placeholder with sample element
                    var placeholder = new ScheduleRow(sampleElement);

                    // Add loading indicator
                    foreach (var field in _visibleFields)
                    {
                        placeholder.AddValue(field.GetName(), "⏳ Loading...");
                    }

                    Add(placeholder);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating placeholder {i}: {ex.Message}");
                    break; // Stop creating placeholders if there's an error
                }
            }

            System.Diagnostics.Debug.WriteLine($"VirtualScheduleCollection initialized with {Count} placeholder items");
            
            // Load first page immediately for instant display
            _ = Task.Run(async () => 
            {
                await LoadPageAsync(0);
                
                // Force a complete collection refresh after first page
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                    OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    System.Diagnostics.Debug.WriteLine("VirtualScheduleCollection: Initial page loaded and UI refreshed");
                });
            });
        }

        /// <summary>
        /// Load a specific page of data (called when items become visible)
        /// </summary>
        public async Task LoadPageAsync(int pageIndex)
        {
            if (_isLoading || _loadedPages.ContainsKey(pageIndex))
                return;

            lock (_lockObject)
            {
                if (_loadedPages.ContainsKey(pageIndex))
                    return;

                _loadedPages[pageIndex] = true;
            }

            _isLoading = true;

            try
            {
                await Task.Run(() => LoadPageSync(pageIndex));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading page {pageIndex}: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void LoadPageSync(int pageIndex)
        {
            var startIndex = pageIndex * _pageSize;
            var endIndex = Math.Min(startIndex + _pageSize, _totalElements);

            for (int i = startIndex; i < endIndex; i++)
            {
                if (i < _allElements.Count && i < Count)
                {
                    var element = _allElements[i];
                    var scheduleRow = CreateScheduleRowFromElement(element, i);

                    // Update the item in the collection on UI thread
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (i < Count)
                        {
                            // Force complete replacement - clear and set again
                            var oldItem = base[i];
                            
                            // Remove the old item first
                            RemoveAt(i);
                            
                            // Insert the new item at the same position
                            Insert(i, scheduleRow);
                            
                            System.Diagnostics.Debug.WriteLine($"Replaced item {i}: {scheduleRow.Id}");
                        }
                    });

                    _loadedElements++;
                }
            }

            System.Diagnostics.Debug.WriteLine($"Loaded page {pageIndex} ({startIndex}-{endIndex}) - Total loaded: {_loadedElements}/{_totalElements}");
            
            // Force UI refresh after loading page
            RefreshCollection();
        }

        private ScheduleRow CreateScheduleRowFromElement(Element element, int index)
        {
            var scheduleRow = new ScheduleRow(element);

            // Populate row data efficiently
            foreach (var field in _visibleFields)
            {
                try
                {
                    var value = GetParameterValueFast(element, field);
                    scheduleRow.AddValue(field.GetName(), value ?? "");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting parameter {field.GetName()}: {ex.Message}");
                    scheduleRow.AddValue(field.GetName(), "");
                }
            }

            return scheduleRow;
        }

        private string GetParameterValueFast(Element element, ScheduleField field)
        {
            try
            {
                // Use the same approach as in ScheduleEditorViewModel
                var fieldName = field.GetName();
                
                // Try to get the parameter using the field's parameter definition
                var param = element.LookupParameter(fieldName);
                if (param != null)
                {
                    return GetParameterDisplayText(param);
                }

                // Fallback: try built-in parameters
                var parameterId = field.ParameterId;
                if (parameterId.IntegerValue < 0)
                {
                    var builtInParam = (BuiltInParameter)parameterId.IntegerValue;
                    param = element.get_Parameter(builtInParam);
                    return GetParameterDisplayText(param);
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        private string GetParameterDisplayText(Parameter parameter)
        {
            if (parameter == null || !parameter.HasValue)
                return "";

            try
            {
                // Use AsValueString() for formatted display text when available
                var valueString = parameter.AsValueString();
                if (!string.IsNullOrEmpty(valueString))
                    return valueString;

                // Fallback to raw value conversion
                switch (parameter.StorageType)
                {
                    case StorageType.String:
                        return parameter.AsString() ?? "";
                    case StorageType.Integer:
                        return parameter.AsInteger().ToString();
                    case StorageType.Double:
                        return parameter.AsDouble().ToString("F2");
                    case StorageType.ElementId:
                        var elementId = parameter.AsElementId();
                        if (elementId != ElementId.InvalidElementId)
                        {
                            var referencedElement = _doc.GetElement(elementId);
                            return referencedElement?.Name ?? elementId.IntegerValue.ToString();
                        }
                        return "";
                    default:
                        return "";
                }
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Load initial pages for visible items
        /// </summary>
        public async Task LoadInitialPagesAsync(int visibleItemCount = 100)
        {
            var pagesToLoad = Math.Ceiling((double)visibleItemCount / _pageSize);
            var tasks = new List<Task>();

            for (int i = 0; i < pagesToLoad; i++)
            {
                tasks.Add(LoadPageAsync(i));
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Get the page index for a specific item index
        /// </summary>
        public int GetPageIndex(int itemIndex)
        {
            return itemIndex / _pageSize;
        }

        /// <summary>
        /// Override indexer to provide controlled loading
        /// </summary>
        public new ScheduleRow this[int index]
        {
            get
            {
                // Return existing item without triggering loads during iteration
                if (index >= 0 && index < Count)
                {
                    return base[index];
                }
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            set
            {
                if (index >= 0 && index < Count)
                {
                    base[index] = value;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
        }

        /// <summary>
        /// Check if an item is loaded (not a placeholder)
        /// </summary>
        public bool IsItemLoaded(int index)
        {
            if (index < 0 || index >= Count)
                return false;

            var item = this[index];
            return item.Id != ElementId.InvalidElementId;
        }

        /// <summary>
        /// Force refresh the collection to trigger UI updates
        /// </summary>
        public void RefreshCollection()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                System.Diagnostics.Debug.WriteLine("VirtualScheduleCollection: Force refresh triggered");
            });
        }

        /// <summary>
        /// Debug method to check data loading status
        /// </summary>
        public void DebugDataStatus()
        {
            System.Diagnostics.Debug.WriteLine($"=== VirtualScheduleCollection Debug Status ===");
            System.Diagnostics.Debug.WriteLine($"Total Count: {Count}");
            System.Diagnostics.Debug.WriteLine($"Total Elements: {_totalElements}");
            System.Diagnostics.Debug.WriteLine($"Loaded Elements: {_loadedElements}");
            System.Diagnostics.Debug.WriteLine($"Loaded Pages: {string.Join(", ", _loadedPages.Keys)}");
            
            // Check first few items
            for (int i = 0; i < Math.Min(5, Count); i++)
            {
                var item = this[i];
                var hasRealData = !item.Values.Values.Any(v => v.Contains("Loading"));
                System.Diagnostics.Debug.WriteLine($"Item {i}: HasRealData={hasRealData}, Id={item.Id}");
            }
            System.Diagnostics.Debug.WriteLine($"=======================================");
        }
    }
}
