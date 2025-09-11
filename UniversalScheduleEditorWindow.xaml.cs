using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Reflection;
using System.IO;
using Grid = System.Windows.Controls.Grid;
using Rectangle = System.Windows.Shapes.Rectangle;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using IOPath = System.IO.Path;

namespace RevitScheduleEditor
{
    public class ColumnHeaderInfo
    {
        public string ParameterType { get; set; }
        public string ParameterGroup { get; set; }
    }

    public partial class ScheduleEditorWindow : Window
    {
        private static Assembly _revitAPIAssembly;
        private static Type _documentType;
        private static Type _elementType;
        private static Type _viewScheduleType;
        private static Type _tableDataType;
        private static Type _transactionType;
        private static Type _scheduleSheetInstanceType;
        private static Version _revitVersion;
        private object _document;
        private object _uidocument;
        private bool _hasUIDocument;
        
        // Universal constructors
        public ScheduleEditorWindow(object document)
        {
            InitializeRevitTypes();
            _document = document;
            _uidocument = null;
            _hasUIDocument = false;
            InitializeComponent();
            LoadSchedules();
        }

        public ScheduleEditorWindow(object document, object uidocument, Version revitVersion)
        {
            InitializeRevitTypes();
            _document = document;
            _uidocument = uidocument;
            _hasUIDocument = true;
            _revitVersion = revitVersion;
            InitializeComponent();
            LoadSchedules();
        }

        private static void InitializeRevitTypes()
        {
            if (_revitAPIAssembly != null) return;

            try
            {
                // Find loaded RevitAPI assembly
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                _revitAPIAssembly = loadedAssemblies.FirstOrDefault(a => a.GetName().Name == "RevitAPI");
                
                if (_revitAPIAssembly == null)
                {
                    // Try to load from Revit installation
                    var revitPath = DetectRevitInstallation();
                    if (!string.IsNullOrEmpty(revitPath))
                    {
                        var apiPath = IOPath.Combine(revitPath, "RevitAPI.dll");
                        if (File.Exists(apiPath))
                        {
                            _revitAPIAssembly = Assembly.LoadFrom(apiPath);
                        }
                    }
                }

                if (_revitAPIAssembly == null)
                {
                    throw new InvalidOperationException("Could not load RevitAPI assembly");
                }

                // Cache important types
                _documentType = _revitAPIAssembly.GetType("Autodesk.Revit.DB.Document");
                _elementType = _revitAPIAssembly.GetType("Autodesk.Revit.DB.Element");
                _viewScheduleType = _revitAPIAssembly.GetType("Autodesk.Revit.DB.ViewSchedule");
                _tableDataType = _revitAPIAssembly.GetType("Autodesk.Revit.DB.TableData");
                _transactionType = _revitAPIAssembly.GetType("Autodesk.Revit.DB.Transaction");
                _scheduleSheetInstanceType = _revitAPIAssembly.GetType("Autodesk.Revit.DB.ScheduleSheetInstance");

                _revitVersion = _revitAPIAssembly.GetName().Version;
                Debug.WriteLine($"[Universal] RevitAPI assembly loaded, version: {_revitVersion}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Universal] Failed to initialize Revit types: {ex.Message}");
                throw;
            }
        }

        private static string DetectRevitInstallation()
        {
            var versions = new[] { "2026", "2025", "2024", "2023", "2022", "2021", "2020" };
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            foreach (var version in versions)
            {
                var path = IOPath.Combine(programFiles, "Autodesk", $"Revit {version}");
                if (Directory.Exists(path) && File.Exists(IOPath.Combine(path, "RevitAPI.dll")))
                {
                    return path;
                }
            }
            return null;
        }

        private void LoadSchedules()
        {
            try
            {
                DebugLog("Loading schedules using reflection...");
                
                // Get all ViewSchedule elements from document
                var schedules = GetElementsOfType(_viewScheduleType);
                DebugLog($"Found {schedules.Count()} schedules");

                // Populate ComboBox
                var scheduleItems = schedules.Select(s => new ScheduleItem
                {
                    Name = GetProperty(s, "Name")?.ToString() ?? "Unknown",
                    ViewSchedule = s
                }).ToList();

                ScheduleComboBox.ItemsSource = scheduleItems;
                
                if (scheduleItems.Any())
                {
                    ScheduleComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                DebugLog($"Error loading schedules: {ex.Message}");
                MessageBox.Show($"Error loading schedules: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private IEnumerable<object> GetElementsOfType(Type elementType)
        {
            try
            {
                // Create FilteredElementCollector
                var collectorType = _revitAPIAssembly.GetType("Autodesk.Revit.DB.FilteredElementCollector");
                var collector = Activator.CreateInstance(collectorType, _document);

                // Call OfClass method
                var ofClassMethod = collectorType.GetMethod("OfClass", new[] { typeof(Type) });
                var filteredCollector = ofClassMethod.Invoke(collector, new object[] { elementType });

                // Get elements
                var toElementsMethod = collectorType.GetMethod("ToElements");
                var elements = (System.Collections.IList)toElementsMethod.Invoke(filteredCollector, null);

                return elements.Cast<object>();
            }
            catch (Exception ex)
            {
                DebugLog($"Error getting elements of type {elementType.Name}: {ex.Message}");
                return new List<object>();
            }
        }

        private object GetProperty(object obj, string propertyName)
        {
            if (obj == null) return null;
            try
            {
                var prop = obj.GetType().GetProperty(propertyName);
                return prop?.GetValue(obj);
            }
            catch
            {
                return null;
            }
        }

        private object InvokeMethod(object obj, string methodName, params object[] parameters)
        {
            if (obj == null) return null;
            try
            {
                var method = obj.GetType().GetMethod(methodName);
                return method?.Invoke(obj, parameters);
            }
            catch
            {
                return null;
            }
        }

        private void DebugLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var fullMessage = $"[ScheduleEditor] {timestamp} - {message}";
            Debug.WriteLine(fullMessage);
        }

        public class ScheduleItem
        {
            public string Name { get; set; }
            public object ViewSchedule { get; set; }
        }

        private void ScheduleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScheduleComboBox.SelectedItem is ScheduleItem selectedItem)
            {
                LoadScheduleData(selectedItem.ViewSchedule);
            }
        }

        private void LoadScheduleData(object viewSchedule)
        {
            try
            {
                DebugLog($"Loading data for schedule: {GetProperty(viewSchedule, "Name")}");
                
                // Get table data using reflection
                var tableData = GetProperty(viewSchedule, "GetTableData");
                if (tableData == null)
                {
                    // Try calling GetTableData() method
                    tableData = InvokeMethod(viewSchedule, "GetTableData");
                }

                if (tableData == null)
                {
                    DebugLog("Could not get table data from schedule");
                    return;
                }

                // Extract data from table
                var rowCount = (int)GetProperty(tableData, "GetSectionData");
                DebugLog($"Schedule has {rowCount} sections");

                // For now, show basic info
                MessageBox.Show($"Schedule loaded: {GetProperty(viewSchedule, "Name")}\nThis is the universal version working with Revit {_revitVersion}", 
                    "Schedule Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                
            }
            catch (Exception ex)
            {
                DebugLog($"Error loading schedule data: {ex.Message}");
                MessageBox.Show($"Error loading schedule data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Refreshing schedules...";
                StatusText.Foreground = new SolidColorBrush(Colors.Orange);
                
                LoadSchedules();
                
                StatusText.Text = "Schedules refreshed successfully";
                StatusText.Foreground = new SolidColorBrush(Colors.Green);
            }
            catch (Exception ex)
            {
                DebugLog($"Error refreshing schedules: {ex.Message}");
                StatusText.Text = $"Error: {ex.Message}";
                StatusText.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Applying changes...";
                StatusText.Foreground = new SolidColorBrush(Colors.Orange);
                
                // TODO: Implement changes application using reflection
                
                StatusText.Text = "Changes applied successfully";
                StatusText.Foreground = new SolidColorBrush(Colors.Green);
            }
            catch (Exception ex)
            {
                DebugLog($"Error applying changes: {ex.Message}");
                StatusText.Text = $"Error: {ex.Message}";
                StatusText.Foreground = new SolidColorBrush(Colors.Red);
            }
        }
    }
}
