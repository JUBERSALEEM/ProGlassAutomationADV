// ═══════════════════════════════════════════════════════════════
// FILE: ProductionStageViewModelBase.cs
// MODULE: Production — stage identity, master list, filters, KPIs,
//         loading, release and floor actions (partial of
//         ProductionStageViewModelBase)
// ═══════════════════════════════════════════════════════════════

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
    /// <summary>Shared behaviour for all eight production stage modules.</summary>
    public abstract partial class ProductionStageViewModelBase : ViewModelBase
    {
        private const string AllJobsFilter = "All jobs";
        private const string AllStatusesFilter = "All statuses";

        public abstract ProductionStage Stage { get; }
        public abstract string ModuleName { get; }
        public abstract string Description { get; }
        public abstract string Icon { get; }

        /// <summary>
        /// One-line "what this station physically does" hero line, shown in
        /// each stage's own detail sidebar (industry focus). Distinct per
        /// stage — this is what makes the eight screens read as separate
        /// factory stations instead of one shared grid.
        /// </summary>
        public abstract string IndustryFocus { get; }

        /// <summary>"pcs" for pane stages, "units" for pairing/assembly.</summary>
        public string WorkUnitLabel =>
            Stage is ProductionStage.Pairing or ProductionStage.DoublingLamination
                ? "units" : "pcs";

        // Cross-navigation hooks — wired by MainViewModel exactly like
        // Sales Report / Statement drill-downs.
        public Action<string>? OnOpenJobOrderByNumber { get; set; }
        public Action<string>? OnOpenProformaByNumber { get; set; }

        /// <summary>Chain-bar navigation: open any stage module by nav key.</summary>
        public Action<string>? OnOpenStage { get; set; }

        /// <summary>
        /// Creates (or reopens) the Dispatching delivery note for a Job
        /// Order and navigates to it. Returns a message to surface —
        /// empty string on success.
        /// </summary>
        public Func<string, Task<string>>? OnCreateDeliveryNote { get; set; }

        protected ProductionStageViewModelBase()
        {
            RefreshCommand = new RelayCommand(_ =>
                AsyncUtility.SafeFireAndForget(LoadAllAsync, "Production stage refresh"));

            LoadSourcesCommand = new RelayCommand(_ =>
                AsyncUtility.SafeFireAndForget(LoadSourcesAsync, "Production sources load"));

            ReleaseCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(ReleaseSelectedAsync, "Production release"),
                _ => SelectedSource != null && !IsReleasing);

            OpenStageCommand = new RelayCommand(p =>
            {
                if (p is string navKey && !string.IsNullOrWhiteSpace(navKey))
                    OnOpenStage?.Invoke(navKey);
            }, _ => true);

            OpenJobOrderCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrWhiteSpace(SelectedJobNo))
                    OnOpenJobOrderByNumber?.Invoke(SelectedJobNo);
            }, _ => HasSelection && !string.IsNullOrWhiteSpace(SelectedJobNo));

            OpenProformaCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrWhiteSpace(SelectedPiNo))
                    OnOpenProformaByNumber?.Invoke(SelectedPiNo);
            }, _ => HasSelection && !string.IsNullOrWhiteSpace(SelectedPiNo));

            SaveQtyCommand = new RelayCommand(p =>
                AsyncUtility.SafeFireAndForget(() => ApplyDoneQtyAsync(p, null), "Work item qty save"),
                p => p is ProductionWorkItemRow r && r.CanWork);

            CompleteAllCommand = new RelayCommand(p =>
                AsyncUtility.SafeFireAndForget(() => ApplyDoneQtyAsync(p, CompleteAllMarker), "Work item qty all"),
                p => p is ProductionWorkItemRow r && r.CanWork);

            ResetQtyCommand = new RelayCommand(p =>
                AsyncUtility.SafeFireAndForget(() => ApplyDoneQtyAsync(p, 0), "Work item qty reset"),
                p => p is ProductionWorkItemRow r && r.Done > 0);

            CompleteUnitCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(CompleteUnitAsync, "Production unit complete"),
                _ => HasSelection && SelectedStageItems.Any(i => i.CanWork && i.Done < i.Inflow));

            // Always clickable for a production-managed job — the click
            // itself explains when there is nothing ready to deliver, so
            // the button never silently "fades out" while the floor works.
            CreateDeliveryNoteCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(CreateDeliveryNoteAsync, "Production delivery note"),
                _ => HasSelection && !string.IsNullOrWhiteSpace(SelectedJobNo));

            OpenUnitCommand = new RelayCommand(p => OpenUnit(p as ProductionListRow));
            CloseDetailCommand = new RelayCommand(_ => IsDetailOpen = false);
            InitStationCommands();
            InitPhase14Commands();

            SaveAllDoneCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(SaveAllDoneAsync, "Work item qty save all"),
                _ => SelectedStageItems.Any(i => i.CanWork));

            // Live refresh: the instant any floor entry is saved (by any
            // screen), every stage module re-reads its list, KPIs, chain
            // and open detail page — no manual refresh button needed.
            ProductionRepository.WorkItemsChanged += OnWorkItemsChanged;
        }

        /// <summary>Marker: "complete every piece that arrived" (inflow).</summary>
        private const int CompleteAllMarker = -1;

        // ── Commands ─────────────────────────────────────────────────────

        public ICommand RefreshCommand { get; }
        public ICommand LoadSourcesCommand { get; }
        public ICommand ReleaseCommand { get; }
        public ICommand OpenStageCommand { get; }
        public ICommand OpenJobOrderCommand { get; }
        public ICommand OpenProformaCommand { get; }
        public ICommand SaveQtyCommand { get; }
        public ICommand CompleteAllCommand { get; }
        public ICommand ResetQtyCommand { get; }
        public ICommand CompleteUnitCommand { get; }
        public ICommand CreateDeliveryNoteCommand { get; }

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
                AsyncUtility.SafeFireAndForget(LoadListAsync, "Production job filter");
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
                AsyncUtility.SafeFireAndForget(LoadListAsync, "Production status filter");
            }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                AsyncUtility.SafeFireAndForget(LoadListAsync, "Production search");
            }
        }

        // ── Master list ──────────────────────────────────────────────────

        /// <summary>
        /// Rows render in pages of 100 (virtualized grid + on-scroll
        /// loading) so jobs with very long unit lists never lag.
        /// </summary>
        private const int ListPageSize = 100;

        /// <summary>One job's arrived units, grouped for the per-job list.</summary>
        private sealed class JobGroup
        {
            public string JobRef = "";
            public List<ProductionRepository.ProductionUnitProjection> Units = new();
        }

        /// <summary>Full filtered projection behind the current list (one group per job).</summary>
        private List<JobGroup> _listCache = new();
        private int _loadedRowCount;
        private bool _isLoadingMore;

        public ObservableCollection<ProductionListRow> ListRows { get; } = new();

        private ProductionListRow? _selectedRow;
        public ProductionListRow? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (_selectedRow == value) return;
                _selectedRow = value;
                OnPropertyChanged();
                RefreshDetailPanel();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); }
        }

        private bool _isListEmpty;
        public bool IsListEmpty
        {
            get => _isListEmpty;
            private set { if (_isListEmpty == value) return; _isListEmpty = value; OnPropertyChanged(); }
        }

        private string _listFooterLabel = "";
        /// <summary>"Showing X of Y jobs — scroll for more".</summary>
        public string ListFooterLabel
        {
            get => _listFooterLabel;
            private set { if (_listFooterLabel == value) return; _listFooterLabel = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasListFooter)); }
        }

        public bool HasListFooter => !string.IsNullOrWhiteSpace(_listFooterLabel);

        // ── KPIs ─────────────────────────────────────────────────────────

        private int _totalCount;
        public int TotalCount { get => _totalCount; private set { if (_totalCount == value) return; _totalCount = value; OnPropertyChanged(); } }

        private int _pendingCount;
        public int PendingCount { get => _pendingCount; private set { if (_pendingCount == value) return; _pendingCount = value; OnPropertyChanged(); } }

        private int _inProgressCount;
        public int InProgressCount { get => _inProgressCount; private set { if (_inProgressCount == value) return; _inProgressCount = value; OnPropertyChanged(); } }

        private int _completedCount;
        public int CompletedCount { get => _completedCount; private set { if (_completedCount == value) return; _completedCount = value; OnPropertyChanged(); } }

        private string _pendingPiecesHint = "";
        public string PendingPiecesHint { get => _pendingPiecesHint; private set { if (_pendingPiecesHint == value) return; _pendingPiecesHint = value; OnPropertyChanged(); } }

        private string _inProgressPiecesHint = "";
        public string InProgressPiecesHint { get => _inProgressPiecesHint; private set { if (_inProgressPiecesHint == value) return; _inProgressPiecesHint = value; OnPropertyChanged(); } }

        private string _completedPiecesHint = "";
        public string CompletedPiecesHint { get => _completedPiecesHint; private set { if (_completedPiecesHint == value) return; _completedPiecesHint = value; OnPropertyChanged(); } }

        private string _areaTotalLabel = "0 m²";
        public string AreaTotalLabel { get => _areaTotalLabel; private set { if (_areaTotalLabel == value) return; _areaTotalLabel = value; OnPropertyChanged(); } }

        private string _metricTotalLabel = "—";
        public string MetricTotalLabel { get => _metricTotalLabel; private set { if (_metricTotalLabel == value) return; _metricTotalLabel = value; OnPropertyChanged(); } }

        private string _unitsReadyLabel = "0 / 0";
        public string UnitsReadyLabel { get => _unitsReadyLabel; private set { if (_unitsReadyLabel == value) return; _unitsReadyLabel = value; OnPropertyChanged(); } }

        private string _actionWarning = "";
        /// <summary>Feedback from clamped floor writes ("only N arrived").</summary>
        public string ActionWarning
        {
            get => _actionWarning;
            private set { if (_actionWarning == value) return; _actionWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasActionWarning)); OnPropertyChanged(nameof(HasAndon)); OnPropertyChanged(nameof(AndonLabel)); }
        }

        public bool HasActionWarning => !string.IsNullOrWhiteSpace(_actionWarning);

        private string _waitingUpstreamLabel = "";
        /// <summary>
        /// Pipeline backlog: units allocated to this stage where no piece
        /// has arrived yet — they flow in as upstream completes pieces.
        /// </summary>
        public string WaitingUpstreamLabel
        {
            get => _waitingUpstreamLabel;
            private set { if (_waitingUpstreamLabel == value) return; _waitingUpstreamLabel = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasWaitingUpstream)); OnPropertyChanged(nameof(HasAndon)); OnPropertyChanged(nameof(AndonLabel)); }
        }

        public bool HasWaitingUpstream => !string.IsNullOrWhiteSpace(_waitingUpstreamLabel);

        // ── Stage identity ───────────────────────────────────────────────

        /// <summary>"Stage 3 of 8" — position in the factory pipeline.</summary>
        public string StageOrdinalLabel => $"Stage {((int)Stage).ToString(CultureInfo.InvariantCulture)} of 8";

        // ── Loading ──────────────────────────────────────────────────────

        private bool _initialLoadDone;

        /// <summary>Called by the view on first load and via Refresh.</summary>
        public async Task LoadAsync()
        {
            await LoadAllAsync().ConfigureAwait(true);
            if (!_initialLoadDone)
            {
                _initialLoadDone = true;
                await LoadSourcesAsync().ConfigureAwait(true);
            }
        }

        private async Task LoadAllAsync()
        {
            await LoadListAsync().ConfigureAwait(true);
            await LoadKpisAsync().ConfigureAwait(true);
            await LoadChainAsync().ConfigureAwait(true);
        }

        private async Task LoadListAsync()
        {
            IsLoading = true;
            string? previousKey = _selectedRow?.RowKey;
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

                // Per-job list: the repository projects units; we group them
                // here so the list shows ONE row per job. The status filter
                // is applied at the JOB level (aggregate) below, not per unit.
                var data = await Task.Run(() =>
                    ProductionRepository.GetStageListAsync(
                        Stage, jobRef, null,
                        string.IsNullOrWhiteSpace(_searchText) ? null : _searchText))
                    .ConfigureAwait(true);

                _listCache = GroupJobs(data.Units, status);
                _loadedRowCount = 0;
                ListRows.Clear();

                // AutoSum footer over the full filtered set — TOTAL / DONE /
                // BALANCE column ends, summed across every job.
                int total = 0, done = 0, balance = 0;
                foreach (var g in _listCache)
                {
                    total += g.Units.Sum(u => u.Inflow);
                    done += g.Units.Sum(u => u.DoneQty);
                    balance += g.Units.Sum(u => u.BalanceQty);
                }
                ListTotalSum = total; ListDoneSum = done; ListBalanceSum = balance;

                // Keep the selection visible across reloads: extend the
                // first page far enough to include the selected job.
                int firstPageSize = Math.Min(ListPageSize, _listCache.Count);
                if (previousKey != null)
                {
                    int idx = _listCache.FindIndex(g => $"J|{g.JobRef}" == previousKey);
                    if (idx >= firstPageSize) firstPageSize = idx + 1;
                }
                AppendRows(firstPageSize);

                IsListEmpty = _listCache.Count == 0;

                int waiting = data.WaitingUnits;
                WaitingUpstreamLabel = waiting > 0
                    ? $"{waiting.ToString(CultureInfo.InvariantCulture)} unit(s) allocated to this stage have " +
                      "no pieces arrived yet — they flow in as the previous stage completes pieces."
                    : "";

                // Restore the selection across reloads (floor writes reload).
                if (previousKey != null)
                {
                    var match = ListRows.FirstOrDefault(x => x.RowKey == previousKey);
                    if (match != null) { _selectedRow = match; OnPropertyChanged(nameof(SelectedRow)); RefreshDetailPanel(); }
                    else ClearDetailPanel();
                }
                else if (_selectedRow == null)
                {
                    ClearDetailPanel();
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>Groups the unit projection into one entry per job, keeping the repository's order.</summary>
        private static List<JobGroup> GroupJobs(
            List<ProductionRepository.ProductionUnitProjection> units, WorkItemStatus? status)
        {
            var groups = units
                .GroupBy(u => u.JobRef)
                .Select(g => new JobGroup { JobRef = g.Key, Units = g.ToList() })
                .ToList();

            if (status.HasValue)
                groups = groups.Where(g => JobStatus(g) == status.Value).ToList();

            return groups;
        }

        /// <summary>A job's aggregate status: COMPLETED only when every unit
        /// has arrived and is fully done; IN PROGRESS once any piece is done.</summary>
        private static WorkItemStatus JobStatus(JobGroup group)
        {
            if (group.Units.Count == 0) return WorkItemStatus.Pending;
            bool anyDone = group.Units.Any(u => u.DoneQty > 0);
            bool allDone = group.Units.All(u => u.Inflow > 0 && u.DoneQty >= u.Inflow);
            return allDone ? WorkItemStatus.Completed
                : anyDone ? WorkItemStatus.InProgress
                : WorkItemStatus.Pending;
        }

        /// <summary>Appends rows until the list holds <paramref name="targetCount"/> rows.</summary>
        private void AppendRows(int targetCount)
        {
            targetCount = Math.Min(targetCount, _listCache.Count);
            while (_loadedRowCount < targetCount)
            {
                ListRows.Add(BuildJobRow(_listCache[_loadedRowCount], _loadedRowCount + 1));
                _loadedRowCount++;
            }

            ListFooterLabel = _listCache.Count == 0 ? "" :
                _loadedRowCount < _listCache.Count
                    ? $"Showing {_loadedRowCount.ToString(CultureInfo.InvariantCulture)} of " +
                      $"{_listCache.Count.ToString(CultureInfo.InvariantCulture)} jobs — scroll for the next " +
                      $"{ListPageSize.ToString(CultureInfo.InvariantCulture)}"
                    : $"Showing all {_listCache.Count.ToString(CultureInfo.InvariantCulture)} job(s)";
        }

        /// <summary>
        /// Incremental loading: the (virtualized) grid calls in from its
        /// ScrollChanged event; near the bottom, the next 100 rows render.
        /// </summary>
        public void OnMasterListScrolled(double verticalOffset, double viewportHeight, double extentHeight)
        {
            if (_isLoadingMore || _loadedRowCount >= _listCache.Count) return;
            if (extentHeight <= 0) return;
            if (verticalOffset + viewportHeight < extentHeight - 80) return;

            _isLoadingMore = true;
            try
            {
                AppendRows(_loadedRowCount + ListPageSize);
            }
            finally
            {
                _isLoadingMore = false;
            }
        }

        private async Task LoadKpisAsync()
        {
            // KPIs describe what is ACTIONABLE in this stage right now —
            // arrived pieces, done pieces and the load they carry. Job
            // filter applies; search/status filters don't.
            string? jobRef = _selectedJobFilter == AllJobsFilter ? null : _selectedJobFilter;
            var data = await Task.Run(() =>
                ProductionRepository.GetStageListAsync(Stage, jobRef, null, null, includeWaiting: true))
                .ConfigureAwait(true);

            // The main list is one row per job, so the KPI chips count jobs
            // (with the same job-level status buckets as the status filter);
            // only jobs that have actually arrived work count as "in queue".
            var jobs = data.Units
                .GroupBy(u => u.JobRef)
                .Select(g => new JobGroup { JobRef = g.Key, Units = g.ToList() })
                .Where(g => g.Units.Any(u => u.Inflow > 0))
                .ToList();

            TotalCount = jobs.Count;
            PendingCount = jobs.Count(g => JobStatus(g) == WorkItemStatus.Pending);
            InProgressCount = jobs.Count(g => JobStatus(g) == WorkItemStatus.InProgress);
            CompletedCount = jobs.Count(g => JobStatus(g) == WorkItemStatus.Completed);

            // Piece-flow KPI hints: the chips count jobs, the floor reads
            // pieces — both stay visible (units for assembly stages).
            string unitSuffix = Stage is ProductionStage.Pairing or ProductionStage.DoublingLamination
                ? " units" : " pcs";
            PendingPiecesHint = "Σ " +
                jobs.Where(g => JobStatus(g) == WorkItemStatus.Pending)
                    .Sum(g => g.Units.Sum(u => u.Inflow))
                    .ToString(CultureInfo.InvariantCulture) + unitSuffix;
            InProgressPiecesHint = "Σ " +
                jobs.Where(g => JobStatus(g) == WorkItemStatus.InProgress)
                    .Sum(g => g.Units.Sum(u => u.DoneQty))
                    .ToString(CultureInfo.InvariantCulture) + unitSuffix + " done";
            CompletedPiecesHint = "Σ " +
                jobs.Where(g => JobStatus(g) == WorkItemStatus.Completed)
                    .Sum(g => g.Units.Sum(u => u.DoneQty))
                    .ToString(CultureInfo.InvariantCulture) + unitSuffix + " done";

            decimal area = jobs.Sum(g => g.Units.Sum(u => u.ArrivedArea));
            AreaTotalLabel = area.ToString("0.##", CultureInfo.InvariantCulture) + " m²";

            decimal stageMetric = Stage is ProductionStage.Polish or ProductionStage.Mitring
                ? jobs.Sum(g => g.Units.Sum(u => u.ArrivedLm))
                : jobs.Sum(g => g.Units.Sum(u => u.Inflow));

            MetricTotalLabel = Stage switch
            {
                ProductionStage.Polish or ProductionStage.Mitring =>
                    stageMetric.ToString("0.##", CultureInfo.InvariantCulture) + " LM",
                ProductionStage.HolesCutouts =>
                    stageMetric.ToString("0", CultureInfo.InvariantCulture) + " pcs to drill/cut",
                ProductionStage.Pairing =>
                    stageMetric.ToString("0", CultureInfo.InvariantCulture) + " units to pair",
                ProductionStage.DoublingLamination =>
                    stageMetric.ToString("0", CultureInfo.InvariantCulture) + " units to assemble",
                _ => stageMetric.ToString("0", CultureInfo.InvariantCulture) + " pcs"
            };

            // Dispatch readiness is a QUANTITY: pieces that finished the
            // chain (SGU off the pane chain, multi-pane units off
            // doubling) — partial quantities count.
            var (totalUnits, readyUnits, totalPieces, readyPieces) = await Task.Run(() =>
                ProductionRepository.GetUnitReadinessAsync(jobRef))
                .ConfigureAwait(true);
            UnitsReadyLabel =
                $"{readyPieces.ToString(CultureInfo.InvariantCulture)} / {totalPieces.ToString(CultureInfo.InvariantCulture)} pcs" +
                (readyUnits > 0 ? $" · {readyUnits.ToString(CultureInfo.InvariantCulture)} unit(s) complete" : "");

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

            // If the selected job disappeared (release removed), the list
            // was filtered by it — resync the list to the reset filter.
            if (_selectedJobFilter != previous)
                await LoadListAsync().ConfigureAwait(true);
        }

        // ── Row builder ──────────────────────────────────────────────────

        /// <summary>
        /// Builds ONE list row per job: the job queue entry aggregates every
        /// arrived unit of the job into TOTAL / DONE / BALANCE and a status.
        /// The full per-piece data lives in <see cref="ProductionListRow.Units"/>
        /// and is edited in the full-page job detail.
        /// </summary>
        private ProductionListRow BuildJobRow(JobGroup group, int index)
        {
            int total = group.Units.Sum(u => u.Inflow);
            int done = group.Units.Sum(u => u.DoneQty);
            int balance = group.Units.Sum(u => u.BalanceQty);

            WorkItemStatus status = JobStatus(group);
            string glyph = status switch
            {
                WorkItemStatus.Completed => "✓",
                WorkItemStatus.InProgress => "◐",
                _ => "●"
            };
            string statusLabel = status switch
            {
                WorkItemStatus.Completed => "COMPLETED",
                WorkItemStatus.InProgress => "IN PROGRESS",
                _ => "PENDING"
            };

            var first = group.Units[0];
            int unitCount = group.Units.Count;
            int specCount = group.Units.Select(u => u.SpecGroupNumber).Distinct().Count();

            string size = first.W1 > 0 && first.H1 > 0
                ? $"{first.W1.ToString(CultureInfo.InvariantCulture)}×{first.H1.ToString(CultureInfo.InvariantCulture)}"
                : "—";
            if (first.W2 > 0 && first.H2 > 0)
                size += $" / {first.W2.ToString(CultureInfo.InvariantCulture)}×{first.H2.ToString(CultureInfo.InvariantCulture)}";
            if (unitCount > 1) size += " · …";

            string glass = string.Join(" · ", group.Units
                .Select(u => string.IsNullOrWhiteSpace(u.SpecName) ? u.GlassRef : u.SpecName)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3));
            if (string.IsNullOrWhiteSpace(glass)) glass = "—";

            decimal lm = group.Units.Sum(u => u.ArrivedLm);
            decimal area = group.Units.Sum(u => u.ArrivedArea);
            string treatment = group.Units.SelectMany(u => u.FlowItems)
                .Select(fi => fi.Item.Treatment)
                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "—";
            int innerUnits = group.Units.Count(u => u.W2 > 0);
            string match = innerUnits == 0
                ? "SGU"
                : innerUnits.ToString(CultureInfo.InvariantCulture) + " inner · " +
                  unitCount.ToString(CultureInfo.InvariantCulture) + " units";

            return new ProductionListRow
            {
                RowKey = $"J|{group.JobRef}",
                StatusGlyph = glyph,
                StatusLabel = statusLabel,
                JobRef = group.JobRef,
                PiNo = first.SourceProformaId,
                Customer = first.CustomerName,
                Salesman = first.Salesman,
                IndexLabel = index.ToString(CultureInfo.InvariantCulture),
                UnitsLabel = $"{unitCount.ToString(CultureInfo.InvariantCulture)} unit(s) · " +
                             $"{specCount.ToString(CultureInfo.InvariantCulture)} spec(s)",
                SizeLabel = size,
                GlassLabel = glass,
                LmLabel = lm.ToString("0.##", CultureInfo.InvariantCulture) + " LM",
                TreatmentLabel = treatment,
                LoadLabel = area.ToString("0.##", CultureInfo.InvariantCulture) + " m²",
                MatchLabel = match,
                InflowLabel = total.ToString(CultureInfo.InvariantCulture),
                DoneLabel = done.ToString(CultureInfo.InvariantCulture),
                BalanceLabel = balance.ToString(CultureInfo.InvariantCulture),
                Units = group.Units
            };
        }

        // ── Release sources ──────────────────────────────────────────────

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
                ReleaseMessage = "Pick a Job Order to release.";
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

                if (success) await LoadAllAsync().ConfigureAwait(true);
            }
            finally
            {
                IsReleasing = false;
            }
        }

        // ── Floor actions (piece quantities) ─────────────────────────────

        /// <summary>
        /// Saves the done-piece count for one stage item. The repository
        /// clamps to the pieces that actually arrived from upstream — the
        /// tool chain can never be overstated. <paramref name="qtyOverride"/>
        /// supports Complete-all (inflow) and Reset (0); null reads the
        /// row's editor input.
        /// </summary>
        private async Task ApplyDoneQtyAsync(object? parameter, int? qtyOverride)
        {
            if (parameter is not ProductionWorkItemRow row) return;

            int requested;
            if (qtyOverride.HasValue)
            {
                requested = qtyOverride.Value == CompleteAllMarker ? row.Inflow : qtyOverride.Value;
            }
            else
            {
                string raw = (row.DoneInput ?? "").Trim();
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out requested))
                {
                    ActionWarning = "Enter a whole number of pieces.";
                    return;
                }
            }

            var (ok, applied, message) = await Task.Run(() =>
                ProductionRepository.SetDoneQtyAsync(row.Id, requested))
                .ConfigureAwait(true);

            if (ok)
            {
                ActionWarning = message; // clamp notice or empty
                await LoadAllAsync().ConfigureAwait(true);
            }
        }

        /// <summary>
        /// Floor convenience: complete every arrived piece of the selected
        /// job in one action. Each item is clamped to its inflow by the
        /// repository, so a job with partly-arrived panes still only
        /// completes what actually arrived.
        /// </summary>
        private async Task CompleteUnitAsync()
        {
            var targets = SelectedStageItems
                .Where(i => i.CanWork && i.Done < i.Inflow)
                .ToList();
            if (targets.Count == 0) return;

            int remaining = targets.Sum(t => t.Inflow - t.Done);
            if (remaining > 1)
            {
                var answer = System.Windows.MessageBox.Show(
                    "Complete " + remaining.ToString(CultureInfo.InvariantCulture) +
                    " remaining piece(s) at " + ModuleName + "?",
                    "Complete all",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
                if (answer != System.Windows.MessageBoxResult.Yes) return;
            }

            foreach (var row in targets)
            {
                var (ok, _, message) = await Task.Run(() =>
                    ProductionRepository.SetDoneQtyAsync(row.Id, row.Inflow))
                    .ConfigureAwait(true);
                if (ok && !string.IsNullOrWhiteSpace(message))
                    ActionWarning = message;
            }

            await LoadAllAsync().ConfigureAwait(true);
        }
    }
}
