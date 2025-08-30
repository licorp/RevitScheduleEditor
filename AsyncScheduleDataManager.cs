using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RevitScheduleEditor
{
    /// <summary>
    /// Quản lý việc tải dữ liệu schedule một cách bất đồng bộ với hiệu năng cao
    /// Sử dụng các kỹ thuật như SheetLink: Incremental Loading, Background Processing, IExternalEventHandler
    /// </summary>
    public class AsyncScheduleDataManager : INotifyPropertyChanged
    {
        private readonly UIApplication _uiApp;
        private readonly DataLoadingEventHandler _eventHandler;
        private readonly ExternalEvent _externalEvent;
        private readonly Dispatcher _dispatcher;

        // Cấu hình batch loading
        private const int ELEMENT_COUNT_THRESHOLD = 1000; // Ngưỡng để quyết định có dùng batch loading không
        private const int INITIAL_BATCH_SIZE = 200;  // Số lượng elements tải ban đầu (không dùng nữa)
        private const int BACKGROUND_BATCH_SIZE = 50; // Số lượng elements tải trong mỗi batch nền (giảm xuống 50)
        
        // Dữ liệu và trạng thái
        private List<ElementId> _allElementIds;
        private List<ScheduleRow> _loadedRows;
        private ViewSchedule _currentSchedule;
        private int _totalElements;
        private int _loadedElements;
        private bool _isLoading;
        private string _loadingStatus;
        private double _loadingProgress;

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<BatchLoadedEventArgs> BatchLoaded;
        public event EventHandler LoadingCompleted;

        // Properties cho data binding
        public int TotalElements
        {
            get => _totalElements;
            private set
            {
                _totalElements = value;
                OnPropertyChanged(nameof(TotalElements));
            }
        }

        public int LoadedElements
        {
            get => _loadedElements;
            private set
            {
                _loadedElements = value;
                OnPropertyChanged(nameof(LoadedElements));
                LoadingProgress = TotalElements > 0 ? (double)LoadedElements / TotalElements * 100.0 : 0;
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public string LoadingStatus
        {
            get => _loadingStatus;
            private set
            {
                _loadingStatus = value;
                OnPropertyChanged(nameof(LoadingStatus));
            }
        }

        public double LoadingProgress
        {
            get => _loadingProgress;
            private set
            {
                _loadingProgress = value;
                OnPropertyChanged(nameof(LoadingProgress));
            }
        }

        public List<ScheduleRow> LoadedRows => _loadedRows;

        public AsyncScheduleDataManager(UIApplication uiApp)
        {
            _uiApp = uiApp;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _loadedRows = new List<ScheduleRow>();
            
            // Tạo External Event Handler để giao tiếp an toàn với Revit API
            _eventHandler = new DataLoadingEventHandler();
            _eventHandler.DataLoadingCompleted += OnDataLoadingCompleted;
            _externalEvent = ExternalEvent.Create(_eventHandler);
            
            DebugLog("AsyncScheduleDataManager initialized");
        }

        /// <summary>
        /// Bắt đầu tải dữ liệu schedule theo cơ chế SheetLink-style
        /// </summary>
        public async void LoadScheduleAsync(ViewSchedule schedule)
        {
            if (IsLoading)
            {
                DebugLog("Already loading, ignoring new request");
                return;
            }

            DebugLog($"=== Starting async load for schedule: {schedule.Name} ===");
            
            try
            {
                IsLoading = true;
                _currentSchedule = schedule;
                _allElementIds?.Clear();
                _loadedRows.Clear();
                TotalElements = 0;
                LoadedElements = 0;
                LoadingStatus = "Initializing...";

                // Bước 1: Lấy tất cả ElementId trước để đếm số lượng (nhanh)
                DebugLog("Step 1: Getting element count to determine loading strategy");
                LoadingStatus = "Checking element count...";
                
                var request = new DataLoadingRequest
                {
                    RequestType = DataLoadingRequestType.GetAllElementIds,
                    Schedule = schedule
                };
                
                _eventHandler.QueueRequest(request);
                _externalEvent.Raise();
            }
            catch (Exception ex)
            {
                DebugLog($"ERROR in LoadScheduleAsync: {ex.Message}\n{ex.StackTrace}");
                IsLoading = false;
                LoadingStatus = $"Error: {ex.Message}";
            }
        }

        private void OnDataLoadingCompleted(object sender, DataLoadingCompletedEventArgs e)
        {
            // Đảm bảo cập nhật UI trên UI thread
            _dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var result = e.Result;
                    DebugLog($"Data loading completed: Type={result.RequestType}, Time={result.ProcessingTime.TotalMilliseconds}ms");

                    switch (result.RequestType)
                    {
                        case DataLoadingRequestType.GetAllElementIds:
                            HandleElementIdsLoaded(result);
                            break;
                            
                        case DataLoadingRequestType.LoadElementBatch:
                            HandleBatchLoaded(result);
                            break;
                            
                        case DataLoadingRequestType.LoadAllElementsWithData:
                            HandleAllElementsLoaded(result);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"ERROR in OnDataLoadingCompleted: {ex.Message}\n{ex.StackTrace}");
                    IsLoading = false;
                    LoadingStatus = $"Error: {ex.Message}";
                }
            }));
        }

        private void HandleElementIdsLoaded(DataLoadingResult result)
        {
            _allElementIds = result.ElementIds;
            TotalElements = _allElementIds.Count;
            
            DebugLog($"Element IDs loaded: {TotalElements} elements");
            LoadingStatus = $"Found {TotalElements} elements. Loading data...";

            if (TotalElements == 0)
            {
                IsLoading = false;
                LoadingStatus = "No elements found in schedule";
                LoadingCompleted?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Kiểm tra số lượng element để quyết định cách load
            if (TotalElements < ELEMENT_COUNT_THRESHOLD)
            {
                // Nếu dưới 1000 element: load thẳng luôn tất cả (không cần lưu ElementIds)
                DebugLog($"Element count ({TotalElements}) is below threshold ({ELEMENT_COUNT_THRESHOLD}). Loading all elements with data at once.");
                LoadingStatus = "Loading all elements with data...";
                
                var request = new DataLoadingRequest
                {
                    RequestType = DataLoadingRequestType.LoadAllElementsWithData,
                    Schedule = _currentSchedule
                };
                
                _eventHandler.QueueRequest(request);
                _externalEvent.Raise();
            }
            else
            {
                // Nếu trên hoặc bằng 1000 element: load từng batch 50 element
                DebugLog($"Element count ({TotalElements}) is above threshold ({ELEMENT_COUNT_THRESHOLD}). Using batch loading with size {BACKGROUND_BATCH_SIZE}.");
                LoadingStatus = "Loading elements in batches...";
                LoadNextBatch(0, BACKGROUND_BATCH_SIZE); // Load batch đầu tiên 50 element
            }
        }

        private void HandleBatchLoaded(DataLoadingResult result)
        {
            var newRows = result.ScheduleRows;
            _loadedRows.AddRange(newRows);
            LoadedElements += newRows.Count;
            
            DebugLog($"Batch {result.BatchIndex + 1} loaded: {newRows.Count} rows, Total loaded: {LoadedElements}");
            LoadingStatus = $"Loading element {LoadedElements} of {TotalElements}...";

            // Thông báo có batch mới được tải
            BatchLoaded?.Invoke(this, new BatchLoadedEventArgs(newRows, LoadedElements, TotalElements));

            // Kiểm tra xem đã tải xong chưa
            if (LoadedElements >= TotalElements)
            {
                DebugLog("All data loaded successfully");
                IsLoading = false;
                LoadingStatus = $"Completed! Loaded {LoadedElements} elements";
                LoadingCompleted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                // Tải batch tiếp theo
                int nextBatchStart = LoadedElements;
                LoadNextBatch(nextBatchStart, BACKGROUND_BATCH_SIZE);
            }
        }

        private void LoadNextBatch(int startIndex, int batchSize)
        {
            if (startIndex >= _allElementIds.Count)
            {
                DebugLog("No more elements to load");
                return;
            }

            int actualBatchSize = Math.Min(batchSize, _allElementIds.Count - startIndex);
            var batchElementIds = _allElementIds.Skip(startIndex).Take(actualBatchSize).ToList();
            int batchIndex = startIndex / batchSize;

            DebugLog($"Loading batch {batchIndex + 1}: elements {startIndex + 1}-{startIndex + actualBatchSize}");

            var request = new DataLoadingRequest
            {
                RequestType = DataLoadingRequestType.LoadElementBatch,
                Schedule = _currentSchedule,
                ElementIds = batchElementIds,
                BatchIndex = batchIndex,
                BatchSize = actualBatchSize
            };

            _eventHandler.QueueRequest(request);
            _externalEvent.Raise();
        }

        private void HandleAllElementsLoaded(DataLoadingResult result)
        {
            // Xử lý khi load tất cả element với data trong một lần (cho < 1000 elements)
            var allRows = result.ScheduleRows;
            var elementIds = result.ElementIds;
            
            _allElementIds = elementIds; // Lưu ElementIds sau khi load xong data
            _loadedRows.AddRange(allRows);
            LoadedElements = allRows.Count;
            TotalElements = elementIds.Count; // Cập nhật lại total nếu cần
            
            DebugLog($"All elements loaded: {allRows.Count} rows");
            LoadingStatus = $"Completed! Loaded {LoadedElements} elements";

            // Thông báo có tất cả data được tải
            BatchLoaded?.Invoke(this, new BatchLoadedEventArgs(allRows, LoadedElements, TotalElements));
            
            // Hoàn thành
            IsLoading = false;
            LoadingCompleted?.Invoke(this, EventArgs.Empty);
        }

        public void CancelLoading()
        {
            DebugLog("Loading cancelled by user");
            IsLoading = false;
            LoadingStatus = "Cancelled";
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void DebugLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string fullMessage = $"[AsyncScheduleDataManager] {timestamp} - {message}";
            OutputDebugStringA(fullMessage + "\r\n");
            Debug.WriteLine(fullMessage);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
        private static extern void OutputDebugStringA(string lpOutputString);

        public void Dispose()
        {
            _externalEvent?.Dispose();
        }
    }

    /// <summary>
    /// Event args cho khi có batch mới được tải
    /// </summary>
    public class BatchLoadedEventArgs : EventArgs
    {
        public List<ScheduleRow> NewRows { get; }
        public int TotalLoadedElements { get; }
        public int TotalElements { get; }
        public double Progress => TotalElements > 0 ? (double)TotalLoadedElements / TotalElements * 100.0 : 0;

        public BatchLoadedEventArgs(List<ScheduleRow> newRows, int totalLoaded, int total)
        {
            NewRows = newRows;
            TotalLoadedElements = totalLoaded;
            TotalElements = total;
        }
    }
}
