using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RevitScheduleEditor
{
    /// <summary>
    /// External Event Handler để xử lý việc tải dữ liệu từ Revit API một cách an toàn
    /// từ background thread thông qua Main UI Thread của Revit
    /// </summary>
    public class DataLoadingEventHandler : IExternalEventHandler
    {
        private Queue<DataLoadingRequest> _requestQueue = new Queue<DataLoadingRequest>();
        private readonly object _lockObject = new object();
        
        // Event để báo hiệu khi có dữ liệu mới được tải
        public event EventHandler<DataLoadingCompletedEventArgs> DataLoadingCompleted;

        public void Execute(UIApplication app)
        {
            DebugLog("DataLoadingEventHandler.Execute started");
            
            try
            {
                Document doc = app.ActiveUIDocument.Document;
                
                lock (_lockObject)
                {
                    while (_requestQueue.Count > 0)
                    {
                        var request = _requestQueue.Dequeue();
                        DebugLog($"Processing request: Type={request.RequestType}, Batch={request.BatchIndex}, Count={request.ElementIds?.Count ?? 0}");
                        
                        switch (request.RequestType)
                        {
                            case DataLoadingRequestType.GetAllElementIds:
                                ProcessGetAllElementIds(doc, request);
                                break;
                                
                            case DataLoadingRequestType.LoadElementBatch:
                                ProcessLoadElementBatch(doc, request);
                                break;
                                
                            case DataLoadingRequestType.LoadAllElementsWithData:
                                ProcessLoadAllElementsWithData(doc, request);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"ERROR in DataLoadingEventHandler.Execute: {ex.Message}\n{ex.StackTrace}");
            }
            
            DebugLog("DataLoadingEventHandler.Execute completed");
        }

        private void ProcessGetAllElementIds(Document doc, DataLoadingRequest request)
        {
            DebugLog($"Getting all element IDs for schedule: {request.Schedule.Name}");
            
            var stopwatch = Stopwatch.StartNew();
            
            var collector = new FilteredElementCollector(doc, request.Schedule.Id)
                .WhereElementIsNotElementType();
            var elementIds = collector.ToElementIds().ToList();
            
            stopwatch.Stop();
            DebugLog($"Found {elementIds.Count} element IDs in {stopwatch.ElapsedMilliseconds}ms");
            
            var result = new DataLoadingResult
            {
                RequestType = request.RequestType,
                ElementIds = elementIds,
                Schedule = request.Schedule,
                ProcessingTime = stopwatch.Elapsed
            };
            
            DataLoadingCompleted?.Invoke(this, new DataLoadingCompletedEventArgs(result));
        }

        private void ProcessLoadElementBatch(Document doc, DataLoadingRequest request)
        {
            DebugLog($"Loading batch {request.BatchIndex + 1}: {request.ElementIds.Count} elements");
            
            var stopwatch = Stopwatch.StartNew();
            var scheduleRows = new List<ScheduleRow>();
            
            // Lấy thông tin fields của schedule
            var visibleFields = request.Schedule.Definition.GetFieldOrder()
                .Select(id => request.Schedule.Definition.GetField(id))
                .Where(f => !f.IsHidden).ToList();
            
            foreach (var elementId in request.ElementIds)
            {
                try
                {
                    Element elem = doc.GetElement(elementId);
                    if (elem == null) continue;
                    
                    var scheduleRow = new ScheduleRow(elem);
                    foreach (var field in visibleFields)
                    {
                        Parameter param = GetParameterFromField(elem, field);
                        string value = param != null ? param.AsValueString() ?? param.AsString() ?? string.Empty : string.Empty;
                        scheduleRow.AddValue(field.GetName(), value);
                    }
                    scheduleRows.Add(scheduleRow);
                }
                catch (Exception ex)
                {
                    DebugLog($"Error processing element {elementId}: {ex.Message}");
                }
            }
            
            stopwatch.Stop();
            DebugLog($"Loaded batch {request.BatchIndex + 1}: {scheduleRows.Count} rows in {stopwatch.ElapsedMilliseconds}ms");
            
            var result = new DataLoadingResult
            {
                RequestType = request.RequestType,
                BatchIndex = request.BatchIndex,
                ScheduleRows = scheduleRows,
                ProcessingTime = stopwatch.Elapsed
            };
            
            DataLoadingCompleted?.Invoke(this, new DataLoadingCompletedEventArgs(result));
        }

        private void ProcessLoadAllElementsWithData(Document doc, DataLoadingRequest request)
        {
            DebugLog($"Loading all elements with data for schedule: {request.Schedule.Name}");
            
            var stopwatch = Stopwatch.StartNew();
            var scheduleRows = new List<ScheduleRow>();
            
            // Bước 1: Lấy tất cả ElementId
            var collector = new FilteredElementCollector(doc, request.Schedule.Id)
                .WhereElementIsNotElementType();
            var elementIds = collector.ToElementIds().ToList();
            
            DebugLog($"Found {elementIds.Count} elements, now loading data...");
            
            // Bước 2: Load data của tất cả elements luôn
            var visibleFields = request.Schedule.Definition.GetFieldOrder()
                .Select(id => request.Schedule.Definition.GetField(id))
                .Where(f => !f.IsHidden).ToList();
            
            foreach (var elementId in elementIds)
            {
                try
                {
                    Element elem = doc.GetElement(elementId);
                    if (elem == null) continue;
                    
                    var scheduleRow = new ScheduleRow(elem);
                    foreach (var field in visibleFields)
                    {
                        Parameter param = GetParameterFromField(elem, field);
                        string value = param != null ? param.AsValueString() ?? param.AsString() ?? string.Empty : string.Empty;
                        scheduleRow.AddValue(field.GetName(), value);
                    }
                    scheduleRows.Add(scheduleRow);
                }
                catch (Exception ex)
                {
                    DebugLog($"Error processing element {elementId}: {ex.Message}");
                }
            }
            
            stopwatch.Stop();
            DebugLog($"Loaded all elements: {scheduleRows.Count} rows in {stopwatch.ElapsedMilliseconds}ms");
            
            var result = new DataLoadingResult
            {
                RequestType = request.RequestType,
                ElementIds = elementIds,
                ScheduleRows = scheduleRows,
                Schedule = request.Schedule,
                BatchIndex = 0,
                ProcessingTime = stopwatch.Elapsed
            };
            
            DataLoadingCompleted?.Invoke(this, new DataLoadingCompletedEventArgs(result));
        }

        private Parameter GetParameterFromField(Element element, ScheduleField field)
        {
            try
            {
                // Use the same approach as original code
                return element.get_Parameter((BuiltInParameter)field.ParameterId.IntegerValue);
            }
            catch
            {
                return null;
            }
        }

        public void QueueRequest(DataLoadingRequest request)
        {
            lock (_lockObject)
            {
                _requestQueue.Enqueue(request);
                DebugLog($"Queued request: Type={request.RequestType}, Queue size={_requestQueue.Count}");
            }
        }

        public string GetName()
        {
            return "RevitScheduleEditor_DataLoadingEventHandler";
        }

        private void DebugLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string fullMessage = $"[DataLoadingEventHandler] {timestamp} - {message}";
            OutputDebugStringA(fullMessage + "\r\n");
            Debug.WriteLine(fullMessage);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
        private static extern void OutputDebugStringA(string lpOutputString);
    }

    /// <summary>
    /// Các loại yêu cầu tải dữ liệu
    /// </summary>
    public enum DataLoadingRequestType
    {
        GetAllElementIds,
        LoadElementBatch,
        LoadAllElementsWithData // Mới: Load tất cả element với data luôn (cho < 1000 elements)
    }

    /// <summary>
    /// Yêu cầu tải dữ liệu
    /// </summary>
    public class DataLoadingRequest
    {
        public DataLoadingRequestType RequestType { get; set; }
        public ViewSchedule Schedule { get; set; }
        public List<ElementId> ElementIds { get; set; }
        public int BatchIndex { get; set; }
        public int BatchSize { get; set; }
    }

    /// <summary>
    /// Kết quả sau khi tải dữ liệu
    /// </summary>
    public class DataLoadingResult
    {
        public DataLoadingRequestType RequestType { get; set; }
        public List<ElementId> ElementIds { get; set; }
        public List<ScheduleRow> ScheduleRows { get; set; }
        public ViewSchedule Schedule { get; set; }
        public int BatchIndex { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// Event args cho khi hoàn thành tải dữ liệu
    /// </summary>
    public class DataLoadingCompletedEventArgs : EventArgs
    {
        public DataLoadingResult Result { get; }

        public DataLoadingCompletedEventArgs(DataLoadingResult result)
        {
            Result = result;
        }
    }
}
