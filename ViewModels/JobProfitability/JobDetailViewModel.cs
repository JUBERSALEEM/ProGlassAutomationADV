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
}
