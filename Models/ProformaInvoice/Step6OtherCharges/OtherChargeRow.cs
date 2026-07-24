using ProGlassAutomation.ViewModels;
using System;
using System.Collections.ObjectModel;

namespace ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges
{
    /// <summary>
    /// One row in the Other Charges worksheet.
    /// Bound to UI checkbox + dropdown + rate input + formula display.
    ///
    /// Special behaviour for TemplateKey == "POLISH":
    ///   - Uses PolishSubType instead of AvailableSources
    ///   - MetricQty is calculated from raw spec-group row dimensions per sub-type
    ///   - Custom sub-type shows extra description + manual qty inputs
    ///
    /// Special behaviour for TemplateKey == "DELIVERY" (Chat 7 Bug 4c hack):
    ///   - Always renders as 1 TRIP × Rate (per-invoice pricing)
    ///   - Ignores SelectedSource dropdown value
    ///   - Matches PDF/Excel export behavior
    ///   - For multi-trip billing: use Custom Charges section instead
    ///   - Proper fix (PER_ROUND source + editable qty) planned for Chat 8
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

        /// <summary>True if this is a Polish row (activates sub-type UI).</summary>
        public bool IsPolishRow => TemplateKey == "POLISH";

        /// <summary>
        /// True if this is a Delivery row (Chat 7 Bug 4c hack).
        /// When true, MetricQty is hardcoded to 1 (per-trip pricing).
        /// User's SelectedSource dropdown value is ignored.
        /// </summary>
        public bool IsDeliveryRow => TemplateKey == "DELIVERY";

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

        // ── Grid source (target) — for non-polish rows ─────────────────────

        /// <summary>Available options filtered by Unit type. Not used for Polish rows.</summary>
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

        // ── Polish-specific state ──────────────────────────────────────────

        /// <summary>Available polish sub-types (populated once).</summary>
        public ObservableCollection<PolishSubTypeMeta> AvailablePolishSubTypes { get; } = new();

        private PolishSubTypeMeta _selectedPolishSubType;
        public PolishSubTypeMeta SelectedPolishSubType
        {
            get => _selectedPolishSubType;
            set
            {
                if (_selectedPolishSubType == value) return;
                _selectedPolishSubType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCustomPolish));
                OnPropertyChanged(nameof(MetricQty));
                Recalculate();
            }
        }

        /// <summary>True when user picked "Custom" — shows extra inputs.</summary>
        public bool IsCustomPolish => IsPolishRow
            && _selectedPolishSubType?.Type == PolishSubType.Custom;

        /// <summary>User-typed description for Custom polish (e.g. "Special beveled edge").</summary>
        private string _customPolishDescription = "";
        public string CustomPolishDescription
        {
            get => _customPolishDescription;
            set { if (_customPolishDescription == value) return; _customPolishDescription = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormulaDisplay)); }
        }

        /// <summary>User-typed manual qty for Custom polish.</summary>
        private decimal _customPolishQty;
        public decimal CustomPolishQty
        {
            get => _customPolishQty;
            set
            {
                if (_customPolishQty == value) return;
                _customPolishQty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MetricQty));
                Recalculate();
            }
        }

        /// <summary>
        /// For Polish rows: this value is calculated externally by
        /// OtherChargesViewModel and pushed in via SetCalculatedPolishQty().
        /// Non-Custom sub-types get a computed LM based on the sub-type formula.
        /// </summary>
        private decimal _calculatedPolishQty;
        public decimal CalculatedPolishQty
        {
            get => _calculatedPolishQty;
            private set { if (_calculatedPolishQty == value) return; _calculatedPolishQty = value; OnPropertyChanged(); OnPropertyChanged(nameof(MetricQty)); Recalculate(); }
        }

        /// <summary>Called by OtherChargesViewModel when spec groups / dimensions change.</summary>
        public void SetCalculatedPolishQty(decimal value)
        {
            CalculatedPolishQty = value;
        }

        // ── Metric qty (unified accessor) ──────────────────────────────────

        /// <summary>
        /// Live quantity read from either:
        ///   - 1m                    (DELIVERY — Chat 7 Bug 4c hack)
        ///   - SelectedSource.Value  (normal rows)
        ///   - CustomPolishQty       (Polish + Custom)
        ///   - CalculatedPolishQty   (Polish + predefined sub-type)
        /// </summary>
        public decimal MetricQty
        {
            get
            {
                // ═══════════════ Bug 4c (Chat 7) — QUICK HACK ═══════════════
                // DELIVERY is per-invoice, not per-piece — always = 1 trip.
                // Ignore SelectedSource dropdown value.
                // Matches PDF/Excel export behavior.
                // For multi-trip billing: user should use Custom Charges section.
                // Proper fix (PER_ROUND source + editable qty) planned for Chat 8.
                // ══════════════════════════════════════════════════════════════
                if (IsDeliveryRow) return 1m;

                if (IsPolishRow)
                {
                    return IsCustomPolish ? _customPolishQty : _calculatedPolishQty;
                }
                return _selectedSource?.Value ?? 0m;
            }
        }

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
        /// For Polish + Custom: uses CustomPolishDescription as hint.
        /// For Delivery: shows "1 TRIP × Rate = Amount (PER TRIP - FIXED)"
        /// </summary>
        public string FormulaDisplay
        {
            get
            {
                if (!IsActive) return "DISABLED";

                // ═══════════════ Bug 4c (Chat 7) — Delivery display ═══════════
                // Shows fixed 1 TRIP × Rate formula with hint explaining behavior
                // ══════════════════════════════════════════════════════════════
                if (IsDeliveryRow)
                {
                    return $"1 TRIP × {Rate:0.##} AED = {ResultAmount:N2} AED (PER TRIP - FIXED)";
                }

                string unitLabel = Unit.ToString().ToUpper();
                string hint;

                if (IsPolishRow)
                {
                    if (IsCustomPolish)
                    {
                        hint = string.IsNullOrWhiteSpace(_customPolishDescription)
                            ? "(CUSTOM POLISH)"
                            : $"({_customPolishDescription.ToUpper()})";
                    }
                    else
                    {
                        hint = _selectedPolishSubType == null
                            ? ""
                            : $"({_selectedPolishSubType.DisplayLabel.ToUpper()})";
                    }
                }
                else
                {
                    if (_selectedSource == null) return "DISABLED";
                    hint = string.IsNullOrEmpty(_selectedSource.FormulaHint)
                        ? ""
                        : $" ({_selectedSource.FormulaHint})";
                }

                return $"{MetricQty:0.##} {unitLabel} × {Rate:0.##} AED = {ResultAmount:N2} AED {hint}".Trim();
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