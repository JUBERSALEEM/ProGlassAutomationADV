// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Sheet Profitability Analysis (Step 9 — SHEET DETAIL tab)
//
// MODEL (proforma-based, NOT stock-based)
//   A job buys whole glass sheets and cuts them. The bought sheets split
//   into exactly two parts:
//
//       BUY SHEET  =  USED SHEET  +  WASTAGE (leftover)
//
//   • BUY SHEET   — sheets bought, priced at the authoritative Step 9
//                   glass row amount (BuildGlassRowAsync already applies
//                   the Step 6 SHEET QTY when the operator set one).
//   • USED SHEET  — whole sheets required to cover the required area
//                   (cut SQM + wastage %), via PnLMeasurement.
//   • WASTAGE     — the LEFTOVER of cutting = BUY − USED (sheets + area +
//                   material value). It is DERIVED, never purchased. There
//                   is deliberately no separate "wastage sheet" line and
//                   no scrap/remnant value.
//
//   The Step 6 WASTAGE CHARGE (WastageRow.ResultAmount) is a separate
//   monetary fee the operator set. It is read from the matching Step 6
//   row's SOURCE (pane side: outer / inner / lam outer / lam inner /
//   middle), its SQM, its SHEET QTY and its rate, then cross-checked
//   against the derived usage:
//
//       bought < used        → SHORTFALL  (Step 6 SHEET QTY too low)
//       charge > 0, no waste → REVIEW     (charged but nothing leftover)
//       waste > 0, no charge → NO CHARGE  (leftover but nothing billed)
//
// FORMULAS
//   Required area  = cut SQM × (1 + wastage % / 100)
//   Used sheets    = ⌈required area ÷ sheet SQM⌉
//   Wastage sheets = buy sheets − used sheets           (raw; negative =
//                                                         shortfall)
//   Buy price      = Step 9 glass row amount            (authoritative)
//   Used price     = required area × sheet rate
//   Wastage value  = buy price − used price             (material leftover)
//   Step 6 charge  = Σ share of WastageRow.ResultAmount per pane source
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Services.Export.Core;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss
{
    /// <summary>
    /// Sheet usage breakdown with a per-pane Step 6 wastage cross-check.
    /// </summary>
    public class SheetProfitabilityViewModel : ViewModelBase
    {
        private readonly ProfitLossStepViewModel _parentViewModel;

        public SheetProfitabilityViewModel(ProfitLossStepViewModel parentViewModel)
        {
            _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));

            SheetBreakdown = new ObservableCollection<SheetProfitRow>();
            ShortfallMessages = new ObservableCollection<string>();
            ReviewMessages = new ObservableCollection<string>();

            RefreshCommand = new RelayCommand(_ => RefreshAnalysis());
            SelectRowCommand = new RelayCommand(_ => SetSelectedRow(_));
            BackCommand = new RelayCommand(_ => BackToSheetList());
            ExportCsvCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(() => ExportAsync(ExportFormat.Csv), "Step9 Sheet CSV"));
            ExportExcelCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(() => ExportAsync(ExportFormat.Excel), "Step9 Sheet Excel"));
            ExportPdfCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(() => ExportAsync(ExportFormat.Pdf), "Step9 Sheet PDF"));

            // Initial load
            RefreshAnalysis();
        }

        // ══════════════════════════════════════════════════════════════
        //  COLLECTIONS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Per-pane sheet rows.</summary>
        public ObservableCollection<SheetProfitRow> SheetBreakdown { get; }

        /// <summary>Hard errors: a pane buys fewer sheets than it needs.</summary>
        public ObservableCollection<string> ShortfallMessages { get; }

        /// <summary>Softer cross-check notes against the Step 6 setup.</summary>
        public ObservableCollection<string> ReviewMessages { get; }

        // ══════════════════════════════════════════════════════════════
        //  TOTALS
        // ══════════════════════════════════════════════════════════════

        private decimal _totalBuySheets;
        public decimal TotalBuySheets
        {
            get => _totalBuySheets;
            private set => SetTotals(ref _totalBuySheets, value, nameof(TotalBuySheetsDisplay));
        }

        public string TotalBuySheetsDisplay => $"{TotalBuySheets:0}";

        private decimal _totalUsedSheets;
        public decimal TotalUsedSheets
        {
            get => _totalUsedSheets;
            private set => SetTotals(ref _totalUsedSheets, value, nameof(TotalUsedSheetsDisplay));
        }

        public string TotalUsedSheetsDisplay => $"{TotalUsedSheets:0}";

        private decimal _totalWastageSheets;
        /// <summary>Total leftover sheets (shortfalls are not subtracted).</summary>
        public decimal TotalWastageSheets
        {
            get => _totalWastageSheets;
            private set => SetTotals(ref _totalWastageSheets, value, nameof(TotalWastageSheetsDisplay));
        }

        public string TotalWastageSheetsDisplay => $"{TotalWastageSheets:0}";

        private decimal _totalBuySheetArea;
        public decimal TotalBuySheetArea
        {
            get => _totalBuySheetArea;
            private set => SetTotals(ref _totalBuySheetArea, value, nameof(TotalBuySheetAreaDisplay));
        }

        public string TotalBuySheetAreaDisplay => $"{TotalBuySheetArea:0.##} SQM";

        private decimal _totalUsedArea;
        public decimal TotalUsedArea
        {
            get => _totalUsedArea;
            private set => SetTotals(ref _totalUsedArea, value, nameof(TotalUsedAreaDisplay));
        }

        public string TotalUsedAreaDisplay => $"{TotalUsedArea:0.##} SQM";

        private decimal _totalBuyPrice;
        public decimal TotalBuyPrice
        {
            get => _totalBuyPrice;
            private set => SetTotals(ref _totalBuyPrice, value, nameof(TotalBuyPriceDisplay));
        }

        public string TotalBuyPriceDisplay => $"AED {TotalBuyPrice:N2}";

        private decimal _totalUsedPrice;
        public decimal TotalUsedPrice
        {
            get => _totalUsedPrice;
            private set => SetTotals(ref _totalUsedPrice, value, nameof(TotalUsedPriceDisplay));
        }

        public string TotalUsedPriceDisplay => $"AED {TotalUsedPrice:N2}";

        private decimal _totalWastageValue;
        /// <summary>Material value of the leftover glass (buy − used).</summary>
        public decimal TotalWastageValue
        {
            get => _totalWastageValue;
            private set => SetTotals(ref _totalWastageValue, value, nameof(TotalWastageValueDisplay));
        }

        public string TotalWastageValueDisplay => $"AED {TotalWastageValue:N2}";

        private decimal _totalStep6Charge;
        /// <summary>Sum of the Step 6 wastage charges read per pane.</summary>
        public decimal TotalStep6Charge
        {
            get => _totalStep6Charge;
            private set => SetTotals(ref _totalStep6Charge, value, nameof(TotalStep6ChargeDisplay));
        }

        public string TotalStep6ChargeDisplay => $"AED {TotalStep6Charge:N2}";

        private decimal _usageEfficiencyPercent;
        public decimal UsageEfficiencyPercent
        {
            get => _usageEfficiencyPercent;
            private set => SetTotals(ref _usageEfficiencyPercent, value, nameof(UsageEfficiencyDisplay));
        }

        public string UsageEfficiencyDisplay => $"{UsageEfficiencyPercent:0.#}%";

        /// <summary>Reconciliation balance: buy − used − wastage value (0 when healthy).</summary>
        public decimal ReconciliationBalance => Math.Round(TotalBuyPrice - TotalUsedPrice - TotalWastageValue, 2);

        public string ReconciliationBalanceDisplay => $"{ReconciliationBalance:+#,##0.00;−#,##0.00;0.00}";

        /// <summary>True when the material identity is in balance (no shortfall).</summary>
        public bool IsInBalance => ReconciliationBalance >= 0m;

        /// <summary>Used price as a share of the buy price.</summary>
        public decimal UsedPriceSharePercent => TotalBuyPrice > 0m
            ? Math.Round(TotalUsedPrice / TotalBuyPrice * 100m, 1)
            : 0m;

        public string UsedPriceShareDisplay => $"{UsedPriceSharePercent:0.#}%";

        /// <summary>Wastage material value as a share of the buy price.</summary>
        public decimal WastageValueSharePercent => TotalBuyPrice > 0m
            ? Math.Round(TotalWastageValue / TotalBuyPrice * 100m, 1)
            : 0m;

        public string WastageValueShareDisplay => $"{WastageValueSharePercent:0.#}%";

        /// <summary>Step 6 wastage charge as a share of the buy price.</summary>
        public decimal Step6ChargeSharePercent => TotalBuyPrice > 0m
            ? Math.Round(TotalStep6Charge / TotalBuyPrice * 100m, 1)
            : 0m;

        public string Step6ChargeShareDisplay => $"{Step6ChargeSharePercent:0.#}%";

        private bool _hasShortfalls;
        public bool HasShortfalls
        {
            get => _hasShortfalls;
            private set
            {
                if (_hasShortfalls == value) return;
                _hasShortfalls = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDiagnostics));
            }
        }

        private bool _hasReviewItems;
        public bool HasReviewItems
        {
            get => _hasReviewItems;
            private set
            {
                if (_hasReviewItems == value) return;
                _hasReviewItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDiagnostics));
            }
        }

        /// <summary>True when any pane needs a second look at Step 6.</summary>
        public bool HasDiagnostics => HasShortfalls || HasReviewItems;

        private void SetTotals(ref decimal field, decimal value, string displayName)
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(displayName);
        }

        private string _statusMessage = "Press REFRESH to analyze sheet profitability.";
        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (_statusMessage == value) return;
                _statusMessage = value ?? "";
                OnPropertyChanged();
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Refresh command.</summary>
        public ICommand RefreshCommand { get; }

        /// <summary>Opens the full-screen detail for the clicked sheet row.</summary>
        public ICommand SelectRowCommand { get; }

        /// <summary>Returns from the detail screen back to the sheet list.</summary>
        public ICommand BackCommand { get; }

        // ══════════════════════════════════════════════════════════════
        //  EXPORT (CSV / Excel / PDF) — Step 9 report output
        // ══════════════════════════════════════════════════════════════

        public ICommand ExportCsvCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportPdfCommand { get; }

        private bool _isExporting;
        /// <summary>True while an export runs (disables the toolbar).</summary>
        public bool IsExporting
        {
            get => _isExporting;
            private set
            {
                if (_isExporting == value) return;
                _isExporting = value;
                OnPropertyChanged();
            }
        }

        private async Task ExportAsync(ExportFormat format)
        {
            if (IsExporting || SheetBreakdown.Count == 0) return;
            IsExporting = true;
            try
            {
                var rows = SheetBreakdown.ToList();
                rows.Add(BuildTotalRow());

                var result = await Step9ReportExport.RunAsync(
                    format, rows, "Sheet Profitability",
                    Step9ReportExport.SheetHeaders,
                    Step9ReportExport.SheetMap,
                    Step9ReportExport.SheetMapExcel,
                    $"Sheet_Profitability_{DateTime.Now:yyyyMMdd}");

                if (result.Success)
                    StatusMessage = $"Exported {format} — {result.SavedPath}";
                else if (!result.Cancelled)
                    App.Dialogs.Warning(result.ErrorMessage ?? "Export failed.", "Export");
            }
            catch (Exception ex)
            {
                App.Dialogs.Error($"Export failed: {ExceptionMessages.Deep(ex)}", "Export");
            }
            finally
            {
                IsExporting = false;
            }
        }

        /// <summary>Synthetic TOTAL row appended to the export.</summary>
        private SheetProfitRow BuildTotalRow()
        {
            return new SheetProfitRow
            {
                SpecName = "",
                GlassTypeLabel = "TOTAL",
                PaneSide = "",
                SheetSize = "",
                SheetSqm = SheetBreakdown.Sum(r => r.SheetSqm),
                BuySheets = SheetBreakdown.Sum(r => r.BuySheets),
                UsedSheets = SheetBreakdown.Sum(r => r.UsedSheets),
                WastageSheets = SheetBreakdown.Sum(r => r.WastageSheets),
                BuySheetArea = SheetBreakdown.Sum(r => r.BuySheetArea),
                UsedArea = SheetBreakdown.Sum(r => r.UsedArea),
                WastageArea = SheetBreakdown.Sum(r => r.WastageArea),
                PricePerSqm = 0m,
                BuySheetPrice = SheetBreakdown.Sum(r => r.BuySheetPrice),
                UsedSheetPrice = SheetBreakdown.Sum(r => r.UsedSheetPrice),
                WastageValue = SheetBreakdown.Sum(r => r.WastageValue),
                Step6Charge = SheetBreakdown.Sum(r => r.Step6Charge),
                Step6SourceLabel = "",
                Step6SourceSqm = 0m,
                Step6SheetQuantity = 0,
                Step6Rate = 0m,
                UsageEfficiencyPercent = 0m,
                DiagnosticMessage = $"All sheet rows ({SheetBreakdown.Count})"
            };
        }

        private SheetProfitRow? _selectedRow;
        public SheetProfitRow? SelectedRow
        {
            get => _selectedRow;
            private set
            {
                if (ReferenceEquals(_selectedRow, value)) return;
                _selectedRow = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedRow));
            }
        }

        /// <summary>True while a sheet row's detail screen is open.</summary>
        public bool HasSelectedRow => SelectedRow != null;

        // ══════════════════════════════════════════════════════════════
        //  METHODS
        // ══════════════════════════════════════════════════════════════

        private void SetSelectedRow(object? parameter)
        {
            var row = parameter as SheetProfitRow;
            foreach (var r in SheetBreakdown)
                r.IsSelected = ReferenceEquals(r, row);

            SelectedRow = row;
        }

        private void BackToSheetList()
        {
            foreach (var r in SheetBreakdown)
                r.IsSelected = false;

            SelectedRow = null;
        }

        /// <summary>
        /// Rebuild the sheet analysis from the authoritative Step 9 glass
        /// rows and the Step 6 wastage setup, then run the cross-check.
        /// </summary>
        public void RefreshAnalysis()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                SheetBreakdown.Clear();
                ShortfallMessages.Clear();
                ReviewMessages.Clear();
                SelectedRow = null;

                var groups = _parentViewModel.SpecGroupsView;
                if (groups == null || groups.Count == 0)
                {
                    ResetTotals();
                    StatusMessage = "No specifications found. Please add specifications in Step 5.";
                    return;
                }

                decimal wastagePercent = _parentViewModel.WastagePercent;

                decimal buySheetsSum = 0m;
                decimal usedSheetsSum = 0m;
                decimal wastageSheetsSum = 0m;
                decimal buyAreaSum = 0m;
                decimal usedAreaSum = 0m;
                decimal buyPriceSum = 0m;
                decimal usedPriceSum = 0m;
                decimal wastageValueSum = 0m;
                decimal step6ChargeSum = 0m;

                foreach (var specGroup in groups)
                {
                    var specName = specGroup.SpecName ?? "";
                    var cutSqm = specGroup.CutSqm;
                    if (cutSqm <= 0) continue;

                    var glassFamilyLabel = FamilyLabelFor(specName);

                    var glassRows = specGroup.Rows.Where(r => r.Kind == "Glass").ToList();
                    foreach (var glassRow in glassRows)
                    {
                        var sheetSqm = glassRow.SheetSqm;
                        if (sheetSqm <= 0) continue;

                        var unitRate = glassRow.PricePerSqm;

                        string paneSide = PaneSideFrom(glassRow.Description);

                        // ── Step 6 linkage ────────────────────────────────
                        int step6SheetQuantity = ResolveStep6SheetQuantity(
                            specGroup.SpecNumber, paneSide);
                        var step6 = ResolveStep6Charge(specGroup.SpecNumber, paneSide);

                        // Used side of the ledger. When Step 6 sheet setup is
                        // present, the bought sheet count is the used sheet
                        // count (e.g. 17 bought = 17 used). The Step 6 OPT %
                        // then splits that bought area into utilized vs wastage
                        // area/value. Without Step 6 setup, preserve the legacy
                        // cut SQM + Step 9 wastage-percent fallback.
                        var requiredSqm = PnLMeasurement.AreaWithWastage(cutSqm, wastagePercent);
                        var roundedRequiredSheets = PnLMeasurement.WholeSheetsRequired(requiredSqm, sheetSqm);

                        bool pricedByArea = (glassRow.Description ?? "")
                            .IndexOf("priced by required area", StringComparison.OrdinalIgnoreCase) >= 0;

                        decimal buySheets = step6SheetQuantity > 0
                            ? step6SheetQuantity
                            : pricedByArea ? roundedRequiredSheets : glassRow.Quantity;
                        if (buySheets <= 0) continue;

                        decimal buySheetArea = pricedByArea && step6SheetQuantity <= 0
                            ? requiredSqm
                            : Math.Round(buySheets * sheetSqm, 4);

                        decimal utilizationPercent = step6.OptimizationPercent > 0m
                            ? step6.OptimizationPercent
                            : buySheetArea > 0m
                                ? Math.Min(100m, Math.Round(requiredSqm / buySheetArea * 100m, 2))
                                : 0m;

                        decimal usedSheets = step6SheetQuantity > 0
                            ? buySheets
                            : roundedRequiredSheets;
                        decimal usedArea = step6SheetQuantity > 0 && utilizationPercent > 0m
                            ? Math.Round(buySheetArea * utilizationPercent / 100m, 4)
                            : requiredSqm;

                        decimal wastageSheets = step6SheetQuantity > 0
                            ? 0m
                            : buySheets - usedSheets;
                        decimal wastageArea = buySheetArea - usedArea;

                        decimal buySheetPrice = step6SheetQuantity > 0 && unitRate > 0m
                            ? Math.Round(buySheetArea * unitRate, 2)
                            : glassRow.Amount;
                        decimal usedSheetPrice = Math.Round(usedArea * unitRate, 2);
                        decimal wastageValue = Math.Max(0m, buySheetPrice - usedSheetPrice);

                        string diagnostic;
                        if (wastageSheets < 0m)
                        {
                            string qtyNote = step6SheetQuantity > 0
                                ? $"Step 6 SHEET QTY is {step6SheetQuantity}."
                                : "No Step 6 SHEET QTY is set for this pane.";
                            diagnostic = $"SHORTFALL — bought {buySheets:0} sheet(s) but {usedSheets:0} " +
                                         $"are required. {qtyNote} Bought sheets are less than the " +
                                         $"sheets needed — review the Step 6 wastage setup.";
                        }
                        else if (step6.Charge > 0m && wastageArea <= 0m)
                        {
                            diagnostic = $"Step 6 charges {step6.Charge:N2} AED for this pane " +
                                         $"(source {step6.SourceLabel}), but there is no leftover " +
                                         $"wastage area. Review the Step 6 wastage setup.";
                        }
                        else if (wastageArea > 0m && step6.Charge <= 0m)
                        {
                            diagnostic = $"No Step 6 wastage charge is set for this pane, though " +
                                         $"{wastageArea:0.##} SQM is leftover.";
                        }
                        else
                        {
                            diagnostic = "";
                        }

                        var row = new SheetProfitRow
                        {
                            SpecName = specName,
                            SpecNumber = specGroup.SpecNumber,
                            GlassTypeLabel = glassFamilyLabel,
                            PaneSide = paneSide,
                            SheetSize = $"{glassRow.SheetWidthMm} × {glassRow.SheetHeightMm} mm",
                            SheetSqm = sheetSqm,
                            BuySheets = buySheets,
                            UsedSheets = usedSheets,
                            WastageSheets = wastageSheets,
                            BuySheetArea = buySheetArea,
                            UsedArea = usedArea,
                            WastageArea = wastageArea,
                            PricePerSqm = unitRate,
                            BuySheetPrice = buySheetPrice,
                            UsedSheetPrice = usedSheetPrice,
                            WastageValue = wastageValue,
                            Step6Charge = step6.Charge,
                            Step6SourceLabel = step6.SourceLabel,
                            Step6SourceSqm = step6.SourceSqm,
                            Step6SheetQuantity = step6SheetQuantity,
                            Step6Rate = step6.Rate,
                            UsageEfficiencyPercent = utilizationPercent,
                            DiagnosticMessage = diagnostic
                        };

                        SheetBreakdown.Add(row);

                        if (row.HasShortfall)
                            ShortfallMessages.Add($"{specName} · {paneSide} — {diagnostic}");
                        else if (row.HasChargeWithoutWastage || row.HasWastageWithoutCharge)
                            ReviewMessages.Add($"{specName} · {paneSide} — {diagnostic}");

                        buySheetsSum += buySheets;
                        usedSheetsSum += usedSheets;
                        wastageSheetsSum += Math.Max(0m, wastageSheets);
                        buyAreaSum += buySheetArea;
                        usedAreaSum += usedArea;
                        buyPriceSum += buySheetPrice;
                        usedPriceSum += usedSheetPrice;
                        wastageValueSum += wastageValue;
                        step6ChargeSum += step6.Charge;
                    }
                }

                TotalBuySheets = buySheetsSum;
                TotalUsedSheets = usedSheetsSum;
                TotalWastageSheets = wastageSheetsSum;
                TotalBuySheetArea = buyAreaSum;
                TotalUsedArea = usedAreaSum;
                TotalBuyPrice = buyPriceSum;
                TotalUsedPrice = usedPriceSum;
                TotalWastageValue = wastageValueSum;
                TotalStep6Charge = step6ChargeSum;
                UsageEfficiencyPercent = buyAreaSum > 0m
                    ? Math.Round(usedAreaSum / buyAreaSum * 100m, 1)
                    : 0m;

                HasShortfalls = ShortfallMessages.Count > 0;
                HasReviewItems = ReviewMessages.Count > 0;

                // Re-raise computed comparison figures (they derive from the
                // totals set above).
                OnPropertyChanged(nameof(ReconciliationBalance));
                OnPropertyChanged(nameof(ReconciliationBalanceDisplay));
                OnPropertyChanged(nameof(IsInBalance));
                OnPropertyChanged(nameof(UsedPriceSharePercent));
                OnPropertyChanged(nameof(UsedPriceShareDisplay));
                OnPropertyChanged(nameof(WastageValueSharePercent));
                OnPropertyChanged(nameof(WastageValueShareDisplay));
                OnPropertyChanged(nameof(Step6ChargeSharePercent));
                OnPropertyChanged(nameof(Step6ChargeShareDisplay));

                if (SheetBreakdown.Count == 0)
                {
                    StatusMessage = "No priced glass sheets found — add glass rows in Step 5 and press REFRESH.";
                }
                else if (HasShortfalls)
                {
                    StatusMessage = $"{ShortfallMessages.Count} shortfall(s) detected — bought sheets are less " +
                                    $"than the sheets needed. Review the Step 6 wastage setup.";
                }
                else if (HasReviewItems)
                {
                    StatusMessage = $"{ReviewMessages.Count} item(s) to review against the Step 6 wastage setup.";
                }
                else
                {
                    StatusMessage = $"Analysis complete. Bought {TotalBuySheets:0} · used {TotalUsedSheets:0} · " +
                                    $"wastage {TotalWastageSheets:0} sheet(s). Step 6 wastage charge: " +
                                    $"{TotalStep6ChargeDisplay}. In balance.";
                }
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[SheetProfitabilityViewModel] Refresh failed: {ExceptionMessages.Deep(ex)}");
                StatusMessage = "Analysis failed. Please check the data and try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ResetTotals()
        {
            TotalBuySheets = 0m;
            TotalUsedSheets = 0m;
            TotalWastageSheets = 0m;
            TotalBuySheetArea = 0m;
            TotalUsedArea = 0m;
            TotalBuyPrice = 0m;
            TotalUsedPrice = 0m;
            TotalWastageValue = 0m;
            TotalStep6Charge = 0m;
            UsageEfficiencyPercent = 0m;
            HasShortfalls = false;
            HasReviewItems = false;
        }

        /// <summary>
        /// The pane side a Step 9 glass row belongs to, read from the row
        /// description's "{side} pane · …" prefix.
        /// </summary>
        private static string PaneSideFrom(string? description)
        {
            if (string.IsNullOrWhiteSpace(description)) return "Outer";

            int idx = description.IndexOf(" pane", StringComparison.OrdinalIgnoreCase);
            string side = idx > 0 ? description[..idx].Trim() : "Outer";
            return side.Length == 0 ? "Outer" : side;
        }

        /// <summary>
        /// Maps a Step 9 pane side to the exact Step 6 wastage source label.
        /// Unknown sides (e.g. an extra "Pane N" of a 4+ pane hybrid) return
        /// null — they have no Step 6 wastage source.
        /// </summary>
        private static string? Step6SideLabelForPane(string paneSide)
        {
            string side = (paneSide ?? "").Trim();
            if (side.Length == 0) return null;

            if (side.IndexOf("Lam Inner", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Lam Inner (W2/H2)";
            if (side.IndexOf("Lam Outer", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Lam Outer (W2/H2)";
            if (side.IndexOf("Middle", StringComparison.OrdinalIgnoreCase) >= 0)
                return $"{side} (W2/H2)";
            if (side.IndexOf("Inner", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Inner (W2/H2)";
            if (side.IndexOf("Outer", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Outer (W1/H1)";
            return null;
        }

        /// <summary>
        /// The SHEET QTY the operator set in Step 6 for THIS pane — read
        /// even from a DISABLED wastage row (it still owns the setup
        /// quantity). Mirrors ResolveStep6WastageSheetQuantity. 0 = none.
        /// </summary>
        private int ResolveStep6SheetQuantity(int specNumber, string paneSide)
        {
            string? sideLabel = Step6SideLabelForPane(paneSide);
            if (sideLabel == null) return 0;

            var rows = _parentViewModel._otherChargesVm?.WastagesSection?.WastageRows;
            if (rows == null) return 0;

            var row = rows
                .Where(r => r.AvailableSources.Any(s =>
                    s.IsChecked
                    && s.SpecNumber == specNumber
                    && string.Equals(s.SideLabel, sideLabel, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(r => r.IsActive)
                .FirstOrDefault();

            return row?.SheetQuantity ?? 0;
        }

        /// <summary>
        /// The Step 6 wastage charge for THIS pane, with the source it was
        /// read from. An ACTIVE row's ResultAmount is split across its
        /// checked sources by SQM share — the same allocation Step 9's
        /// AppendWastageCostRows uses — and only this pane's share plus its
        /// source identity (side, colour, SQM) and the row's rate are
        /// returned, so the operator can see exactly which outer/inner pane
        /// was charged.
        /// </summary>
        private Step6ChargeInfo ResolveStep6Charge(int specNumber, string paneSide)
        {
            string? sideLabel = Step6SideLabelForPane(paneSide);
            if (sideLabel == null) return Step6ChargeInfo.None;

            var rows = _parentViewModel._otherChargesVm?.WastagesSection?.WastageRows;
            if (rows == null) return Step6ChargeInfo.None;

            foreach (var row in rows)
            {
                if (row == null) continue;

                var sources = row.AvailableSources
                    .Where(s => s != null && s.IsChecked && s.SqmValue > 0m)
                    .ToList();
                if (sources.Count == 0) continue;

                decimal totalSqm = row.TotalSqm;
                if (totalSqm <= 0m) continue;

                foreach (var src in sources)
                {
                    if (src.SpecNumber != specNumber) continue;
                    if (!string.Equals(src.SideLabel, sideLabel, StringComparison.OrdinalIgnoreCase))
                        continue;

                    decimal charge = row.IsActive && row.ResultAmount > 0m
                        ? Math.Round(row.ResultAmount * (src.SqmValue / totalSqm), 2)
                        : 0m;
                    return new Step6ChargeInfo(
                        src.DisplayLabel, src.SqmValue, row.SheetQuantity,
                        row.Rate, charge, row.OptimizationPercent);
                }
            }

            return Step6ChargeInfo.None;
        }

        /// <summary>
        /// Build family shown in the GLASS TYPE column: "SGU", "DGU",
        /// "LAM" or "DGU+LAM". Only the build family is shown — the outer/
        /// inner pane identity lives in the pane-specific cross-check, not
        /// in the glass type.
        /// </summary>
        private static string FamilyLabelFor(string specName)
        {
            bool dgu = PnLSpecParser.Spacer(specName).Success
                       || specName.IndexOf("DGU", StringComparison.OrdinalIgnoreCase) >= 0;
            bool lam = PnLSpecParser.Interlayer(specName).Success
                       || specName.IndexOf("LAM", StringComparison.OrdinalIgnoreCase) >= 0;

            if (dgu && lam) return "DGU+LAM";
            if (dgu) return "DGU";
            if (lam) return "LAM";
            return "SGU";
        }

        /// <summary>A Step 6 wastage charge read for one pane source.</summary>
        private sealed class Step6ChargeInfo
        {
            public static readonly Step6ChargeInfo None =
                new Step6ChargeInfo("", 0m, 0, 0m, 0m, 0m);

            public string SourceLabel { get; }
            public decimal SourceSqm { get; }
            public int SheetQuantity { get; }
            public decimal Rate { get; }
            public decimal Charge { get; }
            public decimal OptimizationPercent { get; }

            public Step6ChargeInfo(
                string sourceLabel, decimal sourceSqm, int sheetQuantity,
                decimal rate, decimal charge, decimal optimizationPercent)
            {
                SourceLabel = sourceLabel ?? "";
                SourceSqm = sourceSqm;
                SheetQuantity = sheetQuantity;
                Rate = rate;
                Charge = charge;
                OptimizationPercent = optimizationPercent;
            }
        }
    }

    /// <summary>
    /// One glass pane of the job: the buy / used / wastage (leftover)
    /// split, its Step 6 wastage charge readout, and its cross-check
    /// status.
    /// </summary>
    public class SheetProfitRow : ViewModelBase
    {
        private string _specName = "";
        public string SpecName
        {
            get => _specName;
            set
            {
                if (_specName == value) return;
                _specName = value ?? "";
                OnPropertyChanged();
            }
        }

        private int _specNumber;
        public int SpecNumber
        {
            get => _specNumber;
            set
            {
                if (_specNumber == value) return;
                _specNumber = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Build family: "SGU", "DGU", "LAM" or "DGU+LAM".</summary>
        private string _glassTypeLabel = "";
        public string GlassTypeLabel
        {
            get => _glassTypeLabel;
            set
            {
                if (_glassTypeLabel == value) return;
                _glassTypeLabel = value ?? "";
                OnPropertyChanged();
            }
        }

        /// <summary>Pane side (Outer / Inner / Lam Outer / Lam Inner / Middle).</summary>
        private string _paneSide = "";
        public string PaneSide
        {
            get => _paneSide;
            set
            {
                if (_paneSide == value) return;
                _paneSide = value ?? "";
                OnPropertyChanged();
            }
        }

        private string _sheetSize = "";
        public string SheetSize
        {
            get => _sheetSize;
            set
            {
                if (_sheetSize == value) return;
                _sheetSize = value ?? "";
                OnPropertyChanged();
            }
        }

        private decimal _sheetSqm;
        public decimal SheetSqm
        {
            get => _sheetSqm;
            set => SetRow(ref _sheetSqm, value, nameof(SheetSqmDisplay));
        }

        public string SheetSqmDisplay => $"{SheetSqm:0.####} SQM";

        private decimal _buySheets;
        /// <summary>Sheets bought (Step 9 quantity; Step 6 SHEET QTY when set).</summary>
        public decimal BuySheets
        {
            get => _buySheets;
            set => SetRow(ref _buySheets, value, nameof(BuySheetsDisplay));
        }

        public string BuySheetsDisplay => $"{BuySheets:0}";

        private decimal _usedSheets;
        /// <summary>Whole sheets required to cover the required area.</summary>
        public decimal UsedSheets
        {
            get => _usedSheets;
            set => SetRow(ref _usedSheets, value, nameof(UsedSheetsDisplay));
        }

        public string UsedSheetsDisplay => $"{UsedSheets:0}";

        private decimal _wastageSheets;
        /// <summary>Leftover = buy − used. Negative means a shortfall.</summary>
        public decimal WastageSheets
        {
            get => _wastageSheets;
            set => SetRow(ref _wastageSheets, value, nameof(WastageSheetsDisplay));
        }

        public string WastageSheetsDisplay => $"{WastageSheets:0}";

        private decimal _buySheetArea;
        public decimal BuySheetArea
        {
            get => _buySheetArea;
            set => SetRow(ref _buySheetArea, value, nameof(BuySheetAreaDisplay));
        }

        public string BuySheetAreaDisplay => $"{BuySheetArea:0.##} SQM";

        private decimal _usedArea;
        /// <summary>Required area including wastage (SQM).</summary>
        public decimal UsedArea
        {
            get => _usedArea;
            set => SetRow(ref _usedArea, value, nameof(UsedAreaDisplay));
        }

        public string UsedAreaDisplay => $"{UsedArea:0.##} SQM";

        private decimal _wastageArea;
        /// <summary>Leftover area = bought − required. Negative = shortfall.</summary>
        public decimal WastageArea
        {
            get => _wastageArea;
            set => SetRow(ref _wastageArea, value, nameof(WastageAreaDisplay));
        }

        public string WastageAreaDisplay => $"{WastageArea:0.##} SQM";

        private decimal _pricePerSqm;
        public decimal PricePerSqm
        {
            get => _pricePerSqm;
            set => SetRow(ref _pricePerSqm, value, nameof(PricePerSqmDisplay));
        }

        public string PricePerSqmDisplay => $"AED {PricePerSqm:N2}/SQM";

        private decimal _buySheetPrice;
        /// <summary>Total sheet price = the Step 9 glass row amount.</summary>
        public decimal BuySheetPrice
        {
            get => _buySheetPrice;
            set => SetRow(ref _buySheetPrice, value, nameof(BuySheetPriceDisplay));
        }

        public string BuySheetPriceDisplay => $"AED {BuySheetPrice:N2}";

        private decimal _usedSheetPrice;
        /// <summary>Used sheet price = required area × rate.</summary>
        public decimal UsedSheetPrice
        {
            get => _usedSheetPrice;
            set => SetRow(ref _usedSheetPrice, value, nameof(UsedSheetPriceDisplay));
        }

        public string UsedSheetPriceDisplay => $"AED {UsedSheetPrice:N2}";

        private decimal _wastageValue;
        /// <summary>Material value of the leftover = buy − used.</summary>
        public decimal WastageValue
        {
            get => _wastageValue;
            set => SetRow(ref _wastageValue, value, nameof(WastageValueDisplay));
        }

        public string WastageValueDisplay => $"AED {WastageValue:N2}";

        private decimal _step6Charge;
        /// <summary>The Step 6 wastage charge for this pane.</summary>
        public decimal Step6Charge
        {
            get => _step6Charge;
            set => SetRow(ref _step6Charge, value, nameof(Step6ChargeDisplay));
        }

        public string Step6ChargeDisplay => $"AED {Step6Charge:N2}";

        private string _step6SourceLabel = "";
        /// <summary>The Step 6 source this charge was read from (pane + colour + W1/H1).</summary>
        public string Step6SourceLabel
        {
            get => _step6SourceLabel;
            set
            {
                if (_step6SourceLabel == value) return;
                _step6SourceLabel = value ?? "";
                OnPropertyChanged();
            }
        }

        private decimal _step6SourceSqm;
        /// <summary>The source SQM recorded in Step 6 for this pane.</summary>
        public decimal Step6SourceSqm
        {
            get => _step6SourceSqm;
            set => SetRow(ref _step6SourceSqm, value, nameof(Step6SourceSqmDisplay));
        }

        public string Step6SourceSqmDisplay => $"{Step6SourceSqm:0.###} SQM";

        private int _step6SheetQuantity;
        /// <summary>The SHEET QTY entered in Step 6 for this pane (0 = none).</summary>
        public int Step6SheetQuantity
        {
            get => _step6SheetQuantity;
            set => SetRow(ref _step6SheetQuantity, value, nameof(Step6SheetQuantityDisplay));
        }

        public string Step6SheetQuantityDisplay =>
            Step6SheetQuantity > 0 ? $"{Step6SheetQuantity:0}" : "—";

        private decimal _step6Rate;
        /// <summary>The Step 6 wastage row rate (AED/SQM).</summary>
        public decimal Step6Rate
        {
            get => _step6Rate;
            set => SetRow(ref _step6Rate, value, nameof(Step6RateDisplay));
        }

        public string Step6RateDisplay =>
            Step6Rate > 0m ? $"AED {Step6Rate:0.####}/SQM" : "—";

        private decimal _usageEfficiencyPercent;
        public decimal UsageEfficiencyPercent
        {
            get => _usageEfficiencyPercent;
            set => SetRow(ref _usageEfficiencyPercent, value, nameof(UsageEfficiencyDisplay));
        }

        public string UsageEfficiencyDisplay => $"{UsageEfficiencyPercent:0.#}%";

        private string _diagnosticMessage = "";
        public string DiagnosticMessage
        {
            get => _diagnosticMessage;
            set
            {
                if (_diagnosticMessage == value) return;
                _diagnosticMessage = value ?? "";
                OnPropertyChanged();
            }
        }

        /// <summary>Bought fewer sheets than required — Step 6 SHEET QTY too low.</summary>
        public bool HasShortfall => WastageSheets < 0m;

        /// <summary>Step 6 charges wastage but this pane has no leftover.</summary>
        public bool HasChargeWithoutWastage => Step6Charge > 0m && WastageArea <= 0m && !HasShortfall;

        /// <summary>This pane has leftover but Step 6 charges nothing.</summary>
        public bool HasWastageWithoutCharge => WastageArea > 0m && Step6Charge <= 0m;

        /// <summary>No cross-check issue for this pane.</summary>
        public bool IsHealthy => !HasShortfall && !HasChargeWithoutWastage && !HasWastageWithoutCharge;

        /// <summary>Badge shown in the STATUS column.</summary>
        public string StatusLabel
        {
            get
            {
                if (HasShortfall) return "SHORTFALL";
                if (HasChargeWithoutWastage) return "REVIEW";
                if (HasWastageWithoutCharge) return "NO CHARGE";
                return "OK";
            }
        }

        /// <summary>Used price as a share of the buy price.</summary>
        public decimal UsedPriceSharePercent => BuySheetPrice > 0m
            ? Math.Round(UsedSheetPrice / BuySheetPrice * 100m, 1)
            : 0m;

        public string UsedPriceShareDisplay => $"{UsedPriceSharePercent:0.#}%";

        /// <summary>Wastage material value as a share of the buy price.</summary>
        public decimal WastageValueSharePercent => BuySheetPrice > 0m
            ? Math.Round(WastageValue / BuySheetPrice * 100m, 1)
            : 0m;

        public string WastageValueShareDisplay => $"{WastageValueSharePercent:0.#}%";

        /// <summary>Step 6 wastage charge as a share of the buy price.</summary>
        public decimal Step6ChargeSharePercent => BuySheetPrice > 0m
            ? Math.Round(Step6Charge / BuySheetPrice * 100m, 1)
            : 0m;

        public string Step6ChargeShareDisplay => $"{Step6ChargeSharePercent:0.#}%";

        private bool _isSelected;
        /// <summary>True while this row's full-screen detail is open.</summary>
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

        private void SetRow(ref decimal field, decimal value, string displayName)
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(displayName);
        }

        private void SetRow(ref int field, int value, string displayName)
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(displayName);
        }
    }
}
