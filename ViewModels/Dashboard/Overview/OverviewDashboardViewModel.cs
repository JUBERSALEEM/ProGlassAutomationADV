using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Dashboard;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.SheetStore;
using Microsoft.EntityFrameworkCore;
using System;
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
    /// Feed    : Recent Activity — last 10 events merged from invoices + sheet history.
    /// Phase 8.2a: Cards + rows now use TintColor for subtle tinted backgrounds.
    /// Phase 8.2e: Card BadgeColor/TintColor and activity StatusColor/TintColor
    ///             now hold theme brush KEY names for automatic theme adaptation.
    /// Phase 8.2f: Overrides OnThemeChanged() to force re-notify observable
    ///             collections so WPF rebuilds card templates on theme swap.
    /// Phase 8.3 : Adds SparklineValues on Total Invoices card + hero trend
    ///             badge properties for the reusable GradientHeroCard control.
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

        // Phase 8.3 — Hero card additional properties (consumed by GradientHeroCard)

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

            // ── 3. Hero ───────────────────────────────────────────────────
            TotalSalesDisplay = $"AED {totalSales:N0}";
            HeroSubtitle = "VAT inclusive · all time";
            HeroTrendText = totalInvoices > 0
                ? $"{totalInvoices} invoice{(totalInvoices == 1 ? "" : "s")}"
                : "No data yet";
            HeroTrendVisibility = totalInvoices > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            // ── 4. Sparkline for Total Invoices — last 14 days count/day ──
            string invoicesSparkline = BuildInvoicesSparkline(invoices);

            // ── 5. KPI cards (3 secondary — theme brush keys) ─────────────
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

            // ── 6. Monthly sales chart (last 6 months) ────────────────────
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

            // ── 7. Recent activity feed ───────────────────────────────────
            var feedItems = new System.Collections.Generic.List<ActivityFeedItem>();

            // 7a. Invoice activity
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

            // 7b. Sheet store history
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

            var merged = feedItems
                .OrderByDescending(f => f.SortDate)
                .Take(10)
                .ToList();

            RecentActivity = new ObservableCollection<ActivityFeedItem>(merged);
            HasRecentActivity = merged.Count > 0;
        }

        // ═════════════════════════════════════════════════════════════════
        // PHASE 8.2f — THEME SWAP HANDLER
        // Forces WPF to rebuild card + activity DataTemplates so the
        // BrushKeyConverter re-resolves against the new theme brushes.
        // ═════════════════════════════════════════════════════════════════

        protected override void OnThemeChanged()
        {
            // Rebuild observable collections with the same data — this
            // triggers WPF to teardown+recreate the DataTemplates which
            // in turn re-runs BrushKeyConverter against the new theme.
            KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
            RecentActivity = new ObservableCollection<ActivityFeedItem>(_recentActivity);
        }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 8.3 — Sparkline builder
        //  Groups invoices by day for the last 14 days and returns a
        //  comma-separated string of counts for SparklineControl.
        // ══════════════════════════════════════════════════════════════════

        private static string BuildInvoicesSparkline(
            System.Collections.Generic.IEnumerable<Models.ProformaInvoice.SavedInvoice.SavedProformaInvoice> invoices)
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

            // If all zeros, return empty (sparkline shows nothing) so we
            // don't render a flat line.
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

        private static bool Eq(string? a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}