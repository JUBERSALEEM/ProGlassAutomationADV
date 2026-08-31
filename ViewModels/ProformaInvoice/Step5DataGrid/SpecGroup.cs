// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.cs
// PROJECT: ProGlassAutomation
// MODULE: Proforma Invoice — Step 5 data grid
//
// SpecGroup.cs extracted from the large DataGridViewModel file (Phase 11/12
// God-VM split) — behaviour unchanged, relocated for organisation only.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Behaviors;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid.GrandTotalSummary;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;
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
    // PARTIAL CLASS (senior audit — future safe extraction seam)
    // ─────────────────────────────────────────────────────────
    // Declared partial so a follow-up PR can move regions into
    // sibling files (SpecGroup.Paste.cs, SpecGroup.FindReplace.cs,
    // SpecGroup.Selection.cs, SpecGroup.Rendering.cs) without
    // touching this file's git blame or its 700+ commits of
    // audit history. Splitting today would move blocks whose
    // semantics are stable; the file's real cost is search,
    // not risk.
    public partial class SpecGroup : ViewModelBase, IExcelPasteable
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
            set
            {
                if (_groupName == value) return;
                _groupName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SpecDisplayName));
            }
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

        /// <summary>
        /// Ephemeral Step 4 → Step 5 background sync markers. Not persisted:
        /// they only tell the shell whether a group still mirrors the last
        /// auto-synced Step 4 section or has been manually changed in Step 5.
        /// </summary>
        public string LastSyncedStep4SpecName { get; set; } = "";
        public string LastSyncedStep4GlassType { get; set; } = "";

        private string _outerGlassColor = "";
        /// <summary>Step 4 outer / laminated-outer color for this spec.</summary>
        public string OuterGlassColor
        {
            get => _outerGlassColor;
            set
            {
                var next = value ?? "";
                if (_outerGlassColor == next) return;
                _outerGlassColor = next;
                OnPropertyChanged();
            }
        }

        private string _innerGlassColor = "";
        /// <summary>Step 4 inner / laminated-inner color for this spec.</summary>
        public string InnerGlassColor
        {
            get => _innerGlassColor;
            set
            {
                var next = value ?? "";
                if (_innerGlassColor == next) return;
                _innerGlassColor = next;
                OnPropertyChanged();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  PHASE G — SHEET PROVENANCE (Step 4 → Step 5 → Step 9)
        //
        //  These carry the stock sheet the user actually CHOSE in Step 4
        //  so the profit calculation in Step 9 costs that sheet, not one
        //  it re-guesses from the store. Zero means "not chosen" and
        //  Step 9 then falls back (store lookup, then shop default) while
        //  telling the user which source it used.
        //
        //  Display-only in Step 5: the grid never edits these.
        // ══════════════════════════════════════════════════════════════

        private int _outerSheetWidthMm;
        /// <summary>Chosen OUTER stock sheet width in mm (0 = not chosen).</summary>
        public int OuterSheetWidthMm
        {
            get => _outerSheetWidthMm;
            set
            {
                if (_outerSheetWidthMm == value) return;
                _outerSheetWidthMm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OuterSheetDisplay));
                OnPropertyChanged(nameof(HasOuterSheet));
                OnPropertyChanged(nameof(SheetSelectionSummary));
                OnPropertyChanged(nameof(HasSheetSelectionSummary));
            }
        }

        private int _outerSheetHeightMm;
        /// <summary>Chosen OUTER stock sheet height in mm (0 = not chosen).</summary>
        public int OuterSheetHeightMm
        {
            get => _outerSheetHeightMm;
            set
            {
                if (_outerSheetHeightMm == value) return;
                _outerSheetHeightMm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OuterSheetDisplay));
                OnPropertyChanged(nameof(HasOuterSheet));
                OnPropertyChanged(nameof(SheetSelectionSummary));
                OnPropertyChanged(nameof(HasSheetSelectionSummary));
            }
        }

        private int _innerSheetWidthMm;
        /// <summary>Chosen INNER stock sheet width in mm (DGU only).</summary>
        public int InnerSheetWidthMm
        {
            get => _innerSheetWidthMm;
            set
            {
                if (_innerSheetWidthMm == value) return;
                _innerSheetWidthMm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InnerSheetDisplay));
                OnPropertyChanged(nameof(HasInnerSheet));
                OnPropertyChanged(nameof(SheetSelectionSummary));
                OnPropertyChanged(nameof(HasSheetSelectionSummary));
            }
        }

        private int _innerSheetHeightMm;
        /// <summary>Chosen INNER stock sheet height in mm (DGU only).</summary>
        public int InnerSheetHeightMm
        {
            get => _innerSheetHeightMm;
            set
            {
                if (_innerSheetHeightMm == value) return;
                _innerSheetHeightMm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InnerSheetDisplay));
                OnPropertyChanged(nameof(HasInnerSheet));
                OnPropertyChanged(nameof(SheetSelectionSummary));
                OnPropertyChanged(nameof(HasSheetSelectionSummary));
            }
        }

        private decimal _outerSheetPricePerSqm;
        public decimal OuterSheetPricePerSqm
        {
            get => _outerSheetPricePerSqm;
            set
            {
                if (_outerSheetPricePerSqm == value) return;
                _outerSheetPricePerSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SheetSelectionSummary));
                OnPropertyChanged(nameof(HasSheetSelectionSummary));
            }
        }

        private decimal _innerSheetPricePerSqm;
        public decimal InnerSheetPricePerSqm
        {
            get => _innerSheetPricePerSqm;
            set
            {
                if (_innerSheetPricePerSqm == value) return;
                _innerSheetPricePerSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SheetSelectionSummary));
                OnPropertyChanged(nameof(HasSheetSelectionSummary));
            }
        }

        private decimal _uInsertPricePerSqm;
        /// <summary>
        /// Step 4 seeded U-Insert rate for this spec. Step 5 does not render
        /// it; Step 6 advanced mode consumes it when the spec carries a
        /// U-Insert spacer extra.
        /// </summary>
        public decimal UInsertPricePerSqm
        {
            get => _uInsertPricePerSqm;
            set
            {
                if (_uInsertPricePerSqm == value) return;
                _uInsertPricePerSqm = value;
                OnPropertyChanged();
            }
        }

        /// <summary>True when an outer stock sheet was chosen in Step 4.</summary>
        public bool HasOuterSheet =>
            OuterSheetWidthMm > 0 && OuterSheetHeightMm > 0;

        /// <summary>True when an inner stock sheet was chosen in Step 4.</summary>
        public bool HasInnerSheet =>
            InnerSheetWidthMm > 0 && InnerSheetHeightMm > 0;

        /// <summary>e.g. "3210 × 2250 · 7.2225 SQM", or "—".</summary>
        public string OuterSheetDisplay => HasOuterSheet
            ? $"{OuterSheetWidthMm} × {OuterSheetHeightMm} · " +
              $"{(OuterSheetWidthMm * (decimal)OuterSheetHeightMm) / 1_000_000m:0.####} SQM"
            : "—";

        /// <summary>e.g. "3210 × 2250 · 7.2225 SQM", or "—".</summary>
        public string InnerSheetDisplay => HasInnerSheet
            ? $"{InnerSheetWidthMm} × {InnerSheetHeightMm} · " +
              $"{(InnerSheetWidthMm * (decimal)InnerSheetHeightMm) / 1_000_000m:0.####} SQM"
            : "—";

        /// <summary>
        /// Human-readable sheet provenance carried from Step 4 so Step 5 can
        /// show exactly which third-party stock sheet this spec will use.
        /// </summary>
        public string SheetSelectionSummary
        {
            get
            {
                var parts = new List<string>();
                if (HasOuterSheet)
                    parts.Add(OuterSheetPricePerSqm > 0m
                        ? $"Outer sheet {OuterSheetDisplay} @ AED {OuterSheetPricePerSqm:N2}/SQM"
                        : $"Outer sheet {OuterSheetDisplay}");
                if (HasInnerSheet)
                    parts.Add(InnerSheetPricePerSqm > 0m
                        ? $"Inner sheet {InnerSheetDisplay} @ AED {InnerSheetPricePerSqm:N2}/SQM"
                        : $"Inner sheet {InnerSheetDisplay}");
                return parts.Count == 0 ? "" : string.Join("  ·  ", parts);
            }
        }

        public bool HasSheetSelectionSummary =>
            !string.IsNullOrWhiteSpace(SheetSelectionSummary);

        // ── Glass type ────────────────────────────────────────────────────

        private string _selectedGlassType = "";
        public string SelectedGlassType
        {
            get => _selectedGlassType;
            set
            {
                string next = value ?? "";
                if (_selectedGlassType == next) return;

                string previous = _selectedGlassType;
                _selectedGlassType = next;
                OnPropertyChanged();
                UpdatePerimeterFactors();

                if (!_isAutoSwitchingGlassType)
                    ResetSpecSelectionForManualGlassTypeChange(previous, next);

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
                RefreshGlassColorsFromSpecName();
                OnPropertyChanged(nameof(SheetSelectionSummary));
                OnPropertyChanged(nameof(HasSheetSelectionSummary));

                // PHASE G: adopt the stock sheet chosen for this spec in
                // Step 4. A miss (spec from an earlier session) leaves the
                // fields at zero and Step 9 falls back, stating the source.
                AdoptSheetSelection(newValue);

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

                        RefreshGlassColorsFromSpecName();
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
                    RefreshGlassColorsFromSpecName();
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

        /// <summary>
        /// PHASE G — pulls the Step 4 sheet choice for this spec name out
        /// of SpecSheetRegistry. Only overwrites when a selection exists,
        /// so a restored invoice keeps its persisted sheet. Never throws.
        /// </summary>
        public void ApplyLaminateColors(string? outerColor, string? innerColor)
        {
            OuterGlassColor = outerColor ?? "";
            InnerGlassColor = innerColor ?? "";
        }

        /// <summary>
        /// Public entry point for Step 6's RefreshWorkspace to force a
        /// re-extraction of outer/inner glass colors from the current
        /// spec name. Ensures wastage suggestions always have color data
        /// even when the spec group was populated before Step 6 opened.
        /// </summary>
        public void RefreshColorsFromSpecName() => RefreshGlassColorsFromSpecName();

        private void RefreshGlassColorsFromSpecName()
        {
            if (string.Equals(_selectedGlassType, "LAM", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_selectedGlassType, "DGU+LAM", StringComparison.OrdinalIgnoreCase))
            {
                LaminateGlassMatcher.ExtractColorsFromSpecName(
                    _selectedSpecName, _selectedGlassType,
                    out string lamOuter, out string lamInner);
                OuterGlassColor = lamOuter;
                InnerGlassColor = lamInner;
                return;
            }

            var panes = PnLSpecParser.GlassMatches(_selectedSpecName);
            OuterGlassColor = panes.Count > 0
                ? (panes[0].Groups["color"].Value ?? "").Trim()
                : "";
            InnerGlassColor = panes.Count > 1
                ? (panes[^1].Groups["color"].Value ?? "").Trim()
                : "";
        }

        private void AdoptSheetSelection(string specName)
        {
            try
            {
                // A miss is normal (spec from an earlier session). Reset to
                // zero so Step 9 / Step 6 fall back honestly instead of
                // inheriting the previous spec's sheet or U-Insert rate.
                OuterSheetWidthMm = 0;
                OuterSheetHeightMm = 0;
                InnerSheetWidthMm = 0;
                InnerSheetHeightMm = 0;
                OuterSheetPricePerSqm = 0m;
                InnerSheetPricePerSqm = 0m;
                UInsertPricePerSqm = 0m;

                var selection = ProGlassAutomation.Services.ProformaInvoice
                    .SpecSheetRegistry.TryGet(specName);

                if (selection == null) return;

                if (selection.HasOuter)
                {
                    OuterSheetWidthMm = selection.OuterWidthMm;
                    OuterSheetHeightMm = selection.OuterHeightMm;
                }

                if (selection.HasInner)
                {
                    InnerSheetWidthMm = selection.InnerWidthMm;
                    InnerSheetHeightMm = selection.InnerHeightMm;
                }

                OuterSheetPricePerSqm = selection.OuterPricePerSqm;
                InnerSheetPricePerSqm = selection.InnerPricePerSqm;
                UInsertPricePerSqm = selection.UInsertPricePerSqm;
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[SpecGroup] sheet selection not adopted: {ex.Message}");
            }
        }

        public SpecGroup()
        {
            _filteredSpecsView = CollectionViewSource.GetDefaultView(_specSource);
            _filteredSpecsView.Filter = SpecFilterPredicate;

            ItemRows.CollectionChanged += OnItemRowsChanged;

            AddRowCommand = new RelayCommand(_ => AddNewRow());

            RemoveRowCommand = new RelayCommand(p =>
            {
                if (p is not GlassItemRow r) return;
                // Keep one empty workspace row so the grid never collapses.
                if (ItemRows.Count <= 1)
                {
                    r.GlassRef = "";
                    r.W1 = 0; r.H1 = 0; r.W2 = 0; r.H2 = 0;
                    r.Qty = 1;
                    return;
                }
                r.PropertyChanged -= OnRowChanged;
                ItemRows.Remove(r);
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

        public int ApplyImportedRows(
            System.Collections.Generic.IReadOnlyList<ProGlassAutomation.Services.ProformaInvoice.Step5Grid.CutListImportRow> rows)
        {
            if (rows == null || rows.Count == 0) return 0;
            int factor = _selectedGlassType == "LAM" ? 4 : 2;
            int added = 0;
            _isBulkAdding = true;
            try
            {
                bool firstEmpty = ItemRows.Count == 1
                    && string.IsNullOrWhiteSpace(ItemRows[0].GlassRef)
                    && ItemRows[0].W1 == 0 && ItemRows[0].H1 == 0;

                for (int i = 0; i < rows.Count; i++)
                {
                    var src = rows[i];
                    GlassItemRow dest;
                    if (i == 0 && firstEmpty)
                    {
                        dest = ItemRows[0];
                    }
                    else
                    {
                        dest = new GlassItemRow
                        {
                            Qty = 1,
                            Rate = SurchargeRate,
                            PerimeterFactor = factor
                        };
                        ItemRows.Add(dest);
                        dest.PropertyChanged += OnRowChanged;
                    }

                    dest.GlassRef = src.GlassRef ?? "";
                    dest.W1 = src.W1;
                    dest.H1 = src.H1;
                    dest.W2 = src.W2;
                    dest.H2 = src.H2;
                    dest.Qty = src.Qty > 0 ? src.Qty : 1;
                    added++;
                }
            }
            finally
            {
                _isBulkAdding = false;
                TotalRows = ItemRows.Count;
                RenumberRows();
                RecalculateFooter();
            }
            ItemRows.RaiseReset();
            return added;
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
            if (ItemRows.Count <= 1)
            {
                row.GlassRef = "";
                row.W1 = 0; row.H1 = 0; row.W2 = 0; row.H2 = 0;
                row.Qty = 1;
                return;
            }
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
                    row.GlassRef = System.Text.RegularExpressions.Regex.Replace(
                        row.GlassRef,
                        System.Text.RegularExpressions.Regex.Escape(findText),
                        replace,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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

            bool keepCommittedTypedText =
                !string.IsNullOrEmpty(_selectedSpecName) &&
                string.Equals(_specTypedText, _selectedSpecName,
                    StringComparison.Ordinal);

            if (!_isAutoSwitchingGlassType &&
                !string.IsNullOrEmpty(_specTypedText) &&
                !keepCommittedTypedText)
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
                AppLogService.Debug($"[SpecGroup.LoadSpecs] {ex.Message}");
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
                AppLogService.Debug($"[SpecGroup.LoadAllTypesFallback] {ex.Message}");
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
                // ──────────────────────────────────────────────────────────
                //  MULTI-PANE SPEC WITHOUT SPACER/INTERLAYER DETAIL
                //
                //  The spec name lists several panes but omits the build
                //  markers (e.g. "6mm Clear FT + 6mm Clear FT" instead of
                //  "... + 12mm ASP"). The text cannot say DGU vs LAM vs
                //  hybrid, so the authoritative glass type (Step 4 selection
                //  or the previous type) is KEPT. Guessing SGU here auto-
                //  switched the field to single glazing and Step 6 advanced
                //  automation stopped suggesting the inner pane / cavity
                //  charges — the "auto charge is not triggering" report.
                // ──────────────────────────────────────────────────────────
                if (GlassTypeDetector.IsMultiPaneWithoutBuildMarkers(specName))
                {
                    int paneCount = PnLSpecParser.GlassMatches(specName).Count;
                    string kept = string.IsNullOrWhiteSpace(_selectedGlassType)
                        ? "(not set)"
                        : _selectedGlassType;

                    _ = ShowToastAsync(
                        $"Spec lists {paneCount} glass panes without " +
                        "spacer/interlayer detail — keeping glass type " +
                        $"{kept}. Add the build markers to the spec name " +
                        "to auto-switch it.");
                    return;
                }

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
                    SelectedGlassType = detected;
                    RefreshGlassColorsFromSpecName();

                    _ = ShowToastAsync(
                        $"✓  Auto-switched glass type: {previous} → {detected}");
                }
                finally { _isAutoSwitchingGlassType = false; }
            }
        }

        /// <summary>
        /// Manual glass-type changes must not leave a ready spec from a
        /// different build family on screen. That stale pairing made Step 6
        /// advanced automation read the wrong type/spec combination and miss
        /// charges that were clearly present in the spec text.
        ///
        /// We only clear when the current spec is recognisably of another
        /// type. Unknown free-typed text is left alone so the operator can
        /// keep editing it.
        /// </summary>
        private void ResetSpecSelectionForManualGlassTypeChange(string previousType, string nextType)
        {
            if (string.IsNullOrWhiteSpace(_selectedSpecName)
                || string.IsNullOrWhiteSpace(nextType)
                || string.Equals(previousType, nextType, StringComparison.OrdinalIgnoreCase))
                return;

            string detected = GlassTypeDetector.Detect(_selectedSpecName);
            if (detected == "Unknown"
                || string.Equals(detected, nextType, StringComparison.OrdinalIgnoreCase))
                return;

            _isProgrammaticSpecChange = true;
            try
            {
                _selectedSpecName = "";
                _specTypedText = "";
                _selectedSpecItem = null;
                OnPropertyChanged(nameof(SelectedSpecName));
                OnPropertyChanged(nameof(SpecTypedText));
                OnPropertyChanged(nameof(SelectedSpecItem));
                OnPropertyChanged(nameof(SpecDisplayName));
                IsSpecDropdownOpen = false;
                ImportedPrice = 0m;
                BaseRate = 0m;
                RefreshGlassColorsFromSpecName();
                AdoptSheetSelection("");
            }
            finally { _isProgrammaticSpecChange = false; }
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
                rawRows = await Task.Run(() => TextParsing.SplitClipboardRows(clipboardText));
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
                        var columnValues = TextParsing.BuildColumnValues(
                            rawRows[parsedIdx], startColumnIndex, GridColumnIndex.Rate);

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

                    var columnValues = TextParsing.BuildColumnValues(
                        rawRows[parsedIdx], startColumnIndex, GridColumnIndex.Rate);

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
}
