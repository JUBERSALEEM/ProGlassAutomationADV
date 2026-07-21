using ProGlassAutomation.Models.Dashboard;
using ProGlassAutomation.Services.SheetStore;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Dashboard
{
    /// <summary>
    /// Sheet Store dashboard page.
    /// KPIs : Sheet Types, Inventory Value, Available SQM, Low/Out-of-Stock count.
    /// Chart: Available SQM per category — vertical bars.
    /// Phase 8.2a: Cards now use TintColor for subtle tinted backgrounds.
    /// Phase 8.2e: BadgeColor and TintColor use theme brush KEY names.
    /// Phase 8.2f: Overrides OnThemeChanged() to force re-notify KpiCards
    ///             so WPF rebuilds card templates instantly on theme swap.
    /// </summary>
    public class SheetStoreDashboardViewModel : DashboardPageBase
    {
        public override string TabTitle => "Sheet Store";
        public override string TabIcon => "📦";

        // ── KPI cards ─────────────────────────────────────────────────────
        private ObservableCollection<KpiCardModel> _kpiCards = new();
        public ObservableCollection<KpiCardModel> KpiCards
        {
            get => _kpiCards;
            private set { if (_kpiCards == value) return; _kpiCards = value; OnPropertyChanged(); }
        }

        // ── category bars ─────────────────────────────────────────────────
        private ObservableCollection<ChartBarModel> _categoryBars = new();
        public ObservableCollection<ChartBarModel> CategoryBars
        {
            get => _categoryBars;
            private set
            {
                if (_categoryBars == value) return;
                _categoryBars = value;
                OnPropertyChanged();
            }
        }

        private bool _hasChartData;
        public bool HasChartData
        {
            get => _hasChartData;
            private set { if (_hasChartData == value) return; _hasChartData = value; OnPropertyChanged(); }
        }

        private readonly ISheetStoreRepository _sheetRepo = new SheetStoreRepository();

        private static readonly string[] CategoryColors =
        {
            "#6366F1", "#10B981", "#F59E0B",
            "#3B82F6", "#EC4899", "#8B5CF6"
        };

        // ═════════════════════════════════════════════════════════════════
        protected override async Task LoadCoreAsync()
        {
            var sheets = await _sheetRepo.GetAllAsync();

            int sheetTypes = sheets.Count;
            decimal totalValue = sheets.Sum(s => s.BalanceValue);
            decimal totalSqm = sheets.Sum(s => s.TotalSqmBalance);
            int lowStockCount = sheets.Count(s => s.Balance <= 5);
            int outOfStock = sheets.Count(s => s.Balance == 0);
            int categories = sheets.Select(s => s.Category).Distinct().Count();

            // ── KPI cards (theme brush keys) ──────────────────────────────
            KpiCards = new ObservableCollection<KpiCardModel>
            {
                new KpiCardModel
                {
                    Icon         = "🗂️",
                    Title        = "Sheet Types",
                    DisplayValue = sheetTypes.ToString("N0"),
                    BadgeColor   = "AccentBrush",
                    TintColor    = "TintIndigoBrush",
                    SubLabel     = $"{categories} categories in store"
                },
                new KpiCardModel
                {
                    Icon         = "💎",
                    Title        = "Inventory Value",
                    DisplayValue = $"AED {totalValue:N0}",
                    BadgeColor   = "SuccessBrush",
                    TintColor    = "TintGreenBrush",
                    SubLabel     = "Balance sheets at sell price"
                },
                new KpiCardModel
                {
                    Icon         = "📐",
                    Title        = "Available SQM",
                    DisplayValue = $"{totalSqm:N1} m²",
                    BadgeColor   = "WarningBrush",
                    TintColor    = "TintAmberBrush",
                    SubLabel     = "Total balance across all types"
                },
                new KpiCardModel
                {
                    Icon         = lowStockCount > 0 ? "⚠️" : "✅",
                    Title        = "Low / Out of Stock",
                    DisplayValue = $"{lowStockCount} / {outOfStock}",
                    BadgeColor   = lowStockCount > 0 ? "DangerBrush" : "SuccessBrush",
                    TintColor    = lowStockCount > 0 ? "TintRedBrush" : "TintGreenBrush",
                    SubLabel     = lowStockCount > 0
                                       ? $"{outOfStock} out · {lowStockCount} critical"
                                       : "All stock levels healthy",
                    IsAlert      = lowStockCount > 0
                }
            };

            // ── category bars ─────────────────────────────────────────────
            var grouped = sheets
                .GroupBy(s => s.Category)
                .Select((g, idx) => new
                {
                    Category = g.Key,
                    SqmBalance = g.Sum(s => s.TotalSqmBalance),
                    ColorIndex = idx % CategoryColors.Length
                })
                .OrderByDescending(g => g.SqmBalance)
                .ToList();

            decimal maxSqm = grouped.Any() ? grouped.Max(g => g.SqmBalance) : 1m;

            CategoryBars = new ObservableCollection<ChartBarModel>(
                grouped.Select(g => new ChartBarModel
                {
                    Label = g.Category,
                    RawValue = g.SqmBalance,
                    ValueDisplay = $"{g.SqmBalance:N1} m²",
                    NormalizedHeight = maxSqm > 0
                                           ? (double)(g.SqmBalance / maxSqm)
                                           : 0.04,
                    BarColor = CategoryColors[g.ColorIndex],
                    IsHighlight = g.SqmBalance == maxSqm
                }));

            HasChartData = grouped.Any();
        }

        // ═════════════════════════════════════════════════════════════════
        // PHASE 8.2f — THEME SWAP HANDLER
        // ═════════════════════════════════════════════════════════════════

        protected override void OnThemeChanged()
        {
            // Force WPF to rebuild KPI card templates with new theme brushes
            KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
        }
    }
}