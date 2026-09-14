// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Production/ProductionStageViewModelBase.Phase14.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 14 extensions (breakage/rework/FOC, QC,
//         material allocation, actual cost, cancellation, progress)
//   Extends existing ProductionStageViewModelBase without rewriting.
//   All new functionality is additive, traceable, idempotent, auditable.
//   Uses existing GDS semantic tokens, no new theme dict.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Production
{
    public abstract partial class ProductionStageViewModelBase
    {
        // ── Phase 14: Breakage / Rework / FOC ────────────────────────────

        private string _breakageReason = "";
        public string BreakageReason
        {
            get => _breakageReason;
            set { if (_breakageReason == value) return; _breakageReason = value; OnPropertyChanged(); }
        }

        private string _breakageQtyInput = "1";
        public string BreakageQtyInput
        {
            get => _breakageQtyInput;
            set { if (_breakageQtyInput == value) return; _breakageQtyInput = value; OnPropertyChanged(); }
        }

        private bool _breakageIsFoc;
        public bool BreakageIsFoc
        {
            get => _breakageIsFoc;
            set { if (_breakageIsFoc == value) return; _breakageIsFoc = value; OnPropertyChanged(); }
        }

        public ICommand RecordBreakageCommand { get; private set; }
        public ICommand RecordReworkCommand { get; private set; }
        public ICommand RecordFocCommand { get; private set; }

        // ── Phase 14: QC ─────────────────────────────────────────────────

        private string _qcReason = "";
        public string QcReason
        {
            get => _qcReason;
            set { if (_qcReason == value) return; _qcReason = value; OnPropertyChanged(); }
        }

        public ICommand RecordQcPassedCommand { get; private set; }
        public ICommand RecordQcFailedCommand { get; private set; }
        public ICommand RecordQcReworkCommand { get; private set; }

        public ObservableCollection<SavedProductionQcEntry> QcEntries { get; } = new();

        private string _qcSummaryLabel = "—";
        public string QcSummaryLabel
        {
            get => _qcSummaryLabel;
            private set { if (_qcSummaryLabel == value) return; _qcSummaryLabel = value; OnPropertyChanged(); }
        }

        // ── Phase 14: Material Reservation / Allocation ──────────────────

        public ObservableCollection<SavedMaterialReservation> Reservations { get; } = new();
        public ObservableCollection<SavedMaterialAllocation> Allocations { get; } = new();

        private string _reservationSummaryLabel = "—";
        public string ReservationSummaryLabel
        {
            get => _reservationSummaryLabel;
            private set { if (_reservationSummaryLabel == value) return; _reservationSummaryLabel = value; OnPropertyChanged(); }
        }

        private string _allocationSummaryLabel = "—";
        public string AllocationSummaryLabel
        {
            get => _allocationSummaryLabel;
            private set { if (_allocationSummaryLabel == value) return; _allocationSummaryLabel = value; OnPropertyChanged(); }
        }

        public ICommand ReleaseReservationCommand { get; private set; }
        public ICommand CancelReservationCommand { get; private set; }

        // ── Phase 14: Actual Cost ────────────────────────────────────────

        public ObservableCollection<SavedActualCost> ActualCosts { get; } = new();

        private string _actualCostSummaryLabel = "—";
        public string ActualCostSummaryLabel
        {
            get => _actualCostSummaryLabel;
            private set { if (_actualCostSummaryLabel == value) return; _actualCostSummaryLabel = value; OnPropertyChanged(); }
        }

        private string _focImpactLabel = "—";
        public string FocImpactLabel
        {
            get => _focImpactLabel;
            private set { if (_focImpactLabel == value) return; _focImpactLabel = value; OnPropertyChanged(); }
        }

        // ── Phase 14: Breakage/Replacement history ───────────────────────

        public ObservableCollection<SavedProductionReplacement> Replacements { get; } = new();

        private string _breakageSummaryLabel = "—";
        public string BreakageSummaryLabel
        {
            get => _breakageSummaryLabel;
            private set { if (_breakageSummaryLabel == value) return; _breakageSummaryLabel = value; OnPropertyChanged(); }
        }

        // ── Phase 14: Progress / CancellationToken ───────────────────────

        private bool _isPhase14Running;
        public bool IsPhase14Running
        {
            get => _isPhase14Running;
            private set { if (_isPhase14Running == value) return; _isPhase14Running = value; OnPropertyChanged(); }
        }

        private string _phase14ProgressLabel = "";
        public string Phase14ProgressLabel
        {
            get => _phase14ProgressLabel;
            private set { if (_phase14ProgressLabel == value) return; _phase14ProgressLabel = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPhase14Progress)); }
        }

        public bool HasPhase14Progress => !string.IsNullOrWhiteSpace(_phase14ProgressLabel);

        private CancellationTokenSource? _phase14Cts;

        public ICommand CancelPhase14Command { get; private set; }
        public ICommand CancelWorkItemCommand { get; private set; }
        public ICommand CancelJobCommand { get; private set; }

        // ── Phase 14: Initialization ─────────────────────────────────────

        private void InitPhase14Commands()
        {
            RecordBreakageCommand = new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(() => RecordBreakageAsync(p, isFoc: BreakageIsFoc), "Phase14 breakage"),
                p => p is ProductionWorkItemRow && !IsPhase14Running);

            RecordReworkCommand = new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(() => RecordReworkAsync(p), "Phase14 rework"),
                p => p is ProductionWorkItemRow && !IsPhase14Running);

            RecordFocCommand = new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(() => RecordBreakageAsync(p, isFoc: true), "Phase14 FOC"),
                p => p is ProductionWorkItemRow && !IsPhase14Running);

            RecordQcPassedCommand = new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(() => RecordQcAsync(p, QcResult.Passed), "Phase14 QC Passed"),
                p => p is ProductionWorkItemRow && !IsPhase14Running);

            RecordQcFailedCommand = new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(() => RecordQcAsync(p, QcResult.Failed), "Phase14 QC Failed"),
                p => p is ProductionWorkItemRow && !IsPhase14Running);

            RecordQcReworkCommand = new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(() => RecordQcAsync(p, QcResult.Rework), "Phase14 QC Rework"),
                p => p is ProductionWorkItemRow && !IsPhase14Running);

            ReleaseReservationCommand = new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(() => ReleaseReservationAsync(p), "Phase14 release reservation"),
                p => p is SavedMaterialReservation r && r.Status != (int)ReservationStatus.Consumed);

            CancelReservationCommand = new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(() => CancelReservationAsync(p), "Phase14 cancel reservation"),
                p => p is SavedMaterialReservation r && r.Status != (int)ReservationStatus.Consumed);

            CancelPhase14Command = new RelayCommand(
                _ => _phase14Cts?.Cancel(),
                _ => IsPhase14Running);

            CancelWorkItemCommand = new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(() => CancelWorkItemAsync(p), "Phase14 cancel work item"),
                p => p is ProductionWorkItemRow && !IsPhase14Running);

            CancelJobCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(() => CancelJobAsync(), "Phase14 cancel job"),
                _ => !string.IsNullOrWhiteSpace(SelectedJobNo) && !IsPhase14Running);
        }

        // ── Phase 14: Breakage logic ─────────────────────────────────────

        private async Task RecordBreakageAsync(object? parameter, bool isFoc)
        {
            if (parameter is not ProductionWorkItemRow row) return;

            if (!int.TryParse(BreakageQtyInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out int qty) || qty <= 0)
            {
                ActionWarning = "Enter valid breakage qty >0.";
                return;
            }

            string reason = string.IsNullOrWhiteSpace(BreakageReason) ? (isFoc ? "FOC replacement" : "Breakage") : BreakageReason;

            IsPhase14Running = true;
            Phase14ProgressLabel = $"Recording {(isFoc ? "FOC" : "breakage")} for {row.GlassRef}…";
            _phase14Cts = new CancellationTokenSource();

            try
            {
                var (ok, breakageEntry, replacement, message) = await ProductionBreakageService
                    .RecordBreakageAsync(row.Id, qty, reason, notes: $"Stage {ModuleName} breakage", isFoc: isFoc, ct: _phase14Cts.Token)
                    .ConfigureAwait(true);

                ActionWarning = message;
                if (ok)
                {
                    Phase14ProgressLabel = message;
                    await LoadAllAsync().ConfigureAwait(true);
                    await LoadPhase14DataAsync().ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException)
            {
                ActionWarning = "Breakage recording cancelled.";
            }
            finally
            {
                IsPhase14Running = false;
                _phase14Cts?.Dispose();
                _phase14Cts = null;
            }
        }

        private async Task RecordReworkAsync(object? parameter)
        {
            if (parameter is not ProductionWorkItemRow row) return;

            if (!int.TryParse(BreakageQtyInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out int qty) || qty <= 0)
            {
                ActionWarning = "Enter valid rework qty >0.";
                return;
            }

            string reason = string.IsNullOrWhiteSpace(BreakageReason) ? "Rework" : BreakageReason;

            IsPhase14Running = true;
            Phase14ProgressLabel = $"Recording rework for {row.GlassRef}…";
            _phase14Cts = new CancellationTokenSource();

            try
            {
                var (ok, reworkEntry, message) = await ProductionBreakageService
                    .RecordReworkAsync(row.Id, qty, reason, notes: $"Stage {ModuleName} rework", ct: _phase14Cts.Token)
                    .ConfigureAwait(true);

                ActionWarning = message;
                if (ok)
                {
                    Phase14ProgressLabel = message;
                    await LoadAllAsync().ConfigureAwait(true);
                    await LoadPhase14DataAsync().ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException)
            {
                ActionWarning = "Rework recording cancelled.";
            }
            finally
            {
                IsPhase14Running = false;
                _phase14Cts?.Dispose();
                _phase14Cts = null;
            }
        }

        // ── Phase 14: QC logic ───────────────────────────────────────────

        private async Task RecordQcAsync(object? parameter, QcResult result)
        {
            if (parameter is not ProductionWorkItemRow row) return;

            string reason = string.IsNullOrWhiteSpace(QcReason) ? result.ToString() : QcReason;

            IsPhase14Running = true;
            Phase14ProgressLabel = $"Recording QC {result} for {row.GlassRef}…";
            _phase14Cts = new CancellationTokenSource();

            try
            {
                var (ok, qcEntry, message) = await ProductionQcService
                    .RecordQcAsync(row.Id, result, reason, notes: $"Stage {ModuleName} QC", ct: _phase14Cts.Token)
                    .ConfigureAwait(true);

                ActionWarning = message;
                if (ok)
                {
                    Phase14ProgressLabel = message;
                    await LoadAllAsync().ConfigureAwait(true);
                    await LoadPhase14DataAsync().ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException)
            {
                ActionWarning = "QC recording cancelled.";
            }
            finally
            {
                IsPhase14Running = false;
                _phase14Cts?.Dispose();
                _phase14Cts = null;
            }
        }

        // ── Phase 14: Reservation logic ──────────────────────────────────

        private async Task ReleaseReservationAsync(object? parameter)
        {
            if (parameter is not SavedMaterialReservation res) return;

            IsPhase14Running = true;
            Phase14ProgressLabel = $"Releasing reservation {res.ReservationId}…";
            _phase14Cts = new CancellationTokenSource();

            try
            {
                var (ok, message) = await MaterialReservationService
                    .ReleaseAsync(res.ReservationId, $"Released from {ModuleName}", _phase14Cts.Token)
                    .ConfigureAwait(true);

                ActionWarning = message;
                if (ok) await LoadPhase14DataAsync().ConfigureAwait(true);
            }
            finally
            {
                IsPhase14Running = false;
                _phase14Cts?.Dispose();
                _phase14Cts = null;
            }
        }

        private async Task CancelReservationAsync(object? parameter)
        {
            if (parameter is not SavedMaterialReservation res) return;

            var answer = System.Windows.MessageBox.Show(
                $"Cancel reservation {res.ReservationId} ({res.Quantity} sheets)?\n\nReason will be recorded, history preserved, material becomes available again only when safe (consumed never released).",
                "Cancel reservation",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (answer != System.Windows.MessageBoxResult.Yes) return;

            IsPhase14Running = true;
            Phase14ProgressLabel = $"Cancelling reservation {res.ReservationId}…";
            _phase14Cts = new CancellationTokenSource();

            try
            {
                var (ok, message) = await MaterialReservationService
                    .CancelAsync(res.ReservationId, $"Cancelled from {ModuleName}", _phase14Cts.Token)
                    .ConfigureAwait(true);

                ActionWarning = message;
                if (ok) await LoadPhase14DataAsync().ConfigureAwait(true);
            }
            finally
            {
                IsPhase14Running = false;
                _phase14Cts?.Dispose();
                _phase14Cts = null;
            }
        }

        // ── Phase 14: Data loading ───────────────────────────────────────

        public async Task LoadPhase14DataAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedJobNo)) return;

            try
            {
                // QC
                var qcList = await ProductionQcService.GetByJobAsync(SelectedJobNo).ConfigureAwait(true);
                QcEntries.Clear();
                foreach (var q in qcList) QcEntries.Add(q);
                var qcSummary = await ProductionQcService.GetSummaryByJobAsync(SelectedJobNo).ConfigureAwait(true);
                QcSummaryLabel = $"QC Total {qcSummary.Total} Passed {qcSummary.Passed} Failed {qcSummary.Failed} Pending {qcSummary.Pending} Rework {qcSummary.Rework}";

                // Reservations / Allocations
                var resList = await MaterialReservationService.GetByJobAsync(SelectedJobNo).ConfigureAwait(true);
                Reservations.Clear();
                foreach (var r in resList) Reservations.Add(r);

                var allocList = await MaterialAllocationService.GetByJobAsync(SelectedJobNo).ConfigureAwait(true);
                Allocations.Clear();
                foreach (var a in allocList) Allocations.Add(a);

                var availSummary = await MaterialReservationService.GetAvailableSummaryAsync().ConfigureAwait(true);
                ReservationSummaryLabel = $"Reserved {availSummary.GetValueOrDefault("Reserved", 0)} Allocated {availSummary.GetValueOrDefault("Allocated", 0)} Consumed {availSummary.GetValueOrDefault("Consumed", 0)} Released {availSummary.GetValueOrDefault("Released", 0)} Cancelled {availSummary.GetValueOrDefault("Cancelled", 0)}";
                AllocationSummaryLabel = $"Allocations {allocList.Count} Sheets {allocList.Select(x => x.SheetNo).Distinct().Count()} Util avg {(allocList.Count > 0 ? allocList.Average(x => (double)x.UtilizationPct).ToString("0.##", CultureInfo.InvariantCulture) : "—")}%";

                // Actual Costs
                var costs = await ActualCostService.GetByJobAsync(SelectedJobNo).ConfigureAwait(true);
                ActualCosts.Clear();
                foreach (var c in costs) ActualCosts.Add(c);
                var costSummary = await ActualCostService.GetSummaryByJobAsync(SelectedJobNo).ConfigureAwait(true);
                ActualCostSummaryLabel = $"Est {costSummary.Estimated:0.##} ActualMat {costSummary.ActualMaterial:0.##} Prod {costSummary.Production:0.##} Wastage {costSummary.Wastage:0.##} Proc {costSummary.Processing:0.##} Total {costSummary.TotalActual:0.##}";
                FocImpactLabel = $"FOC impact {costSummary.FocImpact:0.##} (cost incurred, selling zero)";

                // Replacements / Breakage
                using (var db = new Services.Production.ProductionDbContext())
                {
                    var replList = await db.Replacements.AsNoTracking()
                        .Where(r => r.JobRef == SelectedJobNo)
                        .OrderByDescending(r => r.CreatedAt)
                        .Take(100)
                        .ToListAsync().ConfigureAwait(false);

                    Replacements.Clear();
                    foreach (var repl in replList) Replacements.Add(repl);

                    BreakageSummaryLabel = $"Breakage/Rework/FOC Total {replList.Count} FOC {replList.Count(r => r.IsFoc)} Pending {replList.Count(r => r.Status == 0)} Completed {replList.Count(r => r.Status == 2)}";
                }
            }
            catch (Exception ex)
            {
                ActionWarning = $"Phase14 load failed: {ex.Message}";
            }
        }

        // ── Phase 14: Cancellation ───────────────────────────────────────

        private async Task CancelWorkItemAsync(object? parameter)
        {
            if (parameter is not ProductionWorkItemRow row) return;

            var answer = System.Windows.MessageBox.Show(
                $"Cancel work item {row.GlassRef} {row.Item.SpecGroupNumber}/{row.SerialNumber} {row.Position}?\n\nSoft cancellation — preserves who/when/reason, releases reservations only when safe (not consumed), history kept.",
                "Cancel work item",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (answer != System.Windows.MessageBoxResult.Yes) return;

            IsPhase14Running = true;
            Phase14ProgressLabel = $"Cancelling work item {row.GlassRef}…";
            _phase14Cts = new CancellationTokenSource();

            try
            {
                var (ok, message) = await ProductionCancellationService
                    .CancelWorkItemAsync(row.Id, $"Cancelled from {ModuleName}", _phase14Cts.Token)
                    .ConfigureAwait(true);

                ActionWarning = message;
                if (ok)
                {
                    await LoadAllAsync().ConfigureAwait(true);
                    await LoadPhase14DataAsync().ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException)
            {
                ActionWarning = "Cancellation cancelled.";
            }
            finally
            {
                IsPhase14Running = false;
                _phase14Cts?.Dispose();
                _phase14Cts = null;
            }
        }

        private async Task CancelJobAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedJobNo)) return;

            var answer = System.Windows.MessageBox.Show(
                $"Cancel ALL active work items for job {SelectedJobNo}?\n\nSoft cancellation — preserves audit, releases only safe reservations, consumed material never released.",
                "Cancel job",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (answer != System.Windows.MessageBoxResult.Yes) return;

            IsPhase14Running = true;
            Phase14ProgressLabel = $"Cancelling job {SelectedJobNo}…";
            _phase14Cts = new CancellationTokenSource();

            try
            {
                var (ok, message) = await ProductionCancellationService
                    .CancelJobAsync(SelectedJobNo, $"Job cancelled from {ModuleName}", _phase14Cts.Token)
                    .ConfigureAwait(true);

                ActionWarning = message;
                if (ok)
                {
                    await LoadAllAsync().ConfigureAwait(true);
                    await LoadPhase14DataAsync().ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException)
            {
                ActionWarning = "Job cancellation cancelled.";
            }
            finally
            {
                IsPhase14Running = false;
                _phase14Cts?.Dispose();
                _phase14Cts = null;
            }
        }

        // ── Phase 14: Hook into existing detail refresh ──────────────────

        private void OnPhase14DetailRefreshed()
        {
            // Called after detail panel refresh to load Phase14 data
            AsyncUtility.SafeFireAndForget(LoadPhase14DataAsync, "Phase14 detail load");
        }
    }
}
