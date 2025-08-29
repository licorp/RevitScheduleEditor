using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;

namespace RevitScheduleEditor
{
    public partial class ScheduleSelector : Window, INotifyPropertyChanged
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern void OutputDebugStringA(string message);

        private void DebugLog(string message)
        {
            var logMessage = $"[{System.Diagnostics.Process.GetCurrentProcess().Id}] [ScheduleSelector] {DateTime.Now:HH:mm:ss.fff} - {message}";
            OutputDebugStringA(logMessage);
        }

        private Document _document;
        private ObservableCollection<ScheduleInfo> _allSchedules;
        private ObservableCollection<ScheduleInfo> _filteredSchedules;
        private ScheduleInfo _selectedSchedule;
        private string _searchText = "";

        public ObservableCollection<ScheduleInfo> FilteredSchedules
        {
            get => _filteredSchedules;
            set
            {
                _filteredSchedules = value;
                OnPropertyChanged(nameof(FilteredSchedules));
            }
        }

        public ScheduleInfo SelectedSchedule
        {
            get =&gt; _selectedSchedule;
            set
            {
                _selectedSchedule = value;
                OnPropertyChanged(nameof(SelectedSchedule));
                OnPropertyChanged(nameof(CanEdit));
            }
        }

        public string SearchText
        {
            get =&gt; _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                FilterSchedules();
            }
        }

        public int ScheduleCount => _allSchedules?.Count ?? 0;
        public int FilteredCount => _filteredSchedules?.Count ?? 0;
        public bool CanEdit => SelectedSchedule != null;

        public ViewSchedule SelectedViewSchedule { get; private set; }

        public ScheduleSelector(Document document)
        {
            DebugLog("=== ScheduleSelector Constructor Started ===");
            _document = document;
            
            InitializeComponent();
            DebugLog("InitializeComponent completed");
            
            DataContext = this;
            DebugLog("DataContext set");
            
            LoadSchedules();
            DebugLog("ScheduleSelector constructor completed successfully");
        }

        private void LoadSchedules()
        {
            DebugLog("Loading schedules from document...");
            
            _allSchedules = new ObservableCollection<ScheduleInfo>();

            try
            {
                var schedules = new FilteredElementCollector(_document)
                    .OfClass(typeof(ViewSchedule))
                    .Cast&lt;ViewSchedule&gt;()
                    .Where(schedule =&gt; !schedule.IsTemplate)
                    .OrderBy(schedule =&gt; schedule.Name)
                    .ToList();

                DebugLog($"Found {schedules.Count} schedules in document");

                foreach (var schedule in schedules)
                {
                    var scheduleInfo = new ScheduleInfo
                    {
                        Name = schedule.Name,
                        Id = schedule.Id.IntegerValue.ToString(),
                        Category = GetScheduleCategory(schedule),
                        ViewSchedule = schedule
                    };
                    
                    _allSchedules.Add(scheduleInfo);
                }

                FilterSchedules();
                DebugLog("LoadSchedules completed successfully");
            }
            catch (Exception ex)
            {
                DebugLog($"Error loading schedules: {ex.Message}");
            }
        }

        private string GetScheduleCategory(ViewSchedule schedule)
        {
            try
            {
                if (schedule.Definition.CategoryId == ElementId.InvalidElementId)
                    return "Multi-Category";
                
                var category = Category.GetCategory(_document, schedule.Definition.CategoryId);
                return category?.Name ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private void FilterSchedules()
        {
            if (_allSchedules == null) return;

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredSchedules = new ObservableCollection<ScheduleInfo>(_allSchedules);
            }
            else
            {
                var filtered = _allSchedules
                    .Where(s => s.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                               s.Category.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                
                FilteredSchedules = new ObservableCollection<ScheduleInfo>(filtered);
            }

            OnPropertyChanged(nameof(ScheduleCount));
            OnPropertyChanged(nameof(FilteredCount));
            DebugLog($"Filtered to {FilteredCount} schedules (search: '{SearchText}')");
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSchedule != null)
            {
                DebugLog($"User selected schedule for editing: {SelectedSchedule.Name}");
                SelectedViewSchedule = SelectedSchedule.ViewSchedule;
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DebugLog("User cancelled schedule selection");
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ScheduleInfo
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Category { get; set; }
        public ViewSchedule ViewSchedule { get; set; }
    }
}
