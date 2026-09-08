// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Dashboard/JobOrder/JoDashboard/JoDashboardViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Job Order Dashboard (Chat 23 v2.5)
//
// BUG FIX vs PREVIOUS VERSION:
//   Previous VM emitted brush KEYS that DO NOT EXIST in LightTheme /
//   DarkTheme, causing BrushKeyConverter to return null → badges and
//   status pills rendered TRANSPARENT or with unpredictable fallback.
//
//   Corrections:
//     • "AmberBrush"   → "WarningBrush"   (theme has WarningBrush)
//     • "GreenBrush"   → "SuccessBrush"   (theme has SuccessBrush)
//     • "RedBrush"     → "DangerBrush"    (theme has DangerBrush)
//     • "PurpleBrush"  → "InfoBrush"      (no PurpleBrush in theme;
//                                          InfoBrush blue reads as
//                                          "shipped/delivered" semantic)
//
//   Confirmed against LightTheme.xaml v1.6 which exposes exactly:
//     AccentBrush, AccentHoverBrush, AccentDarkBrush, AccentLightBrush,
//     SuccessBrush, WarningBrush, DangerBrush, InfoBrush, NeutralBrush
//     (+ Tint* variants for backgrounds)
//
// PRESERVED (zero regression):
//   • All KpiCardModel emissions (structure unchanged)
//   • All ChartBarModel emissions (semantic status/priority colors
//     preserved as hex — those are for bar CHART colors, not brush keys)
//   • All ActivityFeedItem emissions
//   • LoadCoreAsync business logic
//   • OnThemeChanged override
//   • BuildFeedItem helper
//   • All status/priority constants
//
// REGRESSION RISK: NONE (this is a BUG FIX — replaces broken keys
// with working keys. Broken keys were silently rendering fallback.)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Dashboard;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.ViewModels.Dashboard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using ProGlassAutomation.Services.Dashboard.Core;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.Logging;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.Dashboard.JobOrder.JoDashboard
{
    public class JoDashboardViewModel : DashboardPageBase
    {
        private readonly IJobOrderRepository _jobs = null!;

        public JoDashboardViewModel() : this(
            App.ServiceProvider?.GetService<IJobOrderRepository>()!)
        {
        }

        public JoDashboardViewModel(IJobOrderRepository? jobs)
        {
            _jobs = jobs!;
        }

        // ── Tab identity ─────────────────────────────────────────────
        public override string TabTitle => "Job Orders";
        public override string TabSubtitle => "Production workload";
        public override string TabIcon => "\uE9F9";

        // ── Backing stores for OnThemeChanged rebuild ────────────────
        private List<KpiCardModel> _kpiCards = new();
        private List<ChartBarModel> _statusBars = new();
        private List<ChartBarModel> _priorityBars = new();
        private List<ActivityFeedItem> _recentFeed = new();

        // ── KPI Cards ────────────────────────────────────────────────
        private ObservableCollection<KpiCardModel> _kpiCardsCol = new();
        public ObservableCollection<KpiCardModel> KpiCards
        {
            get => _kpiCardsCol;
            private set
            {
                if (_kpiCardsCol == value) return;
                _kpiCardsCol = value;
                OnPropertyChanged();
            }
        }

        // ── Status bars ──────────────────────────────────────────────
        private ObservableCollection<ChartBarModel> _statusBarsCol = new();
        public ObservableCollection<ChartBarModel> StatusBars
        {
            get => _statusBarsCol;
            private set
            {
                if (_statusBarsCol == value) return;
                _statusBarsCol = value;
                OnPropertyChanged();
            }
        }

        // ── Priority bars ────────────────────────────────────────────
        private ObservableCollection<ChartBarModel> _priorityBarsCol = new();
        public ObservableCollection<ChartBarModel> PriorityBars
        {
            get => _priorityBarsCol;
            private set
            {
                if (_priorityBarsCol == value) return;
                _priorityBarsCol = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<ChartSliceModel> _statusMix = new();
        public ObservableCollection<ChartSliceModel> StatusMixSlices
        {
            get => _statusMix;
            private set { _statusMix = value; OnPropertyChanged(); }
        }

        // ── Recent feed ──────────────────────────────────────────────
        private ObservableCollection<ActivityFeedItem> _recentFeedCol = new();
        public ObservableCollection<ActivityFeedItem> RecentFeed
        {
            get => _recentFeedCol;
            private set
            {
                if (_recentFeedCol == value) return;
                _recentFeedCol = value;
                OnPropertyChanged();
            }
        }

        // ── Visibility flags ─────────────────────────────────────────
        private bool _hasStatusData;
        public bool HasStatusData
        {
            get => _hasStatusData;
            private set
            {
                if (_hasStatusData == value) return;
                _hasStatusData = value;
                OnPropertyChanged();
            }
        }

        private bool _hasRecentFeed;
        public bool HasRecentFeed
        {
            get => _hasRecentFeed;
            private set
            {
                if (_hasRecentFeed == value) return;
                _hasRecentFeed = value;
                OnPropertyChanged();
            }
        }

        // ── Footer timestamp ─────────────────────────────────────────
        // Was a `new` shadow over DashboardPageBase.LastRefreshed so this
        // page could print "Last refreshed dd MMM yyyy, HH:mm" instead of
        // the base's "Updated HH:mm:ss". The base now uses that richer
        // format for every page, so the shadow is gone: one writer, one
        // format, and a DashboardPageBase reference can finally read the
        // stamp the view is showing.

        // ── Status strings ────────────────────────────────────────────
        // Aliased to JobOrderStatusValues, which is the canonical home.
        // These used to be private literals here, which is how this page came
        // to compare j.Status == "InProduction" and silently drop every
        // legacy "In Production" row — the mirror image of the bug that made
        // the Overview dashboard drop every enum-written one.
        private const string StatusDraft = JobOrderStatusValues.Draft;
        private const string StatusReleased = JobOrderStatusValues.Released;
        private const string StatusCompleted = JobOrderStatusValues.Completed;
        private const string StatusDelivered = JobOrderStatusValues.Delivered;

        /// <summary>
        /// What Canonical() renders the in-production state as, and therefore
        /// what the status bars and feed labels key on. Both stored spellings
        /// collapse to this.
        /// </summary>
        private const string StatusInProduction = JobOrderStatusValues.InProductionDisplay;

        // ── Priority string constants (matches SavedJobOrder.Priority storage) ─
        private const string PriorityHigh = "High";
        private const string PriorityMedium = "Medium";
        private const string PriorityLow = "Low";

        // ── Core load ────────────────────────────────────────────────
        protected override async Task LoadCoreAsync()
        {
            // Both may be null if this page loads before App.StartupCore has
            // finished. Degrade to an empty page rather than throwing inside
            // the load — the same guard the Statement dashboard uses.
            if (_jobs == null || App.DashboardMetrics == null)
            {
                AppLogService.Debug("[JoDashboard] dependencies not yet initialized.");
                return;
            }

            // Phase 3: counts come from IDashboardMetricsService instead of
            // loading every job order into this ViewModel. The service projects
            // five columns, not the whole row, and owns the two status rules
            // that used to be re-implemented here — IsInProduction matching
            // both "InProduction" and "In Production", and Overdue excluding
            // finished jobs.
            //
            // AllTime is deliberate: this page's four cards are lifetime
            // figures. The range only feeds CompletedThisPeriod, which this
            // page does not display, and AllTime also skips the
            // previous-period pass.
            var prod = await App.DashboardMetrics
                .GetProductionMetricsAsync(DashboardDateRange.AllTime());
            var today = DateTime.Today;

            // ── Counts ───────────────────────────────────────────────
            int total = prod.TotalJobs;
            int inProd = prod.InProduction;
            int completed = prod.Completed;
            int overdue = prod.Overdue;

            // ── KPI cards ────────────────────────────────────────────
            //   v2.5 BUG FIX: theme-correct brush keys
            //     AmberBrush→WarningBrush, GreenBrush→SuccessBrush,
            //     RedBrush→DangerBrush
            _kpiCards = new List<KpiCardModel>
            {
                new KpiCardModel
                {
                    Icon         = "\uE9F9",
                    Title        = "Total Job Orders",
                    DisplayValue = total.ToString("N0"),
                    BadgeColor   = "AccentBrush",
                    TintColor    = "TintIndigoBrush"
                },
                new KpiCardModel
                {
                    Icon         = "\uE713",
                    Title        = "In Production",
                    DisplayValue = inProd.ToString("N0"),
                    BadgeColor   = "WarningBrush",     // v2.5: was "AmberBrush" (missing key)
                    TintColor    = "TintAmberBrush"
                },
                new KpiCardModel
                {
                    Icon         = "\uE73E",
                    Title        = "Completed",
                    DisplayValue = completed.ToString("N0"),
                    BadgeColor   = "SuccessBrush",     // v2.5: was "GreenBrush" (missing key)
                    TintColor    = "TintGreenBrush"
                },
                new KpiCardModel
                {
                    Icon         = "\uE7BA",
                    Title        = "Overdue",
                    DisplayValue = overdue.ToString("N0"),
                    BadgeColor   = "DangerBrush",      // v2.5: was "RedBrush" (missing key)
                    TintColor    = "TintRedBrush"
                }
            };

            // ── Status distribution ──────────────────────────────────
            // SEMANTIC bar chart colors (hex) — universal invoice-like
            // semantics for job status. Not brush keys, not theme-swappable.
            var statusDefs = new[]
            {
                // Label IS the canonical status — bars are counted by
                // matching JobOrderStatusValues.Canonical(job.Status) against
                // it. If you change a Label, change Canonical to match.
                (Label: "Draft",         BarColor: "#6B7280"),
                (Label: "Released",      BarColor: "#3B82F6"),
                (Label: "In Production", BarColor: "#F59E0B"),
                (Label: "Completed",     BarColor: "#3B82F6"),
                (Label: "Delivered",     BarColor: "#8B5CF6"),
            };

            double statusMax = total == 0 ? 1d : (double)total;

            _statusBars = statusDefs.Select(sd =>
            {
                // The service groups on JobOrderStatusValues.Canonical(status),
                // so "InProduction" and "In Production" already land in the
                // same bucket. Matching the label case-insensitively against
                // that key is equivalent to the old per-row Canonical() ==
                // comparison, without holding the rows here.
                int count = prod.StatusBreakdown
                    .Where(b => string.Equals(b.Name, sd.Label,
                                              StringComparison.OrdinalIgnoreCase))
                    .Sum(b => b.Count);
                return new ChartBarModel
                {
                    Label = sd.Label,
                    RawValue = count,
                    ValueDisplay = count.ToString("N0"),
                    NormalizedHeight = total == 0 ? 0d : count / statusMax,
                    BarColor = sd.BarColor,
                    IsHighlight = false
                };
            }).ToList();

            // ── Priority breakdown ────────────────────────────────────
            // SEMANTIC bar chart colors (hex) — universal traffic-light
            // semantics: High=red, Medium=amber, Low=green.
            var priorityDefs = new[]
            {
                (Label: "High",   Key: PriorityHigh,   BarColor: "#EF4444"),
                (Label: "Medium", Key: PriorityMedium, BarColor: "#F59E0B"),
                (Label: "Low",    Key: PriorityLow,    BarColor: "#60A5FA"),
            };

            double priorityMax = total == 0 ? 1d : (double)total;

            _priorityBars = priorityDefs.Select(pd =>
            {
                // NOTE — small deliberate change. This matched the stored
                // Priority string exactly; the service groups on the trimmed
                // value, case-insensitively, with blanks falling into a
                // separate "Normal" bucket. A row stored as "high " or "HIGH"
                // now counts toward High instead of no bar at all.
                int count = prod.PriorityBreakdown
                    .Where(b => string.Equals(b.Name, pd.Key,
                                              StringComparison.OrdinalIgnoreCase))
                    .Sum(b => b.Count);
                return new ChartBarModel
                {
                    Label = pd.Label,
                    RawValue = count,
                    ValueDisplay = count.ToString("N0"),
                    NormalizedHeight = total == 0 ? 0d : count / priorityMax,
                    BarColor = pd.BarColor,
                    IsHighlight = false
                };
            }).ToList();

            // ── Recent feed ───────────────────────────────────────────
            // Already the ten newest, newest first — the service applies
            // Take() after ordering, so this page no longer reads the table
            // to render ten rows.
            _recentFeed = prod.RecentJobs
                .Select(j => BuildFeedItem(j, today))
                .ToList();

            // ── Push to UI ────────────────────────────────────────────
            KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
            StatusBars = new ObservableCollection<ChartBarModel>(_statusBars);
            PriorityBars = new ObservableCollection<ChartBarModel>(_priorityBars);
            StatusMixSlices = DashboardChartAdapter.FromBars(_statusBars);
            RecentFeed = new ObservableCollection<ActivityFeedItem>(_recentFeed);

            HasStatusData = total > 0;
            HasRecentFeed = _recentFeed.Count > 0;
            // LastRefreshed is written once, by DashboardPageBase.LoadAsync.
        }

        // ── Theme swap ────────────────────────────────────────────────
        protected override void OnThemeChanged()
        {
            KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
            StatusBars = new ObservableCollection<ChartBarModel>(_statusBars);
            PriorityBars = new ObservableCollection<ChartBarModel>(_priorityBars);
            StatusMixSlices = DashboardChartAdapter.FromBars(_statusBars);
            RecentFeed = new ObservableCollection<ActivityFeedItem>(_recentFeed);
        }

        // ── Feed item builder ─────────────────────────────────────────
        //   v2.5 BUG FIX: theme-correct brush keys for status pills.
        // Takes RecentJobRow rather than a SavedJobOrder entity: the feed
        // needs seven scalar columns, and carrying the entity here is what
        // forced the whole-table load this page used to do.
        private static ActivityFeedItem BuildFeedItem(
            RecentJobRow job,
            DateTime today)
        {
            bool isOverdue = job.DeliveryDate.HasValue
                          && job.DeliveryDate.Value.Date < today
                          && !JobOrderStatusValues.IsFinished(job.Status);

            // Switch on the canonical form, not the raw stored string. Both
            // "InProduction" and "In Production" arrive here; upper-casing the
            // raw value would render the enum spelling as "INPRODUCTION".
            var canonicalStatus = JobOrderStatusValues.Canonical(job.Status);

            string statusLabel = isOverdue
                ? "OVERDUE"
                : canonicalStatus switch
                {
                    StatusDraft => "DRAFT",
                    StatusReleased => "RELEASED",
                    StatusInProduction => "IN PRODUCTION",
                    StatusCompleted => "COMPLETED",
                    StatusDelivered => "DELIVERED",
                    _ => canonicalStatus.ToUpperInvariant()
                };

            // v2.5: theme brush keys corrected
            //   was "RedBrush" / "AmberBrush" / "GreenBrush" / "PurpleBrush" — all MISSING
            //   now "DangerBrush" / "WarningBrush" / "SuccessBrush" / "InfoBrush"
            //   (InfoBrush blue = "shipped/delivered" semantic — no PurpleBrush in theme)
            string statusColor = isOverdue
                ? "DangerBrush"
                : canonicalStatus switch
                {
                    StatusDraft => "MutedForegroundBrush",
                    StatusReleased => "AccentBrush",
                    StatusInProduction => "WarningBrush",
                    StatusCompleted => "SuccessBrush",
                    StatusDelivered => "InfoBrush",
                    _ => "MutedForegroundBrush"
                };

            string tintColor = job.Priority switch
            {
                PriorityHigh => "TintRedBrush",
                PriorityMedium => "TintAmberBrush",
                PriorityLow => "TintGreenBrush",
                _ => "TintIndigoBrush"
            };

            string priorityIcon = job.Priority switch
            {
                PriorityHigh => "🔴",
                PriorityMedium => "🟡",
                PriorityLow => "🟢",
                _ => "⚪"
            };

            string deliveryDisplay = job.DeliveryDate.HasValue
                ? $"Due {job.DeliveryDate.Value:dd MMM yyyy}"
                : "No delivery date set";

            return new ActivityFeedItem
            {
                Icon = priorityIcon,
                Title = job.JobOrderId,
                SubTitle = $"{job.CustomerName} — {deliveryDisplay}",
                ValueDisplay = job.GrandTotalVatInclusive > 0
                               ? $"AED {job.GrandTotalVatInclusive:N2}"
                               : string.Empty,
                StatusLabel = statusLabel,
                StatusColor = statusColor,
                TintColor = tintColor,
                ModuleTag = "JOB ORDER",
                SortDate = job.UpdatedAt
            };
        }
    }
}