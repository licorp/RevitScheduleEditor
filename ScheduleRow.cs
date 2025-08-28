using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RevitScheduleEditor
{
    public class ScheduleRow : INotifyPropertyChanged
    {
        public ElementId Id { get; }
        private readonly Element _element;
        private readonly Dictionary<string, string> _values;
        private readonly Dictionary<string, string> _originalValues;

        public IReadOnlyDictionary<string, string> Values => _values;

        public ScheduleRow(Element element)
        {
            _element = element;
            Id = element.Id;
            _values = new Dictionary<string, string>();
            _originalValues = new Dictionary<string, string>();
        }

        public string this[string fieldName]
        {
            get => _values.ContainsKey(fieldName) ? _values[fieldName] : string.Empty;
            set
            {
                if (!_values.ContainsKey(fieldName) || _values[fieldName] != value)
                {
                    _values[fieldName] = value;
                    OnPropertyChanged(nameof(IsModified));
                }
            }
        }

        public void AddValue(string fieldName, string value)
        {
            _values[fieldName] = value;
            _originalValues[fieldName] = value;
        }
        
        public Element GetElement() => _element;

        public bool IsModified => _originalValues.Any(kvp => _values.ContainsKey(kvp.Key) && _values[kvp.Key] != kvp.Value);
        
        public Dictionary<string, string> GetModifiedValues()
        {
            return _originalValues
                .Where(kvp => _values.ContainsKey(kvp.Key) && _values[kvp.Key] != kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => _values[kvp.Key]);
        }

        public void AcceptChanges()
        {
            foreach (var key in _values.Keys.ToList())
            {
                _originalValues[key] = _values[key];
            }
            OnPropertyChanged(nameof(IsModified));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
