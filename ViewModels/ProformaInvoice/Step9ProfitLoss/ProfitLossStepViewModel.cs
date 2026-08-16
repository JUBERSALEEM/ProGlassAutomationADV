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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
        //  Rather than silently picking one, the basis is now explicit,
        //  defaults to Purchase (the true-profit answer, and the previous
        //  behaviour, so nothing changes unless it is deliberately switched)
        //  and is named in the status line so a figure is never ambiguous.
        // ══════════════════════════════════════════════════════════════

        private bool _costAtSellingPrice;
        /// <summary>
        /// False (default) = cost glass at Sheet Store PURCHASE price.
        /// True = cost at SELLING price, matching the Step 4 base rates.
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

                            rows.Add(new ProfitLossRow
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
                            });

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

                            rows.Add(new ProfitLossRow
                            {
                                Kind = "Production",
                                SpecNumber = group.GroupNumber,
                                SpecName = group.SelectedSpecName ?? "",
                                Description = BuildRowDescription(
                                    group, lamLabel, $"{cutSqm:0.##} SQM"),
                                Quantity = Math.Round(cutSqm, 2),
                                PricePerSqm = lamRate,
                                Amount = lamAmount
                            });

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

                            rows.Add(new ProfitLossRow
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
                            });

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

                            rows.Add(new ProfitLossRow
                            {
                                Kind = "Production",
                                SpecNumber = group.GroupNumber,
                                SpecName = group.SelectedSpecName ?? "",
                                Description = BuildRowDescription(
                                    group, label, $"{cutSqm:0.##} SQM"),
                                Quantity = Math.Round(cutSqm, 2),
                                PricePerSqm = lamRate,
                                Amount = amount
                            });

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
                    $"{sheets} sheet(s) · {source}"),
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
                WastagePercent = WastagePercent
            };

            foreach (var row in Rows)
                snap.Rows.Add(row.ToSnapshot());

            return snap;
        }
    }
}
