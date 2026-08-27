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
        private const decimal BaselineOptimizationPercent = 85m;

        public Guid Id { get; } = Guid.NewGuid();

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value) return;
                _isActive = value;
                OnPropertyChanged();
                Recalculate();
            }
        }

        private string _colorName = "";
        public string ColorName
        {
            get => _colorName;
            set
            {
                if (_colorName == value) return;
                _colorName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormulaDisplay));
            }
        }

        public ObservableCollection<SpecGroupColorSide> AvailableSources { get; } = new();

        /// <summary>
        /// Live label for the SOURCE toggle. Must update whenever the user
        /// ticks or unticks a spec side.
        /// </summary>
        public string SourceSummaryText
        {
            get
            {
                var checkedSources = AvailableSources.Where(s => s.IsChecked).ToList();
                if (checkedSources.Count == 0)
                    return "Select sources";

                if (checkedSources.Count == 1)
                {
                    var s = checkedSources[0];
                    return $"{s.ShortLabel} · {s.SqmValue:0.000} sqm";
                }

                return $"{checkedSources.Count} sources · {TotalSqm:0.000} sqm";
            }
        }

        private decimal _totalSqm;
        public decimal TotalSqm
        {
            get => _totalSqm;
            private set
            {
                if (_totalSqm == value) return;
                _totalSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChargeableSqm));
                Recalculate();
            }
        }

        /// <summary>
        /// Chargeable SQM for the wastage formula. In Step 6 this is driven by
        /// the checked outer/inner spec sources.
        /// </summary>
        public decimal ChargeableSqm => TotalSqm;

        private decimal _rate;
        private decimal _manualRate;
        private bool _isApplyingCalculatedRate;

        /// <summary>
        /// Effective wastage rate in AED per SQM. In standard mode the user
        /// edits it manually. In optimization mode it is derived from the
        /// optimization formula and becomes read-only in the UI.
        /// </summary>
        public decimal Rate
        {
            get => _rate;
            set
            {
                if (_rate == value) return;
                _rate = value;
                if (!_isApplyingCalculatedRate)
                    _manualRate = value;
                OnPropertyChanged();
                Recalculate();
            }
        }

        private bool _useOptimizationFormula;
        /// <summary>
        /// False = classic SQM × manual rate.
        /// True  = derive rate from price / optimization / chargeable SQM.
        /// </summary>
        public bool UseOptimizationFormula
        {
            get => _useOptimizationFormula;
            set
            {
                if (_useOptimizationFormula == value) return;
                _useOptimizationFormula = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsManualRateMode));
                OnPropertyChanged(nameof(IsOptimizationFormulaMode));
                Recalculate();
            }
        }

        public bool IsManualRateMode => !UseOptimizationFormula;
        public bool IsOptimizationFormulaMode => UseOptimizationFormula;

        private decimal _formulaPricePerSqm;
        /// <summary>
        /// Glass price used by the optimization-based wastage calculation.
        /// </summary>
        public decimal FormulaPricePerSqm
        {
            get => _formulaPricePerSqm;
            set
            {
                if (_formulaPricePerSqm == value) return;
                _formulaPricePerSqm = value;
                OnPropertyChanged();
                Recalculate();
            }
        }

        private decimal _optimizationPercent;
        /// <summary>
        /// Third-party optimization percentage, e.g. 70 means 70%.
        /// </summary>
        public decimal OptimizationPercent
        {
            get => _optimizationPercent;
            set
            {
                if (_optimizationPercent == value) return;
                _optimizationPercent = value;
                OnPropertyChanged();
                Recalculate();
            }
        }

        private decimal _section1Value;
        public decimal Section1Value
        {
            get => _section1Value;
            private set { if (_section1Value == value) return; _section1Value = value; OnPropertyChanged(); }
        }

        private decimal _section2Value;
        public decimal Section2Value
        {
            get => _section2Value;
            private set { if (_section2Value == value) return; _section2Value = value; OnPropertyChanged(); }
        }

        private decimal _baseResultValue;
        /// <summary>Optimization-derived AED per SQM delta.</summary>
        public decimal BaseResultValue
        {
            get => _baseResultValue;
            private set { if (_baseResultValue == value) return; _baseResultValue = value; OnPropertyChanged(); }
        }

        private decimal _finalResultValue;
        public decimal FinalResultValue
        {
            get => _finalResultValue;
            private set { if (_finalResultValue == value) return; _finalResultValue = value; OnPropertyChanged(); }
        }

        public string OptimizationSummaryText
        {
            get
            {
                if (!UseOptimizationFormula)
                    return "Manual rate mode — selected SQM × entered rate.";

                if (FormulaPricePerSqm <= 0m || OptimizationPercent <= 0m || ChargeableSqm <= 0m)
                    return "Optimization formula — enter glass price and optimization %, then select wastage sources.";

                return $"Section 1: {Section1Value:N2}  ·  Section 2: {Section2Value:N2}  ·  Base rate: {BaseResultValue:N4} / SQM  ·  Chargeable SQM: {ChargeableSqm:0.000}";
            }
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
                string colorSuffix = string.IsNullOrWhiteSpace(_colorName)
                    ? ""
                    : $" ({_colorName.ToUpper()} — {count} sources)";

                if (UseOptimizationFormula)
                {
                    if (FormulaPricePerSqm <= 0m || OptimizationPercent <= 0m || ChargeableSqm <= 0m)
                        return $"Optimization formula pending input{colorSuffix}";

                    return $"({FormulaPricePerSqm:0.##} ÷ {OptimizationPercent:0.##}%) - ({FormulaPricePerSqm:0.##} ÷ {BaselineOptimizationPercent:0.##}%) = {BaseResultValue:N4} AED/SQM × {ChargeableSqm:0.000} SQM = {ResultAmount:N2} AED{colorSuffix}";
                }

                return $"{TotalSqm:0.000} SQM × {Rate:0.##} AED = {ResultAmount:N2} AED{colorSuffix}";
            }
        }

        public WastageRow()
        {
            AvailableSources.CollectionChanged += OnSourcesCollectionChanged;
        }

        private void OnSourcesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (SpecGroupColorSide item in e.NewItems)
                    item.PropertyChanged += OnSourceChanged;
            }

            if (e.OldItems != null)
            {
                foreach (SpecGroupColorSide item in e.OldItems)
                    item.PropertyChanged -= OnSourceChanged;
            }

            RecalcFromSources();
        }

        private void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SpecGroupColorSide.IsChecked)
                or nameof(SpecGroupColorSide.SqmValue)
                or null
                or "")
            {
                RecalcFromSources();
            }
        }

        private void Recalculate()
        {
            if (UseOptimizationFormula)
            {
                RecalculateOptimizationResults();
                ResultAmount = _isActive ? Math.Round(FinalResultValue, 2) : 0m;
            }
            else
            {
                ResetOptimizationResults();
                RestoreManualRateDisplay();
                ResultAmount = _isActive ? Math.Round(TotalSqm * _rate, 2) : 0m;
            }

            OnPropertyChanged(nameof(FormulaDisplay));
            OnPropertyChanged(nameof(SourceSummaryText));
            OnPropertyChanged(nameof(OptimizationSummaryText));
        }

        public void ForceRefreshSqm()
        {
            RecalcFromSources();
        }

        private void RecalcFromSources()
        {
            TotalSqm = AvailableSources.Where(s => s.IsChecked).Sum(s => s.SqmValue);
            OnPropertyChanged(nameof(FormulaDisplay));
            OnPropertyChanged(nameof(SourceSummaryText));
            OnPropertyChanged(nameof(OptimizationSummaryText));
        }

        private void RecalculateOptimizationResults()
        {
            if (FormulaPricePerSqm <= 0m || OptimizationPercent <= 0m || ChargeableSqm <= 0m)
            {
                Section1Value = 0m;
                Section2Value = 0m;
                BaseResultValue = 0m;
                FinalResultValue = 0m;
                SetCalculatedRate(0m);
                return;
            }

            decimal optimizationFactor = OptimizationPercent / 100m;
            if (optimizationFactor == 0m)
            {
                Section1Value = 0m;
                Section2Value = 0m;
                BaseResultValue = 0m;
                FinalResultValue = 0m;
                SetCalculatedRate(0m);
                return;
            }

            Section1Value = Math.Round(FormulaPricePerSqm / optimizationFactor, 6);
            Section2Value = Math.Round(FormulaPricePerSqm / (BaselineOptimizationPercent / 100m), 6);
            BaseResultValue = Math.Round(Section1Value - Section2Value, 6);
            FinalResultValue = Math.Round(BaseResultValue * ChargeableSqm, 6);
            SetCalculatedRate(BaseResultValue);
        }

        private void ResetOptimizationResults()
        {
            Section1Value = 0m;
            Section2Value = 0m;
            BaseResultValue = 0m;
            FinalResultValue = 0m;
        }

        private void RestoreManualRateDisplay()
        {
            if (_rate == _manualRate) return;

            _isApplyingCalculatedRate = true;
            try
            {
                _rate = _manualRate;
                OnPropertyChanged(nameof(Rate));
            }
            finally
            {
                _isApplyingCalculatedRate = false;
            }
        }

        private void SetCalculatedRate(decimal value)
        {
            if (_rate == value) return;

            _isApplyingCalculatedRate = true;
            try
            {
                _rate = value;
                OnPropertyChanged(nameof(Rate));
            }
            finally
            {
                _isApplyingCalculatedRate = false;
            }
        }
    }
}
