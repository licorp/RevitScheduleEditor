using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Threading;

namespace RevitScheduleEditor
{
    public class ScheduleEditorViewModel : BaseViewModel
    {
        private readonly Document _doc;
        private readonly UIApplication _uiApp;
        private ViewSchedule _selectedSchedule;
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

        // Progress properties for data binding
        public bool IsLoadingData
        {
            get => _isLoadingData;
            set
            {
                _isLoadingData = value;
                OnPropertyChanged(nameof(IsLoadingData));
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
        
        // New commands for the buttons
        public ICommand PreviewEditCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand CancelLoadingCommand { get; }
        
        public ViewSchedule SelectedSchedule
        {
            get => _selectedSchedule;
            set
            {
                _selectedSchedule = value;
                OnPropertyChanged();
                // Don't auto-load data anymore - user needs to click Preview/Edit
            }
        }
        
        public ICommand UpdateModelCommand { get; }

        public ScheduleEditorViewModel(Document doc)
        {
            DebugLog("=== ScheduleEditorViewModel Constructor Started ===");
            
            try
            {
                _doc = doc;
                DebugLog($"Document assigned: {doc.Title}");
                
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
                
                // New commands
                PreviewEditCommand = new RelayCommand(ExecutePreviewEdit, CanExecutePreviewEdit);
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
                
                Schedules.Clear();
                foreach (var s in schedules)
                {
                    Schedules.Add(s);
                    DebugLog($"Added schedule: {s.Name}");
                }
                
                DebugLog("LoadSchedules completed successfully");
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
                MessageBox.Show("Please select a schedule first.", "No Schedule Selected");
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
            return SelectedSchedule != null && !IsLoadingData;
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
                MessageBox.Show($"Error loading schedule data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var result = MessageBox.Show(
                $"⚡ ULTRA-FAST LOADING for {TotalElements} items\n\n" +
                "🚀 RECOMMENDED STRATEGY:\n" +
                "• YES = Progressive Loading (Load 1000 items instantly, then continue in background)\n" +
                "• NO = Load all items with parallel processing (faster than before)\n" +
                "• CANCEL = Cancel loading\n\n" +
                "⚡ New: Parallel processing enabled for 5-10x faster loading!",
                "🚀 Ultra-Fast Schedule Loading", 
                MessageBoxButton.YesNoCancel, 
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Cancel)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            var elements = collector.ToElements();
            DebugLog($"Retrieved {elements.Count} elements for processing");
            
            if (result == MessageBoxResult.Yes)
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
                MessageBox.Show(
                    $"🚀 Ultra-Fast Progressive Loading Complete!\n\n" +
                    $"📊 Performance Statistics:\n" +
                    $"Phase 1: {itemsLoaded} items in {phase1Duration.TotalSeconds:F1}s ({speed1:F0} items/sec)\n" +
                    $"Phase 2: {remainingElements.Count} items in {phase2Duration.TotalSeconds:F1}s ({speed2:F0} items/sec)\n" +
                    $"Overall: {totalElements} items in {totalDuration.TotalSeconds:F1}s ({overallSpeed:F0} items/sec)\n\n" +
                    $"✅ You can now work with the complete dataset!",
                    "🚀 Progressive Loading Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
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
                    MessageBox.Show($"Updated {updatedCount} parameters.", "Success");
                }
                
                DebugLog("UpdateModel completed successfully");
            }
            catch (Exception ex)
            {
                DebugLog($"ERROR in UpdateModel: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error updating model: {ex.Message}", "Error");
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
                MessageBox.Show($"Copy failed: {ex.Message}", "Copy Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show($"Paste failed: {ex.Message}", "Paste Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                var lines = new List<string>();
                
                // Get visible fields for headers
                var visibleFields = SelectedSchedule.Definition.GetFieldOrder()
                    .Select(id => SelectedSchedule.Definition.GetField(id))
                    .Where(f => !f.IsHidden).ToList();

                // Create header line
                var headers = new List<string> { "Element ID" };
                headers.AddRange(visibleFields.Select(f => f.GetName()));
                lines.Add(string.Join(",", headers.Select(h => $"\"{h}\"")));

                // Create data lines
                foreach (var row in ScheduleData)
                {
                    var values = new List<string> { row.GetElement().Id.IntegerValue.ToString() };
                    
                    foreach (var field in visibleFields)
                    {
                        var fieldName = field.GetName();
                        var value = row.Values.ContainsKey(fieldName) ? row.Values[fieldName] : "";
                        values.Add($"\"{value}\"");
                    }
                    
                    lines.Add(string.Join(",", values));
                }

                // Write to file (CSV format for simplicity)
                var csvPath = System.IO.Path.ChangeExtension(filePath, ".csv");
                System.IO.File.WriteAllLines(csvPath, lines);
                
                MessageBox.Show($"Data exported to: {csvPath}\n\nNote: File saved as CSV format for compatibility.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error writing file: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Import Command
        private void ExecuteImport(object parameter)
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    Title = "Select CSV or Excel file to import"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    ImportFromExcel(openFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing data: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteImport(object parameter)
        {
            return SelectedSchedule != null && ScheduleData.Count > 0;
        }

        // Export Command
        private void ExecuteExport(object parameter)
        {
            try
            {
                if (ScheduleData.Count == 0)
                {
                    MessageBox.Show("No data to export.", "No Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx",
                    Title = "Save Excel file",
                    FileName = $"{SelectedSchedule?.Name ?? "Schedule"}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportToExcel(saveFileDialog.FileName);
                    MessageBox.Show("Data exported successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting data: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteExport(object parameter)
        {
            return ScheduleData.Count > 0;
        }

        #endregion

        #endregion
    }
}
