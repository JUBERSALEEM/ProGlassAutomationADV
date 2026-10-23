// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Dashboard/Production/ProductionDashboardViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Production Dashboard (Phase 28)
//
// Ninth dashboard page. Same contract as the other eight — see
// OptimizationDashboardViewModel for the rules spelled out.
//
// DATA SOURCE: Services/Production/Control/ProductionDashboardService, which
// already existed and was already in production use by
// ProductionControlCenterViewModel and ProductionControlReportService. This
// page adds a second consumer of a proven feed rather than a new query path,
// so its numbers cannot drift from the Control Center's.
//
// SMOOTHNESS: the three service calls are deliberately SEQUENTIAL, not
// Task.WhenAll. Each one independently calls ProductionQueueService
// .GetQueueAsync (and two call ProductionRepository.EnsureInitialized), so
// running them concurrently would triple the queue build for no benefit and
// would race a static initializer that was never written to be re-entered.
// The page shows its skeleton state while they run, which is what the base
// class's IsLoading is for.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Dashboard;
using ProGlassAutomation.Models.Production.Control;
using ProGlassAutomation.Services.Production.Control;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Dashboard
{
    /// <summary>
    /// Production dashboard page.
    /// KPIs : Jobs in Production, Completed Today, Overdue / Due Soon, QC &amp; Rework.
    /// Chart: queue pressure per process — vertical bars.
    /// Donut: readiness mix across the live queue.
    /// </summary>
    public class ProductionDashboardViewModel : DashboardPageBase
    {
        public override string TabTitle => "Production";
        public override string TabSubtitle => "Floor load, bottlenecks and QC backlog";
        public override string TabIcon => "\uE8C6";

        // ── KPI cards ─────────────────────────────────────────────────────
        private ObservableCollection<KpiCardModel> _kpiCards = new();
        public ObservableCollection<KpiCardModel> KpiCards
        {
            get => _kpiCards;
            private set { if (_kpiCards == value) return; _kpiCards = value; OnPropertyChanged(); }
        }

        // ── queue pressure per process ────────────────────────────────────
        private ObservableCollection<ChartBarModel> _pressureBars = new();
        public ObservableCollection<ChartBarModel> PressureBars
        {
            get => _pressureBars;
            private set { if (_pressureBars == value) return; _pressureBars = value; OnPropertyChanged(); }
        }

        // ── readiness mix ─────────────────────────────────────────────────
        private ObservableCollection<ChartSliceModel> _readinessSlices = new();
        public ObservableCollection<ChartSliceModel> ReadinessSlices
        {
            get => _readinessSlices;
            private set { if (_readinessSlices == value) return; _readinessSlices = value; OnPropertyChanged(); }
        }

        private bool _hasChartData;
        public bool HasChartData
        {
            get => _hasChartData;
            private set { if (_hasChartData == value) return; _hasChartData = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// One line naming the detected bottleneck. Empty when nothing is
        /// queued, so the page never invents a bottleneck to fill the space.
        /// </summary>
        private string _bottleneckNote = string.Empty;
        public string BottleneckNote
        {
            get => _bottleneckNote;
            private set { if (_bottleneckNote == value) return; _bottleneckNote = value; OnPropertyChanged(); }
        }

        // Industrial blue palette, consistent with the other module pages.
        private static readonly string[] ProcessColors =
        {
            "#1D4ED8", "#0369A1", "#F59E0B",
            "#3B82F6", "#EC4899", "#8B5CF6"
        };

        // ═════════════════════════════════════════════════════════════════
        protected override async Task LoadCoreAsync()
        {
            ProductionDashboardKpis kpis = await ProductionDashboardService
                .GetKpisAsync()
                .ConfigureAwait(true);

            List<ProductionQueuePressure> pressure = await ProductionDashboardService
                .GetQueuePressureAsync()
                .ConfigureAwait(true);

            int qcBacklog = kpis.QcPending + kpis.QcFailed;

            KpiCards = new ObservableCollection<KpiCardModel>
            {
                new KpiCardModel
                {
                    Icon         = "\uE8C6",
                    Title        = "Jobs in Production",
                    DisplayValue = kpis.JobsInProduction.ToString("N0"),
                    BadgeColor   = "AccentBrush",
                    TintColor    = "TintIndigoBrush",
                    SubLabel     = $"{kpis.ReadyToStart} ready · {kpis.InProgress} in progress"
                },
                new KpiCardModel
                {
                    Icon         = "\uE73E",
                    Title        = "Completed Today",
                    DisplayValue = kpis.CompletedToday.ToString("N0"),
                    BadgeColor   = "SuccessBrush",
                    TintColor    = "TintGreenBrush",
                    SubLabel     = $"{kpis.ReadyForDelivery} ready for delivery"
                },
                new KpiCardModel
                {
                    // Due-date risk is the card a supervisor actually acts on,
                    // so overdue drives the alert ring rather than the softer
                    // due-soon figure.
                    Icon         = kpis.OverdueJobs > 0 ? "\uE7BA" : "\uE823",
                    Title        = "Overdue / Due Soon",
                    DisplayValue = $"{kpis.OverdueJobs} / {kpis.DueSoon}",
                    BadgeColor   = kpis.OverdueJobs > 0 ? "DangerBrush"
                                   : kpis.DueSoon > 0 ? "WarningBrush" : "SuccessBrush",
                    TintColor    = kpis.OverdueJobs > 0 ? "TintRedBrush"
                                   : kpis.DueSoon > 0 ? "TintAmberBrush" : "TintGreenBrush",
                    SubLabel     = kpis.MaterialShortages > 0
                                       ? $"{kpis.MaterialShortages} waiting on material"
                                       : kpis.OverdueJobs > 0 ? "Needs attention now" : "Schedule is holding",
                    IsAlert      = kpis.OverdueJobs > 0
                },
                new KpiCardModel
                {
                    // A FAILED QC entry is a defect; a PENDING one is just a
                    // queue. Only the failure side raises the alert, otherwise
                    // a normal busy day reads as a problem.
                    Icon         = kpis.QcFailed > 0 ? "\uE7BA" : "\uE73E",
                    Title        = "QC & Rework",
                    DisplayValue = $"{qcBacklog} / {kpis.Rework}",
                    BadgeColor   = kpis.QcFailed > 0 ? "DangerBrush"
                                   : qcBacklog > 0 ? "WarningBrush" : "SuccessBrush",
                    TintColor    = kpis.QcFailed > 0 ? "TintRedBrush"
                                   : qcBacklog > 0 ? "TintAmberBrush" : "TintGreenBrush",
                    SubLabel     = kpis.QcFailed > 0
                                       ? $"{kpis.QcFailed} failed inspection"
                                       : $"{kpis.QcPending} awaiting inspection",
                    IsAlert      = kpis.QcFailed > 0
                }
            };

            // ── queue pressure bars. The service already sorted by Total
            // descending and Total is ready + blocked, i.e. the work actually
            // waiting on that process — not everything that ever passed
            // through it.
            int maxTotal = pressure.Count > 0 ? pressure.Max(p => p.Total) : 0;

            PressureBars = new ObservableCollection<ChartBarModel>(
                pressure.Select((p, idx) => new ChartBarModel
                {
                    Label = p.ProcessName,
                    RawValue = p.Total,
                    ValueDisplay = p.Total.ToString("N0"),
                    NormalizedHeight = maxTotal > 0
                                           ? (double)p.Total / maxTotal
                                           : 0.04,
                    BarColor = ProcessColors[idx % ProcessColors.Length],
                    IsHighlight = idx == 0 && p.Total > 0
                }));

            // ── readiness mix. Zeros are filtered out rather than drawn: a
            // donut slice with no value still takes an arc and a legend row,
            // which is how a quiet floor ends up looking like a busy one.
            var mix = new (string Label, int Count, string SliceColor)[]
            {
                ("Ready",       kpis.ReadyToStart, "#1D4ED8"),
                ("In Progress", kpis.InProgress,   "#0369A1"),
                ("QC Pending",  kpis.QcPending,    "#3B82F6"),
                ("Rework",      kpis.Rework,       "#F59E0B"),
                ("On Hold",     kpis.OnHold,       "#8B5CF6"),
                ("Blocked",     kpis.Blocked,      "#DC2626")
            };

            ReadinessSlices = new ObservableCollection<ChartSliceModel>(
                mix.Where(m => m.Count > 0)
                   .Select(m => new ChartSliceModel
                   {
                       Label = m.Label,
                       RawValue = m.Count,
                       ValueDisplay = m.Count.ToString("N0"),
                       Accent = "",
                       SliceColor = m.SliceColor
                   }));

            // Drives the BAR chart's well only. The donut is not gated on it:
            // DonutChartControl has its own EmptyMessage, so a floor with work
            // queued but no per-process pressure still renders a correct
            // readiness ring instead of hiding behind the bar chart's flag.
            HasChartData = pressure.Count > 0;

            // The bottleneck comes from the service's own deterministic rule
            // (largest READY queue). Reporting it only when the service found
            // one keeps this page from contradicting the Control Center.
            BottleneckNote = string.IsNullOrWhiteSpace(kpis.BottleneckProcess) || kpis.BottleneckCount <= 0
                ? string.Empty
                : $"Bottleneck: {kpis.BottleneckProcess} — {kpis.BottleneckCount} jobs waiting to start.";
        }

        // ═════════════════════════════════════════════════════════════════
        // PHASE 8.2f — THEME SWAP HANDLER
        // ═════════════════════════════════════════════════════════════════

        protected override void OnThemeChanged()
        {
            KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
        }
    }
}
