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

        /// <summary>Full filtered projection behind the current list.</summary>
        private List<ProductionRepository.ProductionUnitProjection> _listCache = new();
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
        /// <summary>"Showing X of Y units — scroll for more".</summary>
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
            private set { if (_actionWarning == value) return; _actionWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasActionWarning)); }
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
            private set { if (_waitingUpstreamLabel == value) return; _waitingUpstreamLabel = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasWaitingUpstream)); }
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

                var data = await Task.Run(() =>
                    ProductionRepository.GetStageListAsync(
                        Stage, jobRef, status,
                        string.IsNullOrWhiteSpace(_searchText) ? null : _searchText))
                    .ConfigureAwait(true);

                // Full filtered projection is cached; the grid renders it
                // in pages of 100 (virtualized) so huge jobs stay fluid.
                _listCache = data.Units;
                _loadedRowCount = 0;
                ListRows.Clear();

                // AutoSum footer over the full filtered set (not just the
                // virtualized page) — TOTAL / DONE / BALANCE column ends.
                ListTotalSum = _listCache.Sum(u => u.Inflow);
                ListDoneSum = _listCache.Sum(u => u.DoneQty);
                ListBalanceSum = _listCache.Sum(u => u.BalanceQty);

                // Keep the selection visible across reloads: extend the
                // first page far enough to include the selected row.
                int firstPageSize = Math.Min(ListPageSize, _listCache.Count);
                if (previousKey != null)
                {
                    int idx = _listCache.FindIndex(u =>
                        $"U|{u.JobRef}|{u.SpecGroupNumber}|{u.SerialNumber}" == previousKey);
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

        /// <summary>Appends rows until the list holds <paramref name="targetCount"/> rows.</summary>
        private void AppendRows(int targetCount)
        {
            targetCount = Math.Min(targetCount, _listCache.Count);
            while (_loadedRowCount < targetCount)
            {
                ListRows.Add(BuildUnitRow(_listCache[_loadedRowCount]));
                _loadedRowCount++;
            }

            ListFooterLabel = _listCache.Count == 0 ? "" :
                _loadedRowCount < _listCache.Count
                    ? $"Showing {_loadedRowCount.ToString(CultureInfo.InvariantCulture)} of " +
                      $"{_listCache.Count.ToString(CultureInfo.InvariantCulture)} units — scroll for the next " +
                      $"{ListPageSize.ToString(CultureInfo.InvariantCulture)}"
                    : $"Showing all {_listCache.Count.ToString(CultureInfo.InvariantCulture)} unit(s)";
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

            var arrived = data.Units.Where(u => u.Inflow > 0).ToList();

            TotalCount = arrived.Count;
            PendingCount = arrived.Count(u => u.AggregateStatus == 0);
            InProgressCount = arrived.Count(u => u.AggregateStatus == 1);
            CompletedCount = arrived.Count(u => u.AggregateStatus == 2);

            // Piece-flow KPI hints: the boards count rows, the floor reads
            // pieces — both stay visible (units for assembly stages).
            string unitSuffix = Stage is ProductionStage.Pairing or ProductionStage.DoublingLamination
                ? " units" : " pcs";
            PendingPiecesHint = "Σ " +
                arrived.Where(u => u.AggregateStatus == 0).Sum(u => u.Inflow)
                    .ToString(CultureInfo.InvariantCulture) + unitSuffix;
            InProgressPiecesHint = "Σ " +
                arrived.Where(u => u.AggregateStatus == 1).Sum(u => u.DoneQty)
                    .ToString(CultureInfo.InvariantCulture) + unitSuffix + " done";
            CompletedPiecesHint = "Σ " +
                arrived.Where(u => u.AggregateStatus == 2).Sum(u => u.DoneQty)
                    .ToString(CultureInfo.InvariantCulture) + unitSuffix + " done";

            decimal area = arrived.Sum(u => u.ArrivedArea);
            AreaTotalLabel = area.ToString("0.##", CultureInfo.InvariantCulture) + " m²";

            decimal stageMetric = Stage is ProductionStage.Polish or ProductionStage.Mitring
                ? arrived.Sum(u => u.ArrivedLm)
                : arrived.Sum(u => u.Inflow);

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

        private static string FmtInt(int v) =>
            v > 0 ? v.ToString(CultureInfo.InvariantCulture) : "—";

        /// <summary>
        /// Step 4/5 size semantics: a second size pair on the row means
        /// OVERLAP glass; an inherited (equal) pair means an EQUAL-SIZE
        /// unit where the blank W2/H2 took W1/H1 at distribution.
        /// </summary>
        private static string BuildSizeRule(ProductionRepository.ProductionUnitProjection unit)
        {
            if (unit.BuildType == "Sgu" || unit.W2 <= 0 || unit.H2 <= 0) return "";

            return unit.W2 == unit.W1 && unit.H2 == unit.H1
                ? $"EQUAL-SIZE UNIT — both lites {unit.W1.ToString(CultureInfo.InvariantCulture)}×{unit.H1.ToString(CultureInfo.InvariantCulture)}"
                : $"OVERLAP GLASS — inner {unit.W2.ToString(CultureInfo.InvariantCulture)}×{unit.H2.ToString(CultureInfo.InvariantCulture)}";
        }

        private static string FmtDec(decimal v, string suffix) =>
            v > 0 ? v.ToString("0.###", CultureInfo.InvariantCulture) + suffix : "—";

        private ProductionListRow BuildUnitRow(ProductionRepository.ProductionUnitProjection unit)
        {
            string glyph = unit.AggregateStatus switch
            {
                2 => "✓",
                1 => "◐",
                _ => "●"
            };
            string statusLabel = unit.AggregateStatus switch
            {
                2 => "COMPLETED",
                1 => "IN PROGRESS",
                _ => unit.Inflow == 0 ? "WAITING UPSTREAM" : "PENDING"
            };

            return new ProductionListRow
            {
                RowKey = $"U|{unit.JobRef}|{unit.SpecGroupNumber}|{unit.SerialNumber}",
                SpecGroupNumber = unit.SpecGroupNumber,
                SerialNumber = unit.SerialNumber,
                StatusGlyph = glyph,
                StatusLabel = statusLabel,
                JobRef = unit.JobRef,
                PiNo = unit.SourceProformaId,
                Customer = unit.CustomerName,
                Salesman = unit.Salesman,
                SpecLabel = $"Spec {unit.SpecGroupNumber.ToString(CultureInfo.InvariantCulture)}" +
                    (string.IsNullOrWhiteSpace(unit.SpecName) ? "" : $" · {unit.SpecName}"),
                SerialLabel = unit.SerialNumber > 0
                    ? unit.SerialNumber.ToString(CultureInfo.InvariantCulture) : "—",
                GlassRef = string.IsNullOrWhiteSpace(unit.GlassRef) ? "—" : unit.GlassRef,
                BuildType = unit.BuildType,
                W1Label = FmtInt(unit.W1),
                H1Label = FmtInt(unit.H1),
                W2Label = FmtInt(unit.W2),
                H2Label = FmtInt(unit.H2),
                QtyLabel = FmtInt(unit.Qty),
                SizeRuleLabel = BuildSizeRule(unit),
                Sqm1Label = FmtDec(unit.UnitSqm1, ""),
                TotalSqm1Label = FmtDec(unit.TotalSqm1, ""),
                Lm1Label = FmtDec(unit.Lm1, ""),
                Sqm2Label = FmtDec(unit.UnitSqm2, ""),
                TotalSqm2Label = FmtDec(unit.TotalSqm2, ""),
                Lm2Label = FmtDec(unit.Lm2, ""),
                InflowLabel = unit.Inflow.ToString(CultureInfo.InvariantCulture),
                DoneLabel = unit.DoneQty.ToString(CultureInfo.InvariantCulture),
                BalanceLabel = unit.BalanceQty.ToString(CultureInfo.InvariantCulture),
                WaitingOn = unit.WaitingOn,
                FlowItems = unit.FlowItems
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
        /// unit in one action. Each item is clamped to its inflow by the
        /// repository, so a unit with partly-arrived panes still only
        /// completes what actually arrived.
        /// </summary>
        private async Task CompleteUnitAsync()
        {
            var targets = SelectedStageItems
                .Where(i => i.CanWork && i.Done < i.Inflow)
                .ToList();
            if (targets.Count == 0) return;

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
