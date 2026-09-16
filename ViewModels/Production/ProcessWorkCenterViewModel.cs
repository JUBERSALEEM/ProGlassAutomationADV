// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Production/ProcessWorkCenterViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 16 Reusable Production Process Work Center
//   Create reusable Production Process Work Center, do NOT create eight completely unrelated screens.
//   UI should allow selecting/filtering by Process Job Customer PI JO Specification Material Color Thickness Status Date
//   Display Job Spec Piece Process Previous stage Current status Qty Progress QC Operator
//   Actions Start Pause Complete Breakage Rework QC View Traceability
//   Process-specific details via detail panel/dialog per existing UI patterns.
//   Use GDS authoritative, no new theme dictionary, no hardcoded colors/typography, no separate production card system.
//   Use existing shared surface hierarchy Application Background → Surface Primary → Surface Secondary → Detail → Elevated
//   Support Normal NormalIndustrial Monet CrystalFluid Ios26 Light/Dark Glass Blue Emerald Amber Rose Violet Teal Compact Normal Comfortable Spacious
//   Performance async/await CancellationToken IProgress.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Process;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Services.Production.Process;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Production
{
    public sealed class ProcessWorkCenterViewModel : ViewModelBase
    {
        // ── Header ───────────────────────────────────────────────────────
        public string Title => "Production Process Work Center";
        public string Subtitle => "Cutting → Holes/Cutouts → Polish → Edge Deletion → Mitring → Tempering → Pairing → Doubling/Lamination → QC → Ready — specification-driven route, shared execution framework";

        // ── Filters ──────────────────────────────────────────────────────
        private string _processFilter = "All";
        public string ProcessFilter
        {
            get => _processFilter;
            set { if (_processFilter == value) return; _processFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Process filter"); }
        }

        public IReadOnlyList<string> ProcessOptions { get; } = new[] { "All", "Cutting", "HolesCutouts", "Polish", "EdgeDeletion", "Mitring", "Tempering", "Pairing", "DoublingLamination" };

        private string _jobFilter = "";
        public string JobFilter { get => _jobFilter; set { if (_jobFilter == value) return; _jobFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Process filter"); } }

        private string _customerFilter = "";
        public string CustomerFilter { get => _customerFilter; set { if (_customerFilter == value) return; _customerFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Process filter"); } }

        private string _piFilter = "";
        public string PiFilter { get => _piFilter; set { if (_piFilter == value) return; _piFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Process filter"); } }

        private string _specFilter = "";
        public string SpecFilter { get => _specFilter; set { if (_specFilter == value) return; _specFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Process filter"); } }

        private string _materialFilter = "";
        public string MaterialFilter { get => _materialFilter; set { if (_materialFilter == value) return; _materialFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Process filter"); } }

        private string _colorFilter = "";
        public string ColorFilter { get => _colorFilter; set { if (_colorFilter == value) return; _colorFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Process filter"); } }

        private string _thicknessFilter = "";
        public string ThicknessFilter { get => _thicknessFilter; set { if (_thicknessFilter == value) return; _thicknessFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Process filter"); } }

        private string _statusFilter = "All";
        public string StatusFilter { get => _statusFilter; set { if (_statusFilter == value) return; _statusFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Process filter"); } }

        public IReadOnlyList<string> StatusOptions { get; } = new[] { "All", "Pending", "InProgress", "Completed", "Paused", "Rework", "Rejected", "Cancelled", "QcPending", "QcFailed" };

        // ── KPIs ─────────────────────────────────────────────────────────
        private int _pendingCount, _inProgressCount, _completedCount, _reworkCount, _qcPendingCount, _readyCount;
        public int PendingCount { get => _pendingCount; private set { if (_pendingCount == value) return; _pendingCount = value; OnPropertyChanged(); } }
        public int InProgressCount { get => _inProgressCount; private set { if (_inProgressCount == value) return; _inProgressCount = value; OnPropertyChanged(); } }
        public int CompletedCount { get => _completedCount; private set { if (_completedCount == value) return; _completedCount = value; OnPropertyChanged(); } }
        public int ReworkCount { get => _reworkCount; private set { if (_reworkCount == value) return; _reworkCount = value; OnPropertyChanged(); } }
        public int QcPendingCount { get => _qcPendingCount; private set { if (_qcPendingCount == value) return; _qcPendingCount = value; OnPropertyChanged(); } }
        public int ReadyCount { get => _readyCount; private set { if (_readyCount == value) return; _readyCount = value; OnPropertyChanged(); } }

        private string _kpiSummary = "—";
        public string KpiSummary { get => _kpiSummary; private set { if (_kpiSummary == value) return; _kpiSummary = value; OnPropertyChanged(); } }

        // ── Main work area ───────────────────────────────────────────────
        public ObservableCollection<SavedProcessExecution> Executions { get; } = new();
        public ObservableCollection<SavedProcessPairing> Pairings { get; } = new();
        public ObservableCollection<SavedProcessAssembly> Assemblies { get; } = new();
        public ObservableCollection<SavedProductionWorkItem> WorkItems { get; } = new();

        private SavedProcessExecution? _selectedExecution;
        public SavedProcessExecution? SelectedExecution
        {
            get => _selectedExecution;
            set
            {
                if (_selectedExecution == value) return;
                _selectedExecution = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasExecutionSelection));
                UpdateTraceability();
                UpdateProcessDetail();
            }
        }

        public bool HasExecutionSelection => SelectedExecution != null;

        private string _traceabilityLabel = "—";
        public string TraceabilityLabel { get => _traceabilityLabel; private set { if (_traceabilityLabel == value) return; _traceabilityLabel = value; OnPropertyChanged(); } }

        private string _processDetailLabel = "—";
        public string ProcessDetailLabel { get => _processDetailLabel; private set { if (_processDetailLabel == value) return; _processDetailLabel = value; OnPropertyChanged(); } }

        private string _routeLabel = "—";
        public string RouteLabel { get => _routeLabel; private set { if (_routeLabel == value) return; _routeLabel = value; OnPropertyChanged(); } }

        // ── Progress / Cancellation ──────────────────────────────────────
        private bool _isRunning;
        public bool IsRunning { get => _isRunning; private set { if (_isRunning == value) return; _isRunning = value; OnPropertyChanged(); } }

        private string _progressLabel = "";
        public string ProgressLabel { get => _progressLabel; private set { if (_progressLabel == value) return; _progressLabel = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasProgress)); } }

        public bool HasProgress => !string.IsNullOrWhiteSpace(_progressLabel);

        private CancellationTokenSource? _cts;

        // ── Workflow input ───────────────────────────────────────────────
        private string _jobRefInput = "";
        public string JobRefInput { get => _jobRefInput; set { if (_jobRefInput == value) return; _jobRefInput = value; OnPropertyChanged(); } }

        private string _processTypeInput = "Tempering";
        public string ProcessTypeInput { get => _processTypeInput; set { if (_processTypeInput == value) return; _processTypeInput = value; OnPropertyChanged(); } }

        // ── Commands ─────────────────────────────────────────────────────
        public ICommand LoadCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand CompleteCommand { get; }
        public ICommand RecordBreakageCommand { get; }
        public ICommand RecordReworkCommand { get; }
        public ICommand RecordFocCommand { get; }
        public ICommand QcPassedCommand { get; }
        public ICommand QcFailedCommand { get; }
        public ICommand PairCommand { get; }
        public ICommand AssembleCommand { get; }
        public ICommand CancelCommand { get; }

        private string _actionMessage = "";
        public string ActionMessage { get => _actionMessage; private set { if (_actionMessage == value) return; _actionMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasActionMessage)); } }

        public bool HasActionMessage => !string.IsNullOrWhiteSpace(_actionMessage);

        public ProcessWorkCenterViewModel()
        {
            LoadCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(LoadAsync, "Process load"));
            ProductionRepository.WorkItemsChanged += OnLiveWorkItemsChanged;
            AsyncUtility.SafeFireAndForget(LoadAsync, "Process work center initial load");
            StartCommand = new RelayCommand(p => AsyncUtility.SafeFireAndForget(() => StartAsync(p), "Process start"), _ => !IsRunning);
            PauseCommand = new RelayCommand(p => AsyncUtility.SafeFireAndForget(() => PauseAsync(p), "Process pause"), p => p is SavedProcessExecution e && e.Status == (int)WorkItemStatus.InProgress);
            CompleteCommand = new RelayCommand(p => AsyncUtility.SafeFireAndForget(() => CompleteAsync(p), "Process complete"), p => p is SavedProcessExecution e && e.Status == (int)WorkItemStatus.InProgress);
            RecordBreakageCommand = new RelayCommand(p => AsyncUtility.SafeFireAndForget(() => RecordBreakageAsync(p, isFoc: false, entryType: 1), "Process breakage"), p => p is SavedProcessExecution && !IsRunning);
            RecordReworkCommand = new RelayCommand(p => AsyncUtility.SafeFireAndForget(() => RecordBreakageAsync(p, isFoc: false, entryType: 2), "Process rework"), p => p is SavedProcessExecution && !IsRunning);
            RecordFocCommand = new RelayCommand(p => AsyncUtility.SafeFireAndForget(() => RecordBreakageAsync(p, isFoc: true, entryType: 1), "Process FOC"), p => p is SavedProcessExecution && !IsRunning);
            QcPassedCommand = new RelayCommand(p => AsyncUtility.SafeFireAndForget(() => RecordQcAsync(p, passed: true), "Process QC passed"), p => p is SavedProcessExecution e && e.Status == (int)WorkItemStatus.Completed);
            QcFailedCommand = new RelayCommand(p => AsyncUtility.SafeFireAndForget(() => RecordQcAsync(p, passed: false), "Process QC failed"), p => p is SavedProcessExecution e && e.Status == (int)WorkItemStatus.Completed);
            PairCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(PairAsync, "Process pairing"), _ => !IsRunning && !string.IsNullOrWhiteSpace(JobRefInput));
            AssembleCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(AssembleAsync, "Process assembly"), _ => !IsRunning && !string.IsNullOrWhiteSpace(JobRefInput));
            CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsRunning);

            AsyncUtility.SafeFireAndForget(LoadAsync, "Process initial load");
        }

        
        /// <summary>P1 sync fix: live-refresh this page whenever ANY floor
        /// screen saves work — same contract the eight stage pages get from
        /// ProductionStageViewModelBase. Marshals to the UI thread.</summary>
        private void OnLiveWorkItemsChanged()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            void Reload() => AsyncUtility.SafeFireAndForget(LoadAsync, "Process work center live refresh");
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.BeginInvoke(new System.Action(Reload));
            else
                Reload();
        }

        public async Task LoadAsync()
        {
            try
            {
                IsRunning = true;
                ProgressLabel = "Loading process executions…";

                ProductionRepository.EnsureInitialized();
                using var db = new ProductionDbContext();

                var query = db.ProcessExecutions.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(JobFilter))
                    query = query.Where(e => e.JobRef.Contains(JobFilter));
                if (!string.IsNullOrWhiteSpace(PiFilter))
                    query = query.Where(e => e.SourceProformaId.Contains(PiFilter));
                if (!string.IsNullOrWhiteSpace(SpecFilter))
                    query = query.Where(e => e.SpecName.Contains(SpecFilter));

                if (ProcessFilter != "All" && Enum.TryParse<ProductionProcessType>(ProcessFilter, out var pt))
                    query = query.Where(e => e.ProcessType == (int)pt);

                if (StatusFilter != "All" && Enum.TryParse<WorkItemStatus>(StatusFilter, out var st))
                    query = query.Where(e => e.Status == (int)st);

                var execs = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .ToListAsync(query.OrderByDescending(e => e.CreatedAt).Take(200)).ConfigureAwait(true);

                Executions.Clear();
                foreach (var e in execs) Executions.Add(e);

                // KPIs
                PendingCount = execs.Count(e => e.Status == (int)WorkItemStatus.Pending);
                InProgressCount = execs.Count(e => e.Status == (int)WorkItemStatus.InProgress);
                CompletedCount = execs.Count(e => e.Status == (int)WorkItemStatus.Completed);
                ReworkCount = execs.Count(e => e.Status == (int)WorkItemStatus.Rework);
                QcPendingCount = execs.Count(e => e.QcStatus == 0 && e.Status == (int)WorkItemStatus.Completed);
                ReadyCount = execs.Count(e => e.QcStatus == 1);

                int required = execs.Sum(e => e.QtyRequired);
                int completed = execs.Where(e => e.Status == (int)WorkItemStatus.Completed).Sum(e => e.QtyDone);
                int remaining = Math.Max(0, required - completed);

                KpiSummary = $"Required {required} Completed {completed} Remaining {remaining} — Pending {PendingCount} InProgress {InProgressCount} Rework {ReworkCount} QC Pending {QcPendingCount} Ready {ReadyCount}";

                // Route examples
                var examples = ProductionProcessRoutingService.GetRouteExamples();
                RouteLabel = string.Join(" | ", examples.Select(kv => $"{kv.Key}: {string.Join("→", kv.Value)}"));

                // Load pairings/assemblies for selected job filter if any
                if (!string.IsNullOrWhiteSpace(JobFilter))
                {
                    var pairings = await PairingService.GetByJobAsync(JobFilter).ConfigureAwait(true);
                    Pairings.Clear();
                    foreach (var p in pairings) Pairings.Add(p);

                    var assemblies = await DoublingLaminationService.GetByJobAsync(JobFilter).ConfigureAwait(true);
                    Assemblies.Clear();
                    foreach (var a in assemblies) Assemblies.Add(a);
                }

                ProgressLabel = $"Loaded {execs.Count} executions, {Pairings.Count} pairings, {Assemblies.Count} assemblies";
            }
            catch (Exception ex)
            {
                ActionMessage = $"Load failed: {ex.Message}";
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task StartAsync(object? parameter)
        {
            if (parameter is not SavedProductionWorkItem wi)
            {
                // Try from selected execution
                if (SelectedExecution != null)
                {
                    wi = new SavedProductionWorkItem
                    {
                        Id = SelectedExecution.OutputWorkItemId,
                        JobRef = SelectedExecution.JobRef,
                        SpecGroupNumber = SelectedExecution.SpecGroupNumber,
                        SerialNumber = SelectedExecution.SerialNumber,
                        Position = SelectedExecution.Position,
                        Stage = SelectedExecution.ProcessType,
                        Qty = SelectedExecution.QtyRequired
                    };
                }
                else
                {
                    ActionMessage = "Select a work item to start.";
                    return;
                }
            }

            IsRunning = true;
            ProgressLabel = $"Starting {((ProductionProcessType)wi.Stage)} for {wi.JobRef} Spec {wi.SpecGroupNumber} Serial {wi.SerialNumber}…";
            _cts = new CancellationTokenSource();

            try
            {
                string pieceId = $"{wi.JobRef}|{wi.SpecGroupNumber}|{wi.SerialNumber}|{wi.Position}|{wi.WidthMm}*{wi.HeightMm}|0";
                var (ok, msg, exec) = await ProductionProcessExecutionService.StartAsync(
                    wi.JobRef, wi.SpecGroupNumber, wi.SerialNumber, wi.Position,
                    (ProductionProcessType)wi.Stage, wi.Qty, pieceId,
                    inputWorkItemId: 0, outputWorkItemId: wi.Id,
                    processDetailJson: "", isFoc: wi.IsFoc, notes: $"Started {((ProductionProcessType)wi.Stage)}",
                    ct: _cts.Token).ConfigureAwait(true);

                ActionMessage = msg;
                if (ok) await LoadAsync().ConfigureAwait(true);
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task PauseAsync(object? parameter)
        {
            if (parameter is not SavedProcessExecution exec) return;
            var (ok, msg) = await ProductionProcessExecutionService.PauseAsync(exec.ExecutionId, "Paused by operator").ConfigureAwait(true);
            ActionMessage = msg;
            if (ok) await LoadAsync().ConfigureAwait(true);
        }

        private async Task CompleteAsync(object? parameter)
        {
            if (parameter is not SavedProcessExecution exec) return;
            IsRunning = true;
            ProgressLabel = $"Completing {((ProductionProcessType)exec.ProcessType)} {exec.PieceId}…";
            _cts = new CancellationTokenSource();
            try
            {
                var (ok, msg) = await ProductionProcessExecutionService.CompleteAsync(exec.ExecutionId, 1, $"Completed {exec.PieceId}", ct: _cts.Token).ConfigureAwait(true);
                ActionMessage = msg;
                if (ok) await LoadAsync().ConfigureAwait(true);
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task RecordBreakageAsync(object? parameter, bool isFoc, int entryType)
        {
            if (parameter is not SavedProcessExecution exec) return;

            var answer = System.Windows.MessageBox.Show(
                $"{(isFoc ? "FOC" : entryType == 1 ? "Breakage" : "Rework")} for {exec.PieceId} {((ProductionProcessType)exec.ProcessType)}?\n\nTrace: Original Piece → Failure → Breakage/Rework → Replacement → Required Process Again\nSellingValue={(isFoc ? "0 (FOC)" : "real")} ActualCost remains real.",
                isFoc ? "Record FOC" : entryType == 1 ? "Record Breakage" : "Record Rework",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (answer != System.Windows.MessageBoxResult.Yes) return;

            IsRunning = true;
            ProgressLabel = $"Recording {(isFoc ? "FOC" : entryType == 1 ? "breakage" : "rework")} for {exec.PieceId}…";
            _cts = new CancellationTokenSource();

            try
            {
                var (ok, msg) = await ProductionProcessExecutionService.RecordBreakageAsync(exec.ExecutionId, isFoc ? "FOC during process" : $"{(entryType == 1 ? "Breakage" : "Rework")} during {((ProductionProcessType)exec.ProcessType)}", entryType, isFoc, _cts.Token).ConfigureAwait(true);
                ActionMessage = msg;
                if (ok) await LoadAsync().ConfigureAwait(true);
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task RecordQcAsync(object? parameter, bool passed)
        {
            if (parameter is not SavedProcessExecution exec) return;

            IsRunning = true;
            ProgressLabel = $"Recording QC {(passed ? "Passed" : "Failed")} for {exec.PieceId}…";
            _cts = new CancellationTokenSource();

            try
            {
                var (ok, qcEntry, msg) = await ProductionQcService.RecordQcAsync(
                    exec.OutputWorkItemId, passed ? QcResult.Passed : QcResult.Failed,
                    reason: passed ? "QC passed" : "QC failed during process",
                    notes: $"QC for {((ProductionProcessType)exec.ProcessType)} {exec.PieceId}",
                    ct: _cts.Token).ConfigureAwait(true);

                if (ok && qcEntry != null)
                {
                    // Update execution QcStatus
                    using var db = new ProductionDbContext();
                    var e = await db.ProcessExecutions.FirstOrDefaultAsync(x => x.ExecutionId == exec.ExecutionId, _cts.Token).ConfigureAwait(false);
                    if (e != null)
                    {
                        e.QcStatus = (int)(passed ? QcResult.Passed : QcResult.Failed);
                        e.QcId = qcEntry.QcId;
                        e.UpdatedAt = DateTime.Now;
                        await db.SaveChangesAsync(_cts.Token).ConfigureAwait(false);
                    }
                }

                ActionMessage = msg;
                await LoadAsync().ConfigureAwait(true);
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task PairAsync()
        {
            if (string.IsNullOrWhiteSpace(JobRefInput))
            {
                ActionMessage = "Enter Job Ref for pairing.";
                return;
            }

            IsRunning = true;
            ProgressLabel = $"Pairing DGU components for {JobRefInput}…";
            _cts = new CancellationTokenSource();

            try
            {
                // For simplicity, pair first unit found
                ProductionRepository.EnsureInitialized();
                using var db = new ProductionDbContext();
                var firstUnit = await db.WorkItems.AsNoTracking()
                    .Where(w => w.JobRef == JobRefInput && !w.IsCancelled)
                    .OrderBy(w => w.SpecGroupNumber)
                    .ThenBy(w => w.SerialNumber)
                    .FirstOrDefaultAsync(_cts.Token).ConfigureAwait(false);

                if (firstUnit == null)
                {
                    ActionMessage = $"No work items found for {JobRefInput}";
                    return;
                }

                var pairingResult = await PairingService.PairAsync(JobRefInput, firstUnit.SpecGroupNumber, firstUnit.SerialNumber, _cts.Token).ConfigureAwait(true);
                ActionMessage = pairingResult.Message;
                if (pairingResult.Ok) await LoadAsync().ConfigureAwait(true);
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task AssembleAsync()
        {
            if (string.IsNullOrWhiteSpace(JobRefInput))
            {
                ActionMessage = "Enter Job Ref for assembly.";
                return;
            }

            IsRunning = true;
            ProgressLabel = $"Assembling DGU/Lamination for {JobRefInput}…";
            _cts = new CancellationTokenSource();

            try
            {
                ProductionRepository.EnsureInitialized();
                using var db = new ProductionDbContext();
                var firstUnit = await db.WorkItems.AsNoTracking()
                    .Where(w => w.JobRef == JobRefInput && !w.IsCancelled)
                    .OrderBy(w => w.SpecGroupNumber)
                    .ThenBy(w => w.SerialNumber)
                    .FirstOrDefaultAsync(_cts.Token).ConfigureAwait(false);

                if (firstUnit == null)
                {
                    ActionMessage = $"No work items found for {JobRefInput}";
                    return;
                }

                string type = firstUnit.BuildType.Equals("Lam", StringComparison.OrdinalIgnoreCase) ? "Lamination" : "DGU";

                var assemblyResult = await DoublingLaminationService.AssembleAsync(
                    JobRefInput, firstUnit.SpecGroupNumber, firstUnit.SerialNumber, type,
                    spacerInfo: "12mm Air", interlayerInfo: type == "Lamination" ? "PVB 0.76mm Clear" : "",
                    ct: _cts.Token).ConfigureAwait(true);

                ActionMessage = assemblyResult.Message;
                if (assemblyResult.Ok) await LoadAsync().ConfigureAwait(true);
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void UpdateTraceability()
        {
            if (SelectedExecution == null)
            {
                TraceabilityLabel = "—";
                return;
            }

            var e = SelectedExecution;
            TraceabilityLabel = $"PI {e.SourceProformaId} → JO {e.JobRef} → Spec {e.SpecGroupNumber}/{e.SerialNumber} {e.SpecName} → Position {e.Position} → Production Piece {e.PieceId} → Input {e.InputPieceId} → Process {((ProductionProcessType)e.ProcessType)} Execution {e.ExecutionId} Seq {e.Sequence} → Output {e.OutputPieceId} → WorkItem {e.OutputWorkItemId} → Previous {e.PreviousStage} → Next {e.NextStage} → Parent {e.ParentExecutionId} Rework {e.ReworkSequence} IsFoc {e.IsFoc} → QC {e.QcId} Status {e.QcStatus}";
        }

        private void UpdateProcessDetail()
        {
            if (SelectedExecution == null)
            {
                ProcessDetailLabel = "—";
                return;
            }

            var e = SelectedExecution;
            string summary = ProcessExecutionFactory.SummarizeDetail(e.ProcessDetailJson, (ProductionProcessType)e.ProcessType);
            ProcessDetailLabel = $"{((ProductionProcessType)e.ProcessType)} — {summary} — Qty {e.QtyDone}/{e.QtyRequired} Status {((WorkItemStatus)e.Status)} Operator {e.StartedBy}→{e.CompletedBy}";
        }
    }
}
