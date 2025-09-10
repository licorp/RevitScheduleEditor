using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace RevitScheduleEditor
{
    /// <summary>
    /// Simple progressive loading collection that loads data in chunks
    /// </summary>
    public class ProgressiveScheduleCollection : ObservableCollection<ScheduleRow>
    {
        private readonly Document _doc;
        private readonly ViewSchedule _schedule;
        private readonly List<ScheduleField> _visibleFields;
        private readonly IList<Element> _allElements;
        private readonly int _chunkSize;
        private int _loadedIndex = 0;
        private bool _isLoading = false;
        private readonly DispatcherTimer _loadTimer;

        public int TotalElements => _allElements.Count;
        public int LoadedElements => _loadedIndex;
        public bool IsComplete => _loadedIndex >= _allElements.Count;

        public ProgressiveScheduleCollection(Document doc, ViewSchedule schedule, List<ScheduleField> visibleFields, int chunkSize = 10)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _visibleFields = visibleFields ?? throw new ArgumentNullException(nameof(visibleFields));
            _chunkSize = chunkSize;

            // Initialize elements
            var collector = new FilteredElementCollector(_doc, _schedule.Id);
            _allElements = collector.ToElements();

            // Setup timer for progressive loading with dynamic interval based on size
            var timerInterval = _allElements.Count > 1000 ? 100 : (_allElements.Count > 500 ? 75 : 50);
            _loadTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(timerInterval) // Dynamic interval for better performance
            };
            _loadTimer.Tick += LoadNextChunk;

            System.Diagnostics.Debug.WriteLine($"ProgressiveScheduleCollection initialized - Total: {_allElements.Count}, ChunkSize: {_chunkSize}, Interval: {timerInterval}ms");
            
            // Start loading immediately
            StartProgressiveLoading();
        }

        public void StartProgressiveLoading()
        {
            if (_isLoading || IsComplete) return;

            _isLoading = true;
            _loadTimer.Start();
            System.Diagnostics.Debug.WriteLine("Progressive loading started");
        }

        public void StopProgressiveLoading()
        {
            _loadTimer?.Stop();
            _isLoading = false;
            System.Diagnostics.Debug.WriteLine("Progressive loading stopped");
        }

        private void LoadNextChunk(object sender, EventArgs e)
        {
            try
            {
                if (IsComplete)
                {
                    StopProgressiveLoading();
                    System.Diagnostics.Debug.WriteLine($"Progressive loading completed - {Count} items loaded");
                    return;
                }

                var endIndex = Math.Min(_loadedIndex + _chunkSize, _allElements.Count);
                var chunkItems = new List<ScheduleRow>();

                // Pre-process chunk in background to avoid UI blocking
                for (int i = _loadedIndex; i < endIndex; i++)
                {
                    try
                    {
                        var element = _allElements[i];
                        if (element?.IsValidObject == true)
                        {
                            var scheduleRow = CreateScheduleRowFromElement(element);
                            if (scheduleRow != null)
                            {
                                chunkItems.Add(scheduleRow);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading element {i}: {ex.Message}");
                    }
                }

                // Add all items at once to reduce collection change notifications
                foreach (var item in chunkItems)
                {
                    Add(item);
                }

                _loadedIndex = endIndex;
                
                if (chunkItems.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Loaded chunk: {chunkItems.Count} items, Total: {Count}/{TotalElements}");
                    
                    // Fire events to notify UI (batched)
                    OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                    OnPropertyChanged(new PropertyChangedEventArgs("LoadedElements"));
                    OnPropertyChanged(new PropertyChangedEventArgs("IsComplete"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadNextChunk: {ex.Message}");
                StopProgressiveLoading();
            }
        }

        private ScheduleRow CreateScheduleRowFromElement(Element element)
        {
            try
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating ScheduleRow: {ex.Message}");
                return null;
            }
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

                // Fallback to searching by name or built-in parameter
                var parameters = element.Parameters.Cast<Parameter>()
                    .Where(p => p.Definition.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (parameters.Any())
                {
                    return GetParameterDisplayText(parameters.First());
                }

                return "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting parameter value for {field.GetName()}: {ex.Message}");
                return "";
            }
        }

        private string GetParameterDisplayText(Parameter parameter)
        {
            try
            {
                // Use AsValueString for Revit 2020 compatibility
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
        /// Force load all remaining elements (for Export/Import operations)
        /// </summary>
        public async Task LoadAllRemainingAsync()
        {
            if (IsComplete) return;

            StopProgressiveLoading();
            
            await Task.Run(() =>
            {
                while (!IsComplete)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        LoadNextChunk(null, null);
                    });
                    
                    // Small delay to prevent UI freezing
                    System.Threading.Thread.Sleep(10);
                }
            });

            System.Diagnostics.Debug.WriteLine($"Force loaded all elements - Total: {Count}");
        }

        protected override void ClearItems()
        {
            StopProgressiveLoading();
            _loadedIndex = 0;
            base.ClearItems();
        }
    }
}
