// ══════════════════════════════════════════════════════════════════
// FILE: ViewModels/Optimization/OptimizationViewModel.Costing.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — Step 4 priced costing of a cutting plan
//
// WHY THIS FILE
//   The REPORT tab showed sheets, utilization and offcut but no money, so the
//   plan could not answer the question the owner actually asks: what does this
//   cutting plan COST, and how much of that is glass we threw away?
//
//   Rates come from STEP 4, carried through with the sheet size itself
//   (SpecGroup.OuterSheetPricePerSqm / InnerSheetPricePerSqm → Step4Sheets →
//   StockEntryVm.PricePerSqm). That is deliberate: the alternative is looking
//   the rate up in the Sheet Store by colour, and this module only has a glass
//   REF string ("6mm clear ft glass"), not a Sheet Store colour. Guessing that
//   mapping would silently price the wrong glass, so a row with no Step 4 price
//   shows "—" and says why instead of inventing a number.
//
//   Rows are keyed (width, height, composition) exactly like RefreshStockUsage:
//   keying by size alone prices a low-e rack at the clear rack's rate whenever
//   a job's outer and inner panes share one sheet size.
// ══════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace ProGlassAutomation.ViewModels.Optimization
{
    public partial class OptimizationViewModel
    {
        /// <summary>One row of the priced cutting plan: a glass composition on
        /// one Step 4 sheet size.</summary>
        public sealed class CostingRowVm
        {
            public string Composition { get; set; } = string.Empty;
            public string Thickness { get; set; } = string.Empty;

            /// <summary>The Step 4 sheet this group was cut from, "3210 × 2250".</summary>
            public string SheetSize { get; set; } = string.Empty;

            /// <summary>Sheets consumed — the quantity the saw actually runs.</summary>
            public string SheetQty { get; set; } = "0";

            /// <summary>Parts placed on those sheets.</summary>
            public string PieceQty { get; set; } = "0";

            /// <summary>Glass area ÷ stock area for these sheets.</summary>
            public string Utilization { get; set; } = "—";

            public string GlassArea { get; set; } = "0.00";
            public string StockArea { get; set; } = "0.00";
            public string WasteArea { get; set; } = "0.00";

            /// <summary>Step 4 rate, "AED 170 / m²", or "—" when not recorded.</summary>
            public string RateLabel { get; set; } = "—";

            public string GlassCostLabel { get; set; } = "—";
            public string WasteCostLabel { get; set; } = "—";
            public string TotalCostLabel { get; set; } = "—";

            /// <summary>False when no Step 4 price exists for this row, so the
            /// UI can show the money columns as pending rather than zero.</summary>
            public bool HasPrice { get; set; }

            // Raw values, for the export (Excel keeps numbers as numbers).
            public int SheetCount { get; set; }
            public decimal Rate { get; set; }
            public decimal GlassM2 { get; set; }
            public decimal StockM2 { get; set; }
            public decimal WasteM2Value { get; set; }
            public decimal GlassCost { get; set; }
            public decimal WasteCost { get; set; }
            public decimal TotalCost { get; set; }
        }

        public ObservableCollection<CostingRowVm> CostingRows { get; } = new();

        /// <summary>True once a run has produced priced rows.</summary>
        public bool HasCosting => CostingRows.Count > 0;

        private string _costingTotalLabel = string.Empty;
        /// <summary>Grand total line for the whole plan.</summary>
        public string CostingTotalLabel
        {
            get => _costingTotalLabel;
            private set
            {
                if (_costingTotalLabel == value) return;
                _costingTotalLabel = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCostingTotal));
            }
        }

        public bool HasCostingTotal => !string.IsNullOrWhiteSpace(_costingTotalLabel);

        private string _costingPriceNote = string.Empty;
        /// <summary>Explains any row that could not be priced from Step 4. Empty
        /// when every row carries a rate — the note is never decorative.</summary>
        public string CostingPriceNote
        {
            get => _costingPriceNote;
            private set
            {
                if (_costingPriceNote == value) return;
                _costingPriceNote = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCostingPriceNote));
            }
        }

        public bool HasCostingPriceNote => !string.IsNullOrWhiteSpace(_costingPriceNote);

        /// <summary>
        /// Rebuilds the priced plan from the runs just applied. Called from
        /// ApplyResults immediately after RefreshStockUsage, so the sheet tally
        /// and the costing always describe the same run.
        /// </summary>
        private void RebuildCosting(List<GroupRun> runs)
        {
            CostingRows.Clear();

            var rates = new Dictionary<(int W, int H, string C), decimal>();
            foreach (var s in Stocks)
            {
                if (!int.TryParse(s.WidthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) ||
                    !int.TryParse(s.HeightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int h))
                    continue;

                var key = (w, h, (s.Composition ?? string.Empty).Trim().ToLowerInvariant());

                // A real Step 4 rate is never overwritten by a manual rack's 0.
                if (!rates.TryGetValue(key, out decimal existing) || existing <= 0m)
                    rates[key] = s.PricePerSqm;
            }

            decimal totStock = 0m, totGlass = 0m, totWaste = 0m, totCost = 0m;
            int pricedRows = 0;

            foreach (var r in runs)
            {
                string comp = (r.Composition ?? string.Empty).Trim().ToLowerInvariant();

                foreach (var bySize in r.Result.Sheets
                             .GroupBy(sh => (sh.Stock.WidthMm, sh.Stock.HeightMm))
                             .OrderBy(g => g.Key.WidthMm)
                             .ThenBy(g => g.Key.HeightMm))
                {
                    int qty = bySize.Count();
                    long stockMm2 = bySize.Sum(sh => (long)sh.Stock.WidthMm * sh.Stock.HeightMm);
                    long glassMm2 = bySize.Sum(sh => sh.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm));
                    int pieces = bySize.Sum(sh => sh.Pieces.Count);

                    decimal stockM2 = stockMm2 / 1_000_000m;
                    decimal glassM2 = glassMm2 / 1_000_000m;
                    decimal wasteM2 = stockM2 - glassM2;
                    if (wasteM2 < 0m) wasteM2 = 0m;

                    rates.TryGetValue((bySize.Key.WidthMm, bySize.Key.HeightMm, comp), out decimal rate);
                    bool hasPrice = rate > 0m;
                    if (hasPrice) pricedRows++;

                    decimal glassCost = glassM2 * rate;
                    decimal wasteCost = wasteM2 * rate;
                    decimal totalCost = stockM2 * rate;

                    totStock += stockM2;
                    totGlass += glassM2;
                    totWaste += wasteM2;
                    if (hasPrice) totCost += totalCost;

                    decimal util = stockMm2 == 0 ? 0m : (decimal)glassMm2 / stockMm2 * 100m;

                    CostingRows.Add(new CostingRowVm
                    {
                        Composition = r.Composition,
                        Thickness = r.Thickness,
                        SheetSize = bySize.Key.WidthMm.ToString(CultureInfo.InvariantCulture) + " × " +
                                    bySize.Key.HeightMm.ToString(CultureInfo.InvariantCulture),
                        SheetQty = qty.ToString(CultureInfo.InvariantCulture),
                        PieceQty = pieces.ToString(CultureInfo.InvariantCulture),
                        Utilization = util.ToString("0.00", CultureInfo.InvariantCulture) + "%",
                        GlassArea = glassM2.ToString("0.00", CultureInfo.InvariantCulture),
                        StockArea = stockM2.ToString("0.00", CultureInfo.InvariantCulture),
                        WasteArea = wasteM2.ToString("0.00", CultureInfo.InvariantCulture),
                        RateLabel = hasPrice
                            ? string.Format(CultureInfo.InvariantCulture, "AED {0:0.##} / m²", rate)
                            : "—",
                        GlassCostLabel = hasPrice
                            ? string.Format(CultureInfo.InvariantCulture, "AED {0:N2}", glassCost) : "—",
                        WasteCostLabel = hasPrice
                            ? string.Format(CultureInfo.InvariantCulture, "AED {0:N2}", wasteCost) : "—",
                        TotalCostLabel = hasPrice
                            ? string.Format(CultureInfo.InvariantCulture, "AED {0:N2}", totalCost) : "—",
                        HasPrice = hasPrice,
                        SheetCount = qty,
                        Rate = rate,
                        GlassM2 = glassM2,
                        StockM2 = stockM2,
                        WasteM2Value = wasteM2,
                        GlassCost = glassCost,
                        WasteCost = wasteCost,
                        TotalCost = totalCost
                    });
                }
            }

            CostingTotalLabel = string.Format(CultureInfo.InvariantCulture,
                "{0} sheet(s) · {1:0.00} m² stock · {2:0.00} m² glass · {3:0.00} m² wastage · AED {4:N2}",
                runs.Sum(r => r.Result.SheetsUsed), totStock, totGlass, totWaste, totCost);

            // Never let a missing rate read as free glass.
            CostingPriceNote = CostingRows.Count == 0 || pricedRows == CostingRows.Count
                ? string.Empty
                : pricedRows == 0
                    ? "No Step 4 sheet price is recorded for this job — import the Step 4 sheets on the " +
                      "STOCKS tab, or price them in Step 4, to cost the plan."
                    : string.Format(CultureInfo.InvariantCulture,
                          "{0} of {1} row(s) have no Step 4 price and read \"—\"; the total covers priced rows only.",
                          CostingRows.Count - pricedRows, CostingRows.Count);

            OnPropertyChanged(nameof(HasCosting));
        }
    }
}
