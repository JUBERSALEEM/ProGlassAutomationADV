using ProGlassAutomation.ViewModels;
using System;
using System.Collections.ObjectModel;

namespace ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges
{
    /// <summary>
    /// One row in the Other Charges worksheet.
    /// Bound to UI checkbox + dropdown + rate input + formula display.
    /// </summary>
    public class OtherChargeRow : ViewModelBase
    {
        /// <summary>Template identity — used to look up defaults and behaviour.</summary>
        public string TemplateKey { get; set; } = "";

        /// <summary>Charge name shown in first column (e.g. "Argon Gas (Sqm)").</summary>
        public string ChargeName { get; set; } = "";

        /// <summary>Short description shown under the name.</summary>
        public string Description { get; set; } = "";

        /// <summary>Unit type — Sqm / Lm / Qty. Filters the Grid Source dropdown.</summary>
        public OtherChargeUnit Unit { get; set; }

        // ── Active state ────────────────────────────────────────────────────

        private bool _isActive;
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

        // ── Grid source (target) ────────────────────────────────────────────

        /// <summary>Available options filtered by Unit type.</summary>
        public ObservableCollection<GridSourceOption> AvailableSources { get; } = new();

        private GridSourceOption _selectedSource;
        public GridSourceOption SelectedSource
        {
            get => _selectedSource;
            set
            {
                if (_selectedSource == value) return;

                if (_selectedSource != null)
                    _selectedSource.PropertyChanged -= OnSourceValueChanged;

                _selectedSource = value;

                if (_selectedSource != null)
                    _selectedSource.PropertyChanged += OnSourceValueChanged;

                OnPropertyChanged();
                OnPropertyChanged(nameof(MetricQty));
                Recalculate();
            }
        }

        private void OnSourceValueChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GridSourceOption.Value))
            {
                OnPropertyChanged(nameof(MetricQty));
                Recalculate();
            }
        }

        /// <summary>Live quantity read from selected source.</summary>
        public decimal MetricQty => _selectedSource?.Value ?? 0m;

        // ── Rate ────────────────────────────────────────────────────────────

        private decimal _rate;
        public decimal Rate
        {
            get => _rate;
            set
            {
                if (_rate == value) return;
                _rate = value;
                OnPropertyChanged();
                Recalculate();
            }
        }

        // ── Computed result ─────────────────────────────────────────────────

        private decimal _resultAmount;
        public decimal ResultAmount
        {
            get => _resultAmount;
            private set { if (_resultAmount == value) return; _resultAmount = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Full formula display, e.g. "303.33 LM × 6.00 AED = 1,819.98 AED (W1 H1 PERIMETER (LM1))"
        /// </summary>
        public string FormulaDisplay
        {
            get
            {
                if (!IsActive || _selectedSource == null)
                    return "DISABLED";

                string unitLabel = Unit.ToString().ToUpper();
                string hint = string.IsNullOrEmpty(_selectedSource.FormulaHint) ? "" : $" ({_selectedSource.FormulaHint})";

                return $"{MetricQty:0.##} {unitLabel} × {Rate:0.##} AED = {ResultAmount:N2} AED{hint}";
            }
        }

        // ── Recalculation ───────────────────────────────────────────────────

        private void Recalculate()
        {
            ResultAmount = IsActive ? Math.Round(MetricQty * Rate, 2) : 0m;
            OnPropertyChanged(nameof(FormulaDisplay));
        }

        /// <summary>Called externally when parent VM wants a manual refresh (e.g. after AvailableSources rebuild).</summary>
        public void ForceRefresh()
        {
            OnPropertyChanged(nameof(MetricQty));
            Recalculate();
        }
    }
}