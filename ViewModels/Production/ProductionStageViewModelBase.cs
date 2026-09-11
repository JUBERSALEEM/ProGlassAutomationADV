// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Production/ProductionStageViewModelBase.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — shared stage-queue view model (Phase 3, 2026-09-11)
//
// Base for the eight production stage modules (Cutting, Holes & Cutouts,
// Polish, Edge Deletion, Mitring, Tempering, Pairing, Doubling/Lamination).
// Each subclass only supplies its stage + identity; this base owns:
//   • the release picker (Job Orders + Approved PIs → distribute),
//   • the stage queue loaded from production.db,
//   • job / status filters, KPI chips and the floor actions
//     (Start / Complete / Reopen).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Production
{
    /// <summary>Display projection of one persisted work item.</summary>
    public sealed class ProductionWorkItemRow : ViewModelBase
    {
        public SavedProductionWorkItem Item { get; }

        public ProductionWorkItemRow(SavedProductionWorkItem item)
        {
            Item = item;
        }

        public int Id => Item.Id;
        public string JobRef => Item.JobRef;
        public string Customer => Item.CustomerName;
        public string GlassRef => string.IsNullOrWhiteSpace(Item.GlassRef) ? "—" : Item.GlassRef;
        public string Position => string.IsNullOrWhiteSpace(Item.Position) ? "—" : Item.Position;
        public string BuildType => Item.BuildType;
        public string Treatment => string.IsNullOrWhiteSpace(Item.Treatment) ? "—" : Item.Treatment;

        public string SpecLabel =>
            $"Spec {Item.SpecGroupNumber.ToString(CultureInfo.InvariantCulture)}" +
            (string.IsNullOrWhiteSpace(Item.SpecName) ? "" : $" · {Item.SpecName}");

        public string SizeLabel => Item.WidthMm > 0 && Item.HeightMm > 0
            ? $"{Item.WidthMm.ToString(CultureInfo.InvariantCulture)} × {Item.HeightMm.ToString(CultureInfo.InvariantCulture)}"
            : "—";

        public string QtyLabel => Item.Qty > 0
            ? Item.Qty.ToString(CultureInfo.InvariantCulture) : "—";

        public string SqmLabel => Item.TotalSqm > 0
            ? Item.TotalSqm.ToString("0.###", CultureInfo.InvariantCulture) + " m²" : "—";

        /// <summary>Stage-relevant metric: charge qty for charge items, LM for polish/mitring, else SQM.</summary>
        public string MetricLabel
        {
            get
            {
                if (!string.IsNullOrEmpty(Item.ChargeTemplateKey))
                {
                    return $"{Item.ChargeMetricQty.ToString("0.###", CultureInfo.InvariantCulture)} " +
                           $"{Item.ChargeMetricUnit} ({Item.ChargeTemplateKey.Replace('_', ' ')})";
                }
                return SqmLabel;
            }
        }

        public string DetailLabel
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Item.PolishSubTypeLabel)) parts.Add(Item.PolishSubTypeLabel);
                if (!string.IsNullOrWhiteSpace(Item.Notes)) parts.Add(Item.Notes);
                if (!string.IsNullOrWhiteSpace(Item.PredecessorStage)) parts.Add($"after {Item.PredecessorStage}");
                return parts.Count == 0 ? "—" : string.Join(" · ", parts);
            }
        }

        public WorkItemStatus Status => (WorkItemStatus)Item.Status;

        public string StatusLabel => Status switch
        {
            WorkItemStatus.Pending => "PENDING",
            WorkItemStatus.InProgress => "IN PROGRESS",
            WorkItemStatus.Completed => "COMPLETED",
            _ => ""
        };

        /// <summary>Theme brush key for the status pill.</summary>
        public string StatusBrushKey => Status switch
        {
            WorkItemStatus.Pending => "WarningBrush",
            WorkItemStatus.InProgress => "AccentBrush",
            WorkItemStatus.Completed => "SuccessBrush",
            _ => "MutedBrush"
        };

        public bool CanStart => Status == WorkItemStatus.Pending;
        public bool CanComplete => Status == WorkItemStatus.InProgress;
        public bool CanReopen => Status != WorkItemStatus.Pending;
    }

    /// <summary>Shared behaviour for all eight production stage modules.</summary>
    public abstract class ProductionStageViewModelBase : ViewModelBase
    {
        private const string AllJobsFilter = "All jobs";
        private const string AllStatusesFilter = "All statuses";

        public abstract ProductionStage Stage { get; }
        public abstract string ModuleName { get; }
        public abstract string Description { get; }
        public abstract string Icon { get; }

        protected ProductionStageViewModelBase()
        {
            RefreshCommand = new RelayCommand(_ =>
                AsyncUtility.SafeFireAndForget(LoadQueueAsync, "Production stage refresh"));

            LoadSourcesCommand = new RelayCommand(_ =>
                AsyncUtility.SafeFireAndForget(LoadSourcesAsync, "Production sources load"));

            ReleaseCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(ReleaseSelectedAsync, "Production release"),
                _ => SelectedSource != null && !IsReleasing);

            StartCommand = new RelayCommand(p =>
                AsyncUtility.SafeFireAndForget(() => SetStatusAsync(p, WorkItemStatus.InProgress), "Work item start"),
                p => p is ProductionWorkItemRow r && r.CanStart);

            CompleteCommand = new RelayCommand(p =>
                AsyncUtility.SafeFireAndForget(() => SetStatusAsync(p, WorkItemStatus.Completed), "Work item complete"),
                p => p is ProductionWorkItemRow r && r.CanComplete);

            ReopenCommand = new RelayCommand(p =>
                AsyncUtility.SafeFireAndForget(() => SetStatusAsync(p, WorkItemStatus.Pending), "Work item reopen"),
                p => p is ProductionWorkItemRow r && r.CanReopen);
        }

        // ── Commands ─────────────────────────────────────────────────────

        public ICommand RefreshCommand { get; }
        public ICommand LoadSourcesCommand { get; }
        public ICommand ReleaseCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand CompleteCommand { get; }
        public ICommand ReopenCommand { get; }

        // ── Release picker ───────────────────────────────────────────────

        public ObservableCollection<ProductionReleaseSource> Sources { get; } = new();

        private ProductionReleaseSource? _selectedSource;
        public ProductionReleaseSource? SelectedSource
        {
            get => _selectedSource;
            set { if (_selectedSource == value) return; _selectedSource = value; OnPropertyChanged(); }
        }

        private bool _isReleasing;
        public bool IsReleasing
        {
            get => _isReleasing;
            private set { if (_isReleasing == value) return; _isReleasing = value; OnPropertyChanged(); }
        }

        private string _releaseMessage = "";
        public string ReleaseMessage
        {
            get => _releaseMessage;
            private set { if (_releaseMessage == value) return; _releaseMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasReleaseMessage)); }
        }

        public bool HasReleaseMessage => !string.IsNullOrWhiteSpace(_releaseMessage);

        private bool _releaseSucceeded;
        public bool ReleaseSucceeded
        {
            get => _releaseSucceeded;
            private set { if (_releaseSucceeded == value) return; _releaseSucceeded = value; OnPropertyChanged(); }
        }

        // ── Filters ──────────────────────────────────────────────────────

        public ObservableCollection<string> JobFilters { get; } = new();

        private string _selectedJobFilter = AllJobsFilter;
        public string SelectedJobFilter
        {
            get => _selectedJobFilter;
            set
            {
                if (_selectedJobFilter == value) return;
                _selectedJobFilter = value;
                OnPropertyChanged();
                AsyncUtility.SafeFireAndForget(LoadQueueAsync, "Production job filter");
            }
        }

        public IReadOnlyList<string> StatusFilters { get; } =
            new[] { AllStatusesFilter, "Pending", "In progress", "Completed" };

        private string _selectedStatusFilter = AllStatusesFilter;
        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                if (_selectedStatusFilter == value) return;
                _selectedStatusFilter = value;
                OnPropertyChanged();
                AsyncUtility.SafeFireAndForget(LoadQueueAsync, "Production status filter");
            }
        }

        // ── Queue + KPIs ─────────────────────────────────────────────────

        public ObservableCollection<ProductionWorkItemRow> Queue { get; } = new();

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); }
        }

        private bool _isQueueEmpty;
        public bool IsQueueEmpty
        {
            get => _isQueueEmpty;
            private set { if (_isQueueEmpty == value) return; _isQueueEmpty = value; OnPropertyChanged(); }
        }

        private int _totalCount;
        public int TotalCount { get => _totalCount; private set { if (_totalCount == value) return; _totalCount = value; OnPropertyChanged(); } }

        private int _pendingCount;
        public int PendingCount { get => _pendingCount; private set { if (_pendingCount == value) return; _pendingCount = value; OnPropertyChanged(); } }

        private int _inProgressCount;
        public int InProgressCount { get => _inProgressCount; private set { if (_inProgressCount == value) return; _inProgressCount = value; OnPropertyChanged(); } }

        private int _completedCount;
        public int CompletedCount { get => _completedCount; private set { if (_completedCount == value) return; _completedCount = value; OnPropertyChanged(); } }

        private string _areaTotalLabel = "0 m²";
        public string AreaTotalLabel { get => _areaTotalLabel; private set { if (_areaTotalLabel == value) return; _areaTotalLabel = value; OnPropertyChanged(); } }

        private string _metricTotalLabel = "—";
        public string MetricTotalLabel { get => _metricTotalLabel; private set { if (_metricTotalLabel == value) return; _metricTotalLabel = value; OnPropertyChanged(); } }

        // ── Loading ──────────────────────────────────────────────────────

        private bool _initialLoadDone;

        /// <summary>Called by the view on first load and via Refresh.</summary>
        public async Task LoadAsync()
        {
            await LoadQueueAsync().ConfigureAwait(true);
            if (!_initialLoadDone)
            {
                _initialLoadDone = true;
                await LoadSourcesAsync().ConfigureAwait(true);
            }
        }

        private async Task LoadQueueAsync()
        {
            IsLoading = true;
            try
            {
                string? jobRef = _selectedJobFilter == AllJobsFilter ? null : _selectedJobFilter;
                WorkItemStatus? status = _selectedStatusFilter switch
                {
                    "Pending" => WorkItemStatus.Pending,
                    "In progress" => WorkItemStatus.InProgress,
                    "Completed" => WorkItemStatus.Completed,
                    _ => null
                };

                var items = await Task.Run(() =>
                    ProductionRepository.GetStageQueueAsync(Stage, jobRef, status))
                    .ConfigureAwait(true);

                Queue.Clear();
                foreach (var item in items)
                    Queue.Add(new ProductionWorkItemRow(item));

                IsQueueEmpty = Queue.Count == 0;

                // KPIs reflect the UNFILTERED stage queue so the chips stay
                // honest while the grid is filtered.
                var all = await Task.Run(() =>
                    ProductionRepository.GetStageQueueAsync(Stage, null, null))
                    .ConfigureAwait(true);

                TotalCount = all.Count;
                PendingCount = all.Count(i => i.Status == (int)WorkItemStatus.Pending);
                InProgressCount = all.Count(i => i.Status == (int)WorkItemStatus.InProgress);
                CompletedCount = all.Count(i => i.Status == (int)WorkItemStatus.Completed);

                decimal area = all.Sum(i => i.TotalSqm);
                AreaTotalLabel = area.ToString("0.##", CultureInfo.InvariantCulture) + " m²";

                decimal stageMetric = Stage is ProductionStage.Polish or ProductionStage.Mitring
                    ? all.Sum(i => i.TotalLm)
                    : Stage == ProductionStage.HolesCutouts
                        ? all.Sum(i => i.ChargeMetricQty)
                        : all.Sum(i => i.Qty);

                MetricTotalLabel = Stage switch
                {
                    ProductionStage.Polish or ProductionStage.Mitring =>
                        stageMetric.ToString("0.##", CultureInfo.InvariantCulture) + " LM",
                    ProductionStage.HolesCutouts =>
                        stageMetric.ToString("0", CultureInfo.InvariantCulture) + " holes/cutouts",
                    _ => stageMetric.ToString("0", CultureInfo.InvariantCulture) + " pcs"
                };

                // Job filter picker (preserve current choice when it survives).
                var jobRefs = await Task.Run(async () =>
                    (await ProductionRepository.GetReleasesAsync().ConfigureAwait(false))
                        .Select(r => r.JobRef).Distinct().OrderBy(r => r).ToList())
                    .ConfigureAwait(true);

                string previous = _selectedJobFilter;
                JobFilters.Clear();
                JobFilters.Add(AllJobsFilter);
                foreach (var jobRefEntry in jobRefs) JobFilters.Add(jobRefEntry);

                _selectedJobFilter = jobRefs.Contains(previous) ? previous : AllJobsFilter;
                OnPropertyChanged(nameof(SelectedJobFilter));
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadSourcesAsync()
        {
            var sources = await Task.Run(ProductionReleaseService.GetSourcesAsync)
                .ConfigureAwait(true);

            Sources.Clear();
            foreach (var source in sources) Sources.Add(source);

            if (SelectedSource == null && Sources.Count > 0)
                SelectedSource = Sources[0];
        }

        private async Task ReleaseSelectedAsync()
        {
            var source = SelectedSource;
            if (source == null)
            {
                ReleaseMessage = "Pick a Job Order or Proforma Invoice to release.";
                ReleaseSucceeded = false;
                return;
            }

            IsReleasing = true;
            ReleaseMessage = $"Distributing {source.Reference}…";
            ReleaseSucceeded = false;
            try
            {
                var (success, message, _) = await Task.Run(() =>
                    ProductionReleaseService.ReleaseAsync(source)).ConfigureAwait(true);

                ReleaseMessage = message;
                ReleaseSucceeded = success;

                if (success) await LoadQueueAsync().ConfigureAwait(true);
            }
            finally
            {
                IsReleasing = false;
            }
        }

        private async Task SetStatusAsync(object? parameter, WorkItemStatus status)
        {
            if (parameter is not ProductionWorkItemRow row) return;

            bool ok = await Task.Run(() =>
                ProductionRepository.SetStatusAsync(row.Id, status)).ConfigureAwait(true);

            if (ok) await LoadQueueAsync().ConfigureAwait(true);
        }
    }
}
