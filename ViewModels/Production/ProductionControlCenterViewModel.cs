// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Production/ProductionControlCenterViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 17 Production Control Center
//   Unified Production Control Center with reusable components:
//   KPI Cards, Filters, Production Queue, Selected WorkItem / Traceability
//   Use existing Production Work Center components where practical.
//   Do not duplicate Phase 16 UI logic unnecessarily.
//   GDS authoritative, surface hierarchy Background→Primary→Secondary→Detail→Elevated
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Control;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Services.Production.Control;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Production
{
    public sealed class ProductionControlCenterViewModel : ViewModelBase, IDisposable
    {
        // ── Title ────────────────────────────────────────────────────────
        public string Title => "Production Control Center";
        public string Subtitle => "Eight stations. Counts are the full stage, not the first page of the queue.";

        // ── Eight stations. Chip selection writes the stage; it is not a OneWay radio. ──
        private int _selectedStageTab = (int)ProductionStage.Cutting;
        public int SelectedStageTab
        {
            get => _selectedStageTab;
            set => SelectStage(value);
        }

        public ObservableCollection<ProductionStageChip> StageChips { get; } = new(ProductionStageChip.CreateBoard());
        public ICommand SelectStageCommand { get; private set; } = null!;

        public bool IsCuttingTab => SelectedStageTab == (int)ProductionStage.Cutting;
        public bool IsTemperingTab => SelectedStageTab == (int)ProductionStage.Tempering;
        public bool IsDguTab => SelectedStageTab == (int)ProductionStage.Pairing
            || SelectedStageTab == (int)ProductionStage.DoublingLamination;

        public ICommand SelectCuttingTabCommand => new RelayCommand(_ => SelectStage((int)ProductionStage.Cutting));
        public ICommand SelectTemperingTabCommand => new RelayCommand(_ => SelectStage((int)ProductionStage.Tempering));
        public ICommand SelectDguTabCommand => new RelayCommand(_ => SelectStage((int)ProductionStage.DoublingLamination));

        private void SelectStage(int stage)
        {
            if (stage < 1 || stage > 8 || _selectedStageTab == stage) return;
            _selectedStageTab = stage;
            OnPropertyChanged(nameof(SelectedStageTab));
            OnPropertyChanged(nameof(IsCuttingTab));
            OnPropertyChanged(nameof(IsTemperingTab));
            OnPropertyChanged(nameof(IsDguTab));
            foreach (var chip in StageChips)
                chip.IsSelected = chip.Stage == stage;
            string name = ((ProductionStage)stage).ToString();
            if (_processFilter != name)
            {
                _processFilter = name;
                OnPropertyChanged(nameof(ProcessFilter));
            }
            AsyncUtility.SafeFireAndForget(LoadAsync, "Control stage");
        }

        // ── Auto-Sum Aggregation Metrics ──
        private long _totalScheduledPcs;
        public long TotalScheduledPcs { get => _totalScheduledPcs; private set { _totalScheduledPcs = value; OnPropertyChanged(); } }

        private double _totalScheduledSqm;
        public double TotalScheduledSqm { get => _totalScheduledSqm; private set { _totalScheduledSqm = value; OnPropertyChanged(); } }

        private long _completedDonePcs;
        public long CompletedDonePcs { get => _completedDonePcs; private set { _completedDonePcs = value; OnPropertyChanged(); } }

        private double _completedDoneSqm;
        public double CompletedDoneSqm { get => _completedDoneSqm; private set { _completedDoneSqm = value; OnPropertyChanged(); } }

        private long _balancePendingPcs;
        public long BalancePendingPcs { get => _balancePendingPcs; private set { _balancePendingPcs = value; OnPropertyChanged(); } }

        private double _balancePendingSqm;
        public double BalancePendingSqm { get => _balancePendingSqm; private set { _balancePendingSqm = value; OnPropertyChanged(); } }

        private double _stageCompletionPct;
        public double StageCompletionPct { get => _stageCompletionPct; private set { _stageCompletionPct = value; OnPropertyChanged(); } }

        public void UpdateStageAutoSums()
        {
            // Aggregates across the entire queue without loading heavy data repeatedly
            var items = Queue.ToList();
            if (items.Count == 0)
            {
                TotalScheduledPcs = 0;
                TotalScheduledSqm = 0.0;
                CompletedDonePcs = 0;
                CompletedDoneSqm = 0.0;
                BalancePendingPcs = 0;
                BalancePendingSqm = 0.0;
                StageCompletionPct = 0.0;
                return;
            }

            var filtered = items.Where(MatchesSelectedStage).ToList();

            // Work-item rows are not PCS. Use persisted Qty / QtyDone.
            // Area from W×H×qty (mm→m²), never a invented 1.575 m² average.
            long total = filtered.Sum(i => (long)Math.Max(0, i.Qty));
            long done = filtered.Sum(i => (long)Math.Min(Math.Max(0, i.QtyDone), Math.Max(0, i.Qty)));
            long balance = filtered.Sum(i => (long)i.Remaining);

            TotalScheduledPcs = total;
            CompletedDonePcs = done;
            BalancePendingPcs = balance >= 0 ? balance : 0;
            StageCompletionPct = TotalScheduledPcs > 0 ? Math.Round((double)CompletedDonePcs * 100.0 / TotalScheduledPcs, 1) : 0.0;

            TotalScheduledSqm = Math.Round(filtered.Sum(PaneSqm), 1);
            CompletedDoneSqm = Math.Round(filtered.Sum(DonePaneSqm), 1);
            BalancePendingSqm = Math.Round(Math.Max(0, TotalScheduledSqm - CompletedDoneSqm), 1);
            PublishStageViews();
        }

        private bool MatchesSelectedStage(ProductionQueueItem item)
        {
            if (IsAssemblyStage(item) && (int)item.Stage == SelectedStageTab)
                return true;
            return (int)item.Stage == SelectedStageTab;
        }

        private static bool IsAssemblyStage(ProductionQueueItem i) =>
            i.Stage == ProductionStage.Pairing || i.Stage == ProductionStage.DoublingLamination;

        private static void ReplaceQueue(
            ObservableCollection<ProductionQueueItem> target,
            IEnumerable<ProductionQueueItem> items)
        {
            target.Clear();
            foreach (var item in items)
                target.Add(item);
        }

        private void PublishStageViews()
        {
            ReplaceQueue(StageQueue, Queue.Where(MatchesSelectedStage));
            ReplaceQueue(StageExecutableQueue, ExecutableQueue.Where(MatchesSelectedStage));
            ReplaceQueue(StageBlockedQueue, BlockedQueue.Where(MatchesSelectedStage));
            ReplaceQueue(StageReworkQueue, ReworkQueue.Where(MatchesSelectedStage));
            ReplaceQueue(StageQcQueue, QcQueue.Where(MatchesSelectedStage));
            if (Selected != null && !MatchesSelectedStage(Selected))
                Selected = null;
        }

        // ── Filters ──────────────────────────────────────────────────────
        private string _jobFilter = string.Empty;
        public string JobFilter { get => _jobFilter; set { if (_jobFilter == value) return; _jobFilter = value; OnPropertyChanged(); QueueFilterReload(); } }

        private string _customerFilter = string.Empty;
        public string CustomerFilter { get => _customerFilter; set { if (_customerFilter == value) return; _customerFilter = value; OnPropertyChanged(); QueueFilterReload(); } }

        private string _search = string.Empty;
        public string Search { get => _search; set { if (_search == value) return; _search = value; OnPropertyChanged(); QueueFilterReload(); } }

        public List<string> ProcessOptions { get; } = new List<string> { "All", "Cutting", "HolesCutouts", "Polish", "EdgeDeletion", "Mitring", "Tempering", "Pairing", "DoublingLamination" };
        private string _processFilter = "Cutting";
        public string ProcessFilter
        {
            get => _processFilter;
            set
            {
                if (_processFilter == value) return;
                _processFilter = value;
                OnPropertyChanged();
                if (value != "All" && Enum.TryParse<ProductionStage>(value, out var stage) && (int)stage != _selectedStageTab)
                    SelectStage((int)stage);
                else
                    QueueFilterReload();
            }
        }

        public List<string> ReadinessOptions { get; } = new List<string> { "All", "Ready", "Blocked", "InProgress", "Paused", "Rework", "QcPending", "QcFailed", "Completed", "Cancelled", "OnHold" };
        private string _readinessFilter = "All";
        public string ReadinessFilter { get => _readinessFilter; set { if (_readinessFilter == value) return; _readinessFilter = value; OnPropertyChanged(); QueueFilterReload(); } }

        public List<string> PriorityOptions { get; } = new List<string> { "All", "Normal", "High", "Urgent" };
        private string _priorityFilter = "All";
        public string PriorityFilter { get => _priorityFilter; set { if (_priorityFilter == value) return; _priorityFilter = value; OnPropertyChanged(); QueueFilterReload(); } }

        public List<string> DueRiskOptions { get; } = new List<string> { "All", "OnTrack", "DueSoon", "AtRisk", "Overdue", "NoDate" };
        private string _dueRiskFilter = "All";
        public string DueRiskFilter { get => _dueRiskFilter; set { if (_dueRiskFilter == value) return; _dueRiskFilter = value; OnPropertyChanged(); } }

        // ── KPIs ─────────────────────────────────────────────────────────
        private ProductionDashboardKpis _kpis = new ProductionDashboardKpis();
        public ProductionDashboardKpis Kpis { get => _kpis; private set { _kpis = value; OnPropertyChanged(); OnPropertyChanged(nameof(KpiSummary)); } }
        public string KpiSummary => $"Jobs {Kpis.JobsInProduction} Ready {Kpis.ReadyToStart} InProg {Kpis.InProgress} Blocked {Kpis.Blocked} OnHold {Kpis.OnHold} QC Pend {Kpis.QcPending} Fail {Kpis.QcFailed} Rework {Kpis.Rework} Today {Kpis.CompletedToday} ReadyDel {Kpis.ReadyForDelivery} Overdue {Kpis.OverdueJobs} Shortage {Kpis.MaterialShortages} Bottleneck {Kpis.BottleneckProcess} ({Kpis.BottleneckCount})";

        // ── Queue ────────────────────────────────────────────────────────
        public ObservableCollection<ProductionQueueItem> Queue { get; } = new ObservableCollection<ProductionQueueItem>();
        public ObservableCollection<ProductionQueueItem> ExecutableQueue { get; } = new ObservableCollection<ProductionQueueItem>();
        public ObservableCollection<ProductionQueueItem> BlockedQueue { get; } = new ObservableCollection<ProductionQueueItem>();
        public ObservableCollection<ProductionQueueItem> ReworkQueue { get; } = new ObservableCollection<ProductionQueueItem>();
        public ObservableCollection<ProductionQueueItem> QcQueue { get; } = new ObservableCollection<ProductionQueueItem>();

        // Stage ribbon used to change only the totals. The grids stayed on the
        // full queue, so Cutting / Tempering / DGU looked live and were not.
        public ObservableCollection<ProductionQueueItem> StageQueue { get; } = new();
        public ObservableCollection<ProductionQueueItem> StageExecutableQueue { get; } = new();
        public ObservableCollection<ProductionQueueItem> StageBlockedQueue { get; } = new();
        public ObservableCollection<ProductionQueueItem> StageReworkQueue { get; } = new();
        public ObservableCollection<ProductionQueueItem> StageQcQueue { get; } = new();

        // ── Progress ─────────────────────────────────────────────────────
        public ObservableCollection<ProductionProgress> JobProgress { get; } = new ObservableCollection<ProductionProgress>();
        public ObservableCollection<ProductionProgress> ProcessProgress { get; } = new ObservableCollection<ProductionProgress>();
        private ProductionProgress _overallProgress = new ProductionProgress { JobRef = "OVERALL" };
        public ProductionProgress OverallProgress { get => _overallProgress; private set { _overallProgress = value; OnPropertyChanged(); } }

        // ── Pressure / Load ──────────────────────────────────────────────
        public ObservableCollection<ProductionQueuePressure> QueuePressure { get; } = new ObservableCollection<ProductionQueuePressure>();
        public ObservableCollection<WorkstationLoad> WorkstationLoad { get; } = new ObservableCollection<WorkstationLoad>();
        public ObservableCollection<SavedWorkstation> Workstations { get; } = new ObservableCollection<SavedWorkstation>();

        // ── Selection ────────────────────────────────────────────────────
        private ProductionQueueItem? _selected;
        public ProductionQueueItem? Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(DetailLabel));
                OnPropertyChanged(nameof(TraceabilityLabel));
                OnPropertyChanged(nameof(MaterialLabel));
                OnPropertyChanged(nameof(QcLabel));
            }
        }
        public bool HasSelection => Selected != null;
        public string DetailLabel => Selected == null ? "Select a work item to view detail" :
            $"Job {Selected.JobRef} | Customer {Selected.CustomerName} | PI {Selected.SourceProformaId} | Spec {Selected.SpecName} ({Selected.SpecGroupNumber}/{Selected.SerialNumber}) | Piece {Selected.Position} {Selected.WidthMm}x{Selected.HeightMm} Qty {Selected.Qty} | Process {Selected.StageLabel} | Status {Selected.Status} | Readiness {Selected.Readiness} | Block {Selected.BlockReason} | Priority {Selected.Priority} | Due {Selected.DueDate:yyyy-MM-dd} Risk {Selected.DueRisk} | Operator {Selected.Operator} | WS {Selected.WorkstationId} {Selected.WorkstationName} | Qty {Selected.QtyDone}/{Selected.Qty} Remaining {Selected.Remaining} | QC {Selected.QcResult} Rework {Selected.IsRework} FOC {Selected.IsFoc} | Hold {Selected.HoldReason} | Material Req {Selected.MaterialRequired} Res {Selected.MaterialReserved} Alloc {Selected.MaterialAllocated} Shortage {Selected.MaterialShortage} | Notes {Selected.Notes}";

        public string TraceabilityLabel => Selected == null ? "" :
            $"Previous {Selected.PreviousLabel} → Current {Selected.CurrentLabel} → Next {Selected.NextLabel} | Trace {Selected.TraceabilityLabel} | Pair {Selected.PairId} Outer {Selected.OuterPieceId} Inner {Selected.InnerPieceId} Assembly {Selected.AssemblyId} | DGU Outer {Selected.OuterPieceId} Inner {Selected.InnerPieceId} Pair {Selected.PairId} Assembly {Selected.AssemblyId} QC {Selected.QcResult} Ready | Lam Glass A + Interlayer + Glass B → Laminate";

        public string MaterialLabel => Selected == null ? "" :
            $"Stage PCS required {Selected.MaterialRequired} | Reserved {Selected.MaterialReserved} sheets | Allocated {Selected.MaterialAllocated} pieces | Consumed {Selected.MaterialConsumed} allocations | Available {Selected.MaterialAvailable} sheets | Shortage {Selected.MaterialShortage} {(Selected.HasMaterialShortage ? "Material Shortage" : "Material OK")} | Cutting Requirement → Reservation → Optimization → Allocation → Cutting Piece → Cutting Completion";

        public string QcLabel => Selected == null ? "" :
            $"QC {Selected.QcResult} Id {Selected.QcId} | Rework Seq {Selected.ReplacementSequence} | FOC {Selected.IsFoc} | Cancelled {Selected.IsCancelled}";

        // ── Inputs for actions ───────────────────────────────────────────
        private string _operatorInput = string.Empty;
        public string OperatorInput { get => _operatorInput; set { if (_operatorInput == value) return; _operatorInput = value; OnPropertyChanged(); } }

        private string _workstationInput = string.Empty;
        public string WorkstationInput { get => _workstationInput; set { if (_workstationInput == value) return; _workstationInput = value; OnPropertyChanged(); } }

        private string _holdReasonInput = "Customer clarification";
        public string HoldReasonInput { get => _holdReasonInput; set { if (_holdReasonInput == value) return; _holdReasonInput = value; OnPropertyChanged(); } }

        public List<string> HoldReasonOptions { get; } = new List<string> { "Customer clarification", "Material issue", "Quality issue", "Machine unavailable", "Supervisor hold", "Documentation issue" };

        private string _priorityInput = "Normal";
        public string PriorityInput { get => _priorityInput; set { if (_priorityInput == value) return; _priorityInput = value; OnPropertyChanged(); } }

        // ── Running / progress ───────────────────────────────────────────
        private bool _isRunning;
        public bool IsRunning { get => _isRunning; private set { _isRunning = value; OnPropertyChanged(); } }

        private string _progressLabel = string.Empty;
        public string ProgressLabel { get => _progressLabel; private set { _progressLabel = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasProgress)); } }
        public bool HasProgress => !string.IsNullOrWhiteSpace(ProgressLabel);

        private CancellationTokenSource? _cts;
        private int _loadGeneration;
        private DispatcherTimer? _filterTimer;

        /// <summary>Filters used to notify and then do nothing. A short
        /// debounce keeps a typing burst from starting a query per key,
        /// and a newer load cancels the one it replaces.</summary>
        private void QueueFilterReload()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                ArmFilterTimer();
            else
                dispatcher.BeginInvoke(new Action(ArmFilterTimer));
        }

        private void ArmFilterTimer()
        {
            _filterTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
            _filterTimer.Stop();
            _filterTimer.Tick -= OnFilterTick;
            _filterTimer.Tick += OnFilterTick;
            _filterTimer.Start();
        }

        private void OnFilterTick(object? sender, EventArgs e)
        {
            _filterTimer?.Stop();
            AsyncUtility.SafeFireAndForget(LoadAsync, "Control filter");
        }

        // ── Commands ─────────────────────────────────────────────────────
        public ICommand LoadCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand ResumeCommand { get; }
        public ICommand CompleteCommand { get; }
        public ICommand HoldCommand { get; }
        public ICommand ReleaseHoldCommand { get; }
        public ICommand AssignCommand { get; }
        public ICommand UnassignCommand { get; }
        public ICommand SetPriorityCommand { get; }
        public ICommand CancelCommand { get; }

        public ProductionControlCenterViewModel()
        {
            LoadCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(LoadAsync, "Control load"), _ => !IsRunning);
            SelectStageCommand = new RelayCommand(p =>
            {
                if (p is ProductionStageChip chip)
                    SelectStage(chip.Stage);
            });
            foreach (var chip in StageChips)
                chip.IsSelected = chip.Stage == _selectedStageTab;
            ListenForFloorChanges();
            AsyncUtility.SafeFireAndForget(LoadAsync, "Production control initial load");
            StartCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(StartAsync, "Control start"), _ => HasSelection && !IsRunning);
            PauseCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(PauseAsync, "Control pause"), _ => HasSelection && !IsRunning);
            ResumeCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(ResumeAsync, "Control resume"), _ => HasSelection && !IsRunning);
            CompleteCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(CompleteAsync, "Control complete"), _ => HasSelection && !IsRunning);
            HoldCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(HoldAsync, "Control hold"), _ => HasSelection && !IsRunning);
            ReleaseHoldCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(ReleaseHoldAsync, "Control release hold"), _ => HasSelection && Selected!.IsOnHold && !IsRunning);
            AssignCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(AssignAsync, "Control assign"), _ => HasSelection && !IsRunning);
            UnassignCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(UnassignAsync, "Control unassign"), _ => HasSelection && !IsRunning);
            SetPriorityCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(SetPriorityAsync, "Control priority"), _ => HasSelection && !IsRunning);
            CancelCommand = new RelayCommand(_ => { _cts?.Cancel(); ProgressLabel = "Cancellation requested"; }, _ => IsRunning);
        }

        
        /// <summary>P1 sync fix: live-refresh this page whenever ANY floor
        /// screen saves work — same contract the eight stage pages get from
        /// ProductionStageViewModelBase. Marshals to the UI thread.</summary>
        private bool _listening;

        private void ListenForFloorChanges()
        {
            if (_listening) return;
            ProductionRepository.WorkItemsChanged += OnLiveWorkItemsChanged;
            _listening = true;
        }

        public void Dispose()
        {
            if (_listening)
            {
                ProductionRepository.WorkItemsChanged -= OnLiveWorkItemsChanged;
                _listening = false;
            }
            _filterTimer?.Stop();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void OnLiveWorkItemsChanged()
        {
            if (!_listening) return;
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            void Reload() => AsyncUtility.SafeFireAndForget(LoadAsync, "Production control live refresh");
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.BeginInvoke(new System.Action(Reload));
            else
                Reload();
        }

        public async Task LoadAsync()
        {
            int generation = Interlocked.Increment(ref _loadGeneration);
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            IsRunning = true;
            ProgressLabel = "Loading production control queue...";

            try
            {
                await WorkstationService.EnsureSeededAsync(token).ConfigureAwait(false);

                // The station chip is the stage. Counts below are not taken from this page.
                int? stageFilter = SelectedStageTab;

                ProductionReadiness? readinessFilter = null;
                if (ReadinessFilter != "All" && Enum.TryParse<ProductionReadiness>(ReadinessFilter, out var rd))
                    readinessFilter = rd;

                ProductionPriority? priorityFilter = null;
                if (PriorityFilter != "All" && Enum.TryParse<ProductionPriority>(PriorityFilter, out var pr))
                    priorityFilter = pr;

                var queue = await ProductionQueueService.GetQueueAsync(
                    jobRefFilter: string.IsNullOrWhiteSpace(JobFilter) ? null : JobFilter.Trim(),
                    stageFilter: stageFilter,
                    readinessFilter: readinessFilter,
                    priorityFilter: priorityFilter,
                    search: string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
                    customerFilter: string.IsNullOrWhiteSpace(CustomerFilter) ? null : CustomerFilter.Trim(),
                    includeCompleted: false,
                    includeCancelled: false,
                    take: 200,
                    ct: token).ConfigureAwait(false);

                await ProductionTimingService.EnrichTimingAsync(queue, token).ConfigureAwait(false);

                var executable = queue.Where(q => q.Readiness == ProductionReadiness.Ready).OrderBy(q => q.DueDate ?? DateTime.MaxValue).ThenByDescending(q => q.Priority).ToList();
                var blocked = queue.Where(q => q.Readiness == ProductionReadiness.Blocked).OrderByDescending(q => q.Priority).ToList();
                var rework = queue.Where(q => q.Readiness == ProductionReadiness.Rework || q.IsRework).ToList();
                var qc = queue.Where(q => q.Readiness == ProductionReadiness.QcPending || q.Readiness == ProductionReadiness.QcFailed).ToList();

                var kpis = await ProductionDashboardService.GetKpisAsync(token).ConfigureAwait(false);
                var pressure = await ProductionDashboardService.GetQueuePressureAsync(token).ConfigureAwait(false);
                var wsLoad = await ProductionDashboardService.GetWorkstationLoadAsync(token).ConfigureAwait(false);
                var wsList = await WorkstationService.GetAllAsync(token).ConfigureAwait(false);

                var jobProg = await ProductionProgressService.GetJobProgressAsync(string.IsNullOrWhiteSpace(JobFilter) ? null : JobFilter.Trim(), token).ConfigureAwait(false);
                var procProg = await ProductionProgressService.GetProcessProgressAsync(token).ConfigureAwait(false);
                var overall = await ProductionProgressService.GetOverallProgressAsync(token).ConfigureAwait(false);
                var board = await ProductionRepository.GetStageBoardAsync(
                    string.IsNullOrWhiteSpace(JobFilter) ? null : JobFilter.Trim(),
                    string.IsNullOrWhiteSpace(CustomerFilter) ? null : CustomerFilter.Trim(),
                    token).ConfigureAwait(false);

                // Due risk filter post
                if (DueRiskFilter != "All" && Enum.TryParse<DueDateRisk>(DueRiskFilter, out var dr))
                {
                    queue = queue.Where(q => q.DueRisk == dr).ToList();
                }

                if (generation != _loadGeneration || token.IsCancellationRequested)
                    return;

                App.Current?.Dispatcher.Invoke(() =>
                {
                    if (generation != _loadGeneration) return;
                    Queue.Clear();
                    foreach (var qi in queue) Queue.Add(qi);

                    ExecutableQueue.Clear();
                    foreach (var qi in executable) ExecutableQueue.Add(qi);

                    BlockedQueue.Clear();
                    foreach (var qi in blocked) BlockedQueue.Add(qi);

                    ReworkQueue.Clear();
                    foreach (var qi in rework) ReworkQueue.Add(qi);

                    QcQueue.Clear();
                    foreach (var qi in qc) QcQueue.Add(qi);

                    Kpis = kpis;
                    UpdateStageAutoSums();
                    ApplyStageBoard(board);

                    QueuePressure.Clear();
                    foreach (var p in pressure) QueuePressure.Add(p);

                    WorkstationLoad.Clear();
                    foreach (var l in wsLoad) WorkstationLoad.Add(l);

                    Workstations.Clear();
                    foreach (var w in wsList) Workstations.Add(w);

                    JobProgress.Clear();
                    foreach (var jp in jobProg) JobProgress.Add(jp);

                    ProcessProgress.Clear();
                    foreach (var pp in procProg) ProcessProgress.Add(pp);

                    OverallProgress = overall;
                });

                if (generation == _loadGeneration)
                    ProgressLabel = $"Loaded {queue.Count} queue items | {kpis.JobsInProduction} jobs | Ready {kpis.ReadyToStart} Blocked {kpis.Blocked} InProg {kpis.InProgress} OnHold {kpis.OnHold} QC {kpis.QcPending}/{kpis.QcFailed} Rework {kpis.Rework} ReadyDel {kpis.ReadyForDelivery} Overdue {kpis.OverdueJobs} Shortage {kpis.MaterialShortages} Bottleneck {kpis.BottleneckProcess} ({kpis.BottleneckCount}) | Overall {overall.ProgressPct}%";
            }
            catch (OperationCanceledException)
            {
                if (generation == _loadGeneration)
                    ProgressLabel = "Cancelled";
            }
            catch (Exception ex)
            {
                if (generation != _loadGeneration) return;
                ProgressLabel = $"Load failed: {ex.Message}";
                Services.Logging.AppLogService.Error($"[ProductionControlCenter] Load failed: {ex.Message}", ex);
            }
            finally
            {
                if (generation == _loadGeneration)
                {
                    IsRunning = false;
                    _cts?.Dispose();
                    _cts = null;
                }
            }
        }

        private async Task StartAsync()
        {
            if (Selected == null) return;
            IsRunning = true;
            ProgressLabel = $"Starting {Selected.JobRef} {Selected.SpecName} {Selected.StageLabel}...";
            try
            {
                var startResult = await ProductionControlService.StartAsync(Selected.WorkItemId, OperatorInput, CancellationToken.None).ConfigureAwait(false);
                ProgressLabel = startResult.Ok ? $"Started {Selected.JobRef} {Selected.StageLabel}" : $"Start blocked: {startResult.Message}";
                if (startResult.Ok) await LoadAsync().ConfigureAwait(false);
            }
            catch (Exception ex) { ProgressLabel = $"Start failed: {ex.Message}"; }
            finally { IsRunning = false; }
        }

        private async Task PauseAsync()
        {
            if (Selected == null) return;
            IsRunning = true;
            ProgressLabel = $"Pausing {Selected.JobRef} {Selected.StageLabel}...";
            try
            {
                var pauseResult = await ProductionControlService.PauseAsync(Selected.WorkItemId, "Paused from Production Control Center").ConfigureAwait(false);
                ProgressLabel = pauseResult.Ok ? $"Paused {Selected.JobRef}" : $"Pause failed: {pauseResult.Message}";
                if (pauseResult.Ok) await LoadAsync().ConfigureAwait(false);
            }
            catch (Exception ex) { ProgressLabel = $"Pause failed: {ex.Message}"; }
            finally { IsRunning = false; }
        }

        private async Task ResumeAsync()
        {
            if (Selected == null) return;
            IsRunning = true;
            ProgressLabel = $"Resuming {Selected.JobRef} {Selected.StageLabel}...";
            try
            {
                var resumeResult = await ProductionControlService.ResumeAsync(Selected.WorkItemId).ConfigureAwait(false);
                ProgressLabel = resumeResult.Ok ? $"Resumed {Selected.JobRef}" : $"Resume failed: {resumeResult.Message}";
                if (resumeResult.Ok) await LoadAsync().ConfigureAwait(false);
            }
            catch (Exception ex) { ProgressLabel = $"Resume failed: {ex.Message}"; }
            finally { IsRunning = false; }
        }

        private async Task CompleteAsync()
        {
            if (Selected == null) return;
            IsRunning = true;
            ProgressLabel = $"Completing {Selected.JobRef} {Selected.StageLabel}...";
            try
            {
                var completeResult = await ProductionControlService.CompleteAsync(Selected.WorkItemId, Selected.Qty).ConfigureAwait(false);
                ProgressLabel = completeResult.Ok ? $"Completed {completeResult.AppliedQty}/{Selected.Qty} {Selected.JobRef} {completeResult.Message}" : $"Complete failed: {completeResult.Message}";
                if (completeResult.Ok) await LoadAsync().ConfigureAwait(false);
            }
            catch (Exception ex) { ProgressLabel = $"Complete failed: {ex.Message}"; }
            finally { IsRunning = false; }
        }

        private async Task HoldAsync()
        {
            if (Selected == null) return;
            IsRunning = true;
            ProgressLabel = $"Holding {Selected.JobRef} {Selected.StageLabel} Reason {HoldReasonInput}...";
            try
            {
                var holdResult = await ProductionControlService.HoldAsync(Selected.WorkItemId, HoldReasonInput, $"Hold from Control Center by {Services.Audit.AuditService.CurrentUsername}").ConfigureAwait(false);
                ProgressLabel = holdResult.Ok ? $"On Hold {holdResult.HoldId} {holdResult.Message}" : $"Hold failed: {holdResult.Message}";
                if (holdResult.Ok) await LoadAsync().ConfigureAwait(false);
            }
            catch (Exception ex) { ProgressLabel = $"Hold failed: {ex.Message}"; }
            finally { IsRunning = false; }
        }

        private async Task ReleaseHoldAsync()
        {
            if (Selected == null) return;
            // Need holdId — get from DB
            IsRunning = true;
            ProgressLabel = $"Releasing hold {Selected.JobRef}...";
            try
            {
                ProductionRepository.EnsureInitialized();
                using var db = new Services.Production.ProductionDbContext();
                var hold = await db.ControlHolds.FirstOrDefaultAsync(h => h.WorkItemId == Selected.WorkItemId && !h.IsReleased).ConfigureAwait(false);
                if (hold == null) { ProgressLabel = "No active hold found"; return; }
                var releaseResult = await ProductionControlService.ReleaseHoldAsync(hold.HoldId, "Released from Control Center").ConfigureAwait(false);
                ProgressLabel = releaseResult.Ok ? $"Hold Released {hold.HoldId}" : $"Release failed: {releaseResult.Message}";
                if (releaseResult.Ok) await LoadAsync().ConfigureAwait(false);
            }
            catch (Exception ex) { ProgressLabel = $"Release hold failed: {ex.Message}"; }
            finally { IsRunning = false; }
        }

        private async Task AssignAsync()
        {
            if (Selected == null) return;
            IsRunning = true;
            ProgressLabel = $"Assigning {Selected.JobRef} WS {WorkstationInput} Operator {OperatorInput}...";
            try
            {
                var assignResult = await ProductionControlService.AssignAsync(Selected.WorkItemId, WorkstationInput, OperatorInput, "Assigned from Control Center").ConfigureAwait(false);
                ProgressLabel = assignResult.Ok ? $"Assigned {assignResult.AssignmentId} {assignResult.Message}" : $"Assign failed: {assignResult.Message}";
                if (assignResult.Ok) await LoadAsync().ConfigureAwait(false);
            }
            catch (Exception ex) { ProgressLabel = $"Assign failed: {ex.Message}"; }
            finally { IsRunning = false; }
        }

        private async Task UnassignAsync()
        {
            if (Selected == null) return;
            IsRunning = true;
            ProgressLabel = $"Unassigning {Selected.JobRef}...";
            try
            {
                var unassignResult = await ProductionControlService.UnassignAsync(Selected.WorkItemId).ConfigureAwait(false);
                ProgressLabel = unassignResult.Ok ? $"Unassigned {unassignResult.Message}" : $"Unassign failed: {unassignResult.Message}";
                if (unassignResult.Ok) await LoadAsync().ConfigureAwait(false);
            }
            catch (Exception ex) { ProgressLabel = $"Unassign failed: {ex.Message}"; }
            finally { IsRunning = false; }
        }

        private async Task SetPriorityAsync()
        {
            if (Selected == null) return;
            if (!Enum.TryParse<ProductionPriority>(PriorityInput, out var prio)) prio = ProductionPriority.Normal;
            IsRunning = true;
            ProgressLabel = $"Setting priority {Selected.JobRef} → {prio}...";
            try
            {
                var prioResult = await ProductionControlService.SetPriorityAsync(Selected.WorkItemId, prio, $"Priority changed to {prio} from Control Center").ConfigureAwait(false);
                ProgressLabel = prioResult.Ok ? prioResult.Message : $"Priority failed: {prioResult.Message}";
                if (prioResult.Ok) await LoadAsync().ConfigureAwait(false);
            }
            catch (Exception ex) { ProgressLabel = $"Priority failed: {ex.Message}"; }
            finally { IsRunning = false; }
        }

        private void ApplyStageBoard(IReadOnlyList<ProductionRepository.StageBoardRow> board)
        {
            foreach (var chip in StageChips)
            {
                var row = board.FirstOrDefault(r => r.Stage == chip.Stage);
                long open = row == null ? 0 : Math.Max(0, row.Pieces - row.DonePieces);
                chip.Count = open > int.MaxValue ? int.MaxValue : (int)open;
                chip.IsSelected = chip.Stage == SelectedStageTab;
            }

            var selected = board.FirstOrDefault(r => r.Stage == SelectedStageTab);
            long pcs = selected?.Pieces ?? 0;
            long done = selected?.DonePieces ?? 0;
            TotalScheduledPcs = pcs;
            CompletedDonePcs = done;
            BalancePendingPcs = Math.Max(0, pcs - done);
            StageCompletionPct = pcs > 0 ? Math.Round(done * 100.0 / pcs, 1) : 0;
            TotalScheduledSqm = Math.Round(selected?.ScheduledSqm ?? 0, 1);
            CompletedDoneSqm = Math.Round(selected?.DoneSqm ?? 0, 1);
            BalancePendingSqm = Math.Round(Math.Max(0, (selected?.ScheduledSqm ?? 0) - (selected?.DoneSqm ?? 0)), 1);
        }

        private static double PaneSqm(ProductionQueueItem i)
        {
            if (i.WidthMm <= 0 || i.HeightMm <= 0 || i.Qty <= 0) return 0;
            return (double)i.WidthMm * i.HeightMm * i.Qty / 1_000_000.0;
        }

        private static double DonePaneSqm(ProductionQueueItem i)
        {
            int done = Math.Min(Math.Max(0, i.QtyDone), Math.Max(0, i.Qty));
            if (i.WidthMm <= 0 || i.HeightMm <= 0 || done <= 0) return 0;
            return (double)i.WidthMm * i.HeightMm * done / 1_000_000.0;
        }
    }
}
