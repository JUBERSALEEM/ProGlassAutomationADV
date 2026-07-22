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
    /// Overview dashboard page.
    /// KPIs    : Total Invoices, Inventory Value, Low Stock Alerts (3 secondary cards).
    /// Hero    : Total Sales — bound to TotalSalesDisplay (Phase 8.2a).
    /// Actions : Quick action buttons (New Invoice, Saved Invoices, View Stock, Export).
    /// Chart   : Monthly Sales vertical bar chart — last 6 months.
    /// Feed    : Recent Activity — last 10 events merged from invoices + sheet history + job orders.
    /// Phase 9.4a: Production Snapshot section adds 3 Job Order mini-cards
    ///             (In Production, Overdue, Completed This Month).
    ///             Recent Activity now merges job order creation events too.
    /// </summary>
    public class OverviewDashboardViewModel : DashboardPageBase
    {
        public override string TabTitle => "Overview";
        public override string TabIcon => "🏠";

        // ── Job Order status constants (mirrors SavedJobOrder.Status storage) ─
        private const string JobStatusInProduction = "InProduction";
        private const string JobStatusCompleted = "Completed";
        private const string JobStatusDelivered = "Delivered";

        // ── navigation callbacks (wired by DashboardViewModel via MainViewModel) ──
        public Action<string>? NavigateTo { get; set; }
        public Action? TriggerNewInvoice { get; set; }

        // ── quick action commands ─────────────────────────────────────────
        public ICommand NewInvoiceCommand { get; }
        public ICommand SavedInvoicesCommand { get; }
        public ICommand ViewStockCommand { get; }
        public ICommand ExportReportCommand { get; }

        // ── KPI cards (3 secondary; Total Sales handled by hero) ──────────
        private ObservableCollection<KpiCardModel> _kpiCards = new();
        public ObservableCollection<KpiCardModel> KpiCards
        {
            get => _kpiCards;
            private set { if (_kpiCards == value) return; _kpiCards = value; OnPropertyChanged(); }
        }

        // ── Production snapshot cards (Phase 9.4a — 3 Job Order mini KPIs) ──
        private ObservableCollection<KpiCardModel> _productionCards = new();
        public ObservableCollection<KpiCardModel> ProductionCards
        {
            get => _productionCards;
            private set { if (_productionCards == value) return; _productionCards = value; OnPropertyChanged(); }
        }

        private bool _hasJobOrders;
        public bool HasJobOrders
        {
            get => _hasJobOrders;
            private set { if (_hasJobOrders == value) return; _hasJobOrders = value; OnPropertyChanged(); }
        }

        // ── hero card ─────────────────────────────────────────────────────
        private string _totalSalesDisplay = "AED 0";
        public string TotalSalesDisplay
        {
            get => _totalSalesDisplay;
            private set
            {
                if (_totalSalesDisplay == value) return;
                _totalSalesDisplay = value;
                OnPropertyChanged();
            }
        }

        private string _heroSubtitle = "VAT inclusive · all time";
        public string HeroSubtitle
        {
            get => _heroSubtitle;
            private set
            {
                if (_heroSubtitle == value) return;
                _heroSubtitle = value;
                OnPropertyChanged();
            }
        }

        private string _heroTrendText = "Live data";
        public string HeroTrendText
        {
            get => _heroTrendText;
            private set
            {
                if (_heroTrendText == value) return;
                _heroTrendText = value;
                OnPropertyChanged();
            }
        }

        private Visibility _heroTrendVisibility = Visibility.Visible;
        public Visibility HeroTrendVisibility
        {
            get => _heroTrendVisibility;
            private set
            {
                if (_heroTrendVisibility == value) return;
                _heroTrendVisibility = value;
                OnPropertyChanged();
            }
        }

        // ── monthly sales chart ───────────────────────────────────────────
        private ObservableCollection<ChartBarModel> _monthlySalesBars = new();
        public ObservableCollection<ChartBarModel> MonthlySalesBars
        {
            get => _monthlySalesBars;
            private set
            {
                if (_monthlySalesBars == value) return;
                _monthlySalesBars = value;
                OnPropertyChanged();
            }
        }

        private string _chartTitle = "Monthly Sales — Last 6 Months";
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

        // ── recent activity feed ──────────────────────────────────────────
        private ObservableCollection<ActivityFeedItem> _recentActivity = new();
        public ObservableCollection<ActivityFeedItem> RecentActivity
        {
            get => _recentActivity;
            private set
            {
                if (_recentActivity == value) return;
                _recentActivity = value;
                OnPropertyChanged();
            }
        }

        private bool _hasRecentActivity;
        public bool HasRecentActivity
        {
            get => _hasRecentActivity;
            private set
            {
                if (_hasRecentActivity == value) return;
                _hasRecentActivity = value;
                OnPropertyChanged();
            }
        }

        // ── sheet repo ────────────────────────────────────────────────────
        private readonly ISheetStoreRepository _sheetRepo = new SheetStoreRepository();

        // ═════════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public OverviewDashboardViewModel()
        {
            NewInvoiceCommand = new RelayCommand(_ =>
            {
                NavigateTo?.Invoke("ProformaInvoice");
                TriggerNewInvoice?.Invoke();
            });

            SavedInvoicesCommand = new RelayCommand(_ =>
            {
                NavigateTo?.Invoke("ProformaInvoice");
            });

            ViewStockCommand = new RelayCommand(_ =>
            {
                NavigateTo?.Invoke("SheetStore");
            });

            ExportReportCommand = new RelayCommand(
                _ => { },
                _ => false);
        }

        // ═════════════════════════════════════════════════════════════════
        // LOAD
        // ═════════════════════════════════════════════════════════════════

        protected override async Task LoadCoreAsync()
        {
            // ── 1. invoices ───────────────────────────────────────────────
            var invoices = await App.Invoices.GetAllAsync();

            int totalInvoices = invoices.Count;
            decimal totalSales = invoices.Sum(i => i.GrandTotalVatInclusive);
            int draftCount = invoices.Count(i => Eq(i.Status, "Draft"));
            int sentCount = invoices.Count(i => Eq(i.Status, "Sent"));
            int approvedCount = invoices.Count(i => Eq(i.Status, "Approved"));

            // ── 2. sheet store ────────────────────────────────────────────
            var sheets = await _sheetRepo.GetAllAsync();
            decimal inventory = sheets.Sum(s => s.BalanceValue);
            int lowStock = sheets.Count(s => s.Balance <= 5);

            // ── 3. Job Orders (Phase 9.4a) ───────────────────────────────
            var jobs = await App.Jobs.GetAllAsync();
            var today = DateTime.Today;
            var firstOfMonth = new DateTime(today.Year, today.Month, 1);

            int jobInProduction = jobs.Count(j => j.Status == JobStatusInProduction);
            int jobOverdue = jobs.Count(j => j.DeliveryDate.HasValue
                                              && j.DeliveryDate.Value.Date < today
                                              && j.Status != JobStatusCompleted
                                              && j.Status != JobStatusDelivered);
            int jobCompletedMtd = jobs.Count(j => (j.Status == JobStatusCompleted
                                                || j.Status == JobStatusDelivered)
                                              && j.UpdatedAt >= firstOfMonth);

            HasJobOrders = jobs.Count > 0;

            ProductionCards = new ObservableCollection<KpiCardModel>
            {
                new KpiCardModel
                {
                    Icon         = "⚙️",
                    Title        = "In Production",
                    DisplayValue = jobInProduction.ToString("N0"),
                    BadgeColor   = "WarningBrush",
                    TintColor    = "TintAmberBrush",
                    SubLabel     = jobInProduction > 0
                                       ? "Active on factory floor"
                                       : "No jobs in production"
                },
                new KpiCardModel
                {
                    Icon         = jobOverdue > 0 ? "⚠️" : "✅",
                    Title        = "Overdue Jobs",
                    DisplayValue = jobOverdue.ToString("N0"),
                    BadgeColor   = jobOverdue > 0 ? "DangerBrush" : "SuccessBrush",
                    TintColor    = jobOverdue > 0 ? "TintRedBrush" : "TintGreenBrush",
                    SubLabel     = jobOverdue > 0
                                       ? $"{jobOverdue} past delivery date"
                                       : "All jobs on schedule",
                    IsAlert      = jobOverdue > 0
                },
                new KpiCardModel
                {
                    Icon         = "✅",
                    Title        = "Completed This Month",
                    DisplayValue = jobCompletedMtd.ToString("N0"),
                    BadgeColor   = "SuccessBrush",
                    TintColor    = "TintGreenBrush",
                    SubLabel     = $"Since {firstOfMonth:dd MMM yyyy}"
                }
            };

            // ── 4. Hero ───────────────────────────────────────────────────
            TotalSalesDisplay = $"AED {totalSales:N0}";
            HeroSubtitle = "VAT inclusive · all time";
            HeroTrendText = totalInvoices > 0
                ? $"{totalInvoices} invoice{(totalInvoices == 1 ? "" : "s")}"
                : "No data yet";
            HeroTrendVisibility = totalInvoices > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            // ── 5. Sparkline for Total Invoices — last 14 days count/day ──
            string invoicesSparkline = BuildInvoicesSparkline(invoices);

            // ── 6. KPI cards (3 secondary — theme brush keys) ─────────────
            KpiCards = new ObservableCollection<KpiCardModel>
            {
                new KpiCardModel
                {
                    Icon         = "📄",
                    Title        = "Total Invoices",
                    DisplayValue = totalInvoices.ToString("N0"),
                    BadgeColor   = "AccentBrush",
                    TintColor    = "TintIndigoBrush",
                    SubLabel     = $"Draft {draftCount}  ·  Sent {sentCount}  ·  Approved {approvedCount}",
                    SparklineValues = invoicesSparkline
                },
                new KpiCardModel
                {
                    Icon         = "🏪",
                    Title        = "Inventory Value",
                    DisplayValue = $"AED {inventory:N0}",
                    BadgeColor   = "WarningBrush",
                    TintColor    = "TintAmberBrush",
                    SubLabel     = $"{sheets.Count} sheet types · balance at sell price"
                },
                new KpiCardModel
                {
                    Icon         = lowStock > 0 ? "⚠️" : "✅",
                    Title        = "Low Stock Alerts",
                    DisplayValue = lowStock.ToString("N0"),
                    BadgeColor   = lowStock > 0 ? "DangerBrush" : "SuccessBrush",
                    TintColor    = lowStock > 0 ? "TintRedBrush" : "TintGreenBrush",
                    SubLabel     = lowStock > 0
                                       ? $"{lowStock} sheet type(s) with ≤ 5 remaining"
                                       : "All stock levels OK",
                    IsAlert      = lowStock > 0
                }
            };

            // ── 7. Monthly sales chart (last 6 months) ────────────────────
            var now = DateTime.Now;
            var months = Enumerable.Range(0, 6)
                .Select(i => now.AddMonths(-5 + i))
                .ToList();

            var rawBars = months.Select(m =>
            {
                decimal total = invoices
                    .Where(inv =>
                    {
                        var d = inv.UpdatedAt != default
                            ? inv.UpdatedAt
                            : inv.CreatedAt;
                        return d.Year == m.Year && d.Month == m.Month;
                    })
                    .Sum(inv => inv.GrandTotalVatInclusive);

                return new ChartBarModel
                {
                    Label = m.ToString("MMM"),
                    RawValue = total,
                    ValueDisplay = total > 0 ? $"AED {total:N0}" : "—",
                    BarColor = "#6366F1"
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
                bar.BarColor = bar.IsHighlight ? "#10B981" : "#6366F1";
            }

            MonthlySalesBars = new ObservableCollection<ChartBarModel>(rawBars);
            HasChartData = anyData;
            ChartTitle = $"Monthly Sales — " +
                               $"{months.First():MMM yyyy} to {months.Last():MMM yyyy}";

            // ── 8. Recent activity feed ───────────────────────────────────
            var feedItems = new List<ActivityFeedItem>();

            // 8a. Invoice activity
            var invoiceActivity = invoices
                .Select(i =>
                {
                    var date = i.UpdatedAt != default ? i.UpdatedAt : i.CreatedAt;
                    var isUpdated = i.UpdatedAt != default && i.UpdatedAt != i.CreatedAt;
                    return new ActivityFeedItem
                    {
                        Icon = "📄",
                        Title = $"{(string.IsNullOrWhiteSpace(i.ProformaId) ? "Invoice" : i.ProformaId)}" +
                                       $"{(string.IsNullOrWhiteSpace(i.CustomerName) ? "" : $"  ·  {i.CustomerName}")}",
                        SubTitle = $"Proforma Invoice  ·  " +
                                       $"{(isUpdated ? "Updated" : "Created")} {date:dd MMM yyyy}",
                        ValueDisplay = $"AED {i.GrandTotalVatInclusive:N0}",
                        StatusLabel = i.Status ?? "Draft",
                        StatusColor = ResolveStatusColor(i.Status),
                        TintColor = ResolveStatusTint(i.Status),
                        ModuleTag = "ProformaInvoice",
                        SortDate = date
                    };
                })
                .ToList();

            feedItems.AddRange(invoiceActivity);

            // 8b. Sheet store history
            try
            {
                using var db = new AppDbContext();
                var historyRows = await db.SheetHistories
                    .OrderByDescending(h => h.Timestamp)
                    .Take(20)
                    .ToListAsync();

                var sheetActivity = historyRows.Select(h => new ActivityFeedItem
                {
                    Icon = "📦",
                    Title = string.IsNullOrWhiteSpace(h.SheetDescription)
                                       ? "Sheet Store"
                                       : h.SheetDescription,
                    SubTitle = $"Sheet Store  ·  {h.Action}  ·  {h.Timestamp:dd MMM yyyy HH:mm}",
                    ValueDisplay = h.QuantityChanged > 0
                                       ? $"{h.QuantityChanged} sheet{(h.QuantityChanged == 1 ? "" : "s")}"
                                       : h.ValueChanged.HasValue
                                           ? $"AED {h.ValueChanged:N0}"
                                           : "—",
                    StatusLabel = h.Action,
                    StatusColor = ResolveActionColor(h.Action),
                    TintColor = ResolveActionTint(h.Action),
                    ModuleTag = "SheetStore",
                    SortDate = h.Timestamp
                });

                feedItems.AddRange(sheetActivity);
            }
            catch
            {
                // sheet history unavailable — feed shows invoice activity only
            }

            // 8c. Job Order activity (Phase 9.4a)
            var jobActivity = jobs.Select(j =>
            {
                bool isOverdue = j.DeliveryDate.HasValue
                              && j.DeliveryDate.Value.Date < today
                              && j.Status != JobStatusCompleted
                              && j.Status != JobStatusDelivered;

                string statusLabel = isOverdue
                    ? "OVERDUE"
                    : (j.Status ?? "Draft").ToUpperInvariant();

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
                                       ? $"Due {j.DeliveryDate.Value:dd MMM yyyy}"
                                       : "No delivery date"),
                    ValueDisplay = j.GrandTotalVatInclusive > 0
                                       ? $"AED {j.GrandTotalVatInclusive:N0}"
                                       : "—",
                    StatusLabel = statusLabel,
                    StatusColor = ResolveJobStatusColor(j.Status, isOverdue),
                    TintColor = ResolveJobPriorityTint(j.Priority),
                    ModuleTag = "JobOrder",
                    SortDate = j.UpdatedAt
                };
            });

            feedItems.AddRange(jobActivity);

            var merged = feedItems
                .OrderByDescending(f => f.SortDate)
                .Take(10)
                .ToList();

            RecentActivity = new ObservableCollection<ActivityFeedItem>(merged);
            HasRecentActivity = merged.Count > 0;
        }

        // ═════════════════════════════════════════════════════════════════
        // PHASE 8.2f — THEME SWAP HANDLER
        // ═════════════════════════════════════════════════════════════════

        protected override void OnThemeChanged()
        {
            KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
            ProductionCards = new ObservableCollection<KpiCardModel>(_productionCards);
            RecentActivity = new ObservableCollection<ActivityFeedItem>(_recentActivity);
        }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 8.3 — Sparkline builder
        // ══════════════════════════════════════════════════════════════════

        private static string BuildInvoicesSparkline(
            IEnumerable<Models.ProformaInvoice.SavedInvoice.SavedProformaInvoice> invoices)
        {
            if (invoices == null) return "";

            var today = DateTime.Today;
            var days = Enumerable.Range(0, 14)
                .Select(i => today.AddDays(-13 + i))
                .ToList();

            var counts = days.Select(day =>
            {
                int count = invoices.Count(inv =>
                {
                    var d = inv.UpdatedAt != default ? inv.UpdatedAt.Date : inv.CreatedAt.Date;
                    return d == day;
                });
                return count;
            }).ToList();

            if (counts.All(c => c == 0)) return "";

            return string.Join(",", counts);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Theme-aware color helpers — return brush KEY names, not hex
        // ══════════════════════════════════════════════════════════════════

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

        private static bool Eq(string? a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}