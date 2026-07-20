using ProGlassAutomation.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages
{
    public class WastageRow : ViewModelBase
    {
        public Guid Id { get; } = Guid.NewGuid();

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive == value) return; _isActive = value; OnPropertyChanged(); Recalculate(); }
        }

        private string _colorName = "";
        public string ColorName
        {
            get => _colorName;
            set { if (_colorName == value) return; _colorName = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormulaDisplay)); }
        }

        public ObservableCollection<SpecGroupColorSide> AvailableSources { get; } = new();

        private decimal _totalSqm;
        public decimal TotalSqm
        {
            get => _totalSqm;
            private set { if (_totalSqm == value) return; _totalSqm = value; OnPropertyChanged(); Recalculate(); }
        }

        private decimal _rate = 40m;
        public decimal Rate
        {
            get => _rate;
            set { if (_rate == value) return; _rate = value; OnPropertyChanged(); Recalculate(); }
        }

        private decimal _resultAmount;
        public decimal ResultAmount
        {
            get => _resultAmount;
            private set { if (_resultAmount == value) return; _resultAmount = value; OnPropertyChanged(); }
        }

        public string FormulaDisplay
        {
            get
            {
                if (!_isActive) return "DISABLED";
                int count = AvailableSources.Count(s => s.IsChecked);
                string colorSuffix = string.IsNullOrWhiteSpace(_colorName) ? "" : $" ({_colorName.ToUpper()} — {count} sources)";
                return $"{TotalSqm:0.000} SQM × {Rate:0.##} AED = {ResultAmount:N2} AED{colorSuffix}";
            }
        }

        public WastageRow()
        {
            AvailableSources.CollectionChanged += (s, e) => {
                if (e.NewItems != null) foreach (SpecGroupColorSide item in e.NewItems) item.PropertyChanged += OnSourceChanged;
                if (e.OldItems != null) foreach (SpecGroupColorSide item in e.OldItems) item.PropertyChanged -= OnSourceChanged;
            };
        }

        private void OnSourceChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SpecGroupColorSide.IsChecked))
            {
                TotalSqm = AvailableSources.Where(s => s.IsChecked).Sum(s => s.SqmValue);
                OnPropertyChanged(nameof(FormulaDisplay));
            }
        }

        private void Recalculate()
        {
            ResultAmount = _isActive ? Math.Round(TotalSqm * _rate, 2) : 0m;
            OnPropertyChanged(nameof(FormulaDisplay));
        }

        public void ForceRefreshSqm()
        {
            TotalSqm = AvailableSources.Where(s => s.IsChecked).Sum(s => s.SqmValue);
        }
    }
}