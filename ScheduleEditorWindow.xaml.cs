using Autodesk.Revit.DB;
using System;
using System.Data;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace RevitScheduleEditor
{
    public class ColumnHeaderInfo
    {
        public string ParameterType { get; set; }
        public string ParameterGroup { get; set; }
    }


    public partial class ScheduleEditorWindow : Window
    {
        private readonly ScheduleEditorViewModel _viewModel;

        public ScheduleEditorWindow(Document doc)
        {
            InitializeComponent();
            _viewModel = new ScheduleEditorViewModel(doc);
            this.DataContext = _viewModel;
            _viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(_viewModel.SelectedSchedule))
                {
                    GenerateDataGridColumns();
                }
            };
        }

        private void GenerateDataGridColumns()
        {
            ScheduleDataGrid.Columns.Clear();
            if (_viewModel.SelectedSchedule == null) return;

            var visibleFields = _viewModel.SelectedSchedule.Definition.GetFieldOrder()
                .Select(id => _viewModel.SelectedSchedule.Definition.GetField(id))
                .Where(f => !f.IsHidden).ToList();

            foreach (var field in visibleFields)
            {
                string fieldName = field.GetName();
                var column = new DataGridTextColumn
                {
                    Header = fieldName,
                    Binding = new System.Windows.Data.Binding($"[{fieldName}]") { Mode = System.Windows.Data.BindingMode.TwoWay, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged }
                };
                ScheduleDataGrid.Columns.Add(column);
            }
        }

        private void ScheduleDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }
    }
}
