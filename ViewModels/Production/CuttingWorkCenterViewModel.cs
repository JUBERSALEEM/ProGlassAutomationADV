// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Production/CuttingWorkCenterViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 15 Cutting Work Center
//   Professional Cutting Work Center using existing WPF architecture, GDS semantic resources.
//   Header: Cutting Work Center
//   KPI: Pending, In Progress, Completed, Rework, QC Pending, Ready
//   Filters: Job, Customer, PI, Spec, Material, Color, Thickness, Status, Date
//   Main: Production cutting jobs/cards/table per existing conventions
//   Each work item: Job, Spec, Piece, Size, Material, Sheet, Rotation, Status, Progress
//   Actions: Open, Start, Pause, Complete, Breakage, Rework, QC
//   No giant ViewModels, reuse existing services.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Cutting;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Services.Production.Cutting;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Production
{
    public sealed class CuttingWorkCenterViewModel : ViewModelBase
    {
        // ── Header ───────────────────────────────────────────────────────
        public string Title => "Cutting Work Center";
        public string Subtitle => "Confirmed PI → JO → Production Release → Cutting Requirements → Material → 2D Optimization → Allocation → Cutting → QC → Ready";

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

        // ── Filters ──────────────────────────────────────────────────────
        private string _jobFilter = "";
        public string JobFilter { get => _jobFilter; set { if (_jobFilter == value) return; _jobFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Cutting filter"); } }

        private string _customerFilter = "";
        public string CustomerFilter { get => _customerFilter; set { if (_customerFilter == value) return; _customerFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Cutting filter"); } }

        private string _piFilter = "";
        public string PiFilter { get => _piFilter; set { if (_piFilter == value) return; _piFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Cutting filter"); } }

        private string _specFilter = "";
        public string SpecFilter { get => _specFilter; set { if (_specFilter == value) return; _specFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Cutting filter"); } }

        private string _materialFilter = "";
        public string MaterialFilter { get => _materialFilter; set { if (_materialFilter == value) return; _materialFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Cutting filter"); } }

        private string _colorFilter = "";
        public string ColorFilter { get => _colorFilter; set { if (_colorFilter == value) return; _colorFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Cutting filter"); } }

        private string _thicknessFilter = "";
        public string ThicknessFilter { get => _thicknessFilter; set { if (_thicknessFilter == value) return; _thicknessFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Cutting filter"); } }

        private string _statusFilter = "All";
        public string StatusFilter { get => _statusFilter; set { if (_statusFilter == value) return; _statusFilter = value; OnPropertyChanged(); AsyncUtility.SafeFireAndForget(LoadAsync, "Cutting filter"); } }

        public IReadOnlyList<string> StatusOptions { get; } = new[] { "All", "Pending", "InProgress", "Completed", "Rework", "QcPending", "Ready", "Breakage", "Foc", "Cancelled" };

        // ── Main work area ───────────────────────────────────────────────
        public ObservableCollection<SavedCuttingSession> CuttingSessions { get; } = new();
        public ObservableCollection<SavedCuttingSheet> CuttingSheets { get; } = new();
        public ObservableCollection<SavedCuttingPlacedPiece> PlacedPieces { get; } = new();
        public ObservableCollection<CuttingRequirement> CuttingRequirements { get; } = new();

        private SavedCuttingSession? _selectedSession;
        public SavedCuttingSession? SelectedSession
        {
            get => _selectedSession;
            set
            {
                if (_selectedSession == value) return;
                _selectedSession = value;
                OnPropertyChanged();
                if (value != null)
                    AsyncUtility.SafeFireAndForget(() => LoadSessionDetailsAsync(value.CuttingSessionId), "Cutting session details");
            }
        }

        private SavedCuttingPlacedPiece? _selectedPlacedPiece;
        public SavedCuttingPlacedPiece? SelectedPlacedPiece
        {
            get => _selectedPlacedPiece;
            set
            {
                if (_selectedPlacedPiece == value) return;
                _selectedPlacedPiece = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPlacedSelection));
                UpdateTraceability();
            }
        }

        public bool HasPlacedSelection => SelectedPlacedPiece != null;

        private string _traceabilityLabel = "—";
        public string TraceabilityLabel
        {
            get => _traceabilityLabel;
            private set { if (_traceabilityLabel == value) return; _traceabilityLabel = value; OnPropertyChanged(); }
        }

        // ── Progress / Cancellation ──────────────────────────────────────
        private bool _isRunning;
        public bool IsRunning { get => _isRunning; private set { if (_isRunning == value) return; _isRunning = value; OnPropertyChanged(); } }

        private string _progressLabel = "";
        public string ProgressLabel { get => _progressLabel; private set { if (_progressLabel == value) return; _progressLabel = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasProgress)); } }

        public bool HasProgress => !string.IsNullOrWhiteSpace(_progressLabel);

        private CancellationTokenSource? _cts;

        // ── Workflow settings ────────────────────────────────────────────
        private string _jobRefInput = "";
        public string JobRefInput { get => _jobRefInput; set { if (_jobRefInput == value) return; _jobRefInput = value; OnPropertyChanged(); } }

        private RotationPolicy _rotation = RotationPolicy.Free90;
        public RotationPolicy Rotation { get => _rotation; set { if (_rotation == value) return; _rotation = value; OnPropertyChanged(); } }

        public IReadOnlyList<RotationPolicy> RotationOptions { get; } = new[] { RotationPolicy.Locked, RotationPolicy.Free90 };

        private int _effort = 3;
        public int Effort { get => _effort; set { if (_effort == value) return; _effort = value; OnPropertyChanged(); } }

        // ── Commands ─────────────────────────────────────────────────────
        public ICommand LoadCommand { get; }
        public ICommand RunCuttingWorkflowCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand CompletePieceCommand { get; }
        public ICommand RecordBreakageCommand { get; }
        public ICommand RecordFocCommand { get; }
        public ICommand CompleteSessionCommand { get; }

        private string _actionMessage = "";
        public string ActionMessage { get => _actionMessage; private set { if (_actionMessage == value) return; _actionMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasActionMessage)); } }

        public bool HasActionMessage => !string.IsNullOrWhiteSpace(_actionMessage);

        public CuttingWorkCenterViewModel()
        {
            LoadCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(LoadAsync, "Cutting load"));
            ProductionRepository.WorkItemsChanged += OnLiveWorkItemsChanged;
            AsyncUtility.SafeFireAndForget(LoadAsync, "Cutting work center initial load");
            RunCuttingWorkflowCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(RunWorkflowAsync, "Cutting workflow run"),
                _ => !IsRunning && !string.IsNullOrWhiteSpace(JobRefInput));
            CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsRunning);
            CompletePieceCommand = new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(() => CompletePieceAsync(p), "Cutting complete piece"),
                p => p is SavedCuttingPlacedPiece pp && pp.Status == (int)CuttingPieceStatus.Allocated && !IsRunning);
            RecordBreakageCommand = new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(() => RecordBreakageAsync(p, isFoc: false), "Cutting breakage"),
                p => p is SavedCuttingPlacedPiece && !IsRunning);
            RecordFocCommand = new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(() => RecordBreakageAsync(p, isFoc: true), "Cutting FOC"),
                p => p is SavedCuttingPlacedPiece && !IsRunning);
            CompleteSessionCommand = new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(() => CompleteSessionAsync(p), "Cutting complete session"),
                p => p is SavedCuttingSession && !IsRunning);

            AsyncUtility.SafeFireAndForget(LoadAsync, "Cutting initial load");
        }

        
        /// <summary>P1 sync fix: live-refresh this page whenever ANY floor
        /// screen saves work — same contract the eight stage pages get from
        /// ProductionStageViewModelBase. Marshals to the UI thread.</summary>
        private void OnLiveWorkItemsChanged()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            void Reload() => AsyncUtility.SafeFireAndForget(LoadAsync, "Cutting work center live refresh");
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
                ProgressLabel = "Loading cutting sessions…";

                var sessions = await CuttingWorkflowService.GetSessionsByJobAsync(JobFilter, CancellationToken.None).ConfigureAwait(true);

                // Apply filters
                var filtered = sessions.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(JobFilter))
                    filtered = filtered.Where(s => s.JobRef.Contains(JobFilter, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(PiFilter))
                    filtered = filtered.Where(s => s.SourceProformaId.Contains(PiFilter, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(SpecFilter))
                    filtered = filtered.Where(s => s.MaterialGroupId.Contains(SpecFilter, StringComparison.OrdinalIgnoreCase));

                // Status filter
                if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "All")
                {
                    if (Enum.TryParse<CuttingSessionStatus>(StatusFilter, out var status))
                        filtered = filtered.Where(s => s.Status == (int)status);
                }

                CuttingSessions.Clear();
                foreach (var s in filtered.OrderByDescending(s => s.CreatedAt).Take(100))
                    CuttingSessions.Add(s);

                // KPIs
                var allPieces = new List<SavedCuttingPlacedPiece>();
                foreach (var sess in CuttingSessions)
                {
                    var pieces = await CuttingWorkflowService.GetPlacedPiecesBySessionAsync(sess.CuttingSessionId).ConfigureAwait(true);
                    allPieces.AddRange(pieces);
                }

                PendingCount = allPieces.Count(p => p.Status == (int)CuttingPieceStatus.Pending || p.Status == (int)CuttingPieceStatus.Allocated);
                InProgressCount = allPieces.Count(p => p.Status == (int)CuttingPieceStatus.Cutting);
                CompletedCount = allPieces.Count(p => p.Status == (int)CuttingPieceStatus.Completed);
                ReworkCount = allPieces.Count(p => p.Status == (int)CuttingPieceStatus.Rework);
                QcPendingCount = allPieces.Count(p => p.Status == (int)CuttingPieceStatus.QcPending);
                ReadyCount = allPieces.Count(p => p.Status == (int)CuttingPieceStatus.QcPassed);

                int required = allPieces.Count;
                int completed = CompletedCount;
                int remaining = Math.Max(0, required - completed);

                KpiSummary = $"Required {required} Completed {completed} Remaining {remaining} — Pending {PendingCount} InProgress {InProgressCount} Rework {ReworkCount} QC Pending {QcPendingCount} Ready {ReadyCount}";

                ProgressLabel = $"Loaded {CuttingSessions.Count} sessions, {allPieces.Count} pieces";
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

        private async Task LoadSessionDetailsAsync(string cuttingSessionId)
        {
            try
            {
                IsRunning = true;
                ProgressLabel = $"Loading session {cuttingSessionId}…";

                var sheets = await CuttingWorkflowService.GetSheetsBySessionAsync(cuttingSessionId).ConfigureAwait(true);
                var pieces = await CuttingWorkflowService.GetPlacedPiecesBySessionAsync(cuttingSessionId).ConfigureAwait(true);

                CuttingSheets.Clear();
                foreach (var sh in sheets) CuttingSheets.Add(sh);

                PlacedPieces.Clear();
                foreach (var p in pieces) PlacedPieces.Add(p);

                ProgressLabel = $"Session {cuttingSessionId}: {sheets.Count} sheets, {pieces.Count} pieces";

                if (pieces.Count > 0)
                {
                    SelectedPlacedPiece = pieces.First();
                }
            }
            catch (Exception ex)
            {
                ActionMessage = $"Load session failed: {ex.Message}";
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task RunWorkflowAsync()
        {
            if (string.IsNullOrWhiteSpace(JobRefInput))
            {
                ActionMessage = "Enter Job Ref.";
                return;
            }

            IsRunning = true;
            ProgressLabel = $"Running cutting workflow for {JobRefInput}…";
            _cts = new CancellationTokenSource();

            var progress = new Progress<CuttingWorkflowProgress>(p =>
            {
                ProgressLabel = $"{p.Stage} {p.Percent}% — {p.Message}";
            });

            try
            {
                var result = await CuttingWorkflowService.RunAsync(
                    JobRefInput,
                    rotation: Rotation,
                    effortLevel: Effort,
                    progress: progress,
                    ct: _cts.Token).ConfigureAwait(true);

                ActionMessage = result.Message;

                if (result.Warnings.Count > 0)
                    ActionMessage += $"\nWarnings: {string.Join("; ", result.Warnings.Take(3))}";

                await LoadAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                ActionMessage = "Cutting workflow cancelled.";
            }
            catch (Exception ex)
            {
                ActionMessage = $"Workflow failed: {ex.Message}";
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task CompletePieceAsync(object? parameter)
        {
            if (parameter is not SavedCuttingPlacedPiece piece) return;

            IsRunning = true;
            ProgressLabel = $"Completing {piece.PieceLabel}…";
            _cts = new CancellationTokenSource();

            try
            {
                var (ok, msg) = await CuttingCompletionService.CompletePieceAsync(piece.PlacedPieceId, _cts.Token).ConfigureAwait(true);
                ActionMessage = msg;
                if (ok && SelectedSession != null)
                    await LoadSessionDetailsAsync(SelectedSession.CuttingSessionId).ConfigureAwait(true);
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task RecordBreakageAsync(object? parameter, bool isFoc)
        {
            if (parameter is not SavedCuttingPlacedPiece piece) return;

            var answer = System.Windows.MessageBox.Show(
                $"{(isFoc ? "FOC" : "Breakage")} for {piece.PieceLabel} {piece.WidthMm}×{piece.HeightMm}?\n\nTrace: Original Piece → Breakage → Replacement → Cutting → QC\nSellingValue={(isFoc ? "0 (FOC)" : "real")} ActualCost remains real.",
                isFoc ? "Record FOC" : "Record Breakage",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (answer != System.Windows.MessageBoxResult.Yes) return;

            IsRunning = true;
            ProgressLabel = $"Recording {(isFoc ? "FOC" : "breakage")} for {piece.PieceLabel}…";
            _cts = new CancellationTokenSource();

            try
            {
                var (ok, msg) = await CuttingCompletionService.RecordBreakageDuringCuttingAsync(piece.PlacedPieceId, isFoc ? "FOC during cutting" : "Breakage during cutting", isFoc, _cts.Token).ConfigureAwait(true);
                ActionMessage = msg;
                if (ok && SelectedSession != null)
                    await LoadSessionDetailsAsync(SelectedSession.CuttingSessionId).ConfigureAwait(true);
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task CompleteSessionAsync(object? parameter)
        {
            if (parameter is not SavedCuttingSession session) return;

            var answer = System.Windows.MessageBox.Show(
                $"Complete all allocated pieces for session {session.CuttingSessionId}?\n\nSupports partial: Required {session.TotalPieces} will track completed vs remaining.",
                "Complete session",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (answer != System.Windows.MessageBoxResult.Yes) return;

            IsRunning = true;
            ProgressLabel = $"Completing session {session.CuttingSessionId}…";
            _cts = new CancellationTokenSource();

            var prog = new Progress<string>(s => ProgressLabel = s);

            try
            {
                var (ok, msg) = await CuttingCompletionService.CompleteSessionAsync(session.CuttingSessionId, prog, _cts.Token).ConfigureAwait(true);
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

        // ── Traceability ─────────────────────────────────────────────────
        private void UpdateTraceability()
        {
            if (SelectedPlacedPiece == null)
            {
                TraceabilityLabel = "—";
                return;
            }

            var p = SelectedPlacedPiece;
            TraceabilityLabel = $"PI {p.JobRef} → JO {p.JobRef} → Spec {p.SpecGroupNumber}/{p.SerialNumber} {p.SpecName} → Production Piece {p.PieceId} → Cutting Session {p.CuttingSessionId} → Sheet {p.CuttingSheetId} → Placed Piece {p.PieceLabel} {p.WidthMm}×{p.HeightMm} {(p.IsRotated ? "90°" : "0°")} at {p.X},{p.Y} seq {p.CutSequence} → WorkItem {p.SourceWorkItemId} → Reservation {p.ReservationId} → Allocation {p.AllocationId}";
        }
    }
}
