using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Threading;

namespace RevitScheduleEditor
{
    public class ScheduleEditorViewModel : BaseViewModel
    {
        private readonly Document _doc;
        private readonly UIApplication _uiApp;
        private ViewSchedule _selectedSchedule;
        private ScheduleEditorWindow _parentWindow; // Add reference to parent window
        
        public ObservableCollection<ViewSchedule> Schedules { get; set; }
        
        // Use ObservableCollection base to support both Virtual and Progressive collections
        private ObservableCollection<ScheduleRow> _scheduleData;
        public ObservableCollection<ScheduleRow> ScheduleData 
        { 
            get => _scheduleData;
            set
            {
                _scheduleData = value;
                OnPropertyChanged();
            }
        }
        
        private List<ScheduleRow> _allScheduleData;
        private string _searchText;
        
        // Excel-like features
        private string[,] _clipboardData;
        private List<Dictionary<string, string>> _undoHistory;
        private List<Dictionary<string, string>> _redoHistory;
        private const int MaxHistorySize = 50;
        
        // Progress tracking properties
        private bool _isLoadingData;
        private string _loadingStatus;
        private double _loadingProgress;
        private int _totalElements;
        private int _loadedElements;
        
        // Performance optimization properties
        private CancellationTokenSource _loadingCancellationTokenSource;
        private bool _isVirtualScrollingEnabled = true;
        private const int VIRTUAL_SCROLL_THRESHOLD = 500;
        private const int CHUNK_SIZE = 25; // Reduced chunk size for better responsiveness
        
        // Progressive loading properties
        private bool _isBackgroundLoading;
        private string _backgroundLoadingStatus;
        private bool _hasMoreDataToLoad;

        public bool IsBackgroundLoading
        {
            get => _isBackgroundLoading;
            set
            {
                _isBackgroundLoading = value;
                OnPropertyChanged(nameof(IsBackgroundLoading));
            }
        }

        public string BackgroundLoadingStatus
        {
            get => _backgroundLoadingStatus;
            set
            {
                _backgroundLoadingStatus = value;
                OnPropertyChanged(nameof(BackgroundLoadingStatus));
            }
        }

        public bool HasMoreDataToLoad
        {
            get => _hasMoreDataToLoad;
            set
            {
                _hasMoreDataToLoad = value;
                OnPropertyChanged(nameof(HasMoreDataToLoad));
            }
        }

        // Debug logging methods
        private void DebugLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string fullMessage = $"[ScheduleEditorViewModel] {timestamp} - {message}";
            OutputDebugStringA(fullMessage + "\r\n");
            Debug.WriteLine(fullMessage);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
        private static extern void OutputDebugStringA(string lpOutputString);

        // Non-blocking notification helper
        private void ShowNonBlockingNotification(string message, string title = "Thông báo", int autoCloseSeconds = 3)
        {
            try
            {
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var notificationWindow = new Window
                    {
                        Title = title,
                        Content = new TextBlock
                        {
                            Text = message,
                            Margin = new Thickness(20),
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 400
                        },
                        SizeToContent = SizeToContent.WidthAndHeight,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        Topmost = true,
                        ResizeMode = ResizeMode.NoResize,
                        ShowInTaskbar = false, // Notification không cần taskbar
                        // Bỏ Owner để tránh lock trong Revit add-in
                    };

                    notificationWindow.Show();

                    // Auto-close timer
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(autoCloseSeconds) };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        notificationWindow.Close();
                    };
                    timer.Start();
                }));
            }
            catch (Exception ex)
            {
                // Fallback to debug log if UI fails
                DebugLog($"ShowNonBlockingNotification failed: {ex.Message}. Original message: {message}");
            }
        }

        // Progress properties for data binding
        public bool IsLoadingData
        {
            get => _isLoadingData;
            set
            {
                _isLoadingData = value;
                OnPropertyChanged(nameof(IsLoadingData));
                
                // Force command re-evaluation when loading state changes
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                
                DebugLog($"IsLoadingData set to: {value}");
            }
        }

        public string LoadingStatus
        {
            get => _loadingStatus;
            set
            {
                _loadingStatus = value;
                OnPropertyChanged(nameof(LoadingStatus));
            }
        }

        public double LoadingProgress
        {
            get => _loadingProgress;
            set
            {
                _loadingProgress = value;
                OnPropertyChanged(nameof(LoadingProgress));
            }
        }

        public int TotalElements
        {
            get => _totalElements;
            set
            {
                _totalElements = value;
                OnPropertyChanged(nameof(TotalElements));
            }
        }

        public int LoadedElements
        {
            get => _loadedElements;
            set
            {
                _loadedElements = value;
                OnPropertyChanged(nameof(LoadedElements));
            }
        }
        
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                // Filter sẽ được handle bởi Window
            }
        }

        public ICommand AutofillCommand { get; }
        public ICommand CopyCommand { get; }
        public ICommand PasteCommand { get; }
        public ICommand CutCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand FillDownCommand { get; }
        public ICommand FillRightCommand { get; }
        public ICommand ClearAllFiltersCommand { get; }
        public ICommand SelectHighlightedElementsCommand { get; }
        public ICommand ShowHighlightedElementsCommand { get; }
        
        // New commands for the buttons - with backing field for PreviewEditCommand  
        private ICommand _previewEditCommand;
        public ICommand PreviewEditCommand 
        { 
            get => _previewEditCommand;
            private set
            {
                _previewEditCommand = value;
                OnPropertyChanged();
            }
        }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand CancelLoadingCommand { get; }
        
        public ViewSchedule SelectedSchedule
        {
            get => _selectedSchedule;
            set
            {
                DebugLog($"SelectedSchedule setter called - Old: {_selectedSchedule?.Name ?? "null"}, New: {value?.Name ?? "null"}");
                
                _selectedSchedule = value;
                OnPropertyChanged();
                
                // Force immediate command re-evaluation
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                DebugLog($"Commands invalidated immediately for SelectedSchedule: {_selectedSchedule?.Name ?? "null"}");
                
                // Also force PreviewEditCommand to re-evaluate specifically
                if (PreviewEditCommand is RelayCommand cmd)
                {
                    cmd.RaiseCanExecuteChanged();
                    DebugLog($"PreviewEditCommand.RaiseCanExecuteChanged() called");
                }
                
                // NUCLEAR OPTION: Recreate the command object entirely to force UI rebinding
                if (_selectedSchedule != null)
                {
                    DebugLog($"Recreating PreviewEditCommand to force UI rebinding");
                    _previewEditCommand = new RelayCommand(ExecutePreviewEdit, CanExecutePreviewEdit);
                    OnPropertyChanged(nameof(PreviewEditCommand));
                    DebugLog($"PreviewEditCommand recreated and PropertyChanged fired");
                }
                
                // Force PropertyChanged for PreviewEditCommand to trigger UI rebinding
                OnPropertyChanged(nameof(PreviewEditCommand));
                DebugLog($"OnPropertyChanged(PreviewEditCommand) called");
                
                // Force UI update using Dispatcher with higher priority - with error handling
                try 
                {
                    var app = Application.Current;
                    var dispatcher = app?.Dispatcher;
                    DebugLog($"Application.Current: {app != null}, Dispatcher: {dispatcher != null}");
                    
                    if (dispatcher != null)
                    {
                        dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                DebugLog($"Dispatcher callback executed - about to check CanExecutePreviewEdit again");
                                var canExecute = CanExecutePreviewEdit(null);
                                DebugLog($"Force CanExecutePreviewEdit check result: {canExecute}");
                                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                            }
                            catch (Exception dispEx)
                            {
                                DebugLog($"Error in Dispatcher callback: {dispEx.Message}");
                            }
                        }), DispatcherPriority.DataBind);
                        DebugLog($"Dispatcher.BeginInvoke called successfully");
                    }
                    else
                    {
                        DebugLog($"Dispatcher is null - using immediate command check");
                        var canExecute = CanExecutePreviewEdit(null);
                        DebugLog($"Immediate CanExecutePreviewEdit check result: {canExecute}");
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"Error setting up Dispatcher: {ex.Message}");
                }
                
                DebugLog($"SelectedSchedule property changed and commands invalidated");
                
                // Don't auto-load data anymore - user needs to click Preview/Edit
                
                // Show helpful hint when schedule is selected
                if (_selectedSchedule != null && (ScheduleData == null || ScheduleData.Count == 0))
                {
                    DebugLog($"Showing notification for selected schedule: {_selectedSchedule.Name}");
                    ShowNonBlockingNotification($"Đã chọn '{_selectedSchedule.Name}'. Nhấn nút 'Preview/Edit' để tải dữ liệu.", "Gợi ý", 3);
                }
            }
        }
        
        public ICommand UpdateModelCommand { get; }

        public ScheduleEditorViewModel(Document doc, ScheduleEditorWindow parentWindow = null)
        {
            DebugLog("=== ScheduleEditorViewModel Constructor Started ===");
            
            try
            {
                _doc = doc;
                _parentWindow = parentWindow; // Store reference to parent window
                DebugLog($"Document assigned: {doc.Title}");
                DebugLog($"Parent window assigned: {parentWindow != null}");
                
                _allScheduleData = new List<ScheduleRow>();
                Schedules = new ObservableCollection<ViewSchedule>();
                
                // Initialize with null VirtualScheduleCollection - will be created when schedule is selected
                ScheduleData = null;
                DebugLog("Collections initialized - VirtualScheduleCollection will be created on demand");
                
                // Initialize history
                _undoHistory = new List<Dictionary<string, string>>();
                _redoHistory = new List<Dictionary<string, string>>();
                DebugLog("Undo/Redo history initialized");
                
                // Commands
                UpdateModelCommand = new RelayCommand(UpdateModel, CanUpdateModel);
                AutofillCommand = new RelayCommand(ExecuteAutofill, CanExecuteAutofill);
                CopyCommand = new RelayCommand(ExecuteCopy, CanExecuteCopy);
                PasteCommand = new RelayCommand(ExecutePaste, CanExecutePaste);
                CutCommand = new RelayCommand(ExecuteCut, CanExecuteCut);
                UndoCommand = new RelayCommand(ExecuteUndo, CanExecuteUndo);
                RedoCommand = new RelayCommand(ExecuteRedo, CanExecuteRedo);
                FillDownCommand = new RelayCommand(ExecuteFillDown, CanExecuteFillDown);
                FillRightCommand = new RelayCommand(ExecuteFillRight, CanExecuteFillRight);
                ClearAllFiltersCommand = new RelayCommand(ExecuteClearAllFilters, CanExecuteClearAllFilters);
                SelectHighlightedElementsCommand = new RelayCommand(ExecuteSelectHighlightedElements, CanExecuteSelectHighlightedElements);
                ShowHighlightedElementsCommand = new RelayCommand(ExecuteShowHighlightedElements, CanExecuteShowHighlightedElements);
                
                // New commands
                _previewEditCommand = new RelayCommand(ExecutePreviewEdit, CanExecutePreviewEdit);
                ImportCommand = new RelayCommand(ExecuteImport, CanExecuteImport);
                ExportCommand = new RelayCommand(ExecuteExport, CanExecuteExport);
                
                // Add cancel loading command
                CancelLoadingCommand = new RelayCommand(ExecuteCancelLoading, CanExecuteCancelLoading);
                DebugLog("All commands initialized");

                LoadSchedules();
                DebugLog("ScheduleEditorViewModel constructor completed successfully");
            }
            catch (Exception ex)
            {
                DebugLog($"ERROR in ScheduleEditorViewModel constructor: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
        
        // Method to set parent window reference after construction
        public void SetParentWindow(ScheduleEditorWindow parentWindow)
        {
            _parentWindow = parentWindow;
            DebugLog($"Parent window set: {parentWindow != null}");
        }

        private void LoadSchedules()
        {
            DebugLog("Loading schedules from document...");
            
            try
            {
                var schedules = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .Where(s => !s.IsTemplate && s.ViewType == ViewType.Schedule)
                    .OrderBy(s => s.Name)
                    .ToList();
                
                DebugLog($"Found {schedules.Count} schedules in document");
                
                if (schedules.Count == 0)
                {
                    DebugLog("WARNING: No schedules found in document!");
                    ShowNonBlockingNotification("Không tìm thấy schedule nào trong document!", "Không có Schedule", 3);
                }
                
                Schedules.Clear();
                foreach (var s in schedules)
                {
                    Schedules.Add(s);
                    DebugLog($"Added schedule: {s.Name} (ID: {s.Id})");
                }
                
                DebugLog($"LoadSchedules completed successfully - Total schedules in collection: {Schedules.Count}");
            }
            catch (Exception ex)
            {
                DebugLog($"ERROR in LoadSchedules: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        // Command methods
        private async void ExecutePreviewEdit(object obj)
        {
            DebugLog("ExecutePreviewEdit called - Loading schedule data");
            
            if (SelectedSchedule == null)
            {
                ShowNonBlockingNotification("Vui lòng chọn một schedule trước.", "Chưa chọn Schedule");
                return;
            }

            // Prevent recursive calls
            if (IsLoadingData)
            {
                DebugLog("Already loading data - ignoring duplicate call");
                return;
            }

            // Cancel any existing loading operation
            _loadingCancellationTokenSource?.Cancel();
            _loadingCancellationTokenSource = new CancellationTokenSource();

            // Clear current data safely
            try
            {
                ScheduleData?.Clear();
                _allScheduleData?.Clear();
            }
            catch (Exception ex)
            {
                DebugLog($"Error clearing data: {ex.Message}");
            }
            
            // Use optimized async loading
            await LoadScheduleDataAsync(_loadingCancellationTokenSource.Token);
        }

        private bool CanExecutePreviewEdit(object obj)
        {
            var canExecute = SelectedSchedule != null && !IsLoadingData;
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            DebugLog($"[{timestamp}] CanExecutePreviewEdit - SelectedSchedule: {SelectedSchedule?.Name ?? "null"}, IsLoadingData: {IsLoadingData}, Result: {canExecute}");
            return canExecute;
        }

        private void ExecuteCancelLoading(object obj)
        {
            DebugLog("User cancelled data loading");
            _loadingCancellationTokenSource?.Cancel();
            IsLoadingData = false;
            LoadingStatus = "Loading cancelled by user";
        }

        private bool CanExecuteCancelLoading(object obj)
        {
            return IsLoadingData;
        }

        // NEW: Optimized async loading method
        // NEW: Optimized async loading method with VirtualizingCollection
        private async Task LoadScheduleDataAsync(CancellationToken cancellationToken)
        {
            DebugLog("=== LoadScheduleDataAsync Started (VirtualizingCollection) ===");
            
            if (SelectedSchedule == null) 
            {
                DebugLog("SelectedSchedule is null, returning");
                LoadingStatus = "No schedule selected";
                return;
            }

            DebugLog($"Loading data for schedule: {SelectedSchedule.Name}");
            IsLoadingData = true;
            LoadingStatus = "🚀 Initializing Virtual Data Loading...";
            LoadingProgress = 0;
            
            try
            {
                // Step 1: Quick element count check
                LoadingStatus = "📊 Analyzing schedule structure...";
                var collector = new FilteredElementCollector(_doc, SelectedSchedule.Id).WhereElementIsNotElementType();
                var elementCount = collector.GetElementCount();
                TotalElements = elementCount;
                
                DebugLog($"Found {elementCount} elements in schedule");
                
                if (elementCount == 0)
                {
                    LoadingStatus = "No elements found in schedule";
                    LoadingProgress = 100;
                    IsLoadingData = false;
                    DebugLog("No elements found in schedule - schedule may be empty");
                    return;
                }

                // Step 2: Get visible fields
                LoadingStatus = "⚙️ Preparing data structure...";
                var visibleFields = SelectedSchedule.Definition.GetFieldOrder()
                    .Select(id => SelectedSchedule.Definition.GetField(id))
                    .Where(f => !f.IsHidden).ToList();

                DebugLog($"Found {visibleFields.Count} visible fields");

                // Step 3: Use VirtualizingCollection for ALL schedules (small or large)
                await LoadWithVirtualizationAsync(visibleFields, cancellationToken);
                
                if (!cancellationToken.IsCancellationRequested)
                {
                    LoadingProgress = 100;
                    LoadingStatus = $"✅ Ready! Virtual scrolling enabled for {elementCount:N0} elements";
                    DebugLog($"Virtual data loading completed for {elementCount} elements");
                    
                    // Debug VirtualScheduleCollection status
                    if (ScheduleData is VirtualScheduleCollection virtualCollection)
                    {
                        virtualCollection.DebugDataStatus();
                        
                        // Force a UI refresh after 1 second delay to ensure data is loaded
                        _ = Task.Delay(1000).ContinueWith(_ => 
                        {
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                virtualCollection.RefreshCollection();
                                OnPropertyChanged(nameof(ScheduleData));
                                DebugLog("VirtualScheduleCollection: Forced UI refresh applied");
                            });
                        });
                    }
                    
                    // Notify that data has changed
                    OnPropertyChanged(nameof(ScheduleData));
                }
            }
            catch (OperationCanceledException)
            {
                LoadingStatus = "❌ Loading cancelled";
                DebugLog("Loading was cancelled");
            }
            catch (OutOfMemoryException)
            {
                LoadingStatus = "Not enough memory to load this schedule";
                DebugLog("OutOfMemoryException during loading - falling back to virtual loading");
                
                // VirtualizingCollection should handle memory better
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                LoadingStatus = $"❌ Error: {ex.Message}";
                DebugLog($"Error during loading: {ex}");
                ShowNonBlockingNotification($"Lỗi khi tải dữ liệu schedule: {ex.Message}", "Lỗi", 5);
            }
            finally
            {
                IsLoadingData = false;
                DebugLog("=== LoadScheduleDataAsync Completed ===");
                
                // Refresh Update Model button state after data loading
                RefreshUpdateButtonState();
            }
        }

        // NEW: Load data using VirtualScheduleCollection for instant UI responsiveness
        private async Task LoadWithVirtualizationAsync(List<ScheduleField> visibleFields, CancellationToken cancellationToken)
        {
            DebugLog("Loading with ProgressiveScheduleCollection - instant UI response with real-time loading");
            
            LoadingStatus = "🔄 Creating progressive loading collection...";
            await Task.Delay(50, cancellationToken); // Allow UI update
            
            // Create progressive schedule collection with larger chunk size for better performance
            var chunkSize = TotalElements > 1000 ? 50 : (TotalElements > 500 ? 30 : 20);
            DebugLog($"Using dynamic chunk size: {chunkSize} for {TotalElements} elements");
            
            var progressiveCollection = new ProgressiveScheduleCollection(_doc, SelectedSchedule, visibleFields, chunkSize);
            ScheduleData = progressiveCollection;
            
            LoadingStatus = "⚡ Progressive loading started - items appear in real-time!";
            await Task.Delay(50, cancellationToken); // Allow UI update
            
            LoadingStatus = $"🚀 Progressive loading active! Total: {TotalElements:N0} elements (loading in background)";
            
            DebugLog($"ProgressiveScheduleCollection created - ChunkSize: {chunkSize}, Total: {TotalElements}");
            
            // No MessageBox - let the progressive loading work silently
            // Progress will be visible as items appear in the DataGrid in real-time
        }

        // NEW: Optimized loading for small schedules
        private async Task LoadSmallScheduleAsync(FilteredElementCollector collector, List<ScheduleField> visibleFields, CancellationToken cancellationToken)
        {
            DebugLog("Using small schedule loading strategy");
            LoadingStatus = "Loading all elements...";
            
            var elements = collector.ToElements();
            var processedCount = 0;
            
            foreach (Element elem in elements)
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                try
                {
                    if (elem == null || !elem.IsValidObject) continue;

                    var scheduleRow = await ProcessElementAsync(elem, visibleFields, cancellationToken);
                    if (scheduleRow != null)
                    {
                        _allScheduleData.Add(scheduleRow);
                        ScheduleData.Add(scheduleRow);
                        processedCount++;
                    }

                    // Update progress every 50 items
                    if (processedCount % 50 == 0)
                    {
                        LoadingProgress = (double)processedCount / elements.Count * 100;
                        LoadingStatus = $"Loaded {processedCount}/{elements.Count} elements...";
                        
                        // Yield control to UI thread
                        await Task.Delay(1, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"Error processing element {elem?.Id}: {ex.Message}");
                }
            }
        }

        // NEW: Progressive loading for large schedules - Load 1000 first, then continue background loading
        private async Task LoadLargeScheduleAsync(FilteredElementCollector collector, List<ScheduleField> visibleFields, CancellationToken cancellationToken)
        {
            DebugLog("Using large schedule progressive loading strategy");
            
            // Show user choice for large schedules with speed optimization
            // Use progressive loading by default for non-blocking experience
            var useProgressiveLoading = true; // Default to progressive loading
            
            ShowNonBlockingNotification($"Đang tải {TotalElements:N0} phần tử với chế độ Progressive Loading để tối ưu hiệu năng.", "Tải dữ liệu lớn", 2);

            var elements = collector.ToElements();
            DebugLog($"Retrieved {elements.Count} elements for processing");
            
            if (useProgressiveLoading)
            {
                // Progressive loading: 1000 first, then background loading
                await LoadProgressiveAsync(elements, visibleFields, cancellationToken);
            }
            else
            {
                // Load all at once with parallel processing
                await LoadAllAtOnceAsync(elements, visibleFields, cancellationToken);
            }
        }

        // Progressive loading implementation with speed tracking
        private async Task LoadProgressiveAsync(IList<Element> elements, List<ScheduleField> visibleFields, CancellationToken cancellationToken)
        {
            DebugLog("Starting ultra-fast progressive loading");
            var startTime = DateTime.Now;
            
            const int INITIAL_LOAD_COUNT = 1000;
            var totalElements = elements.Count;
            
            // Phase 1: Load first 1000 items with parallel processing
            LoadingStatus = "🚀 Phase 1: Ultra-fast loading first 1000 items...";
            var initialElements = elements.Take(INITIAL_LOAD_COUNT).ToList();
            
            var phase1Start = DateTime.Now;
            await LoadElementBatch(initialElements, visibleFields, cancellationToken, 0, INITIAL_LOAD_COUNT);
            var phase1Duration = DateTime.Now - phase1Start;
            
            // Update UI immediately after Phase 1
            LoadingProgress = (double)INITIAL_LOAD_COUNT / totalElements * 100;
            var itemsLoaded = Math.Min(INITIAL_LOAD_COUNT, totalElements);
            var speed1 = itemsLoaded / phase1Duration.TotalSeconds;
            
            LoadingStatus = $"✅ Ready to use! {itemsLoaded} items loaded in {phase1Duration.TotalSeconds:F1}s ({speed1:F0} items/sec)";
            LoadedElements = itemsLoaded;
            IsLoadingData = false; // User can start working now
            
            DebugLog($"Phase 1 completed: {ScheduleData.Count} items loaded at {speed1:F0} items/sec");
            
            // Phase 2: Continue loading remaining items in background if there are more
            if (totalElements > INITIAL_LOAD_COUNT)
            {
                HasMoreDataToLoad = true;
                IsBackgroundLoading = true;
                
                var remainingElements = elements.Skip(INITIAL_LOAD_COUNT).ToList();
                DebugLog($"Phase 2: Loading remaining {remainingElements.Count} items in background");
                
                BackgroundLoadingStatus = $"🔄 Background loading {remainingElements.Count} more items with parallel processing...";
                
                // Add small delay to let user start working with initial data
                await Task.Delay(500, cancellationToken);
                
                var phase2Start = DateTime.Now;
                await LoadElementBatch(remainingElements, visibleFields, cancellationToken, INITIAL_LOAD_COUNT, totalElements);
                var phase2Duration = DateTime.Now - phase2Start;
                var speed2 = remainingElements.Count / phase2Duration.TotalSeconds;
                
                // Background loading completed
                IsBackgroundLoading = false;
                HasMoreDataToLoad = false;
                
                var totalDuration = DateTime.Now - startTime;
                var overallSpeed = totalElements / totalDuration.TotalSeconds;
                
                BackgroundLoadingStatus = $"🎉 Complete! {remainingElements.Count} items added in {phase2Duration.TotalSeconds:F1}s ({speed2:F0} items/sec)";
                LoadingStatus = $"Complete: {totalElements} items in {totalDuration.TotalSeconds:F1}s (avg: {overallSpeed:F0} items/sec)";
                
                DebugLog($"Progressive loading completed - Total: {overallSpeed:F0} items/sec");
                
                // Show completion notification with performance stats
                ShowNonBlockingNotification(
                    $"🚀 Hoàn tất tải dữ liệu!\n\n" +
                    $"📊 Thống kê hiệu năng:\n" +
                    $"Giai đoạn 1: {itemsLoaded} phần tử trong {phase1Duration.TotalSeconds:F1}s ({speed1:F0} phần tử/giây)\n" +
                    $"Giai đoạn 2: {remainingElements.Count} phần tử trong {phase2Duration.TotalSeconds:F1}s ({speed2:F0} phần tử/giây)\n" +
                    $"Tổng thể: {totalElements} phần tử trong {totalDuration.TotalSeconds:F1}s ({overallSpeed:F0} phần tử/giây)\n\n" +
                    $"✅ Bạn có thể làm việc với toàn bộ dữ liệu!",
                    "🚀 Hoàn tất Progressive Loading", 5);
            }
            else
            {
                var totalDuration = DateTime.Now - startTime;
                var overallSpeed = totalElements / totalDuration.TotalSeconds;
                LoadingStatus = $"Complete: {totalElements} items in {totalDuration.TotalSeconds:F1}s ({overallSpeed:F0} items/sec)";
                HasMoreDataToLoad = false;
                DebugLog($"All items loaded in Phase 1 at {overallSpeed:F0} items/sec");
            }
        }

        // Load all elements at once (original behavior)
        private async Task LoadAllAtOnceAsync(IList<Element> elements, List<ScheduleField> visibleFields, CancellationToken cancellationToken)
        {
            DebugLog("Loading all elements at once");
            LoadingStatus = $"Loading all {elements.Count} elements...";
            
            await LoadElementBatch(elements, visibleFields, cancellationToken, 0, elements.Count);
            
            LoadingStatus = $"Loading complete: {elements.Count} items loaded";
        }

        // Helper method to load a batch of elements with PARALLEL PROCESSING
        private async Task LoadElementBatch(IList<Element> elements, List<ScheduleField> visibleFields, CancellationToken cancellationToken, int startIndex, int totalCount)
        {
            DebugLog($"Loading batch of {elements.Count} elements with parallel processing");
            
            var processedCount = 0;
            var batchSize = Math.Min(100, elements.Count); // Process in smaller batches for UI updates
            
            for (int i = 0; i < elements.Count; i += batchSize)
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                var batch = elements.Skip(i).Take(batchSize).ToList();
                
                // Process batch in parallel for maximum speed
                var tasks = batch.Select(async elem =>
                {
                    if (elem == null || !elem.IsValidObject) return null;
                    return await ProcessElementAsync(elem, visibleFields, cancellationToken);
                }).ToArray();
                
                try
                {
                    // Wait for all elements in batch to complete
                    var batchResults = await Task.WhenAll(tasks);
                    
                    // Add successful results to collections
                    var validResults = batchResults.Where(row => row != null).ToList();
                    
                    foreach (var row in validResults)
                    {
                        _allScheduleData.Add(row);
                        ScheduleData.Add(row);
                    }
                    
                    processedCount += validResults.Count;
                    
                    // Update progress
                    var totalProcessed = startIndex + processedCount;
                    LoadingProgress = (double)totalProcessed / totalCount * 100;
                    LoadingStatus = $"Loaded {totalProcessed}/{totalCount} elements... (batch: {validResults.Count}/{batch.Count})";
                    LoadedElements = totalProcessed;
                    
                    // Force UI update after each batch
                    OnPropertyChanged(nameof(ScheduleData));
                    
                    // Yield control to UI thread - shorter delay for faster loading
                    await Task.Delay(1, cancellationToken);
                    
                    // Aggressive garbage collection for large datasets
                    if (processedCount % 1000 == 0)
                    {
                        GC.Collect(0, GCCollectionMode.Optimized);
                        DebugLog($"GC performed after {processedCount} elements");
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"Error processing batch: {ex.Message}");
                    // Continue with next batch
                }
            }
            
            DebugLog($"Batch loading completed: {processedCount} elements processed");
        }

        // NEW: Super fast async element processing with parallel processing
        private async Task<ScheduleRow> ProcessElementAsync(Element elem, List<ScheduleField> visibleFields, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return null;
            
            try
            {
                var scheduleRow = new ScheduleRow(elem);
                
                // Fast parameter reading - avoid try-catch overhead for each field
                foreach (var field in visibleFields)
                {
                    if (cancellationToken.IsCancellationRequested) return null;
                    
                    string value = GetParameterValueFast(elem, field);
                    scheduleRow.AddValue(field.GetName(), value);
                }
                
                return scheduleRow;
            }
            catch (Exception ex)
            {
                DebugLog($"Error creating schedule row for element {elem?.Id}: {ex.Message}");
                return null;
            }
        }

        // NEW: Super fast parameter value extraction
        private string GetParameterValueFast(Element elem, ScheduleField field)
        {
            try
            {
                Parameter param = GetParameterFromField(elem, field);
                if (param?.HasValue != true) return string.Empty;
                
                // Fast path for common parameter types
                switch (param.StorageType)
                {
                    case StorageType.String:
                        return param.AsString() ?? string.Empty;
                        
                    case StorageType.Integer:
                        return param.AsInteger().ToString();
                        
                    case StorageType.Double:
                        // Use AsValueString() first as it's faster than AsDouble().ToString()
                        var valueStr = param.AsValueString();
                        return !string.IsNullOrEmpty(valueStr) ? valueStr : param.AsDouble().ToString("F2");
                        
                    case StorageType.ElementId:
                        var elemId = param.AsElementId();
                        if (elemId == ElementId.InvalidElementId) return string.Empty;
                        
                        // Fast element name lookup with caching potential
                        try
                        {
                            var referencedElem = _doc.GetElement(elemId);
                            return referencedElem?.Name ?? elemId.IntegerValue.ToString();
                        }
                        catch
                        {
                            return elemId.IntegerValue.ToString();
                        }
                        
                    default:
                        return param.AsValueString() ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private void ExecuteAutofill(object parameter)
        {
            var selectedCells = parameter as IList;
            if (selectedCells == null) return;
            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            if (cellInfos.Count < 2) return;
            var firstCell = cellInfos.First();
            var firstCellColumn = firstCell.Column as DataGridBoundColumn;
            if (firstCellColumn == null) return;
            var bindingPath = (firstCellColumn.Binding as System.Windows.Data.Binding).Path.Path;
            string columnName = bindingPath.Trim('[', ']');
            var firstRow = firstCell.Item as ScheduleRow;
            if (firstRow == null) return;
            var valueToFill = firstRow[columnName];
            foreach (var cellInfo in cellInfos.Skip(1))
            {
                if (cellInfo.Item is ScheduleRow rowToFill)
                {
                    if (cellInfo.Column == firstCell.Column)
                    {
                        rowToFill[columnName] = valueToFill;
                    }
                }
            }
        }

        private bool CanExecuteAutofill(object parameter)
        {
            var selectedCells = parameter as IList;
            if (selectedCells == null || selectedCells.Count < 2)
                return false;
            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            var firstColumn = cellInfos.First().Column;
            return cellInfos.All(c => c.Column == firstColumn);
        }

        private bool CanUpdateModel(object obj) 
        {
            // Check ScheduleData instead of _allScheduleData since that's what's bound to DataGrid
            if (ScheduleData == null)
            {
                DebugLog("CanUpdateModel: ScheduleData is null");
                return false;
            }
            
            var allRows = ScheduleData.Cast<ScheduleRow>().ToList();
            var modifiedRows = allRows.Where(row => row.IsModified).ToList();
            var canUpdate = modifiedRows.Any();
            
            DebugLog($"CanUpdateModel: Found {modifiedRows.Count} modified rows out of {allRows.Count} total rows, can update: {canUpdate}");
            
            // Debug: List first few modified rows for troubleshooting
            if (modifiedRows.Any())
            {
                var firstModified = modifiedRows.Take(3);
                foreach (var row in firstModified)
                {
                    var modifiedValues = row.GetModifiedValues();
                    DebugLog($"CanUpdateModel: Modified row {row.Id} has {modifiedValues.Count} changed fields: {string.Join(", ", modifiedValues.Keys)}");
                }
            }
            
            return canUpdate;
        }
        
        /// <summary>
        /// Manually refresh the Update Model button state
        /// </summary>
        public void RefreshUpdateButtonState()
        {
            CommandManager.InvalidateRequerySuggested();
        }
        
        private void UpdateModel(object obj)
        {
            DebugLog("=== UpdateModel Started ===");
            
            try
            {
                using (var trans = new Transaction(_doc, "Update from Schedule Editor"))
                {
                    trans.Start();
                    DebugLog("Transaction started");
                    
                    int updatedCount = 0;
                    var allRows = ScheduleData?.Cast<ScheduleRow>().ToList() ?? new List<ScheduleRow>();
                    var changedRows = allRows.Where(row => row.IsModified).ToList();
                    DebugLog($"Found {changedRows.Count} rows with modifications");
                    
                    foreach (var row in changedRows)
                    {
                        Element elem = row.GetElement();
                        var modifiedValues = row.GetModifiedValues();
                        DebugLog($"Processing Element ID {elem.Id}: {modifiedValues.Count} modified parameters");
                        
                        foreach (var modifiedPair in modifiedValues)
                        {
                            Parameter param = elem.LookupParameter(modifiedPair.Key);
                            if (param != null && !param.IsReadOnly)
                            {
                                try
                                {
                                    switch (param.StorageType)
                                    {
                                        case StorageType.Integer:
                                            if (int.TryParse(modifiedPair.Value, out int intValue)) 
                                            {
                                                param.Set(intValue);
                                                DebugLog($"  Set Integer parameter '{modifiedPair.Key}' = {intValue}");
                                            }
                                            break;
                                        case StorageType.Double:
                                            param.SetValueString(modifiedPair.Value);
                                            DebugLog($"  Set Double parameter '{modifiedPair.Key}' = {modifiedPair.Value}");
                                            break;
                                        case StorageType.String:
                                            param.Set(modifiedPair.Value);
                                            DebugLog($"  Set String parameter '{modifiedPair.Key}' = {modifiedPair.Value}");
                                            break;
                                        case StorageType.ElementId:
                                            DebugLog($"  Skipped ElementId parameter '{modifiedPair.Key}'");
                                            break;
                                    }
                                    updatedCount++;
                                } 
                                catch (Exception paramEx) 
                                {
                                    DebugLog($"  ERROR setting parameter '{modifiedPair.Key}': {paramEx.Message}");
                                }
                            }
                            else
                            {
                                DebugLog($"  Skipped parameter '{modifiedPair.Key}' (null or read-only)");
                            }
                        }
                        row.AcceptChanges();
                    }
                    
                    trans.Commit();
                    DebugLog($"Transaction committed. Updated {updatedCount} parameters.");
                    ShowNonBlockingNotification($"Đã cập nhật {updatedCount} tham số.", "Thành công");
                }
                
                DebugLog("UpdateModel completed successfully");
            }
            catch (Exception ex)
            {
                DebugLog($"ERROR in UpdateModel: {ex.Message}\n{ex.StackTrace}");
                ShowNonBlockingNotification($"Lỗi khi cập nhật model: {ex.Message}", "Lỗi", 5);
            }
        }
        
        private Parameter GetParameterFromField(Element elem, ScheduleField field)
        {
            if (elem == null || field == null) return null;
            if (field.ParameterId.IntegerValue < 0)
            {
                return elem.get_Parameter((BuiltInParameter)field.ParameterId.IntegerValue);
            }
            return elem.LookupParameter(field.GetName());
        }

        #region Excel-like Features

        // Copy functionality - Enhanced for better Excel-like experience
        private void ExecuteCopy(object parameter)
        {
            var selectedCells = parameter as IList;
            if (selectedCells == null) return;
            
            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            if (cellInfos.Count == 0) return;

            DebugLog($"Copy: Processing {cellInfos.Count} selected cells");

            try
            {
                // Sort cells by row and column to maintain proper order
                var sortedCells = cellInfos
                    .Where(c => c.Item is ScheduleRow && c.Column is DataGridBoundColumn)
                    .OrderBy(c => ScheduleData.IndexOf((ScheduleRow)c.Item))
                    .ThenBy(c => GetColumnIndex(c.Column))
                    .ToList();

                if (!sortedCells.Any())
                {
                    DebugLog("Copy: No valid cells to copy");
                    return;
                }

                // Create clipboard data as tab-separated string (Excel format)
                var clipboardText = new StringBuilder();
                var lastRowIndex = -1;
                
                foreach (var cell in sortedCells)
                {
                    var row = (ScheduleRow)cell.Item;
                    var currentRowIndex = ScheduleData.IndexOf(row);
                    
                    // Add new line if we're on a different row
                    if (lastRowIndex != -1 && currentRowIndex != lastRowIndex)
                    {
                        clipboardText.AppendLine();
                    }
                    
                    // Add tab separator if not the first cell in the row
                    if (lastRowIndex == currentRowIndex && clipboardText.Length > 0 && 
                        !clipboardText.ToString().EndsWith("\r\n") && !clipboardText.ToString().EndsWith("\n"))
                    {
                        clipboardText.Append("\t");
                    }
                    
                    // Get cell value
                    if (cell.Column is DataGridBoundColumn boundCol)
                    {
                        var bindingPath = (boundCol.Binding as System.Windows.Data.Binding)?.Path.Path;
                        if (!string.IsNullOrEmpty(bindingPath))
                        {
                            string columnName = bindingPath.Trim('[', ']');
                            string cellValue = row[columnName] ?? string.Empty;
                            clipboardText.Append(cellValue);
                        }
                    }
                    
                    lastRowIndex = currentRowIndex;
                }

                // Copy to Windows clipboard
                var textToCopy = clipboardText.ToString();
                if (!string.IsNullOrEmpty(textToCopy))
                {
                    Clipboard.SetText(textToCopy);
                    DebugLog($"Copy: Copied text to clipboard: {textToCopy.Length} characters");
                    
                    // Also store in internal clipboard for paste functionality
                    _clipboardData = ParseClipboardText(textToCopy);
                }
            }
            catch (Exception ex)
            {
                DebugLog($"Copy error: {ex.Message}");
                ShowNonBlockingNotification($"Lỗi Copy: {ex.Message}", "Lỗi Copy", 5);
            }
        }

        // Helper method to parse clipboard text into 2D array
        private string[,] ParseClipboardText(string text)
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length == 0) return null;
            
            var maxCols = lines.Max(line => line.Split('\t').Length);
            var result = new string[lines.Length, maxCols];
            
            for (int row = 0; row < lines.Length; row++)
            {
                var cols = lines[row].Split('\t');
                for (int col = 0; col < maxCols; col++)
                {
                    result[row, col] = col < cols.Length ? cols[col] : string.Empty;
                }
            }
            
            return result;
        }

        private bool CanExecuteCopy(object parameter)
        {
            var selectedCells = parameter as IList;
            bool canCopy = selectedCells != null && selectedCells.Count > 0;
            DebugLog($"CanExecuteCopy - Selected cells: {selectedCells?.Count ?? 0}, Can copy: {canCopy}");
            return canCopy;
        }

        // Paste functionality - Enhanced to handle both internal and external clipboard
        private void ExecutePaste(object parameter)
        {
            var selectedCells = parameter as IList;
            if (selectedCells == null) return;
            
            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            if (cellInfos.Count == 0) return;

            DebugLog($"Paste: Processing {cellInfos.Count} selected cells");

            try
            {
                string[,] clipboardData = null;
                
                // Try to get data from Windows clipboard first
                if (Clipboard.ContainsText())
                {
                    var clipboardText = Clipboard.GetText();
                    clipboardData = ParseClipboardText(clipboardText);
                    DebugLog($"Paste: Got clipboard text: {clipboardText.Length} characters");
                }
                
                // Fall back to internal clipboard
                if (clipboardData == null)
                {
                    clipboardData = _clipboardData;
                }
                
                if (clipboardData == null) 
                {
                    DebugLog("Paste: No clipboard data available");
                    return;
                }

                SaveStateForUndo();

                // Find the top-left cell of selection
                var startCell = cellInfos
                    .OrderBy(c => ScheduleData.IndexOf((ScheduleRow)c.Item))
                    .ThenBy(c => GetColumnIndex(c.Column))
                    .First();

                if (!(startCell.Item is ScheduleRow startRow)) return;

                var startRowIndex = ScheduleData.IndexOf(startRow);
                var startColIndex = GetColumnIndex(startCell.Column);
                
                if (startRowIndex < 0 || startColIndex < 0) 
                {
                    DebugLog($"Paste: Invalid start position - row: {startRowIndex}, col: {startColIndex}");
                    return;
                }

                var clipRows = clipboardData.GetLength(0);
                var clipCols = clipboardData.GetLength(1);
                var pastedCount = 0;

                DebugLog($"Paste: Clipboard data size: {clipRows}x{clipCols}, starting at row {startRowIndex}, col {startColIndex}");

                // Paste data
                for (int r = 0; r < clipRows && startRowIndex + r < ScheduleData.Count; r++)
                {
                    for (int c = 0; c < clipCols; c++)
                    {
                        var targetRow = ScheduleData[startRowIndex + r];
                        var targetColName = GetColumnNameByIndex(startColIndex + c);
                        
                        if (!string.IsNullOrEmpty(targetColName))
                        {
                            var newValue = clipboardData[r, c];
                            targetRow[targetColName] = newValue;
                            pastedCount++;
                        }
                    }
                }
                
                DebugLog($"Paste: Successfully pasted {pastedCount} values");
                
                // Refresh Update Model button state after paste
                RefreshUpdateButtonState();
            }
            catch (Exception ex)
            {
                DebugLog($"Paste error: {ex.Message}");
                ShowNonBlockingNotification($"Lỗi Paste: {ex.Message}", "Lỗi Paste", 5);
            }
        }

        private bool CanExecutePaste(object parameter)
        {
            var selectedCells = parameter as IList;
            bool hasSelection = selectedCells != null && selectedCells.Count > 0;
            
            bool hasClipboardData = false;
            try
            {
                hasClipboardData = _clipboardData != null || Clipboard.ContainsText();
            }
            catch (Exception ex)
            {
                DebugLog($"CanExecutePaste - Clipboard access error: {ex.Message}");
                hasClipboardData = _clipboardData != null;
            }
            
            DebugLog($"CanExecutePaste - Selection: {hasSelection}, Clipboard: {hasClipboardData}");
            return hasSelection && hasClipboardData;
        }

        // Cut functionality
        private void ExecuteCut(object parameter)
        {
            ExecuteCopy(parameter);
            
            var selectedCells = parameter as IList;
            if (selectedCells == null) return;
            
            SaveStateForUndo();
            
            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            foreach (var cellInfo in cellInfos)
            {
                if (cellInfo.Item is ScheduleRow row && cellInfo.Column is DataGridBoundColumn boundCol)
                {
                    var bindingPath = (boundCol.Binding as System.Windows.Data.Binding)?.Path.Path;
                    if (!string.IsNullOrEmpty(bindingPath))
                    {
                        string columnName = bindingPath.Trim('[', ']');
                        row[columnName] = string.Empty;
                    }
                }
            }
        }

        private bool CanExecuteCut(object parameter)
        {
            return CanExecuteCopy(parameter);
        }

        // Fill Down functionality
        private void ExecuteFillDown(object parameter)
        {
            var selectedCells = parameter as IList;
            if (selectedCells == null) return;
            
            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            if (cellInfos.Count < 2) return;

            SaveStateForUndo();

            var columnGroups = cellInfos.GroupBy(c => c.Column);
            foreach (var columnGroup in columnGroups)
            {
                var cellsInColumn = columnGroup.OrderBy(c => ScheduleData.IndexOf(c.Item as ScheduleRow)).ToList();
                var firstCell = cellsInColumn.First();
                
                if (!(firstCell.Item is ScheduleRow firstRow) || !(firstCell.Column is DataGridBoundColumn boundCol)) 
                    continue;

                var bindingPath = (boundCol.Binding as System.Windows.Data.Binding)?.Path.Path;
                if (string.IsNullOrEmpty(bindingPath)) continue;

                string columnName = bindingPath.Trim('[', ']');
                string valueToFill = firstRow[columnName];

                foreach (var cell in cellsInColumn.Skip(1))
                {
                    if (cell.Item is ScheduleRow targetRow)
                    {
                        targetRow[columnName] = valueToFill;
                    }
                }
            }
        }

        private bool CanExecuteFillDown(object parameter)
        {
            var selectedCells = parameter as IList;
            return selectedCells != null && selectedCells.Count > 1;
        }

        // Fill Right functionality
        private void ExecuteFillRight(object parameter)
        {
            var selectedCells = parameter as IList;
            if (selectedCells == null) return;
            
            var cellInfos = selectedCells.Cast<DataGridCellInfo>().ToList();
            if (cellInfos.Count < 2) return;

            SaveStateForUndo();

            var rowGroups = cellInfos.GroupBy(c => c.Item);
            foreach (var rowGroup in rowGroups)
            {
                var cellsInRow = rowGroup.OrderBy(c => GetColumnIndex(c.Column)).ToList();
                var firstCell = cellsInRow.First();
                
                if (!(firstCell.Item is ScheduleRow row) || !(firstCell.Column is DataGridBoundColumn boundCol)) 
                    continue;

                var bindingPath = (boundCol.Binding as System.Windows.Data.Binding)?.Path.Path;
                if (string.IsNullOrEmpty(bindingPath)) continue;

                string firstColumnName = bindingPath.Trim('[', ']');
                string valueToFill = row[firstColumnName];

                foreach (var cell in cellsInRow.Skip(1))
                {
                    if (cell.Column is DataGridBoundColumn targetBoundCol)
                    {
                        var targetBindingPath = (targetBoundCol.Binding as System.Windows.Data.Binding)?.Path.Path;
                        if (!string.IsNullOrEmpty(targetBindingPath))
                        {
                            string targetColumnName = targetBindingPath.Trim('[', ']');
                            row[targetColumnName] = valueToFill;
                        }
                    }
                }
            }
        }

        private bool CanExecuteFillRight(object parameter)
        {
            var selectedCells = parameter as IList;
            return selectedCells != null && selectedCells.Count > 1;
        }

        // Undo/Redo functionality
        public void SaveStateForUndo()
        {
            var state = new Dictionary<string, string>();
            for (int i = 0; i < ScheduleData.Count; i++)
            {
                var row = ScheduleData[i];
                foreach (var kvp in row.Values)
                {
                    state[$"{i}_{kvp.Key}"] = kvp.Value;
                }
            }
            
            _undoHistory.Add(state);
            if (_undoHistory.Count > MaxHistorySize)
            {
                _undoHistory.RemoveAt(0);
            }
            
            _redoHistory.Clear(); // Clear redo history when new action is performed
        }

        private void ExecuteUndo(object parameter)
        {
            if (_undoHistory.Count == 0) return;

            var currentState = new Dictionary<string, string>();
            for (int i = 0; i < ScheduleData.Count; i++)
            {
                var row = ScheduleData[i];
                foreach (var kvp in row.Values)
                {
                    currentState[$"{i}_{kvp.Key}"] = kvp.Value;
                }
            }
            _redoHistory.Add(currentState);

            var previousState = _undoHistory.Last();
            _undoHistory.RemoveAt(_undoHistory.Count - 1);

            RestoreState(previousState);
        }

        private bool CanExecuteUndo(object parameter)
        {
            return _undoHistory.Count > 0;
        }

        private void ExecuteRedo(object parameter)
        {
            if (_redoHistory.Count == 0) return;

            SaveStateForUndo();

            var redoState = _redoHistory.Last();
            _redoHistory.RemoveAt(_redoHistory.Count - 1);

            RestoreState(redoState);
        }

        private bool CanExecuteRedo(object parameter)
        {
            return _redoHistory.Count > 0;
        }

        private void RestoreState(Dictionary<string, string> state)
        {
            foreach (var kvp in state)
            {
                var parts = kvp.Key.Split('_');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int rowIndex))
                {
                    if (rowIndex < ScheduleData.Count)
                    {
                        string columnName = string.Join("_", parts.Skip(1));
                        ScheduleData[rowIndex][columnName] = kvp.Value;
                    }
                }
            }
        }

        // Helper methods
        private int GetColumnIndex(DataGridColumn column)
        {
            if (SelectedSchedule == null) return -1;
            
            var visibleFields = SelectedSchedule.Definition.GetFieldOrder()
                .Select(id => SelectedSchedule.Definition.GetField(id))
                .Where(f => !f.IsHidden).ToList();

            if (column is DataGridBoundColumn boundCol)
            {
                var bindingPath = (boundCol.Binding as System.Windows.Data.Binding)?.Path.Path;
                if (!string.IsNullOrEmpty(bindingPath))
                {
                    string columnName = bindingPath.Trim('[', ']');
                    for (int i = 0; i < visibleFields.Count; i++)
                    {
                        if (visibleFields[i].GetName() == columnName)
                            return i;
                    }
                }
            }
            return -1;
        }

        private string GetColumnNameByIndex(int columnIndex)
        {
            if (SelectedSchedule == null) return null;
            
            var visibleFields = SelectedSchedule.Definition.GetFieldOrder()
                .Select(id => SelectedSchedule.Definition.GetField(id))
                .Where(f => !f.IsHidden).ToList();

            if (columnIndex >= 0 && columnIndex < visibleFields.Count)
            {
                return visibleFields[columnIndex].GetName();
            }
            return null;
        }

        #region Excel Import/Export Implementation

        private void ImportFromExcel(string filePath)
        {
            try
            {
                // Simple CSV import implementation
                var lines = System.IO.File.ReadAllLines(filePath);
                if (lines.Length < 2)
                {
                    MessageBox.Show("File must contain header and at least one data row.", "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var headers = lines[0].Split(',');
                var elementIdIndex = Array.FindIndex(headers, h => h.Trim().Equals("Element ID", StringComparison.OrdinalIgnoreCase));
                
                if (elementIdIndex == -1)
                {
                    MessageBox.Show("File must contain 'Element ID' column.", "Missing Element ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int updatedCount = 0;
                SaveStateForUndo(); // Save state before import

                for (int i = 1; i < lines.Length; i++)
                {
                    var values = lines[i].Split(',');
                    if (values.Length <= elementIdIndex) continue;

                    var elementIdStr = values[elementIdIndex].Trim();
                    if (!long.TryParse(elementIdStr, out long elementId)) continue;

                    // Find matching row
                    var scheduleRow = ScheduleData.FirstOrDefault(row => row.GetElement().Id.IntegerValue == elementId);
                    if (scheduleRow == null) continue;

                    // Update values
                    for (int j = 0; j < headers.Length && j < values.Length; j++)
                    {
                        if (j == elementIdIndex) continue; // Skip Element ID column

                        var columnName = headers[j].Trim();
                        var newValue = values[j].Trim();
                        
                        if (scheduleRow.Values.ContainsKey(columnName))
                        {
                            scheduleRow[columnName] = newValue;
                        }
                    }
                    updatedCount++;
                }

                MessageBox.Show($"Successfully imported data for {updatedCount} elements.", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading file: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToExcel(string filePath)
        {
            try
            {
                // Get visible fields for headers
                var visibleFields = SelectedSchedule.Definition.GetFieldOrder()
                    .Select(id => SelectedSchedule.Definition.GetField(id))
                    .Where(f => !f.IsHidden).ToList();

                // Create Excel content using XML format (Excel 2003 XML)
                var excelContent = CreateExcelXmlContent(visibleFields);
                
                // Change extension to .xls for XML format compatibility
                var excelPath = System.IO.Path.ChangeExtension(filePath, ".xls");
                System.IO.File.WriteAllText(excelPath, excelContent, System.Text.Encoding.UTF8);
                
                DebugLog($"Excel file exported to: {excelPath}");
            }
            catch (Exception ex)
            {
                DebugLog($"Error writing Excel file: {ex.Message}");
                throw;
            }
        }

        private string CreateExcelXmlContent(List<Autodesk.Revit.DB.ScheduleField> visibleFields)
        {
            var xml = new System.Text.StringBuilder();
            
            // XML Header
            xml.AppendLine("<?xml version=\"1.0\"?>");
            xml.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
            xml.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
            xml.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
            xml.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
            xml.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"");
            xml.AppendLine(" xmlns:html=\"http://www.w3.org/TR/REC-html40\">");
            
            // Document Properties
            xml.AppendLine("<DocumentProperties xmlns=\"urn:schemas-microsoft-com:office:office\">");
            xml.AppendLine($"<Title>{SelectedSchedule?.Name ?? "Schedule"} Export</Title>");
            xml.AppendLine("<Author>Revit Schedule Editor</Author>");
            xml.AppendLine($"<Created>{DateTime.Now:yyyy-MM-ddTHH:mm:ssZ}</Created>");
            xml.AppendLine("</DocumentProperties>");
            
            // Styles
            xml.AppendLine("<Styles>");
            
            // Header style
            xml.AppendLine("<Style ss:ID=\"HeaderStyle\">");
            xml.AppendLine("<Font ss:Bold=\"1\" ss:Size=\"12\" ss:Color=\"#FFFFFF\"/>");
            xml.AppendLine("<Interior ss:Color=\"#4472C4\" ss:Pattern=\"Solid\"/>");
            xml.AppendLine("<Borders>");
            xml.AppendLine("<Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/>");
            xml.AppendLine("<Border ss:Position=\"Left\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/>");
            xml.AppendLine("<Border ss:Position=\"Right\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/>");
            xml.AppendLine("<Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/>");
            xml.AppendLine("</Borders>");
            xml.AppendLine("<Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\"/>");
            xml.AppendLine("</Style>");
            
            // Data style - even rows
            xml.AppendLine("<Style ss:ID=\"DataEven\">");
            xml.AppendLine("<Font ss:Size=\"10\"/>");
            xml.AppendLine("<Interior ss:Color=\"#F8F9FA\" ss:Pattern=\"Solid\"/>");
            xml.AppendLine("<Borders>");
            xml.AppendLine("<Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E0E0E0\"/>");
            xml.AppendLine("<Border ss:Position=\"Left\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E0E0E0\"/>");
            xml.AppendLine("<Border ss:Position=\"Right\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E0E0E0\"/>");
            xml.AppendLine("<Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E0E0E0\"/>");
            xml.AppendLine("</Borders>");
            xml.AppendLine("</Style>");
            
            // Data style - odd rows
            xml.AppendLine("<Style ss:ID=\"DataOdd\">");
            xml.AppendLine("<Font ss:Size=\"10\"/>");
            xml.AppendLine("<Interior ss:Color=\"#FFFFFF\" ss:Pattern=\"Solid\"/>");
            xml.AppendLine("<Borders>");
            xml.AppendLine("<Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E0E0E0\"/>");
            xml.AppendLine("<Border ss:Position=\"Left\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E0E0E0\"/>");
            xml.AppendLine("<Border ss:Position=\"Right\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E0E0E0\"/>");
            xml.AppendLine("<Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#E0E0E0\"/>");
            xml.AppendLine("</Borders>");
            xml.AppendLine("</Style>");
            
            // Number style
            xml.AppendLine("<Style ss:ID=\"NumberStyle\">");
            xml.AppendLine("<NumberFormat ss:Format=\"#,##0.00\"/>");
            xml.AppendLine("</Style>");
            
            xml.AppendLine("</Styles>");
            
            // Worksheet
            xml.AppendLine($"<Worksheet ss:Name=\"{SelectedSchedule?.Name ?? "Schedule"}\">");
            xml.AppendLine("<Table>");
            
            // Define column widths
            xml.AppendLine("<Column ss:Width=\"80\"/>"); // Element ID
            foreach (var field in visibleFields)
            {
                var width = EstimateColumnWidth(field.GetName());
                xml.AppendLine($"<Column ss:Width=\"{width}\"/>");
            }
            
            // Header row
            xml.AppendLine("<Row ss:Height=\"25\">");
            xml.AppendLine("<Cell ss:StyleID=\"HeaderStyle\"><Data ss:Type=\"String\">Element ID</Data></Cell>");
            foreach (var field in visibleFields)
            {
                var fieldName = System.Security.SecurityElement.Escape(field.GetName());
                xml.AppendLine($"<Cell ss:StyleID=\"HeaderStyle\"><Data ss:Type=\"String\">{fieldName}</Data></Cell>");
            }
            xml.AppendLine("</Row>");
            
            // Data rows
            var rowIndex = 0;
            foreach (var row in ScheduleData)
            {
                var styleId = (rowIndex % 2 == 0) ? "DataEven" : "DataOdd";
                xml.AppendLine($"<Row ss:Height=\"20\">");
                
                // Element ID
                xml.AppendLine($"<Cell ss:StyleID=\"{styleId}\"><Data ss:Type=\"Number\">{row.GetElement().Id.IntegerValue}</Data></Cell>");
                
                // Field values
                foreach (var field in visibleFields)
                {
                    var fieldName = field.GetName();
                    var value = row.Values.ContainsKey(fieldName) ? row.Values[fieldName] : "";
                    var escapedValue = System.Security.SecurityElement.Escape(value);
                    
                    // Determine data type
                    if (IsNumericField(value))
                    {
                        xml.AppendLine($"<Cell ss:StyleID=\"{styleId}\"><Data ss:Type=\"Number\">{value}</Data></Cell>");
                    }
                    else
                    {
                        xml.AppendLine($"<Cell ss:StyleID=\"{styleId}\"><Data ss:Type=\"String\">{escapedValue}</Data></Cell>");
                    }
                }
                
                xml.AppendLine("</Row>");
                rowIndex++;
            }
            
            xml.AppendLine("</Table>");
            
            // Auto-filter
            xml.AppendLine("<AutoFilter x:Range=\"R1C1:R1C" + (visibleFields.Count + 1) + "\" xmlns=\"urn:schemas-microsoft-com:office:excel\"/>");
            
            xml.AppendLine("</Worksheet>");
            xml.AppendLine("</Workbook>");
            
            return xml.ToString();
        }
        
        private int EstimateColumnWidth(string headerText)
        {
            // Estimate column width based on header text length
            var baseWidth = Math.Max(headerText.Length * 8, 80);
            return Math.Min(baseWidth, 200); // Cap at 200
        }
        
        private bool IsNumericField(string value)
        {
            return double.TryParse(value, out _) || int.TryParse(value, out _);
        }

        // Import Command
        private void ExecuteImport(object parameter)
        {
            try
            {
                // Use non-blocking approach for file dialogs
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    Title = "Chọn file CSV hoặc Excel để import"
                };

                // Show dialog on main thread without blocking
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var result = openFileDialog.ShowDialog();
                        if (result == true)
                        {
                            ImportFromExcel(openFileDialog.FileName);
                            ShowNonBlockingNotification("Import thành công!", "Import hoàn tất");
                        }
                    }
                    catch (Exception dialogEx)
                    {
                        ShowNonBlockingNotification($"Lỗi import dữ liệu: {dialogEx.Message}", "Lỗi Import", 5);
                    }
                }));
            }
            catch (Exception ex)
            {
                ShowNonBlockingNotification($"Lỗi import dữ liệu: {ex.Message}", "Lỗi Import", 5);
            }
        }

        private bool CanExecuteImport(object parameter)
        {
            return SelectedSchedule != null && ScheduleData != null && ScheduleData.Count > 0;
        }

        // Export Command
        private void ExecuteExport(object parameter)
        {
            try
            {
                if (ScheduleData == null || ScheduleData.Count == 0)
                {
                    ShowNonBlockingNotification("Không có dữ liệu để export.", "Không có dữ liệu", 3);
                    return;
                }

                // Use non-blocking approach for file dialogs
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx",
                    Title = "Lưu file Excel",
                    FileName = $"{SelectedSchedule?.Name ?? "Schedule"}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                // Show dialog on main thread without blocking
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var result = saveFileDialog.ShowDialog();
                        if (result == true)
                        {
                            ExportToExcel(saveFileDialog.FileName);
                            ShowNonBlockingNotification("Export dữ liệu thành công!", "Export hoàn tất");
                        }
                    }
                    catch (Exception dialogEx)
                    {
                        ShowNonBlockingNotification($"Lỗi export dữ liệu: {dialogEx.Message}", "Lỗi Export", 5);
                    }
                }));
            }
            catch (Exception ex)
            {
                ShowNonBlockingNotification($"Lỗi export dữ liệu: {ex.Message}", "Lỗi Export", 5);
            }
        }

        private bool CanExecuteExport(object parameter)
        {
            return ScheduleData != null && ScheduleData.Count > 0;
        }

        #endregion
        
        #region Clear All Filters Command
        
        // Clear All Filters Command - works with Window to clear filters
        private void ExecuteClearAllFilters(object parameter)
        {
            DebugLog("ExecuteClearAllFilters - Command executed from ViewModel");
            
            try
            {
                // Find the window and call its clear filters method
                var window = Application.Current.Windows
                    .OfType<ScheduleEditorWindow>()
                    .FirstOrDefault(w => w.DataContext == this);
                
                if (window != null)
                {
                    // Use reflection to call the private method or make it public
                    var methodInfo = typeof(ScheduleEditorWindow)
                        .GetMethod("ClearAllFiltersButton_Click", 
                                  System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (methodInfo != null)
                    {
                        methodInfo.Invoke(window, new object[] { null, null });
                        DebugLog("ExecuteClearAllFilters - Successfully called window method via reflection");
                    }
                    else
                    {
                        DebugLog("ExecuteClearAllFilters - Could not find ClearAllFiltersButton_Click method");
                    }
                }
                else
                {
                    DebugLog("ExecuteClearAllFilters - Could not find ScheduleEditorWindow");
                }
            }
            catch (Exception ex)
            {
                DebugLog($"ExecuteClearAllFilters - Error: {ex.Message}");
            }
        }
        
        private bool CanExecuteClearAllFilters(object parameter)
        {
            // This command is always available if we have data loaded
            return ScheduleData != null && ScheduleData.Count > 0;
        }

        #endregion

        #region Select/Show Highlighted Elements Commands
        
        // Select Highlighted Elements Command - chọn element từ dòng tại vị trí chuột hoặc dòng selected
        private void ExecuteSelectHighlightedElements(object parameter)
        {
            DebugLog("ExecuteSelectHighlightedElements - Starting command execution");
            
            try
            {
                List<ScheduleRow> targetRows = new List<ScheduleRow>();

                // Cách 1 (Thông minh): Sử dụng ScheduleRow từ parameter (dòng tại vị trí chuột)
                if (parameter is ScheduleRow singleRow)
                {
                    targetRows.Add(singleRow);
                    DebugLog($"ExecuteSelectHighlightedElements - Using single row from mouse position: ID {singleRow.Id?.IntegerValue ?? -1}");
                }
                else
                {
                    // Cách 2 (Fallback): Sử dụng selected rows từ DataGrid
                    DebugLog("ExecuteSelectHighlightedElements - No parameter row, falling back to selected rows");
                    
                    ScheduleEditorWindow window = _parentWindow;
                    
                    if (window == null)
                    {
                        DebugLog("ExecuteSelectHighlightedElements - _parentWindow is null, searching through Application windows");
                        
                        try
                        {
                            foreach (Window openWindow in System.Windows.Application.Current?.Windows ?? new WindowCollection())
                            {
                                if (openWindow is ScheduleEditorWindow scheduleWindow && scheduleWindow.DataContext == this)
                                {
                                    window = scheduleWindow;
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugLog($"ExecuteSelectHighlightedElements - Error searching windows: {ex.Message}");
                        }
                    }
                    
                    if (window?.ScheduleDataGrid?.SelectedItems != null)
                    {
                        targetRows = window.ScheduleDataGrid.SelectedItems.OfType<ScheduleRow>().ToList();
                        DebugLog($"ExecuteSelectHighlightedElements - Found {targetRows.Count} selected rows from DataGrid");
                    }
                    else
                    {
                        DebugLog("ExecuteSelectHighlightedElements - Could not find window or selected items");
                    }
                }

                if (targetRows.Count > 0)
                {
                    // Lấy danh sách ElementId từ các dòng
                    var elementIds = targetRows
                        .Select(row => row?.Id)
                        .Where(id => id != null && id != ElementId.InvalidElementId)
                        .ToList();
                        
                    if (elementIds.Count > 0)
                    {
                        // Chuyển đổi sang List<ElementId> cho Revit API
                        var elementIdsList = new System.Collections.Generic.List<ElementId>(elementIds);
                        
                        // Chọn các element trong Revit
                        var uidoc = new UIDocument(_doc);
                        uidoc.Selection.SetElementIds(elementIdsList);
                        
                        DebugLog($"ExecuteSelectHighlightedElements - Selected {elementIds.Count} elements in Revit");
                        
                        // Tạo non-blocking notification window
                        var notificationWindow = new Window
                        {
                            Title = "🎯 Select Elements - Success",
                            Width = 400,
                            Height = 180,
                            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                            ResizeMode = ResizeMode.NoResize,
                            Topmost = true,
                            ShowInTaskbar = false,
                            Content = new System.Windows.Controls.StackPanel
                            {
                                Margin = new System.Windows.Thickness(20),
                                Children = 
                                {
                                    new System.Windows.Controls.TextBlock
                                    {
                                        Text = $"🎯 Đã chọn {elementIds.Count} element(s) trong Revit model!",
                                        FontSize = 14,
                                        FontWeight = System.Windows.FontWeights.Bold,
                                        Margin = new System.Windows.Thickness(0, 0, 0, 10),
                                        TextWrapping = System.Windows.TextWrapping.Wrap
                                    },
                                    new System.Windows.Controls.TextBlock
                                    {
                                        Text = $"Element IDs: {string.Join(", ", elementIds.Select(id => id.IntegerValue))}",
                                        FontSize = 11,
                                        Foreground = System.Windows.Media.Brushes.Gray,
                                        TextWrapping = System.Windows.TextWrapping.Wrap
                                    }
                                }
                            }
                        };
                        
                        // Hiển thị non-modal và auto-close sau 3 giây
                        notificationWindow.Show();
                        
                        // Auto close after 3 seconds
                        var timer = new System.Windows.Threading.DispatcherTimer();
                        timer.Interval = TimeSpan.FromSeconds(3);
                        timer.Tick += (sender, e) => 
                        {
                            timer.Stop();
                            if (notificationWindow.IsVisible)
                            {
                                notificationWindow.Close();
                            }
                        };
                        timer.Start();
                    }
                    else
                    {
                        DebugLog("ExecuteSelectHighlightedElements - No valid ElementIds found");
                        System.Windows.MessageBox.Show(
                            "Không tìm thấy ElementId hợp lệ trong dòng được chọn.", 
                            "Select Highlighted Elements", 
                            System.Windows.MessageBoxButton.OK, 
                            System.Windows.MessageBoxImage.Warning);
                    }
                }
                else
                {
                    DebugLog("ExecuteSelectHighlightedElements - No rows found");
                    System.Windows.MessageBox.Show(
                        "Không tìm thấy dòng nào để xử lý.\nVui lòng nhấp chuột phải trên dòng có dữ liệu.", 
                        "Select Highlighted Elements", 
                        System.Windows.MessageBoxButton.OK, 
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                DebugLog($"ExecuteSelectHighlightedElements - Error: {ex.Message}\n{ex.StackTrace}");
                System.Windows.MessageBox.Show($"Lỗi khi chọn elements: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        
        private bool CanExecuteSelectHighlightedElements(object parameter)
        {
            // Always enable the menu item - we'll check for selection in the Execute method
            return true;
        }

        // Show Highlighted Elements Command - hiển thị thông tin element từ dòng tại vị trí chuột hoặc dòng selected
        private void ExecuteShowHighlightedElements(object parameter)
        {
            DebugLog("ExecuteShowHighlightedElements - Starting command execution");
            
            try
            {
                List<ScheduleRow> targetRows = new List<ScheduleRow>();

                // Cách 1 (Thông minh): Sử dụng ScheduleRow từ parameter (dòng tại vị trí chuột)
                if (parameter is ScheduleRow singleRow)
                {
                    targetRows.Add(singleRow);
                    DebugLog($"ExecuteShowHighlightedElements - Using single row from mouse position: ID {singleRow.Id?.IntegerValue ?? -1}");
                }
                else
                {
                    // Cách 2 (Fallback): Sử dụng selected rows từ DataGrid
                    DebugLog("ExecuteShowHighlightedElements - No parameter row, falling back to selected rows");
                    
                    ScheduleEditorWindow window = _parentWindow;
                    
                    if (window == null)
                    {
                        DebugLog("ExecuteShowHighlightedElements - _parentWindow is null, searching through Application windows");
                        
                        try
                        {
                            foreach (Window openWindow in System.Windows.Application.Current?.Windows ?? new WindowCollection())
                            {
                                if (openWindow is ScheduleEditorWindow scheduleWindow && scheduleWindow.DataContext == this)
                                {
                                    window = scheduleWindow;
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugLog($"ExecuteShowHighlightedElements - Error searching windows: {ex.Message}");
                        }
                    }
                    
                    if (window?.ScheduleDataGrid?.SelectedItems != null)
                    {
                        targetRows = window.ScheduleDataGrid.SelectedItems.OfType<ScheduleRow>().ToList();
                        DebugLog($"ExecuteShowHighlightedElements - Found {targetRows.Count} selected rows from DataGrid");
                    }
                    else
                    {
                        DebugLog("ExecuteShowHighlightedElements - Could not find window or selected items");
                    }
                }

                if (targetRows.Count > 0)
                {
                    var elementIds = targetRows
                        .Select(row => row?.Id)
                        .Where(id => id != null && id != ElementId.InvalidElementId)
                        .ToList();
                    
                    if (elementIds.Count > 0)
                    {
                        // Tạo thông tin chi tiết về các element
                        var elementInfos = new List<string>();
                        
                        foreach (var elementId in elementIds)
                        {
                            try
                            {
                                var element = _doc.GetElement(elementId);
                                if (element != null)
                                {
                                    var elementInfo = $"🔍 Element ID: {elementId.IntegerValue}\n" +
                                                    $"📝 Name: {element.Name ?? "N/A"}\n" +
                                                    $"📂 Category: {element.Category?.Name ?? "N/A"}\n" +
                                                    $"🏷️ Type: {element.GetType().Name}\n";
                                    
                                    // Thêm thông tin parameters quan trọng
                                    var parameters = element.GetOrderedParameters();
                                    if (parameters.Count > 0)
                                    {
                                        elementInfo += "🔧 Key Parameters:\n";
                                        var keyParams = parameters
                                            .Where(p => p.HasValue && !string.IsNullOrEmpty(p.AsString()))
                                            .Take(5) // Lấy 5 parameter đầu
                                            .Select(p => $"  • {p.Definition.Name}: {p.AsString()}")
                                            .ToArray();
                                        
                                        if (keyParams.Length > 0)
                                        {
                                            elementInfo += string.Join("\n", keyParams);
                                        }
                                        else
                                        {
                                            elementInfo += "  • No parameters with values";
                                        }
                                    }
                                    
                                    elementInfos.Add(elementInfo);
                                }
                            }
                            catch (Exception ex)
                            {
                                elementInfos.Add($"❌ ID: {elementId.IntegerValue} - Error: {ex.Message}");
                            }
                        }
                        
                        var fullMessage = $"👁️ Thông tin chi tiết {elementIds.Count} element(s):\n\n" + 
                                        string.Join("\n" + new string('═', 60) + "\n", elementInfos);
                        
                        DebugLog($"ExecuteShowHighlightedElements - Showing info for {elementIds.Count} elements");
                        
                        // Hiển thị window async để hoàn toàn không block Revit
                        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var infoWindow = new Window
                            {
                                Title = "👁️ Show Highlighted Elements Info",
                                Width = 600,
                                Height = 500,
                                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                                // Bỏ Owner để tránh lock trong Revit add-in
                                ResizeMode = ResizeMode.CanResize,
                                Topmost = true, // Giữ window trên cùng
                                ShowInTaskbar = true, // Hiển thị trong taskbar
                                Content = new ScrollViewer
                                {
                                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                                    HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                                    Content = new System.Windows.Controls.TextBlock
                                    {
                                        Text = fullMessage,
                                        Margin = new System.Windows.Thickness(15),
                                        TextWrapping = System.Windows.TextWrapping.Wrap,
                                        FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New"),
                                        FontSize = 12
                                    }
                                }
                            };
                            
                            // Show window non-blocking
                            infoWindow.Show();
                            
                            // Đưa window lên foreground
                            infoWindow.Activate();
                        }));
                    }
                    else
                    {
                        DebugLog("ExecuteShowHighlightedElements - No valid ElementIds found");
                        ShowNonBlockingNotification(
                            "Không tìm thấy ElementId hợp lệ trong dòng được chọn.", 
                            "Show Highlighted Elements", 3);
                    }
                }
                else
                {
                    DebugLog("ExecuteShowHighlightedElements - No rows found");
                    ShowNonBlockingNotification(
                        "Không tìm thấy dòng nào để xử lý.\nVui lòng nhấp chuột phải trên dòng có dữ liệu.", 
                        "Show Highlighted Elements", 3);
                }
            }
            catch (Exception ex)
            {
                DebugLog($"ExecuteShowHighlightedElements - Error: {ex.Message}");
                ShowNonBlockingNotification(
                    $"Lỗi khi hiển thị thông tin elements: {ex.Message}", 
                    "Lỗi", 5);
            }
        }
        
        private bool CanExecuteShowHighlightedElements(object parameter)
        {
            // Always enable the menu item - we'll check for data/selection in the Execute method
            return true;
        }

        #endregion

        #endregion
    }
}
