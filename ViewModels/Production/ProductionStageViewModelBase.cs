// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Production/ProductionStageViewModelBase.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — shared stage view model (screens redesign 2026-09-11)
//
// Base for the eight production stage modules. Each module screen is a
// two-tier master–detail:
//
//   TIER 1 — UNIT LIST: one row per unit (Job + Spec + Step 5 serial),
//            shaped like the Step 5 grid the estimator built:
//            PI/Job No · Salesman · Ref · W1 · H1 · W2 · H2 · Qty ·
//            Unit SQM · Total SQM · LM · this stage's progress.
//            Charge-driven work (fan holes, polish LM, mitring LM) shows
//            as "(whole spec)" batch rows.
//
//   TIER 2 — UNIT DETAIL: select a row → job header (customer, project,
//            salesman, PI No, Job No) with OPEN JOB ORDER / OPEN PI
//            cross-navigation, plus the pane-level stage items for this
//            module with Start / Complete / Reopen floor actions.
//
// See docs/PRODUCTION_SCREENS_DESIGN_2026-09-11.md for the full design.
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
    /// <summary>One pane-level stage item in the detail panel.</summary>
    public sealed class ProductionWorkItemRow : ViewModelBase
    {
        public SavedProductionWorkItem Item { get; }

        public ProductionWorkItemRow(SavedProductionWorkItem item)
        {
            Item = item;
        }

        public int Id => Item.Id;
        public string Position => string.IsNullOrWhiteSpace(Item.Position) ? "—" : Item.Position;
        public string Treatment => string.IsNullOrWhiteSpace(Item.Treatment) ? "—" : Item.Treatment;

        public string SizeLabel => Item.WidthMm > 0 && Item.HeightMm > 0
            ? $"{Item.WidthMm.ToString(CultureInfo.InvariantCulture)} × {Item.HeightMm.ToString(CultureInfo.InvariantCulture)} mm"
            : "—";

        public string QtyLabel => Item.Qty > 0
            ? Item.Qty.ToString(CultureInfo.InvariantCulture) : "—";

        public string SqmLabel => Item.TotalSqm > 0
            ? Item.TotalSqm.ToString("0.###", CultureInfo.InvariantCulture) + " m²" : "—";

        public string LmLabel => Item.TotalLm > 0
            ? Item.TotalLm.ToString("0.###", CultureInfo.InvariantCulture) + " LM" : "—";

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

        public bool CanStart => Status == WorkItemStatus.Pending;
        public bool CanComplete => Status == WorkItemStatus.InProgress;
        public bool CanReopen => Status != WorkItemStatus.Pending;
    }

    /// <summary>
    /// One master-list row: a unit envelope (Step-5 shaped) or a charge
    /// batch row ("whole spec").
    /// </summary>
    public sealed class ProductionListRow
    {
        public bool IsBatch { get; init; }

        /// <summary>Unit identity — JobRef|Spec|Serial. Batch rows use the item id.</summary>
        public string RowKey { get; init; } = "";

        // ── Shared columns ───────────────────────────────────────────────
        public string StatusGlyph { get; init; } = "";
        public string StatusLabel { get; init; } = "";
        public string JobRef { get; init; } = "";
        public string PiNo { get; init; } = "";
        public string Customer { get; init; } = "";
        public string Salesman { get; init; } = "";
        public string SpecLabel { get; init; } = "";
        public string GlassRef { get; init; } = "";
        public string BuildType { get; init; } = "";
        public string SerialLabel { get; init; } = "—";

        // ── Unit envelope — full Step 5 shape, per pane, no pricing ─────
        public string W1Label { get; init; } = "—";
        public string H1Label { get; init; } = "—";
        public string W2Label { get; init; } = "—";
        public string H2Label { get; init; } = "—";
        public string QtyLabel { get; init; } = "—";
        public string Sqm1Label { get; init; } = "—";
        public string TotalSqm1Label { get; init; } = "—";
        public string Lm1Label { get; init; } = "—";
        public string Sqm2Label { get; init; } = "—";
        public string TotalSqm2Label { get; init; } = "—";
        public string Lm2Label { get; init; } = "—";
        public string ProgressLabel { get; init; } = "";

        /// <summary>Production-chain flow: released = actionable here.</summary>
        public bool FlowReleased { get; init; } = true;
        public string WaitingOn { get; init; } = "";

        // ── Batch rows (Step 6 charge work) ──────────────────────────────
        public string MetricLabel { get; init; } = "";

        /// <summary>Unit rows: this stage's items (detail panel source).</summary>
        public List<SavedProductionWorkItem> StageItems { get; init; } = new();

        /// <summary>Batch rows: the charge item itself.</summary>
        public SavedProductionWorkItem? BatchItem { get; init; }
    }

    /// <summary>
    /// One chip in the pipeline chain bar. Every stage module renders the
    /// whole eight-stage chain so the floor can follow work from Cutting
    /// through to Doubling/Lamination; the current stage is highlighted
    /// and any chip navigates to its module.
    /// </summary>
    public sealed class ProductionChainStageChip
    {
        public int Ordinal { get; init; }
        public string NavKey { get; init; } = "";
        public string Name { get; init; } = "";
        public string Icon { get; init; } = "";

        /// <summary>Released into the stage AND not yet completed.</summary>
        public int Open { get; init; }
        public int Completed { get; init; }
        public int Total { get; init; }

        public bool HasWork => Total > 0;
        public bool IsCleared => Total > 0 && Completed == Total;
        public bool IsCurrent { get; init; }

        public string CountLabel => Open > 0
            ? $"{Open.ToString(CultureInfo.InvariantCulture)} open · {Completed.ToString(CultureInfo.InvariantCulture)}/{Total.ToString(CultureInfo.InvariantCulture)}"
            : $"{Completed.ToString(CultureInfo.InvariantCulture)}/{Total.ToString(CultureInfo.InvariantCulture)} done";
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

        // Cross-navigation hooks — wired by MainViewModel exactly like
        // Sales Report / Statement drill-downs.
        public Action<string>? OnOpenJobOrderByNumber { get; set; }
        public Action<string>? OnOpenProformaByNumber { get; set; }

        /// <summary>Chain-bar navigation: open any stage module by nav key.</summary>
        public Action<string>? OnOpenStage { get; set; }

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
        public ICommand OpenStageCommand { get; }
        public ICommand OpenJobOrderCommand { get; }
        public ICommand OpenProformaCommand { get; }
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

        // ── KPIs ─────────────────────────────────────────────────────────

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

        private string _unitsReadyLabel = "0 / 0";
        public string UnitsReadyLabel { get => _unitsReadyLabel; private set { if (_unitsReadyLabel == value) return; _unitsReadyLabel = value; OnPropertyChanged(); } }

        private string _waitingUpstreamLabel = "";
        /// <summary>
        /// Production-chain backlog: work allocated to this stage but held
        /// back until the upstream stage completes (tool-chain gating).
        /// </summary>
        public string WaitingUpstreamLabel
        {
            get => _waitingUpstreamLabel;
            private set { if (_waitingUpstreamLabel == value) return; _waitingUpstreamLabel = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasWaitingUpstream)); }
        }

        public bool HasWaitingUpstream => !string.IsNullOrWhiteSpace(_waitingUpstreamLabel);

        private string _actionWarning = "";
        /// <summary>Floor connectivity feedback (completing before the predecessor stage).</summary>
        public string ActionWarning
        {
            get => _actionWarning;
            private set { if (_actionWarning == value) return; _actionWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasActionWarning)); }
        }

        public bool HasActionWarning => !string.IsNullOrWhiteSpace(_actionWarning);

        // ── Detail panel (tier 2) ────────────────────────────────────────

        private bool _hasSelection;
        public bool HasSelection
        {
            get => _hasSelection;
            private set { if (_hasSelection == value) return; _hasSelection = value; OnPropertyChanged(); }
        }

        private bool _isBatchSelection;
        public bool IsBatchSelection
        {
            get => _isBatchSelection;
            private set { if (_isBatchSelection == value) return; _isBatchSelection = value; OnPropertyChanged(); }
        }

        private string _selectedJobNo = "";
        public string SelectedJobNo { get => _selectedJobNo; private set { if (_selectedJobNo == value) return; _selectedJobNo = value; OnPropertyChanged(); } }

        private string _selectedPiNo = "";
        public string SelectedPiNo { get => _selectedPiNo; private set { if (_selectedPiNo == value) return; _selectedPiNo = value; OnPropertyChanged(); } }

        private string _selectedCustomer = "";
        public string SelectedCustomer { get => _selectedCustomer; private set { if (_selectedCustomer == value) return; _selectedCustomer = value; OnPropertyChanged(); } }

        private string _selectedProject = "";
        public string SelectedProject { get => _selectedProject; private set { if (_selectedProject == value) return; _selectedProject = value; OnPropertyChanged(); } }

        private string _selectedSalesman = "";
        public string SelectedSalesman { get => _selectedSalesman; private set { if (_selectedSalesman == value) return; _selectedSalesman = value; OnPropertyChanged(); } }

        private string _selectedSpecLabel = "";
        public string SelectedSpecLabel { get => _selectedSpecLabel; private set { if (_selectedSpecLabel == value) return; _selectedSpecLabel = value; OnPropertyChanged(); } }

        private string _selectedRef = "";
        public string SelectedRef { get => _selectedRef; private set { if (_selectedRef == value) return; _selectedRef = value; OnPropertyChanged(); } }

        private string _selectedSizeSummary = "";
        public string SelectedSizeSummary { get => _selectedSizeSummary; private set { if (_selectedSizeSummary == value) return; _selectedSizeSummary = value; OnPropertyChanged(); } }

        private string _selectedBatchMetric = "";
        public string SelectedBatchMetric { get => _selectedBatchMetric; private set { if (_selectedBatchMetric == value) return; _selectedBatchMetric = value; OnPropertyChanged(); } }

        /// <summary>Pane-level items for the selected unit (detail grid).</summary>
        public ObservableCollection<ProductionWorkItemRow> SelectedStageItems { get; } = new();

        // ── Pipeline chain bar ───────────────────────────────────────────

        /// <summary>
        /// The eight-stage chain, in floor order. Shared metadata with the
        /// stage VMs (same names, same Segoe MDL2 glyphs).
        /// </summary>
        private static readonly (ProductionStage Stage, string NavKey, string Name, string Icon)[] ChainMetadata =
        {
            (ProductionStage.Cutting,             "Cutting",             "Cutting",         "\uE8C6"),
            (ProductionStage.HolesCutouts,        "HolesCutouts",        "Holes & Cutouts", "\uE71D"),
            (ProductionStage.Polish,              "Polish",              "Polish",          "\uE895"),
            (ProductionStage.EdgeDeletion,        "EdgeDeletion",        "Edge Deletion",   "\uE774"),
            (ProductionStage.Mitring,             "Mitring",             "Mitring",         "\uE7E8"),
            (ProductionStage.Tempering,           "Tempering",           "Tempering",       "\uE728"),
            (ProductionStage.Pairing,             "Pairing",             "Pairing",         "\uE71B"),
            (ProductionStage.DoublingLamination,  "DoublingLamination",  "Double / Lam",    "\uE81C")
        };

        /// <summary>Whole-chain chips, refreshed with every list/KPI load.</summary>
        public ObservableCollection<ProductionChainStageChip> ChainStages { get; } = new();

        private async Task LoadChainAsync()
        {
            var rows = await Task.Run(ProductionRepository.GetChainSummaryAsync)
                .ConfigureAwait(true);

            var byStage = rows.ToDictionary(r => r.Stage);

            ChainStages.Clear();
            int ordinal = 0;
            foreach (var (stage, navKey, name, icon) in ChainMetadata)
            {
                ordinal++;
                byStage.TryGetValue((int)stage, out var counts);
                ChainStages.Add(new ProductionChainStageChip
                {
                    Ordinal = ordinal,
                    NavKey = navKey,
                    Name = name,
                    Icon = icon,
                    Open = counts?.Open ?? 0,
                    Completed = counts?.Completed ?? 0,
                    Total = counts?.Total ?? 0,
                    IsCurrent = stage == Stage
                });
            }
        }

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

                ListRows.Clear();
                foreach (var unit in data.Units)
                    ListRows.Add(BuildUnitRow(unit));
                foreach (var batch in data.BatchItems)
                    ListRows.Add(BuildBatchRow(batch));

                IsListEmpty = ListRows.Count == 0;

                int waiting = data.WaitingUnits + data.WaitingBatch;
                WaitingUpstreamLabel = waiting > 0
                    ? $"{waiting.ToString(CultureInfo.InvariantCulture)} row(s) allocated to this stage are " +
                      "held upstream — they appear here the moment their previous stage completes."
                    : "";

                // Restore the selection across reloads (status actions reload).
                if (previousKey != null)
                {
                    var match = ListRows.FirstOrDefault(r => r.RowKey == previousKey);
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

        private async Task LoadKpisAsync()
        {
            // KPIs describe what is ACTIONABLE in this stage right now —
            // the same flow-gated population the list shows (job filter
            // applies; search/status filters don't). Waiting rows are
            // loaded too so the backlog stays honest.
            string? jobRef = _selectedJobFilter == AllJobsFilter ? null : _selectedJobFilter;
            var data = await Task.Run(() =>
                ProductionRepository.GetStageListAsync(Stage, jobRef, null, null, includeWaiting: true))
                .ConfigureAwait(true);

            var units = data.Units.Where(u => u.FlowReleased).ToList();
            var batches = data.BatchItems.Where(b => b.FlowReleased).Select(b => b.Item).ToList();

            TotalCount = units.Count + batches.Count;
            PendingCount = units.Count(u => u.AggregateStatus == 0)
                + batches.Count(b => b.Status == (int)WorkItemStatus.Pending);
            InProgressCount = units.Count(u => u.AggregateStatus == 1)
                + batches.Count(b => b.Status == (int)WorkItemStatus.InProgress);
            CompletedCount = units.Count(u => u.AggregateStatus == 2)
                + batches.Count(b => b.Status == (int)WorkItemStatus.Completed);

            decimal area = units.Sum(u => u.TotalSqm1 + u.TotalSqm2);
            AreaTotalLabel = area.ToString("0.##", CultureInfo.InvariantCulture) + " m²";

            decimal stageMetric = Stage is ProductionStage.Polish or ProductionStage.Mitring
                ? units.Sum(u => u.Lm1 + u.Lm2) + batches.Sum(b => b.TotalLm)
                : Stage == ProductionStage.HolesCutouts
                    ? batches.Sum(b => b.ChargeMetricQty)
                    : units.Sum(u => u.Qty);

            MetricTotalLabel = Stage switch
            {
                ProductionStage.Polish or ProductionStage.Mitring =>
                    stageMetric.ToString("0.##", CultureInfo.InvariantCulture) + " LM",
                ProductionStage.HolesCutouts =>
                    stageMetric.ToString("0", CultureInfo.InvariantCulture) + " holes/cutouts",
                _ => stageMetric.ToString("0", CultureInfo.InvariantCulture) + " pcs"
            };

            var (totalUnits, readyUnits) = await Task.Run(() =>
                ProductionRepository.GetUnitReadinessAsync(jobRef))
                .ConfigureAwait(true);
            UnitsReadyLabel = $"{readyUnits.ToString(CultureInfo.InvariantCulture)} / {totalUnits.ToString(CultureInfo.InvariantCulture)}";

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

        // ── Row builders ─────────────────────────────────────────────────

        private static string FmtInt(int v) =>
            v > 0 ? v.ToString(CultureInfo.InvariantCulture) : "—";

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
                _ => "PENDING"
            };

            return new ProductionListRow
            {
                IsBatch = false,
                RowKey = $"U|{unit.JobRef}|{unit.SpecGroupNumber}|{unit.SerialNumber}",
                StatusGlyph = glyph,
                StatusLabel = statusLabel,
                JobRef = unit.JobRef,
                PiNo = unit.SourceProformaId,
                Customer = unit.CustomerName,
                Salesman = unit.Salesman,
                SpecLabel = $"Spec {unit.SpecGroupNumber.ToString(CultureInfo.InvariantCulture)}" +
                    (string.IsNullOrWhiteSpace(unit.SpecName) ? "" : $" · {unit.SpecName}"),
                GlassRef = string.IsNullOrWhiteSpace(unit.GlassRef) ? "—" : unit.GlassRef,
                BuildType = unit.BuildType,
                SerialLabel = unit.SerialNumber > 0
                    ? unit.SerialNumber.ToString(CultureInfo.InvariantCulture) : "—",
                W1Label = FmtInt(unit.W1),
                H1Label = FmtInt(unit.H1),
                W2Label = FmtInt(unit.W2),
                H2Label = FmtInt(unit.H2),
                QtyLabel = FmtInt(unit.Qty),
                Sqm1Label = FmtDec(unit.UnitSqm1, ""),
                TotalSqm1Label = FmtDec(unit.TotalSqm1, ""),
                Lm1Label = FmtDec(unit.Lm1, ""),
                Sqm2Label = FmtDec(unit.UnitSqm2, ""),
                TotalSqm2Label = FmtDec(unit.TotalSqm2, ""),
                Lm2Label = FmtDec(unit.Lm2, ""),
                ProgressLabel = $"{unit.DoneCount.ToString(CultureInfo.InvariantCulture)}/{unit.TotalCount.ToString(CultureInfo.InvariantCulture)}",
                FlowReleased = unit.FlowReleased,
                WaitingOn = unit.WaitingOn,
                StageItems = unit.Items
            };
        }

        private ProductionListRow BuildBatchRow(ProductionRepository.ProductionBatchProjection projection)
        {
            var batch = projection.Item;
            string statusLabel = batch.Status switch
            {
                (int)WorkItemStatus.Completed => "COMPLETED",
                (int)WorkItemStatus.InProgress => "IN PROGRESS",
                _ => "PENDING"
            };

            return new ProductionListRow
            {
                IsBatch = true,
                RowKey = $"B|{batch.Id}",
                StatusGlyph = batch.Status == (int)WorkItemStatus.Completed ? "✓"
                    : batch.Status == (int)WorkItemStatus.InProgress ? "◐" : "●",
                StatusLabel = statusLabel,
                JobRef = batch.JobRef,
                PiNo = batch.SourceProformaId,
                Customer = batch.CustomerName,
                Salesman = batch.Salesman,
                SpecLabel = $"Spec {batch.SpecGroupNumber.ToString(CultureInfo.InvariantCulture)}" +
                    (string.IsNullOrWhiteSpace(batch.SpecName) ? "" : $" · {batch.SpecName}"),
                GlassRef = "(whole spec)",
                BuildType = batch.BuildType,
                MetricLabel = $"{batch.ChargeMetricQty.ToString("0.###", CultureInfo.InvariantCulture)} " +
                    $"{batch.ChargeMetricUnit} · {(batch.ChargeTemplateKey ?? "").Replace('_', ' ')}",
                ProgressLabel = statusLabel,
                FlowReleased = projection.FlowReleased,
                WaitingOn = projection.WaitingOn,
                BatchItem = batch
            };
        }

        // ── Detail panel ─────────────────────────────────────────────────

        private void RefreshDetailPanel()
        {
            var row = _selectedRow;
            if (row == null)
            {
                ClearDetailPanel();
                return;
            }

            HasSelection = true;
            IsBatchSelection = row.IsBatch;
            SelectedJobNo = row.JobRef;
            SelectedPiNo = row.PiNo;
            SelectedCustomer = row.Customer;
            SelectedSalesman = row.Salesman;
            SelectedSpecLabel = row.SpecLabel;
            SelectedRef = row.GlassRef;

            SelectedStageItems.Clear();

            if (row.IsBatch && row.BatchItem != null)
            {
                var b = row.BatchItem;
                SelectedProject = b.ProjectName;
                SelectedSizeSummary = string.IsNullOrWhiteSpace(b.Notes) ? "—" : b.Notes;
                SelectedBatchMetric =
                    $"{b.ChargeMetricQty.ToString("0.###", CultureInfo.InvariantCulture)} {b.ChargeMetricUnit} · " +
                    $"{(b.ChargeTemplateKey ?? "").Replace('_', ' ')}" +
                    (string.IsNullOrWhiteSpace(b.PolishSubTypeLabel) ? "" : $" · {b.PolishSubTypeLabel}");
                SelectedStageItems.Add(new ProductionWorkItemRow(b));
            }
            else
            {
                var first = row.StageItems.FirstOrDefault();
                SelectedProject = first?.ProjectName ?? "";

                var sizes = new List<string>();
                if (row.W1Label != "—") sizes.Add($"W1 {row.W1Label} × H1 {row.H1Label}");
                if (row.W2Label != "—") sizes.Add($"W2 {row.W2Label} × H2 {row.H2Label}");
                SelectedSizeSummary = sizes.Count == 0 ? "—"
                    : string.Join("  ·  ", sizes) + $"  ·  Qty {row.QtyLabel}";
                SelectedBatchMetric = "";

                foreach (var item in row.StageItems)
                    SelectedStageItems.Add(new ProductionWorkItemRow(item));
            }
        }

        private void ClearDetailPanel()
        {
            HasSelection = false;
            IsBatchSelection = false;
            SelectedJobNo = "";
            SelectedPiNo = "";
            SelectedCustomer = "";
            SelectedProject = "";
            SelectedSalesman = "";
            SelectedSpecLabel = "";
            SelectedRef = "";
            SelectedSizeSummary = "";
            SelectedBatchMetric = "";
            SelectedStageItems.Clear();
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

        // ── Floor actions ────────────────────────────────────────────────

        private async Task SetStatusAsync(object? parameter, WorkItemStatus status)
        {
            if (parameter is not ProductionWorkItemRow row) return;

            // FLOOR CONNECTIVITY: completing a stage whose predecessor work
            // for this unit is still open is allowed (the floor sometimes
            // has a reason) but surfaced loudly — never silently.
            if (status == WorkItemStatus.Completed)
            {
                int pendingPredecessors = await Task.Run(() =>
                    ProductionRepository.GetPendingPredecessorCountAsync(row.Item))
                    .ConfigureAwait(true);

                ActionWarning = pendingPredecessors > 0
                    ? $"Heads-up: {pendingPredecessors.ToString(CultureInfo.InvariantCulture)} " +
                      $"{row.Item.PredecessorStage} item(s) for this unit are not completed yet — " +
                      "the upstream stage normally finishes first."
                    : "";
            }
            else
            {
                ActionWarning = "";
            }

            bool ok = await Task.Run(() =>
                ProductionRepository.SetStatusAsync(row.Id, status)).ConfigureAwait(true);

            if (ok) await LoadAllAsync().ConfigureAwait(true);
        }
    }
}
