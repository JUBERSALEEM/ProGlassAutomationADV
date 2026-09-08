// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/JobProfitability/JobDetailViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Job Profitability — Detail Sidebar (drill-down per PI)
//
// WHAT IT IS
//   When the user clicks a row in the P&L Summary table, this VM loads
//   the FULL snapshot for that PI and exposes:
//     • Header info (PI#, company, customer, project, salesman, dates)
//     • P&L line items (glass, spacer, production — each with calculation)
//     • Cost breakdown (glass cost, production cost, total cost)
//     • Revenue breakdown (gross, net, VAT)
//     • Profit calculation (revenue − cost = profit, margin %)
//
// WHY
//   The P&L Summary shows only totals. Users need to cross-check HOW
//   each number was derived. This sidebar makes every AED traceable.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.ProformaInvoice.TargetPricing;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProGlassAutomation.ViewModels.JobProfitability
{
    public class JobDetailViewModel : ViewModelBase
    {
        private readonly IProformaInvoiceRepository _invoices;

        /// <summary>ProformaId of the load currently in flight. A double-click
        /// first fires the single-click handler(s) and then the full-detail
        /// command, so this guard keeps one gesture from starting several
        /// identical DB reads for the same invoice.</summary>
        private string? _loadingProformaId;

        public JobDetailViewModel(IProformaInvoiceRepository invoices)
        {
            _invoices = invoices;
            LineItems = new ObservableCollection<JobDetailLineItem>();
            TargetSpecs = new ObservableCollection<JobTargetSpecLine>();
            CloseCommand = new RelayCommand(_ => ClearSelection());
        }

        // ── Header ──────────────────────────────────────────────────────
        private string _proformaId = "";
        public string ProformaId { get => _proformaId; set { if (_proformaId == value) return; _proformaId = value ?? ""; OnPropertyChanged(); } }

        private string _companyName = "";
        public string CompanyName { get => _companyName; set { if (_companyName == value) return; _companyName = value ?? ""; OnPropertyChanged(); } }

        private string _customerName = "";
        public string CustomerName { get => _customerName; set { if (_customerName == value) return; _customerName = value ?? ""; OnPropertyChanged(); } }

        private string _projectName = "";
        public string ProjectName { get => _projectName; set { if (_projectName == value) return; _projectName = value ?? ""; OnPropertyChanged(); } }

        private string _salesman = "";
        public string Salesman { get => _salesman; set { if (_salesman == value) return; _salesman = value ?? ""; OnPropertyChanged(); } }

        private string _issueDateDisplay = "";
        public string IssueDateDisplay { get => _issueDateDisplay; set { if (_issueDateDisplay == value) return; _issueDateDisplay = value ?? ""; OnPropertyChanged(); } }

        private string _glassTypeSummary = "";
        public string GlassTypeSummary { get => _glassTypeSummary; set { if (_glassTypeSummary == value) return; _glassTypeSummary = value ?? ""; OnPropertyChanged(); } }

        // ── Financial Summary ───────────────────────────────────────────
        private decimal _grossRevenue;
        public decimal GrossRevenue { get => _grossRevenue; set { if (_grossRevenue == value) return; _grossRevenue = value; OnPropertyChanged(); OnPropertyChanged(nameof(GrossRevenueDisplay)); } }
        public string GrossRevenueDisplay => $"AED {GrossRevenue:N2}";

        private decimal _netRevenue;
        public decimal NetRevenue { get => _netRevenue; set { if (_netRevenue == value) return; _netRevenue = value; OnPropertyChanged(); OnPropertyChanged(nameof(NetRevenueDisplay)); } }
        public string NetRevenueDisplay => $"AED {NetRevenue:N2}";

        private decimal _vatAmount;
        public decimal VatAmount { get => _vatAmount; set { if (_vatAmount == value) return; _vatAmount = value; OnPropertyChanged(); OnPropertyChanged(nameof(VatAmountDisplay)); } }
        public string VatAmountDisplay => $"AED {VatAmount:N2}";

        private decimal _totalGlassCost;
        public decimal TotalGlassCost { get => _totalGlassCost; set { if (_totalGlassCost == value) return; _totalGlassCost = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalGlassCostDisplay)); } }
        public string TotalGlassCostDisplay => $"AED {TotalGlassCost:N2}";

        private decimal _totalProductionCost;
        public decimal TotalProductionCost { get => _totalProductionCost; set { if (_totalProductionCost == value) return; _totalProductionCost = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalProductionCostDisplay)); } }
        public string TotalProductionCostDisplay => $"AED {TotalProductionCost:N2}";

        private decimal _totalCost;
        public decimal TotalCost { get => _totalCost; set { if (_totalCost == value) return; _totalCost = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalCostDisplay)); } }
        public string TotalCostDisplay => $"AED {TotalCost:N2}";

        private decimal _profit;
        public decimal Profit { get => _profit; set { if (_profit == value) return; _profit = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProfitDisplay)); OnPropertyChanged(nameof(IsLoss)); } }
        public string ProfitDisplay => $"AED {Math.Abs(Profit):N2}";
        public bool IsLoss => Profit < 0;

        private decimal _marginPercent;
        public decimal MarginPercent { get => _marginPercent; set { if (_marginPercent == value) return; _marginPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(MarginDisplay)); } }
        public string MarginDisplay => $"{MarginPercent:0.#}%";

        private decimal _overallMarginPercent;
        public decimal OverallMarginPercent { get => _overallMarginPercent; set { if (_overallMarginPercent == value) return; _overallMarginPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(OverallMarginDisplay)); } }
        public string OverallMarginDisplay => $"{OverallMarginPercent:0.#}%";

        private decimal _netMarginPercent;
        public decimal NetMarginPercent { get => _netMarginPercent; set { if (_netMarginPercent == value) return; _netMarginPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(NetMarginDisplay)); } }
        public string NetMarginDisplay => $"{NetMarginPercent:0.#}%";

        private string _profitLabel = "PROFIT";
        public string ProfitLabel { get => _profitLabel; set { if (_profitLabel == value) return; _profitLabel = value ?? ""; OnPropertyChanged(); } }

        // ── Line Items ──────────────────────────────────────────────────
        public ObservableCollection<JobDetailLineItem> LineItems { get; }

        private int _lineItemCount;
        public int LineItemCount { get => _lineItemCount; set { if (_lineItemCount == value) return; _lineItemCount = value; OnPropertyChanged(); } }

        // ── Target-Price Model (approved KPI) ──────────────────────────
        // Loaded from the additive snapshot block (SpecTargetSnapshot +
        // job-level totals). Absent on old snapshots → INCOMPLETE/STALE,
        // never a fake 0% margin.
        public ObservableCollection<JobTargetSpecLine> TargetSpecs { get; }

        private bool _hasTargetData;
        public bool HasTargetData { get => _hasTargetData; set { if (_hasTargetData == value) return; _hasTargetData = value; OnPropertyChanged(); OnPropertyChanged(nameof(TargetPanelVisibility)); } }
        public System.Windows.Visibility TargetPanelVisibility => HasTargetData ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        private string _targetStatusLabel = "INCOMPLETE";
        public string TargetStatusLabel { get => _targetStatusLabel; set { if (_targetStatusLabel == value) return; _targetStatusLabel = value ?? "INCOMPLETE"; OnPropertyChanged(); OnPropertyChanged(nameof(IsAboveTarget)); OnPropertyChanged(nameof(IsBelowTarget)); OnPropertyChanged(nameof(IsTargetIncomplete)); OnPropertyChanged(nameof(IsTargetStale)); } }
        public bool IsAboveTarget => TargetStatusLabel == "ABOVE TARGET";
        public bool IsBelowTarget => TargetStatusLabel == "BELOW TARGET";
        public bool IsTargetIncomplete => TargetStatusLabel == "INCOMPLETE";
        public bool IsTargetStale => TargetStatusLabel == "STALE";

        private decimal _givenPricePerSqm;
        public decimal GivenPricePerSqm { get => _givenPricePerSqm; set { if (_givenPricePerSqm == value) return; _givenPricePerSqm = value; OnPropertyChanged(); OnPropertyChanged(nameof(GivenPricePerSqmDisplay)); } }
        public string GivenPricePerSqmDisplay => HasTargetData ? $"AED {GivenPricePerSqm:N2}/m²" : "—";

        private decimal _requiredTargetPerSqm;
        public decimal RequiredTargetPerSqm { get => _requiredTargetPerSqm; set { if (_requiredTargetPerSqm == value) return; _requiredTargetPerSqm = value; OnPropertyChanged(); OnPropertyChanged(nameof(RequiredTargetPerSqmDisplay)); } }
        public string RequiredTargetPerSqmDisplay => HasTargetData ? $"AED {RequiredTargetPerSqm:N2}/m²" : "—";

        private decimal _specificationTargetPerSqm;
        public decimal SpecificationTargetPerSqm { get => _specificationTargetPerSqm; set { if (_specificationTargetPerSqm == value) return; _specificationTargetPerSqm = value; OnPropertyChanged(); OnPropertyChanged(nameof(SpecificationTargetPerSqmDisplay)); } }
        public string SpecificationTargetPerSqmDisplay => HasTargetData ? $"AED {SpecificationTargetPerSqm:N2}/m²" : "—";

        private decimal _appliedUInsertPerSqm;
        public decimal AppliedUInsertPerSqm { get => _appliedUInsertPerSqm; set { if (_appliedUInsertPerSqm == value) return; _appliedUInsertPerSqm = value; OnPropertyChanged(); OnPropertyChanged(nameof(AppliedUInsertPerSqmDisplay)); } }
        public string AppliedUInsertPerSqmDisplay => HasTargetData
            ? AppliedUInsertPerSqm > 0m ? $"AED {AppliedUInsertPerSqm:N2}/m²" : "—"
            : "—";

        private decimal _differencePerSqm;
        public decimal DifferencePerSqm { get => _differencePerSqm; set { if (_differencePerSqm == value) return; _differencePerSqm = value; OnPropertyChanged(); OnPropertyChanged(nameof(DifferencePerSqmDisplay)); } }
        public string DifferencePerSqmDisplay
        {
            get
            {
                if (!HasTargetData) return "—";
                return DifferencePerSqm < 0m
                    ? $"-AED {Math.Abs(DifferencePerSqm):N2}/m²"
                    : $"AED {DifferencePerSqm:N2}/m²";
            }
        }

        private decimal _givenValue;
        public decimal GivenValue { get => _givenValue; set { if (_givenValue == value) return; _givenValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(GivenValueDisplay)); } }
        public string GivenValueDisplay => HasTargetData ? $"AED {GivenValue:N2}" : "—";

        private decimal _requiredTargetValue;
        public decimal RequiredTargetValue { get => _requiredTargetValue; set { if (_requiredTargetValue == value) return; _requiredTargetValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(RequiredTargetValueDisplay)); } }
        public string RequiredTargetValueDisplay => HasTargetData ? $"AED {RequiredTargetValue:N2}" : "—";

        private decimal _differenceValue;
        public decimal DifferenceValue { get => _differenceValue; set { if (_differenceValue == value) return; _differenceValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(DifferenceValueDisplay)); } }
        public string DifferenceValueDisplay
        {
            get
            {
                if (!HasTargetData) return "—";
                return DifferenceValue < 0m
                    ? $"-AED {Math.Abs(DifferenceValue):N2}"
                    : $"AED {DifferenceValue:N2}";
            }
        }

        private decimal? _targetMarginPercent;
        public decimal? TargetMarginPercent
        {
            get => _targetMarginPercent;
            set { if (_targetMarginPercent == value) return; _targetMarginPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(TargetMarginDisplay)); }
        }
        public string TargetMarginDisplay => TargetMarginPercent is decimal m ? $"{m:0.##}%" : "—";

        private string _uInsertTreatmentSummary = "";
        public string UInsertTreatmentSummary { get => _uInsertTreatmentSummary; set { if (_uInsertTreatmentSummary == value) return; _uInsertTreatmentSummary = value ?? ""; OnPropertyChanged(); } }

        private string _targetStatusMessage = "";
        public string TargetStatusMessage { get => _targetStatusMessage; set { if (_targetStatusMessage == value) return; _targetStatusMessage = value ?? ""; OnPropertyChanged(); } }

        // ── Visibility ──────────────────────────────────────────────────
        private bool _isPanelVisible;
        public bool IsPanelVisible { get => _isPanelVisible; set { if (_isPanelVisible == value) return; _isPanelVisible = value; OnPropertyChanged(); } }

        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set { if (_statusMessage == value) return; _statusMessage = value ?? ""; OnPropertyChanged(); } }

        // ── Load ────────────────────────────────────────────────────────
        public async void LoadJobDetail(string proformaId)
        {
            if (string.IsNullOrWhiteSpace(proformaId))
            {
                IsPanelVisible = false;
                return;
            }

            // Skip a duplicate read while this exact invoice is already
            // loading — otherwise one double-click (two single-click toggles
            // plus the full-detail command) starts several identical DB reads.
            if (string.Equals(_loadingProformaId, proformaId, StringComparison.Ordinal))
                return;

            _loadingProformaId = proformaId;

            try
            {
                LineItems.Clear();

                // EFFICIENT: query single invoice by ID, not all approved
                var inv = await _invoices.GetByProformaIdAsync(proformaId).ConfigureAwait(true);
                if (inv == null)
                {
                    StatusMessage = $"Invoice {proformaId} not found.";
                    IsPanelVisible = false;
                    return;
                }

                var snap = ProformaInvoiceMapper.DeserializeSnapshot(inv);
                if (snap == null)
                {
                    StatusMessage = "Could not read invoice snapshot.";
                    IsPanelVisible = false;
                    return;
                }

                // Header
                ProformaId = inv.ProformaId;
                CompanyName = inv.CompanyName ?? "—";
                CustomerName = inv.CustomerName ?? "—";
                ProjectName = snap.Project?.ProjectIdentifierName ?? "—";
                Salesman = snap.Client?.SalesRepresentative ?? "—";
                IssueDateDisplay = inv.IssueDate.ToString("dd MMM yyyy");

                // Glass type summary
                var glassTypes = snap.SpecGroups?
                    .Select(sg => sg.SelectedGlassType)
                    .Where(gt => !string.IsNullOrWhiteSpace(gt))
                    .Distinct()
                    .ToList();
                GlassTypeSummary = glassTypes?.Any() == true
                    ? string.Join(", ", glassTypes)
                    : "—";

                // Financial summary from P&L snapshot
                var pl = snap.ProfitLoss;
                if (pl != null)
                {
                    GrossRevenue = pl.GrossVatInclusive;
                    NetRevenue = pl.NetExVat;
                    VatAmount = pl.GrossVatInclusive - pl.NetExVat;
                    TotalGlassCost = pl.TotalGlassCost;
                    TotalProductionCost = pl.TotalProductionCost;
                    TotalCost = pl.TotalGlassCost + pl.TotalProductionCost;
                    Profit = pl.Profit;
                    MarginPercent = pl.MarginPercent;
                    OverallMarginPercent = pl.OverallMarginPercent;
                    NetMarginPercent = pl.NetMarginPercent;
                    ProfitLabel = Profit >= 0 ? "PROFIT" : "LOSS";
                }

                // Line items from P&L rows
                if (pl?.Rows != null)
                {
                    foreach (var row in pl.Rows)
                    {
                        var item = new JobDetailLineItem
                        {
                            Kind = row.Kind ?? "",
                            KindIcon = GetKindIcon(row.Kind),
                            Description = row.Description ?? "",
                            SpecNumber = row.SpecNumber,
                            Quantity = row.Quantity,
                            QuantityDisplay = FormatQuantity(row),
                            UnitPrice = row.PricePerSqm,
                            UnitPriceDisplay = $"AED {row.PricePerSqm:N2}",
                            SheetSqm = row.SheetSqm,
                            SheetSqmDisplay = row.SheetSqm > 0 ? $"{row.SheetSqm:0.##} SQM" : "",
                            TotalArea = row.SheetSqm * row.Quantity,
                            TotalAreaDisplay = (row.SheetSqm * row.Quantity) > 0 ? $"{row.SheetSqm * row.Quantity:0.##} SQM" : "",
                            Amount = row.Amount,
                            AmountDisplay = $"AED {row.Amount:N2}",
                            IsGlass = row.Kind == "Glass",
                            IsSpacer = row.Kind == "Spacer",
                            IsProduction = row.Kind == "Production",
                            CalculationNote = BuildCalculationNote(row)
                        };
                        LineItems.Add(item);
                    }
                }

                LineItemCount = LineItems.Count;

                // ── Target-price model (approved KPI) from snapshot ──
                // The additive target block lives on the P&L snapshot
                // (ProfitLossSnapshot.SpecTargets + job-level target totals).
                // The aggregate and every per-spec row are REBUILT from the
                // per-spec rows on the BASE-PRICE specification target: a
                // U-insert rate that an older build folded into the stored
                // "final required target" is ignored, so old invoices display
                // the corrected margin here without any migration.
                TargetSpecs.Clear();
                if (pl != null && pl.SpecTargets is { Count: > 0 })
                {
                    HasTargetData = true;

                    var rebuilt = TargetSnapshotRecompute.Recompute(pl.SpecTargets);
                    if (rebuilt != null)
                    {
                        GivenPricePerSqm = rebuilt.AverageGivenPricePerSqm;
                        SpecificationTargetPerSqm = rebuilt.AverageRequiredPerSqm;
                        RequiredTargetPerSqm = rebuilt.AverageRequiredPerSqm;
                        AppliedUInsertPerSqm = 0m;
                        DifferencePerSqm = Math.Round(
                            rebuilt.AverageGivenPricePerSqm - rebuilt.AverageRequiredPerSqm, 2);
                        GivenValue = rebuilt.TotalGivenValue;
                        RequiredTargetValue = rebuilt.TotalRequiredValue;
                        DifferenceValue = rebuilt.TotalDifferenceValue;
                        TargetMarginPercent = rebuilt.ExtraProfitMarginPercent
                                              ?? rebuilt.MarginAgainstTargetPercent;
                        TargetStatusLabel = TargetStatusLabelFromName(rebuilt.Status);
                        TargetStatusMessage = BuildTargetStatusMessage(rebuilt, TargetStatusLabel);
                        UInsertTreatmentSummary = BuildUInsertSummaryText(pl);
                    }
                    else
                    {
                        GivenPricePerSqm = pl.TargetAverageGivenPricePerSqm;
                        SpecificationTargetPerSqm = pl.TargetAverageSpecificationTargetPerSqm;
                        RequiredTargetPerSqm = pl.TargetAverageSpecificationTargetPerSqm;
                        AppliedUInsertPerSqm = 0m;
                        DifferencePerSqm = pl.TargetAverageGivenPricePerSqm
                                           - pl.TargetAverageSpecificationTargetPerSqm;
                        GivenValue = pl.TargetTotalGivenValue;
                        RequiredTargetValue = pl.TargetTotalRequiredValue;
                        DifferenceValue = pl.TargetTotalDifferenceValue;
                        TargetMarginPercent = pl.TargetExtraProfitMarginPercent
                                              ?? pl.TargetMarginAgainstTargetPercent;
                        TargetStatusLabel = TargetStatusLabelFromName(pl.TargetStatus ?? "");
                        TargetStatusMessage = BuildTargetStatusMessage(pl, TargetStatusLabel);
                        UInsertTreatmentSummary = BuildUInsertSummaryText(pl);
                    }

                    foreach (var t in pl.SpecTargets.Where(t => t != null))
                    {
                        decimal specGiven = Math.Max(0m, t.GivenPricePerSqm);
                        decimal specTarget = Math.Max(0m, t.SpecificationTargetPerSqm);

                        // Given JUDGED for the margin: stored when Step 9
                        // saved this spec; self-corrected on read for older
                        // snapshots (net any U-insert inside the given).
                        decimal specJudged = t.GivenPriceJudgedPerSqm > 0m
                            ? t.GivenPriceJudgedPerSqm
                            : Math.Max(0m, specGiven
                                - (string.Equals(t.UInsertTreatment, "IncludedInStep5Price",
                                       StringComparison.OrdinalIgnoreCase)
                                    ? Math.Max(0m, t.UInsertRatePerSqm)
                                    : 0m));

                        bool complete = specGiven > 0m && specTarget > 0m && t.AreaSqm > 0m
                            && (HasCompleteSpecStatus(t) || t.MarginAgainstTargetPercent.HasValue);

                        // Status margin — judged given vs the required target.
                        decimal? specStatusMargin = complete && specJudged > 0m
                            ? Math.Round((specJudged - specTarget) / specTarget * 100m, 2)
                            : (decimal?)null;

                        // Display margin — the Step 9 Card 3 KPI: judged given
                        // vs the BASE price.
                        decimal specBase = TargetSnapshotRecompute.SnapshotBasePricePerSqm(t);
                        decimal? specBaseMargin = complete && specJudged > 0m && specBase > 0m
                            ? Math.Round((specJudged - specBase) / specBase * 100m, 2)
                            : (decimal?)null;

                        TargetSpecs.Add(new JobTargetSpecLine
                        {
                            SpecNumber = t.SpecNumber,
                            SpecName = string.IsNullOrWhiteSpace(t.SpecName) ? $"Spec {t.SpecNumber}" : t.SpecName,
                            GivenPricePerSqmDisplay = specJudged > 0m && specJudged != specGiven
                                ? $"AED {specJudged:N2}/m²"
                                : $"AED {specGiven:N2}/m²",
                            SpecificationTargetPerSqmDisplay = $"AED {specTarget:N2}/m²",
                            AppliedUInsertPerSqmDisplay = t.UInsertRatePerSqm > 0m
                                ? $"AED {t.UInsertRatePerSqm:N2}/m²"
                                : "—",
                            FinalRequiredTargetPerSqmDisplay = $"AED {specTarget:N2}/m²",
                            DifferencePerSqmDisplay = specJudged > 0m
                                ? (specJudged - specTarget) < 0m
                                    ? $"-AED {Math.Abs(specJudged - specTarget):N2}/m²"
                                    : $"AED {specJudged - specTarget:N2}/m²"
                                : "—",
                            MarginDisplay = specBaseMargin is decimal sm ? $"{sm:0.##}%" : "—",
                            // TargetMarginTolerance, not a local 0.05m: the
                            // proforma side classifies at 0.005m, so a local
                            // band made this row read AT TARGET for a
                            // specification the quotation showed as ABOVE.
                            StatusLabel = specStatusMargin is decimal sv
                                ? TargetMarginTolerance.IsAbove(sv) ? "ABOVE TARGET"
                                    : TargetMarginTolerance.IsBelow(sv) ? "BELOW TARGET"
                                    : "AT TARGET"
                                : "INCOMPLETE",
                            UInsertNote = BuildUInsertNote(t)
                        });
                    }
                }
                else
                {
                    HasTargetData = false;
                    TargetStatusLabel = pl != null ? "STALE" : "INCOMPLETE";
                    TargetStatusMessage = pl != null
                        ? "This invoice was saved before the target-price model existed. Actual P&L is shown; target KPIs are not available for this job."
                        : "No stored P&L or target data for this invoice.";
                }

                StatusMessage = $"{LineItemCount} line item(s) • {CompanyName} • {CustomerName}";
                IsPanelVisible = true;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[JobDetailVM] Load failed: {ex.Message}");
                StatusMessage = "Could not load job detail.";
                IsPanelVisible = false;
            }
            finally
            {
                if (string.Equals(_loadingProformaId, proformaId, StringComparison.Ordinal))
                    _loadingProformaId = null;
            }
        }

        public void ClearSelection()
        {
            IsPanelVisible = false;
            LineItems.Clear();
            TargetSpecs.Clear();
        }

        /// <summary>TargetStatus names that mean "fully calculable".</summary>
        private static readonly System.Collections.Generic.HashSet<string> CompleteSpecStatusNames =
            new(System.StringComparer.OrdinalIgnoreCase)
            {
                "UpToDate", "AboveTarget", "AtTarget", "BelowTarget"
            };

        private static bool HasCompleteSpecStatus(SpecTargetSnapshot t) =>
            CompleteSpecStatusNames.Contains(t?.Status ?? "");

        /// <summary>Maps a TargetStatus enum-name to its display label.</summary>
        private static string TargetStatusLabelFromName(string? statusName)
        {
            if (string.IsNullOrWhiteSpace(statusName)) return "INCOMPLETE";
            return statusName switch
            {
                "AboveTarget" => "ABOVE TARGET",
                "AtTarget" => "AT TARGET",
                "BelowTarget" => "BELOW TARGET",
                "SnapshotStale" => "STALE",
                "CalculationIncomplete" => "INCOMPLETE",
                _ => statusName.ToUpperInvariant().Replace("_", " ")
            };
        }

        private static string BuildTargetStatusMessage(RebuiltTargetTotals r, string label)
        {
            if (label is "ABOVE TARGET" or "AT TARGET" or "BELOW TARGET")
                return $"Step 5 given price is {label.Replace(" TARGET", "").ToLowerInvariant()} the base-price required target across {r.ValidSpecCount} specification(s). " +
                       "The U-insert rate is shown separately and is never added to the target.";

            if (label == "INCOMPLETE")
                return "One or more specifications are missing target inputs (glass rates, ASP rate, yield or Step 5 base price) — open the invoice in Step 9 to complete them.";

            if (label == "STALE")
                return "This invoice was saved before the target-price model existed; target KPIs are not available.";

            return $"{label} — open this invoice in Step 9 to repair the target calculation, then save.";
        }

        private static string BuildTargetStatusMessage(ProfitLossSnapshot pl, string label)
        {
            if (label is "ABOVE TARGET" or "AT TARGET" or "BELOW TARGET")
                return $"Step 5 given price is {label.Replace(" TARGET", "").ToLowerInvariant()} the base-price required target across {pl.TargetValidSpecCount} specification(s). " +
                       "The U-insert rate is shown separately and is never added to the target.";

            if (label == "INCOMPLETE")
                return "One or more specifications are missing target inputs (glass rates, ASP rate, yield or Step 5 base price) — open the invoice in Step 9 to complete them.";

            if (label == "STALE")
                return "This invoice was saved before the target-price model existed; target KPIs are not available.";

            return $"{label} — open this invoice in Step 9 to repair the target calculation, then save.";
        }

        private static string BuildUInsertSummaryText(ProfitLossSnapshot pl)
        {
            var rows = pl.SpecTargets?.Where(s => s != null).ToList();
            if (rows == null || rows.Count == 0)
                return pl.TargetUInsertTreatmentSummary ?? "";

            int included = rows.Count(s =>
                string.Equals(s.UInsertTreatment, "IncludedInStep5Price", StringComparison.OrdinalIgnoreCase)
                && s.UInsertRatePerSqm > 0m);
            int additional = rows.Count(s =>
                string.Equals(s.UInsertTreatment, "Additional", StringComparison.OrdinalIgnoreCase));

            if (included > 0 && additional > 0)
                return "Mixed — U-insert included in some given prices, billed separately for others (rate never added to target)";
            if (included > 0)
                return "U-insert included in given price — rate shown, not added to target";
            if (additional > 0)
                return "U-insert billed separately as a Step 6 extra — not in target";
            return "No U-insert extra";
        }

        private static string BuildUInsertNote(SpecTargetSnapshot t)
        {
            // Revised rule: the U-insert rate is NEVER added to the required
            // target. IncludedInStep5Price = the given price already covers
            // it (the rate is shown for reference); Additional = the Step 6
            // row bills it separately on top of the given price.
            bool included = string.Equals(
                t.UInsertTreatment, "IncludedInStep5Price", StringComparison.OrdinalIgnoreCase);

            if (t.UInsertRatePerSqm > 0m)
            {
                return included
                    ? $"Rate AED {t.UInsertRatePerSqm:N2}/m² is inside the given price — shown for reference, never added to the target"
                    : $"Billed separately in Step 6 at AED {t.UInsertRatePerSqm:N2}/m² (not in target)";
            }

            return "No U-insert extra recorded";
        }

        /// <summary>Command to close the detail panel.</summary>
        public System.Windows.Input.ICommand CloseCommand { get; }

        // Constructor wiring for CloseCommand is in the main ctor above

        private static string GetKindIcon(string kind) => kind switch
        {
            "Glass" => "\uE8FD",      // grid/table
            "Spacer" => "\uE7F1",     // connector
            "Production" => "\uE71D", // tools
            "UInsert" => "\uE8A8",    // insert
            _ => "\uE7C3"             // list
        };

        private static string FormatQuantity(ProfitLossRowSnapshot row)
        {
            if (row.Kind == "Glass")
                return $"{row.Quantity:0} sheets";
            if (row.Kind == "Spacer")
                return $"{row.Quantity:0.##} LM";
            return $"{row.Quantity:0.##}";
        }

        private static string BuildCalculationNote(ProfitLossRowSnapshot row)
        {
            if (row.Kind == "Glass" && row.SheetSqm > 0 && row.Quantity > 0)
                return $"{row.Quantity:0} sheets × {row.SheetSqm:0.##} SQM × AED {row.PricePerSqm:N2}";
            if (row.Kind == "Spacer" && row.Quantity > 0)
                return $"{row.Quantity:0.##} LM × AED {row.PricePerSqm:N2}/LM";
            if (row.Kind == "Production")
                return $"{row.Quantity:0.##} × AED {row.PricePerSqm:N2}";
            return "";
        }
    }

    /// <summary>One P&L line item in the detail sidebar.</summary>
    public class JobDetailLineItem : ViewModelBase
    {
        public string Kind { get; set; } = "";
        public string KindIcon { get; set; } = "";
        public string Description { get; set; } = "";
        public int SpecNumber { get; set; }
        public decimal Quantity { get; set; }
        public string QuantityDisplay { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public string UnitPriceDisplay { get; set; } = "";
        public decimal SheetSqm { get; set; }
        public string SheetSqmDisplay { get; set; } = "";
        public decimal TotalArea { get; set; }
        public string TotalAreaDisplay { get; set; } = "";
        public decimal Amount { get; set; }
        public string AmountDisplay { get; set; } = "";
        public bool IsGlass { get; set; }
        public bool IsSpacer { get; set; }
        public bool IsProduction { get; set; }
        public string CalculationNote { get; set; } = "";
    }

    /// <summary>
    /// One specification-group row of the target-price result in the
    /// detail sidebar (approved KPI model).
    /// </summary>
    public class JobTargetSpecLine : ViewModelBase
    {
        public int SpecNumber { get; set; }
        public string SpecName { get; set; } = "";
        public string GivenPricePerSqmDisplay { get; set; } = "";
        public string SpecificationTargetPerSqmDisplay { get; set; } = "";
        public string AppliedUInsertPerSqmDisplay { get; set; } = "";
        public string FinalRequiredTargetPerSqmDisplay { get; set; } = "";
        public string DifferencePerSqmDisplay { get; set; } = "";
        public string MarginDisplay { get; set; } = "";
        public string StatusLabel { get; set; } = "";
        public string UInsertNote { get; set; } = "";

        public bool IsAboveTarget => StatusLabel == "ABOVE TARGET";
        public bool IsBelowTarget => StatusLabel == "BELOW TARGET";
        public bool IsIncomplete => StatusLabel == "INCOMPLETE";
    }
}
