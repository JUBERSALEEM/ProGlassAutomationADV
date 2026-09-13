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

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Control;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Production.Control;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Production
{
    public sealed class ProductionControlCenterViewModel : ViewModelBase
    {
        // ── Title ────────────────────────────────────────────────────────
        public string Title => "Production Control Center";
        public string Subtitle => "Factory readiness, queue, blocking reasons, priority, due-date risk, workstation, operator, progress, dashboard";

        // ── Filters ──────────────────────────────────────────────────────
        private string _jobFilter = string.Empty;
        public string JobFilter { get => _jobFilter; set { if (_jobFilter == value) return; _jobFilter = value; OnPropertyChanged(); } }

        private string _customerFilter = string.Empty;
        public string CustomerFilter { get => _customerFilter; set { if (_customerFilter == value) return; _customerFilter = value; OnPropertyChanged(); } }

        private string _search = string.Empty;
        public string Search { get => _search; set { if (_search == value) return; _search = value; OnPropertyChanged(); } }

        public List<string> ProcessOptions { get; } = new List<string> { "All", "Cutting", "HolesCutouts", "Polish", "EdgeDeletion", "Mitring", "Tempering", "Pairing", "DoublingLamination" };
        private string _processFilter = "All";
        public string ProcessFilter { get => _processFilter; set { if (_processFilter == value) return; _processFilter = value; OnPropertyChanged(); } }

        public List<string> ReadinessOptions { get; } = new List<string> { "All", "Ready", "Blocked", "InProgress", "Paused", "Rework", "QcPending", "QcFailed", "Completed", "Cancelled", "OnHold" };
        private string _readinessFilter = "All";
        public string ReadinessFilter { get => _readinessFilter; set { if (_readinessFilter == value) return; _readinessFilter = value; OnPropertyChanged(); } }

        public List<string> PriorityOptions { get; } = new List<string> { "All", "Normal", "High", "Urgent" };
        private string _priorityFilter = "All";
        public string PriorityFilter { get => _priorityFilter; set { if (_priorityFilter == value) return; _priorityFilter = value; OnPropertyChanged(); } }

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
            $"Material Required {Selected.MaterialRequired} Reserved {Selected.MaterialReserved} Allocated {Selected.MaterialAllocated} Consumed {Selected.MaterialConsumed} Available {Selected.MaterialAvailable} Shortage {Selected.MaterialShortage} {(Selected.HasMaterialShortage ? "Material Shortage" : "Material OK")} | Cutting Requirement → Reservation → Optimization → Allocation → Cutting Piece → Cutting Completion";

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
            LoadCommand = new AsyncRelayCommand(_ => LoadAsync());
            StartCommand = new AsyncRelayCommand(_ => StartAsync(), _ => HasSelection);
            PauseCommand = new AsyncRelayCommand(_ => PauseAsync(), _ => HasSelection);
            ResumeCommand = new AsyncRelayCommand(_ => ResumeAsync(), _ => HasSelection);
            CompleteCommand = new AsyncRelayCommand(_ => CompleteAsync(), _ => HasSelection);
            HoldCommand = new AsyncRelayCommand(_ => HoldAsync(), _ => HasSelection);
            ReleaseHoldCommand = new AsyncRelayCommand(_ => ReleaseHoldAsync(), _ => HasSelection && Selected!.IsOnHold);
            AssignCommand = new AsyncRelayCommand(_ => AssignAsync(), _ => HasSelection);
            UnassignCommand = new AsyncRelayCommand(_ => UnassignAsync(), _ => HasSelection);
            SetPriorityCommand = new AsyncRelayCommand(_ => SetPriorityAsync(), _ => HasSelection);
            CancelCommand = new RelayCommand(_ => { _cts?.Cancel(); ProgressLabel = "Cancellation requested"; });
        }

        public async Task LoadAsync()
        {
            if (IsRunning) return;
            IsRunning = true;
            _cts = new CancellationTokenSource();
            ProgressLabel = "Loading production control queue...";

            try
            {
                await WorkstationService.EnsureSeededAsync(_cts.Token).ConfigureAwait(false);

                // Parse filters
                int? stageFilter = null;
                if (ProcessFilter != "All" && Enum.TryParse<ProductionStage>(ProcessFilter, out var st))
                    stageFilter = (int)st;

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
                    includeCompleted: false,
                    includeCancelled: false,
                    take: 200,
                    ct: _cts.Token).ConfigureAwait(false);

                await ProductionTimingService.EnrichTimingAsync(queue, _cts.Token).ConfigureAwait(false);

                var executable = queue.Where(q => q.Readiness == ProductionReadiness.Ready).OrderBy(q => q.DueDate ?? DateTime.MaxValue).ThenByDescending(q => q.Priority).ToList();
                var blocked = queue.Where(q => q.Readiness == ProductionReadiness.Blocked).OrderByDescending(q => q.Priority).ToList();
                var rework = queue.Where(q => q.Readiness == ProductionReadiness.Rework || q.IsRework).ToList();
                var qc = queue.Where(q => q.Readiness == ProductionReadiness.QcPending || q.Readiness == ProductionReadiness.QcFailed).ToList();

                var kpis = await ProductionDashboardService.GetKpisAsync(_cts.Token).ConfigureAwait(false);
                var pressure = await ProductionDashboardService.GetQueuePressureAsync(_cts.Token).ConfigureAwait(false);
                var wsLoad = await ProductionDashboardService.GetWorkstationLoadAsync(_cts.Token).ConfigureAwait(false);
                var wsList = await WorkstationService.GetAllAsync(_cts.Token).ConfigureAwait(false);

                var jobProg = await ProductionProgressService.GetJobProgressAsync(string.IsNullOrWhiteSpace(JobFilter) ? null : JobFilter.Trim(), _cts.Token).ConfigureAwait(false);
                var procProg = await ProductionProgressService.GetProcessProgressAsync(_cts.Token).ConfigureAwait(false);
                var overall = await ProductionProgressService.GetOverallProgressAsync(_cts.Token).ConfigureAwait(false);

                // Due risk filter post
                if (DueRiskFilter != "All" && Enum.TryParse<DueDateRisk>(DueRiskFilter, out var dr))
                {
                    queue = queue.Where(q => q.DueRisk == dr).ToList();
                }

                // Customer filter
                if (!string.IsNullOrWhiteSpace(CustomerFilter))
                {
                    var cf = CustomerFilter.Trim();
                    queue = queue.Where(q => q.CustomerName.IndexOf(cf, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                }

                App.Current?.Dispatcher.Invoke(() =>
                {
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

                ProgressLabel = $"Loaded {queue.Count} queue items | {kpis.JobsInProduction} jobs | Ready {kpis.ReadyToStart} Blocked {kpis.Blocked} InProg {kpis.InProgress} OnHold {kpis.OnHold} QC {kpis.QcPending}/{kpis.QcFailed} Rework {kpis.Rework} ReadyDel {kpis.ReadyForDelivery} Overdue {kpis.OverdueJobs} Shortage {kpis.MaterialShortages} Bottleneck {kpis.BottleneckProcess} ({kpis.BottleneckCount}) | Overall {overall.ProgressPct}%";
            }
            catch (OperationCanceledException)
            {
                ProgressLabel = "Cancelled";
            }
            catch (Exception ex)
            {
                ProgressLabel = $"Load failed: {ex.Message}";
                Services.Logging.AppLogService.Error($"[ProductionControlCenter] Load failed: {ex.Message}", ex);
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task StartAsync()
        {
            if (Selected == null) return;
            IsRunning = true;
            ProgressLabel = $"Starting {Selected.JobRef} {Selected.SpecName} {Selected.StageLabel}...";
            try
            {
                var (ok, msg) = await ProductionControlService.StartAsync(Selected.WorkItemId, OperatorInput, CancellationToken.None).ConfigureAwait(false);
                ProgressLabel = ok ? $"Started {Selected.JobRef} {Selected.StageLabel}" : $"Start blocked: {msg}";
                if (ok) await LoadAsync().ConfigureAwait(false);
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
                var (ok, msg) = await ProductionControlService.PauseAsync(Selected.WorkItemId, "Paused from Production Control Center").ConfigureAwait(false);
                ProgressLabel = ok ? $"Paused {Selected.JobRef}" : $"Pause failed: {msg}";
                if (ok) await LoadAsync().ConfigureAwait(false);
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
                var (ok, msg) = await ProductionControlService.ResumeAsync(Selected.WorkItemId).ConfigureAwait(false);
                ProgressLabel = ok ? $"Resumed {Selected.JobRef}" : $"Resume failed: {msg}";
                if (ok) await LoadAsync().ConfigureAwait(false);
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
                var (ok, applied, msg) = await ProductionControlService.CompleteAsync(Selected.WorkItemId, Selected.Qty).ConfigureAwait(false);
                ProgressLabel = ok ? $"Completed {applied}/{Selected.Qty} {Selected.JobRef} {msg}" : $"Complete failed: {msg}";
                if (ok) await LoadAsync().ConfigureAwait(false);
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
                var (ok, msg, holdId) = await ProductionControlService.HoldAsync(Selected.WorkItemId, HoldReasonInput, $"Hold from Control Center by {Services.Audit.AuditService.CurrentUsername}").ConfigureAwait(false);
                ProgressLabel = ok ? $"On Hold {holdId} {msg}" : $"Hold failed: {msg}";
                if (ok) await LoadAsync().ConfigureAwait(false);
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
                var (ok, msg) = await ProductionControlService.ReleaseHoldAsync(hold.HoldId, "Released from Control Center").ConfigureAwait(false);
                ProgressLabel = ok ? $"Hold Released {hold.HoldId}" : $"Release failed: {msg}";
                if (ok) await LoadAsync().ConfigureAwait(false);
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
                var (ok, msg, assignId) = await ProductionControlService.AssignAsync(Selected.WorkItemId, WorkstationInput, OperatorInput, "Assigned from Control Center").ConfigureAwait(false);
                ProgressLabel = ok ? $"Assigned {assignId} {msg}" : $"Assign failed: {msg}";
                if (ok) await LoadAsync().ConfigureAwait(false);
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
                var (ok, msg) = await ProductionControlService.UnassignAsync(Selected.WorkItemId).ConfigureAwait(false);
                ProgressLabel = ok ? $"Unassigned {msg}" : $"Unassign failed: {msg}";
                if (ok) await LoadAsync().ConfigureAwait(false);
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
                var (ok, msg) = await ProductionControlService.SetPriorityAsync(Selected.WorkItemId, prio, $"Priority changed to {prio} from Control Center").ConfigureAwait(false);
                ProgressLabel = ok ? msg : $"Priority failed: {msg}";
                if (ok) await LoadAsync().ConfigureAwait(false);
            }
            catch (Exception ex) { ProgressLabel = $"Priority failed: {ex.Message}"; }
            finally { IsRunning = false; }
        }
    }
}
