// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Dashboard/OverviewDashboardViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Dashboard Overview (Chat 23 v2.0.1)
//
// CHANGES vs PREVIOUS VERSION:
//   ONLY chart bar color hex values changed to align with industrial
//   blue palette:
//     • Regular bar: "#6366F1" (indigo) → "#3B82F6" (blue-500)
//     • Highlight bar (tallest month): "#10B981" (green) → "#1D4ED8" (royal)
//
//   ALL original documentation comments (Phase 10.9a description,
//   FIX-1 through FIX-5 inline annotations) RESTORED after v2.0
//   accidentally stripped them.
//
// PRESERVED (zero regression):
//   • ALL LoadCoreAsync logic
//   • ALL KpiTileModel emissions (Icon/Label/DisplayValue/Accent/IsAlert)
//   • ALL ActivityFeedItem emissions
//   • ALL commands
//   • ALL color helper methods
//   • FIX-1 (currency N2) preserved
//   • FIX-2 (Gotcha #40 pending pieces) preserved
//   • FIX-3 (dispatched includes all sent-out) preserved
//   • FIX-4 (delivered today pieces tile) preserved
//   • FIX-5 (total delivered lifetime tile) preserved
//   • Original inline documentation comments RESTORED
//
// REGRESSION RISK: ZERO
//   Only 2 hex color literals changed. All comments and logic intact.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Dashboard;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.SheetStore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Dashboard
{
    /// <summary>
    /// Overview dashboard page — Phase 10.9a redesign.
    ///
    /// Layout: 2-column grid of parent cards, each with nested tiles.
    ///   💰 Sales        | 🏭 Production
    ///   🚚 Delivery     | 💬 Recent Activity (chat style, scrolls)
    ///   📦 Inventory    | 📈 Sales Chart
    ///
    /// FIXES APPLIED:
    ///   FIX-1 (Currency): Money values use N2 (2 decimals) not N0 (rounded).
    ///   FIX-2 (Pending Balance): Shows actual PIECES not JOB count (Gotcha #40).
    ///   FIX-3 (Dispatched): Includes Delivered/Signed/Received (all sent-out).
    ///   FIX-4 (4 tiles): Added Delivered Today tile.
    ///   FIX-5 (5 tiles): Added Total Delivered (pcs) — comprehensive delivery view.
    ///
    /// Chat 23 v2.0.1: Chart bar colors updated to industrial blue palette
    ///                 (regular = #3B82F6, highlight = #1D4ED8). Zero
    ///                 other logic changes.
    /// </summary>
    public class OverviewDashboardViewModel : DashboardPageBase
    {
        public override string TabTitle => "Overview";
        public override string TabIcon => "\uE80F";

        private const string JobStatusInProduction = "InProduction";
        private const string JobStatusCompleted = "Completed";
        private const string JobStatusDelivered = "Delivered";

        // ── navigation callbacks ──────────────────────────────────────────
        public Action<string>? NavigateTo { get; set; }
        public Action? TriggerNewInvoice { get; set; }

        // ── quick action commands ─────────────────────────────────────────
        public ICommand NewInvoiceCommand { get; }
        public ICommand SavedInvoicesCommand { get; }
        public ICommand ViewStockCommand { get; }
        public ICommand ViewJobsCommand { get; }
        public ICommand ViewDeliveriesCommand { get; }
        public ICommand ExportReportCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        //  PARENT CARDS
        // ═════════════════════════════════════════════════════════════════

        private ObservableCollection<KpiTileModel> _salesTiles = new();
        public ObservableCollection<KpiTileModel> SalesTiles
        {
            get => _salesTiles;
            private set { if (_salesTiles == value) return; _salesTiles = value; OnPropertyChanged(); }
        }

        private ObservableCollection<KpiTileModel> _productionTiles = new();
        public ObservableCollection<KpiTileModel> ProductionTiles
        {
            get => _productionTiles;
            private set { if (_productionTiles == value) return; _productionTiles = value; OnPropertyChanged(); }
        }

        // 🚚 DELIVERY parent card — 5 tiles (FIX-5: was 4)
        private ObservableCollection<KpiTileModel> _deliveryTiles = new();
        public ObservableCollection<KpiTileModel> DeliveryTiles
        {
            get => _deliveryTiles;
            private set { if (_deliveryTiles == value) return; _deliveryTiles = value; OnPropertyChanged(); }
        }

        private ObservableCollection<KpiTileModel> _inventoryTiles = new();
        public ObservableCollection<KpiTileModel> InventoryTiles
        {
            get => _inventoryTiles;
            private set { if (_inventoryTiles == value) return; _inventoryTiles = value; OnPropertyChanged(); }
        }

        // ═════════════════════════════════════════════════════════════════
        //  RECENT ACTIVITY
        // ═════════════════════════════════════════════════════════════════

        private ObservableCollection<ActivityFeedItem> _recentActivity = new();
        public ObservableCollection<ActivityFeedItem> RecentActivity
        {
            get => _recentActivity;
            private set { if (_recentActivity == value) return; _recentActivity = value; OnPropertyChanged(); }
        }

        private bool _hasRecentActivity;
        public bool HasRecentActivity
        {
            get => _hasRecentActivity;
            private set { if (_hasRecentActivity == value) return; _hasRecentActivity = value; OnPropertyChanged(); }
        }

        // ═════════════════════════════════════════════════════════════════
        //  MONTHLY SALES CHART
        // ═════════════════════════════════════════════════════════════════

        private ObservableCollection<ChartBarModel> _monthlySalesBars = new();
        public ObservableCollection<ChartBarModel> MonthlySalesBars
        {
            get => _monthlySalesBars;
            private set { if (_monthlySalesBars == value) return; _monthlySalesBars = value; OnPropertyChanged(); }
        }

        private string _chartTitle = "Last 6 months";
        public string ChartTitle
        {
            get => _chartTitle;
            private set { if (_chartTitle == value) return; _chartTitle = value; OnPropertyChanged(); }
        }

        private bool _hasChartData;
        public bool HasChartData
        {
            get => _hasChartData;
            private set { if (_hasChartData == value) return; _hasChartData = value; OnPropertyChanged(); }
        }

        private readonly ISheetStoreRepository _sheetRepo = new SheetStoreRepository();

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public OverviewDashboardViewModel()
        {
            NewInvoiceCommand = new RelayCommand(_ =>
            {
                NavigateTo?.Invoke("ProformaInvoice");
                TriggerNewInvoice?.Invoke();
            });

            SavedInvoicesCommand = new RelayCommand(_ => NavigateTo?.Invoke("ProformaInvoice"));
            ViewStockCommand = new RelayCommand(_ => NavigateTo?.Invoke("SheetStore"));
            ViewJobsCommand = new RelayCommand(_ => NavigateTo?.Invoke("JobOrder"));
            ViewDeliveriesCommand = new RelayCommand(_ => NavigateTo?.Invoke("DeliveryNote"));

            ExportReportCommand = new RelayCommand(_ => { }, _ => false);
        }

        // ═════════════════════════════════════════════════════════════════
        //  LOAD
        // ═════════════════════════════════════════════════════════════════

        protected override async Task LoadCoreAsync()
        {
            var today = DateTime.Today;
            var firstOfMonth = new DateTime(today.Year, today.Month, 1);

            // ── Fetch all data ─────────────────────────────────────────
            var invoices = await App.Invoices.GetAllAsync();
            var sheets = await _sheetRepo.GetAllAsync();
            var jobs = await App.Jobs.GetAllAsync();
            var allDns = await App.DeliveryNotes.GetAllAsync();
            var pendingJobs = await App.DeliveryNotes.GetPendingQtyJobsAsync();

            // ── Compute stats ──────────────────────────────────────────
            decimal totalSales = invoices.Sum(i => i.GrandTotalVatInclusive);
            decimal salesMtd = invoices
                .Where(i => (i.UpdatedAt != default ? i.UpdatedAt : i.CreatedAt) >= firstOfMonth)
                .Sum(i => i.GrandTotalVatInclusive);

            int totalInvoices = invoices.Count;
            int approvedCount = invoices.Count(i => Eq(i.Status, "Approved"));

            int jobInProduction = jobs.Count(j => j.Status == JobStatusInProduction);
            int jobOverdue = jobs.Count(j =>
                                        j.DeliveryDate.HasValue
                                     && j.DeliveryDate.Value.Date < today
                                     && j.Status != JobStatusCompleted
                                     && j.Status != JobStatusDelivered);
            int jobCompletedMtd = jobs.Count(j =>
                                       (j.Status == JobStatusCompleted || j.Status == JobStatusDelivered)
                                     && j.UpdatedAt >= firstOfMonth);
            int totalJobs = jobs.Count;

            // ─────────────────────────────────────────────────────────────
            //  DELIVERY calculations — ALL FIXES + NEW METRICS
            // ─────────────────────────────────────────────────────────────

            // Preparing = Draft DNs (not yet dispatched)
            int dnDraft = allDns.Count(d => Eq(d.Status, "Draft"));

            // FIX-3: Dispatched includes ALL sent-out states
            int dnDispatched = allDns.Count(d =>
                Eq(d.Status, "Dispatched") ||
                Eq(d.Status, "Delivered") ||
                Eq(d.Status, "Signed") ||
                Eq(d.Status, "Received"));

            // FIX-4: Delivered Today = pieces delivered today (SUM, not COUNT)
            //        Better metric than DN count — shows actual pieces moving today
            int dnDeliveredTodayPcs = allDns
                .Where(d => d.DeliveryDate.Date == today &&
                            (Eq(d.Status, "Dispatched") ||
                             Eq(d.Status, "Delivered") ||
                             Eq(d.Status, "Signed") ||
                             Eq(d.Status, "Received")))
                .Sum(d => d.TotalDeliveredQty);

            // FIX-5 (NEW): Total Delivered = all pieces ever delivered
            //              Cumulative metric showing lifetime volume
            int dnTotalDeliveredPcs = allDns
                .Where(d => !Eq(d.Status, "Draft"))
                .Sum(d => d.TotalDeliveredQty);

            // FIX-2 (CRITICAL — Gotcha #40): Pending pieces from correct aggregation
            int dnPendingPcs = pendingJobs.Sum(p => p.TotalBalance);

            // ─────────────────────────────────────────────────────────────

            decimal inventoryValue = sheets.Sum(s => s.BalanceValue);
            int lowStock = sheets.Count(s => s.Balance <= 5);
            int totalSheets = sheets.Count;
            int totalStockQty = sheets.Sum(s => s.Balance);

            // ═════════════════════════════════════════════════════════════
            //  💰 SALES CARD — 4 tiles
            // ═════════════════════════════════════════════════════════════
            SalesTiles = new ObservableCollection<KpiTileModel>
            {
                new KpiTileModel
                {
                    Icon         = "\uE8EF",
                    Label        = "Total Sales",
                    DisplayValue = $"AED {totalSales:N2}",
                    Accent       = "Green"
                },
                new KpiTileModel
                {
                    Icon         = "\uE8A5",
                    Label        = "Invoices",
                    DisplayValue = totalInvoices.ToString("N0"),
                    Accent       = "Green"
                },
                new KpiTileModel
                {
                    Icon         = "\uE787",
                    Label        = "This Month",
                    DisplayValue = $"AED {salesMtd:N2}",
                    Accent       = "Green"
                },
                new KpiTileModel
                {
                    Icon         = "\uE73E",
                    Label        = "Approved",
                    DisplayValue = approvedCount.ToString("N0"),
                    Accent       = "Green"
                }
            };

            // ═════════════════════════════════════════════════════════════
            //  🏭 PRODUCTION CARD — 4 tiles
            // ═════════════════════════════════════════════════════════════
            ProductionTiles = new ObservableCollection<KpiTileModel>
            {
                new KpiTileModel
                {
                    Icon         = "\uE713",
                    Label        = "In Production",
                    DisplayValue = jobInProduction.ToString("N0"),
                    Accent       = "Blue"
                },
                new KpiTileModel
                {
                    Icon         = jobOverdue > 0 ? "\uE7BA" : "\uE73E",
                    Label        = "Overdue",
                    DisplayValue = jobOverdue.ToString("N0"),
                    Accent       = jobOverdue > 0 ? "Red" : "Blue",
                    IsAlert      = jobOverdue > 0
                },
                new KpiTileModel
                {
                    Icon         = "\uE730",
                    Label        = "Completed MTD",
                    DisplayValue = jobCompletedMtd.ToString("N0"),
                    Accent       = "Blue"
                },
                new KpiTileModel
                {
                    Icon         = "\uE8FD",
                    Label        = "Total Jobs",
                    DisplayValue = totalJobs.ToString("N0"),
                    Accent       = "Blue"
                }
            };

            // ═════════════════════════════════════════════════════════════
            //  🚚 DELIVERY CARD — 5 tiles (comprehensive)
            //  Preparing | Dispatched | Delivered Today | Total Delivered | Pending Balance
            // ═════════════════════════════════════════════════════════════
            DeliveryTiles = new ObservableCollection<KpiTileModel>
            {
                new KpiTileModel
                {
                    Icon         = "\uE70F",
                    Label        = "Preparing",
                    DisplayValue = dnDraft.ToString("N0"),
                    Accent       = "Amber"
                },
                new KpiTileModel
                {
                    Icon         = "\uE806",
                    Label        = "Dispatched",
                    DisplayValue = dnDispatched.ToString("N0"),
                    Accent       = "Amber"
                },
                new KpiTileModel
                {
                    Icon         = "\uE787",
                    Label        = "Delivered Today (pcs)",
                    DisplayValue = dnDeliveredTodayPcs.ToString("N0"),
                    Accent       = "Green"
                },
                new KpiTileModel
                {
                    Icon         = "\uE73E",
                    Label        = "Total Delivered (pcs)",   // FIX-5: NEW comprehensive tile
                    DisplayValue = dnTotalDeliveredPcs.ToString("N0"),
                    Accent       = "Green"
                },
                new KpiTileModel
                {
                    Icon         = dnPendingPcs > 0 ? "\uE9D5" : "\uE73E",
                    Label        = "Pending Balance (pcs)",
                    DisplayValue = dnPendingPcs.ToString("N0"),
                    Accent       = dnPendingPcs > 0 ? "Red" : "Amber",
                    IsAlert      = dnPendingPcs > 0
                }
            };

            // ═════════════════════════════════════════════════════════════
            //  📦 INVENTORY CARD — 4 tiles
            // ═════════════════════════════════════════════════════════════
            InventoryTiles = new ObservableCollection<KpiTileModel>
            {
                new KpiTileModel
                {
                    Icon         = "\uE8EF",
                    Label        = "Stock Value",
                    DisplayValue = $"AED {inventoryValue:N2}",
                    Accent       = "Red"
                },
                new KpiTileModel
                {
                    Icon         = lowStock > 0 ? "\uE7BA" : "\uE73E",
                    Label        = "Low Stock",
                    DisplayValue = lowStock.ToString("N0"),
                    Accent       = lowStock > 0 ? "Red" : "Red",
                    IsAlert      = lowStock > 0
                },
                new KpiTileModel
                {
                    Icon         = "\uE719",
                    Label        = "Sheet Types",
                    DisplayValue = totalSheets.ToString("N0"),
                    Accent       = "Red"
                },
                new KpiTileModel
                {
                    Icon         = "\uE7B8",
                    Label        = "Total Sheets",
                    DisplayValue = totalStockQty.ToString("N0"),
                    Accent       = "Red"
                }
            };

            // ═════════════════════════════════════════════════════════════
            //  📈 CHART — Monthly sales, last 6 months
            //  Chat 23 v2.0.1: bar colors updated to industrial blue palette.
            //    Regular:   "#6366F1" (indigo) → "#3B82F6" (blue-500)
            //    Highlight: "#10B981" (green)  → "#1D4ED8" (royal blue)
            // ═════════════════════════════════════════════════════════════
            var now = DateTime.Now;
            var months = Enumerable.Range(0, 6)
                .Select(i => now.AddMonths(-5 + i))
                .ToList();

            var rawBars = months.Select(m =>
            {
                decimal total = invoices
                    .Where(inv =>
                    {
                        var d = inv.UpdatedAt != default ? inv.UpdatedAt : inv.CreatedAt;
                        return d.Year == m.Year && d.Month == m.Month;
                    })
                    .Sum(inv => inv.GrandTotalVatInclusive);

                return new ChartBarModel
                {
                    Label = m.ToString("MMM"),
                    RawValue = total,
                    ValueDisplay = total > 0 ? $"AED {total:N2}" : "—",
                    BarColor = "#3B82F6"    // v2.0.1: blue-500 (was #6366F1 indigo)
                };
            }).ToList();

            decimal maxVal = rawBars.Any() ? rawBars.Max(b => b.RawValue) : 0m;
            bool anyData = maxVal > 0;

            foreach (var bar in rawBars)
            {
                bar.NormalizedHeight = anyData
                    ? (double)(bar.RawValue / maxVal)
                    : 0.04;
                bar.IsHighlight = anyData && bar.RawValue == maxVal;
                // v2.0.1: highlight = royal blue (was #10B981 green)
                bar.BarColor = bar.IsHighlight ? "#1D4ED8" : "#3B82F6";
            }

            MonthlySalesBars = new ObservableCollection<ChartBarModel>(rawBars);
            HasChartData = anyData;
            ChartTitle = $"{months.First():MMM yyyy} → {months.Last():MMM yyyy}";

            // ═════════════════════════════════════════════════════════════
            //  💬 RECENT ACTIVITY — merge all module events
            // ═════════════════════════════════════════════════════════════
            var feedItems = new List<ActivityFeedItem>();

            // Invoice activity
            var invoiceActivity = invoices.Select(i =>
            {
                var date = i.UpdatedAt != default ? i.UpdatedAt : i.CreatedAt;
                var isUpdated = i.UpdatedAt != default && i.UpdatedAt != i.CreatedAt;
                return new ActivityFeedItem
                {
                    Icon = "\uE8A5",
                    Title = $"{(string.IsNullOrWhiteSpace(i.ProformaId) ? "Invoice" : i.ProformaId)}" +
                                   $"{(string.IsNullOrWhiteSpace(i.CustomerName) ? "" : $"  ·  {i.CustomerName}")}",
                    SubTitle = $"Proforma  ·  {(isUpdated ? "Updated" : "Created")} {date:dd MMM}",
                    ValueDisplay = $"AED {i.GrandTotalVatInclusive:N2}",
                    StatusLabel = i.Status ?? "Draft",
                    StatusColor = ResolveStatusColor(i.Status),
                    TintColor = ResolveStatusTint(i.Status),
                    ModuleTag = "ProformaInvoice",
                    SortDate = date
                };
            });
            feedItems.AddRange(invoiceActivity);

            // Sheet store history
            try
            {
                using var db = new AppDbContext();
                var historyRows = await db.SheetHistories
                    .OrderByDescending(h => h.Timestamp)
                    .Take(20)
                    .ToListAsync();

                var sheetActivity = historyRows.Select(h => new ActivityFeedItem
                {
                    Icon = "\uE7B8",
                    Title = string.IsNullOrWhiteSpace(h.SheetDescription)
                                       ? "Sheet Store" : h.SheetDescription,
                    SubTitle = $"Sheet Store  ·  {h.Action}  ·  {h.Timestamp:dd MMM HH:mm}",
                    ValueDisplay = h.QuantityChanged > 0
                                       ? $"{h.QuantityChanged} sheet{(h.QuantityChanged == 1 ? "" : "s")}"
                                       : h.ValueChanged.HasValue
                                           ? $"AED {h.ValueChanged:N2}" : "—",
                    StatusLabel = h.Action,
                    StatusColor = ResolveActionColor(h.Action),
                    TintColor = ResolveActionTint(h.Action),
                    ModuleTag = "SheetStore",
                    SortDate = h.Timestamp
                });

                feedItems.AddRange(sheetActivity);
            }
            catch { /* sheet history unavailable */ }

            // Job Order activity
            var jobActivity = jobs.Select(j =>
            {
                bool isOverdue = j.DeliveryDate.HasValue
                              && j.DeliveryDate.Value.Date < today
                              && j.Status != JobStatusCompleted
                              && j.Status != JobStatusDelivered;

                string statusLabel = isOverdue ? "OVERDUE" : (j.Status ?? "Draft").ToUpperInvariant();
                string priorityIcon = j.Priority switch
                {
                    "High" => "🔴",
                    "Medium" => "🟡",
                    "Low" => "🟢",
                    _ => "🏭"
                };

                return new ActivityFeedItem
                {
                    Icon = priorityIcon,
                    Title = $"{j.JobOrderId}" +
                                   $"{(string.IsNullOrWhiteSpace(j.CustomerName) ? "" : $"  ·  {j.CustomerName}")}",
                    SubTitle = $"Job Order  ·  " +
                                   (j.DeliveryDate.HasValue
                                       ? $"Due {j.DeliveryDate.Value:dd MMM}"
                                       : "No due date"),
                    ValueDisplay = j.GrandTotalVatInclusive > 0
                                       ? $"AED {j.GrandTotalVatInclusive:N2}" : "—",
                    StatusLabel = statusLabel,
                    StatusColor = ResolveJobStatusColor(j.Status, isOverdue),
                    TintColor = ResolveJobPriorityTint(j.Priority),
                    ModuleTag = "JobOrder",
                    SortDate = j.UpdatedAt
                };
            });
            feedItems.AddRange(jobActivity);

            // Delivery Note activity
            var dnActivity = allDns
                .Where(d => !Eq(d.Status, "Draft"))
                .Select(d =>
                {
                    string icon = d.Status switch
                    {
                        "Dispatched" => "🚚",
                        "Delivered" => "✅",
                        "Signed" => "✍️",
                        "Received" => "📬",
                        _ => "📦"
                    };

                    string valueLine = d.TotalDeliveredQty > 0
                        ? $"{d.TotalDeliveredQty} pcs"
                        : "—";

                    return new ActivityFeedItem
                    {
                        Icon = icon,
                        Title = $"{d.DeliveryNoteId}" +
                                       $"{(string.IsNullOrWhiteSpace(d.CustomerName) ? "" : $"  ·  {d.CustomerName}")}",
                        SubTitle = $"Delivery  ·  {d.Status}  ·  {d.DeliveryDate:dd MMM}",
                        ValueDisplay = valueLine,
                        StatusLabel = d.Status.ToUpperInvariant(),
                        StatusColor = ResolveDnStatusColor(d.Status),
                        TintColor = ResolveDnStatusTint(d.Status),
                        ModuleTag = "DeliveryNote",
                        SortDate = d.DeliveryDate
                    };
                });
            feedItems.AddRange(dnActivity);

            var merged = feedItems
                .OrderByDescending(f => f.SortDate)
                .Take(20)
                .ToList();

            RecentActivity = new ObservableCollection<ActivityFeedItem>(merged);
            HasRecentActivity = merged.Count > 0;
        }

        // ═════════════════════════════════════════════════════════════════
        //  THEME SWAP
        // ═════════════════════════════════════════════════════════════════

        protected override void OnThemeChanged()
        {
            SalesTiles = new ObservableCollection<KpiTileModel>(_salesTiles);
            ProductionTiles = new ObservableCollection<KpiTileModel>(_productionTiles);
            DeliveryTiles = new ObservableCollection<KpiTileModel>(_deliveryTiles);
            InventoryTiles = new ObservableCollection<KpiTileModel>(_inventoryTiles);
            RecentActivity = new ObservableCollection<ActivityFeedItem>(_recentActivity);
        }

        // ═════════════════════════════════════════════════════════════════
        //  COLOR HELPERS
        // ═════════════════════════════════════════════════════════════════

        private static string ResolveStatusColor(string? status) =>
            status?.ToLowerInvariant() switch
            {
                "draft" => "NeutralBrush",
                "sent" => "InfoBrush",
                "approved" => "SuccessBrush",
                "rejected" => "DangerBrush",
                _ => "NeutralBrush"
            };

        private static string ResolveStatusTint(string? status) =>
            status?.ToLowerInvariant() switch
            {
                "draft" => "TintGrayBrush",
                "sent" => "TintBlueBrush",
                "approved" => "TintGreenBrush",
                "rejected" => "TintRedBrush",
                _ => "TintGrayBrush"
            };

        private static string ResolveActionColor(string? action) =>
            action?.ToLowerInvariant() switch
            {
                "added" => "SuccessBrush",
                "edited" => "WarningBrush",
                "deleted" => "DangerBrush",
                "bought" => "InfoBrush",
                "used" => "AccentBrush",
                _ => "NeutralBrush"
            };

        private static string ResolveActionTint(string? action) =>
            action?.ToLowerInvariant() switch
            {
                "added" => "TintGreenBrush",
                "edited" => "TintAmberBrush",
                "deleted" => "TintRedBrush",
                "bought" => "TintBlueBrush",
                "used" => "TintPurpleBrush",
                _ => "TintGrayBrush"
            };

        private static string ResolveJobStatusColor(string? status, bool isOverdue)
        {
            if (isOverdue) return "DangerBrush";
            return status switch
            {
                "Draft" => "NeutralBrush",
                "Released" => "InfoBrush",
                "InProduction" => "WarningBrush",
                "Completed" => "SuccessBrush",
                "Delivered" => "AccentBrush",
                _ => "NeutralBrush"
            };
        }

        private static string ResolveJobPriorityTint(string? priority) =>
            priority switch
            {
                "High" => "TintRedBrush",
                "Medium" => "TintAmberBrush",
                "Low" => "TintGreenBrush",
                _ => "TintIndigoBrush"
            };

        private static string ResolveDnStatusColor(string? status) =>
            status switch
            {
                "Dispatched" => "AccentBrush",
                "Delivered" => "SuccessBrush",
                "Signed" => "SuccessBrush",
                "Received" => "InfoBrush",
                _ => "NeutralBrush"
            };

        private static string ResolveDnStatusTint(string? status) =>
            status switch
            {
                "Dispatched" => "TintIndigoBrush",
                "Delivered" => "TintGreenBrush",
                "Signed" => "TintGreenBrush",
                "Received" => "TintBlueBrush",
                _ => "TintGrayBrush"
            };

        private static bool Eq(string? a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  KPI TILE MODEL
    // ═════════════════════════════════════════════════════════════════════

    public class KpiTileModel : ViewModelBase
    {
        public string Icon { get; set; } = "";
        public string Label { get; set; } = "";
        public string DisplayValue { get; set; } = "";
        public string Accent { get; set; } = "Green";  // Green|Blue|Amber|Red
        public bool IsAlert { get; set; }
    }
}