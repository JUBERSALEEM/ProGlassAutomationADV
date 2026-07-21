using ProGlassAutomation.Models.Dashboard;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Dashboard
{
    /// <summary>
    /// Proforma Invoice dashboard page.
    /// KPIs : Total, Approved, Sent/Pending, Draft counts + values.
    /// Charts: Status distribution bars + Top 5 customers by value.
    /// Phase 8.2a: Cards now use TintColor for subtle tinted backgrounds.
    /// Phase 8.2e: BadgeColor and TintColor use theme brush KEY names.
    /// Phase 8.2f: Overrides OnThemeChanged() to force re-notify KpiCards
    ///             so WPF rebuilds card templates instantly on theme swap.
    /// </summary>
    public class PiDashboardViewModel : DashboardPageBase
    {
        public override string TabTitle => "Proforma Invoice";
        public override string TabIcon => "📄";

        // ── KPI cards ─────────────────────────────────────────────────────
        private ObservableCollection<KpiCardModel> _kpiCards = new();
        public ObservableCollection<KpiCardModel> KpiCards
        {
            get => _kpiCards;
            private set { if (_kpiCards == value) return; _kpiCards = value; OnPropertyChanged(); }
        }

        // ── status bars ───────────────────────────────────────────────────
        private ObservableCollection<ChartBarModel> _statusBars = new();
        public ObservableCollection<ChartBarModel> StatusBars
        {
            get => _statusBars;
            private set { if (_statusBars == value) return; _statusBars = value; OnPropertyChanged(); }
        }

        // ── top customer bars ─────────────────────────────────────────────
        private ObservableCollection<ChartBarModel> _topCustomerBars = new();
        public ObservableCollection<ChartBarModel> TopCustomerBars
        {
            get => _topCustomerBars;
            private set
            {
                if (_topCustomerBars == value) return;
                _topCustomerBars = value;
                OnPropertyChanged();
            }
        }

        private bool _hasChartData;
        public bool HasChartData
        {
            get => _hasChartData;
            private set { if (_hasChartData == value) return; _hasChartData = value; OnPropertyChanged(); }
        }

        // ═════════════════════════════════════════════════════════════════
        protected override async Task LoadCoreAsync()
        {
            var invoices = await App.Invoices.GetAllAsync();

            int total = invoices.Count;
            decimal totalVal = invoices.Sum(i => i.GrandTotalVatInclusive);
            int draftCount = invoices.Count(i => Eq(i.Status, "Draft"));
            int sentCount = invoices.Count(i => Eq(i.Status, "Sent"));
            int approvedCount = invoices.Count(i => Eq(i.Status, "Approved"));
            int rejectedCount = invoices.Count(i => Eq(i.Status, "Rejected"));

            decimal approvedVal = invoices
                .Where(i => Eq(i.Status, "Approved"))
                .Sum(i => i.GrandTotalVatInclusive);

            decimal pendingVal = invoices
                .Where(i => Eq(i.Status, "Sent"))
                .Sum(i => i.GrandTotalVatInclusive);

            // ── KPI cards (theme brush keys) ──────────────────────────────
            KpiCards = new ObservableCollection<KpiCardModel>
            {
                new KpiCardModel
                {
                    Icon         = "📋",
                    Title        = "Total Invoices",
                    DisplayValue = total.ToString("N0"),
                    BadgeColor   = "AccentBrush",
                    TintColor    = "TintIndigoBrush",
                    SubLabel     = $"Total value AED {totalVal:N0}"
                },
                new KpiCardModel
                {
                    Icon         = "✅",
                    Title        = "Approved",
                    DisplayValue = approvedCount.ToString("N0"),
                    BadgeColor   = "SuccessBrush",
                    TintColor    = "TintGreenBrush",
                    SubLabel     = $"AED {approvedVal:N0} confirmed"
                },
                new KpiCardModel
                {
                    Icon         = "📨",
                    Title        = "Sent / Pending",
                    DisplayValue = sentCount.ToString("N0"),
                    BadgeColor   = "InfoBrush",
                    TintColor    = "TintBlueBrush",
                    SubLabel     = $"AED {pendingVal:N0} awaiting response"
                },
                new KpiCardModel
                {
                    Icon         = "✏️",
                    Title        = "Draft",
                    DisplayValue = draftCount.ToString("N0"),
                    BadgeColor   = "NeutralBrush",
                    TintColor    = "TintGrayBrush",
                    SubLabel     = $"{rejectedCount} rejected"
                }
            };

            // ── status distribution bars (chart hex accents) ──────────────
            var statusData = new[]
            {
                (Label: "Draft",    Count: draftCount,    Color: "#6B7280"),
                (Label: "Sent",     Count: sentCount,     Color: "#3B82F6"),
                (Label: "Approved", Count: approvedCount, Color: "#10B981"),
                (Label: "Rejected", Count: rejectedCount, Color: "#EF4444"),
            };

            int maxStatus = statusData.Max(s => s.Count);

            StatusBars = new ObservableCollection<ChartBarModel>(
                statusData.Select(s => new ChartBarModel
                {
                    Label = s.Label,
                    RawValue = s.Count,
                    ValueDisplay = s.Count.ToString("N0"),
                    NormalizedHeight = maxStatus > 0
                                           ? (double)s.Count / maxStatus
                                           : 0.04,
                    BarColor = s.Color,
                    IsHighlight = maxStatus > 0 && s.Count == maxStatus
                }));

            // ── top 5 customers ───────────────────────────────────────────
            var topCustomers = invoices
                .GroupBy(i => string.IsNullOrWhiteSpace(i.CustomerName)
                                  ? "Unknown" : i.CustomerName)
                .Select(g => new
                {
                    Name = g.Key,
                    Total = g.Sum(i => i.GrandTotalVatInclusive)
                })
                .OrderByDescending(g => g.Total)
                .Take(5)
                .ToList();

            decimal maxCust = topCustomers.Any()
                ? topCustomers.Max(g => g.Total)
                : 1m;

            string[] custColors =
            {
                "#6366F1", "#10B981", "#F59E0B", "#3B82F6", "#EC4899"
            };

            TopCustomerBars = new ObservableCollection<ChartBarModel>(
                topCustomers.Select((c, idx) => new ChartBarModel
                {
                    Label = c.Name.Length > 14
                                           ? c.Name[..14] + "…"
                                           : c.Name,
                    RawValue = c.Total,
                    ValueDisplay = $"AED {c.Total:N0}",
                    NormalizedHeight = maxCust > 0
                                           ? (double)(c.Total / maxCust)
                                           : 0.04,
                    BarColor = custColors[idx % custColors.Length],
                    IsHighlight = idx == 0
                }));

            HasChartData = invoices.Any();
        }

        // ═════════════════════════════════════════════════════════════════
        // PHASE 8.2f — THEME SWAP HANDLER
        // ═════════════════════════════════════════════════════════════════

        protected override void OnThemeChanged()
        {
            // Force WPF to rebuild KPI card templates with new theme brushes
            KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
        }

        private static bool Eq(string? a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}