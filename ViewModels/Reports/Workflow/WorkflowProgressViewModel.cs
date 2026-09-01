// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/Workflow/WorkflowProgressViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report — Workflow Engine (Chat 17)
//
// PURPOSE:
//   ViewModel for the WorkflowProgressDialog.
//   Wraps the StatementWorkflowEngine + its Steps for XAML binding.
//   Exposes commands for user interactions (View Warnings, Close, Cancel).
//
// LIFECYCLE:
//   1. Constructed with engine + statement info
//   2. Dialog opens → RunAsync() starts engine on background thread
//   3. Step states update live via ObservableCollection<WorkflowStepState>
//   4. Engine completes → OverallProgress = 100 → Close button enabled
//   5. User closes dialog → OnCompleted callback fires with WorkflowResult
//
// THREADING:
//   All updates from engine happen via Dispatcher.Invoke on step state
//   properties (already implemented in WorkflowStepState via OnPropertyChanged).
//   ViewModel itself needs no marshaling — WPF binding engine handles it.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Statement.Workflow;
using ProGlassAutomation.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;

namespace ProGlassAutomation.ViewModels.Reports.Workflow
{
    /// <summary>
    /// ViewModel for the workflow progress dialog.
    /// Owns the engine instance and reports live progress to the UI.
    /// </summary>
    public class WorkflowProgressViewModel : ViewModelBase
    {
        // ── Engine + inputs ───────────────────────────────────────────────

        private readonly StatementWorkflowEngine _engine;
        private readonly StatementFilter _filter;
        private readonly string _statementNumber;

        /// <summary>
        /// User-supplied callback invoked when engine detects missing invoices.
        /// Wired by the WorkflowProgressDialog code-behind to open the
        /// warning resolution dialog.
        /// </summary>
        public ResolveMissingInvoicesCallback? ResolveMissingInvoices { get; set; }

        // ── Header info ───────────────────────────────────────────────────

        public string StatementNumber => _statementNumber;

        private string _customerName = "";
        public string CustomerName
        {
            get => _customerName;
            set
            {
                if (_customerName == value) return;
                _customerName = value; OnPropertyChanged();
            }
        }

        // ── Steps (bound directly to ItemsControl) ────────────────────────

        /// <summary>
        /// Observable collection of step states.
        /// Each WorkflowStepState is itself observable — updates propagate live.
        /// </summary>
        public ObservableCollection<WorkflowStepState> Steps { get; }

        // ── Overall progress ──────────────────────────────────────────────

        private int _overallProgress;
        /// <summary>0-100 percent — drives the progress bar.</summary>
        public int OverallProgress
        {
            get => _overallProgress;
            set
            {
                if (_overallProgress == value) return;
                _overallProgress = value; OnPropertyChanged();
                OnPropertyChanged(nameof(OverallProgressText));
            }
        }

        public string OverallProgressText => $"{_overallProgress}%";

        // ── State flags ───────────────────────────────────────────────────

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (_isRunning == value) return;
                _isRunning = value; OnPropertyChanged();
                OnPropertyChanged(nameof(IsCompleted));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsCompleted => !_isRunning && Result != null;

        // ── Result (populated when engine completes) ──────────────────────

        private WorkflowResult? _result;
        public WorkflowResult? Result
        {
            get => _result;
            private set
            {
                _result = value; OnPropertyChanged();
                OnPropertyChanged(nameof(HasWarnings));
                OnPropertyChanged(nameof(HasResult));
                OnPropertyChanged(nameof(ResultSummary));
                OnPropertyChanged(nameof(ResultOutcomeIcon));
                OnPropertyChanged(nameof(ResultOutcomeColor));
                OnPropertyChanged(nameof(IsCompleted));
            }
        }

        public bool HasResult => _result != null;

        public bool HasWarnings =>
            _result != null && _result.AllWarnings.Count > 0;

        public string ResultSummary => _result?.Summary ?? "";

        /// <summary>Icon shown next to the outcome text.</summary>
        public string ResultOutcomeIcon
        {
            get
            {
                if (_result == null) return "";
                return _result.Outcome switch
                {
                    WorkflowOutcome.Success => "✓",
                    WorkflowOutcome.Warning => "⚠",
                    WorkflowOutcome.Blocked => "✗",
                    WorkflowOutcome.Cancelled => "○",
                    _ => "?"
                };
            }
        }

        /// <summary>Color for the outcome icon + text.</summary>
        public string ResultOutcomeColor
        {
            get
            {
                if (_result == null) return "#94A3B8";
                return _result.Outcome switch
                {
                    WorkflowOutcome.Success => "#047857",
                    WorkflowOutcome.Warning => "#B45309",
                    WorkflowOutcome.Blocked => "#B91C1C",
                    WorkflowOutcome.Cancelled => "#94A3B8",
                    _ => "#94A3B8"
                };
            }
        }

        // ── Commands ──────────────────────────────────────────────────────

        public ICommand CloseCommand { get; }
        public ICommand ViewWarningsCommand { get; }

        // ── Dialog result callback ────────────────────────────────────────

        /// <summary>
        /// Fired when user closes the dialog after workflow completes.
        /// The caller (StatementGeneratorViewModel) uses this to proceed
        /// with PDF export or handle cancellation.
        /// </summary>
        public Action<WorkflowResult>? OnDialogClosing { get; set; }

        /// <summary>Fired when the user clicks the Close button.</summary>
        public Action? RequestClose { get; set; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public WorkflowProgressViewModel(
            StatementFilter filter,
            string statementNumber,
            string customerName)
        {
            _filter = filter;
            _statementNumber = statementNumber;
            _customerName = customerName;

            _engine = new StatementWorkflowEngine();
            Steps = new ObservableCollection<WorkflowStepState>(_engine.Steps);

            // Subscribe to each step's status change to update overall progress
            foreach (var step in Steps)
            {
                step.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(WorkflowStepState.Status))
                        RecalculateProgress();
                };
            }

            CloseCommand = new RelayCommand(
                _ =>
                {
                    if (_result != null)
                        OnDialogClosing?.Invoke(_result);
                    RequestClose?.Invoke();
                },
                _ => IsCompleted);

            ViewWarningsCommand = new RelayCommand(
                _ => ShowWarningsDialog(),
                _ => HasWarnings);
        }

        // ═════════════════════════════════════════════════════════════════
        //  RUN THE WORKFLOW
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Executes the workflow asynchronously.
        /// Called by the dialog's Loaded event via code-behind.
        /// </summary>
        public async Task RunAsync()
        {
            if (IsRunning) return;

            IsRunning = true;
            OverallProgress = 0;
            Result = null;

            try
            {
                // The engine handles all step transitions internally.
                // WorkflowStepState objects update via OnPropertyChanged
                // → WPF binding engine marshals to UI thread automatically.
                Result = await _engine.RunAsync(
                    _filter,
                    _statementNumber,
                    ResolveMissingInvoices);
            }
            catch (Exception ex)
            {
                // Should not happen — engine has its own try/catch.
                // Safety net for unexpected exceptions.
                Result = new WorkflowResult
                {
                    Outcome = WorkflowOutcome.Blocked,
                    Summary = $"Unexpected error: {ex.Message}",
                    Steps = _engine.Steps
                };
            }
            finally
            {
                IsRunning = false;
                OverallProgress = 100;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  PROGRESS CALCULATION
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Recalculates overall progress based on step completion.
        /// Called every time a step's status changes.
        /// </summary>
        private void RecalculateProgress()
        {
            int totalSteps = Steps.Count;
            if (totalSteps == 0)
            {
                OverallProgress = 0;
                return;
            }

            int completedSteps = Steps.Count(s => s.IsComplete);
            int runningSteps = Steps.Count(s => s.IsRunning);

            // Each complete step = 100%, each running step = 50%
            double progress = (completedSteps * 100.0 + runningSteps * 50.0)
                              / totalSteps;

            OverallProgress = (int)Math.Round(progress);
        }

        // ═════════════════════════════════════════════════════════════════
        //  WARNINGS DIALOG
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Shows a simple MessageBox with all warnings.
        /// (Could be replaced with a richer dialog later.)
        /// </summary>
        private void ShowWarningsDialog()
        {
            if (_result == null || _result.AllWarnings.Count == 0) return;

            string message = "Warnings collected during workflow:\n\n" +
                             string.Join("\n\n", _result.AllWarnings
                                 .Select((w, i) => $"{i + 1}. {w}"));

            MessageBox.Show(
                message,
                LocalizationService.Instance.T("Msg.CapWorkflowWarningsX"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}