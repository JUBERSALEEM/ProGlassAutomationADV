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
                TargetSpecs.Clear();
                if (pl != null && pl.SpecTargets is { Count: > 0 })
                {
                    HasTargetData = true;

                    GivenPricePerSqm = pl.TargetAverageGivenPricePerSqm;
                    SpecificationTargetPerSqm = pl.TargetAverageSpecificationTargetPerSqm;
                    AppliedUInsertPerSqm = pl.TargetAverageAppliedUInsertPerSqm;
                    RequiredTargetPerSqm = pl.TargetAverageFinalRequiredTargetPerSqm;
                    DifferencePerSqm = pl.TargetAverageGivenPricePerSqm - pl.TargetAverageFinalRequiredTargetPerSqm;
                    GivenValue = pl.TargetTotalGivenValue;
                    RequiredTargetValue = pl.TargetTotalRequiredValue;
                    DifferenceValue = pl.TargetTotalDifferenceValue;
                    TargetMarginPercent = pl.TargetMarginAgainstTargetPercent;
                    UInsertTreatmentSummary = string.IsNullOrWhiteSpace(pl.TargetUInsertTreatmentSummary)
                        ? "Not recorded"
                        : pl.TargetUInsertTreatmentSummary;

                    var parsedStatus = TargetStatusLabelFor(pl, true);
                    TargetStatusLabel = parsedStatus;
                    TargetStatusMessage = BuildTargetStatusMessage(pl, parsedStatus);

                    foreach (var t in pl.SpecTargets.Where(t => t != null))
                    {
                        TargetSpecs.Add(new JobTargetSpecLine
                        {
                            SpecNumber = t.SpecNumber,
                            SpecName = string.IsNullOrWhiteSpace(t.SpecName) ? $"Spec {t.SpecNumber}" : t.SpecName,
                            GivenPricePerSqmDisplay = $"AED {t.GivenPricePerSqm:N2}/m²",
                            SpecificationTargetPerSqmDisplay = $"AED {t.SpecificationTargetPerSqm:N2}/m²",
                            AppliedUInsertPerSqmDisplay = t.UInsertRatePerSqm > 0m
                                ? $"AED {t.AppliedUInsertPerSqm:N2}/m² added"
                                : "no additional U-insert",
                            FinalRequiredTargetPerSqmDisplay = $"AED {t.FinalRequiredTargetPerSqm:N2}/m²",
                            DifferencePerSqmDisplay = t.DifferencePerSqm < 0m
                                ? $"-AED {Math.Abs(t.DifferencePerSqm):N2}/m²"
                                : $"AED {t.DifferencePerSqm:N2}/m²",
                            MarginDisplay = t.MarginAgainstTargetPercent is decimal tm ? $"{tm:0.##}%" : "—",
                            StatusLabel = SpecStatusLabelFor(t),
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
        }

        public void ClearSelection()
        {
            IsPanelVisible = false;
            LineItems.Clear();
            TargetSpecs.Clear();
        }

        /// <summary>Maps the persisted job-level status to the display label.</summary>
        private static string TargetStatusLabelFor(ProfitLossSnapshot pl, bool isCosted)
        {
            if (pl.TargetMarginAgainstTargetPercent.HasValue
                && !string.IsNullOrWhiteSpace(pl.TargetStatus)
                && Enum.TryParse<TargetStatus>(pl.TargetStatus, out var parsed))
            {
                return parsed switch
                {
                    TargetStatus.AboveTarget => "ABOVE TARGET",
                    TargetStatus.AtTarget => "AT TARGET",
                    TargetStatus.BelowTarget => "BELOW TARGET",
                    _ => parsed.ToString().ToUpperInvariant().Replace("_", " ")
                };
            }
            return isCosted ? "STALE" : "INCOMPLETE";
        }

        private static string BuildTargetStatusMessage(ProfitLossSnapshot pl, string label)
        {
            if (label == "ABOVE TARGET" || label == "AT TARGET" || label == "BELOW TARGET")
                return $"Step 5 given price is {label.Replace(" TARGET", "").ToLowerInvariant()} the final required target across {pl.TargetValidSpecCount} specification(s).";

            if (label == "INCOMPLETE")
                return "One or more specifications are missing target inputs (glass rates, ASP rate, yield or Step 5 base price).";

            if (label == "STALE")
                return "This invoice was saved before the target-price model existed; target KPIs are not available.";

            return "No target-price data recorded for this invoice.";
        }

        private static string BuildUInsertNote(SpecTargetSnapshot t)
        {
            if (t.UInsertRatePerSqm <= 0m)
                return "Not applicable / included in Step 5 price";
            return $"Additional AED {t.UInsertRatePerSqm:N2}/m²";
        }

        private static string SpecStatusLabelFor(SpecTargetSnapshot t)
        {
            if (t.MarginAgainstTargetPercent.HasValue
                && !string.IsNullOrWhiteSpace(t.Status)
                && Enum.TryParse<TargetStatus>(t.Status, out var parsed))
            {
                return parsed switch
                {
                    TargetStatus.AboveTarget => "ABOVE TARGET",
                    TargetStatus.AtTarget => "AT TARGET",
                    TargetStatus.BelowTarget => "BELOW TARGET",
                    _ => parsed.ToString().ToUpperInvariant().Replace("_", " ")
                };
            }
            return "INCOMPLETE";
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
