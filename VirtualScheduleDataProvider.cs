using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VitalElement.DataVirtualization;
using Autodesk.Revit.DB;

namespace RevitScheduleEditor
{
    /// <summary>
    /// Data provider for VitalElement.DataVirtualization to enable virtual scrolling of schedule data
    /// </summary>
    public class VirtualScheduleDataProvider : IItemsProvider<ScheduleRow>
    {
        private readonly Document _doc;
        private readonly ViewSchedule _schedule;
        private readonly List<ScheduleField> _visibleFields;
        private IList<Element> _allElements;
        private readonly object _lockObject = new object();

        public VirtualScheduleDataProvider(Document doc, ViewSchedule schedule, List<ScheduleField> visibleFields)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _schedule = schedule; // Allow null for initial construction
            _visibleFields = visibleFields ?? new List<ScheduleField>();
            
            // Initialize elements collection only if schedule is provided
            if (_schedule != null)
            {
                InitializeElements();
            }
            else
            {
                _allElements = new List<Element>();
                Count = 0;
            }
        }

        public void UpdateSchedule(ViewSchedule schedule, List<ScheduleField> visibleFields)
        {
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _visibleFields = visibleFields ?? throw new ArgumentNullException(nameof(visibleFields));
            InitializeElements();
        }

        private void InitializeElements()
        {
            try
            {
                if (_schedule == null)
                {
                    _allElements = new List<Element>();
                    Count = 0;
                    return;
                }

                var collector = new FilteredElementCollector(_doc, _schedule.Id);
                _allElements = collector.ToElements();
                
                // Cache the count for performance
                Count = _allElements.Count;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing elements: {ex.Message}");
                _allElements = new List<Element>();
                Count = 0;
            }
        }

        public int Count { get; private set; }

        public IList<ScheduleRow> FetchRange(int startIndex, int count)
        {
            lock (_lockObject)
            {
                try
                {
                    var result = new List<ScheduleRow>();
                    var endIndex = Math.Min(startIndex + count, _allElements.Count);
                    
                    for (int i = startIndex; i < endIndex; i++)
                    {
                        if (i < _allElements.Count)
                        {
                            var element = _allElements[i];
                            var scheduleRow = CreateScheduleRowFromElement(element, i);
                            result.Add(scheduleRow);
                        }
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error fetching range {startIndex}-{startIndex + count}: {ex.Message}");
                    return new List<ScheduleRow>();
                }
            }
        }

        public async Task<IList<ScheduleRow>> FetchRangeAsync(int startIndex, int count)
        {
            return await Task.Run(() => FetchRange(startIndex, count));
        }

        private ScheduleRow CreateScheduleRowFromElement(Element element, int index)
        {
            var scheduleRow = new ScheduleRow
            {
                ElementId = element.Id,
                RowIndex = index
            };

            // Populate row data efficiently
            foreach (var field in _visibleFields)
            {
                try
                {
                    var value = GetParameterValueFast(element, field);
                    scheduleRow.Data[field.GetName()] = value ?? "";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting parameter {field.GetName()}: {ex.Message}");
                    scheduleRow.Data[field.GetName()] = "";
                }
            }

            return scheduleRow;
        }

        private string GetParameterValueFast(Element element, ScheduleField field)
        {
            try
            {
                var parameterId = field.ParameterId;
                
                // Handle built-in parameters efficiently
                if (parameterId.IntegerValue < 0)
                {
                    var builtInParam = (BuiltInParameter)parameterId.IntegerValue;
                    var param = element.get_Parameter(builtInParam);
                    return GetParameterDisplayText(param);
                }

                // Handle regular parameters
                var parameter = element.get_Parameter(parameterId);
                return GetParameterDisplayText(parameter);
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

        public void Refresh()
        {
            InitializeElements();
        }
    }
}
