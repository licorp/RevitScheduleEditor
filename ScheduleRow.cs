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
                if (_values.ContainsKey(fieldName) && _values[fieldName] != value)
                {
                    _values[fieldName] = value;
                    OnPropertyChanged(fieldName);
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

        public bool IsModified
        {
            get
            {
                foreach (var key in _values.Keys)
                {
                    if (_values[key] != _originalValues[key])
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        
        public Dictionary<string, string> GetModifiedValues()
        {
            var modified = new Dictionary<string, string>();
            foreach(var key in _values.Keys)
            {
                if (_values[key] != _originalValues[key])
                {
                    modified[key] = _values[key];
                }
            }
            return modified;
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
