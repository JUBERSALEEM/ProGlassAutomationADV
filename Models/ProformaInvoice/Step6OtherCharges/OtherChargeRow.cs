using ProGlassAutomation.ViewModels;
using System;
using System.Collections.ObjectModel;

namespace ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges
{
    /// <summary>
    /// One row in the Other Charges worksheet.
    /// Bound to UI checkbox + spec-target selector + rate input + formula display.
    ///
    /// CHAT 17 — Per-Spec Targeting (Two-Part Selector):
    ///   OLD: SelectedSource (single GridSourceOption from flat list)
    ///   NEW: TargetSelection (SpecTargetSelection — multi-select specs + metric)
    ///
    ///   The OLD SelectedSource property is KEPT for backward compatibility
    ///   (existing saved invoices may deserialize into it), but MetricQty
    ///   now reads from TargetSelection.ComputedValue instead.
    ///   OnSourceValueChanged is retained so any legacy code that assigns
    ///   SelectedSource still gets proper change tracking.
    ///
    /// Special behaviour for TemplateKey == "POLISH":
    ///   - Uses PolishSubType instead of AvailableSources
    ///   - ALSO uses TargetSelection for per-spec filtering (Chat 17 A2 pattern)
    ///   - MetricQty is calculated from raw spec-group row dimensions per sub-type
    ///   - Custom sub-type shows extra description + manual qty inputs
    ///
    /// Special behaviour for TemplateKey == "DELIVERY" (Chat 7 Bug 4c hack):
    ///   - Always renders as 1 TRIP × Rate (per-invoice pricing)
    ///   - Ignores TargetSelection value
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

        /// <summary>Unit type — Sqm / Lm / Qty. Filters the metric options.</summary>
        public OtherChargeUnit Unit { get; set; }

        /// <summary>True if this is a Polish row (activates sub-type UI).</summary>
        public bool IsPolishRow => TemplateKey == "POLISH";

        /// <summary>
        /// True if this is a Delivery row (Chat 7 Bug 4c hack).
        /// When true, MetricQty is hardcoded to 1 (per-trip pricing).
        /// User's TargetSelection is ignored.
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

        // ── Spec-target selection (Chat 17 — NEW) ──────────────────────

        private SpecTargetSelection? _targetSelection;

        /// <summary>
        /// Two-part spec-target selector: which specs (checkboxes) + which
        /// metric (radio). Set by OtherChargesViewModel after row construction.
        /// Row subscribes to ComputedValue changes for live MetricQty updates.
        /// </summary>
        public SpecTargetSelection? TargetSelection
        {
            get => _targetSelection;
            set
            {
                if (_targetSelection == value) return;

                if (_targetSelection != null)
                    _targetSelection.PropertyChanged -= OnTargetSelectionChanged;

                _targetSelection = value;

                if (_targetSelection != null)
                    _targetSelection.PropertyChanged += OnTargetSelectionChanged;

                OnPropertyChanged();
                OnPropertyChanged(nameof(MetricQty));
                Recalculate();
            }
        }

        private void OnTargetSelectionChanged(
            object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SpecTargetSelection.ComputedValue)
                or nameof(SpecTargetSelection.SummaryText)
                or nameof(SpecTargetSelection.SelectedMetric)
                or null
                or "")
            {
                OnPropertyChanged(nameof(MetricQty));
                Recalculate();
                // Let the parent VM re-run polish LM when the user changes
                // which specs this row targets.
                OnPropertyChanged(nameof(TargetSelection));
            }
        }

        // ── Legacy grid source — for backward compat with old snapshots ────

        /// <summary>
        /// LEGACY: Available options filtered by Unit type. Kept for
        /// serialization/deserialization compatibility with older invoices.
        /// Not used by MetricQty in Chat 17 — TargetSelection is used instead.
        /// </summary>
        public ObservableCollection<GridSourceOption> AvailableSources { get; } = new();

        private GridSourceOption? _selectedSource;

        /// <summary>
        /// LEGACY: The chosen source. Kept for backward compat.
        /// MetricQty no longer reads from this. Change tracking is retained
        /// so any legacy code path that still assigns this value still works
        /// without silent failures.
        /// </summary>
        public GridSourceOption? SelectedSource
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
            }
        }

        private void OnSourceValueChanged(
            object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // LEGACY: preserved for safety. Chat 17 MetricQty uses
            // TargetSelection, not SelectedSource, so nothing needs
            // to fire here anymore. Kept as a no-op subscription so
            // future refactors can wire it back if needed.
        }

        // ── Polish-specific state ──────────────────────────────────────────

        /// <summary>Available polish sub-types (populated once).</summary>
        public ObservableCollection<PolishSubTypeMeta> AvailablePolishSubTypes { get; } = new();

        private PolishSubTypeMeta? _selectedPolishSubType;
        public PolishSubTypeMeta? SelectedPolishSubType
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
            set
            {
                if (_customPolishDescription == value) return;
                _customPolishDescription = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormulaDisplay));
            }
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
        /// OtherChargesViewModel.RecalculatePolishRowsImmediate() and
        /// pushed in via SetCalculatedPolishQty().
        /// Non-Custom sub-types get a computed LM based on the sub-type
        /// formula applied across all glass rows in the spec groups.
        /// </summary>
        private decimal _calculatedPolishQty;
        public decimal CalculatedPolishQty
        {
            get => _calculatedPolishQty;
            private set
            {
                if (_calculatedPolishQty == value) return;
                _calculatedPolishQty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MetricQty));
                Recalculate();
            }
        }

        /// <summary>
        /// Called externally by OtherChargesViewModel when spec groups
        /// or dimensions change. Pushes a newly-computed polish LM into
        /// this row so MetricQty reflects the latest calc.
        /// </summary>
        public void SetCalculatedPolishQty(decimal value)
        {
            CalculatedPolishQty = value;
        }

        // ── Metric qty (unified accessor) ──────────────────────────────────

        /// <summary>
        /// Live quantity read from one of these sources depending on row type:
        ///   - IsDeliveryRow   → 1m (Chat 7 Bug 4c hack — per-invoice)
        ///   - IsPolishRow + IsCustomPolish → CustomPolishQty (user manual)
        ///   - IsPolishRow + predefined sub-type → CalculatedPolishQty (auto)
        ///   - Everything else → TargetSelection.ComputedValue (Chat 17)
        ///
        /// ═══════════════ Bug 4c (Chat 7) — QUICK HACK ═══════════════
        /// DELIVERY is per-invoice, not per-piece — always = 1 trip.
        /// Ignores TargetSelection value entirely.
        /// Matches PDF/Excel export behavior.
        /// For multi-trip billing: user should use Custom Charges section.
        /// Proper fix (PER_ROUND source + editable qty) planned for Chat 8.
        /// ══════════════════════════════════════════════════════════════
        /// </summary>
        public decimal MetricQty
        {
            get
            {
                if (IsDeliveryRow) return 1m;

                if (IsPolishRow)
                {
                    return IsCustomPolish
                        ? _customPolishQty
                        : _calculatedPolishQty;
                }

                return _targetSelection?.ComputedValue ?? 0m;
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
            private set
            {
                if (_resultAmount == value) return;
                _resultAmount = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Full formula display for the RESULT column.
        /// Examples:
        ///   Normal:  "89.00 LM × 6.00 AED = 534.00 AED (Spec 1, 2 · LM1 · 89.00 lm)"
        ///   Polish:  "45.20 LM × 6.00 AED = 271.20 AED (ALL SIDE POLISH)"
        ///   Custom:  "12.50 LM × 8.00 AED = 100.00 AED (SPECIAL BEVELED EDGE)"
        ///   Delivery:"1 TRIP × 150.00 AED = 150.00 AED (PER TRIP - FIXED)"
        ///   Inactive:"DISABLED"
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
                    // Chat 17: hint sourced from TargetSelection summary
                    hint = _targetSelection == null
                        ? ""
                        : $"({_targetSelection.SummaryText})";
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

        /// <summary>
        /// Called externally when parent VM wants a manual refresh
        /// (e.g. after TargetSelection specs or metric values change).
        /// </summary>
        public void ForceRefresh()
        {
            OnPropertyChanged(nameof(MetricQty));
            Recalculate();
        }
    }
}