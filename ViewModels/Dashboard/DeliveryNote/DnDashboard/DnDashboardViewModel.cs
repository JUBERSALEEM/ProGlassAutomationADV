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
using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.ViewModels.Dashboard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using ProGlassAutomation.Services.Dashboard.Core;
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
        // Was a `new` shadow over DashboardPageBase.LastRefreshed, used for
        // two things: a nicer format, and as a place to park the "database
        // not initialized" notice. The base now uses that format for every
        // page, and a missing database is an ERROR — it belongs on
        // HasError/ErrorMessage where the page's error band renders it, not
        // smuggled into a timestamp. Both reasons for the shadow are gone.

        // ── Status string constants (matches SavedDeliveryNote.Status) ─
        // Aliased to DeliveryNoteStatusValues, the canonical home.
        //
        // StatusDraft used to be a private "Draft" literal here. But
        // DeliveryNoteMapper writes "Dispatching", and Canonical() collapses
        // both to the label DeliveryNoteStatusMeta already displays, so this
        // page had no bar that counted mapper-written notes at all.
        private const string StatusDispatching = DeliveryNoteStatusValues.Dispatching;
        private const string StatusDispatched = DeliveryNoteStatusValues.Dispatched;
        private const string StatusDelivered = DeliveryNoteStatusValues.Delivered;
        private const string StatusSigned = DeliveryNoteStatusValues.Signed;
        private const string StatusReceived = DeliveryNoteStatusValues.Received;

        // ── Type string constants (matches SavedDeliveryNote.DeliveryType) ─
        private const string TypeOriginal = "Original";
        private const string TypeDummy = "Dummy";
        private const string TypeRework = "Rework";
        private const string TypeReturn = "Return";

        // ── Core load ────────────────────────────────────────────────
        protected override async Task LoadCoreAsync()
        {
            if (_deliveryNotes == null || App.DashboardMetrics == null)
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
                // An uninitialised database is an error state, not a refresh
                // stamp. The page's error band renders this; previously it was
                // parked in LastRefreshed, which only worked because this class
                // shadowed the base property.
                HasError = true;
                ErrorMessage = "Delivery Notes database not initialized.";
                return;
            }

            // Phase 3: one metrics call replaces two repository calls plus a
            // whole-table load. The service excludes Master DNs exactly as
            // GetAllAsync did, computes the pending balance from the per-job
            // aggregation rather than summing per-note snapshots, and returns
            // the ten newest notes for the feed.
            //
            // AllTime is deliberate: all four cards are lifetime figures. The
            // range only feeds the period-over-period delta, which this page
            // does not display.
            var m = await App.DashboardMetrics
                .GetDeliveryMetricsAsync(DashboardDateRange.AllTime());

            // ── Counts ───────────────────────────────────────────────
            int total = m.TotalNotes;

            // A NOTE count, not a piece count. DeliveryMetrics carries both;
            // this card has always shown notes, and its label says so.
            int deliveredToday = m.NotesDeliveredToday;

            // GOTCHA #40 is now encoded once, in the service: it sums the
            // repository's per-job balances, never TotalBalanceQty across
            // notes, which double-counts the same outstanding pieces once per
            // note.
            int pendingBalance = m.PendingPieces;
            bool pendingKnown = m.PendingPiecesAvailable;

            int reworkReturned = m.ReworkReturned;

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
                // Label IS the canonical status — bars are counted by
                // matching DeliveryNoteStatusValues.Canonical(d.Status)
                // against it. "Draft" and "Dispatching" both collapse to
                // "Dispatching", which is what DeliveryNoteStatusMeta shows.
                // The first bar used to say "Draft" and therefore counted
                // none of the mapper-written notes.
                (Label: "Dispatching", BarColor: "#6B7280"),
                (Label: "Dispatched",  BarColor: "#F59E0B"),
                (Label: "Delivered",   BarColor: "#3B82F6"),
                (Label: "Signed",      BarColor: "#3B82F6"),
                (Label: "Received",    BarColor: "#8B5CF6"),
            };

            double statusMax = total == 0 ? 1d : (double)total;

            _statusBars = statusDefs.Select(sd =>
            {
                // The service groups on DeliveryNoteStatusValues.Canonical,
                // which already collapses Draft/Dispatching — so matching the
                // label case-insensitively is equivalent to the old per-row
                // comparison without holding the rows here.
                int count = m.StatusBreakdown
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
                // NOTE — small deliberate change. This matched the stored
                // DeliveryType exactly; the service groups on the trimmed
                // value case-insensitively, with blanks in a "Standard" bucket
                // that no bar matches. A row stored as "rework " now counts
                // toward Rework instead of no bar at all.
                int count = m.TypeBreakdown
                    .Where(b => string.Equals(b.Name, td.Key,
                                              StringComparison.OrdinalIgnoreCase))
                    .Sum(b => b.Count);
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
            // Already the ten newest, newest first.
            _recentFeed = m.RecentNotes
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
            // LastRefreshed is written once, by DashboardPageBase.LoadAsync.
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
        // Takes RecentDeliveryRow rather than a DeliveryNoteListItem: the feed
        // renders six scalar columns, and requiring the list item is what forced
        // the whole-table load this page used to do.
        private static ActivityFeedItem BuildFeedItem(RecentDeliveryRow dn)
        {
            // Switch on the canonical form so "Draft" and "Dispatching" get
            // the same pill instead of the legacy one falling through to the
            // default label and the wrong colour.
            var canonicalStatus = DeliveryNoteStatusValues.Canonical(dn.Status);

            string statusLabel = canonicalStatus switch
            {
                StatusDispatching => "DISPATCHING",
                StatusDispatched => "DISPATCHED",
                StatusDelivered => "DELIVERED",
                StatusSigned => "SIGNED",
                StatusReceived => "RECEIVED",
                _ => canonicalStatus.ToUpperInvariant()
            };

            // v2.5: theme brush keys corrected
            //   was "AmberBrush" / "GreenBrush" / "PurpleBrush" — all MISSING
            //   now "WarningBrush" / "SuccessBrush" / "InfoBrush"
            string statusColor = canonicalStatus switch
            {
                StatusDispatching => "MutedForegroundBrush",
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