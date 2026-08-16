// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Dashboard/SheetStoreDashboardViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Sheet Store Dashboard (Chat 23 v2.1)
//
// CHANGES vs PREVIOUS VERSION:
//   Color palette aligned with industrial blue theme:
//     • CategoryColors: "#10B981" (green) → "#0369A1" (teal-blue)
//     • Inventory Value card: SuccessBrush/TintGreenBrush → AccentBrush/TintIndigoBrush
//       (financial informational metric → blue-family, not green)
//   Low/Out-of-Stock card KEEPS semantic Success/Danger (health status).
//
// PRESERVED (zero regression):
//   • All KpiCardModel emissions (Icon/Title/DisplayValue/BadgeColor/
//     TintColor/SubLabel/IsAlert)
//   • All ChartBarModel emissions
//   • All LoadCoreAsync logic
//   • OnThemeChanged override
//   • All phase comments (8.2a, 8.2e, 8.2f)
//
// REGRESSION RISK: LOW
//   Only color hex/brush-key values changed. Zero logic touched.
// ═══════════════════════════════════════════════════════════════════════

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
    /// Chat 23 v2.1: Industrial blue palette alignment.
    ///     • CategoryColors green slot → teal-blue.
    ///     • Inventory Value card colors green → blue (financial info metric).
    ///     • Low/Out-of-Stock KEEPS semantic green/red (health status).
    /// </summary>
    public class SheetStoreDashboardViewModel : DashboardPageBase
    {
        public override string TabTitle => "Sheet Store";
        public override string TabIcon => "\uE7B8";

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

        // Chat 23 v2.1: green slot replaced with teal-blue for industrial palette.
        //   Old: "#6366F1", "#10B981" (green), "#F59E0B", "#3B82F6", "#EC4899", "#8B5CF6"
        //   New: "#1D4ED8", "#0369A1" (teal-blue), "#F59E0B", "#3B82F6", "#EC4899", "#8B5CF6"
        // Amber kept (physical volume semantic). Pink + Violet kept (visual
        // distinctness across 6+ categories).
        private static readonly string[] CategoryColors =
        {
            "#1D4ED8", "#0369A1", "#F59E0B",
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
                    Icon         = "\uE8B7",
                    Title        = "Sheet Types",
                    DisplayValue = sheetTypes.ToString("N0"),
                    BadgeColor   = "AccentBrush",
                    TintColor    = "TintIndigoBrush",
                    SubLabel     = $"{categories} categories in store"
                },
                new KpiCardModel
                {
                    // Chat 23 v2.1: Inventory Value = financial informational metric.
                    // Was SuccessBrush/TintGreenBrush (green). Now AccentBrush/
                    // TintIndigoBrush (industrial blue). Green reserved for
                    // health/success semantic states (see Low/Out card below).
                    Icon         = "\uE734",
                    Title        = "Inventory Value",
                    DisplayValue = $"AED {totalValue:N0}",
                    BadgeColor   = "AccentBrush",
                    TintColor    = "TintIndigoBrush",
                    SubLabel     = "Balance sheets at sell price"
                },
                new KpiCardModel
                {
                    // Amber preserved — physical volume metric, warehouse semantic.
                    Icon         = "\uE799",
                    Title        = "Available SQM",
                    DisplayValue = $"{totalSqm:N1} m²",
                    BadgeColor   = "WarningBrush",
                    TintColor    = "TintAmberBrush",
                    SubLabel     = "Total balance across all types"
                },
                new KpiCardModel
                {
                    // Low/Out KEEPS semantic green (healthy) / red (alert).
                    // This is a health-status metric, not a brand-color slot.
                    Icon         = lowStockCount > 0 ? "\uE7BA" : "\uE73E",
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