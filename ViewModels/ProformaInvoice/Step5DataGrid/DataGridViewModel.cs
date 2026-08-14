// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.cs           ║
// ║                                                                          ║
// ║  CHAT 20 FINAL — Multi-row selection + persistent Find/Replace.         ║
// ║                                                                          ║
// ║  NEW IN THIS VERSION:                                                    ║
// ║    SEL-01: IsSelected property on GlassItemRow (row highlight state)    ║
// ║    SEL-02: SpecGroup implements 6 new IExcelPasteable methods:          ║
// ║              ClearRowSelection, SelectAllRows, SetRowSelection,          ║
// ║              SelectRowRange, GetSelectedRows, FillDownAcrossRows        ║
// ║    FIND-01: ExecuteOpenFind uses callback pattern (persistent dialog)   ║
// ║    FIND-02: ExecuteOpenFindReplace uses callback pattern                ║
// ║    PRICE-01: ImportedPrice set from spec on FIRST pick, preserved       ║
// ║               thereafter — user can edit BaseRate without losing it     ║
// ║    PRICE-02: AddNewSpecGroup defaults to 0m (spec selection sets price) ║
// ║                                                                          ║
// ║  PRESERVED FROM PREVIOUS:                                                ║
// ║    ✅ Deferred row addition via DispatcherPriority.Loaded                ║
// ║    ✅ AddRows / RemoveRow / FindNext / FindAndReplace / FillDown         ║
// ║    ✅ IsHighlighted + HighlightRow (yellow flash)                        ║
// ║    ✅ Paste engine (cursor-column-aware, AddRangeSilent)                 ║
// ║    ✅ HOTFIX-5 spec autocomplete, HOTFIX-4 glass type auto-switch       ║
// ║    ✅ HOTFIX-1 BaseRate propagation to rows                              ║
// ║    ✅ Global Ctrl+F/H commands on DataGridViewModel                      ║
// ╚══════════════════════════════════════════════════════════════════════════╝

using ProGlassAutomation.Behaviors;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid.GrandTotalSummary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid
{
    // ════════════════════════════════════════════════════════════════════════
    // GRID COLUMN INDEX CONSTANTS
    // ════════════════════════════════════════════════════════════════════════

    public static class GridColumnIndex
    {
        public const int GlassRef = 0;
        public const int W1 = 1;
        public const int H1 = 2;
        public const int W2 = 3;
        public const int H2 = 4;
        public const int Qty = 5;
        public const int Rate = 6;
    }

    // ════════════════════════════════════════════════════════════════════════
    // CLASS: GlassItemRow
    // ════════════════════════════════════════════════════════════════════════

    public class GlassItemRow : ViewModelBase
    {
        // ── Serial number ──────────────────────────────────────────────────

        private int _serialNumber;
        public int SerialNumber
        {
            get => _serialNumber;
            set
            {
                if (_serialNumber == value) return;
                _serialNumber = value;
                OnPropertyChanged();
            }
        }

        // ── GlassRef ───────────────────────────────────────────────────────

        private string _glassRef = "";
        public string GlassRef
        {
            get => _glassRef;
            set
            {
                if (_glassRef == value) return;
                _glassRef = value;
                OnPropertyChanged();
            }
        }

        // ── Dimensions ────────────────────────────────────────────────────

        private int _w1;
        public int W1
        {
            get => _w1;
            set
            {
                if (_w1 == value) return;
                _w1 = value;
                OnPropertyChanged();
                RecalculateAll();
            }
        }

        private int _h1;
        public int H1
        {
            get => _h1;
            set
            {
                if (_h1 == value) return;
                _h1 = value;
                OnPropertyChanged();
                RecalculateAll();
            }
        }

        private int _w2;
        public int W2
        {
            get => _w2;
            set
            {
                if (_w2 == value) return;
                _w2 = value;
                OnPropertyChanged();
                RecalculateAll();
            }
        }

        private int _h2;
        public int H2
        {
            get => _h2;
            set
            {
                if (_h2 == value) return;
                _h2 = value;
                OnPropertyChanged();
                RecalculateAll();
            }
        }

        private int _qty = 1;
        public int Qty
        {
            get => _qty;
            set
            {
                if (_qty == value) return;
                _qty = value;
                OnPropertyChanged();
                RecalculateAll();
            }
        }

        // ── Calculated outputs ────────────────────────────────────────────

        private decimal _sqm1;
        public decimal Sqm1
        {
            get => _sqm1;
            private set
            {
                if (_sqm1 == value) return;
                _sqm1 = value;
                OnPropertyChanged();
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
            }
        }

        private decimal _sqm2;
        public decimal Sqm2
        {
            get => _sqm2;
            private set
            {
                if (_sqm2 == value) return;
                _sqm2 = value;
                OnPropertyChanged();
            }
        }

        private decimal _totalSqm2;
        public decimal TotalSqm2
        {
            get => _totalSqm2;
            private set
            {
                if (_totalSqm2 == value) return;
                _totalSqm2 = value;
                OnPropertyChanged();
            }
        }

        private decimal _lm1;
        public decimal Lm1
        {
            get => _lm1;
            private set
            {
                if (_lm1 == value) return;
                _lm1 = value;
                OnPropertyChanged();
            }
        }

        private decimal _lm2;
        public decimal Lm2
        {
            get => _lm2;
            private set
            {
                if (_lm2 == value) return;
                _lm2 = value;
                OnPropertyChanged();
            }
        }

        // ── Rate and Value ────────────────────────────────────────────────

        private decimal _rate;
        public decimal Rate
        {
            get => _rate;
            set
            {
                if (_rate == value) return;
                _rate = value;
                _value = Math.Round(_sqm1 * _qty * _rate, 2);
                OnPropertyChanged();
                OnPropertyChanged(nameof(Value));
            }
        }

        private decimal _value;
        public decimal Value
        {
            get => _value;
            private set
            {
                if (_value == value) return;
                _value = value;
                OnPropertyChanged();
            }
        }

        // ── Perimeter factor ──────────────────────────────────────────────

        private int _perimeterFactor = 2;
        public int PerimeterFactor
        {
            get => _perimeterFactor;
            set
            {
                if (_perimeterFactor == value) return;
                _perimeterFactor = value;
                OnPropertyChanged();
                RecalculateAll();
            }
        }

        // ── Flags ─────────────────────────────────────────────────────────

        internal bool SuppressRateAutoAdjust { get; set; } = false;

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(_glassRef) &&
            _w1 == 0 && _h1 == 0 && _w2 == 0 && _h2 == 0;

        // ── Highlight state (transient — for Find visual feedback) ─────

        private bool _isHighlighted;
        /// <summary>
        /// Transient visual highlight — set true briefly by SpecGroup.HighlightRow
        /// when Find locates this row. Cleared automatically after ~2 seconds.
        /// Bound to row background via DataTrigger for yellow flash.
        /// </summary>
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                if (_isHighlighted == value) return;
                _isHighlighted = value;
                OnPropertyChanged();
            }
        }

        // ── Selection state (for multi-row Ctrl+D fill down) ──────────

        private bool _isSelected;
        /// <summary>
        /// True when this row is part of the current selection.
        /// Set by DataGridNavigationBehavior via SpecGroup.SetRowSelection.
        /// Bound to row background via DataTrigger for blue selection highlight.
        /// Cleared by clicking on a TextBox, clicking empty area, or pressing Escape.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        // ── Calculations ──────────────────────────────────────────────────

        public void RecalculateAll()
        {
            decimal newSqm1 = _w1 > 0 && _h1 > 0
                ? SqmCalculator.CalculatePanel(_w1, _h1) : 0m;
            decimal newTotalSqm = Math.Round(newSqm1 * _qty, 3);

            decimal newSqm2 = _w2 > 0 && _h2 > 0
                ? SqmCalculator.CalculatePanel(_w2, _h2) : 0m;
            decimal newTotalSqm2 = Math.Round(newSqm2 * _qty, 3);

            decimal newLm1 = _w1 > 0 && _h1 > 0
                ? Math.Round(
                    ((decimal)_w1 + _h1) / 1000m * _perimeterFactor * _qty, 3)
                : 0m;
            decimal newLm2 = _w2 > 0 && _h2 > 0
                ? Math.Round(
                    ((decimal)_w2 + _h2) / 1000m * _perimeterFactor * _qty, 3)
                : 0m;

            decimal newValue = Math.Round(newSqm1 * _qty * _rate, 2);

            _sqm1 = newSqm1;
            _totalSqm = newTotalSqm;
            _sqm2 = newSqm2;
            _totalSqm2 = newTotalSqm2;
            _lm1 = newLm1;
            _lm2 = newLm2;
            _value = newValue;

            OnPropertyChanged(nameof(Sqm1));
            OnPropertyChanged(nameof(TotalSqm));
            OnPropertyChanged(nameof(Sqm2));
            OnPropertyChanged(nameof(TotalSqm2));
            OnPropertyChanged(nameof(Lm1));
            OnPropertyChanged(nameof(Lm2));
            OnPropertyChanged(nameof(Value));
        }

        public void SetAllSilent(
            string glassRef,
            int w1, int h1,
            int w2, int h2,
            int qty,
            int perimeterFactor,
            decimal rate)
        {
            _glassRef = glassRef;
            _w1 = w1;
            _h1 = h1;
            _w2 = w2;
            _h2 = h2;
            _qty = qty <= 0 ? 1 : qty;
            _perimeterFactor = perimeterFactor;
            _rate = rate;

            _sqm1 = _w1 > 0 && _h1 > 0
                ? SqmCalculator.CalculatePanel(_w1, _h1) : 0m;
            _totalSqm = Math.Round(_sqm1 * _qty, 3);

            _sqm2 = _w2 > 0 && _h2 > 0
                ? SqmCalculator.CalculatePanel(_w2, _h2) : 0m;
            _totalSqm2 = Math.Round(_sqm2 * _qty, 3);

            _lm1 = _w1 > 0 && _h1 > 0
                ? Math.Round(
                    ((decimal)_w1 + _h1) / 1000m * _perimeterFactor * _qty, 3)
                : 0m;
            _lm2 = _w2 > 0 && _h2 > 0
                ? Math.Round(
                    ((decimal)_w2 + _h2) / 1000m * _perimeterFactor * _qty, 3)
                : 0m;

            _value = Math.Round(_sqm1 * _qty * _rate, 2);
        }

        public void ApplyColumnValues(
            Dictionary<int, string> columnValues,
            int perimeterFactor,
            decimal surchargeRate,
            decimal baseRate)
        {
            foreach (var kv in columnValues)
            {
                switch (kv.Key)
                {
                    case GridColumnIndex.GlassRef:
                        _glassRef = kv.Value ?? "";
                        break;

                    case GridColumnIndex.W1:
                        if (int.TryParse(CleanNum(kv.Value), out int w1))
                            _w1 = w1;
                        break;

                    case GridColumnIndex.H1:
                        if (int.TryParse(CleanNum(kv.Value), out int h1))
                            _h1 = h1;
                        break;

                    case GridColumnIndex.W2:
                        if (int.TryParse(CleanNum(kv.Value), out int w2))
                            _w2 = w2;
                        break;

                    case GridColumnIndex.H2:
                        if (int.TryParse(CleanNum(kv.Value), out int h2))
                            _h2 = h2;
                        break;

                    case GridColumnIndex.Qty:
                        if (int.TryParse(CleanNum(kv.Value), out int qty)
                            && qty > 0)
                            _qty = qty;
                        break;

                    case GridColumnIndex.Rate:
                        if (decimal.TryParse(
                                CleanNum(kv.Value),
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out decimal rate)
                            && rate > 0)
                        {
                            _rate = rate;
                        }
                        break;
                }
            }

            _perimeterFactor = perimeterFactor;

            _sqm1 = _w1 > 0 && _h1 > 0
                ? SqmCalculator.CalculatePanel(_w1, _h1) : 0m;
            _totalSqm = Math.Round(_sqm1 * _qty, 3);

            _sqm2 = _w2 > 0 && _h2 > 0
                ? SqmCalculator.CalculatePanel(_w2, _h2) : 0m;
            _totalSqm2 = Math.Round(_sqm2 * _qty, 3);

            _lm1 = _w1 > 0 && _h1 > 0
                ? Math.Round(
                    ((decimal)_w1 + _h1) / 1000m * _perimeterFactor * _qty, 3)
                : 0m;
            _lm2 = _w2 > 0 && _h2 > 0
                ? Math.Round(
                    ((decimal)_w2 + _h2) / 1000m * _perimeterFactor * _qty, 3)
                : 0m;

            if (!SuppressRateAutoAdjust &&
                !columnValues.ContainsKey(GridColumnIndex.Rate))
            {
                _rate = _sqm1 >= 4m ? surchargeRate : baseRate;
            }

            _value = Math.Round(_sqm1 * _qty * _rate, 2);
        }

        public void FireAllChanged()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                FireAllChangedCore();
            else
                dispatcher.InvokeAsync(
                    FireAllChangedCore, DispatcherPriority.Background);
        }

        private void FireAllChangedCore()
        {
            OnPropertyChanged(nameof(SerialNumber));
            OnPropertyChanged(nameof(GlassRef));
            OnPropertyChanged(nameof(W1));
            OnPropertyChanged(nameof(H1));
            OnPropertyChanged(nameof(W2));
            OnPropertyChanged(nameof(H2));
            OnPropertyChanged(nameof(Qty));
            OnPropertyChanged(nameof(Sqm1));
            OnPropertyChanged(nameof(TotalSqm));
            OnPropertyChanged(nameof(Sqm2));
            OnPropertyChanged(nameof(TotalSqm2));
            OnPropertyChanged(nameof(Lm1));
            OnPropertyChanged(nameof(Lm2));
            OnPropertyChanged(nameof(Rate));
            OnPropertyChanged(nameof(Value));
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static string CleanNum(string s) =>
            s?.Trim().Replace(",", "").Replace(" ", "") ?? "0";
    }

    // ════════════════════════════════════════════════════════════════════════
    // CLASS: SpecGroup
    // ════════════════════════════════════════════════════════════════════════

    public class SpecGroup : ViewModelBase, IExcelPasteable
    {
        private readonly ObservableCollection<SavedSpecification> _specSource = new();
        private ICollectionView _filteredSpecsView;
        private HashSet<string> _sourceKeys = new(StringComparer.OrdinalIgnoreCase);
        private bool _allTypeSpecsMerged = false;
        private bool _isAutoSwitchingGlassType = false;
        private bool _isProgrammaticSpecChange = false;

        // ── Group identity ─────────────────────────────────────────────────

        private int _groupNumber;
        public int GroupNumber
        {
            get => _groupNumber;
            set
            {
                if (_groupNumber == value) return;
                _groupNumber = value;
                OnPropertyChanged();
            }
        }

        private string _groupName = "";
        public string GroupName
        {
            get => _groupName;
            set { _groupName = value; OnPropertyChanged(); }
        }

        // ── PRICING ───────────────────────────────────────────────────────

        private decimal _importedPrice;
        /// <summary>
        /// Fixed reference price imported from Step 4 (Spec &amp; Price Generator).
        /// Set ONCE on first spec pick — preserved thereafter even if user
        /// manually edits BaseRate. Displayed read-only in UI.
        /// </summary>
        public decimal ImportedPrice
        {
            get => _importedPrice;
            set
            {
                if (_importedPrice == value) return;
                _importedPrice = value;
                OnPropertyChanged();
            }
        }

        private decimal _baseRate;
        public decimal BaseRate
        {
            get => _baseRate;
            set
            {
                if (_baseRate == value) return;
                _baseRate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SurchargeRate));
                UpdateAllRowRates();
            }
        }

        private decimal _surchargePercent;
        public decimal SurchargePercent
        {
            get => _surchargePercent;
            set
            {
                if (_surchargePercent == value) return;
                _surchargePercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SurchargeRate));
                UpdateAllRowRates();
            }
        }

        public decimal SurchargeRate =>
            Math.Round(_baseRate * (1m + (_surchargePercent / 100m)), 2);

        public string SpecDisplayName => !string.IsNullOrWhiteSpace(SelectedSpecName)
            ? SelectedSpecName
            : (GroupName ?? $"Spec Group {GroupNumber}");

        // ── Glass type ────────────────────────────────────────────────────

        private string _selectedGlassType = "";
        public string SelectedGlassType
        {
            get => _selectedGlassType;
            set
            {
                if (_selectedGlassType == value) return;
                _selectedGlassType = value;
                OnPropertyChanged();
                UpdatePerimeterFactors();
                _ = LoadSpecsAsync();
            }
        }

        // ── Spec autocomplete ─────────────────────────────────────────────

        private string _selectedSpecName = "";
        public string SelectedSpecName
        {
            get => _selectedSpecName;
            set
            {
                var newValue = value ?? "";
                if (string.Equals(
                        _selectedSpecName, newValue,
                        StringComparison.Ordinal)) return;

                _selectedSpecName = newValue;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SpecDisplayName));

                if (!_isProgrammaticSpecChange)
                {
                    _isProgrammaticSpecChange = true;
                    try
                    {
                        if (!string.Equals(
                                _specTypedText, newValue,
                                StringComparison.Ordinal))
                        {
                            _specTypedText = newValue;
                            OnPropertyChanged(nameof(SpecTypedText));
                        }

                        var match = _specSource.FirstOrDefault(s =>
                            string.Equals(
                                s.SpecName, newValue,
                                StringComparison.OrdinalIgnoreCase));

                        if (!ReferenceEquals(_selectedSpecItem, match))
                        {
                            _selectedSpecItem = match;
                            OnPropertyChanged(nameof(SelectedSpecItem));
                        }
                    }
                    finally { _isProgrammaticSpecChange = false; }
                }

                TryAutoFillRate();
            }
        }

        private string _specTypedText = "";
        public string SpecTypedText
        {
            get => _specTypedText;
            set
            {
                var newValue = value ?? "";
                if (string.Equals(
                        _specTypedText, newValue,
                        StringComparison.Ordinal)) return;

                _specTypedText = newValue;
                OnPropertyChanged();

                if (_isProgrammaticSpecChange) return;

                _filteredSpecsView?.Refresh();
                IsSpecDropdownOpen =
                    !string.IsNullOrEmpty(_specTypedText) && ViewHasAnyItems();

                if (!string.IsNullOrEmpty(_specTypedText) &&
                    !_allTypeSpecsMerged && !ViewHasAnyItems())
                {
                    _ = LoadAllTypesFallbackAsync();
                }

                var exact = _specSource.FirstOrDefault(s =>
                    string.Equals(
                        s.SpecName, _specTypedText,
                        StringComparison.OrdinalIgnoreCase));

                _isProgrammaticSpecChange = true;
                try
                {
                    if (exact != null)
                    {
                        _selectedSpecName = exact.SpecName;
                        OnPropertyChanged(nameof(SelectedSpecName));
                        OnPropertyChanged(nameof(SpecDisplayName));

                        _selectedSpecItem = exact;
                        OnPropertyChanged(nameof(SelectedSpecItem));

                        TryAutoFillRate();
                        HandleSpecCommit(exact.SpecName);
                    }
                    else
                    {
                        _selectedSpecName = _specTypedText;
                        OnPropertyChanged(nameof(SelectedSpecName));
                        OnPropertyChanged(nameof(SpecDisplayName));

                        if (_selectedSpecItem != null)
                        {
                            _selectedSpecItem = null;
                            OnPropertyChanged(nameof(SelectedSpecItem));
                        }
                    }
                }
                finally { _isProgrammaticSpecChange = false; }
            }
        }

        private SavedSpecification _selectedSpecItem;
        public SavedSpecification SelectedSpecItem
        {
            get => _selectedSpecItem;
            set
            {
                if (ReferenceEquals(_selectedSpecItem, value)) return;
                _selectedSpecItem = value;
                OnPropertyChanged();

                if (value == null || _isProgrammaticSpecChange) return;

                _isProgrammaticSpecChange = true;
                try
                {
                    _specTypedText = value.SpecName;
                    OnPropertyChanged(nameof(SpecTypedText));

                    _selectedSpecName = value.SpecName;
                    OnPropertyChanged(nameof(SelectedSpecName));
                    OnPropertyChanged(nameof(SpecDisplayName));

                    IsSpecDropdownOpen = false;
                    TryAutoFillRate();
                    HandleSpecCommit(value.SpecName);
                }
                finally { _isProgrammaticSpecChange = false; }
            }
        }

        private bool _isSpecDropdownOpen;
        public bool IsSpecDropdownOpen
        {
            get => _isSpecDropdownOpen;
            set
            {
                if (_isSpecDropdownOpen == value) return;
                _isSpecDropdownOpen = value;
                OnPropertyChanged();
            }
        }

        public ICollectionView FilteredSpecs => _filteredSpecsView;

        // ── ItemRows ──────────────────────────────────────────────────────

        public BulkObservableCollection<GlassItemRow> ItemRows { get; } = new();

        // ── Bulk row generation ───────────────────────────────────────────

        private string _bulkRowCountText = "";
        public string BulkRowCountText
        {
            get => _bulkRowCountText;
            set
            {
                if (_bulkRowCountText == value) return;
                _bulkRowCountText = value;
                OnPropertyChanged();
            }
        }

        // ── Paste progress ────────────────────────────────────────────────

        private bool _isPasting;
        public bool IsPasting
        {
            get => _isPasting;
            private set
            {
                if (_isPasting == value) return;
                _isPasting = value;
                OnPropertyChanged();
            }
        }

        private int _pasteProgress;
        public int PasteProgress
        {
            get => _pasteProgress;
            private set
            {
                if (_pasteProgress == value) return;
                _pasteProgress = value;
                OnPropertyChanged();
            }
        }

        private string _pasteStatusText = "";
        public string PasteStatusText
        {
            get => _pasteStatusText;
            private set
            {
                if (_pasteStatusText == value) return;
                _pasteStatusText = value;
                OnPropertyChanged();
            }
        }

        // ── Toast ─────────────────────────────────────────────────────────

        private bool _isToastVisible;
        public bool IsToastVisible
        {
            get => _isToastVisible;
            private set
            {
                if (_isToastVisible == value) return;
                _isToastVisible = value;
                OnPropertyChanged();
            }
        }

        private string _toastMessage = "";
        public string ToastMessage
        {
            get => _toastMessage;
            private set
            {
                if (_toastMessage == value) return;
                _toastMessage = value;
                OnPropertyChanged();
            }
        }

        private bool _toastIsError;
        public bool ToastIsError
        {
            get => _toastIsError;
            private set
            {
                if (_toastIsError == value) return;
                _toastIsError = value;
                OnPropertyChanged();
            }
        }

        // ── Footer totals ─────────────────────────────────────────────────

        private int _totalRows;
        public int TotalRows
        {
            get => _totalRows;
            private set
            {
                if (_totalRows == value) return;
                _totalRows = value;
                OnPropertyChanged();
            }
        }

        private int _footerTotalQty;
        public int FooterTotalQty
        {
            get => _footerTotalQty;
            private set
            {
                if (_footerTotalQty == value) return;
                _footerTotalQty = value;
                OnPropertyChanged();
            }
        }

        private decimal _footerTotalSqm;
        public decimal FooterTotalSqm
        {
            get => _footerTotalSqm;
            private set
            {
                if (_footerTotalSqm == value) return;
                _footerTotalSqm = value;
                OnPropertyChanged();
            }
        }

        private decimal _footerTotalSqm2;
        public decimal FooterTotalSqm2
        {
            get => _footerTotalSqm2;
            private set
            {
                if (_footerTotalSqm2 == value) return;
                _footerTotalSqm2 = value;
                OnPropertyChanged();
            }
        }

        private decimal _footerTotalLm1;
        public decimal FooterTotalLm1
        {
            get => _footerTotalLm1;
            private set
            {
                if (_footerTotalLm1 == value) return;
                _footerTotalLm1 = value;
                OnPropertyChanged();
            }
        }

        private decimal _footerTotalLm2;
        public decimal FooterTotalLm2
        {
            get => _footerTotalLm2;
            private set
            {
                if (_footerTotalLm2 == value) return;
                _footerTotalLm2 = value;
                OnPropertyChanged();
            }
        }

        private decimal _footerTotalValue;
        public decimal FooterTotalValue
        {
            get => _footerTotalValue;
            private set
            {
                if (_footerTotalValue == value) return;
                _footerTotalValue = value;
                OnPropertyChanged();
            }
        }

        // ── Commands ──────────────────────────────────────────────────────

        public ICommand AddRowCommand { get; }
        public ICommand RemoveRowCommand { get; }
        public ICommand PasteExcelCommand { get; }
        public ICommand GenerateRowsCommand { get; }
        public ICommand OpenFindCommand { get; }
        public ICommand OpenFindReplaceCommand { get; }

        private bool _isBulkAdding = false;

        // ══════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════════

        public SpecGroup()
        {
            _filteredSpecsView = CollectionViewSource.GetDefaultView(_specSource);
            _filteredSpecsView.Filter = SpecFilterPredicate;

            ItemRows.CollectionChanged += OnItemRowsChanged;

            AddRowCommand = new RelayCommand(_ => AddNewRow());

            RemoveRowCommand = new RelayCommand(p =>
            {
                if (p is GlassItemRow r)
                {
                    r.PropertyChanged -= OnRowChanged;
                    ItemRows.Remove(r);
                }
            });

            PasteExcelCommand = new RelayCommand(
                _ => _ = PasteFromClipboardAsync());

            GenerateRowsCommand = new RelayCommand(
                _ => ExecuteGenerateRows());

            OpenFindCommand = new RelayCommand(
                _ => ExecuteOpenFind());

            OpenFindReplaceCommand = new RelayCommand(
                _ => ExecuteOpenFindReplace());
        }

        // ══════════════════════════════════════════════════════════════════
        //  IEXCELPASTEABLE IMPLEMENTATION — CORE
        // ══════════════════════════════════════════════════════════════════

        public void PasteFromText(
            string clipboardText,
            object startRowContext = null,
            int startColumnIndex = 0)
        {
            _ = PasteFromTextAsync(clipboardText, startRowContext, startColumnIndex);
        }

        public void AddRows(int count)
        {
            if (count <= 0) return;
            if (count > 10_000) count = 10_000;

            int factor = _selectedGlassType == "LAM" ? 4 : 2;
            var newRows = new List<GlassItemRow>(count);

            for (int i = 0; i < count; i++)
            {
                newRows.Add(new GlassItemRow
                {
                    SerialNumber = 0,
                    GlassRef = "",
                    Qty = 1,
                    Rate = SurchargeRate,
                    PerimeterFactor = factor
                });
            }

            _isBulkAdding = true;
            try
            {
                ItemRows.AddRangeSilent(newRows);

                foreach (var row in newRows)
                    row.PropertyChanged += OnRowChanged;
            }
            finally
            {
                _isBulkAdding = false;
                TotalRows = ItemRows.Count;
                RenumberRows();
                RecalculateFooter();
            }

            ItemRows.RaiseReset();
        }

        public void RemoveRow(object rowContext)
        {
            if (rowContext is not GlassItemRow row) return;
            row.PropertyChanged -= OnRowChanged;
            ItemRows.Remove(row);
        }

        // ══════════════════════════════════════════════════════════════════
        //  IEXCELPASTEABLE — FIND / REPLACE
        // ══════════════════════════════════════════════════════════════════

        public object FindNext(string searchText, object fromRowContext)
        {
            if (string.IsNullOrWhiteSpace(searchText)) return null;

            var term = searchText.Trim();
            int startIdx = 0;

            if (fromRowContext is GlassItemRow fromRow)
            {
                int idx = ItemRows.IndexOf(fromRow);
                if (idx >= 0) startIdx = idx + 1;
            }

            for (int i = startIdx; i < ItemRows.Count; i++)
            {
                if (RowMatchesSearch(ItemRows[i], term))
                    return ItemRows[i];
            }

            return null;
        }

        private static bool RowMatchesSearch(GlassItemRow row, string term)
        {
            if (row.GlassRef.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (row.W1.ToString().Contains(term)) return true;
            if (row.H1.ToString().Contains(term)) return true;
            if (row.W2.ToString().Contains(term)) return true;
            if (row.H2.ToString().Contains(term)) return true;
            if (row.Qty.ToString().Contains(term)) return true;
            if (row.Rate.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
                    .Contains(term)) return true;
            return false;
        }

        public int FindAndReplace(string findText, string replaceText)
        {
            if (string.IsNullOrWhiteSpace(findText)) return 0;

            int count = 0;
            var replace = replaceText ?? "";

            foreach (var row in ItemRows)
            {
                if (row.GlassRef.IndexOf(
                        findText, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    row.GlassRef = global::System.Text.RegularExpressions.Regex.Replace(
                        row.GlassRef,
                        global::System.Text.RegularExpressions.Regex.Escape(findText),
                        replace,
                        global::System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    count++;
                }

                if (row.W1.ToString() == findText &&
                    int.TryParse(replace, out int newW1))
                { row.W1 = newW1; count++; }

                if (row.H1.ToString() == findText &&
                    int.TryParse(replace, out int newH1))
                { row.H1 = newH1; count++; }

                if (row.W2.ToString() == findText &&
                    int.TryParse(replace, out int newW2))
                { row.W2 = newW2; count++; }

                if (row.H2.ToString() == findText &&
                    int.TryParse(replace, out int newH2))
                { row.H2 = newH2; count++; }

                if (row.Qty.ToString() == findText &&
                    int.TryParse(replace, out int newQty) && newQty > 0)
                { row.Qty = newQty; count++; }
            }

            if (count > 0)
            {
                ItemRows.RaiseReset();
            }

            return count;
        }

        // ══════════════════════════════════════════════════════════════════
        //  IEXCELPASTEABLE — FILL DOWN
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Single-column fill down. Copies focused column value from row above
        /// targetRow into targetRow. Used when 0 or 1 row is selected.
        /// </summary>
        public void FillDown(object targetRowContext, int columnIndex)
        {
            if (targetRowContext is not GlassItemRow targetRow) return;

            int targetIdx = ItemRows.IndexOf(targetRow);
            if (targetIdx <= 0) return;

            var sourceRow = ItemRows[targetIdx - 1];

            switch (columnIndex)
            {
                case GridColumnIndex.GlassRef:
                    targetRow.GlassRef = sourceRow.GlassRef;
                    break;
                case GridColumnIndex.W1:
                    targetRow.W1 = sourceRow.W1;
                    break;
                case GridColumnIndex.H1:
                    targetRow.H1 = sourceRow.H1;
                    break;
                case GridColumnIndex.W2:
                    targetRow.W2 = sourceRow.W2;
                    break;
                case GridColumnIndex.H2:
                    targetRow.H2 = sourceRow.H2;
                    break;
                case GridColumnIndex.Qty:
                    targetRow.Qty = sourceRow.Qty;
                    break;
                case GridColumnIndex.Rate:
                    targetRow.Rate = sourceRow.Rate;
                    break;
            }
        }

        /// <summary>
        /// Multi-row fill down.
        /// Copies source row's writable columns (GlassRef, W1, H1, W2, H2, Qty)
        /// into ALL target rows.
        /// Rate is excluded — it is auto-calculated by row logic based on Sqm1.
        /// Called by Ctrl+D when 2+ rows are selected.
        /// </summary>
        public void FillDownAcrossRows(object sourceRow, List<object> targetRows)
        {
            if (sourceRow is not GlassItemRow src) return;
            if (targetRows == null || targetRows.Count == 0) return;

            _isBulkAdding = true;
            try
            {
                foreach (var t in targetRows)
                {
                    if (t is not GlassItemRow target) continue;
                    if (ReferenceEquals(target, src)) continue;

                    // Copy writable columns only.
                    // Setters trigger RecalculateAll on each target row.
                    target.GlassRef = src.GlassRef;
                    target.W1 = src.W1;
                    target.H1 = src.H1;
                    target.W2 = src.W2;
                    target.H2 = src.H2;
                    target.Qty = src.Qty;
                }
            }
            finally
            {
                _isBulkAdding = false;

                // Apply Rate auto-adjust for each target row after bulk fill.
                // OnRowChanged was suppressed during the loop by _isBulkAdding,
                // so we manually apply the same rate logic here to ensure
                // target rows get the correct rate based on their new Sqm1.
                foreach (var t in targetRows)
                {
                    if (t is not GlassItemRow target) continue;
                    if (ReferenceEquals(target, src)) continue;
                    if (target.SuppressRateAutoAdjust) continue;

                    var targetRate = target.Sqm1 >= 4m ? SurchargeRate : BaseRate;
                    if (target.Rate != targetRate)
                        target.Rate = targetRate;
                }

                RecalculateFooter();
            }

            // Single Reset — batches all UI updates after multi-row change
            ItemRows.RaiseReset();
        }

        // ══════════════════════════════════════════════════════════════════
        //  IEXCELPASTEABLE — SELECTION SUPPORT
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Clears IsSelected on all rows.</summary>
        public void ClearRowSelection()
        {
            foreach (var row in ItemRows)
                row.IsSelected = false;
        }

        /// <summary>Sets IsSelected=true on all rows in this group.</summary>
        public void SelectAllRows()
        {
            foreach (var row in ItemRows)
                row.IsSelected = true;
        }

        /// <summary>Sets IsSelected on a specific row.</summary>
        public void SetRowSelection(object rowContext, bool isSelected)
        {
            if (rowContext is not GlassItemRow row) return;
            row.IsSelected = isSelected;
        }

        /// <summary>
        /// Selects rows from anchorRow to targetRow (inclusive).
        /// Clears selection outside this range.
        /// Range order is normalised — works whether anchor is before or after target.
        /// </summary>
        public void SelectRowRange(object anchorRow, object targetRow)
        {
            if (anchorRow is not GlassItemRow anchor) return;
            if (targetRow is not GlassItemRow target) return;

            int anchorIdx = ItemRows.IndexOf(anchor);
            int targetIdx = ItemRows.IndexOf(target);

            if (anchorIdx < 0 || targetIdx < 0) return;

            int start = Math.Min(anchorIdx, targetIdx);
            int end = Math.Max(anchorIdx, targetIdx);

            for (int i = 0; i < ItemRows.Count; i++)
            {
                ItemRows[i].IsSelected = (i >= start && i <= end);
            }
        }

        /// <summary>
        /// Returns currently selected rows in original collection order.
        /// </summary>
        public List<object> GetSelectedRows()
        {
            var result = new List<object>();
            foreach (var row in ItemRows)
            {
                if (row.IsSelected)
                    result.Add(row);
            }
            return result;
        }

        // ══════════════════════════════════════════════════════════════════
        //  HIGHLIGHT ROW — 2 second yellow flash after Find match
        // ══════════════════════════════════════════════════════════════════

        public void HighlightRow(object rowContext)
        {
            if (rowContext is not GlassItemRow row) return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            dispatcher.InvokeAsync(() =>
            {
                row.IsHighlighted = true;

                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(2000)
                };

                EventHandler tickHandler = null;
                tickHandler = (s, e) =>
                {
                    row.IsHighlighted = false;
                    if (s is DispatcherTimer t)
                    {
                        t.Stop();
                        t.Tick -= tickHandler;
                    }
                };

                timer.Tick += tickHandler;
                timer.Start();

            }, DispatcherPriority.Background);
        }

        // ══════════════════════════════════════════════════════════════════
        //  GENERATE ROWS
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteGenerateRows()
        {
            var text = (_bulkRowCountText ?? "").Trim();

            if (!int.TryParse(text, out int count) || count <= 0)
            {
                _ = ShowToastAsync(
                    "⚠  Please enter a valid number of rows to generate.",
                    isError: true);
                return;
            }

            if (count > 10_000)
            {
                _ = ShowToastAsync(
                    "⚠  Maximum 10,000 rows per spec group. " +
                    "Please use import for larger datasets.",
                    isError: true);
                return;
            }

            AddRows(count);
            BulkRowCountText = "";

            _ = ShowToastAsync($"✓  {count} row(s) generated successfully.");
        }

        // ══════════════════════════════════════════════════════════════════
        //  FIND / REPLACE HANDLERS — persistent dialogs via callback pattern
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteOpenFind()
        {
            object lastFoundRow = null;

            ProGlassAutomation.Behaviors.FindReplaceDialog.Show(
                isReplace: false,
                onFindNext: (searchText) =>
                {
                    if (string.IsNullOrWhiteSpace(searchText)) return;

                    var foundRow = FindNext(searchText, lastFoundRow);

                    // Wrap around
                    if (foundRow == null && lastFoundRow != null)
                        foundRow = FindNext(searchText, null);

                    if (foundRow == null)
                    {
                        _ = ShowToastAsync(
                            $"No rows found containing \"{searchText}\".",
                            isError: true);
                        lastFoundRow = null;
                    }
                    else
                    {
                        HighlightRow(foundRow);
                        _ = ShowToastAsync($"✓  Found match — highlighted.");
                        lastFoundRow = foundRow;
                    }
                });
        }

        private void ExecuteOpenFindReplace()
        {
            ProGlassAutomation.Behaviors.FindReplaceDialog.Show(
                isReplace: true,
                onFindNext: null,
                onReplaceAll: (findText, replaceText) =>
                {
                    if (string.IsNullOrWhiteSpace(findText)) return;

                    int count = FindAndReplace(findText, replaceText ?? "");

                    _ = ShowToastAsync(
                        count > 0
                            ? $"✓  Replaced {count} occurrence(s) successfully."
                            : "No matches found.",
                        isError: count == 0);
                });
        }

        // ══════════════════════════════════════════════════════════════════
        //  FILTER PREDICATE
        // ══════════════════════════════════════════════════════════════════

        private bool SpecFilterPredicate(object item)
        {
            if (item is not SavedSpecification spec) return false;
            var typed = _specTypedText?.Trim() ?? "";
            if (string.IsNullOrEmpty(typed)) return true;
            if (string.IsNullOrEmpty(spec.SpecName)) return false;
            return spec.SpecName.IndexOf(
                typed, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool ViewHasAnyItems()
        {
            if (_filteredSpecsView == null) return false;
            var enumerator = _filteredSpecsView.GetEnumerator();
            return enumerator.MoveNext();
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOAD SPECS
        // ══════════════════════════════════════════════════════════════════

        public async Task LoadSpecsAsync()
        {
            _specSource.Clear();
            _sourceKeys.Clear();
            _allTypeSpecsMerged = false;

            if (!_isAutoSwitchingGlassType &&
                !string.IsNullOrEmpty(_specTypedText))
            {
                _isProgrammaticSpecChange = true;
                try
                {
                    _specTypedText = "";
                    OnPropertyChanged(nameof(SpecTypedText));
                }
                finally { _isProgrammaticSpecChange = false; }
            }

            if (string.IsNullOrWhiteSpace(_selectedGlassType) || App.Specs == null)
            {
                _filteredSpecsView?.Refresh();
                return;
            }

            try
            {
                var specs = await App.Specs.GetAllAsync(_selectedGlassType);
                if (specs != null)
                {
                    foreach (var s in specs)
                    {
                        if (s?.SpecName == null) continue;
                        if (_sourceKeys.Add(s.SpecName))
                            _specSource.Add(s);
                    }
                }
                _filteredSpecsView?.Refresh();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpecGroup.LoadSpecs] {ex.Message}");
            }
        }

        public async Task LoadFilteredSpecsAsync() => await LoadSpecsAsync();
        internal void LoadFilteredSpecs() => _ = LoadSpecsAsync();

        private async Task LoadAllTypesFallbackAsync()
        {
            if (_allTypeSpecsMerged || App.Specs == null) return;
            _allTypeSpecsMerged = true;

            try
            {
                foreach (var type in new[] { "SGU", "DGU", "LAM", "DGU+LAM" })
                {
                    if (string.Equals(
                            type, _selectedGlassType,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    var specs = await App.Specs.GetAllAsync(type);
                    if (specs == null) continue;

                    foreach (var s in specs)
                    {
                        if (s?.SpecName == null) continue;
                        if (_sourceKeys.Add(s.SpecName))
                            _specSource.Add(s);
                    }
                }

                _filteredSpecsView?.Refresh();
                IsSpecDropdownOpen =
                    !string.IsNullOrEmpty(_specTypedText) && ViewHasAnyItems();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpecGroup.LoadAllTypesFallback] {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  SPEC COMMIT + GLASS TYPE AUTO-SWITCH
        // ══════════════════════════════════════════════════════════════════

        private void HandleSpecCommit(string specName)
        {
            if (string.IsNullOrWhiteSpace(specName)) return;

            var detected = GlassTypeDetector.Detect(specName);

            if (detected == "Unknown")
            {
                _ = ShowToastAsync(
                    "⚠  Cannot detect glass type for this specification. " +
                    "Please update it in Price & Specs Generator.",
                    isError: true);
                return;
            }

            if (!string.Equals(
                    detected, _selectedGlassType,
                    StringComparison.OrdinalIgnoreCase))
            {
                _isAutoSwitchingGlassType = true;
                try
                {
                    var previous = _selectedGlassType;
                    _selectedGlassType = detected;
                    OnPropertyChanged(nameof(SelectedGlassType));
                    UpdatePerimeterFactors();

                    _ = ShowToastAsync(
                        $"✓  Auto-switched glass type: {previous} → {detected}");

                    _ = LoadSpecsAsync();
                }
                finally { _isAutoSwitchingGlassType = false; }
            }
        }

        /// <summary>
        /// When a spec is picked from autocomplete, sets BaseRate from the
        /// spec's PricePerSqm. Also sets ImportedPrice on FIRST pick (when
        /// ImportedPrice is still 0m) — this preserves the imported reference
        /// even if user later manually edits BaseRate.
        /// </summary>
        private void TryAutoFillRate()
        {
            if (string.IsNullOrWhiteSpace(_selectedSpecName)) return;

            var match = _specSource.FirstOrDefault(s =>
                string.Equals(
                    s.SpecName, _selectedSpecName,
                    StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                // Set ImportedPrice ONCE — first time a spec is picked.
                // This preserves the original imported reference even if
                // the user later edits BaseRate manually.
                if (_importedPrice == 0m)
                {
                    ImportedPrice = match.PricePerSqm;
                }

                BaseRate = match.PricePerSqm;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  COLLECTION HANDLERS
        // ══════════════════════════════════════════════════════════════════

        private void OnItemRowsChanged(
            object s, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (GlassItemRow r in e.OldItems)
                    r.PropertyChanged -= OnRowChanged;

            if (e.NewItems != null)
                foreach (GlassItemRow r in e.NewItems)
                    r.PropertyChanged += OnRowChanged;

            if (_isBulkAdding) return;

            TotalRows = ItemRows.Count;
            RenumberRows();
            RecalculateFooter();
        }

        private void OnRowChanged(object s, PropertyChangedEventArgs e)
        {
            if (_isBulkAdding) return;

            if (e.PropertyName is
                nameof(GlassItemRow.Qty) or
                nameof(GlassItemRow.TotalSqm) or
                nameof(GlassItemRow.TotalSqm2) or
                nameof(GlassItemRow.Lm1) or
                nameof(GlassItemRow.Lm2) or
                nameof(GlassItemRow.Value))
            {
                RecalculateFooter();
            }

            if (e.PropertyName == nameof(GlassItemRow.Sqm1)
                && s is GlassItemRow row
                && !row.SuppressRateAutoAdjust)
            {
                var targetRate = row.Sqm1 >= 4m ? SurchargeRate : BaseRate;
                if (row.Rate != targetRate)
                    row.Rate = targetRate;
            }
        }

        private void RecalculateFooter()
        {
            if (ItemRows.Count == 0)
            {
                FooterTotalQty = 0;
                FooterTotalSqm = 0m;
                FooterTotalSqm2 = 0m;
                FooterTotalLm1 = 0m;
                FooterTotalLm2 = 0m;
                FooterTotalValue = 0m;
                return;
            }

            int qty = 0;
            decimal sqm = 0m, sqm2 = 0m,
                    lm1 = 0m, lm2 = 0m, val = 0m;

            foreach (var r in ItemRows)
            {
                qty += r.Qty;
                sqm += r.TotalSqm;
                sqm2 += r.TotalSqm2;
                lm1 += r.Lm1;
                lm2 += r.Lm2;
                val += r.Value;
            }

            FooterTotalQty = qty;
            FooterTotalSqm = Math.Round(sqm, 3);
            FooterTotalSqm2 = Math.Round(sqm2, 3);
            FooterTotalLm1 = Math.Round(lm1, 3);
            FooterTotalLm2 = Math.Round(lm2, 3);
            FooterTotalValue = Math.Round(val, 2);
        }

        // ══════════════════════════════════════════════════════════════════
        //  ROW MANAGEMENT
        // ══════════════════════════════════════════════════════════════════

        private void AddNewRow()
        {
            var row = new GlassItemRow
            {
                SerialNumber = ItemRows.Count + 1,
                GlassRef = "",
                Qty = 1,
                Rate = SurchargeRate,
                PerimeterFactor = _selectedGlassType == "LAM" ? 4 : 2
            };
            ItemRows.Add(row);
        }

        // ══════════════════════════════════════════════════════════════════
        //  PASTE ENGINE
        // ══════════════════════════════════════════════════════════════════

        private async Task PasteFromClipboardAsync()
        {
            await ShowToastAsync(
                "⚠  Please click on the cell where you want to start " +
                "pasting, then press Ctrl+V.",
                isError: true);
        }

        private async Task PasteFromTextAsync(
            string clipboardText,
            object startRowContext,
            int startColumnIndex)
        {
            if (startColumnIndex < 0)
            {
                await ShowToastAsync(
                    "⚠  Please click on the cell where you want to start " +
                    "pasting before pressing Ctrl+V.",
                    isError: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(clipboardText)) return;

            List<string[]> rawRows;
            try
            {
                rawRows = await Task.Run(() => SplitClipboardRows(clipboardText));
            }
            catch (Exception ex)
            {
                await ShowToastAsync($"Parse error: {ex.Message}", isError: true);
                return;
            }

            if (rawRows.Count == 0)
            {
                await ShowToastAsync("No valid rows found in clipboard.",
                    isError: true);
                return;
            }

            if (startColumnIndex > GridColumnIndex.Rate)
            {
                await ShowToastAsync(
                    "⚠  Cannot paste into a read-only column.",
                    isError: true);
                return;
            }

            bool showProgress = rawRows.Count >= 10;
            if (showProgress)
            {
                IsPasting = true;
                PasteProgress = 0;
                PasteStatusText = $"Preparing {rawRows.Count} rows…";
                await Task.Yield();
            }

            int startIdx = -1;
            if (startRowContext is GlassItemRow anchorRow)
            {
                int anchorIdx = ItemRows.IndexOf(anchorRow);
                if (anchorIdx >= 0)
                    startIdx = anchorIdx;
            }

            int factor = _selectedGlassType == "LAM" ? 4 : 2;
            var newAppendRows = new List<GlassItemRow>();

            _isBulkAdding = true;
            try
            {
                int parsedIdx = 0;

                if (startIdx >= 0)
                {
                    for (int i = startIdx;
                         i < ItemRows.Count && parsedIdx < rawRows.Count;
                         i++, parsedIdx++)
                    {
                        var columnValues = BuildColumnValues(
                            rawRows[parsedIdx], startColumnIndex);

                        ItemRows[i].ApplyColumnValues(
                            columnValues, factor, SurchargeRate, BaseRate);

                        if (showProgress && parsedIdx % 50 == 0)
                        {
                            PasteProgress =
                                (int)((double)parsedIdx / rawRows.Count * 80);
                            PasteStatusText =
                                $"Applying row {parsedIdx + 1} of {rawRows.Count}…";
                            await Task.Yield();
                        }
                    }
                }

                while (parsedIdx < rawRows.Count)
                {
                    var row = new GlassItemRow
                    {
                        Qty = 1,
                        Rate = SurchargeRate,
                        PerimeterFactor = factor
                    };

                    var columnValues = BuildColumnValues(
                        rawRows[parsedIdx], startColumnIndex);

                    row.ApplyColumnValues(
                        columnValues, factor, SurchargeRate, BaseRate);

                    newAppendRows.Add(row);
                    parsedIdx++;

                    if (showProgress && parsedIdx % 50 == 0)
                    {
                        PasteProgress =
                            (int)((double)parsedIdx / rawRows.Count * 80);
                        PasteStatusText =
                            $"Building row {parsedIdx} of {rawRows.Count}…";
                        await Task.Yield();
                    }
                }

                if (newAppendRows.Count > 0)
                {
                    ItemRows.AddRangeSilent(newAppendRows);

                    foreach (var row in newAppendRows)
                        row.PropertyChanged += OnRowChanged;
                }
            }
            finally
            {
                _isBulkAdding = false;
            }

            TotalRows = ItemRows.Count;
            RenumberRows();
            RecalculateFooter();

            if (showProgress)
            {
                PasteProgress = 90;
                PasteStatusText = "Refreshing display…";
                await Task.Yield();
            }

            ItemRows.RaiseReset();

            if (showProgress)
            {
                PasteProgress = 100;
                PasteStatusText = $"✓  {rawRows.Count} row(s) ready.";
                await Task.Yield();
                IsPasting = false;
                PasteProgress = 0;
                PasteStatusText = "";
            }

            await ShowToastAsync($"✓  Pasted {rawRows.Count} row(s) successfully.");
        }

        // ══════════════════════════════════════════════════════════════════
        //  CLIPBOARD PARSING
        // ══════════════════════════════════════════════════════════════════

        private static List<string[]> SplitClipboardRows(string clipboardText)
        {
            var lines = clipboardText.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.RemoveEmptyEntries);

            var result = new List<string[]>(lines.Length);

            foreach (var line in lines)
            {
                var parts = line.Split('\t');

                bool hasContent = false;
                foreach (var p in parts)
                {
                    if (!string.IsNullOrWhiteSpace(p))
                    {
                        hasContent = true;
                        break;
                    }
                }

                if (hasContent)
                    result.Add(parts);
            }

            return result;
        }

        private static Dictionary<int, string> BuildColumnValues(
            string[] clipboardRow,
            int startColumnIndex)
        {
            var result = new Dictionary<int, string>();

            for (int clipCol = 0; clipCol < clipboardRow.Length; clipCol++)
            {
                int gridCol = startColumnIndex + clipCol;
                if (gridCol > GridColumnIndex.Rate) break;
                result[gridCol] = clipboardRow[clipCol]?.Trim() ?? "";
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════

        private async Task ShowToastAsync(string message, bool isError = false)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            await dispatcher.InvokeAsync(() =>
            {
                ToastMessage = message;
                ToastIsError = isError;
                IsToastVisible = true;

                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(3500)
                };

                EventHandler tickHandler = null;
                tickHandler = (s, e) =>
                {
                    IsToastVisible = false;
                    ToastMessage = "";
                    if (s is DispatcherTimer t)
                    {
                        t.Stop();
                        t.Tick -= tickHandler;
                    }
                };

                timer.Tick += tickHandler;
                timer.Start();

            }, DispatcherPriority.Background);
        }

        private void RenumberRows()
        {
            for (int i = 0; i < ItemRows.Count; i++)
                ItemRows[i].SerialNumber = i + 1;
        }

        private void UpdatePerimeterFactors()
        {
            int factor = _selectedGlassType == "LAM" ? 4 : 2;
            foreach (var row in ItemRows)
                row.PerimeterFactor = factor;
        }

        private void UpdateAllRowRates()
        {
            foreach (var row in ItemRows)
            {
                if (row.SuppressRateAutoAdjust) continue;
                var targetRate = row.Sqm1 >= 4m ? SurchargeRate : BaseRate;
                if (row.Rate != targetRate)
                    row.Rate = targetRate;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // CLASS: DataGridViewModel
    // ════════════════════════════════════════════════════════════════════════

    public class DataGridViewModel : ViewModelBase
    {
        public string Title { get; } = "SPECIFICATION SUB-GROUPS";
        public string Subtitle { get; } =
            "Specify the number of unique glass specifications in this proforma.";

        public List<string> GlassTypeOptions { get; } =
            new() { "SGU", "DGU", "LAM", "DGU+LAM" };

        public ObservableCollection<SpecGroup> SpecGroups { get; }

        public int TotalSpecGroups => SpecGroups?.Count ?? 0;

        public ICommand AddSpecGroupCommand { get; }
        public ICommand RemoveSpecGroupCommand { get; }
        public ICommand OpenGlobalFindCommand { get; }
        public ICommand OpenGlobalFindReplaceCommand { get; }

        public GrandTotalSummaryViewModel SummaryViewModel { get; }

        public DataGridViewModel()
        {
            SpecGroups = new ObservableCollection<SpecGroup>();
            SpecGroups.CollectionChanged += (s, e) =>
                OnPropertyChanged(nameof(TotalSpecGroups));

            AddSpecGroupCommand = new RelayCommand(
                _ => AddNewSpecGroup());

            RemoveSpecGroupCommand = new RelayCommand(
                p =>
                {
                    if (p is SpecGroup g)
                    {
                        SpecGroups.Remove(g);
                        RenumberGroups();
                    }
                },
                p => SpecGroups.Count > 1
            );

            // Global Ctrl+F / Ctrl+H — route to first spec group.
            OpenGlobalFindCommand = new RelayCommand(_ =>
            {
                var firstGroup = SpecGroups.FirstOrDefault();
                firstGroup?.OpenFindCommand.Execute(null);
            });

            OpenGlobalFindReplaceCommand = new RelayCommand(_ =>
            {
                var firstGroup = SpecGroups.FirstOrDefault();
                firstGroup?.OpenFindReplaceCommand.Execute(null);
            });

            SummaryViewModel = new GrandTotalSummaryViewModel(SpecGroups);

            AddNewSpecGroup();
        }

        /// <summary>
        /// Creates a new spec group with zero defaults.
        /// ImportedPrice and BaseRate stay at 0 until user picks a spec —
        /// the spec's PricePerSqm sets both (ImportedPrice preserved thereafter
        /// even if user manually edits BaseRate).
        /// </summary>
        private void AddNewSpecGroup()
        {
            var group = new SpecGroup
            {
                GroupNumber = SpecGroups.Count + 1,
                GroupName = $"Spec Group {SpecGroups.Count + 1}",
                // Zero defaults — spec selection populates ImportedPrice + BaseRate.
                ImportedPrice = 0m,
                BaseRate = 0m,
                SurchargePercent = 0m,
                SelectedGlassType = "SGU"
            };

            SpecGroups.Add(group);

            // Deferred row addition — ensures ListView is bound before
            // the CollectionChanged event fires for the new row.
            Application.Current?.Dispatcher.InvokeAsync(
                () => group.AddRowCommand.Execute(null),
                DispatcherPriority.Loaded);
        }

        private void RenumberGroups()
        {
            for (int i = 0; i < SpecGroups.Count; i++)
            {
                SpecGroups[i].GroupNumber = i + 1;
                if (SpecGroups[i].GroupName?.StartsWith("Spec Group ") == true)
                    SpecGroups[i].GroupName = $"Spec Group {i + 1}";
            }
        }
    }
}