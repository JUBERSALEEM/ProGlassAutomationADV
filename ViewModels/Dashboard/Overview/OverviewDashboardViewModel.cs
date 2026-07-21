using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Dashboard;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.SheetStore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Dashboard
{
    /// <summary>
    /// Overview dashboard page.
    /// KPIs    : Total Invoices, Inventory Value, Low Stock Alerts (3 secondary cards).
    /// Hero    : Total Sales — bound to TotalSalesDisplay (Phase 8.2a).
    /// Actions : Quick action buttons (New Invoice, Saved Invoices, View Stock, Export).
    /// Chart   : Monthly Sales vertical bar chart — last 6 months.
    /// Feed    : Recent Activity — last 10 events merged from invoices + sheet history.
    /// Phase 8.2a: Cards + rows now use TintColor for subtle tinted backgrounds.
    /// </summary>
    public class OverviewDashboardViewModel : DashboardPageBase
    {
        public override string TabTitle => "Overview";
        public override string TabIcon => "🏠";

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

            // ── 3. Hero ───────────────────────────────────────────────────
            TotalSalesDisplay = $"AED {totalSales:N0}";

            // ── 4. KPI cards (3 secondary — with tinted backgrounds) ──────
            KpiCards = new ObservableCollection<KpiCardModel>
            {
                new KpiCardModel
                {
                    Icon         = "📄",
                    Title        = "Total Invoices",
                    DisplayValue = totalInvoices.ToString("N0"),
                    BadgeColor   = "#6366F1",
                    TintColor    = "#EEF2FF",   // indigo-50
                    SubLabel     = $"Draft {draftCount}  ·  Sent {sentCount}  ·  Approved {approvedCount}"
                },
                new KpiCardModel
                {
                    Icon         = "🏪",
                    Title        = "Inventory Value",
                    DisplayValue = $"AED {inventory:N0}",
                    BadgeColor   = "#F59E0B",
                    TintColor    = "#FFFBEB",   // amber-50
                    SubLabel     = $"{sheets.Count} sheet types · balance at sell price"
                },
                new KpiCardModel
                {
                    Icon         = lowStock > 0 ? "⚠️" : "✅",
                    Title        = "Low Stock Alerts",
                    DisplayValue = lowStock.ToString("N0"),
                    BadgeColor   = lowStock > 0 ? "#EF4444" : "#10B981",
                    TintColor    = lowStock > 0 ? "#FEF2F2" : "#F0FDF4",  // red-50 or green-50
                    SubLabel     = lowStock > 0
                                       ? $"{lowStock} sheet type(s) with ≤ 5 remaining"
                                       : "All stock levels OK",
                    IsAlert      = lowStock > 0
                }
            };

            // ── 5. Monthly sales chart (last 6 months) ────────────────────
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

            // ── 6. Recent activity feed ───────────────────────────────────
            var feedItems = new System.Collections.Generic.List<ActivityFeedItem>();

            // 6a. Invoice activity
            var invoiceActivity = invoices
                .Select(i =>
                {
                    var date = i.UpdatedAt != default ? i.UpdatedAt : i.CreatedAt;
                    var isUpdated = i.UpdatedAt != default && i.UpdatedAt != i.CreatedAt;
                    var statusColor = ResolveStatusColor(i.Status);
                    return new ActivityFeedItem
                    {
                        Icon = "📄",
                        Title = $"{(string.IsNullOrWhiteSpace(i.ProformaId) ? "Invoice" : i.ProformaId)}" +
                                       $"{(string.IsNullOrWhiteSpace(i.CustomerName) ? "" : $"  ·  {i.CustomerName}")}",
                        SubTitle = $"Proforma Invoice  ·  " +
                                       $"{(isUpdated ? "Updated" : "Created")} {date:dd MMM yyyy}",
                        ValueDisplay = $"AED {i.GrandTotalVatInclusive:N0}",
                        StatusLabel = i.Status ?? "Draft",
                        StatusColor = statusColor,
                        TintColor = ResolveStatusTint(i.Status),
                        ModuleTag = "ProformaInvoice",
                        SortDate = date
                    };
                })
                .ToList();

            feedItems.AddRange(invoiceActivity);

            // 6b. Sheet store history
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
                    StatusColor = h.ActionColor,
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

            var merged = feedItems
                .OrderByDescending(f => f.SortDate)
                .Take(10)
                .ToList();

            RecentActivity = new ObservableCollection<ActivityFeedItem>(merged);
            HasRecentActivity = merged.Count > 0;
        }

        // ── helpers ───────────────────────────────────────────────────────

        private static string ResolveStatusColor(string? status) =>
            status?.ToLowerInvariant() switch
            {
                "draft" => "#6B7280",
                "sent" => "#3B82F6",
                "approved" => "#10B981",
                "rejected" => "#EF4444",
                _ => "#6B7280"
            };

        private static string ResolveStatusTint(string? status) =>
            status?.ToLowerInvariant() switch
            {
                "draft" => "#F9FAFB",    // gray-50
                "sent" => "#EFF6FF",    // blue-50
                "approved" => "#F0FDF4",    // green-50
                "rejected" => "#FEF2F2",    // red-50
                _ => "#F9FAFB"
            };

        private static string ResolveActionTint(string? action) =>
            action?.ToLowerInvariant() switch
            {
                "added" => "#F0FDF4",    // green-50
                "edited" => "#FFFBEB",    // amber-50
                "deleted" => "#FEF2F2",    // red-50
                "bought" => "#EFF6FF",    // blue-50
                "used" => "#F5F3FF",    // purple-50
                _ => "#F9FAFB"
            };

        private static bool Eq(string? a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}