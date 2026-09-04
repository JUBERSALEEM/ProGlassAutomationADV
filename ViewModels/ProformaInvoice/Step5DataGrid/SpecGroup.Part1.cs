// ═══════════════════════════════════════════════════════════════════════
// FILE: SpecGroup.Part1.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Behaviors;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
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
        public partial class SpecGroup : ViewModelBase, IExcelPasteable
    {
            private readonly ObservableCollection<SavedSpecification> _specSource = new();
        private ICollectionView _filteredSpecsView;
        private HashSet<string> _sourceKeys = new(StringComparer.OrdinalIgnoreCase);
        private bool _allTypeSpecsMerged = false;
        private bool _isAutoSwitchingGlassType = false;
        private bool _isProgrammaticSpecChange = false;

        // A Step 4 handoff changes the type before the name. Do not run the
        // manual type-switch cleanup in the short interval between those two
        // assignments, otherwise it can clear/carry the incoming spec.
        private bool _isSynchronizingFromStep4;

        // Async catalogue reads are deliberately fire-and-forget. A monotonic
        // token prevents an older type/repository read from adding stale specs
        // after the user (or the Step 4 coordinator) has selected another type.
        private int _specLoadGeneration;
        private ISpecificationsRepository? _specifications;

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
        /// Step 4 → Step 5 synchronization markers. They travel with the
        /// invoice snapshot so a reopened quote retains the same ownership
        /// boundary: a manual Step 5 edit is never silently reclaimed by
        /// Step 4 on the next Continue.
        /// </summary>
        public string LastSyncedStep4SpecName { get; set; } = "";
        public string LastSyncedStep4GlassType { get; set; } = "";

        /// <summary>
        /// Explicit Step 5 intent survives a blank selection too. A manual
        /// type switch can deliberately clear a mismatched spec, and that
        /// blank must not be mistaken for a brand-new bootstrap group.
        /// Persisted alongside the sync markers for reopen safety.
        /// </summary>
        public bool HasManualStep5SpecOverride { get; set; }
        public bool HasManualStep5GlassTypeOverride { get; set; }

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

        // ══════════════════════════════════════════════════════════════
        //  PHASE 1-AUDIT — per-group production-cost provenance.
        //  Written by ApplyStep4PaneAndSheetMetadata at every Step 4 → 5
        //  handoff; consumed by Step 9's production rows with live-panel
        //  fallback so single-section invoices behave exactly as before.
        //  Null = "no Step 4 production rates captured for this group".
        // ══════════════════════════════════════════════════════════════

        private decimal? _step4SpacerPricePerSqm;
        public decimal? Step4SpacerPricePerSqm
        {
            get => _step4SpacerPricePerSqm;
            set
            {
                if (_step4SpacerPricePerSqm == value) return;
                _step4SpacerPricePerSqm = value;
                OnPropertyChanged();
            }
        }

        private decimal? _step4InterlayerCostPerSqm;
        public decimal? Step4InterlayerCostPerSqm
        {
            get => _step4InterlayerCostPerSqm;
            set
            {
                if (_step4InterlayerCostPerSqm == value) return;
                _step4InterlayerCostPerSqm = value;
                OnPropertyChanged();
            }
        }

        private decimal? _step4CuttingCostPerSqm;
        public decimal? Step4CuttingCostPerSqm
        {
            get => _step4CuttingCostPerSqm;
            set
            {
                if (_step4CuttingCostPerSqm == value) return;
                _step4CuttingCostPerSqm = value;
                OnPropertyChanged();
            }
        }

        private decimal? _step4TemperingCostPerSqm;
        public decimal? Step4TemperingCostPerSqm
        {
            get => _step4TemperingCostPerSqm;
            set
            {
                if (_step4TemperingCostPerSqm == value) return;
                _step4TemperingCostPerSqm = value;
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

                if (!_isAutoSwitchingGlassType && !_isSynchronizingFromStep4)
                {
                    // An explicit type choice means Step 5 now owns both
                    // parts of the selection. Clearing the provenance markers
                    // lets the coordinator distinguish it from a failed old
                    // bootstrap handoff (DGU name + default SGU type).
                    LastSyncedStep4GlassType = "";
                    LastSyncedStep4SpecName = "";
                    if (!string.IsNullOrWhiteSpace(previous))
                    {
                        HasManualStep5GlassTypeOverride = true;
                        // A manual type switch can clear/carry the name below;
                        // keep that resulting blank under operator ownership.
                        HasManualStep5SpecOverride = true;
                    }
                    ResetSpecSelectionForManualGlassTypeChange(previous, next);
                }

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

                if (!_isProgrammaticSpecChange && !_isSynchronizingFromStep4)
                {
                    LastSyncedStep4SpecName = "";
                    HasManualStep5SpecOverride = true;
                }

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

                if (!string.Equals(newValue, LastSyncedStep4SpecName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    LastSyncedStep4SpecName = "";
                    HasManualStep5SpecOverride = true;
                }

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

                if (!string.Equals(value.SpecName, LastSyncedStep4SpecName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    LastSyncedStep4SpecName = "";
                    HasManualStep5SpecOverride = true;
                }

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
        /// Applies the full, per-section Step 4 selection to an owned Step 5
        /// group. This is intentionally a direct snapshot handoff instead of
        /// waiting for SpecSheetRegistry: Step 4's catalogue write is async,
        /// while Continue must immediately preserve the selected sheets,
        /// pane prices, colours and U-insert seed for this exact group.
        /// </summary>
        public void ApplyStep4PricingSnapshot(Step4PricingSnapshot snapshot)
        {
            if (snapshot == null) return;

            string glassType = NormalizeSupportedGlassType(snapshot.SelectedGlassTypeCode);
            bool incomingSpecChangesSelection =
                !string.IsNullOrWhiteSpace(snapshot.GeneratedSpecName)
                && !string.Equals(_selectedSpecName, snapshot.GeneratedSpecName,
                    StringComparison.OrdinalIgnoreCase);

            _isSynchronizingFromStep4 = true;
            try
            {
                HasManualStep5SpecOverride = false;
                HasManualStep5GlassTypeOverride = false;
                // Type MUST be assigned before the spec name. The normal
                // setter protects manual changes by clearing a mismatched name;
                // the synchronization guard above keeps that behavior from
                // affecting this atomic Step 4 handoff.
                if (glassType.Length > 0)
                    SelectedGlassType = glassType;

                if (!string.IsNullOrWhiteSpace(snapshot.GeneratedSpecName))
                    SelectedSpecName = snapshot.GeneratedSpecName;

                ApplyStep4PaneAndSheetMetadata(snapshot, glassType);
            }
            finally
            {
                _isSynchronizingFromStep4 = false;
            }

            // Keep provenance inside the atomic handoff. Besides avoiding a
            // transient type/name mismatch, this makes any future caller of
            // this public method retain Step 4 ownership on the next refresh.
            LastSyncedStep4SpecName = string.IsNullOrWhiteSpace(snapshot.GeneratedSpecName)
                ? string.Empty
                : snapshot.GeneratedSpecName;
            LastSyncedStep4GlassType = glassType;

            if (snapshot.EstimatedRatePerSqm > 0m)
            {
                BaseRate = snapshot.EstimatedRatePerSqm;
                if (incomingSpecChangesSelection || ImportedPrice <= 0m)
                    ImportedPrice = snapshot.EstimatedRatePerSqm;
            }
        }

        private void ApplyStep4PaneAndSheetMetadata(
            Step4PricingSnapshot snapshot,
            string glassType)
        {
            // Always clear old provenance first. A group can move from DGU to
            // SGU/LAM and must never retain a previous inner sheet or U-insert.
            OuterSheetWidthMm = 0;
            OuterSheetHeightMm = 0;
            InnerSheetWidthMm = 0;
            InnerSheetHeightMm = 0;
            OuterSheetPricePerSqm = 0m;
            InnerSheetPricePerSqm = 0m;
            UInsertPricePerSqm = 0m;
            // PHASE 1-AUDIT: production-rate provenance resets alongside —
            // a build switch must never leak section 1's spacer rate into
            // section 2's SGU group.
            Step4SpacerPricePerSqm = null;
            Step4InterlayerCostPerSqm = null;
            Step4CuttingCostPerSqm = null;
            Step4TemperingCostPerSqm = null;

            switch (glassType)
            {
                case "SGU":
                {
                    var sgu = snapshot.Sgu ?? new SguPricingPanelSnapshot();
                    OuterGlassColor = sgu.GlassColor ?? "";
                    InnerGlassColor = "";
                    OuterSheetWidthMm = sgu.SheetWidthMm;
                    OuterSheetHeightMm = sgu.SheetHeightMm;
                    OuterSheetPricePerSqm = sgu.BasePricePerSqm;
                    Step4CuttingCostPerSqm = sgu.CuttingCostPerSqm;
                    Step4TemperingCostPerSqm = sgu.TemperingCostPerSqm;
                    break;
                }

                case "DGU":
                {
                    var dgu = snapshot.Dgu ?? new DguPricingPanelSnapshot();
                    OuterGlassColor = dgu.OuterGlassColor ?? "";
                    InnerGlassColor = dgu.InnerGlassColor ?? "";
                    OuterSheetWidthMm = dgu.OuterSheetWidthMm;
                    OuterSheetHeightMm = dgu.OuterSheetHeightMm;
                    InnerSheetWidthMm = dgu.InnerSheetWidthMm;
                    InnerSheetHeightMm = dgu.InnerSheetHeightMm;
                    OuterSheetPricePerSqm = dgu.OuterBasePrice;
                    InnerSheetPricePerSqm = dgu.InnerBasePrice;
                    UInsertPricePerSqm = HasUInsertExtra(dgu.SpacerExtra)
                        ? dgu.UInsertPricePerSqm
                        : 0m;
                    Step4SpacerPricePerSqm = dgu.SpacerPrice;
                    break;
                }

                case "LAM":
                {
                    var lam = snapshot.Lam ?? new LamPricingPanelSnapshot();
                    OuterGlassColor = lam.OuterGlassColor ?? "";
                    InnerGlassColor = lam.InnerGlassColor ?? "";
                    OuterSheetWidthMm = lam.OuterSheetWidthMm;
                    OuterSheetHeightMm = lam.OuterSheetHeightMm;
                    InnerSheetWidthMm = lam.InnerSheetWidthMm;
                    InnerSheetHeightMm = lam.InnerSheetHeightMm;
                    OuterSheetPricePerSqm = lam.OuterBasePrice;
                    InnerSheetPricePerSqm = lam.InnerBasePrice;
                    // LAM's interlayer is the PVB material rate; cutting and
                    // tempering are stored as the summed pane-pair rates —
                    // exactly the quantities Step 9's LAM branch adds.
                    Step4InterlayerCostPerSqm = lam.PvbMaterialRate;
                    Step4CuttingCostPerSqm =
                        lam.OuterCuttingCost + lam.InnerCuttingCost;
                    Step4TemperingCostPerSqm =
                        lam.OuterTemperingCost + lam.InnerTemperingCost;
                    break;
                }

                case "DGU+LAM":
                {
                    var dguLam = snapshot.DguLam ?? new DguLamPricingPanelSnapshot();

                    // SpecGroup has two colour fields. For the three-pane
                    // hybrid they deliberately model the laminated pair; the
                    // Step 6 source builder reads the physical outer/lam outer/
                    // lam inner values independently from this same snapshot.
                    OuterGlassColor = dguLam.LamOuterColor ?? "";
                    InnerGlassColor = dguLam.LamInnerColor ?? "";
                    // Interlayer is costed at the laminator BUY price — the
                    // sell spread belongs to the quoted rate, not to cost.
                    Step4SpacerPricePerSqm = dguLam.SpacerPrice;
                    Step4InterlayerCostPerSqm = dguLam.InterlayerBuyPrice;
                    Step4CuttingCostPerSqm =
                        dguLam.OuterCuttingCost
                        + dguLam.LamOuterCuttingCost
                        + dguLam.LamInnerCuttingCost;
                    Step4TemperingCostPerSqm =
                        dguLam.OuterTemperingCost
                        + dguLam.LamOuterTemperingCost
                        + dguLam.LamInnerTemperingCost;
                    break;
                }

                default:
                    // A malformed/legacy type should not leave colour data
                    // from the previously selected build on screen.
                    OuterGlassColor = "";
                    InnerGlassColor = "";
                    break;
            }
        }

        private static string NormalizeSupportedGlassType(string? value)
        {
            string type = (value ?? "").Trim().ToUpperInvariant();
            return type is "SGU" or "DGU" or "LAM" or "DGU+LAM" ? type : "";
        }

        private static bool HasUInsertExtra(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && value.IndexOf("U-INSERT", StringComparison.OrdinalIgnoreCase) >= 0;

        public void ApplyLaminateColors(string? outerColor, string? innerColor)
        {
            OuterGlassColor = outerColor ?? "";
            InnerGlassColor = innerColor ?? "";
        }

        /// <summary>
        /// Public entry point for Step 6's RefreshWorkspace to force a
        /// re-extraction of outer/inner glass colors from the current
        /// spec name. A non-empty Step 4 colour is retained when legacy or
        /// free-typed spec text cannot be parsed; blanking it would erase the
        /// only reliable outer/inner source identity for wastage.
        /// </summary>
        public void RefreshColorsFromSpecName() => RefreshGlassColorsFromSpecName();

        private void RefreshGlassColorsFromSpecName()
        {
            if (string.IsNullOrWhiteSpace(_selectedSpecName))
            {
                OuterGlassColor = "";
                InnerGlassColor = "";
                return;
            }

            var panes = PnLSpecParser.GlassMatches(_selectedSpecName);
            if (panes.Count == 0)
                return; // Preserve the explicit Step 4 colours for unparseable text.

            string effectiveType = GlassTypeDetector.Detect(_selectedSpecName);
            if (effectiveType == "Unknown")
                effectiveType = NormalizeSupportedGlassType(_selectedGlassType);

            // DGU+LAM has three panes. The group fields intentionally represent
            // the laminated pair; its physical outer pane is retained by the
            // Step 4 snapshot and resolved separately in the wastage builder.
            int outerIndex = effectiveType == "DGU+LAM" && panes.Count >= 3 ? 1 : 0;
            int innerIndex = effectiveType == "DGU+LAM" && panes.Count >= 3
                ? 2
                : panes.Count - 1;

            string parsedOuter = (panes[outerIndex].Groups["color"].Value ?? "").Trim();
            string parsedInner = (panes[innerIndex].Groups["color"].Value ?? "").Trim();

            if (!string.IsNullOrWhiteSpace(parsedOuter))
                OuterGlassColor = parsedOuter;

            if (panes.Count > 1 && !string.IsNullOrWhiteSpace(parsedInner))
                InnerGlassColor = parsedInner;
            else if (effectiveType == "SGU")
                InnerGlassColor = "";
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
                    $"[SpecGroup] sheet selection not adopted: {ExceptionMessages.Deep(ex)}");
            }
        }

        /// <summary>
        /// Injected specifications store (replaces the App-level static).
        /// A restored group receives its repository after its selected type;
        /// trigger a guarded reload here so that ordering cannot leave the
        /// Step 5 catalogue permanently empty.
        /// </summary>
        public ISpecificationsRepository? Specifications
        {
            get => _specifications;
            set
            {
                if (ReferenceEquals(_specifications, value)) return;
                _specifications = value;

                // Invalidate an in-flight request from a previous repository.
                _specLoadGeneration++;

                if (_specifications != null
                    && !string.IsNullOrWhiteSpace(_selectedGlassType))
                {
                    _ = LoadSpecsAsync();
                }
            }
        }

        // INSTANT CATALOGUE SYNC (2026-09-02).
        //
        // Held as a field so the delegate has a stable identity and can be
        // detached. Subscribed WEAKLY: SpecGroups are dropped via
        // SpecGroups.Clear()/Remove() with no Dispose() call anywhere, so a
        // direct handler on a static event would root every group of every
        // invoice ever opened. The handler only keeps a WeakReference to
        // this instance and unhooks itself once the target is collected.
        private EventHandler? _specCatalogHandler;

        private void HookSpecCatalogNotifications()
        {
            var weakSelf = new WeakReference<SpecGroup>(this);

            _specCatalogHandler = (_, __) =>
            {
                if (!weakSelf.TryGetTarget(out var self))
                {
                    // Target is gone; stop listening so the closure dies too.
                    if (_specCatalogHandler != null)
                        Services.ProformaInvoice.SpecificationsRepository
                            .SpecCatalogNotifier.Changed -= _specCatalogHandler;
                    return;
                }

                // NotifyChanged can fire on a background thread (the write is
                // async) while _specSource is bound to the dropdown, so the
                // reload must be marshalled.
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null || dispatcher.CheckAccess())
                    _ = self.LoadSpecsAsync();
                else
                    dispatcher.BeginInvoke(new Action(() => _ = self.LoadSpecsAsync()));
            };

            Services.ProformaInvoice.SpecificationsRepository
                .SpecCatalogNotifier.Changed += _specCatalogHandler;
        }

        public SpecGroup()
        {
            _filteredSpecsView = CollectionViewSource.GetDefaultView(_specSource);
            _filteredSpecsView.Filter = SpecFilterPredicate;

            HookSpecCatalogNotifications();

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
    }
}
