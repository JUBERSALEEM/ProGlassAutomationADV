using ProGlassAutomation.Models.Dashboard;
using ProGlassAutomation.Models.TaxInvoice;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Dashboard.TaxInvoice
{
    public class TaxInvoiceDashboardViewModel : DashboardPageBase
    {
        public override string TabTitle => "Invoices";
        public override string TabIcon => "🧾";

        private List<KpiCardModel> _kpiCards = new();
        private List<ChartBarModel> _monthlySalesBars = new();
        private List<ActivityFeedItem> _recentFeed = new();

        private ObservableCollection<KpiCardModel> _kpiCardsCol = new();
        public ObservableCollection<KpiCardModel> KpiCards
        {
            get => _kpiCardsCol;
            private set { if (_kpiCardsCol == value) return; _kpiCardsCol = value; OnPropertyChanged(); }
        }

        private ObservableCollection<ChartBarModel> _monthlySalesBarsCol = new();
        public ObservableCollection<ChartBarModel> MonthlySalesBars
        {
            get => _monthlySalesBarsCol;
            private set { if (_monthlySalesBarsCol == value) return; _monthlySalesBarsCol = value; OnPropertyChanged(); }
        }

        private ObservableCollection<ActivityFeedItem> _recentFeedCol = new();
        public ObservableCollection<ActivityFeedItem> RecentFeed
        {
            get => _recentFeedCol;
            private set { if (_recentFeedCol == value) return; _recentFeedCol = value; OnPropertyChanged(); }
        }

        private bool _hasChartData;
        public bool HasChartData
        {
            get => _hasChartData;
            private set { if (_hasChartData == value) return; _hasChartData = value; OnPropertyChanged(); }
        }

        private bool _hasRecentFeed;
        public bool HasRecentFeed
        {
            get => _hasRecentFeed;
            private set { if (_hasRecentFeed == value) return; _hasRecentFeed = value; OnPropertyChanged(); }
        }

        protected override async Task LoadCoreAsync()
        {
            if (App.TaxInvoices == null)
            {
                KpiCards = new ObservableCollection<KpiCardModel>();
                MonthlySalesBars = new ObservableCollection<ChartBarModel>();
                RecentFeed = new ObservableCollection<ActivityFeedItem>();
                HasChartData = false;
                HasRecentFeed = false;
                return;
            }

            var all = await App.TaxInvoices.GetAllAsync();
            var today = DateTime.Today;

            int totalInvoices = all.Count;
            int paidInvoices = all.Count(i => string.Equals(i.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase));
            int pendingInvoices = all.Count(i => !string.Equals(i.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase));

            // Compute total amount in base currency (AED)
            // (Subtotal * ExchangeRate) or GrandTotal in AED.
            // Since entity.GrandTotal is saved in the chosen currency, we multiply by ExchangeRate to get AED!
            decimal totalAmountAed = all.Sum(i => i.GrandTotal * i.ExchangeRate);

            _kpiCards = new List<KpiCardModel>
            {
                new KpiCardModel
                {
                    Icon         = "🧾",
                    Title        = "Total Invoices",
                    DisplayValue = totalInvoices.ToString("N0"),
                    BadgeColor   = "AccentBrush",
                    TintColor    = "TintIndigoBrush"
                },
                new KpiCardModel
                {
                    Icon         = "✅",
                    Title        = "Paid Invoices",
                    DisplayValue = paidInvoices.ToString("N0"),
                    BadgeColor   = "GreenBrush",
                    TintColor    = "TintGreenBrush"
                },
                new KpiCardModel
                {
                    Icon         = "⏳",
                    Title        = "Pending Invoices",
                    DisplayValue = pendingInvoices.ToString("N0"),
                    BadgeColor   = "AmberBrush",
                    TintColor    = "TintAmberBrush"
                },
                new KpiCardModel
                {
                    Icon         = "💰",
                    Title        = "Total Billed Amount",
                    DisplayValue = $"AED {totalAmountAed:N2}",
                    BadgeColor   = "GreenBrush",
                    TintColor    = "TintGreenBrush"
                }
            };

            // ── Chart Sales (last 6 months) ──────────────────────────────
            var now = DateTime.Now;
            var months = Enumerable.Range(0, 6)
                .Select(i => now.AddMonths(-5 + i))
                .ToList();

            var rawBars = months.Select(m =>
            {
                decimal total = all
                    .Where(inv => inv.InvoiceDate.Year == m.Year && inv.InvoiceDate.Month == m.Month)
                    .Sum(inv => inv.GrandTotal * inv.ExchangeRate);

                return new ChartBarModel
                {
                    Label = m.ToString("MMM"),
                    RawValue = total,
                    ValueDisplay = total > 0 ? $"AED {total:N2}" : "—",
                    BarColor = "#6366F1"
                };
            }).ToList();

            decimal maxVal = rawBars.Any() ? rawBars.Max(b => b.RawValue) : 0m;
            bool anyData = maxVal > 0;

            foreach (var bar in rawBars)
            {
                bar.NormalizedHeight = anyData ? (double)(bar.RawValue / maxVal) : 0.04;
                bar.IsHighlight = anyData && bar.RawValue == maxVal;
                bar.BarColor = bar.IsHighlight ? "#10B981" : "#6366F1";
            }

            _monthlySalesBars = rawBars;

            // ── Recent Invoices ──────────────────────────────────────────
            _recentFeed = all
                .OrderByDescending(i => i.UpdatedAt != default ? i.UpdatedAt : i.CreatedAt)
                .Take(10)
                .Select(BuildFeedItem)
                .ToList();

            // ── Push to UI ────────────────────────────────────────────
            KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
            MonthlySalesBars = new ObservableCollection<ChartBarModel>(_monthlySalesBars);
            RecentFeed = new ObservableCollection<ActivityFeedItem>(_recentFeed);

            HasChartData = anyData;
            HasRecentFeed = _recentFeed.Count > 0;
        }

        protected override void OnThemeChanged()
        {
            KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
            MonthlySalesBars = new ObservableCollection<ChartBarModel>(_monthlySalesBars);
            RecentFeed = new ObservableCollection<ActivityFeedItem>(_recentFeed);
        }

        private static ActivityFeedItem BuildFeedItem(Models.TaxInvoice.TaxInvoice inv)
        {
            string statusLabel = inv.PaymentStatus.ToUpperInvariant();
            string statusColor = inv.PaymentStatus.ToLowerInvariant() switch
            {
                "paid" => "GreenBrush",
                "partially paid" => "AmberBrush",
                _ => "MutedForegroundBrush"
            };

            string tintColor = inv.PaymentStatus.ToLowerInvariant() switch
            {
                "paid" => "TintGreenBrush",
                "partially paid" => "TintAmberBrush",
                _ => "TintGrayBrush"
            };

            return new ActivityFeedItem
            {
                Icon = "🧾",
                Title = inv.InvoiceNumber,
                SubTitle = $"{inv.CustomerName} — DN: {inv.DeliveryNoteId}",
                ValueDisplay = $"{inv.Currency} {inv.GrandTotal:N2}",
                StatusLabel = statusLabel,
                StatusColor = statusColor,
                TintColor = tintColor,
                ModuleTag = "TAX INVOICE",
                SortDate = inv.UpdatedAt != default ? inv.UpdatedAt : inv.CreatedAt
            };
        }
    }
}