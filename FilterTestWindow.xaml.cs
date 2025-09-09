using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace RevitScheduleEditor
{
    public partial class FilterTestWindow : Window
    {
        public FilterTestWindow()
        {
            InitializeComponent();
            TestTextFilters();
        }

        private void TestTextFilters()
        {
            // Test data
            var testValues = new List<string>
            {
                "420.05.010d",
                "420.05.013a", 
                "420.05.111",
                "420.08.002",
                "420.08.040",
                "420.11.001",
                "420.11.022",
                "420.11.024"
            };

            try
            {
                var filterWindow = new TextFiltersWindow()
                {
                    Owner = this
                };
                
                // Set filter data using the new method
                filterWindow.SetFilterData(testValues);

                if (filterWindow.ShowDialog() == true)
                {
                    var results = filterWindow.SelectedValues;
                    MessageBox.Show($"Selected {results.Count} items:\n{string.Join("\n", results)}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}\n\nStackTrace:\n{ex.StackTrace}");
            }
        }
    }
}
