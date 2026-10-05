// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Statement/Workflow/WorkflowStepState.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report — Workflow Engine
//
// CRITICAL FIX (Chat 17):
//   ObservableCollection.Add() MUST happen on UI thread.
//   Engine runs on background thread — so AddDetail/AddWarning were
//   silently failing when adding to DetailRows/Warnings collections.
//   User only saw step 1 update, all other steps looked empty.
//
//   FIX: All property setters + collection modifications now marshal
//   to UI thread via Application.Current.Dispatcher.Invoke.
//   Safe from any thread now.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;

namespace ProGlassAutomation.Models.Statement.Workflow
{
    public enum StepStatus
    {
        Pending = 0,
        Running = 1,
        Success = 2,
        Warning = 3,
        Blocked = 4,
        Skipped = 5
    }

    /// <summary>
    /// Runtime state for one workflow step.
    /// All updates marshaled to UI thread — safe from any thread.
    /// </summary>
    public class WorkflowStepState : ViewModelBase
    {
        // ── Step identity (immutable) ─────────────────────────────────────

        public WorkflowStep Step { get; }
        public string Title => Step.GetTitle();
        public string Description => Step.GetDescription();
        public string Icon => Step.GetIcon();
        public int Order => Step.GetOrder();

        // ── Status ────────────────────────────────────────────────────────

        private StepStatus _status = StepStatus.Pending;
        public StepStatus Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                RunOnUi(() =>
                {
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(StatusIcon));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusBrushKey));
                    OnPropertyChanged(nameof(IsPending));
                    OnPropertyChanged(nameof(IsRunning));
                    OnPropertyChanged(nameof(IsSuccess));
                    OnPropertyChanged(nameof(IsWarning));
                    OnPropertyChanged(nameof(IsBlocked));
                    OnPropertyChanged(nameof(IsSkipped));
                    OnPropertyChanged(nameof(IsComplete));
                });
            }
        }

        // ── State query helpers ───────────────────────────────────────────

        public bool IsPending => Status == StepStatus.Pending;
        public bool IsRunning => Status == StepStatus.Running;
        public bool IsSuccess => Status == StepStatus.Success;
        public bool IsWarning => Status == StepStatus.Warning;
        public bool IsBlocked => Status == StepStatus.Blocked;
        public bool IsSkipped => Status == StepStatus.Skipped;

        public bool IsComplete =>
            Status == StepStatus.Success ||
            Status == StepStatus.Warning ||
            Status == StepStatus.Blocked ||
            Status == StepStatus.Skipped;

        // ── Current message (single line) ─────────────────────────────────

        private string _currentMessage = "";
        public string CurrentMessage
        {
            get => _currentMessage;
            set
            {
                if (_currentMessage == value) return;
                _currentMessage = value;
                RunOnUi(() =>
                {
                    OnPropertyChanged(nameof(CurrentMessage));
                    OnPropertyChanged(nameof(HasMessage));
                });
            }
        }

        public bool HasMessage => !string.IsNullOrWhiteSpace(_currentMessage);

        // ── Detail rows + warnings (observable collections) ───────────────

        public ObservableCollection<string> DetailRows { get; } = new();
        public ObservableCollection<string> Warnings { get; } = new();
        public bool HasWarnings => Warnings.Count > 0;

        // ── UI display helpers ────────────────────────────────────────────

        public string StatusIcon => Status switch
        {
            StepStatus.Pending => "○",
            StepStatus.Running => "⏳",
            StepStatus.Success => "✓",
            StepStatus.Warning => "⚠",
            StepStatus.Blocked => "✗",
            StepStatus.Skipped => "—",
            _ => "?"
        };

        public string StatusColor => Status switch
        {
            StepStatus.Pending => "#94A3B8",
            StepStatus.Running => "#4338CA",
            StepStatus.Success => "#047857",
            StepStatus.Warning => "#B45309",
            StepStatus.Blocked => "#B91C1C",
            StepStatus.Skipped => "#94A3B8",
            _ => "#0F172A"
        };

        /// <summary>
        /// Theme brush key for XAML Foreground binding via BrushKeyConverter.
        /// Semantic, theme-aware replacement for the hardcoded StatusColor
        /// hex used on the flat pipeline-step rows (readable in both themes).
        /// </summary>
        public string StatusBrushKey => Status switch
        {
            StepStatus.Pending => "MutedForegroundBrush",
            StepStatus.Running => "AccentBrush",
            StepStatus.Success => "SuccessBrush",
            StepStatus.Warning => "WarningBrush",
            StepStatus.Blocked => "DangerBrush",
            StepStatus.Skipped => "MutedForegroundBrush",
            _ => "PrimaryForegroundBrush"
        };

        // ── Constructor ───────────────────────────────────────────────────

        public WorkflowStepState(WorkflowStep step)
        {
            Step = step;
        }

        // ── State transition helpers ──────────────────────────────────────

        public void MarkRunning(string message = "")
        {
            CurrentMessage = message;
            Status = StepStatus.Running;
        }

        public void MarkSuccess(string message)
        {
            CurrentMessage = message;
            Status = StepStatus.Success;
        }

        public void MarkWarning(string message)
        {
            CurrentMessage = message;
            Status = StepStatus.Warning;
        }

        public void MarkBlocked(string message)
        {
            CurrentMessage = message;
            Status = StepStatus.Blocked;
        }

        public void MarkSkipped(string reason = "Skipped due to earlier failure")
        {
            CurrentMessage = reason;
            Status = StepStatus.Skipped;
        }

        /// <summary>
        /// Adds detail row to the step (safe from any thread).
        /// UI marshals via Dispatcher.
        /// </summary>
        public void AddDetail(string detail)
        {
            RunOnUi(() =>
            {
                DetailRows.Add(detail);
                OnPropertyChanged(nameof(DetailRows));
            });
        }

        /// <summary>
        /// Adds warning (safe from any thread).
        /// </summary>
        public void AddWarning(string warning)
        {
            RunOnUi(() =>
            {
                Warnings.Add(warning);
                OnPropertyChanged(nameof(Warnings));
                OnPropertyChanged(nameof(HasWarnings));
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  THREAD MARSHALING
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Executes action on UI thread. If already on UI thread, runs
        /// immediately. Otherwise uses Dispatcher.Invoke (blocking) to
        /// ensure order preservation.
        /// </summary>
        private static void RunOnUi(System.Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                action();
                return;
            }

            if (dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.BeginInvoke(action, System.Windows.Threading.DispatcherPriority.DataBind);
            }
        }
    }
}