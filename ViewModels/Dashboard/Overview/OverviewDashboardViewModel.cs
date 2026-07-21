using ProGlassAutomation.Models.Dashboard;
using ProGlassAutomation.Services.SheetStore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Dashboard
{
    /// <summary>
    /// Overview dashboard page.
    /// KPIs : Total Invoices, Total Sales, Inventory Value, Low Stock Alerts.
    /// Chart: Monthly Sales vertical bar chart — last 6 months.
    /// </summary>
    public class OverviewDashboardViewModel : DashboardPageBase
    {
        public override string TabTitle => "Overview";
        public override string TabIcon => "🏠";

        // ── KPI cards ─────────────────────────────────────────────────────
        private ObservableCollection<KpiCardModel> _kpiCards = new();
        public ObservableCollection<KpiCardModel> KpiCards
        {
            get => _kpiCards;
            private set { if (_kpiCards == value) return; _kpiCards = value; OnPropertyChanged(); }
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

        private readonly ISheetStoreRepository _sheetRepo = new SheetStoreRepository();

        // ═════════════════════════════════════════════════════════════════
        protected override async Task LoadCoreAsync()
        {
            // ── invoices ──────────────────────────────────────────────────
            var invoices = await App.Invoices.GetAllAsync();

            int totalInvoices = invoices.Count;
            decimal totalSales = invoices.Sum(i => i.GrandTotalVatInclusive);
            int draftCount = invoices.Count(i => Eq(i.Status, "Draft"));
            int sentCount = invoices.Count(i => Eq(i.Status, "Sent"));
            int approvedCount = invoices.Count(i => Eq(i.Status, "Approved"));

            // ── sheet store ───────────────────────────────────────────────
            var sheets = await _sheetRepo.GetAllAsync();
            decimal inventory = sheets.Sum(s => s.BalanceValue);
            int lowStock = sheets.Count(s => s.Balance <= 5);

            // ── KPI cards ─────────────────────────────────────────────────
            KpiCards = new ObservableCollection<KpiCardModel>
            {
                new KpiCardModel
                {
                    Icon         = "📄",
                    Title        = "Total Invoices",
                    DisplayValue = totalInvoices.ToString("N0"),
                    BadgeColor   = "#6366F1",
                    SubLabel     = $"Draft {draftCount}  ·  Sent {sentCount}  ·  Approved {approvedCount}"
                },
                new KpiCardModel
                {
                    Icon         = "💰",
                    Title        = "Total Sales",
                    DisplayValue = $"AED {totalSales:N0}",
                    BadgeColor   = "#10B981",
                    SubLabel     = "VAT inclusive · all time"
                },
                new KpiCardModel
                {
                    Icon         = "🏪",
                    Title        = "Inventory Value",
                    DisplayValue = $"AED {inventory:N0}",
                    BadgeColor   = "#F59E0B",
                    SubLabel     = $"{sheets.Count} sheet types · balance at sell price"
                },
                new KpiCardModel
                {
                    Icon         = lowStock > 0 ? "⚠️" : "✅",
                    Title        = "Low Stock Alerts",
                    DisplayValue = lowStock.ToString("N0"),
                    BadgeColor   = lowStock > 0 ? "#EF4444" : "#10B981",
                    SubLabel     = lowStock > 0
                                       ? $"{lowStock} sheet type(s) with ≤ 5 remaining"
                                       : "All stock levels OK",
                    IsAlert      = lowStock > 0
                }
            };

            // ── monthly sales chart (last 6 months) ───────────────────────
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
        }

        private static bool Eq(string? a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}