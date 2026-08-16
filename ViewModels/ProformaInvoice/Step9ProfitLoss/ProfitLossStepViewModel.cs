// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Wizard Step 9 — Profit & Loss (PHASE B)
//
// WHAT IT DOES
//   Answers the question the estimator cannot: "after we buy the glass and
//   run production, what is actually left on this invoice?"
//
//   For every Step 5 spec group it reverse-engineers the build from the
//   generated spec name, prices the raw sheets against the Sheet Store,
//   adds the production charges, and compares the total against the
//   quoted grand total (ex-VAT).
//
// SPEC NAME GRAMMAR (produced by Step 4, parsed back here)
//   outer   : "6mm HD Grey FT Glass"
//             ^(?<thk>\d+)\s*mm\s+(?<color>.+?)\s+(FT|Annealed|
//              Heat Strengthened|Heat Soaked)\s+Glass
//   spacer  : "+ 12mm ASP"           →  \+\s*(?<mm>\d+)\s*mm\s+ASP
//   inner   : same outer pattern, matched AFTER the spacer
//
// COST MODEL
//   cutSqm       = Σ (W1·H1/1e6  [+ W2·H2/1e6 when W2/H2 > 0]) · Qty
//   perimeterLm  = Σ (2·(W1+H1)/1000) · Qty
//   sheets       = ceil( cutSqm · (1 + Wastage%/100) / sheetSqm ), min 1
//   glass amount = round(sheetSqm · sheets · purchasePricePerSqm, 2)
//   DGU spacer   = round(cutSqm · DguVM.SpacerPrice, 2)
//                  ASP is quoted PER SQM in Step 4 (37.5+((w-6)/2)*2.5,
//                  so 45.00 at 12mm) and that price already includes
//                  cutting, tempering and production for DGU.
//   SGU prod.    = round(cutSqm · (CuttingCostPerSqm + TemperingCostPerSqm), 2)
//
//   Rows whose purchase price is unknown (≤ 0) are SKIPPED rather than
//   costed at zero — a silent zero would fake a profit. The skip is
//   surfaced through StatusMessage so the user knows the figure is partial.
//
// TOTALS
//   NetExVat = round(gross / 1.05, 2)   (5% UAE VAT)
//   Profit   = NetExVat − TotalGlassCost − TotalProductionCost
//   Margin   = round(Profit / NetExVat · 100, 1)
//
// SAFETY: Bind() may be called before every dependency exists and
// RecalculateAsync() never throws — the wizard must stay usable even with
// an empty Sheet Store or a half-filled Step 5 grid.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.ProformaInvoice;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguLamParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.LamParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.SguParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss
{
    // ══════════════════════════════════════════════════════════════════════
    //  ROW
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// One costed line in the Profit &amp; Loss breakdown. Display-only —
    /// created fresh by every recalculation, never edited by the user.
    /// </summary>
    /// <summary>
    /// One specification's worth of the report: its own rows, its own cost,
    /// and its own rate check.
    ///
    /// WHY THIS EXISTS
    /// The report was a single flat list. With two specifications it read
    ///     glass · spacer · glass · glass · spacer · glass
    /// and nothing said where one product ended and the next began. Grouping
    /// by specification means each unit expands on its own and the layers
    /// underneath it are unambiguously its own.
    ///
    /// It also carries the rate check. Costs are known per specification, so
    /// this is the only level where "what margin am I actually getting on
    /// THIS product" can be answered. The invoice total cannot answer it: it
    /// mixes every specification together.
    /// </summary>
    public class SpecCostGroup : ViewModelBase
    {
        /// <summary>Sequence number from the Step 5 DataGrid.</summary>
        public int SpecNumber { get; set; }

        /// <summary>Full specification text, e.g. "6mm HD Grey FT Glass + 12mm ASP + …".</summary>
        public string SpecName { get; set; } = "";

        /// <summary>Cost lines for this specification, in physical build order.</summary>
        public ObservableCollection<ProfitLossRow> Rows { get; } = new();

        /// <summary>Cut area this specification covers, in SQM.</summary>
        public decimal CutSqm { get; set; }

        /// <summary>Glass + production cost for this specification only.</summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// The Step 4 pre-margin cost basis for this specification: the exact
        /// number the quoted rate was built from, before the profit margin.
        /// Set during recalculation; 0 when Step 4 has no matching selection.
        /// </summary>
        public decimal QuoteBasisPerSqm { get; set; }

        /// <summary>
        /// Materials cost per SQM of cut area, from the Profit &amp; Loss
        /// rows. Shown for reference only.
        ///
        /// This is NOT what a quoted rate should be measured against, and
        /// using it here was the bug. It differs from the quote basis in
        /// three ways at once:
        ///   · priced at PURCHASE, while the quote is built from SELLING
        ///   · counts whole SHEETS BOUGHT, not the area actually cut
        ///   · excludes the spacer, cutting, tempering and yield division
        /// On a Clear/Clear DGU the two came out around 60 and 103.82 — the
        /// rate box was reporting a margin against a number that had nothing
        /// to do with the price.
        /// </summary>
        public decimal MaterialsCostPerSqm =>
            CutSqm > 0m ? Math.Round(TotalCost / CutSqm, 2) : 0m;

        /// <summary>
        /// The figure every percentage below is measured against: the Step 4
        /// basis when it is known, falling back to materials cost so a spec
        /// Step 4 cannot identify still reports something rather than zero.
        /// </summary>
        public decimal CostPerSqm =>
            QuoteBasisPerSqm > 0m ? QuoteBasisPerSqm : MaterialsCostPerSqm;

        /// <summary>True when the rate check is using the real quote basis.</summary>
        public bool HasQuoteBasis => QuoteBasisPerSqm > 0m;

        // ══════════════════════════════════════════════════════════════
        //  RATE CHECK
        //
        //  Type any rate and see the mark-up it actually earns. The wizard
        //  computes a rate at the target margin, but the price finally given
        //  to a customer is often negotiated, and until now nothing showed
        //  what that negotiation cost.
        //
        //  Worked example, 6mm HD Grey FT + 12mm ASP + 6mm Clear FT:
        //      cost basis 130.88 per SQM
        //      150.51 -> 15%      143.97 -> 10%      140.00 -> 6.97%
        //  A 4% cut from 150.51 to 143.97 gives away a third of the profit.
        // ══════════════════════════════════════════════════════════════

        private decimal _quotedRatePerSqm;
        /// <summary>Rate to test against this specification's cost. 0 = not set.</summary>
        public decimal QuotedRatePerSqm
        {
            get => _quotedRatePerSqm;
            set
            {
                if (_quotedRatePerSqm == value) return;
                _quotedRatePerSqm = value < 0m ? 0m : value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(QuotedMarkupPercent));
                OnPropertyChanged(nameof(QuotedProfitPerSqm));
                OnPropertyChanged(nameof(RateCheckDisplay));
                OnPropertyChanged(nameof(IsQuotedRateBelowCost));
                OnPropertyChanged(nameof(HasQuotedRate));
            }
        }

        public bool HasQuotedRate => QuotedRatePerSqm > 0m;

        /// <summary>Mark-up the quoted rate earns over cost, as a percentage.</summary>
        public decimal QuotedMarkupPercent
        {
            get
            {
                decimal cost = CostPerSqm;
                if (cost <= 0m || QuotedRatePerSqm <= 0m) return 0m;
                return Math.Round((QuotedRatePerSqm - cost) / cost * 100m, 2);
            }
        }

        /// <summary>Profit per SQM at the quoted rate. Negative below cost.</summary>
        public decimal QuotedProfitPerSqm =>
            QuotedRatePerSqm <= 0m ? 0m
                                   : Math.Round(QuotedRatePerSqm - CostPerSqm, 2);

        /// <summary>True when the rate entered would sell at a loss.</summary>
        public bool IsQuotedRateBelowCost =>
            QuotedRatePerSqm > 0m && QuotedRatePerSqm < CostPerSqm;

        /// <summary>
        /// Plain reading of the rate check, including the total effect across
        /// this specification's whole area — a small per-SQM gap is easy to
        /// wave away until it is multiplied by the job.
        /// </summary>
        public string RateCheckDisplay
        {
            get
            {
                if (CostPerSqm <= 0m)
                    return "Cost not known for this specification yet.";

                // Say plainly which number the percentage is measured against.
                // A margin quoted against the wrong basis is worse than none.
                string basisNote = HasQuoteBasis
                    ? $"basis {CostPerSqm:N2}/SQM"
                    : $"materials {CostPerSqm:N2}/SQM (Step 4 basis unavailable)";

                if (QuotedRatePerSqm <= 0m)
                    return $"{char.ToUpperInvariant(basisNote[0])}{basisNote[1..]}. " +
                           "Enter a rate to check its margin.";

                decimal total = Math.Round(QuotedProfitPerSqm * CutSqm, 2);

                return IsQuotedRateBelowCost
                    ? $"BELOW COST by {Math.Abs(QuotedProfitPerSqm):N2}/SQM " +
                      $"({QuotedMarkupPercent:0.##}%) — {Math.Abs(total):N2} lost over " +
                      $"{CutSqm:0.##} SQM · {basisNote}."
                    : $"{QuotedMarkupPercent:0.##}% margin · {QuotedProfitPerSqm:N2}/SQM " +
                      $"· {total:N2} over {CutSqm:0.##} SQM · {basisNote}.";
            }
        }

        /// <summary>Header caption: "SPEC 1 · 12.40 SQM · cost 130.88/SQM".</summary>
        public string HeaderDisplay =>
            HasQuoteBasis
                ? $"SPEC {SpecNumber} · {CutSqm:0.##} SQM · quote basis {CostPerSqm:N2}/SQM"
                : $"SPEC {SpecNumber} · {CutSqm:0.##} SQM · materials {CostPerSqm:N2}/SQM";

        public string TotalCostDisplay => $"AED {TotalCost:N2}";

        /// <summary>Raised after the rows are filled so computed values refresh.</summary>
        public void NotifyTotalsChanged()
        {
            OnPropertyChanged(nameof(CostPerSqm));
            OnPropertyChanged(nameof(QuoteBasisPerSqm));
            OnPropertyChanged(nameof(MaterialsCostPerSqm));
            OnPropertyChanged(nameof(HasQuoteBasis));
            OnPropertyChanged(nameof(TotalCost));
            OnPropertyChanged(nameof(TotalCostDisplay));
            OnPropertyChanged(nameof(HeaderDisplay));
            OnPropertyChanged(nameof(QuotedMarkupPercent));
            OnPropertyChanged(nameof(QuotedProfitPerSqm));
            OnPropertyChanged(nameof(RateCheckDisplay));
            OnPropertyChanged(nameof(IsQuotedRateBelowCost));
        }
    }

    public class ProfitLossRow : ViewModelBase
    {
        /// <summary>"Glass", "Spacer" or "Production".</summary>
        public string Kind { get; set; } = "";

        /// <summary>Step 5 spec group this line came from.</summary>
        public int SpecNumber { get; set; }

        public string Description { get; set; } = "";

        public int SheetWidthMm { get; set; }
        public int SheetHeightMm { get; set; }
        public decimal SheetSqm { get; set; }

        /// <summary>Sheets consumed, or units of the production charge.</summary>
        public decimal Quantity { get; set; }

        /// <summary>Unit price actually applied (AED / SQM, or / LM for spacers).</summary>
        public decimal PricePerSqm { get; set; }

        /// <summary>Extended amount in AED.</summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// PHASE G — where the sheet size came from: "chosen in Step 4",
        /// "from Sheet Store", or the default fallback. Shown in the UI so
        /// an unexpected cost is always traceable to its sheet.
        /// </summary>
        public string SheetSource { get; set; } = "";

        /// <summary>
        /// The FULL specification this row belongs to, taken from the Step 5
        /// DataGrid (SpecGroup.SelectedSpecName) — e.g.
        /// "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass".
        ///
        /// The SPEC column used to print only "SPEC 2", which said nothing
        /// about what was actually being costed.
        /// </summary>
        public string SpecName { get; set; } = "";

        // ── Display helpers (bound directly by the view) ──────────────────

        /// <summary>
        /// The specification itself, so the P&amp;L reads as the product
        /// being quoted. Falls back to "SPEC n" only when no spec name was
        /// captured, and to blank for rows with no spec origin.
        /// </summary>
        public string SpecDisplay =>
            !string.IsNullOrWhiteSpace(SpecName)
                ? SpecName
                : (SpecNumber > 0 ? $"SPEC {SpecNumber}" : "");

        /// <summary>e.g. "3210 × 2250 · 7.2225 SQM", or "—" for charge rows.</summary>
        public string SizeDisplay =>
            SheetWidthMm > 0 && SheetHeightMm > 0
                ? $"{SheetWidthMm} × {SheetHeightMm} · {SheetSqm:0.####} SQM"
                : "—";

        /// <summary>e.g. "× 4".</summary>
        public string QuantityDisplay =>
            $"× {Quantity:0.##}";

        /// <summary>
        /// e.g. "46.00 / SQM". Every row is now per SQM, including the ASP
        /// spacer: Step 4 defines the spacer rate per SQM, so costing it per
        /// linear metre was a unit mismatch that tripled the figure.
        /// </summary>
        public string PriceDisplay => $"{PricePerSqm:N2} / SQM";

        /// <summary>e.g. "1,328.94".</summary>
        public string AmountDisplay => $"{Amount:N2}";

        /// <summary>Builds the persistable form of this row.</summary>
        public ProfitLossRowSnapshot ToSnapshot() => new()
        {
            Kind = Kind ?? "",
            SpecNumber = SpecNumber,
            Description = Description ?? "",
            SheetWidthMm = SheetWidthMm,
            SheetHeightMm = SheetHeightMm,
            SheetSqm = SheetSqm,
            Quantity = Quantity,
            PricePerSqm = PricePerSqm,
            Amount = Amount,
            SheetSource = SheetSource ?? "",
            SpecName = SpecName ?? ""
        };
    }

    // ══════════════════════════════════════════════════════════════════════
    //  STEP VIEW-MODEL
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Step 9 of the Proforma wizard: costs the invoice against the Sheet
    /// Store and reports profit, loss and margin.
    /// </summary>
    public class ProfitLossStepViewModel : ViewModelBase
    {
        public string Title { get; } = "PROFIT & LOSS";

        public string Subtitle { get; } =
            "Real cost of this invoice — glass from Sheet Store, production from Step 4 rates.";

        // ── Fallback stock sheet when the store has no size on file ──────
        private const int DefaultSheetWidthMm = 2440;
        private const int DefaultSheetHeightMm = 1830;

        /// <summary>UAE standard VAT divisor used to strip VAT off the gross.</summary>
        private const decimal VatDivisor = 1.05m;

        // ── Bound dependencies (assigned by the shell via Bind) ──────────
        private DataGridViewModel? _dataGrid;
        private DguParametersViewModel? _dguVm;
        private SguParametersViewModel? _sguVm;
        private LamParametersViewModel? _lamVm;   // PHASE J
        private DguLamParametersViewModel? _dguLamVm;   // DGU+LAM hybrid
        private Func<decimal>? _grossProvider;

        // ══════════════════════════════════════════════════════════════
        //  SPEC NAME REGEXES
        // ══════════════════════════════════════════════════════════════

        private const string TreatmentAlternation =
            @"(?:FT|Annealed|Heat\s+Strengthened|Heat\s+Soaked)";

        /// <summary>
        /// One glass pane, e.g. "6mm HD Grey FT Glass".
        ///
        /// THE COLOUR CLASS EXCLUDES '+' AND DIGITS, AND THAT IS
        /// LOAD-BEARING — IT IS WHY THE INNER PANE WAS BEING SKIPPED.
        ///
        /// The colour was previously (?&lt;color&gt;.+?), a lazy wildcard that
        /// happily crosses the '+' separator. On
        ///
        ///     "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass"
        ///
        /// the second match therefore began at the SPACER, taking thk=12
        /// and colour="ASP + 6mm Clear". The inner-pane search then looked
        /// for a match starting after the spacer and found none, because
        /// the only remaining match had already been consumed by that
        /// bogus one. Result: DGUs were costed with the outer pane only,
        /// understating glass cost on every double-glazed job.
        ///
        /// [^+\d]+? cannot span a '+' or a thickness digit, so each pane
        /// matches exactly its own segment.
        /// </summary>
        private static readonly Regex GlassPattern = new(
            @"(?<thk>\d+)\s*mm\s+(?<color>[^+\d]+?)\s+" + TreatmentAlternation + @"\s+Glass",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Allows an optional descriptor between the width and "ASP" so
        // "+ 12mm Black Warm Edge ASP" is recognised, not just "+ 12mm ASP".
        // Without it a warm-edge DGU matched nothing, the spacer cost was
        // dropped, and the hybrid below was not detected at all.
        // The class excludes '+' and digits so it cannot run past its own
        // layer into the next one.
        private static readonly Regex SpacerPattern = new(
            @"\+\s*(?<mm>\d+)\s*mm\s+(?:[^+\d]*?\s+)?ASP\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// PHASE J — laminated interlayer, e.g. "+ 1.52mm Clear PVB".
        /// A LAM unit has TWO glass panes like a DGU, but the middle layer
        /// is an interlayer priced per SQM of laminated area, not per LM of
        /// perimeter.
        ///
        /// SGP and EVA are matched alongside PVB. All three are sold as
        /// interlayers, and a spec naming SGP or EVA used to match nothing
        /// here: its bonded pane and its interlayer cost both vanished from
        /// the report while the job looked correctly costed.
        ///
        /// The colour group is bounded to [^+\d] — an unbounded ".+?" ran
        /// straight through the '+' separators and, on the DGU+LAM hybrid,
        /// captured "ASP + 6mm Clear FT Glass + 1.52mm Clear" as a colour.
        /// </summary>
        private static readonly Regex PvbPattern = new(
            @"\+\s*(?<mm>[\d\.]+)\s*mm\s+(?<color>[^+\d]+?)\s+(?<type>PVB|SGP|EVA)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ══════════════════════════════════════════════════════════════
        //  OUTPUTS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Costed breakdown, rebuilt by every recalculation.</summary>
        public ObservableCollection<ProfitLossRow> Rows { get; } = new();

        /// <summary>True when <see cref="Rows"/> is empty (drives the empty state).</summary>
        public bool HasRows => Rows.Count > 0;

        /// <summary>
        /// The same cost rows, grouped by specification, each with its own
        /// cost per SQM and rate check.
        ///
        /// This is what the report shows. The flat <see cref="Rows"/> list is
        /// kept because the snapshot, the printable invoice and the saved
        /// history all read from it, and changing that shape would rewrite
        /// stored financial records.
        /// </summary>
        public ObservableCollection<SpecCostGroup> SpecGroupsView { get; } = new();

        /// <summary>True once at least one specification has been costed.</summary>
        public bool HasSpecGroups => SpecGroupsView.Count > 0;

        // ══════════════════════════════════════════════════════════════
        //  COST BASIS — PURCHASE vs SELLING
        //
        //  Step 4 auto-fills its base prices from the Sheet Store SELLING
        //  price (DguParametersViewModel.SheetStore.cs: "sell" wins, and
        //  only falls back to purchase when no sell price exists). So the
        //  48 / 25 figures quoted in a spec are SELL rates.
        //
        //  The P&L, correctly for accounting, costs glass at the PURCHASE
        //  price - profit measured against what the supplier charged. The
        //  consequence is that the P&L will NOT reproduce hand arithmetic
        //  done with the Step 4 numbers, because those are a different
        //  column of the same Sheet Store row.
        //
        //  Both are legitimate questions:
        //     purchase -> "what did we really make?"
        //     sell     -> "does the quote hold together against list price?"
        //
        //  DEFAULT IS NOW SELLING, on the business's instruction.
        //
        //  The reason it matters here: Step 4 builds its rate from the
        //  SELLING column, so a report costed at purchase measures the
        //  quote against a different column of the same Sheet Store row.
        //  On a 40 SQM Clear/Clear DGU quoted at 119.39 the two answers are
        //  20.4% and 4.8% — not a rounding difference, a different
        //  conclusion about whether the job is worth taking.
        //
        //  The toggle remains. Costing at purchase is still the honest
        //  answer to "what did we actually make against supplier invoices",
        //  and switching to it is one click. Whichever is active is named in
        //  the status line, so no figure on this screen is ambiguous.
        // ══════════════════════════════════════════════════════════════

        private bool _costAtSellingPrice = true;
        /// <summary>
        /// True (default) = cost at SELLING price, matching the Step 4 base
        /// rates so the report reconciles with the quote.
        /// False = cost at Sheet Store PURCHASE price, which answers the
        /// separate question of profit against supplier invoices.
        /// </summary>
        public bool CostAtSellingPrice
        {
            get => _costAtSellingPrice;
            set
            {
                if (_costAtSellingPrice == value) return;
                _costAtSellingPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CostBasisLabel));

                // Switching the basis with stale figures on screen would be
                // worse than not offering the switch at all.
                _ = RecalculateAsync();
            }
        }

        /// <summary>Names the basis so no figure on screen is ambiguous.</summary>
        public string CostBasisLabel => _costAtSellingPrice
            ? "selling price"
            : "purchase price";

        private decimal _wastagePercent = 10m;
        /// <summary>
        /// Cutting wastage allowance applied to the required area before the
        /// sheet count is rounded up. User-editable; 10% is the shop default.
        /// </summary>
        public decimal WastagePercent
        {
            get => _wastagePercent;
            set
            {
                if (_wastagePercent == value) return;
                _wastagePercent = value < 0m ? 0m : value;
                OnPropertyChanged();

                // Wastage feeds the sheet count, which feeds every glass
                // amount and therefore the profit. Editing the box used to
                // change nothing on screen until the user happened to press
                // Refresh, so the figures shown did not match the wastage
                // shown -- the same screen reported two different jobs.
                _ = RecalculateAsync();
            }
        }

        private decimal _grossVatInclusive;
        /// <summary>Quoted grand total including VAT (from Step 7).</summary>
        public decimal GrossVatInclusive
        {
            get => _grossVatInclusive;
            private set
            {
                if (_grossVatInclusive == value) return;
                _grossVatInclusive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GrossDisplay));
            }
        }

        private decimal _netExVat;
        /// <summary>Revenue excluding VAT.</summary>
        public decimal NetExVat
        {
            get => _netExVat;
            private set
            {
                if (_netExVat == value) return;
                _netExVat = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NetDisplay));
            }
        }

        private decimal _totalGlassCost;
        /// <summary>Sum of all glass rows.</summary>
        public decimal TotalGlassCost
        {
            get => _totalGlassCost;
            private set
            {
                if (_totalGlassCost == value) return;
                _totalGlassCost = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GlassCostDisplay));
                OnPropertyChanged(nameof(MarkupPercent));
                OnPropertyChanged(nameof(MarkupDisplay));
            }
        }

        private decimal _totalProductionCost;
        /// <summary>Sum of all spacer + production rows.</summary>
        public decimal TotalProductionCost
        {
            get => _totalProductionCost;
            private set
            {
                if (_totalProductionCost == value) return;
                _totalProductionCost = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProductionCostDisplay));
                OnPropertyChanged(nameof(MarkupPercent));
                OnPropertyChanged(nameof(MarkupDisplay));
            }
        }

        private decimal _profit;
        /// <summary>Net revenue minus every cost. Negative means a loss.</summary>
        public decimal Profit
        {
            get => _profit;
            private set
            {
                if (_profit == value) return;
                _profit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProfitDisplay));
                OnPropertyChanged(nameof(ProfitLabel));
                OnPropertyChanged(nameof(MarkupPercent));
                OnPropertyChanged(nameof(MarkupDisplay));
            }
        }

        private decimal _marginPercent;
        /// <summary>Profit as a percentage of net revenue.</summary>
        public decimal MarginPercent
        {
            get => _marginPercent;
            private set
            {
                if (_marginPercent == value) return;
                _marginPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MarginDisplay));
            }
        }

        private bool _isLoss;
        /// <summary>
        /// True when the invoice loses money. The view flips the profit
        /// tile to DangerBrush through a DataTrigger on this flag.
        /// </summary>
        public bool IsLoss
        {
            get => _isLoss;
            private set
            {
                if (_isLoss == value) return;
                _isLoss = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProfitLabel));
            }
        }

        private bool _isBusy;
        /// <summary>True while a recalculation is running.</summary>
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

        private string _statusMessage = "Press RECALCULATE to cost this invoice against Sheet Store.";
        /// <summary>Human-readable state / warning line under the tiles.</summary>
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

        // ── Formatted display strings (keeps StringFormat out of the XAML) ─

        public string GrossDisplay => $"AED {GrossVatInclusive:N2}";
        public string NetDisplay => $"AED {NetExVat:N2}";
        public string GlassCostDisplay => $"AED {TotalGlassCost:N2}";
        public string ProductionCostDisplay => $"AED {TotalProductionCost:N2}";
        /// <summary>
        /// Math.Abs is correct ONLY because the caption flips to "LOSS",
        /// so "AED 500.00" under a LOSS label reads right and avoids the
        /// double-negative "-AED 500.00 LOSS".
        ///
        /// But IsLoss requires gross > 0. With no revenue entered yet the
        /// profit is -cost, the caption still says PROFIT, and Math.Abs
        /// would render that as a POSITIVE profit - the exact opposite of
        /// the truth. That case now keeps its minus sign.
        /// </summary>
        public string ProfitDisplay =>
            (!IsLoss && Profit < 0m)
                ? $"AED -{Math.Abs(Profit):N2}"
                : $"AED {Math.Abs(Profit):N2}";
        public string MarginDisplay => $"{MarginPercent:0.#}% margin";

        /// <summary>
        /// Mark-up actually achieved over total cost, which is the number
        /// Step 4's "margin profit %" box sets.
        ///
        /// The two are different measures and used to be easy to confuse:
        ///   MarginPercent = profit / NET REVENUE   (accounting margin)
        ///   MarkupPercent = profit / TOTAL COST    (Step 4's setting)
        ///
        /// Worked example, 6mm HD Grey FT + 12mm ASP + 6mm Clear FT:
        ///     cost basis = (48 + 25) / 0.85 + 45 = 130.88 per SQM
        ///     Step 4 at 15%                      = 150.51 per SQM
        /// Discount that to 140 and the mark-up actually earned is
        ///     (140 - 130.88) / 130.88 = 6.97%
        /// — less than half the intended 15%, for a 7% cut in price. Showing
        /// the mark-up makes that visible in the same units the price was set
        /// in; the accounting margin alone understates how much was given
        /// away.
        ///
        /// One caveat this figure depends on: Step 4 quotes EX-VAT, while
        /// Profit is measured against NET (gross / 1.05). If a Step 4 rate is
        /// typed into Step 7 as a VAT-INCLUSIVE price, the mark-up shown here
        /// lands near 9.5% rather than 15%, because a twentieth of the
        /// revenue is tax rather than income.
        /// </summary>
        public decimal MarkupPercent
        {
            get
            {
                decimal cost = TotalGlassCost + TotalProductionCost;
                if (cost <= 0m) return 0m;
                return Math.Round(Profit / cost * 100m, 1);
            }
        }

        /// <summary>Reads "7% mark-up over cost", or explains why it cannot.</summary>
        public string MarkupDisplay =>
            (TotalGlassCost + TotalProductionCost) <= 0m
                ? "mark-up not calculable — no costed rows"
                : $"{MarkupPercent:0.#}% mark-up over cost";

        /// <summary>Tile caption — flips wording instead of showing a minus sign.</summary>
        public string ProfitLabel => IsLoss ? "LOSS" : "PROFIT";

        // ── Commands ──────────────────────────────────────────────────────

        /// <summary>Recomputes the whole breakdown.</summary>
        public ICommand RecalculateCommand { get; }

        public ProfitLossStepViewModel()
        {
            RecalculateCommand = new RelayCommand(_ => FireRecalculate());

            Rows.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasRows));
        }

        // ══════════════════════════════════════════════════════════════
        //  BINDING TO THE REST OF THE WIZARD
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Wires this step to its data sources. Called once by the wizard
        /// shell after all step VMs are constructed. Any argument may be
        /// null; the step then simply produces an empty report.
        /// </summary>
        public void Bind(
            DataGridViewModel? dataGrid,
            DguParametersViewModel? dguVm,
            SguParametersViewModel? sguVm,
            Func<decimal>? grossProvider,
            LamParametersViewModel? lamVm = null,
            DguLamParametersViewModel? dguLamVm = null)
        {
            _dataGrid = dataGrid;
            _dguVm = dguVm;
            _sguVm = sguVm;
            _lamVm = lamVm;
            _dguLamVm = dguLamVm;
            _grossProvider = grossProvider;

            WireLiveRecalculation();
        }

        // ══════════════════════════════════════════════════════════════════
        //  LIVE RECALCULATION
        //
        //  Bind() used to store these references and subscribe to nothing, so
        //  the report only rebuilt when the step was entered or REFRESH was
        //  pressed. Edit a size in Step 5, come back, and the figures were
        //  from the previous visit — silently wrong rather than obviously
        //  empty, which is the worse failure for a money screen.
        //
        //  Everything the costing reads is now watched:
        //    · spec groups added or removed
        //    · item rows added or removed inside a group
        //    · width / height / quantity edits on any row
        //    · the Step 4 pricing panels (rates, yield, margin)
        //
        //  Recalculation is coalesced: a paste that changes fifty rows fires
        //  one rebuild, not fifty. Without that, bulk edits would re-run the
        //  whole Sheet Store lookup per keystroke.
        // ══════════════════════════════════════════════════════════════════

        private bool _liveWired;
        private bool _recalcQueued;

        private void WireLiveRecalculation()
        {
            if (_liveWired) return;
            _liveWired = true;

            if (_dataGrid?.SpecGroups is INotifyCollectionChanged groups)
            {
                groups.CollectionChanged += (_, e) =>
                {
                    // Watch whatever just arrived, stop watching what left.
                    if (e.NewItems != null)
                        foreach (SpecGroup g in e.NewItems.OfType<SpecGroup>())
                            WatchSpecGroup(g);

                    if (e.OldItems != null)
                        foreach (SpecGroup g in e.OldItems.OfType<SpecGroup>())
                            UnwatchSpecGroup(g);

                    QueueRecalculate();
                };
            }

            foreach (var g in _dataGrid?.SpecGroups ?? Enumerable.Empty<SpecGroup>())
                WatchSpecGroup(g);

            // Step 4 panels: a rate, yield or margin change moves the cost
            // basis, so the report must follow it.
            WatchPanel(_dguVm);
            WatchPanel(_sguVm);
            WatchPanel(_lamVm);
            WatchPanel(_dguLamVm);
        }

        private void WatchPanel(INotifyPropertyChanged? panel)
        {
            if (panel == null) return;
            panel.PropertyChanged += (_, __) => QueueRecalculate();
        }

        private void WatchSpecGroup(SpecGroup group)
        {
            if (group == null) return;

            // Detach first. A group can reach this method twice — added,
            // then seen again in a collection Reset — and a doubled handler
            // would rebuild the report twice per edit. Removing a handler
            // that was never attached is a no-op, so this is always safe.
            group.PropertyChanged -= OnSpecGroupChanged;
            group.PropertyChanged += OnSpecGroupChanged;

            if (group.ItemRows is INotifyCollectionChanged rows)
            {
                rows.CollectionChanged -= OnItemRowsChanged;
                rows.CollectionChanged += OnItemRowsChanged;
            }

            foreach (var row in group.ItemRows ?? Enumerable.Empty<GlassItemRow>())
                WatchItemRow(row);
        }

        private void UnwatchSpecGroup(SpecGroup group)
        {
            if (group == null) return;

            group.PropertyChanged -= OnSpecGroupChanged;

            if (group.ItemRows is INotifyCollectionChanged rows)
                rows.CollectionChanged -= OnItemRowsChanged;

            foreach (var row in group.ItemRows ?? Enumerable.Empty<GlassItemRow>())
                row.PropertyChanged -= OnItemRowChanged;
        }

        private void WatchItemRow(GlassItemRow row)
        {
            if (row == null) return;

            // Unsubscribe first: a row can be handed to Watch twice (added,
            // then seen again in a Reset), and a doubled handler would fire
            // two rebuilds per keystroke.
            row.PropertyChanged -= OnItemRowChanged;
            row.PropertyChanged += OnItemRowChanged;
        }

        private void OnSpecGroupChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Only the fields the costing actually reads.
            if (e.PropertyName is nameof(SpecGroup.SelectedSpecName)
                               or nameof(SpecGroup.SelectedGlassType))
            {
                QueueRecalculate();
            }
        }

        private void OnItemRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (GlassItemRow r in e.NewItems.OfType<GlassItemRow>())
                    WatchItemRow(r);

            if (e.OldItems != null)
                foreach (GlassItemRow r in e.OldItems.OfType<GlassItemRow>())
                    r.PropertyChanged -= OnItemRowChanged;

            // A Reset carries no item lists — re-watch the whole collection.
            if (e.Action == NotifyCollectionChangedAction.Reset
                && sender is IEnumerable<GlassItemRow> all)
            {
                foreach (var r in all) WatchItemRow(r);
            }

            QueueRecalculate();
        }

        private void OnItemRowChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Dimensions and quantity are the only row fields that change the
            // cut area. Reacting to every property would rebuild the report on
            // cosmetic edits such as the reference text.
            if (e.PropertyName is nameof(GlassItemRow.W1) or nameof(GlassItemRow.H1)
                               or nameof(GlassItemRow.W2) or nameof(GlassItemRow.H2)
                               or nameof(GlassItemRow.Qty))
            {
                QueueRecalculate();
            }
        }

        /// <summary>
        /// Coalesces a burst of edits into a single rebuild on the dispatcher's
        /// next idle turn.
        /// </summary>
        private void QueueRecalculate()
        {
            if (_recalcQueued) return;
            _recalcQueued = true;

            var dispatcher = Application.Current?.Dispatcher;

            if (dispatcher == null)
            {
                // No UI thread (unit tests) — run it straight through.
                _recalcQueued = false;
                _ = RecalculateAsync();
                return;
            }

            dispatcher.BeginInvoke(
                new Action(async () =>
                {
                    _recalcQueued = false;

                    // RecalculateAsync early-returns while a run is already
                    // in flight. Without re-queueing, an edit made during
                    // that run would be silently dropped and the report would
                    // sit one change behind the data — the exact staleness
                    // this wiring exists to remove.
                    if (IsBusy)
                    {
                        QueueRecalculate();
                        return;
                    }

                    await RecalculateAsync().ConfigureAwait(true);
                }),
                DispatcherPriority.Background);
        }

        private async void FireRecalculate()
        {
            // async void is confined to this command entry point and the
            // body cannot throw (RecalculateAsync swallows everything).
            await RecalculateAsync().ConfigureAwait(true);
        }

        // ══════════════════════════════════════════════════════════════
        //  THE CALCULATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Rebuilds every row and total. Never throws: any failure leaves
        /// the previous figures in place and reports through StatusMessage.
        /// </summary>
        public async Task RecalculateAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                var rows = new List<ProfitLossRow>();
                var specAreas = new Dictionary<int, decimal>();
                var specBases = new Dictionary<int, decimal>();
                decimal glassCost = 0m;
                decimal productionCost = 0m;
                int skipped = 0;

                var groups = _dataGrid?.SpecGroups?.ToList()
                             ?? new List<SpecGroup>();

                foreach (var group in groups)
                {
                    if (group == null) continue;

                    // Perimeter is still measured by MeasureGroup (a future
                    // edge-work charge will need it) but is deliberately
                    // discarded here: nothing in the P&L is priced per linear
                    // metre any more.
                    var (cutSqm, _) = MeasureGroup(group);
                    if (cutSqm <= 0m) continue;

                    // Kept for the per-specification view: cost per SQM needs
                    // the area this specification actually covers, and the
                    // row list alone cannot supply it (sheet counts are
                    // rounded up, so summing rows would overstate the area).
                    specAreas[group.GroupNumber] = cutSqm;

                    string specName = group.SelectedSpecName ?? "";

                    // ── Parse the build out of the spec name ──────────────
                    var spacerMatch = SpacerPattern.Match(specName);
                    bool spacerDetected = spacerMatch.Success;

                    bool isDgu =
                        (group.SelectedGlassType ?? "")
                            .IndexOf("DGU", StringComparison.OrdinalIgnoreCase) >= 0
                        || spacerDetected;

                    // PHASE J — laminated: two panes bonded by PVB. Detected
                    // from the spec name or the glass type. A LAM unit that
                    // is not a DGU still has an INNER pane, which the old
                    // code missed entirely (it costed only the outer).
                    var pvbMatch = PvbPattern.Match(specName);
                    bool pvbDetected = pvbMatch.Success;

                    bool isLam =
                        (group.SelectedGlassType ?? "")
                            .IndexOf("LAM", StringComparison.OrdinalIgnoreCase) >= 0
                        || pvbDetected;

                    // ══════════════════════════════════════════════════════
                    //  DGU+LAM — a THREE-pane hybrid:
                    //     outer + ASP + lam-outer + interlayer + lam-inner
                    //
                    //  It shows BOTH a spacer and an interlayer, which is the
                    //  only build that does. The outer/inner assumption below
                    //  costed just two of its three sheets and silently threw
                    //  a whole pane of glass away, so panes are now taken
                    //  from the spec itself rather than assumed.
                    // ══════════════════════════════════════════════════════
                    bool isDguLam = spacerDetected && pvbDetected;

                    // Pre-margin cost basis for THIS specification's own glass
                    // type — the number a quoted rate must be measured
                    // against.
                    //
                    // Placed here deliberately: it needs isDgu / isLam /
                    // isDguLam, which are parsed out of the spec name just
                    // above. An earlier version computed it before those
                    // flags existed, which is a use-before-declaration and
                    // would not compile.
                    //
                    // The build is read from the SPEC NAME rather than from
                    // whichever Step 4 tab is open: one proforma can mix an
                    // SGU and a DGU, and using the live selection would score
                    // both against the same panel.
                    specBases[group.GroupNumber] =
                        QuoteBasisFor(group.SelectedGlassType, isDgu, isLam, isDguLam);

                    // Either construction has a second pane to cost.
                    bool hasInnerPane = isDgu || isLam;

                    // Outer = first glass match. Inner = first match that
                    // starts after the spacer (so a DGU never double-counts
                    // the outer pane).
                    var glassMatches = GlassPattern.Matches(specName)
                        .Cast<Match>()
                        .ToList();

                    var outerMatch = glassMatches.FirstOrDefault();

                    Match? innerMatch = null;
                    if (isDguLam)
                    {
                        // Three-pane hybrid: cost EVERY pane the spec names.
                        // Handled in its own block below so the two-pane
                        // outer/inner logic is left exactly as it was for
                        // plain DGU and plain LAM units.
                    }
                    else if (hasInnerPane && glassMatches.Count > 0)
                    {
                        // Inner pane starts after the middle layer (ASP or
                        // PVB), so a two-pane unit never double-counts the
                        // outer pane.
                        int after =
                            spacerDetected ? spacerMatch.Index + spacerMatch.Length
                          : pvbDetected   ? pvbMatch.Index + pvbMatch.Length
                          : (outerMatch?.Index + outerMatch?.Length) ?? 0;

                        innerMatch = glassMatches.FirstOrDefault(m => m.Index >= after);
                    }

                    // ── Glass rows ────────────────────────────────────────
                    //
                    //  ROW ORDER MIRRORS THE PHYSICAL BUILD.
                    //
                    //  Rows used to come out as: every pane of glass first,
                    //  then the spacer and interlayer underneath. That reads
                    //  nothing like the product and made a DGU indistinguishable
                    //  from a laminate at a glance.
                    //
                    //  The layer rows are produced below as before, but they
                    //  are captured into `middleRows` instead of being appended,
                    //  then spliced in after the outer pane:
                    //
                    //      DGU      outer · ASP · inner
                    //      LAM      outer · PVB/SGP/EVA · inner
                    //      DGU+LAM  outer · ASP · lam outer · interlayer · lam inner
                    //
                    //  Only the ORDER changes. Every amount, every total and
                    //  every Kind is exactly what it was.
                    int spacerInsertAt = -1;
                    int interlayerInsertAt = -1;

                    if (isDguLam)
                    {
                        // One row per pane actually present in the spec.
                        // Labels follow the physical build so the P&L reads
                        // the same way the glass is assembled.
                        string[] paneLabels = { "Outer", "Lam Outer", "Lam Inner" };

                        for (int paneIndex = 0; paneIndex < glassMatches.Count; paneIndex++)
                        {
                            string paneLabel = paneIndex < paneLabels.Length
                                ? paneLabels[paneIndex]
                                : $"Pane {paneIndex + 1}";

                            var paneRow = await BuildGlassRowAsync(
                                group, paneLabel, glassMatches[paneIndex], cutSqm)
                                .ConfigureAwait(true);

                            if (paneRow != null)
                            {
                                rows.Add(paneRow);
                                glassCost += paneRow.Amount;
                            }
                            else skipped++;

                            // The spacer sits between pane 1 and pane 2, and
                            // the interlayer between pane 2 and pane 3, so
                            // remember where each boundary fell.
                            //
                            // The interlayer index allows for the spacer that
                            // will be spliced in ahead of it. Both positions
                            // are captured now, but the spacer is inserted
                            // FIRST and pushes everything after it down by
                            // one. Recording the raw index here produced
                            //     outer · spacer · interlayer · lam outer
                            // — the interlayer landing before the pane it is
                            // supposed to sit behind.
                            if (paneIndex == 0) spacerInsertAt = rows.Count;
                            if (paneIndex == 1) interlayerInsertAt = rows.Count + 1;
                        }
                    }
                    else if (outerMatch != null)
                    {
                        var row = await BuildGlassRowAsync(
                            group, "Outer", outerMatch, cutSqm)
                            .ConfigureAwait(true);

                        if (row != null)
                        {
                            rows.Add(row);
                            glassCost += row.Amount;
                        }
                        else skipped++;
                    }

                    // Two-pane builds: the middle layer belongs immediately
                    // after the outer pane, before the inner one is appended.
                    if (!isDguLam) spacerInsertAt = rows.Count;

                    if (!isDguLam && hasInnerPane && innerMatch != null)
                    {
                        var row = await BuildGlassRowAsync(
                            group, "Inner", innerMatch, cutSqm)
                            .ConfigureAwait(true);

                        if (row != null)
                        {
                            rows.Add(row);
                            glassCost += row.Amount;
                        }
                        else skipped++;
                    }

                    // Splice a layer row into build order rather than
                    // appending it, returning the next insert position.
                    //
                    // Takes and returns the index by value: a local function
                    // cannot take `ref` to a captured outer variable, and the
                    // two call sites need different counters anyway.
                    //
                    // Falls back to appending when the position was never set,
                    // so an unrecognised shape still reports its costs instead
                    // of silently dropping them.
                    int AddLayerRow(ProfitLossRow layerRow, int at)
                    {
                        if (at >= 0 && at <= rows.Count)
                        {
                            rows.Insert(at, layerRow);
                            return at + 1;
                        }

                        rows.Add(layerRow);
                        return at;
                    }

                    // ── Production rows ───────────────────────────────────
                    // DGU+LAM is tested FIRST: it satisfies both isDgu and
                    // isLam, so either of those branches would claim it and
                    // charge only half its production.
                    if (isDguLam)
                    {
                        // ══════════════════════════════════════════════════
                        //  DGU+LAM production has THREE parts:
                        //
                        //   1. Air spacer, per SQM, which the business says
                        //      already covers cutting, tempering and
                        //      production on the DGU side.
                        //   2. The outsourced interlayer, costed at what we
                        //      PAY the laminator (InterlayerBuyPrice). The
                        //      customer-facing 110 belongs in Step 4's sell
                        //      rate; costing the job at 110 would erase the
                        //      spread the business actually earns and report
                        //      a profit lower than reality.
                        //   3. Cutting and tempering on ALL THREE panes,
                        //      each at its own rate. The rates are not
                        //      equal — tinted glass tempers dearer than
                        //      clear — so they are summed individually
                        //      rather than doubled from one figure.
                        // ══════════════════════════════════════════════════
                        // Zero unless this job was given a spacer charge in
                        // Step 4, in which case no spacer row is produced.
                        decimal spacerPrice = _dguLamVm?.SpacerPrice ?? 0m;

                        if (spacerPrice > 0m)
                        {
                            int spacerMm = ParseInt(spacerMatch.Groups["mm"].Value);
                            decimal spacerAmount = Math.Round(cutSqm * spacerPrice, 2);

                            spacerInsertAt = AddLayerRow(new ProfitLossRow
                            {
                                Kind = "Spacer",
                                SpecNumber = group.GroupNumber,
                                SpecName = group.SelectedSpecName ?? "",
                                Description = BuildRowDescription(
                                    group,
                                    $"{spacerMm}mm ASP (includes cutting, tempering and production)",
                                    $"{cutSqm:0.##} SQM"),
                                Quantity = Math.Round(cutSqm, 2),
                                PricePerSqm = spacerPrice,
                                Amount = spacerAmount
                            }, spacerInsertAt);

                            // Inserting the spacer pushed every later row one
                            // place right, including the boundary the
                            // interlayer was going to use. Without this the
                            // hybrid came out as outer, spacer, interlayer,
                            // lam-outer, lam-inner -- the interlayer floating
                            // above the pane it bonds.
                            if (interlayerInsertAt >= spacerInsertAt - 1)
                                interlayerInsertAt++;

                            productionCost += spacerAmount;
                        }

                        decimal interlayerBuy = _dguLamVm?.InterlayerBuyPrice ?? 0m;

                        // All three panes, each at its own rate.
                        decimal lamProcessing =
                            (_dguLamVm?.TotalCuttingCost ?? 0m) +
                            (_dguLamVm?.TotalTemperingCost ?? 0m);

                        decimal lamRate = interlayerBuy + lamProcessing;

                        if (lamRate > 0m)
                        {
                            decimal lamAmount = Math.Round(cutSqm * lamRate, 2);

                            string interlayerType = _dguLamVm?.InterlayerType ?? "PVB";
                            string interlayerMm = pvbMatch.Groups["mm"].Value;

                            string lamLabel = interlayerMm.Length > 0
                                ? $"{interlayerMm}mm {interlayerType} interlayer (outsourced) + cutting/tempering (all three panes)"
                                : $"{interlayerType} interlayer (outsourced) + cutting/tempering (all three panes)";

                            interlayerInsertAt = AddLayerRow(new ProfitLossRow
                            {
                                Kind = "Production",
                                SpecNumber = group.GroupNumber,
                                SpecName = group.SelectedSpecName ?? "",
                                Description = BuildRowDescription(
                                    group, lamLabel, $"{cutSqm:0.##} SQM"),
                                Quantity = Math.Round(cutSqm, 2),
                                PricePerSqm = lamRate,
                                Amount = lamAmount
                            }, interlayerInsertAt);

                            productionCost += lamAmount;
                        }
                    }
                    else if (isDgu)
                    {
                        // ══════════════════════════════════════════════════
                        //  ASP IS PRICED PER SQM, NOT PER LINEAR METRE.
                        //
                        //  Step 4's CalculateDguRate builds the sell rate as
                        //      (outer + inner) / yield + spacerPrice
                        //  where every term is AED per SQM. The ASP table
                        //  37.5 + ((w-6)/2)*2.5 therefore yields 45.00 PER SQM
                        //  at 12mm - the figure the business quotes.
                        //
                        //  This code multiplied that same 45.00 by the
                        //  PERIMETER in linear metres. On 20 panes of
                        //  1.5 x 1.2 m that is 108 LM against 36 SQM, so the
                        //  spacer cost came out 4,860 instead of 1,620 -
                        //  overstated by 3,240 AED, three times too high, and
                        //  the error grows with every small pane because
                        //  perimeter-to-area rises as panes shrink.
                        //
                        //  Costing must use the same unit the price was
                        //  defined in, so this is now cut area x rate.
                        //
                        //  The stated business rule is also that the ASP
                        //  price INCLUDES cutting, tempering and production
                        //  for DGU, which is why there is no separate
                        //  production line in this branch.
                        // ══════════════════════════════════════════════════
                        decimal spacerPrice = _dguVm?.SpacerPrice ?? 0m;

                        if (spacerPrice > 0m && cutSqm > 0m)
                        {
                            int spacerMm = spacerDetected
                                ? ParseInt(spacerMatch.Groups["mm"].Value)
                                : 0;

                            decimal amount = Math.Round(cutSqm * spacerPrice, 2);

                            spacerInsertAt = AddLayerRow(new ProfitLossRow
                            {
                                Kind = "Spacer",
                                SpecNumber = group.GroupNumber,
                                SpecName = group.SelectedSpecName ?? "",
                                Description = BuildRowDescription(
                                    group,
                                    $"{spacerMm}mm ASP (includes cutting, tempering and production)",
                                    $"{cutSqm:0.##} SQM"),
                                Quantity = Math.Round(cutSqm, 2),
                                PricePerSqm = spacerPrice,
                                Amount = amount
                            }, spacerInsertAt);

                            productionCost += amount;
                        }
                    }
                    else if (isLam)
                    {
                        // PHASE J — laminated production: PVB interlayer
                        // material plus cutting and tempering on BOTH panes.
                        // Priced per SQM of laminated area (unlike the DGU
                        // spacer, which is per linear metre of perimeter).
                        decimal pvbRate = _lamVm?.PvbMaterialRate ?? 0m;

                        decimal processing =
                            (_lamVm?.OuterCuttingCost ?? 0m) +
                            (_lamVm?.InnerCuttingCost ?? 0m) +
                            (_lamVm?.OuterTemperingCost ?? 0m) +
                            (_lamVm?.InnerTemperingCost ?? 0m);

                        decimal lamRate = pvbRate + processing;

                        if (lamRate > 0m)
                        {
                            decimal amount = Math.Round(cutSqm * lamRate, 2);

                            string pvbMm = pvbDetected
                                ? pvbMatch.Groups["mm"].Value
                                : "";

                            // Report the interlayer the spec actually names.
                            // Now that SGP and EVA are matched, hardcoding
                            // "PVB" would print the wrong material on a real
                            // cost line.
                            string pvbType = pvbDetected
                                ? pvbMatch.Groups["type"].Value.ToUpperInvariant()
                                : "PVB";

                            string label = pvbMm.Length > 0
                                ? $"{pvbMm}mm {pvbType} interlayer + cutting/tempering (both panes)"
                                : $"{pvbType} interlayer + cutting/tempering (both panes)";

                            spacerInsertAt = AddLayerRow(new ProfitLossRow
                            {
                                Kind = "Production",
                                SpecNumber = group.GroupNumber,
                                SpecName = group.SelectedSpecName ?? "",
                                Description = BuildRowDescription(
                                    group, label, $"{cutSqm:0.##} SQM"),
                                Quantity = Math.Round(cutSqm, 2),
                                PricePerSqm = lamRate,
                                Amount = amount
                            }, spacerInsertAt);

                            productionCost += amount;
                        }
                    }
                    else
                    {
                        // Single glazing: cutting + tempering over the cut area.
                        decimal rate =
                            (_sguVm?.CuttingCostPerSqm ?? 0m) +
                            (_sguVm?.TemperingCostPerSqm ?? 0m);

                        if (rate > 0m)
                        {
                            decimal amount = Math.Round(cutSqm * rate, 2);

                            rows.Add(new ProfitLossRow
                            {
                                Kind = "Production",
                                SpecNumber = group.GroupNumber,
                                SpecName = group.SelectedSpecName ?? "",
                                Description = BuildRowDescription(
                                    group, "Cutting + tempering", $"{cutSqm:0.##} SQM"),
                                Quantity = Math.Round(cutSqm, 2),
                                PricePerSqm = rate,
                                Amount = amount
                            });

                            productionCost += amount;
                        }
                    }
                }

                // ── Publish rows ──────────────────────────────────────────
                Rows.Clear();
                foreach (var r in rows) Rows.Add(r);
                OnPropertyChanged(nameof(HasRows));

                // ── Publish the same rows grouped by specification ────────
                //
                // Built from the finished flat list rather than inside the
                // costing loop: the loop already splices layer rows into
                // build order, and rebuilding that ordering a second time
                // would be two places to keep in step. Grouping afterwards
                // inherits the order for free.
                //
                // Rate checks the user has typed are carried across a rebuild
                // by specification name. A recalculation fires on every edit
                // now, so discarding them would make the field unusable —
                // the value would vanish while being typed next to it.
                var previousQuotes = SpecGroupsView
                    .Where(g => g.QuotedRatePerSqm > 0m)
                    .ToDictionary(g => g.SpecName, g => g.QuotedRatePerSqm,
                                  StringComparer.OrdinalIgnoreCase);

                SpecGroupsView.Clear();

                foreach (var bySpec in rows
                             .GroupBy(r => r.SpecNumber)
                             .OrderBy(g => g.Key))
                {
                    var first = bySpec.First();

                    var specGroup = new SpecCostGroup
                    {
                        SpecNumber = bySpec.Key,
                        SpecName = first.SpecName ?? "",
                        TotalCost = Math.Round(bySpec.Sum(r => r.Amount), 2),
                        CutSqm = specAreas.TryGetValue(bySpec.Key, out decimal a)
                            ? Math.Round(a, 2)
                            : 0m,
                        QuoteBasisPerSqm = specBases.TryGetValue(bySpec.Key, out decimal b)
                            ? b
                            : 0m
                    };

                    foreach (var r in bySpec) specGroup.Rows.Add(r);

                    if (previousQuotes.TryGetValue(specGroup.SpecName, out decimal q))
                        specGroup.QuotedRatePerSqm = q;

                    specGroup.NotifyTotalsChanged();
                    SpecGroupsView.Add(specGroup);
                }

                OnPropertyChanged(nameof(HasSpecGroups));

                // ── Totals ────────────────────────────────────────────────
                decimal gross = 0m;
                try { gross = _grossProvider?.Invoke() ?? 0m; }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ProfitLossStepViewModel] gross provider: {ex.Message}");
                }

                GrossVatInclusive = gross;
                NetExVat = Math.Round(gross / VatDivisor, 2);
                TotalGlassCost = Math.Round(glassCost, 2);
                TotalProductionCost = Math.Round(productionCost, 2);

                decimal profit = NetExVat - TotalGlassCost - TotalProductionCost;
                Profit = Math.Round(profit, 2);

                // Only call it a loss when there is revenue to lose against.
                // With gross at zero the negative figure is simply
                // "cost known, revenue not entered yet" - painting that red
                // tells the user they are losing money when they are not.
                IsLoss = gross > 0m && Profit < 0m;

                MarginPercent = NetExVat > 0m
                    ? Math.Round(Profit / NetExVat * 100m, 1)
                    : 0m;

                StatusMessage = BuildStatus(
                    groups.Count, rows.Count, skipped, gross);
            }
            catch (Exception ex)
            {
                // A P&L failure must never break the wizard.
                Debug.WriteLine($"[ProfitLossStepViewModel] Recalculate failed: {ex.Message}");
                StatusMessage =
                    "Could not complete the calculation — check Sheet Store data and try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string BuildStatus(
            int groupCount, int rowCount, int skipped, decimal gross)
        {
            if (groupCount == 0)
                return "No spec groups in Step 5 yet — add glass items first.";

            // REVENUE MISSING IS NOT A LOSS.
            //
            // When the gross provider returns zero - typically because the
            // invoice totals in Step 7 have not been produced yet - net is
            // zero and Profit becomes -(total cost). The figures used to be
            // presented as a genuine loss, so the user could not tell an
            // unprofitable job from an incomplete one. Say so plainly.
            if (gross <= 0m && rowCount > 0)
                return "Costs are calculated, but this invoice has no revenue " +
                       "total yet — the profit figure below is NOT a real loss. " +
                       "Complete Step 7 (invoice totals), then recalculate.";

            if (rowCount == 0)
                return "Nothing could be costed. The glass in this invoice is not in " +
                       "Sheet Store — add it there, or treat this figure as unavailable.";

            // Naming the cost basis matters: the same job shows a different
            // profit at purchase vs selling price, and a figure whose basis
            // is unstated is a figure nobody can check.
            string basis =
                $"{rowCount} line(s) from {groupCount} spec group(s) · " +
                $"glass costed at {CostBasisLabel} · " +
                $"sheets = ceil(cut area × (1 + {WastagePercent:0.#}% wastage) ÷ sheet sqm) · " +
                $"VAT stripped at 5%.";

            return skipped > 0
                ? basis + $" {skipped} pane(s) skipped — not priced in Sheet Store."
                : basis;
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Total cut area and total perimeter for one spec group.
        /// A second pane (W2/H2) counts toward area but not perimeter —
        /// the spacer follows the outer pane only.
        /// </summary>
        // ══════════════════════════════════════════════════════════════════
        //  QUOTE BASIS PER SPECIFICATION
        //
        //  Mirrors Step 4's rate formulas, stopping one step short: every
        //  Calculate*Rate is  basis x (1 + margin%),  and this returns the
        //  basis. That is the number a quoted rate has to be measured
        //  against.
        //
        //  Worked example, 6mm Clear FT + 12mm ASP + 6mm Clear FT:
        //      (25 + 25) / 0.85 + 45 = 103.82
        //      x 1.15 = 119.39     x 1.10 = 114.20
        //  So entering 114.20 reads as 10%, which is what the shop expects.
        //
        //  The build is identified from the SPEC NAME first (isDgu / isLam /
        //  isDguLam were already parsed out of it) and only falls back to the
        //  group's glass-type text. A proforma can hold several different
        //  builds at once, so using the live Step 4 selection would score
        //  every specification against whichever tab was last open.
        //
        //  Returns 0 when the owning panel is not bound, which makes the
        //  caller fall back to materials cost and say so on screen rather
        //  than silently quoting a wrong percentage.
        // ══════════════════════════════════════════════════════════════════

        private static decimal SafeYield(decimal yieldFactor) =>
            yieldFactor <= 0m ? 1m : yieldFactor;

        private decimal QuoteBasisFor(
            string? glassType, bool isDgu, bool isLam, bool isDguLam)
        {
            string type = (glassType ?? "").Trim().ToUpperInvariant();

            // Spec-name evidence wins: it describes the product actually
            // costed, whereas the type text can be blank or stale.
            if (isDguLam || type == "DGU+LAM")
            {
                var vm = _dguLamVm;
                if (vm == null) return 0m;

                decimal basis =
                    (vm.OuterBasePrice + vm.LamOuterBasePrice + vm.LamInnerBasePrice)
                        / SafeYield(vm.YieldFactor)
                    + vm.TotalCuttingCost
                    + vm.TotalTemperingCost
                    + vm.InterlayerSellPrice
                    + vm.SpacerPrice;

                return Math.Round(basis, 2);
            }

            if (isLam || type == "LAM")
            {
                var vm = _lamVm;
                if (vm == null) return 0m;

                decimal basis =
                    (vm.OuterBasePrice + vm.InnerBasePrice) / SafeYield(vm.YieldFactor)
                    + vm.PvbMaterialRate
                    + vm.OuterCuttingCost + vm.InnerCuttingCost
                    + vm.OuterTemperingCost + vm.InnerTemperingCost;

                return Math.Round(basis, 2);
            }

            if (isDgu || type == "DGU")
            {
                var vm = _dguVm;
                if (vm == null) return 0m;

                decimal basis =
                    (vm.OuterBasePrice + vm.InnerBasePrice) / SafeYield(vm.YieldFactor)
                    + vm.SpacerPrice;

                return Math.Round(basis, 2);
            }

            // SINGLE GLAZED — the default, and the only build with no marker
            // of its own in the spec name.
            //
            // The other three are recognised from the specification text: a
            // spacer means DGU, an interlayer means LAM, both mean the
            // hybrid. Single glazing has neither, so it can only be reached
            // by elimination. Requiring the glass-type text to read exactly
            // "SGU" meant a specification with that field blank, or worded
            // "Single Glazed", got no basis at all — the rate box then fell
            // back to materials cost and reported a margin against the wrong
            // number, silently.
            //
            // Reaching here means the spec carries no spacer and no
            // interlayer, so a single pane is the correct reading.
            if (type == "SGU" || type.Length == 0 || type.Contains("SINGLE"))
            {
                var vm = _sguVm;
                if (vm == null) return 0m;

                decimal basis =
                    vm.BasePricePerSqm / SafeYield(vm.YieldFactor)
                    + vm.CuttingCostPerSqm
                    + vm.TemperingCostPerSqm;

                return Math.Round(basis, 2);
            }

            return 0m;
        }

        private static (decimal CutSqm, decimal PerimeterLm) MeasureGroup(SpecGroup group)
        {
            decimal cutSqm = 0m;
            decimal perimeterLm = 0m;

            var rows = group.ItemRows;
            if (rows == null) return (0m, 0m);

            foreach (var row in rows)
            {
                if (row == null) continue;

                int qty = row.Qty > 0 ? row.Qty : 0;
                if (qty == 0) continue;

                decimal area = (row.W1 * (decimal)row.H1) / 1_000_000m;

                if (row.W2 > 0 && row.H2 > 0)
                    area += (row.W2 * (decimal)row.H2) / 1_000_000m;

                cutSqm += area * qty;

                perimeterLm += (2m * (row.W1 + row.H1) / 1000m) * qty;
            }

            return (cutSqm, perimeterLm);
        }

        /// <summary>
        /// Prices one pane against the Sheet Store. Returns null when the
        /// store has no usable purchase price (caller counts it as skipped).
        /// </summary>
        /// <summary>
        /// Builds the detail half of a P&amp;L row.
        ///
        /// The full specification is carried separately on
        /// <see cref="ProfitLossRow.SpecName"/> and rendered in the SPEC
        /// column, so it deliberately is NOT repeated here — printing it in
        /// both places made every row unreadably long.
        /// </summary>
        private static string BuildRowDescription(
            SpecGroup group, string part, string detail)
        {
            return string.IsNullOrWhiteSpace(detail) ? part : $"{part} · {detail}";
        }

        private async Task<ProfitLossRow?> BuildGlassRowAsync(
            SpecGroup group,
            string side,
            Match glassMatch,
            decimal cutSqm)
        {
            int specNumber = group.GroupNumber;

            int thicknessMm = ParseInt(glassMatch.Groups["thk"].Value);
            string color = (glassMatch.Groups["color"].Value ?? "").Trim();

            if (thicknessMm <= 0 || color.Length == 0) return null;

            // ══════════════════════════════════════════════════════════
            //  PHASE G — SHEET RESOLUTION, IN PRIORITY ORDER
            //
            //  1. The sheet the user CHOSE in Step 4 (carried on the spec
            //     group). This wins: costing must reflect the decision
            //     the user actually made.
            //  2. The first sheet the Sheet Store stocks for this
            //     colour + thickness.
            //  3. The shop default (2440 × 1830).
            //
            //  The source is recorded on the row so a surprising number
            //  is always explainable, instead of silently costing a
            //  sheet nobody picked.
            // ══════════════════════════════════════════════════════════
            bool isInner = side.Equals("Inner", StringComparison.OrdinalIgnoreCase);

            int sheetW = 0;
            int sheetH = 0;
            string source;

            if (isInner && group.HasInnerSheet)
            {
                sheetW = group.InnerSheetWidthMm;
                sheetH = group.InnerSheetHeightMm;
                source = "chosen in Step 4";
            }
            else if (!isInner && group.HasOuterSheet)
            {
                sheetW = group.OuterSheetWidthMm;
                sheetH = group.OuterSheetHeightMm;
                source = "chosen in Step 4";
            }
            else
            {
                var sizes = await SheetStorePricingService
                    .GetSizesAsync(color, thicknessMm).ConfigureAwait(true);

                if (sizes.Count > 0)
                {
                    sheetW = sizes[0].W;
                    sheetH = sizes[0].H;
                    source = "from Sheet Store";
                }
                else
                {
                    sheetW = DefaultSheetWidthMm;
                    sheetH = DefaultSheetHeightMm;
                    source = "default sheet — not in Sheet Store";
                }
            }

            decimal sheetSqm = Math.Round((sheetW * (decimal)sheetH) / 1_000_000m, 4);
            if (sheetSqm <= 0m) return null;

            // ══════════════════════════════════════════════════════════
            //  PRICE MUST FOLLOW THE SHEET WE ARE COSTING.
            //
            //  This used to call TryGetPriceAsync(color, thickness), which
            //  returns the CHEAPEST row for that colour+thickness
            //  regardless of size. The amount below then multiplied that
            //  price by the area of a DIFFERENT sheet - the one actually
            //  chosen. So 6mm Clear costed at the 2440x1830 rate while
            //  being multiplied by 3660x2440 of area silently understated
            //  the cost on every jumbo job.
            //
            //  TryGetPriceForSizeAsync matches colour + thickness + size
            //  (either orientation), and falls back to the cheapest
            //  positive price only when that exact size is not stocked.
            //  Same unit, same sheet, same money.
            // ══════════════════════════════════════════════════════════
            var price = await SheetStorePricingService
                .TryGetPriceForSizeAsync(color, thicknessMm, sheetW, sheetH)
                .ConfigureAwait(true);

            // Which column of the Sheet Store row to cost against. Selling
            // falls back to purchase (and vice versa) rather than costing a
            // pane at zero, because a missing price must never look free.
            decimal purchaseRate = price?.PurchasePerSqm ?? 0m;
            decimal sellRate = price?.SellPerSqm ?? 0m;

            decimal unitRate = CostAtSellingPrice
                ? (sellRate > 0m ? sellRate : purchaseRate)
                : (purchaseRate > 0m ? purchaseRate : sellRate);

            if (unitRate <= 0m) return null;

            // ── Sheets required, wastage applied, always at least one ─────
            //
            // Wastage inflates the area BEFORE the sheet count is rounded up,
            // so its effect is a step function: raising it from 10% to 20%
            // often changes nothing, then a single extra point adds a whole
            // sheet. That reads as "wastage is not being applied" when it is,
            // so the row now states the inflated area it was rounded from —
            // see the description below.
            decimal needed = cutSqm * (1m + (WastagePercent / 100m));
            int sheets = (int)Math.Ceiling(needed / sheetSqm);
            if (sheets < 1) sheets = 1;

            decimal amount = Math.Round(sheetSqm * sheets * unitRate, 2);

            return new ProfitLossRow
            {
                Kind = "Glass",
                SpecNumber = specNumber,
                SpecName = group?.SelectedSpecName ?? "",
                // Full specification from the Step 5 DataGrid, so the row
                // reads as the product actually being quoted rather than a
                // fragment. Falls back to the parsed pane when a group has
                // no spec selected.
                Description = BuildRowDescription(
                    group,
                    $"{side} pane · {thicknessMm}mm {color}",
                    // Shows the cut area, the wastage allowance and the area
                    // actually rounded up, so the sheet count can be checked
                    // by hand and wastage is visibly doing something even
                    // when the count has not changed.
                    $"{cutSqm:0.##} + {WastagePercent:0.#}% = {needed:0.##} sqm " +
                    $"→ {sheets} sheet(s) · {source}"),
                SheetSource = source,
                SheetWidthMm = sheetW,
                SheetHeightMm = sheetH,
                SheetSqm = sheetSqm,
                Quantity = sheets,
                PricePerSqm = unitRate,
                Amount = amount
            };
        }

        private static int ParseInt(string? text) =>
            int.TryParse(
                (text ?? "").Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value)
                ? value
                : 0;

        // ══════════════════════════════════════════════════════════════
        //  PERSISTENCE
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds the JSON-serializable form consumed by
        /// ProformaInvoiceMapper.BuildSnapshot(). Always returns an object
        /// (never null) so the mapper needs no extra guard.
        /// </summary>
        public ProfitLossSnapshot BuildSnapshot()
        {
            var snap = new ProfitLossSnapshot
            {
                GrossVatInclusive = GrossVatInclusive,
                NetExVat = NetExVat,
                TotalGlassCost = TotalGlassCost,
                TotalProductionCost = TotalProductionCost,
                Profit = Profit,
                MarginPercent = MarginPercent,
                WastagePercent = WastagePercent,
                CostAtSellingPrice = CostAtSellingPrice
            };

            foreach (var row in Rows)
                snap.Rows.Add(row.ToSnapshot());

            return snap;
        }
    }
}
