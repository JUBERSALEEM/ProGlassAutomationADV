using ProGlassAutomation.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
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

        private int _sheetQuantity = 1;
        public int SheetQuantity
        {
            get => _sheetQuantity;
            set
            {
                if (value < 1) value = 1;
                if (_sheetQuantity == value) return;
                _sheetQuantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SheetQuantityText));
                Recalculate();
            }
        }

        public string SheetQuantityText
        {
            get => _sheetQuantity.ToString();
            set
            {
                if (int.TryParse(value, out int parsed) && parsed > 0)
                {
                    SheetQuantity = parsed;
                }
            }
        }

        public ObservableCollection<SpecGroupColorSide> AvailableSources { get; } = new();

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

        /// <summary>
        /// The pane side this wastage charge belongs to, shown beside the
        /// colour so a DGU's OUTER and INNER rows stay distinguishable even
        /// when both panes share the same colour.
        /// </summary>
        public string SideDisplay
        {
            get
            {
                var first = AvailableSources.FirstOrDefault(s => s.IsChecked);
                if (first == null) return "";

                string side = first.SideLabel ?? "";
                if (side.IndexOf("Lam Inner", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Lam Inner";
                if (side.IndexOf("Lam Outer", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Lam Outer";
                if (side.IndexOf("Inner", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Inner";
                return "Outer";
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
                OnPropertyChanged(nameof(SelectedSourceSqm));
                Recalculate();
            }
        }

        public decimal ChargeableSqm => TotalSqm;
        public decimal SelectedSourceSqm => ChargeableSqm;
        public decimal UtilizedSqm => BaseResultValue;

        private decimal _selectedSourcePricePerSqm;
        public decimal SelectedSourcePricePerSqm
        {
            get => _selectedSourcePricePerSqm;
            private set
            {
                if (_selectedSourcePricePerSqm == value) return;
                _selectedSourcePricePerSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedSourcePriceDisplay));
            }
        }

        public string SelectedSourcePriceDisplay =>
            SelectedSourcePricePerSqm > 0m ? SelectedSourcePricePerSqm.ToString("0.####", CultureInfo.InvariantCulture) : "";

        private int _selectedSheetWidthMm;
        public int SelectedSheetWidthMm
        {
            get => _selectedSheetWidthMm;
            private set
            {
                if (_selectedSheetWidthMm == value) return;
                _selectedSheetWidthMm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedSheetWidthText));
                OnPropertyChanged(nameof(SelectedSheetSummary));
            }
        }

        private int _selectedSheetHeightMm;
        public int SelectedSheetHeightMm
        {
            get => _selectedSheetHeightMm;
            private set
            {
                if (_selectedSheetHeightMm == value) return;
                _selectedSheetHeightMm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedSheetHeightText));
                OnPropertyChanged(nameof(SelectedSheetSummary));
            }
        }

        private bool _hasMixedSelectedSheets;
        public bool HasMixedSelectedSheets
        {
            get => _hasMixedSelectedSheets;
            private set
            {
                if (_hasMixedSelectedSheets == value) return;
                _hasMixedSelectedSheets = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedSheetWidthText));
                OnPropertyChanged(nameof(SelectedSheetHeightText));
                OnPropertyChanged(nameof(SelectedSheetSummary));
            }
        }

        public string SelectedSheetWidthText =>
            HasMixedSelectedSheets ? "Mixed" : (SelectedSheetWidthMm > 0 ? SelectedSheetWidthMm.ToString(CultureInfo.InvariantCulture) : "");

        public string SelectedSheetHeightText =>
            HasMixedSelectedSheets ? "Mixed" : (SelectedSheetHeightMm > 0 ? SelectedSheetHeightMm.ToString(CultureInfo.InvariantCulture) : "");

        public string SelectedSheetSummary
        {
            get
            {
                if (HasMixedSelectedSheets) return "Mixed selected sheets";
                if (SelectedSheetWidthMm > 0 && SelectedSheetHeightMm > 0)
                    return $"{SelectedSheetWidthMm} × {SelectedSheetHeightMm}";
                return "";
            }
        }

        private decimal _rate;
        private decimal _manualRate;
        private bool _isApplyingCalculatedRate;

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
        public decimal FormulaPricePerSqm
        {
            get => _formulaPricePerSqm;
            set
            {
                if (_formulaPricePerSqm == value) return;
                _formulaPricePerSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormulaPricePerSqmText));
                Recalculate();
            }
        }

        public string FormulaPricePerSqmText =>
            FormulaPricePerSqm > 0m ? FormulaPricePerSqm.ToString("0.####", CultureInfo.InvariantCulture) : "";

        private decimal _optimizationPercent;
        private string _optimizationPercentText = "";
        private bool _isSyncingOptimizationText;

        public decimal OptimizationPercent
        {
            get => _optimizationPercent;
            set
            {
                if (_optimizationPercent == value) return;
                _optimizationPercent = value;
                SyncOptimizationTextFromValue();
                OnPropertyChanged();
                Recalculate();
            }
        }

        public string OptimizationPercentText
        {
            get => _optimizationPercentText;
            set
            {
                var next = value ?? string.Empty;
                if (string.Equals(_optimizationPercentText, next, StringComparison.Ordinal))
                    return;

                _optimizationPercentText = next;
                OnPropertyChanged();

                if (_isSyncingOptimizationText) return;

                decimal parsed = ParseDecimalText(next);
                if (_optimizationPercent != parsed)
                {
                    _optimizationPercent = parsed;
                    OnPropertyChanged(nameof(OptimizationPercent));
                }

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
        public decimal BaseResultValue
        {
            get => _baseResultValue;
            private set
            {
                if (_baseResultValue == value) return;
                _baseResultValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UtilizedSqm));
            }
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

                if (SelectedSourceSqm <= 0m)
                    return "Optimization formula — select wastage sources first so the wastage SQM can be calculated.";

                if (FormulaPricePerSqm <= 0m)
                    return "Optimization formula — sheet price could not be resolved from Step 4 for the selected source(s).";

                if (OptimizationPercent <= 0m)
                    return "Optimization formula — enter optimization % (decimals allowed, e.g. 15.60).";

                return $"{FormulaPricePerSqm:0.####} ÷ {OptimizationPercent:0.##}% = {Section1Value:N2}  ·  {FormulaPricePerSqm:0.####} ÷ {BaselineOptimizationPercent:0.##}% = {Section2Value:N2}  ·  Utilized SQM: {UtilizedSqm:N2}";
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
                if (!IsActive) return "DISABLED";

                int count = AvailableSources.Count(s => s.IsChecked);
                string colorSuffix = string.IsNullOrWhiteSpace(ColorName)
                    ? ""
                    : $" ({ColorName.ToUpper()} — {count} sources)";

                if (UseOptimizationFormula)
                {
                    if (FormulaPricePerSqm <= 0m || OptimizationPercent <= 0m || SelectedSourceSqm <= 0m)
                        return $"Optimization formula pending input{colorSuffix}";

                    return $"{FormulaPricePerSqm:0.####} ÷ {OptimizationPercent:0.##}% = {Section1Value:N2}  ·  {FormulaPricePerSqm:0.####} ÷ {BaselineOptimizationPercent:0.##}% = {Section2Value:N2}  ·  RATE {BaseResultValue:N2} × {SelectedSourceSqm:0.000} SQM = {ResultAmount:N2} AED{colorSuffix}";
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
            OnPropertyChanged(nameof(SideDisplay));
        }

        private void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SpecGroupColorSide.IsChecked)
                or nameof(SpecGroupColorSide.SqmValue)
                or nameof(SpecGroupColorSide.PricePerSqm)
                or null
                or "")
            {
                RecalcFromSources();
                OnPropertyChanged(nameof(SideDisplay));
            }
        }

        private void Recalculate()
        {
            if (UseOptimizationFormula)
            {
                RecalculateOptimizationResults();
                ResultAmount = IsActive ? Math.Round(FinalResultValue, 2) : 0m;
            }
            else
            {
                ResetOptimizationResults();
                RestoreManualRateDisplay();
                ResultAmount = IsActive ? Math.Round(TotalSqm * Rate, 2) : 0m;
            }

            OnPropertyChanged(nameof(FormulaDisplay));
            OnPropertyChanged(nameof(SourceSummaryText));
            OnPropertyChanged(nameof(OptimizationSummaryText));
        }

        public void ForceRefreshSqm() => RecalcFromSources();

        private void RecalcFromSources()
        {
            TotalSqm = AvailableSources.Where(s => s.IsChecked).Sum(s => s.SqmValue);
            RefreshSelectedSourceDefaults();
            OnPropertyChanged(nameof(FormulaDisplay));
            OnPropertyChanged(nameof(SourceSummaryText));
            OnPropertyChanged(nameof(OptimizationSummaryText));
        }

        private void RefreshSelectedSourceDefaults()
        {
            var checkedSources = AvailableSources
                .Where(s => s.IsChecked)
                .ToList();

            if (checkedSources.Count == 0)
            {
                HasMixedSelectedSheets = false;
                SelectedSheetWidthMm = 0;
                SelectedSheetHeightMm = 0;
                SelectedSourcePricePerSqm = 0m;
                SetAutoFormulaPrice(0m);
                return;
            }

            var uniqueSizes = checkedSources
                .Where(s => s.SheetWidthMm > 0 && s.SheetHeightMm > 0)
                .Select(s => (s.SheetWidthMm, s.SheetHeightMm))
                .Distinct()
                .ToList();

            HasMixedSelectedSheets = uniqueSizes.Count > 1;
            if (uniqueSizes.Count == 1)
            {
                SelectedSheetWidthMm = uniqueSizes[0].SheetWidthMm;
                SelectedSheetHeightMm = uniqueSizes[0].SheetHeightMm;
            }
            else
            {
                SelectedSheetWidthMm = 0;
                SelectedSheetHeightMm = 0;
            }

            decimal totalPricedSqm = checkedSources
                .Where(s => s.PricePerSqm > 0m && s.SqmValue > 0m)
                .Sum(s => s.SqmValue);

            decimal autoPrice = 0m;
            if (totalPricedSqm > 0m)
            {
                autoPrice = checkedSources
                    .Where(s => s.PricePerSqm > 0m && s.SqmValue > 0m)
                    .Sum(s => s.PricePerSqm * s.SqmValue) / totalPricedSqm;
            }

            autoPrice = Math.Round(autoPrice, 6);
            SelectedSourcePricePerSqm = autoPrice;
            SetAutoFormulaPrice(autoPrice);
        }

        private void RecalculateOptimizationResults()
        {
            if (FormulaPricePerSqm <= 0m || OptimizationPercent <= 0m || SelectedSourceSqm <= 0m)
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
            FinalResultValue = Math.Round(BaseResultValue * SelectedSourceSqm, 6);
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
            if (Rate == _manualRate) return;

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
            if (Rate == value) return;

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

        private void SetAutoFormulaPrice(decimal value)
        {
            if (FormulaPricePerSqm == value) return;
            _formulaPricePerSqm = value;
            OnPropertyChanged(nameof(FormulaPricePerSqm));
            OnPropertyChanged(nameof(FormulaPricePerSqmText));
            Recalculate();
        }

        private void SyncOptimizationTextFromValue()
        {
            string next = _optimizationPercent > 0m
                ? _optimizationPercent.ToString("0.####", CultureInfo.InvariantCulture)
                : string.Empty;

            if (string.Equals(_optimizationPercentText, next, StringComparison.Ordinal))
                return;

            _isSyncingOptimizationText = true;
            try
            {
                _optimizationPercentText = next;
                OnPropertyChanged(nameof(OptimizationPercentText));
            }
            finally
            {
                _isSyncingOptimizationText = false;
            }
        }

        private static decimal ParseDecimalText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0m;

            string normalized = text.Trim().Replace(',', '.');
            return decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out decimal result)
                ? result
                : 0m;
        }
    }
}
