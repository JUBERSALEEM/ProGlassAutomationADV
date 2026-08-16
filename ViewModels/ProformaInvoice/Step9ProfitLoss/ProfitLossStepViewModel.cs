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
//   DGU spacer   = round(perimeterLm · DguVM.SpacerPrice, 2)
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

        /// <summary>e.g. "46.00 / SQM" (or "/ LM" for spacer rows).</summary>
        public string PriceDisplay =>
            string.Equals(Kind, "Spacer", StringComparison.OrdinalIgnoreCase)
                ? $"{PricePerSqm:N2} / LM"
                : $"{PricePerSqm:N2} / SQM";

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
        private Func<decimal>? _grossProvider;

        // ══════════════════════════════════════════════════════════════
        //  SPEC NAME REGEXES
        // ══════════════════════════════════════════════════════════════

        private const string TreatmentAlternation =
            @"(?:FT|Annealed|Heat\s+Strengthened|Heat\s+Soaked)";

        private static readonly Regex GlassPattern = new(
            @"(?<thk>\d+)\s*mm\s+(?<color>.+?)\s+" + TreatmentAlternation + @"\s+Glass",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SpacerPattern = new(
            @"\+\s*(?<mm>\d+)\s*mm\s+ASP",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// PHASE J — laminated interlayer, e.g. "+ 1.52mm Clear PVB".
        /// A LAM unit has TWO glass panes like a DGU, but the middle layer
        /// is PVB priced per SQM of laminated area, not per LM of perimeter.
        /// </summary>
        private static readonly Regex PvbPattern = new(
            @"\+\s*(?<mm>[\d\.]+)\s*mm\s+(?<color>.+?)\s+PVB",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ══════════════════════════════════════════════════════════════
        //  OUTPUTS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Costed breakdown, rebuilt by every recalculation.</summary>
        public ObservableCollection<ProfitLossRow> Rows { get; } = new();

        /// <summary>True when <see cref="Rows"/> is empty (drives the empty state).</summary>
        public bool HasRows => Rows.Count > 0;

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
        public string ProfitDisplay => $"AED {Math.Abs(Profit):N2}";
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
            LamParametersViewModel? lamVm = null)
        {
            _dataGrid = dataGrid;
            _dguVm = dguVm;
            _sguVm = sguVm;
            _lamVm = lamVm;
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

                    var (cutSqm, perimeterLm) = MeasureGroup(group);
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
                    if (hasInnerPane && glassMatches.Count > 0)
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
                    if (outerMatch != null)
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

                    if (hasInnerPane && innerMatch != null)
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
                    if (isDgu)
                    {
                        // Air spacer is priced per linear metre of perimeter.
                        decimal spacerPrice = _dguVm?.SpacerPrice ?? 0m;

                        if (spacerPrice > 0m && perimeterLm > 0m)
                        {
                            int spacerMm = spacerDetected
                                ? ParseInt(spacerMatch.Groups["mm"].Value)
                                : 0;

                            decimal amount = Math.Round(perimeterLm * spacerPrice, 2);

                            rows.Add(new ProfitLossRow
                            {
                                Kind = "Spacer",
                                SpecNumber = group.GroupNumber,
                                SpecName = group.SelectedSpecName ?? "",
                                Description = BuildRowDescription(
                                    group,
                                    $"{spacerMm}mm air spacer (incl. cutting/tempering)",
                                    $"{perimeterLm:0.##} LM"),
                                Quantity = Math.Round(perimeterLm, 2),
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

                            string label = pvbMm.Length > 0
                                ? $"{pvbMm}mm PVB interlayer + cutting/tempering (both panes)"
                                : "PVB interlayer + cutting/tempering (both panes)";

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

            string basis =
                $"{rowCount} line(s) from {groupCount} spec group(s) · " +
                $"wastage {WastagePercent:0.#}% · VAT stripped at 5%.";

            return skipped > 0
                ? basis + $" {skipped} pane(s) skipped — no Sheet Store purchase price."
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

            // ── Price: no purchase price → do not fake a zero cost ────────
            var price = await SheetStorePricingService
                .TryGetPriceAsync(color, thicknessMm).ConfigureAwait(true);

            decimal purchase = price?.PurchasePerSqm ?? 0m;
            if (purchase <= 0m) return null;

            // ── Sheets required, wastage applied, always at least one ─────
            decimal needed = cutSqm * (1m + (WastagePercent / 100m));
            int sheets = (int)Math.Ceiling(needed / sheetSqm);
            if (sheets < 1) sheets = 1;

            decimal amount = Math.Round(sheetSqm * sheets * purchase, 2);

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
                PricePerSqm = purchase,
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
