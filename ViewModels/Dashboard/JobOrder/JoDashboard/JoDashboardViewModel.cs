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
                    BadgeColor   = "AmberBrush",
                    TintColor    = "TintAmberBrush"
                },
                new KpiCardModel
                {
                    Icon         = "✅",
                    Title        = "Completed",
                    DisplayValue = completed.ToString("N0"),
                    BadgeColor   = "GreenBrush",
                    TintColor    = "TintGreenBrush"
                },
                new KpiCardModel
                {
                    Icon         = "⚠️",
                    Title        = "Overdue",
                    DisplayValue = overdue.ToString("N0"),
                    BadgeColor   = "RedBrush",
                    TintColor    = "TintRedBrush"
                }
            };

            // ── Status distribution ──────────────────────────────────
            var statusDefs = new[]
            {
                (Label: "Draft",         Key: StatusDraft,        BarColor: "#6B7280"),
                (Label: "Released",      Key: StatusReleased,     BarColor: "#3B82F6"),
                (Label: "In Production", Key: StatusInProduction, BarColor: "#F59E0B"),
                (Label: "Completed",     Key: StatusCompleted,    BarColor: "#22C55E"),
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
            var priorityDefs = new[]
            {
                (Label: "High",   Key: PriorityHigh,   BarColor: "#EF4444"),
                (Label: "Medium", Key: PriorityMedium, BarColor: "#F59E0B"),
                (Label: "Low",    Key: PriorityLow,    BarColor: "#22C55E"),
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

            string statusColor = isOverdue
                ? "RedBrush"
                : job.Status switch
                {
                    StatusDraft => "MutedForegroundBrush",
                    StatusReleased => "AccentBrush",
                    StatusInProduction => "AmberBrush",
                    StatusCompleted => "GreenBrush",
                    StatusDelivered => "PurpleBrush",
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