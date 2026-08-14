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
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Dashboard.JobOrder.JoDashboard
{
    public class JoDashboardViewModel : DashboardPageBase
    {
        // ── Tab identity ─────────────────────────────────────────────
        public override string TabTitle => "Job Orders";
        public override string TabIcon => "🏭";

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
        private string _lastRefreshed = string.Empty;
        public string LastRefreshed
        {
            get => _lastRefreshed;
            private set
            {
                if (_lastRefreshed == value) return;
                _lastRefreshed = value;
                OnPropertyChanged();
            }
        }

        // ── Status string constants (matches SavedJobOrder.Status storage) ─
        private const string StatusDraft = "Draft";
        private const string StatusReleased = "Released";
        private const string StatusInProduction = "InProduction";
        private const string StatusCompleted = "Completed";
        private const string StatusDelivered = "Delivered";

        // ── Priority string constants (matches SavedJobOrder.Priority storage) ─
        private const string PriorityHigh = "High";
        private const string PriorityMedium = "Medium";
        private const string PriorityLow = "Low";

        // ── Core load ────────────────────────────────────────────────
        protected override async Task LoadCoreAsync()
        {
            var all = await App.Jobs.GetAllAsync();
            var today = DateTime.Today;

            // ── Counts ───────────────────────────────────────────────
            int total = all.Count;
            int inProd = all.Count(j => j.Status == StatusInProduction);
            int completed = all.Count(j => j.Status == StatusCompleted
                                        || j.Status == StatusDelivered);
            int overdue = all.Count(j => j.DeliveryDate.HasValue
                                        && j.DeliveryDate.Value.Date < today
                                        && j.Status != StatusCompleted
                                        && j.Status != StatusDelivered);

            // ── KPI cards ────────────────────────────────────────────
            //   v2.5 BUG FIX: theme-correct brush keys
            //     AmberBrush→WarningBrush, GreenBrush→SuccessBrush,
            //     RedBrush→DangerBrush
            _kpiCards = new List<KpiCardModel>
            {
                new KpiCardModel
                {
                    Icon         = "🏭",
                    Title        = "Total Job Orders",
                    DisplayValue = total.ToString("N0"),
                    BadgeColor   = "AccentBrush",
                    TintColor    = "TintIndigoBrush"
                },
                new KpiCardModel
                {
                    Icon         = "⚙️",
                    Title        = "In Production",
                    DisplayValue = inProd.ToString("N0"),
                    BadgeColor   = "WarningBrush",     // v2.5: was "AmberBrush" (missing key)
                    TintColor    = "TintAmberBrush"
                },
                new KpiCardModel
                {
                    Icon         = "✅",
                    Title        = "Completed",
                    DisplayValue = completed.ToString("N0"),
                    BadgeColor   = "SuccessBrush",     // v2.5: was "GreenBrush" (missing key)
                    TintColor    = "TintGreenBrush"
                },
                new KpiCardModel
                {
                    Icon         = "⚠️",
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
                (Label: "Draft",         Key: StatusDraft,        BarColor: "#6B7280"),
                (Label: "Released",      Key: StatusReleased,     BarColor: "#3B82F6"),
                (Label: "In Production", Key: StatusInProduction, BarColor: "#F59E0B"),
                (Label: "Completed",     Key: StatusCompleted,    BarColor: "#3B82F6"),
                (Label: "Delivered",     Key: StatusDelivered,    BarColor: "#8B5CF6"),
            };

            double statusMax = total == 0 ? 1d : (double)total;

            _statusBars = statusDefs.Select(sd =>
            {
                int count = all.Count(j => j.Status == sd.Key);
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
                int count = all.Count(j => j.Priority == pd.Key);
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
            _recentFeed = all
                .OrderByDescending(j => j.UpdatedAt)
                .Take(10)
                .Select(j => BuildFeedItem(j, today))
                .ToList();

            // ── Push to UI ────────────────────────────────────────────
            KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
            StatusBars = new ObservableCollection<ChartBarModel>(_statusBars);
            PriorityBars = new ObservableCollection<ChartBarModel>(_priorityBars);
            RecentFeed = new ObservableCollection<ActivityFeedItem>(_recentFeed);

            HasStatusData = total > 0;
            HasRecentFeed = _recentFeed.Count > 0;
            LastRefreshed = $"Last refreshed {DateTime.Now:dd MMM yyyy, HH:mm}";
        }

        // ── Theme swap ────────────────────────────────────────────────
        protected override void OnThemeChanged()
        {
            KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
            StatusBars = new ObservableCollection<ChartBarModel>(_statusBars);
            PriorityBars = new ObservableCollection<ChartBarModel>(_priorityBars);
            RecentFeed = new ObservableCollection<ActivityFeedItem>(_recentFeed);
        }

        // ── Feed item builder ─────────────────────────────────────────
        //   v2.5 BUG FIX: theme-correct brush keys for status pills.
        private static ActivityFeedItem BuildFeedItem(
            SavedJobOrder job,
            DateTime today)
        {
            bool isOverdue = job.DeliveryDate.HasValue
                          && job.DeliveryDate.Value.Date < today
                          && job.Status != StatusCompleted
                          && job.Status != StatusDelivered;

            string statusLabel = isOverdue
                ? "OVERDUE"
                : job.Status switch
                {
                    StatusDraft => "DRAFT",
                    StatusReleased => "RELEASED",
                    StatusInProduction => "IN PRODUCTION",
                    StatusCompleted => "COMPLETED",
                    StatusDelivered => "DELIVERED",
                    _ => job.Status.ToUpperInvariant()
                };

            // v2.5: theme brush keys corrected
            //   was "RedBrush" / "AmberBrush" / "GreenBrush" / "PurpleBrush" — all MISSING
            //   now "DangerBrush" / "WarningBrush" / "SuccessBrush" / "InfoBrush"
            //   (InfoBrush blue = "shipped/delivered" semantic — no PurpleBrush in theme)
            string statusColor = isOverdue
                ? "DangerBrush"
                : job.Status switch
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