// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Dashboard/TaxInvoice/TiDashboard/TiDashboardViewModel.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.ViewModels.Dashboard.TaxInvoice.TiDashboard
// VERSION: Chat 23 v2.5.1 — Industrial blue chart palette
//
// PURPOSE:
//   Dashboard sub-page ViewModel for the Tax Invoice tab.
//   Inherits DashboardPageBase → gets LoadAsync(), IsLoading, HasError,
//   LastRefreshed, TabTitle, TabIcon, OnThemeChanged().
//
// WIDGETS:
//   1. 5 KPI hero cards  (KpiTileModel collection — reuses Overview pattern)
//   2. Recent Invoices   (last 5, ObservableCollection<RecentInvoiceItem>)
//   3. Aging Report      (4 brackets, ObservableCollection<AgingBracket>)
//   4. Top Customers     (top 5, ObservableCollection<TopCustomerItem>)
//   5. Revenue Trend     (6-month bars, ObservableCollection<MonthlyRevenuePoint>)
//   6. Overdue Banner    (conditional red alert)
//
// NAVIGATION:
//   NavigateTo callback set by DashboardViewModel after construction.
//   "Create Tax Invoice" button → NavigateTo("TaxInvoice")
//   Recent invoice row click  → NavigateTo("TaxInvoice")
//   View overdue button       → NavigateTo("TaxInvoice")
//
// PATTERN:
//   Follows OverviewDashboardViewModel exactly:
//   • LoadCoreAsync() calls service, then normalizes chart data
//   • OnThemeChanged() rebuilds ObservableCollections for brush refresh
//   • KPI tiles use same KpiTileModel as Overview (already in Models/Dashboard)
//
// CHANGES vs previous version:
//   Chat 23 v2.5.1: ONLY revenue chart bar hex colors changed to align
//   with industrial blue palette (matches OverviewDashboardViewModel v2.0.1):
//     • Regular bar:   "#6366F1" (indigo)        → "#3B82F6" (blue-500)
//     • Highlight bar: "#10B981" (emerald green) → "#1D4ED8" (royal blue)
//
//   ZERO other logic changes. All original documentation comments
//   preserved verbatim (v2.5 accidentally stripped them — restored here).
//
// FLAG FOR LATER:
//   Cannot verify brush keys emitted by TaxInvoiceMetricsService for:
//     RecentInvoiceItem.StatusColor
//     RecentInvoiceItem.TintColor
//     AgingBracket.SeverityColor
//     AgingBracket.TintColor
//   If the service emits "RedBrush"/"AmberBrush"/"GreenBrush"/"PurpleBrush"
//   (all MISSING from LightTheme.xaml v1.6), status pills and severity
//   bars render TRANSPARENT — same silent bug that JoDashboard and
//   DnDashboard had. To verify/fix, share TaxInvoiceMetricsService.cs.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Dashboard;
using ProGlassAutomation.Models.Dashboard.TaxInvoiceDashboard;
using ProGlassAutomation.Services.Dashboard.TaxInvoiceMetrics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Dashboard.TaxInvoice.TiDashboard
{
    /// <summary>
    /// Tax Invoice dashboard tab page.
    /// Registered as a tab in DashboardViewModel.Pages.
    /// </summary>
    public sealed class TiDashboardViewModel : DashboardPageBase
    {
        // ── Tab identity ──────────────────────────────────────────────────
        public override string TabTitle => "Tax Invoices";
        public override string TabIcon => "🧾";

        // ── Navigation callback (set by DashboardViewModel after ctor) ────
        public Action<string>? NavigateTo { get; set; }

        // ── Service ───────────────────────────────────────────────────────
        private readonly ITaxInvoiceMetricsService _metricsService;

        // ═════════════════════════════════════════════════════════════════
        //  KPI HERO CARDS (5 tiles — reuses KpiTileModel from Overview)
        // ═════════════════════════════════════════════════════════════════

        private ObservableCollection<KpiTileModel> _kpiTiles = new();
        public ObservableCollection<KpiTileModel> KpiTiles
        {
            get => _kpiTiles;
            private set
            {
                if (_kpiTiles == value) return;
                _kpiTiles = value;
                OnPropertyChanged();
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  OVERDUE ALERT BANNER
        // ═════════════════════════════════════════════════════════════════

        private bool _hasOverdueInvoices;
        public bool HasOverdueInvoices
        {
            get => _hasOverdueInvoices;
            private set
            {
                if (_hasOverdueInvoices == value) return;
                _hasOverdueInvoices = value;
                OnPropertyChanged();
            }
        }

        private int _overdueCount;
        public int OverdueCount
        {
            get => _overdueCount;
            private set
            {
                if (_overdueCount == value) return;
                _overdueCount = value;
                OnPropertyChanged();
            }
        }

        private decimal _overdueAmount;
        public decimal OverdueAmount
        {
            get => _overdueAmount;
            private set
            {
                if (_overdueAmount == value) return;
                _overdueAmount = value;
                OnPropertyChanged();
            }
        }

        private string _overdueAmountDisplay = "AED 0.00";
        public string OverdueAmountDisplay
        {
            get => _overdueAmountDisplay;
            private set
            {
                if (_overdueAmountDisplay == value) return;
                _overdueAmountDisplay = value;
                OnPropertyChanged();
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  RECENT INVOICES WIDGET
        // ═════════════════════════════════════════════════════════════════

        private ObservableCollection<RecentInvoiceItem> _recentInvoices = new();
        public ObservableCollection<RecentInvoiceItem> RecentInvoices
        {
            get => _recentInvoices;
            private set
            {
                if (_recentInvoices == value) return;
                _recentInvoices = value;
                OnPropertyChanged();
            }
        }

        private bool _hasRecentInvoices;
        public bool HasRecentInvoices
        {
            get => _hasRecentInvoices;
            private set
            {
                if (_hasRecentInvoices == value) return;
                _hasRecentInvoices = value;
                OnPropertyChanged();
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  AGING REPORT WIDGET
        // ═════════════════════════════════════════════════════════════════

        private ObservableCollection<AgingBracket> _agingBrackets = new();
        public ObservableCollection<AgingBracket> AgingBrackets
        {
            get => _agingBrackets;
            private set
            {
                if (_agingBrackets == value) return;
                _agingBrackets = value;
                OnPropertyChanged();
            }
        }

        private bool _hasAgingData;
        public bool HasAgingData
        {
            get => _hasAgingData;
            private set
            {
                if (_hasAgingData == value) return;
                _hasAgingData = value;
                OnPropertyChanged();
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  TOP CUSTOMERS WIDGET
        // ═════════════════════════════════════════════════════════════════

        private ObservableCollection<TopCustomerItem> _topCustomers = new();
        public ObservableCollection<TopCustomerItem> TopCustomers
        {
            get => _topCustomers;
            private set
            {
                if (_topCustomers == value) return;
                _topCustomers = value;
                OnPropertyChanged();
            }
        }

        private bool _hasTopCustomers;
        public bool HasTopCustomers
        {
            get => _hasTopCustomers;
            private set
            {
                if (_hasTopCustomers == value) return;
                _hasTopCustomers = value;
                OnPropertyChanged();
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  REVENUE TREND CHART (6-month bar chart)
        // ═════════════════════════════════════════════════════════════════

        private ObservableCollection<MonthlyRevenuePoint> _revenueBars = new();
        public ObservableCollection<MonthlyRevenuePoint> RevenueBars
        {
            get => _revenueBars;
            private set
            {
                if (_revenueBars == value) return;
                _revenueBars = value;
                OnPropertyChanged();
            }
        }

        private bool _hasRevenueData;
        public bool HasRevenueData
        {
            get => _hasRevenueData;
            private set
            {
                if (_hasRevenueData == value) return;
                _hasRevenueData = value;
                OnPropertyChanged();
            }
        }

        private string _revenueChartTitle = "Last 6 months";
        public string RevenueChartTitle
        {
            get => _revenueChartTitle;
            private set
            {
                if (_revenueChartTitle == value) return;
                _revenueChartTitle = value;
                OnPropertyChanged();
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  COMMANDS
        // ═════════════════════════════════════════════════════════════════

        /// <summary>Navigates to Tax Invoice module list view.</summary>
        public ICommand CreateTaxInvoiceCommand { get; }

        /// <summary>Navigates to Tax Invoice module (overdue filter applied by list VM).</summary>
        public ICommand ViewOverdueCommand { get; }

        /// <summary>Navigates to Tax Invoice module list view ("View All" link).</summary>
        public ICommand ViewAllInvoicesCommand { get; }

        /// <summary>
        /// Click on a Recent Invoice row — navigate to Tax Invoice module.
        /// Parameter: RecentInvoiceItem (the clicked row).
        /// </summary>
        public ICommand OpenInvoiceCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public TiDashboardViewModel()
            : this(new TaxInvoiceMetricsService()) { }

        /// <summary>
        /// Testable constructor — accepts any ITaxInvoiceMetricsService.
        /// </summary>
        public TiDashboardViewModel(ITaxInvoiceMetricsService metricsService)
        {
            _metricsService = metricsService
                ?? throw new ArgumentNullException(nameof(metricsService));

            // All navigation commands just route to TaxInvoice module
            CreateTaxInvoiceCommand = new RelayCommand(_ =>
                NavigateTo?.Invoke("TaxInvoice"));

            ViewOverdueCommand = new RelayCommand(_ =>
                NavigateTo?.Invoke("TaxInvoice"));

            ViewAllInvoicesCommand = new RelayCommand(_ =>
                NavigateTo?.Invoke("TaxInvoice"));

            // Row click — navigate to Tax Invoice module
            // (Full deep-link to specific invoice requires TaxInvoiceVM integration
            //  which is outside scope of Chat 15 — navigates to list for now)
            OpenInvoiceCommand = new RelayCommand(_ =>
                NavigateTo?.Invoke("TaxInvoice"));
        }

        // ═════════════════════════════════════════════════════════════════
        //  LOAD CORE — called by DashboardPageBase.LoadAsync()
        // ═════════════════════════════════════════════════════════════════

        protected override async Task LoadCoreAsync()
        {
            // ── Run all 5 queries concurrently for speed ───────────────────
            // Each creates its own DbContext — safe to run in parallel
            var kpiTask = _metricsService.GetKpiSummaryAsync();
            var recentTask = _metricsService.GetRecentInvoicesAsync(5);
            var agingTask = _metricsService.GetAgingReportAsync();
            var customersTask = _metricsService.GetTopCustomersAsync(12, 5);
            var trendTask = _metricsService.GetMonthlyRevenueTrendAsync(6);

            await Task.WhenAll(kpiTask, recentTask, agingTask, customersTask, trendTask);

            var kpi = await kpiTask;
            var recent = await recentTask;
            var aging = await agingTask;
            var customers = await customersTask;
            var trend = await trendTask;

            // ── 1. Build KPI tiles ────────────────────────────────────────
            BuildKpiTiles(kpi);

            // ── 2. Overdue banner ─────────────────────────────────────────
            HasOverdueInvoices = kpi.HasOverdueInvoices;
            OverdueCount = kpi.OverdueCount;
            OverdueAmount = kpi.OverdueAmount;
            OverdueAmountDisplay = $"AED {kpi.OverdueAmount:N2}";

            // ── 3. Recent invoices ────────────────────────────────────────
            RecentInvoices = new ObservableCollection<RecentInvoiceItem>(recent);
            HasRecentInvoices = recent.Count > 0;

            // ── 4. Aging brackets — normalize bar widths ──────────────────
            NormalizeAgingWidths(aging);
            AgingBrackets = new ObservableCollection<AgingBracket>(aging);
            HasAgingData = aging.Any(b => b.InvoiceCount > 0);

            // ── 5. Top customers — normalize bar widths ───────────────────
            NormalizeCustomerWidths(customers);
            TopCustomers = new ObservableCollection<TopCustomerItem>(customers);
            HasTopCustomers = customers.Count > 0;

            // ── 6. Revenue trend — normalize bar heights + colors ─────────
            NormalizeRevenueBars(trend);
            RevenueBars = new ObservableCollection<MonthlyRevenuePoint>(trend);
            HasRevenueData = trend.Any(p => p.TotalInvoiced > 0);

            if (trend.Count >= 2)
            {
                RevenueChartTitle =
                    $"{trend.First().Month:MMM yyyy} → {trend.Last().Month:MMM yyyy}";
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  THEME SWAP — rebuild collections so brush keys re-resolve
        // ═════════════════════════════════════════════════════════════════

        protected override void OnThemeChanged()
        {
            // Force WPF DataTemplate to re-evaluate brush-key bindings
            // by replacing ObservableCollections (same pattern as Overview)
            KpiTiles = new ObservableCollection<KpiTileModel>(_kpiTiles);
            RecentInvoices = new ObservableCollection<RecentInvoiceItem>(_recentInvoices);
            AgingBrackets = new ObservableCollection<AgingBracket>(_agingBrackets);
            TopCustomers = new ObservableCollection<TopCustomerItem>(_topCustomers);
            RevenueBars = new ObservableCollection<MonthlyRevenuePoint>(_revenueBars);
        }

        // ═════════════════════════════════════════════════════════════════
        //  PRIVATE: BUILD KPI TILES
        // ═════════════════════════════════════════════════════════════════

        private void BuildKpiTiles(TaxInvoiceKpiSummary kpi)
        {
            // ── MoM arrow text for Card 1 sub-label ──────────────────────
            string momText;
            if (kpi.MonthOverMonthPct == null)
            {
                momText = kpi.TotalInvoicedLastMonth == 0m
                    ? "First month on record"
                    : "vs last month";
            }
            else
            {
                var pct = kpi.MonthOverMonthPct.Value;
                var arrow = pct >= 0 ? "↑" : "↓";
                momText = $"{arrow} {Math.Abs(pct):N1}% vs AED {kpi.TotalInvoicedLastMonth:N0} last month";
            }

            // ── Pending sub-label breakdown ───────────────────────────────
            string pendingSubText =
                $"{kpi.DraftCount} Draft · {kpi.IssuedCount} Issued";

            // ── Build 5 tiles ─────────────────────────────────────────────
            var tiles = new List<KpiTileModel>
            {
                // CARD 1: Total Invoiced MTD
                new KpiTileModel
                {
                    Icon         = "💰",
                    Label        = "Invoiced this Month",
                    DisplayValue = $"AED {kpi.TotalInvoicedThisMonth:N2}",
                    Accent       = "Green"
                },

                // CARD 2: VAT Collected MTD
                new KpiTileModel
                {
                    Icon         = "🏛",
                    Label        = "UAE VAT Collected (5%)",
                    DisplayValue = $"AED {kpi.VatCollectedThisMonth:N2}",
                    Accent       = "Blue"
                },

                // CARD 3: Overdue Amount
                new KpiTileModel
                {
                    Icon         = kpi.OverdueCount > 0 ? "⚠️" : "✅",
                    Label        = "Overdue Invoices",
                    DisplayValue = kpi.OverdueCount > 0
                                   ? $"AED {kpi.OverdueAmount:N2}"
                                   : "None",
                    Accent       = kpi.OverdueCount > 0 ? "Red" : "Green",
                    IsAlert      = kpi.OverdueCount > 0
                },

                // CARD 4: Pending (Draft + Issued)
                new KpiTileModel
                {
                    Icon         = "📤",
                    Label        = "Pending Invoices",
                    DisplayValue = kpi.PendingCount.ToString("N0"),
                    Accent       = "Amber"
                },

                // CARD 5: Paid MTD
                new KpiTileModel
                {
                    Icon         = "✅",
                    Label        = "Paid this Month",
                    DisplayValue = kpi.PaidThisMonthCount.ToString("N0"),
                    Accent       = "Green"
                }
            };

            KpiTiles = new ObservableCollection<KpiTileModel>(tiles);
        }

        // ═════════════════════════════════════════════════════════════════
        //  PRIVATE: NORMALIZE BAR WIDTHS (Aging + Top Customers)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sets NormalizedWidth on each AgingBracket (0.0–1.0).
        /// The bracket with the highest Amount gets 1.0.
        /// Zero-amount brackets get 0.03 (stub for visual presence).
        /// </summary>
        private static void NormalizeAgingWidths(List<AgingBracket> brackets)
        {
            if (brackets.Count == 0) return;

            decimal maxAmt = brackets.Max(b => b.Amount);
            if (maxAmt <= 0m)
            {
                foreach (var b in brackets) b.NormalizedWidth = 0.03;
                return;
            }

            foreach (var b in brackets)
            {
                b.NormalizedWidth = b.Amount > 0
                    ? (double)(b.Amount / maxAmt)
                    : 0.03;
            }
        }

        /// <summary>
        /// Sets NormalizedWidth on each TopCustomerItem.
        /// Rank 1 always gets 1.0. Others are proportional.
        /// </summary>
        private static void NormalizeCustomerWidths(List<TopCustomerItem> customers)
        {
            if (customers.Count == 0) return;

            decimal maxAmt = customers.Max(c => c.TotalAmount);
            if (maxAmt <= 0m)
            {
                foreach (var c in customers) c.NormalizedWidth = 0.03;
                return;
            }

            foreach (var c in customers)
            {
                c.NormalizedWidth = c.TotalAmount > 0
                    ? (double)(c.TotalAmount / maxAmt)
                    : 0.03;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  PRIVATE: NORMALIZE REVENUE BARS
        //  Mirrors OverviewDashboardViewModel chart normalization exactly.
        //  Chat 23 v2.5.1: bar colors updated to industrial blue palette.
        //    Regular:   "#6366F1" (indigo) → "#3B82F6" (blue-500)
        //    Highlight: "#10B981" (emerald green) → "#1D4ED8" (royal blue)
        //  Matches Overview + PiDashboard chart palette exactly.
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sets NormalizedHeight (0.0–1.0), IsHighlight, and BarColor
        /// on each MonthlyRevenuePoint. Highest month = 1.0 + royal blue.
        /// Zero months = 0.04 stub + blue-500.
        /// </summary>
        private static void NormalizeRevenueBars(List<MonthlyRevenuePoint> points)
        {
            if (points.Count == 0) return;

            decimal maxVal = points.Max(p => p.TotalInvoiced);
            bool anyData = maxVal > 0m;

            foreach (var p in points)
            {
                p.NormalizedHeight = anyData
                    ? (double)(p.TotalInvoiced / maxVal)
                    : 0.04;

                p.IsHighlight = anyData && p.TotalInvoiced == maxVal;

                // v2.5.1: royal blue for highlight, blue-500 for regular
                // (was emerald #10B981 highlight, indigo #6366F1 regular)
                p.BarColor = p.IsHighlight ? "#1D4ED8" : "#3B82F6";
            }
        }
    }
}