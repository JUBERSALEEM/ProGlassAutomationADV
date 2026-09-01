// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Dashboard/DeliveryNote/DnDashboard/DnDashboardViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Delivery Note Dashboard (Chat 23 v2.5)
//
// BUG FIX vs PREVIOUS VERSION:
//   Previous VM emitted brush KEYS that DO NOT EXIST in LightTheme /
//   DarkTheme (v1.6), causing BrushKeyConverter to return null →
//   badges and status pills rendered TRANSPARENT.
//
//   Corrections:
//     • "AmberBrush"   → "WarningBrush"   (theme has WarningBrush)
//     • "GreenBrush"   → "SuccessBrush"   (theme has SuccessBrush)
//     • "RedBrush"     → "DangerBrush"    (theme has DangerBrush)
//     • "PurpleBrush"  → "InfoBrush"      (no PurpleBrush in theme;
//                                          InfoBrush blue = "received"
//                                          semantic — matches JoDashboard
//                                          v2.5 mapping for Delivered)
//
// PRESERVED (zero regression):
//   • ALL logic, GOTCHA #40 fix, business rules
//   • ALL bar chart hex colors (semantic — status/type distribution)
//   • ALL structure and comments
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Dashboard;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.ViewModels.Dashboard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using ProGlassAutomation.Services.DeliveryNote.Repository;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.Dashboard.DeliveryNote.DnDashboard
{
    /// <summary>
    /// Delivery Notes sub-dashboard.
    ///
    /// Mirrors the JO dashboard pattern:
    ///   - 4 KPI cards (Total / Delivered Today / Pending Balance / Rework+Returned)
    ///   - Status distribution bar chart (Draft / Dispatched / Delivered / Signed / Received)
    ///   - Type distribution bar chart (Original / Dummy / Rework / Return)
    ///   - Top 10 recent DNs feed (color-tinted by status)
    ///
    /// Data source: _deliveryNotes.GetAllAsync() + GetPendingQtyJobsAsync()
    ///
    /// GOTCHA #40 (CRITICAL): Never sum TotalBalanceQty across DNs!
    /// Each DN stores a point-in-time snapshot — summing double-counts.
    /// Use GetPendingQtyJobsAsync() which correctly computes per-JO balance.
    ///
    /// Chat 23 v2.5: theme brush keys corrected (AmberBrush→WarningBrush,
    /// GreenBrush→SuccessBrush, RedBrush→DangerBrush, PurpleBrush→InfoBrush).
    /// </summary>
    public class DnDashboardViewModel : DashboardPageBase
    {
        private readonly IDeliveryNoteRepository _deliveryNotes = null!;

        public DnDashboardViewModel() : this(
            App.ServiceProvider?.GetService<IDeliveryNoteRepository>()!)
        {
        }

        public DnDashboardViewModel(IDeliveryNoteRepository? deliveryNotes)
        {
            _deliveryNotes = deliveryNotes!;
        }

        // ── Tab identity ─────────────────────────────────────────────
        public override string TabTitle => "Deliveries";
        public override string TabSubtitle => "Dispatch and balance";
        public override string TabIcon => "\uE806";

        // ── Backing stores for OnThemeChanged rebuild ────────────────
        private List<KpiCardModel> _kpiCards = new();
        private List<ChartBarModel> _statusBars = new();
        private List<ChartBarModel> _typeBars = new();
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

        // ── Type bars ────────────────────────────────────────────────
        private ObservableCollection<ChartBarModel> _typeBarsCol = new();
        public ObservableCollection<ChartBarModel> TypeBars
        {
            get => _typeBarsCol;
            private set
            {
                if (_typeBarsCol == value) return;
                _typeBarsCol = value;
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
        // Intentionally hides DashboardPageBase.LastRefreshed so this page
        // can use its own format ("Last refreshed dd MMM yyyy, HH:mm") and
        // an error message ("… not initialized.") instead of the base's
        // generic "Updated HH:mm:ss" stamp.
        private string _lastRefreshed = string.Empty;
        public new string LastRefreshed
        {
            get => _lastRefreshed;
            private set
            {
                if (_lastRefreshed == value) return;
                _lastRefreshed = value;
                OnPropertyChanged();
            }
        }

        // ── Status string constants (matches SavedDeliveryNote.Status) ─
        private const string StatusDraft = "Draft";
        private const string StatusDispatched = "Dispatched";
        private const string StatusDelivered = "Delivered";
        private const string StatusSigned = "Signed";
        private const string StatusReceived = "Received";

        // ── Type string constants (matches SavedDeliveryNote.DeliveryType) ─
        private const string TypeOriginal = "Original";
        private const string TypeDummy = "Dummy";
        private const string TypeRework = "Rework";
        private const string TypeReturn = "Return";

        // ── Core load ────────────────────────────────────────────────
        protected override async Task LoadCoreAsync()
        {
            if (_deliveryNotes == null)
            {
                _kpiCards = new List<KpiCardModel>();
                _statusBars = new List<ChartBarModel>();
                _typeBars = new List<ChartBarModel>();
                _recentFeed = new List<ActivityFeedItem>();

                KpiCards = new ObservableCollection<KpiCardModel>();
                StatusBars = new ObservableCollection<ChartBarModel>();
                TypeBars = new ObservableCollection<ChartBarModel>();
                RecentFeed = new ObservableCollection<ActivityFeedItem>();

                HasStatusData = false;
                HasRecentFeed = false;
                LastRefreshed = "Delivery Notes database not initialized.";
                return;
            }

            // ── Parallel loads for speed — Phase 3: no .Result, use await
            var allTask = _deliveryNotes.GetAllAsync();
            var pendingTask = _deliveryNotes.GetPendingQtyJobsAsync();

            await Task.WhenAll(allTask, pendingTask);

            var all = await allTask;
            var pending = await pendingTask;
            var today = DateTime.Today;

            // ── Counts ───────────────────────────────────────────────
            int total = all.Count;

            int deliveredToday = all.Count(d =>
                d.DeliveryDate.Date == today &&
                (d.Status == StatusDelivered ||
                 d.Status == StatusSigned ||
                 d.Status == StatusReceived));

            // GOTCHA #40 FIX: Sum per-JO balances (already de-duplicated by repo),
            // NOT per-DN snapshots. GetPendingQtyJobsAsync returns:
            //   { JobOrderId, TotalOrdered, TotalDelivered, TotalBalance }
            // per JO with only JOs that have balance > 0.
            int pendingBalance = pending.Sum(p => p.TotalBalance);

            int reworkReturned = all.Count(d =>
                d.DeliveryType == TypeRework ||
                d.DeliveryType == TypeReturn);

            // ── KPI cards ────────────────────────────────────────────
            //   v2.5 BUG FIX: theme-correct brush keys
            _kpiCards = new List<KpiCardModel>
            {
                new KpiCardModel
                {
                    Icon         = "\uE7B8",
                    Title        = "Total Delivery Notes",
                    DisplayValue = total.ToString("N0"),
                    BadgeColor   = "AccentBrush",
                    TintColor    = "TintIndigoBrush"
                },
                new KpiCardModel
                {
                    Icon         = "\uE73E",
                    Title        = "Delivered Today",
                    DisplayValue = deliveredToday.ToString("N0"),
                    BadgeColor   = "SuccessBrush",     // v2.5: was "GreenBrush" (missing key)
                    TintColor    = "TintGreenBrush"
                },
                new KpiCardModel
                {
                    Icon         = "\uE823",
                    Title        = "Pending Balance (pcs)",
                    DisplayValue = pendingBalance.ToString("N0"),
                    BadgeColor   = "WarningBrush",     // v2.5: was "AmberBrush" (missing key)
                    TintColor    = "TintAmberBrush"
                },
                new KpiCardModel
                {
                    Icon         = "\uE90F",
                    Title        = "Rework + Returned",
                    DisplayValue = reworkReturned.ToString("N0"),
                    BadgeColor   = "DangerBrush",      // v2.5: was "RedBrush" (missing key)
                    TintColor    = "TintRedBrush"
                }
            };

            // ── Status distribution ──────────────────────────────────
            // SEMANTIC bar chart hex colors — universal DN status.
            var statusDefs = new[]
            {
                (Label: "Draft",      Key: StatusDraft,      BarColor: "#6B7280"),
                (Label: "Dispatched", Key: StatusDispatched, BarColor: "#F59E0B"),
                (Label: "Delivered",  Key: StatusDelivered,  BarColor: "#3B82F6"),
                (Label: "Signed",     Key: StatusSigned,     BarColor: "#3B82F6"),
                (Label: "Received",   Key: StatusReceived,   BarColor: "#8B5CF6"),
            };

            double statusMax = total == 0 ? 1d : (double)total;

            _statusBars = statusDefs.Select(sd =>
            {
                int count = all.Count(d => d.Status == sd.Key);
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

            // ── Type distribution ────────────────────────────────────
            // SEMANTIC bar chart hex colors — universal type semantic.
            var typeDefs = new[]
            {
                (Label: "Original", Key: TypeOriginal, BarColor: "#4338CA"),
                (Label: "Dummy",    Key: TypeDummy,    BarColor: "#94A3B8"),
                (Label: "Rework",   Key: TypeRework,   BarColor: "#F59E0B"),
                (Label: "Return",   Key: TypeReturn,   BarColor: "#EF4444"),
            };

            double typeMax = total == 0 ? 1d : (double)total;

            _typeBars = typeDefs.Select(td =>
            {
                int count = all.Count(d => d.DeliveryType == td.Key);
                return new ChartBarModel
                {
                    Label = td.Label,
                    RawValue = count,
                    ValueDisplay = count.ToString("N0"),
                    NormalizedHeight = total == 0 ? 0d : count / typeMax,
                    BarColor = td.BarColor,
                    IsHighlight = false
                };
            }).ToList();

            // ── Recent feed ───────────────────────────────────────────
            _recentFeed = all
                .OrderByDescending(d => d.UpdatedAt)
                .Take(10)
                .Select(BuildFeedItem)
                .ToList();

            // ── Push to UI ────────────────────────────────────────────
            KpiCards = new ObservableCollection<KpiCardModel>(_kpiCards);
            StatusBars = new ObservableCollection<ChartBarModel>(_statusBars);
            TypeBars = new ObservableCollection<ChartBarModel>(_typeBars);
            StatusMixSlices = DashboardChartAdapter.FromBars(_statusBars);
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
            TypeBars = new ObservableCollection<ChartBarModel>(_typeBars);
            StatusMixSlices = DashboardChartAdapter.FromBars(_statusBars);
            RecentFeed = new ObservableCollection<ActivityFeedItem>(_recentFeed);
        }

        // ── Feed item builder ─────────────────────────────────────────
        //   v2.5 BUG FIX: theme-correct brush keys for status pills.
        private static ActivityFeedItem BuildFeedItem(DeliveryNoteListItem dn)
        {
            string statusLabel = dn.Status switch
            {
                StatusDraft => "DRAFT",
                StatusDispatched => "DISPATCHED",
                StatusDelivered => "DELIVERED",
                StatusSigned => "SIGNED",
                StatusReceived => "RECEIVED",
                _ => dn.Status.ToUpperInvariant()
            };

            // v2.5: theme brush keys corrected
            //   was "AmberBrush" / "GreenBrush" / "PurpleBrush" — all MISSING
            //   now "WarningBrush" / "SuccessBrush" / "InfoBrush"
            string statusColor = dn.Status switch
            {
                StatusDraft => "MutedForegroundBrush",
                StatusDispatched => "WarningBrush",
                StatusDelivered => "SuccessBrush",
                StatusSigned => "AccentBrush",
                StatusReceived => "InfoBrush",
                _ => "MutedForegroundBrush"
            };

            string tintColor = dn.DeliveryType switch
            {
                TypeOriginal => "TintIndigoBrush",
                TypeDummy => "TintGrayBrush",
                TypeRework => "TintAmberBrush",
                TypeReturn => "TintRedBrush",
                _ => "TintIndigoBrush"
            };

            string typeIcon = dn.DeliveryType switch
            {
                TypeOriginal => "📦",
                TypeDummy => "📌",
                TypeRework => "🔧",
                TypeReturn => "↩️",
                _ => "📦"
            };

            string driverInfo = string.IsNullOrWhiteSpace(dn.DriverName)
                ? "No driver assigned"
                : $"🚚 {dn.DriverName}";

            string qtyInfo = dn.TotalDeliveredQty > 0
                ? $"{dn.TotalDeliveredQty} pcs delivered"
                : $"{dn.TotalOrderedQty} pcs ordered";

            return new ActivityFeedItem
            {
                Icon = typeIcon,
                Title = dn.DeliveryNoteId,
                SubTitle = $"{dn.CustomerName} — {driverInfo}",
                ValueDisplay = qtyInfo,
                StatusLabel = statusLabel,
                StatusColor = statusColor,
                TintColor = tintColor,
                ModuleTag = "DELIVERY NOTE",
                SortDate = dn.UpdatedAt
            };
        }
    }
}