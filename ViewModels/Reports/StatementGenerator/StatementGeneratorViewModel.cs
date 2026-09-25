// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 22 v3 — Jump Navigation)
//
// CHAT 22 v3 CHANGES:
//   ═══════════════════════════════════════════════════════════════════
//   NEW — CLICKABLE STEPPER NAVIGATION:
//     • GoToStepCommand — parameterized (accepts step number 1-4)
//     • CanNavigateToStep1/2/3/4 — per-step gate properties
//     • IsStepXComplete semantics changed:
//         Now reflects ACTUAL state (not just position past)
//         Step1Complete = HasFilter
//         Step2Complete = PreviewVM.HasSelection
//         Step3Complete = IsLastWorkflowComplete
//         Step4Complete = HasLastPdf
//     • CanGoPrevious no longer restricted to steps <3
//         (user can now go back from Step 4 → Step 3)
//     • RunPipelineAsync guarded against re-run when complete
//
//   BUG FIXES:
//     • Removed duplicate LoadMasterDataAsync() call in constructor
//     • Navigation properties notified when IsGenerating changes
//     • Navigation properties notified when HasLastPdf/HasFilter change
//     • Navigation properties notified when PreviewVM.SelectionChanged fires
//     • Navigation properties notified when StatusItems.IsChecked changes
//     • Navigation properties notified in LiftWorkflowResultAfterCompletion
//
//   PERFORMANCE:
//     • NotifyAllStepStates() helper avoids property-name typos
//     • Batch OnPropertyChanged calls at strategic points
//
//   PRESERVED:
//     • All existing bindings + command signatures
//     • All Chat 21 v2 inline execution logic (unchanged)
//     • All PDF metadata + section browser logic (unchanged)
//     • Live workflow subscriptions (unchanged)
// ═══════════════════════════════════════════════════════════════════════

using PdfSharp.Pdf;
using ProGlassAutomation.Services.Logging;
using PdfSharp.Pdf.IO;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Reconciliation;
using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Statement.Aggregation;
using ProGlassAutomation.Services.Statement.Pdf;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Services.Statement.Workflow;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Views.Reports.Workflow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.Database;
namespace ProGlassAutomation.ViewModels.Reports.StatementGenerator
{
    public partial class StatementGeneratorViewModel : ViewModelBase
    {
        private readonly IStatementRepository _repo;
        private readonly StatementAggregator _aggregator;

        // ── Preview VM ────────────────────────────────────────────────────
        public StatementPreviewViewModel PreviewVM { get; } = new();

        // ── Debounce timer for auto-preview ──────────────────────────────
        private readonly DispatcherTimer _debounceTimer;
        private const int DebounceMs = 400;

        // ── Cancellation for in-flight preview requests ──────────────────
        private CancellationTokenSource? _previewCts;

        // ── Pending list refresh flag ────────────────────────────────────
        private bool _pendingListRefresh;

        // ── Live workflow subscription tracking ──────────────────────────
        private readonly Dictionary<WorkflowStepState, NotifyCollectionChangedEventHandler>
            _liveDetailSubscriptions = new();

        // ── Callbacks ────────────────────────────────────────────────────
        public Func<Task>? OnStatementSaved { get; set; }
        public Action? OnCancelled { get; set; }

        // ── Warning/alert action callbacks (wired by MainViewModel) ─────
        // These make the "→ Open invoice / Create invoice / Open PI" buttons
        // actually navigate to the related document instead of being dead
        // labels. Target values are document references (number / id), not
        // database ids; MainViewModel resolves + navigates.
        public Action<string>? OnOpenTaxInvoiceByNumber { get; set; }
        public Action<string>? OnCreateInvoiceFromDeliveryNote { get; set; }
        public Action<string>? OnOpenProformaByNumber { get; set; }

        // ── Review-step balance actions (wired by MainViewModel) ─────────
        // "View Job Order" opens the JO for the related PI; "Prepare Delivery
        // Note" navigates to the JO and starts a balance-only DN.
        public Action<string>? OnOpenJobOrderByNumber { get; set; }
        public Action<string>? OnPrepareDeliveryNote { get; set; }

        /// <summary>Routes a warning card's action button to the right callback.</summary>
        public ICommand OpenWarningActionCommand { get; private set; } = null!;

        /// <summary>Opens the Job Order referenced by a reconciliation summary.</summary>
        public ICommand OpenJobOrderCommand { get; private set; } = null!;

        /// <summary>Starts a balance-only Delivery Note for a Job Order.</summary>
        public ICommand PrepareDeliveryNoteCommand { get; private set; } = null!;

        // ── Edit mode ────────────────────────────────────────────────────
        private int _editId = 0;
        private string _editNumber = "";
        public bool IsEditMode => _editId > 0;
        public string PageTitle => IsEditMode
            ? $"Edit Statement — {_editNumber}"
            : "New Account Statement";

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

                private readonly IDialogService _dialogs = null!;
                private readonly IMasterDataRepository _masterData = null!;
                private readonly IDatabaseServices _database = null!;

public StatementGeneratorViewModel(IDialogService? dialogs,
            IMasterDataRepository? masterData,
            IStatementRepository repo,
            IDatabaseServices? database)
        {
            _dialogs = dialogs!;
            _masterData = masterData!;
            _database = database!;
            _repo = repo;
            _aggregator = new StatementAggregator();

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceMs) };
            _debounceTimer.Tick += async (s, e) =>
            {
                _debounceTimer.Stop();
                await RunPreviewAsync();
            };

            foreach (var s in new[] { "Draft", "Issued", "Paid", "PartiallyPaid", "Overdue", "Cancelled", "Void" })
            {
                var item = new StatusCheckItem(s, s is "Issued" or "Paid" or "PartiallyPaid" or "Overdue");
                item.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(StatusCheckItem.IsChecked))
                    {
                        NotifyFilterDependents();
                        SchedulePreview();
                    }
                };
                StatusItems.Add(item);
            }

            // [CHAT 22 v3] Wire PreviewVM.SelectionChanged to update Step 3 gate
            PreviewVM.SelectionChanged += () =>
            {
                OnPropertyChanged(nameof(IsStep2Complete));
                OnPropertyChanged(nameof(CanNavigateToStep3));
                ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
            };

            PreviewCommand = new RelayCommand(
                async _ => await RunPreviewAsync(),
                _ => !_isLoading && !_isGenerating && HasFilter);

            GenerateCommand = new RelayCommand(
                async _ => await RunGenerateAsync(),
                _ => !_isLoading && !_isGenerating && _hasPreview && PreviewVM.HasSelection);

            GenerateAgainCommand = new RelayCommand(
                async _ => await RunGenerateAsync(),
                _ => !_isLoading && !_isGenerating && _hasPreview && PreviewVM.HasSelection);

            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

            CancelCommand = new RelayCommand(async _ =>
            {
                if (_pendingListRefresh && OnStatementSaved != null)
                {
                    _pendingListRefresh = false;
                    await OnStatementSaved.Invoke();
                }
                else
                {
                    OnCancelled?.Invoke();
                }
            });

            SelectAllCommand = new RelayCommand(_ => PreviewVM.SelectAll(),
                _ => PreviewVM.Rows.Count > 0);
            DeselectAllCommand = new RelayCommand(_ => PreviewVM.DeselectAll(),
                _ => PreviewVM.Rows.Count > 0);

            DownloadLastPdfCommand = new RelayCommand(_ => OpenLastPdf(), _ => HasLastPdf);
            CopyPdfPathCommand = new RelayCommand(_ => CopyLastPdfPathToClipboard(), _ => HasLastPdf);
            ShareLastPdfCommand = new RelayCommand(_ => ShareLastPdf(), _ => HasLastPdf);

            SelectMergedTabCommand = new RelayCommand(_ => ActivePdfTab = "Merged");
            SelectBalanceTabCommand = new RelayCommand(_ => ActivePdfTab = "Balance");
            SelectAlertsTabCommand = new RelayCommand(_ => ActivePdfTab = "Alerts");

            // ── Warning/alert action routing ──────────────────────────────
            OpenWarningActionCommand = new RelayCommand(param =>
            {
                if (param is not WorkflowWarningItem item) return;

                switch (item.ActionKind)
                {
                    case WarningActionKind.OpenInvoice:
                        OnOpenTaxInvoiceByNumber?.Invoke(item.TargetId);
                        break;
                    case WarningActionKind.CreateInvoiceFromDn:
                        OnCreateInvoiceFromDeliveryNote?.Invoke(item.TargetId);
                        break;
                    case WarningActionKind.OpenProforma:
                        OnOpenProformaByNumber?.Invoke(item.TargetId);
                        break;
                    case WarningActionKind.OpenPdf:
                        OpenLastPdf();
                        break;
                    case WarningActionKind.GoToReview:
                        if (CurrentStep != 4) CurrentStep = 4;
                        break;
                    case WarningActionKind.None:
                    default:
                        break;
                }
            });

            // ── Reconciliation tab switching ─────────────────────────────
            SelectReconciliationTabCommand = new RelayCommand(param =>
            {
                if (param is string tab && tab.Length > 0)
                    ActiveReconciliationTab = tab;
            });

            // ── Balance actions (View Job Order / Prepare Delivery Note) ──
            OpenJobOrderCommand = new RelayCommand(param =>
            {
                string id = param as string ?? FirstReconciliationJobOrderId;
                if (!string.IsNullOrWhiteSpace(id))
                    OnOpenJobOrderByNumber?.Invoke(id);
            });

            PrepareDeliveryNoteCommand = new RelayCommand(param =>
            {
                string id = param as string ?? FirstReconciliationJobOrderId;
                if (!string.IsNullOrWhiteSpace(id))
                    OnPrepareDeliveryNote?.Invoke(id);
            });

            // ── Wizard navigation commands ──────────────────────────────
            NextStepCommand = new RelayCommand(_ =>
            {
                if (_currentStep == 1)
                {
                    if (!HasFilter)
                    {
                        StatusMessage = "Configure at least one filter before continuing.";
                        return;
                    }
                    _ = RunPreviewAsync();
                    CurrentStep = 2;
                    StatusMessage = "";
                }
                else if (_currentStep == 2)
                {
                    if (!PreviewVM.HasSelection)
                    {
                        StatusMessage = "Select at least one invoice before running the pipeline.";
                        return;
                    }
                    StatusMessage = "Starting the statement pipeline…";
                    CurrentStep = 3;
                }
                else if (_currentStep == 4)
                {
                    if (_pendingListRefresh && OnStatementSaved != null)
                    {
                        _pendingListRefresh = false;
                        _ = OnStatementSaved.Invoke();
                    }
                    else
                    {
                        OnCancelled?.Invoke();
                    }
                }
            }, _ => CanGoNext);

            FinishCommand = new RelayCommand(_ =>
            {
                if (_pendingListRefresh && OnStatementSaved != null)
                {
                    _pendingListRefresh = false;
                    _ = OnStatementSaved.Invoke();
                }
                else
                {
                    OnCancelled?.Invoke();
                }
            }, _ => _currentStep == 4 && !_isGenerating);

            PreviousStepCommand = new RelayCommand(_ =>
            {
                if (_currentStep > 1)
                    CurrentStep = _currentStep - 1;
            }, _ => CanGoPrevious);

            // [CHAT 22 v3] GoToStepCommand — clickable stepper navigation
            GoToStepCommand = new RelayCommand(param =>
            {
                if (_isGenerating) return;
                if (param == null) return;

                if (!int.TryParse(param.ToString(), out int targetStep)) return;
                if (targetStep < 1 || targetStep > 4) return;
                if (targetStep == _currentStep) return;

                // Gate check — verify user is allowed to jump to this step
                bool allowed = targetStep switch
                {
                    1 => CanNavigateToStep1,
                    2 => CanNavigateToStep2,
                    3 => CanNavigateToStep3,
                    4 => CanNavigateToStep4,
                    _ => false
                };

                if (!allowed)
                {
                    StatusMessage = targetStep switch
                    {
                        2 => "Configure at least one filter before viewing Preview.",
                        3 => "Select at least one invoice before running the pipeline.",
                        4 => "Generate a statement first before viewing Review.",
                        _ => ""
                    };
                    return;
                }

                CurrentStep = targetStep;
                StatusMessage = "";
            });

            // [CHAT 22 v3] Removed duplicate LoadMasterDataAsync() call
            _ = LoadMasterDataAsync();
        }

    }

}
