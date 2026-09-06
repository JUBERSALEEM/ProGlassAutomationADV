// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Sheet Profitability Analysis (Step 9 — SHEET DETAIL tab)
//
// WHAT IT IS
//   Per-pane breakdown of the glass sheets a job actually consumes, in the
//   business's three-way model — BUY SHEET / USED SHEET / WASTAGE SHEET.
//   There is deliberately NO separate scrap/remnant value here: glass that
//   is bought but not used is WASTAGE, full stop.
//
// WHERE THE NUMBERS COME FROM (single source of truth)
//   • BUY SHEET  — the authoritative sheet quantity and glass amount
//                  already costed by Step 9 (ProfitLossRow.Quantity /
//                  .Amount). When the operator set a SHEET QTY on the
//                  matching Step 6 wastage row, that quantity is already
//                  baked into the row (BuildGlassRowAsync reads it).
//   • USED SHEET — the whole sheets required to cover the required area
//                  (cut SQM + wastage %), via PnLMeasurement.
//   • WASTAGE    — BUY − USED sheets (never negative).
//   • WASTAGE    — the monetary wastage charge is read from the Step 6
//     CHARGE    wastage setup (WastageRow.ResultAmount), matched to the
//                  exact pane side (Outer / Inner / Lam Outer / Lam Inner /
//                  Middle), split across a row's checked sources by SQM
//                  share so a multi-source row is never double-counted.
//
// FORMULAS
//   Required area  = cut SQM × (1 + wastage % / 100)
//   Used sheets    = ⌈required area ÷ sheet SQM⌉
//   Wastage sheets = max(0, buy sheets − used sheets)
//   Buy price      = Step 9 glass row amount (authoritative)
//   Used price     = required area × sheet rate
//   Wastage price  = Step 6 wastage charge (ResultAmount)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss
{
    /// <summary>
    /// Sheet usage breakdown: buy / used / wastage sheets per glass pane,
    /// with the Step 6 wastage charge connected per pane side.
    /// </summary>
    public class SheetProfitabilityViewModel : ViewModelBase
    {
        private readonly ProfitLossStepViewModel _parentViewModel;

        public SheetProfitabilityViewModel(ProfitLossStepViewModel parentViewModel)
        {
            _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));

            SheetBreakdown = new ObservableCollection<SheetProfitRow>();
            RefreshCommand = new RelayCommand(_ => RefreshAnalysis());
            SelectRowCommand = new RelayCommand(_ => SetSelectedRow(_));
            BackCommand = new RelayCommand(_ => BackToSheetList());

            // Initial load
            RefreshAnalysis();
        }

        // ══════════════════════════════════════════════════════════════
        //  PROPERTIES
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Sheet breakdown rows.
        /// </summary>
        public ObservableCollection<SheetProfitRow> SheetBreakdown { get; }

        private decimal _totalBuySheets;
        /// <summary>Total sheets bought.</summary>
        public decimal TotalBuySheets
        {
            get => _totalBuySheets;
            private set
            {
                if (_totalBuySheets == value) return;
                _totalBuySheets = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalBuySheetsDisplay));
            }
        }

        public string TotalBuySheetsDisplay => $"{TotalBuySheets:0}";

        private decimal _totalUsedSheets;
        /// <summary>Total sheets used.</summary>
        public decimal TotalUsedSheets
        {
            get => _totalUsedSheets;
            private set
            {
                if (_totalUsedSheets == value) return;
                _totalUsedSheets = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalUsedSheetsDisplay));
            }
        }

        public string TotalUsedSheetsDisplay => $"{TotalUsedSheets:0}";

        private decimal _totalWastageSheets;
        /// <summary>Total sheets wasted (buy − used).</summary>
        public decimal TotalWastageSheets
        {
            get => _totalWastageSheets;
            private set
            {
                if (_totalWastageSheets == value) return;
                _totalWastageSheets = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalWastageSheetsDisplay));
            }
        }

        public string TotalWastageSheetsDisplay => $"{TotalWastageSheets:0}";

        private decimal _totalBuySheetArea;
        /// <summary>Total area bought (SQM).</summary>
        public decimal TotalBuySheetArea
        {
            get => _totalBuySheetArea;
            private set
            {
                if (_totalBuySheetArea == value) return;
                _totalBuySheetArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalBuySheetAreaDisplay));
            }
        }

        public string TotalBuySheetAreaDisplay => $"{TotalBuySheetArea:0.##} SQM";

        private decimal _totalUsedArea;
        /// <summary>Total area used, required area including wastage (SQM).</summary>
        public decimal TotalUsedArea
        {
            get => _totalUsedArea;
            private set
            {
                if (_totalUsedArea == value) return;
                _totalUsedArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalUsedAreaDisplay));
            }
        }

        public string TotalUsedAreaDisplay => $"{TotalUsedArea:0.##} SQM";

        private decimal _totalBuyPrice;
        /// <summary>Total sheet price (sum of the Step 9 glass row amounts).</summary>
        public decimal TotalBuyPrice
        {
            get => _totalBuyPrice;
            private set
            {
                if (_totalBuyPrice == value) return;
                _totalBuyPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalBuyPriceDisplay));
            }
        }

        public string TotalBuyPriceDisplay => $"AED {TotalBuyPrice:N2}";

        private decimal _totalUsedPrice;
        /// <summary>Total used sheet price (required area × rate).</summary>
        public decimal TotalUsedPrice
        {
            get => _totalUsedPrice;
            private set
            {
                if (_totalUsedPrice == value) return;
                _totalUsedPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalUsedPriceDisplay));
            }
        }

        public string TotalUsedPriceDisplay => $"AED {TotalUsedPrice:N2}";

        private decimal _wastageSheetPrice;
        /// <summary>Total wastage sheet price — the Step 6 wastage charge.</summary>
        public decimal WastageSheetPrice
        {
            get => _wastageSheetPrice;
            private set
            {
                if (_wastageSheetPrice == value) return;
                _wastageSheetPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WastageSheetPriceDisplay));
            }
        }

        public string WastageSheetPriceDisplay => $"AED {WastageSheetPrice:N2}";

        private decimal _usageEfficiencyPercent;
        /// <summary>Used area ÷ bought area, as a percentage.</summary>
        public decimal UsageEfficiencyPercent
        {
            get => _usageEfficiencyPercent;
            private set
            {
                if (_usageEfficiencyPercent == value) return;
                _usageEfficiencyPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UsageEfficiencyDisplay));
            }
        }

        public string UsageEfficiencyDisplay => $"{UsageEfficiencyPercent:0.#}%";

        /// <summary>Used sheet price as a share of the total buy price.</summary>
        public decimal UsedPriceSharePercent => TotalBuyPrice > 0m
            ? Math.Round(TotalUsedPrice / TotalBuyPrice * 100m, 1)
            : 0m;

        public string UsedPriceShareDisplay => $"{UsedPriceSharePercent:0.#}%";

        /// <summary>Step 6 wastage charge as a share of the total buy price.</summary>
        public decimal WastagePriceSharePercent => TotalBuyPrice > 0m
            ? Math.Round(WastageSheetPrice / TotalBuyPrice * 100m, 1)
            : 0m;

        public string WastagePriceShareDisplay => $"{WastagePriceSharePercent:0.#}%";

        private string _statusMessage = "Press REFRESH to analyze sheet profitability.";
        /// <summary>Status message.</summary>
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
        /// <summary>True while refreshing.</summary>
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

        private SheetProfitRow? _selectedRow;
        /// <summary>The sheet row whose full-screen detail is open (null = list).</summary>
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
        /// Refresh the sheet profitability analysis.
        /// Reads the authoritative Step 9 glass rows (buy quantity + amount)
        /// and the Step 6 wastage setup (per-pane charge), then derives the
        /// used/wastage split in the business's three-way sheet model.
        /// </summary>
        public void RefreshAnalysis()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                SheetBreakdown.Clear();
                SelectedRow = null;

                var groups = _parentViewModel.SpecGroupsView;
                if (groups == null || groups.Count == 0)
                {
                    ResetTotals();
                    StatusMessage = "No specifications found. Please add specifications in Step 5.";
                    return;
                }

                decimal wastagePercent = _parentViewModel.WastagePercent;

                decimal totalBuySheets = 0m;
                decimal totalUsedSheets = 0m;
                decimal totalWastageSheets = 0m;
                decimal totalBuySheetArea = 0m;
                decimal totalUsedArea = 0m;
                decimal totalBuyPrice = 0m;
                decimal totalUsedPrice = 0m;
                decimal totalWastageCharge = 0m;

                foreach (var specGroup in groups)
                {
                    var specName = specGroup.SpecName ?? "";
                    var cutSqm = specGroup.CutSqm;
                    if (cutSqm <= 0) continue;

                    // Build family of this specification ("SGU"/"DGU"/"LAM"/
                    // "DGU+LAM") — every sheet row of the spec shares it.
                    var glassFamilyLabel = FamilyLabelFor(specName);

                    var glassRows = specGroup.Rows.Where(r => r.Kind == "Glass").ToList();
                    foreach (var glassRow in glassRows)
                    {
                        var sheetSqm = glassRow.SheetSqm;
                        if (sheetSqm <= 0) continue;

                        var unitRate = glassRow.PricePerSqm;

                        // Required area including the cutting wastage %. The
                        // same area Basis the Step 9 glass rows use.
                        var requiredSqm = PnLMeasurement.AreaWithWastage(cutSqm, wastagePercent);
                        var usedSheets = PnLMeasurement.WholeSheetsRequired(requiredSqm, sheetSqm);

                        string paneSide = PaneSideFrom(glassRow.Description);

                        // SGU is priced by required area unless the operator
                        // set a Step 6 SHEET QTY (which BuildGlassRowAsync then
                        // priced as whole sheets). The row description says
                        // which basis was used, so it is read, never guessed.
                        bool pricedByArea = (glassRow.Description ?? "")
                            .IndexOf("priced by required area", StringComparison.OrdinalIgnoreCase) >= 0;

                        decimal buySheets = pricedByArea ? usedSheets : glassRow.Quantity;
                        if (buySheets <= 0) continue;

                        decimal buySheetArea = pricedByArea
                            ? requiredSqm
                            : Math.Round(buySheets * sheetSqm, 4);

                        decimal wastageSheets = Math.Max(0m, buySheets - usedSheets);
                        decimal wastageArea = Math.Max(0m, buySheetArea - requiredSqm);

                        // BUY price = the Step 9 glass row amount (authoritative).
                        decimal buySheetPrice = glassRow.Amount;
                        // USED price = required area × rate.
                        decimal usedSheetPrice = Math.Round(requiredSqm * unitRate, 2);
                        // WASTAGE price = the Step 6 wastage charge for this pane.
                        decimal wastageSheetPrice =
                            ResolveStep6WastageCharge(specGroup.SpecNumber, paneSide);

                        SheetBreakdown.Add(new SheetProfitRow
                        {
                            SpecName = specName,
                            GlassTypeLabel = glassFamilyLabel,
                            SheetSize = $"{glassRow.SheetWidthMm} × {glassRow.SheetHeightMm} mm",
                            SheetSqm = sheetSqm,
                            BuySheets = buySheets,
                            UsedSheets = usedSheets,
                            WastageSheets = wastageSheets,
                            BuySheetArea = buySheetArea,
                            UsedArea = requiredSqm,
                            WastageArea = wastageArea,
                            PricePerSqm = unitRate,
                            BuySheetPrice = buySheetPrice,
                            UsedSheetPrice = usedSheetPrice,
                            WastageSheetPrice = wastageSheetPrice,
                            UsageEfficiencyPercent = buySheetArea > 0m
                                ? Math.Round(requiredSqm / buySheetArea * 100m, 1)
                                : 0m,
                            UsedPriceSharePercent = buySheetPrice > 0m
                                ? Math.Round(usedSheetPrice / buySheetPrice * 100m, 1)
                                : 0m,
                            WastagePriceSharePercent = buySheetPrice > 0m
                                ? Math.Round(wastageSheetPrice / buySheetPrice * 100m, 1)
                                : 0m
                        });

                        totalBuySheets += buySheets;
                        totalUsedSheets += usedSheets;
                        totalWastageSheets += wastageSheets;
                        totalBuySheetArea += buySheetArea;
                        totalUsedArea += requiredSqm;
                        totalBuyPrice += buySheetPrice;
                        totalUsedPrice += usedSheetPrice;
                        totalWastageCharge += wastageSheetPrice;
                    }
                }

                TotalBuySheets = totalBuySheets;
                TotalUsedSheets = totalUsedSheets;
                TotalWastageSheets = totalWastageSheets;
                TotalBuySheetArea = totalBuySheetArea;
                TotalUsedArea = totalUsedArea;
                TotalBuyPrice = totalBuyPrice;
                TotalUsedPrice = totalUsedPrice;
                WastageSheetPrice = totalWastageCharge;
                UsageEfficiencyPercent = totalBuySheetArea > 0m
                    ? Math.Round(totalUsedArea / totalBuySheetArea * 100m, 1)
                    : 0m;

                // Re-raise the computed comparison figures (they derive from
                // the totals set just above).
                OnPropertyChanged(nameof(UsedPriceSharePercent));
                OnPropertyChanged(nameof(UsedPriceShareDisplay));
                OnPropertyChanged(nameof(WastagePriceSharePercent));
                OnPropertyChanged(nameof(WastagePriceShareDisplay));

                StatusMessage = SheetBreakdown.Count == 0
                    ? "No priced glass sheets found — add glass rows in Step 5 and press REFRESH."
                    : $"Analysis complete. {SheetBreakdown.Count} sheet type(s) analyzed. " +
                      $"Bought {TotalBuySheets:0} · used {TotalUsedSheets:0} · wastage {TotalWastageSheets:0} sheet(s). " +
                      $"Wastage charge (Step 6): {WastageSheetPriceDisplay}.";
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
            WastageSheetPrice = 0m;
            UsageEfficiencyPercent = 0m;
        }

        /// <summary>
        /// The pane side a Step 9 glass row belongs to, read from the row
        /// description's "{side} pane · …" prefix. Falls back to Outer so an
        /// unlabelled row still reports rather than vanishes.
        /// </summary>
        private static string PaneSideFrom(string? description)
        {
            if (string.IsNullOrWhiteSpace(description)) return "Outer";

            int idx = description.IndexOf(" pane", StringComparison.OrdinalIgnoreCase);
            string side = idx > 0 ? description[..idx].Trim() : "Outer";
            return side.Length == 0 ? "Outer" : side;
        }

        /// <summary>
        /// Maps a Step 9 pane side to the exact Step 6 wastage source label
        /// so the charge is read for the right pane — outer, inner, lam
        /// outer, lam inner, or a middle pane. Unknown sides (e.g. an extra
        /// "Pane N" of a 4+ pane hybrid) return null — they have no Step 6
        /// wastage source, so no charge is attributed to them.
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
        /// The wastage charge the Step 6 setup applies to THIS specification's
        /// pane side. A wastage row can cover several checked panes, so the
        /// row's ResultAmount is split across its checked sources by SQM share
        /// and only this pane's share is returned — the same allocation the
        /// Step 9 cost rows use, so the two screens never disagree.
        /// </summary>
        private decimal ResolveStep6WastageCharge(int specNumber, string paneSide)
        {
            string? sideLabel = Step6SideLabelForPane(paneSide);
            if (sideLabel == null) return 0m;

            var rows = _parentViewModel._otherChargesVm?.WastagesSection?.WastageRows;
            if (rows == null) return 0m;

            decimal total = 0m;

            foreach (var wastageRow in rows)
            {
                if (wastageRow == null || !wastageRow.IsActive) continue;
                if (wastageRow.ResultAmount <= 0m) continue;

                var checkedSources = wastageRow.AvailableSources
                    .Where(s => s != null && s.IsChecked && s.SqmValue > 0m)
                    .ToList();
                if (checkedSources.Count == 0) continue;

                // Use the row's own TotalSqm (checked sources' combined SQM) as
                // the allocation denominator — byte-for-byte the same share the
                // Step 9 cost rows use, so the two screens reconcile exactly.
                decimal rowTotalSqm = wastageRow.TotalSqm;
                if (rowTotalSqm <= 0m) continue;

                foreach (var src in checkedSources)
                {
                    if (src.SpecNumber != specNumber) continue;
                    if (!string.Equals(src.SideLabel, sideLabel, StringComparison.OrdinalIgnoreCase))
                        continue;

                    total += wastageRow.ResultAmount * (src.SqmValue / rowTotalSqm);
                }
            }

            return Math.Round(total, 2);
        }

        /// <summary>
        /// Build family shown in the GLASS TYPE column: "SGU", "DGU",
        /// "LAM" or "DGU+LAM". Spacer/interlayer evidence in the spec name
        /// drives DGU / LAM / DGU+LAM; anything without a marker (or a named
        /// spec the wizard cannot parse) defaults to SGU. Only the build
        /// family is shown — never the outer/inner pane identity.
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
    }

    /// <summary>
    /// Sheet profit row for breakdown display: the buy / used / wastage
    /// three-way sheet model for one glass pane.
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

        /// <summary>
        /// Build family of the specification this sheet belongs to:
        /// "SGU", "DGU", "LAM" or "DGU+LAM". The outer/inner pane identity
        /// is intentionally NOT shown here.
        /// </summary>
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
            set
            {
                if (_sheetSqm == value) return;
                _sheetSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SheetSqmDisplay));
            }
        }

        public string SheetSqmDisplay => $"{SheetSqm:0.####} SQM";

        private decimal _buySheets;
        /// <summary>Sheets bought for this pane.</summary>
        public decimal BuySheets
        {
            get => _buySheets;
            set
            {
                if (_buySheets == value) return;
                _buySheets = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BuySheetsDisplay));
            }
        }

        public string BuySheetsDisplay => $"{BuySheets:0}";

        private decimal _usedSheets;
        /// <summary>Whole sheets required to cover the required area.</summary>
        public decimal UsedSheets
        {
            get => _usedSheets;
            set
            {
                if (_usedSheets == value) return;
                _usedSheets = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UsedSheetsDisplay));
            }
        }

        public string UsedSheetsDisplay => $"{UsedSheets:0}";

        private decimal _wastageSheets;
        /// <summary>Sheets wasted = buy − used (never negative).</summary>
        public decimal WastageSheets
        {
            get => _wastageSheets;
            set
            {
                if (_wastageSheets == value) return;
                _wastageSheets = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WastageSheetsDisplay));
            }
        }

        public string WastageSheetsDisplay => $"{WastageSheets:0}";

        private decimal _buySheetArea;
        /// <summary>Area bought (SQM): buy sheets × sheet SQM (or required SQM when priced by area).</summary>
        public decimal BuySheetArea
        {
            get => _buySheetArea;
            set
            {
                if (_buySheetArea == value) return;
                _buySheetArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BuySheetAreaDisplay));
            }
        }

        public string BuySheetAreaDisplay => $"{BuySheetArea:0.##} SQM";

        private decimal _usedArea;
        /// <summary>Required area including wastage (SQM).</summary>
        public decimal UsedArea
        {
            get => _usedArea;
            set
            {
                if (_usedArea == value) return;
                _usedArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UsedAreaDisplay));
            }
        }

        public string UsedAreaDisplay => $"{UsedArea:0.##} SQM";

        private decimal _wastageArea;
        /// <summary>Wasted area (SQM) = bought area − required area (never negative).</summary>
        public decimal WastageArea
        {
            get => _wastageArea;
            set
            {
                if (_wastageArea == value) return;
                _wastageArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WastageAreaDisplay));
            }
        }

        public string WastageAreaDisplay => $"{WastageArea:0.##} SQM";

        private decimal _pricePerSqm;
        public decimal PricePerSqm
        {
            get => _pricePerSqm;
            set
            {
                if (_pricePerSqm == value) return;
                _pricePerSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PricePerSqmDisplay));
            }
        }

        public string PricePerSqmDisplay => $"AED {PricePerSqm:N2}/SQM";

        private decimal _buySheetPrice;
        /// <summary>Total sheet price = the Step 9 glass row amount.</summary>
        public decimal BuySheetPrice
        {
            get => _buySheetPrice;
            set
            {
                if (_buySheetPrice == value) return;
                _buySheetPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BuySheetPriceDisplay));
            }
        }

        public string BuySheetPriceDisplay => $"AED {BuySheetPrice:N2}";

        private decimal _usedSheetPrice;
        /// <summary>Used sheet price = required area × rate.</summary>
        public decimal UsedSheetPrice
        {
            get => _usedSheetPrice;
            set
            {
                if (_usedSheetPrice == value) return;
                _usedSheetPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UsedSheetPriceDisplay));
            }
        }

        public string UsedSheetPriceDisplay => $"AED {UsedSheetPrice:N2}";

        private decimal _wastageSheetPrice;
        /// <summary>Wastage sheet price = the Step 6 wastage charge for this pane.</summary>
        public decimal WastageSheetPrice
        {
            get => _wastageSheetPrice;
            set
            {
                if (_wastageSheetPrice == value) return;
                _wastageSheetPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WastageSheetPriceDisplay));
            }
        }

        public string WastageSheetPriceDisplay => $"AED {WastageSheetPrice:N2}";

        private decimal _usageEfficiencyPercent;
        public decimal UsageEfficiencyPercent
        {
            get => _usageEfficiencyPercent;
            set
            {
                if (_usageEfficiencyPercent == value) return;
                _usageEfficiencyPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UsageEfficiencyDisplay));
            }
        }

        public string UsageEfficiencyDisplay => $"{UsageEfficiencyPercent:0.#}%";

        private decimal _usedPriceSharePercent;
        /// <summary>Used price as a share of the buy price.</summary>
        public decimal UsedPriceSharePercent
        {
            get => _usedPriceSharePercent;
            set
            {
                if (_usedPriceSharePercent == value) return;
                _usedPriceSharePercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UsedPriceShareDisplay));
            }
        }

        public string UsedPriceShareDisplay => $"{UsedPriceSharePercent:0.#}%";

        private decimal _wastagePriceSharePercent;
        /// <summary>Step 6 wastage charge as a share of the buy price.</summary>
        public decimal WastagePriceSharePercent
        {
            get => _wastagePriceSharePercent;
            set
            {
                if (_wastagePriceSharePercent == value) return;
                _wastagePriceSharePercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WastagePriceShareDisplay));
            }
        }

        public string WastagePriceShareDisplay => $"{WastagePriceSharePercent:0.#}%";

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
    }
}
