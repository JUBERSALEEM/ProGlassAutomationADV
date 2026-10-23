// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Dashboard/Optimization/OptimizationDashboardViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization Dashboard (Phase 28)
//
// Ninth dashboard page. Follows the established contract exactly:
//   • derives DashboardPageBase, so IsLoading / HasError / ErrorMessage /
//     LastRefreshed / the reentrancy guard are all inherited;
//   • overrides LoadCoreAsync and NEVER LoadAsync (already inside the guard);
//   • exposes ObservableCollection<KpiCardModel> + ChartBarModel +
//     ChartSliceModel — the shared models, not new ones;
//   • overrides OnThemeChanged to force WPF to rebuild the brush-key-driven
//     card templates on a Light/Dark swap (Phase 8.2f).
//
// SMOOTHNESS: no data access happens here at all. Aggregation is in
// OptimizationDashboardService, which projects six scalar columns and never
// materialises the JSON payload blobs. This ViewModel only formats.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Dashboard;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Optimization;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Dashboard
{
    /// <summary>
    /// Optimization dashboard page.
    /// KPIs : Saved Runs, Average Utilisation, Sheets Used, Unplaced Pieces.
    /// Chart: utilisation of the most recent runs — vertical bars.
    /// Donut: sheets consumed by engine stage over those same runs.
    /// </summary>
    public class OptimizationDashboardViewModel : DashboardPageBase
    {
        public override string TabTitle => "Optimization";
        public override string TabSubtitle => "Nesting yield and sheet usage";
        public override string TabIcon => "\uE9D2";

        // ── KPI cards ─────────────────────────────────────────────────────
        private ObservableCollection<KpiCardModel> _kpiCards = new();
        public ObservableCollection<KpiCardModel> KpiCards
        {
            get => _kpiCards;
            private set { if (_kpiCards == value) return; _kpiCards = value; OnPropertyChanged(); }
        }

        // ── recent-run utilisation bars ───────────────────────────────────
        private ObservableCollection<ChartBarModel> _runBars = new();
        public ObservableCollection<ChartBarModel> RunBars
        {
            get => _runBars;
            private set { if (_runBars == value) return; _runBars = value; OnPropertyChanged(); }
        }

        // ── sheets by engine stage ────────────────────────────────────────
        private ObservableCollection<ChartSliceModel> _stageSlices = new();
        public ObservableCollection<ChartSliceModel> StageSlices
        {
            get => _stageSlices;
            private set { if (_stageSlices == value) return; _stageSlices = value; OnPropertyChanged(); }
        }

        private bool _hasChartData;
        public bool HasChartData
        {
            get => _hasChartData;
            private set { if (_hasChartData == value) return; _hasChartData = value; OnPropertyChanged(); }
        }

        /// <summary>One line under the charts recording exactly which runs the
        /// charts describe, so a 12-run window is never read as "everything".</summary>
        private string _chartScope = string.Empty;
        public string ChartScope
        {
            get => _chartScope;
            private set { if (_chartScope == value) return; _chartScope = value; OnPropertyChanged(); }
        }

        // Industrial blue palette, matching SheetStoreDashboardViewModel after
        // its Chat 23 v2.1 alignment. Amber is reserved for physical volume.
        private static readonly string[] StageColors =
        {
            "#1D4ED8", "#0369A1", "#F59E0B",
            "#3B82F6", "#EC4899", "#8B5CF6"
        };

        // ═════════════════════════════════════════════════════════════════
        protected override async Task LoadCoreAsync()
        {
            // The base class turns anything thrown here into HasError +
            // ErrorMessage, so a missing optimization.db or a locked file
            // surfaces as an error state rather than as a page of confident
            // zeros. That distinction is the whole point of HasData below.
            var (kpis, recent) = await OptimizationDashboardService
                .GetSnapshotAsync()
                .ConfigureAwait(true);

            double avg = kpis.AverageUtilizationPct;

            KpiCards = new ObservableCollection<KpiCardModel>
            {
                new KpiCardModel
                {
                    Icon         = "\uE8B7",
                    Title        = "Saved Runs",
                    DisplayValue = kpis.SavedRuns.ToString("N0"),
                    BadgeColor   = "AccentBrush",
                    TintColor    = "TintIndigoBrush",
                    SubLabel     = kpis.LastRunUtc.HasValue
                                       ? $"Last run {kpis.LastRunUtc.Value.ToLocalTime():dd MMM yyyy, HH:mm}"
                                       : "No run saved yet"
                },
                new KpiCardModel
                {
                    // Utilisation is the yield metric the whole module exists
                    // for, so it carries the health semantic: green when the
                    // nest is tight, amber when there is obvious waste, red
                    // when the engine is leaving a third of the sheet behind.
                    Icon         = "\uE9D2",
                    Title        = "Average Utilisation",
                    DisplayValue = kpis.HasData ? $"{avg:N1}%" : "—",
                    BadgeColor   = avg >= 85 ? "SuccessBrush" : avg >= 70 ? "WarningBrush" : "DangerBrush",
                    TintColor    = avg >= 85 ? "TintGreenBrush" : avg >= 70 ? "TintAmberBrush" : "TintRedBrush",
                    SubLabel     = kpis.HasData ? $"Best single run {kpis.BestUtilizationPct:N1}%" : "Nothing to measure yet",
                    IsAlert      = kpis.HasData && avg < 70
                },
                new KpiCardModel
                {
                    // Physical volume metric — amber, warehouse semantic.
                    Icon         = "\uE7B8",
                    Title        = "Sheets Used",
                    DisplayValue = kpis.TotalSheetsUsed.ToString("N0"),
                    BadgeColor   = "WarningBrush",
                    TintColor    = "TintAmberBrush",
                    SubLabel     = $"{kpis.TotalPieces:N0} pieces nested"
                },
                new KpiCardModel
                {
                    // Unplaced pieces mean the nest did not fit — the one
                    // number here that should normally be zero.
                    Icon         = kpis.TotalUnplaced > 0 ? "\uE7BA" : "\uE73E",
                    Title        = "Unplaced Pieces",
                    DisplayValue = kpis.TotalUnplaced.ToString("N0"),
                    BadgeColor   = kpis.TotalUnplaced > 0 ? "DangerBrush" : "SuccessBrush",
                    TintColor    = kpis.TotalUnplaced > 0 ? "TintRedBrush" : "TintGreenBrush",
                    SubLabel     = kpis.TotalUnplaced > 0
                                       ? "Did not fit any stock sheet"
                                       : "Every piece was placed",
                    IsAlert      = kpis.TotalUnplaced > 0
                }
            };

            // ── utilisation bars, oldest → newest so the trend reads left to
            // right. The service already capped and ordered these; the
            // reversal here is presentation only.
            var ordered = recent.OrderBy(r => r.CreatedUtc).ToList();
            double maxUtil = ordered.Count > 0 ? ordered.Max(r => r.UtilizationPct) : 1.0;

            RunBars = new ObservableCollection<ChartBarModel>(
                ordered.Select((r, idx) => new ChartBarModel
                {
                    Label = ShortLabel(r.Name, idx),
                    RawValue = (decimal)r.UtilizationPct,
                    // A percentage is its own unit — show the real figure, and
                    // normalise against 100 rather than against the tallest
                    // bar, so a page of 92% runs does not draw one full-height
                    // bar and eight stubs and imply a spread that is not there.
                    ValueDisplay = $"{r.UtilizationPct:N1}%",
                    NormalizedHeight = Math.Clamp(r.UtilizationPct / 100.0, 0.04, 1.0),
                    BarColor = StageColors[idx % StageColors.Length],
                    IsHighlight = r.UtilizationPct >= maxUtil && maxUtil > 0
                }));

            // ── sheets by engine stage, over the same window. Grouped and
            // ordered by sheets so the largest stage is always the first
            // palette colour and the donut stops shifting between loads.
            var byStage = recent
                .GroupBy(r => string.IsNullOrWhiteSpace(r.EngineStage) ? "UNSET" : r.EngineStage.Trim())
                .Select(g => new { Stage = g.Key, Sheets = g.Sum(x => x.SheetsUsed) })
                .Where(x => x.Sheets > 0)
                .OrderByDescending(x => x.Sheets)
                .ToList();

            StageSlices = new ObservableCollection<ChartSliceModel>(
                byStage.Select((x, idx) => new ChartSliceModel
                {
                    Label = x.Stage,
                    RawValue = x.Sheets,
                    ValueDisplay = $"{x.Sheets:N0} sheets",
                    Accent = "",
                    SliceColor = StageColors[idx % StageColors.Length]
                }));

            HasChartData = ordered.Count > 0;
            ChartScope = ordered.Count switch
            {
                0 => "No saved optimisation runs yet.",
                1 => "One saved run.",
                _ when ordered.Count < kpis.SavedRuns
                    => $"Last {ordered.Count} of {kpis.SavedRuns:N0} saved runs.",
                _ => $"All {ordered.Count:N0} saved runs."
            };

            if (!kpis.HasData)
                AppLogService.Debug("[OptimizationDashboard] no saved runs; rendered empty state.");
        }

        /// <summary>
        /// Run names are user-entered and can be long; a bar label has room for
        /// about ten characters before it collides with its neighbour. Falls
        /// back to a stable positional label when the name is blank, so the
        /// axis never shows an empty tick.
        /// </summary>
        private static string ShortLabel(string? name, int index)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Run " + (index + 1);
            string t = name.Trim();
            return t.Length <= 10 ? t : t.Substring(0, 9) + "…";
        }

        // ═════════════════════════════════════════════════════════════════
        // PHASE 8.2f — THEME SWAP HANDLER
        // ═════════════════════════════════════════════════════════════════

        protected override void OnThemeChanged()
        {
            // Force WPF to rebuild the KPI card templates with the new theme
            // brushes. Slices and bars carry explicit hex colours, so only the
            // brush-key-driven cards need it.
            KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
        }
    }
}
