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
using ProGlassAutomation.Models.Statement;
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

namespace ProGlassAutomation.ViewModels.Reports.StatementGenerator
{
    public class StatementGeneratorViewModel : ViewModelBase
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

        // ── Edit mode ────────────────────────────────────────────────────
        private int _editId = 0;
        private string _editNumber = "";
        public bool IsEditMode => _editId > 0;
        public string PageTitle => IsEditMode
            ? $"Edit Statement — {_editNumber}"
            : "New Account Statement";

        // ═════════════════════════════════════════════════════════════════
        //  FILTER FIELDS
        // ═════════════════════════════════════════════════════════════════

        private string _filterCompany = "";
        public string FilterCompany
        {
            get => _filterCompany;
            set
            {
                if (_filterCompany == value) return;
                _filterCompany = value;
                OnPropertyChanged();
                NotifyFilterDependents();
                SchedulePreview();
            }
        }

        private string _filterCustomer = "";
        public string FilterCustomer
        {
            get => _filterCustomer;
            set
            {
                if (_filterCustomer == value) return;
                _filterCustomer = value;
                OnPropertyChanged();
                NotifyFilterDependents();
                SchedulePreview();
            }
        }

        private string _filterProject = "";
        public string FilterProject
        {
            get => _filterProject;
            set
            {
                if (_filterProject == value) return;
                _filterProject = value;
                OnPropertyChanged();
                NotifyFilterDependents();
                SchedulePreview();
            }
        }

        private string _filterSalesman = "";
        public string FilterSalesman
        {
            get => _filterSalesman;
            set
            {
                if (_filterSalesman == value) return;
                _filterSalesman = value;
                OnPropertyChanged();
                NotifyFilterDependents();
                SchedulePreview();
            }
        }

        private DateTime? _dateFrom;
        public DateTime? DateFrom
        {
            get => _dateFrom;
            set
            {
                if (_dateFrom == value) return;
                _dateFrom = value;
                OnPropertyChanged();
                NotifyFilterDependents();
                SchedulePreview();
            }
        }

        private DateTime? _dateTo;
        public DateTime? DateTo
        {
            get => _dateTo;
            set
            {
                if (_dateTo == value) return;
                _dateTo = value;
                OnPropertyChanged();
                NotifyFilterDependents();
                SchedulePreview();
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  PDF OPTIONS
        // ═════════════════════════════════════════════════════════════════

        private bool _includeAttachments = true;
        public bool IncludeAttachments
        {
            get => _includeAttachments;
            set { if (_includeAttachments == value) return; _includeAttachments = value; OnPropertyChanged(); }
        }

        private bool _includeDnDetails;
        public bool IncludeDnDetails
        {
            get => _includeDnDetails;
            set { if (_includeDnDetails == value) return; _includeDnDetails = value; OnPropertyChanged(); }
        }

        private bool _includeBalanceAnalysis = true;
        public bool IncludeBalanceAnalysis
        {
            get => _includeBalanceAnalysis;
            set { if (_includeBalanceAnalysis == value) return; _includeBalanceAnalysis = value; OnPropertyChanged(); }
        }

        private bool _includeAgingChart = true;
        public bool IncludeAgingChart
        {
            get => _includeAgingChart;
            set { if (_includeAgingChart == value) return; _includeAgingChart = value; OnPropertyChanged(); }
        }

        private bool _includeInsertPlaceholders = true;
        public bool IncludeInsertPlaceholders
        {
            get => _includeInsertPlaceholders;
            set { if (_includeInsertPlaceholders == value) return; _includeInsertPlaceholders = value; OnPropertyChanged(); }
        }

        private bool _includeWarningsSummary = true;
        public bool IncludeWarningsSummary
        {
            get => _includeWarningsSummary;
            set { if (_includeWarningsSummary == value) return; _includeWarningsSummary = value; OnPropertyChanged(); }
        }

        // ── Auto-preview toggle ──────────────────────────────────────────

        private bool _autoPreviewEnabled = true;
        public bool AutoPreviewEnabled
        {
            get => _autoPreviewEnabled;
            set
            {
                if (_autoPreviewEnabled == value) return;
                _autoPreviewEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutoPreviewStatusDisplay));

                if (_autoPreviewEnabled && HasFilter && !_isLoading)
                    SchedulePreview();
            }
        }

        public string AutoPreviewStatusDisplay =>
            _autoPreviewEnabled
                ? "Auto-preview  ·  400ms debounce"
                : "Auto-preview off  ·  Click Preview to update";

        // ═════════════════════════════════════════════════════════════════
        //  STATUS ITEMS + DROPDOWN SOURCES
        // ═════════════════════════════════════════════════════════════════

        public ObservableCollection<StatusCheckItem> StatusItems { get; } = new();
        public ObservableCollection<string> Companies { get; } = new();
        public ObservableCollection<string> Customers { get; } = new();
        public ObservableCollection<string> Projects { get; } = new();
        public ObservableCollection<string> Salesmen { get; } = new();

        // ═════════════════════════════════════════════════════════════════
        //  CORE STATE
        // ═════════════════════════════════════════════════════════════════

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _isGenerating;
        public bool IsGenerating
        {
            get => _isGenerating;
            set
            {
                if (_isGenerating == value) return;
                _isGenerating = value;
                OnPropertyChanged();
                // [CHAT 22 v3] Notify ALL navigation gates — mid-run is locked
                NotifyAllNavigationStates();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set { if (_statusMessage == value) return; _statusMessage = value; OnPropertyChanged(); }
        }

        private bool _hasPreview;
        public bool HasPreview
        {
            get => _hasPreview;
            set
            {
                if (_hasPreview == value) return;
                _hasPreview = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MatchCountDisplay));
                OnPropertyChanged(nameof(MatchCount));
            }
        }

        public bool HasFilter =>
            !string.IsNullOrWhiteSpace(_filterCompany) ||
            !string.IsNullOrWhiteSpace(_filterCustomer) ||
            !string.IsNullOrWhiteSpace(_filterProject) ||
            !string.IsNullOrWhiteSpace(_filterSalesman) ||
            _dateFrom.HasValue ||
            _dateTo.HasValue ||
            StatusItems.Any(s => s.IsChecked);

        private StatementSnapshot? _previewSnapshot;
        public StatementSnapshot? PreviewSnapshot
        {
            get => _previewSnapshot;
            private set
            {
                _previewSnapshot = value;
                OnPropertyChanged();
                HasPreview = value != null;
                UpdateStatusItemCounts(value);
                OnPropertyChanged(nameof(MatchCountDisplay));
                OnPropertyChanged(nameof(MatchCount));
            }
        }

        public int MatchCount => _previewSnapshot?.InvoiceRows.Count ?? 0;

        public string MatchCountDisplay
        {
            get
            {
                if (_previewSnapshot == null)
                {
                    if (!HasFilter) return "Configure filters to see match count";
                    if (_isLoading) return "Searching…";
                    return "No matches yet";
                }
                int n = _previewSnapshot.InvoiceRows.Count;
                return n == 1 ? "1 invoice matches filters" : $"{n:N0} invoices match filters";
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  SECTION 3 — PIPELINE VISUALIZATION
        // ═════════════════════════════════════════════════════════════════

        public ObservableCollection<WorkflowStepState> LastWorkflowSteps { get; } = new();

        private double _lastWorkflowProgressPercent;
        public double LastWorkflowProgressPercent
        {
            get => _lastWorkflowProgressPercent;
            private set
            {
                if (Math.Abs(_lastWorkflowProgressPercent - value) < 0.01) return;
                _lastWorkflowProgressPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastWorkflowProgressDisplay));
            }
        }

        public string LastWorkflowProgressDisplay
            => $"{(int)Math.Round(_lastWorkflowProgressPercent)}%";

        private string _lastWorkflowStatusText = "Ready to Generate";
        public string LastWorkflowStatusText
        {
            get => _lastWorkflowStatusText;
            private set { if (_lastWorkflowStatusText == value) return; _lastWorkflowStatusText = value; OnPropertyChanged(); }
        }

        private bool _hasWorkflowRun;
        public bool HasWorkflowRun
        {
            get => _hasWorkflowRun;
            private set { if (_hasWorkflowRun == value) return; _hasWorkflowRun = value; OnPropertyChanged(); }
        }

        private bool _isLastWorkflowComplete;
        public bool IsLastWorkflowComplete
        {
            get => _isLastWorkflowComplete;
            private set
            {
                if (_isLastWorkflowComplete == value) return;
                _isLastWorkflowComplete = value;
                OnPropertyChanged();
                // [CHAT 22 v3] Notify navigation dependents
                OnPropertyChanged(nameof(IsStep3Complete));
                OnPropertyChanged(nameof(CanNavigateToStep4));
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  SECTION 4 — PROCESSING LOG + METRICS + WARNINGS
        // ═════════════════════════════════════════════════════════════════

        public ObservableCollection<ProGlassAutomation.Models.Statement.Workflow.LogEntry> ProcessingLog { get; } = new();
        public ObservableCollection<WorkflowWarningItem> WorkflowWarnings { get; } = new();

        private int _orderedQtyMetric;
        public int OrderedQtyMetric
        {
            get => _orderedQtyMetric;
            private set { if (_orderedQtyMetric == value) return; _orderedQtyMetric = value; OnPropertyChanged(); OnPropertyChanged(nameof(OrderedQtyDisplay)); }
        }
        public string OrderedQtyDisplay => $"{_orderedQtyMetric:N0}";

        private int _deliveredQtyMetric;
        public int DeliveredQtyMetric
        {
            get => _deliveredQtyMetric;
            private set { if (_deliveredQtyMetric == value) return; _deliveredQtyMetric = value; OnPropertyChanged(); OnPropertyChanged(nameof(DeliveredQtyDisplay)); }
        }
        public string DeliveredQtyDisplay => $"{_deliveredQtyMetric:N0}";

        private int _balanceQtyMetric;
        public int BalanceQtyMetric
        {
            get => _balanceQtyMetric;
            private set { if (_balanceQtyMetric == value) return; _balanceQtyMetric = value; OnPropertyChanged(); OnPropertyChanged(nameof(BalanceQtyDisplay)); }
        }
        public string BalanceQtyDisplay => $"{_balanceQtyMetric:N0}";

        private double _deliveryRatePercent;
        public double DeliveryRatePercent
        {
            get => _deliveryRatePercent;
            private set { if (Math.Abs(_deliveryRatePercent - value) < 0.01) return; _deliveryRatePercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(DeliveryRateDisplay)); }
        }
        public string DeliveryRateDisplay
            => _deliveredQtyMetric == 0 && _orderedQtyMetric == 0
                ? "—"
                : $"{_deliveryRatePercent:N1}%";

        private int _warningCount;
        public int WarningCount
        {
            get => _warningCount;
            private set { if (_warningCount == value) return; _warningCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(WarningSummaryDisplay)); }
        }

        private int _errorCount;
        public int ErrorCount
        {
            get => _errorCount;
            private set { if (_errorCount == value) return; _errorCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(WarningSummaryDisplay)); }
        }

        private int _infoCount;
        public int InfoCount
        {
            get => _infoCount;
            private set { if (_infoCount == value) return; _infoCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(WarningSummaryDisplay)); }
        }

        public string WarningSummaryDisplay
        {
            get
            {
                var parts = new List<string>(3);
                if (_errorCount > 0) parts.Add($"{_errorCount} error{(_errorCount == 1 ? "" : "s")}");
                if (_warningCount > 0) parts.Add($"{_warningCount} warning{(_warningCount == 1 ? "" : "s")}");
                if (_infoCount > 0) parts.Add($"{_infoCount} info");
                return parts.Count == 0 ? "No warnings" : string.Join(" · ", parts);
            }
        }

        // ── Last PDF metadata ────────────────────────────────────────────

        private string _lastPdfPath = "";
        public string LastPdfPath
        {
            get => _lastPdfPath;
            private set
            {
                if (_lastPdfPath == value) return;
                _lastPdfPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastPdfFileName));
                OnPropertyChanged(nameof(HasLastPdf));
                // [CHAT 22 v3] Notify Step 4 navigation gate
                OnPropertyChanged(nameof(IsStep4Complete));
                OnPropertyChanged(nameof(CanNavigateToStep4));
            }
        }

        public string LastPdfFileName
            => string.IsNullOrWhiteSpace(_lastPdfPath) ? "" : Path.GetFileName(_lastPdfPath);

        private string _lastPdfSize = "";
        public string LastPdfSize
        {
            get => _lastPdfSize;
            private set { if (_lastPdfSize == value) return; _lastPdfSize = value; OnPropertyChanged(); }
        }

        private int _lastPdfPageCount;
        public int LastPdfPageCount
        {
            get => _lastPdfPageCount;
            private set { if (_lastPdfPageCount == value) return; _lastPdfPageCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastPdfPageInfo)); }
        }

        public string LastPdfPageInfo
            => _lastPdfPageCount == 0 ? "" : $"{_lastPdfPageCount:N0} page{(_lastPdfPageCount == 1 ? "" : "s")}";

        private double _lastPdfGenerationSec;
        public double LastPdfGenerationSec
        {
            get => _lastPdfGenerationSec;
            private set { if (Math.Abs(_lastPdfGenerationSec - value) < 0.01) return; _lastPdfGenerationSec = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastPdfGenerationDisplay)); }
        }

        public string LastPdfGenerationDisplay
            => _lastPdfGenerationSec <= 0 ? "" : $"Generated in {_lastPdfGenerationSec:N1}s";

        private DateTime _lastPdfGeneratedAt;
        public DateTime LastPdfGeneratedAt
        {
            get => _lastPdfGeneratedAt;
            private set { _lastPdfGeneratedAt = value; OnPropertyChanged(); }
        }

        public bool HasLastPdf =>
            !string.IsNullOrWhiteSpace(_lastPdfPath) && File.Exists(_lastPdfPath);

        // ═════════════════════════════════════════════════════════════════
        //  SECTION 6 — PDF SECTION BROWSER
        // ═════════════════════════════════════════════════════════════════

        public ObservableCollection<PdfSection> MergedPdfSections { get; } = new();
        public ObservableCollection<PdfSection> BalancePdfSections { get; } = new();
        public ObservableCollection<PdfSection> AlertsPdfSections { get; } = new();

        public int MergedSectionCount => MergedPdfSections.Count;
        public int BalanceSectionCount => BalancePdfSections.Count;
        public int AlertsSectionCount => AlertsPdfSections.Count;

        private string _activePdfTab = "Merged";
        public string ActivePdfTab
        {
            get => _activePdfTab;
            set
            {
                if (_activePdfTab == value) return;
                _activePdfTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowMergedTab));
                OnPropertyChanged(nameof(ShowBalanceTab));
                OnPropertyChanged(nameof(ShowAlertsTab));
            }
        }

        public bool ShowMergedTab => _activePdfTab == "Merged";
        public bool ShowBalanceTab => _activePdfTab == "Balance";
        public bool ShowAlertsTab => _activePdfTab == "Alerts";

        // ═════════════════════════════════════════════════════════════════
        //  SECTION 7 — FOOTER
        // ═════════════════════════════════════════════════════════════════

        public bool IsEngineOperational => true;
        public string EngineStatusDisplay => "Engine Operational";

        // ═════════════════════════════════════════════════════════════════
        //  WIZARD STATE — CLICKABLE STEPPER (Chat 22 v3)
        // ═════════════════════════════════════════════════════════════════

        private int _currentStep = 1;
        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                if (_currentStep == value) return;
                _currentStep = value;
                OnPropertyChanged();
                // [CHAT 22 v3] All step-position-dependent properties
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(ShowNextButton));
                OnPropertyChanged(nameof(ShowGenerateButton));
                OnPropertyChanged(nameof(ShowFinishButton));
                OnPropertyChanged(nameof(IsStep1Active));
                OnPropertyChanged(nameof(IsStep2Active));
                OnPropertyChanged(nameof(IsStep3Active));
                OnPropertyChanged(nameof(IsStep4Active));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // ── Step completion — reflects ACTUAL state, not position (v3) ──
        public bool IsStep1Complete => HasFilter;
        public bool IsStep2Complete => PreviewVM.HasSelection;
        public bool IsStep3Complete => IsLastWorkflowComplete;
        public bool IsStep4Complete => HasLastPdf;

        // ── Which step badge is currently active (v3) ────────────────────
        public bool IsStep1Active => _currentStep == 1;
        public bool IsStep2Active => _currentStep == 2;
        public bool IsStep3Active => _currentStep == 3;
        public bool IsStep4Active => _currentStep == 4;

        // ── Navigation gates — can user jump to this step? (v3) ─────────
        public bool CanNavigateToStep1 => !_isGenerating;
        public bool CanNavigateToStep2 => HasFilter && !_isGenerating;
        public bool CanNavigateToStep3 => PreviewVM.HasSelection && !_isGenerating;
        public bool CanNavigateToStep4 => HasLastPdf && !_isGenerating;

        // ── Footer nav gates ─────────────────────────────────────────────
        public bool CanGoPrevious => _currentStep > 1 && !_isGenerating;
        public bool CanGoNext => _currentStep < 4 && !_isGenerating;

        public bool ShowNextButton => _currentStep == 1 && !_isGenerating;
        public bool ShowGenerateButton => _currentStep == 2 && !_isGenerating;
        public bool ShowFinishButton => _currentStep == 4;

        // ── Commands ─────────────────────────────────────────────────────
        public ICommand NextStepCommand { get; private set; } = null!;
        public ICommand PreviousStepCommand { get; private set; } = null!;
        public ICommand GoToStepCommand { get; private set; } = null!;   // [CHAT 22 v3]

        /// <summary>
        /// Public entry point called by Shell code-behind when Step 3 activates.
        /// [CHAT 22 v3] Added guard: don't re-run if already completed successfully.
        /// </summary>
        public async Task RunPipelineAsync()
        {
            if (_isGenerating) return;

            // [CHAT 22 v3] Don't re-run if we already have a successful result
            if (IsLastWorkflowComplete && HasLastPdf)
            {
                AppLogService.Debug("[VM] Skipping pipeline re-run — already complete");
                return;
            }

            await RunGenerateAsync();

            if (IsLastWorkflowComplete || HasLastPdf)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    CurrentStep = 4;
                });
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  COMMANDS
        // ═════════════════════════════════════════════════════════════════

        public ICommand PreviewCommand { get; }
        public ICommand GenerateCommand { get; }
        public ICommand GenerateAgainCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand DownloadLastPdfCommand { get; }
        public ICommand CopyPdfPathCommand { get; }
        public ICommand SelectMergedTabCommand { get; }
        public ICommand SelectBalanceTabCommand { get; }
        public ICommand SelectAlertsTabCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public StatementGeneratorViewModel(IStatementRepository repo)
        {
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
                CommandManager.InvalidateRequerySuggested();
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

            SelectMergedTabCommand = new RelayCommand(_ => ActivePdfTab = "Merged");
            SelectBalanceTabCommand = new RelayCommand(_ => ActivePdfTab = "Balance");
            SelectAlertsTabCommand = new RelayCommand(_ => ActivePdfTab = "Alerts");

            // ── Wizard navigation commands ──────────────────────────────
            NextStepCommand = new RelayCommand(_ =>
            {
                if (_currentStep == 1 && HasFilter)
                {
                    _ = RunPreviewAsync();
                    CurrentStep = 2;
                }
                else if (_currentStep == 2 && PreviewVM.HasSelection)
                {
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

        // ═════════════════════════════════════════════════════════════════
        //  NAVIGATION NOTIFICATION HELPERS (Chat 22 v3)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Notifies all filter-dependent properties + Step 2 gate.
        /// Called from filter setters + status item changes.
        /// </summary>
        private void NotifyFilterDependents()
        {
            OnPropertyChanged(nameof(HasFilter));
            OnPropertyChanged(nameof(IsStep1Complete));
            OnPropertyChanged(nameof(CanNavigateToStep2));
        }

        /// <summary>
        /// Notifies ALL step navigation gates + completion states.
        /// Called when IsGenerating flips or wizard state resets.
        /// </summary>
        private void NotifyAllNavigationStates()
        {
            OnPropertyChanged(nameof(IsStep1Complete));
            OnPropertyChanged(nameof(IsStep2Complete));
            OnPropertyChanged(nameof(IsStep3Complete));
            OnPropertyChanged(nameof(IsStep4Complete));
            OnPropertyChanged(nameof(CanNavigateToStep1));
            OnPropertyChanged(nameof(CanNavigateToStep2));
            OnPropertyChanged(nameof(CanNavigateToStep3));
            OnPropertyChanged(nameof(CanNavigateToStep4));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(ShowNextButton));
            OnPropertyChanged(nameof(ShowGenerateButton));
            OnPropertyChanged(nameof(ShowFinishButton));
        }

        // ═════════════════════════════════════════════════════════════════
        //  DEBOUNCE
        // ═════════════════════════════════════════════════════════════════

        private void SchedulePreview()
        {
            _debounceTimer.Stop();

            if (!HasFilter)
            {
                PreviewSnapshot = null;
                PreviewVM.UpdateFromSnapshot(null);
                StatusMessage = "";
                return;
            }

            if (!_autoPreviewEnabled)
            {
                StatusMessage = "Filters changed — click Preview to update.";
                return;
            }

            _debounceTimer.Start();
        }

        // ═════════════════════════════════════════════════════════════════
        //  PREPARE
        // ═════════════════════════════════════════════════════════════════

        public void PrepareNew()
        {
            _editId = 0;
            _editNumber = "";
            _pendingListRefresh = false;
            CurrentStep = 1;
            ClearFilters();
            PreviewSnapshot = null;

            Application.Current?.Dispatcher.Invoke(() => PreviewVM.UpdateFromSnapshot(null));

            ResetWorkflowDisplayState();
            NotifyAllNavigationStates();   // [CHAT 22 v3]

            StatusMessage = "";
            OnPropertyChanged(string.Empty);
        }

        public void PrepareEdit(StatementListItem item)
        {
            _editId = item.Id;
            _editNumber = item.StatementNumber;
            _pendingListRefresh = false;
            CurrentStep = 1;

            _debounceTimer.Stop();

            _filterCompany = item.FilterCompany ?? "";
            _filterCustomer = item.FilterCustomer ?? "";
            _filterProject = item.FilterProject ?? "";
            _filterSalesman = item.FilterSalesman ?? "";
            _dateFrom = item.FilterDateFrom;
            _dateTo = item.FilterDateTo;

            PreviewSnapshot = null;

            Application.Current?.Dispatcher.Invoke(() => PreviewVM.UpdateFromSnapshot(null));

            ResetWorkflowDisplayState();
            NotifyAllNavigationStates();   // [CHAT 22 v3]

            StatusMessage = $"Editing {item.StatementNumber} — loading matching invoices...";
            OnPropertyChanged(string.Empty);

            SchedulePreview();
        }

        private void ResetWorkflowDisplayState()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                LastWorkflowSteps.Clear();
                ProcessingLog.Clear();
                WorkflowWarnings.Clear();
                MergedPdfSections.Clear();
                BalancePdfSections.Clear();
                AlertsPdfSections.Clear();

                LastWorkflowProgressPercent = 0;
                LastWorkflowStatusText = "Ready to Generate";
                HasWorkflowRun = false;
                IsLastWorkflowComplete = false;

                OrderedQtyMetric = 0;
                DeliveredQtyMetric = 0;
                BalanceQtyMetric = 0;
                DeliveryRatePercent = 0;

                WarningCount = 0;
                ErrorCount = 0;
                InfoCount = 0;

                LastPdfPath = "";
                LastPdfSize = "";
                LastPdfPageCount = 0;
                LastPdfGenerationSec = 0;

                ActivePdfTab = "Merged";

                OnPropertyChanged(nameof(HasLastPdf));
                OnPropertyChanged(nameof(MergedSectionCount));
                OnPropertyChanged(nameof(BalanceSectionCount));
                OnPropertyChanged(nameof(AlertsSectionCount));
            });
        }

        private void PrepareForNewWorkflowRun()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                LastWorkflowSteps.Clear();
                ProcessingLog.Clear();
                WorkflowWarnings.Clear();
                MergedPdfSections.Clear();
                BalancePdfSections.Clear();
                AlertsPdfSections.Clear();

                LastWorkflowProgressPercent = 0;
                LastWorkflowStatusText = "Preparing pipeline…";
                HasWorkflowRun = true;
                IsLastWorkflowComplete = false;

                OrderedQtyMetric = 0;
                DeliveredQtyMetric = 0;
                BalanceQtyMetric = 0;
                DeliveryRatePercent = 0;

                WarningCount = 0;
                ErrorCount = 0;
                InfoCount = 0;

                LastPdfPath = "";
                LastPdfSize = "";
                LastPdfPageCount = 0;
                LastPdfGenerationSec = 0;

                OnPropertyChanged(nameof(HasLastPdf));
                OnPropertyChanged(nameof(MergedSectionCount));
                OnPropertyChanged(nameof(BalanceSectionCount));
                OnPropertyChanged(nameof(AlertsSectionCount));
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  STATUS ITEM COUNT UPDATE
        // ═════════════════════════════════════════════════════════════════

        private void UpdateStatusItemCounts(StatementSnapshot? snapshot)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (snapshot == null || snapshot.InvoiceRows.Count == 0)
                {
                    foreach (var item in StatusItems) item.Count = 0;
                    return;
                }

                var grouped = snapshot.InvoiceRows
                    .GroupBy(r => (r.Status ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

                foreach (var item in StatusItems)
                    item.Count = grouped.TryGetValue(item.Status, out int c) ? c : 0;
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  MASTER DATA
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadMasterDataAsync()
        {
            try
            {
                var companies = await Task.Run(() => App.MasterData.GetAllCompaniesAsync());
                var clients = await Task.Run(() => App.MasterData.GetAllClientsAsync());
                var projects = await Task.Run(() => App.MasterData.GetAllProjectsAsync());
                var salesmen = await Task.Run(() => App.MasterData.GetAllSalesmenAsync());

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    Companies.Clear();
                    foreach (var c in companies.Select(c => c.CompanyName)
                        .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
                        Companies.Add(c);

                    Customers.Clear();
                    foreach (var c in clients.Select(c => c.CustomerName)
                        .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
                        Customers.Add(c);

                    Projects.Clear();
                    foreach (var p in projects
                        .Select(p => !string.IsNullOrWhiteSpace(p.ProjectContractNo)
                            ? p.ProjectContractNo.Trim()
                            : (p.ProjectIdentifierName?.Trim() ?? ""))
                        .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
                        Projects.Add(p);

                    Salesmen.Clear();
                    foreach (var s in salesmen.Select(s => s.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
                        Salesmen.Add(s);
                });
            }
            catch (Exception ex)
            {
                // Fully qualified to avoid ambiguity: this file also uses
                // LogEntry from Models.Statement.Workflow, which would clash
                // with an imported Logging.LogEntry if we used a using directive.
                ProGlassAutomation.Services.Logging.AppLogService.Error(
                    "[StatementGenerator] Master data load failed", ex);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  PREVIEW
        // ═════════════════════════════════════════════════════════════════

        private async Task RunPreviewAsync()
        {
            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            IsLoading = true;
            StatusMessage = "Loading matching invoices…";
            PreviewSnapshot = null;

            Application.Current?.Dispatcher.Invoke(() => PreviewVM.UpdateFromSnapshot(null));

            try
            {
                var filter = BuildFilter();
                var number = await Task.Run(() => _repo.GetNextStatementNumberAsync(), token);

                if (token.IsCancellationRequested) return;

                var snapshot = await Task.Run(() => _aggregator.BuildAsync(filter, number), token);

                if (token.IsCancellationRequested) return;

                PreviewSnapshot = snapshot;

                Application.Current?.Dispatcher.Invoke(() => PreviewVM.UpdateFromSnapshot(snapshot));

                if (snapshot == null)
                {
                    StatusMessage = "No invoices match the selected filters. Try adjusting your criteria.";
                }
                else
                {
                    int count = snapshot.InvoiceRows.Count;
                    StatusMessage =
                        $"Found {count} invoice{(count == 1 ? "" : "s")}  ·  " +
                        $"Total: AED {snapshot.Financial.TotalInvoiced:N2}  ·  " +
                        $"Outstanding: AED {snapshot.Financial.TotalOutstanding:N2}";
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            catch (Exception ex)
            {
                StatusMessage = $"Preview failed: {ex.Message}";
                PreviewSnapshot = null;
                Application.Current?.Dispatcher.Invoke(() => PreviewVM.UpdateFromSnapshot(null));
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  GENERATE — INLINE EXECUTION (unchanged from Chat 21 v2)
        // ═════════════════════════════════════════════════════════════════

        private async Task RunGenerateAsync()
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.StatementGenerate,
                "generate a statement"))
                return;

            if (_previewSnapshot == null) return;

            var selectedNumbers = PreviewVM.GetSelectedInvoiceNumbers();
            if (selectedNumbers.Count == 0)
            {
                StatusMessage = "No invoices selected. Please select at least one invoice.";
                return;
            }

            IsGenerating = true;
            StatusMessage = "Preparing workflow…";
            var generationStart = DateTime.Now;

            var engine = new StatementWorkflowEngine();
            PrepareForNewWorkflowRun();

            Application.Current?.Dispatcher.Invoke(() =>
            {
                LastWorkflowSteps.Clear();
                foreach (var step in engine.Steps)
                    LastWorkflowSteps.Add(step);
            });

            SubscribeToLiveStepUpdates(engine.Steps);

            LastWorkflowStatusText = "Running Pipeline…";
            LiveAddLogEntry(LogSeverity.Section, "═══ STATEMENT GENERATION STARTED ═══");
            LiveAddLogEntry(LogSeverity.Info, $"› Started at {DateTime.Now:HH:mm:ss}");
            LiveAddLogEntry(LogSeverity.Info, $"› Selected invoices: {selectedNumbers.Count}");

            WorkflowResult? wfResult = null;
            string pdfPath = "";

            try
            {
                var filter = BuildFilter();
                var number = await Task.Run(() => _repo.GetNextStatementNumberAsync());

                LiveAddLogEntry(LogSeverity.Info, $"› Statement number: {number}");

                wfResult = await engine.RunAsync(
                    filter,
                    number,
                    ShowMissingInvoicesDialogAsync);

                if (wfResult == null)
                {
                    StatusMessage = "Workflow was cancelled.";
                    LastWorkflowStatusText = "Cancelled";
                    LiveAddLogEntry(LogSeverity.Warning, "› Workflow cancelled — no result returned");
                    return;
                }

                if (!wfResult.ShouldGeneratePdf)
                {
                    StatusMessage = wfResult.Summary;
                    LastWorkflowStatusText = wfResult.Outcome switch
                    {
                        WorkflowOutcome.Cancelled => "Cancelled by User",
                        WorkflowOutcome.Blocked => "Pipeline Blocked",
                        _ => "Cannot Generate PDF"
                    };
                    LiveAddLogEntry(LogSeverity.Warning, $"› {wfResult.Summary}");
                    LiftWorkflowResultAfterCompletion(wfResult, "", 0);
                    return;
                }

                if (wfResult.Snapshot == null)
                {
                    StatusMessage = "Workflow returned no snapshot.";
                    LastWorkflowStatusText = "No Data";
                    LiveAddLogEntry(LogSeverity.Error, "› ✗ Engine returned null snapshot");
                    return;
                }

                var selectedSet = selectedNumbers.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var filteredRows = wfResult.Snapshot.InvoiceRows
                    .Where(r => selectedSet.Contains(r.InvoiceNumber))
                    .ToList();

                if (filteredRows.Count == 0)
                {
                    StatusMessage = "No selected invoices found in workflow result.";
                    LiveAddLogEntry(LogSeverity.Error, "› ✗ Selected invoices not found in result");
                    return;
                }

                wfResult.Snapshot = StatementSnapshotProjector.FilterByInvoices(
                    wfResult.Snapshot, selectedNumbers);
                wfResult.TaxInvoicePdfs = wfResult.TaxInvoicePdfs
                    .Where(p => selectedSet.Contains(p.InvoiceNumber))
                    .ToList();

                StatusMessage = "Generating final PDF…";
                LiveAddLogEntry(LogSeverity.Section, "═══ PDF GENERATION ═══");
                LiveAddLogEntry(LogSeverity.Info, "› Merging statement + invoices + attachments…");

                pdfPath = await StatementPdfExporter.ExportFromWorkflowAsync(wfResult);

                LiveAddLogEntry(LogSeverity.Success,
                    $"› ✓ PDF ready: {Path.GetFileName(pdfPath)}");

                double elapsedSec = (DateTime.Now - generationStart).TotalSeconds;
                LiftWorkflowResultAfterCompletion(wfResult, pdfPath, elapsedSec);

                AuditService.Log(
                    AuditSections.Statement, "Statement generated",
                    $"{wfResult.Snapshot.Header.StatementNumber} — " +
                    $"{wfResult.Snapshot.Financial.TotalInvoiceCount} invoices, " +
                    $"{elapsedSec:0.#}s",
                    "STMT", wfResult.Snapshot.Header.StatementNumber);

                StatusMessage = "Saving statement record…";
                LiveAddLogEntry(LogSeverity.Info, "› Saving to database…");

                bool dbSaveSucceeded = false;
                try
                {
                    var entity = new SavedStatement
                    {
                        StatementNumber = wfResult.Snapshot.Header.StatementNumber,
                        Title = wfResult.Snapshot.Header.Title,
                        FilterCompany = filter.CompanyName ?? "",
                        FilterCustomer = filter.CustomerName ?? "",
                        FilterProject = filter.ProjectName ?? "",
                        FilterSalesman = filter.SalesmanName ?? "",
                        FilterDateFrom = filter.DateFrom,
                        FilterDateTo = filter.DateTo,
                        InvoiceCount = wfResult.Snapshot.Financial.TotalInvoiceCount,
                        TotalInvoiced = wfResult.Snapshot.Financial.TotalInvoiced,
                        TotalOutstanding = wfResult.Snapshot.Financial.TotalOutstanding,
                        OverdueAmount = wfResult.Snapshot.Financial.OverdueAmount,
                        PdfPath = pdfPath,
                        HasAttachments = true,
                        FullSnapshotJson = JsonSerializer.Serialize(wfResult.Snapshot),
                        FilterJson = JsonSerializer.Serialize(filter)
                    };

                    foreach (var row in wfResult.Snapshot.InvoiceRows)
                    {
                        entity.LineItems.Add(new StatementLineItem
                        {
                            InvoiceNumber = row.InvoiceNumber,
                            ProformaId = row.ProformaId,
                            DeliveryNoteRef = row.DeliveryNoteRef,
                            InvoiceType = row.InvoiceType,
                            Status = row.Status,
                            IssueDate = row.IssueDate,
                            DueDate = row.DueDate,
                            TotalQty = row.TotalQty,
                            TotalSqm = row.TotalSqm,
                            Subtotal = row.Subtotal,
                            VatAmount = row.VatAmount,
                            GrandTotal = row.GrandTotal,
                            PaidAmount = row.PaidAmount,
                            DaysOverdue = row.DaysOverdue
                        });
                    }

                    if (_editId > 0)
                    {
                        entity.Id = _editId;
                        await Task.Run(() => _repo.UpdateAsync(entity));
                        dbSaveSucceeded = true;
                        LiveAddLogEntry(LogSeverity.Success, "› ✓ Updated in database");
                    }
                    else
                    {
                        int retryCount = 0;
                        const int maxRetries = 5;
                        while (retryCount < maxRetries)
                        {
                            try
                            {
                                await Task.Run(() => _repo.InsertAsync(entity));
                                dbSaveSucceeded = true;
                                LiveAddLogEntry(LogSeverity.Success,
                                    $"› ✓ Saved as {entity.StatementNumber}");
                                break;
                            }
                            catch (Exception retryEx) when (
                                retryEx.Message.Contains("UNIQUE constraint failed") ||
                                retryEx.InnerException?.Message.Contains("UNIQUE constraint failed") == true)
                            {
                                retryCount++;
                                LiveAddLogEntry(LogSeverity.Warning,
                                    $"› ⚠ Number {entity.StatementNumber} taken — retrying ({retryCount}/{maxRetries})…");

                                if (retryCount >= maxRetries) throw;

                                string newNumber = await Task.Run(() => _repo.GetNextStatementNumberAsync());
                                entity.StatementNumber = newNumber;
                                wfResult.Snapshot.Header.StatementNumber = newNumber;
                            }
                        }
                    }
                }
                catch (Exception dbEx)
                {
                    LiveAddLogEntry(LogSeverity.Error,
                        $"› ✗ DB save failed: {dbEx.Message}");
                    LiveAddLogEntry(LogSeverity.Warning,
                        $"› ⚠ PDF is available on disk but not registered in statement list");
                    AppLogService.Debug($"[StatementGenerator] DB save error: {dbEx}");
                }

                LiveAddLogEntry(LogSeverity.Section, "═══ COMPLETE ═══");
                LiveAddLogEntry(LogSeverity.Success,
                    $"› ✓ Statement generated in {elapsedSec:N1}s");
                LiveAddLogEntry(LogSeverity.Info,
                    $"› PDF: {Path.GetFileName(pdfPath)}");

                StatusMessage = dbSaveSucceeded
                    ? (wfResult.AllWarnings.Count > 0
                        ? $"✓ Statement generated with {wfResult.AllWarnings.Count} warning(s)."
                        : "✓ Statement generated successfully.")
                    : "⚠ PDF generated but database save failed. Check log.";

                LiveAddLogEntry(LogSeverity.Info,
                    "› Click 'Download PDF' in the results section to view the PDF");

                if (dbSaveSucceeded)
                {
                    LiveAddLogEntry(LogSeverity.Info,
                        "› Click 'Back' in the top bar to return to the statement list");
                    _pendingListRefresh = true;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Generation failed: {ex.Message}";
                LastWorkflowStatusText = "Pipeline Failed";
                LiveAddLogEntry(LogSeverity.Error, $"› ✗ FATAL ERROR: {ex.Message}");
                LiveAddLogEntry(LogSeverity.Error, $"› {ex.GetType().Name}");
                AppLogService.Debug($"[StatementGenerator] Generate error: {ex}");
            }
            finally
            {
                UnsubscribeFromLiveStepUpdates(engine.Steps);
                IsGenerating = false;
                LastWorkflowProgressPercent = 100;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  MISSING INVOICES DIALOG
        // ═════════════════════════════════════════════════════════════════

        private async Task<UserWarningDecision> ShowMissingInvoicesDialogAsync(
            WorkflowResult partialResult)
        {
            LiveAddLogEntry(LogSeverity.Warning,
                $"› ⚠ Uninvoiced deliveries detected — awaiting user decision");

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                return UserWarningDecision.Abort;

            return await dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var dialog = new WorkflowWarningDialog(partialResult)
                    {
                        Owner = Application.Current?.MainWindow
                    };
                    dialog.ShowDialog();

                    LiveAddLogEntry(LogSeverity.Info,
                        $"› User decision: {dialog.UserDecision}");

                    return dialog.UserDecision;
                }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[StatementGenerator] Warning dialog error: {ex}");
                    return UserWarningDecision.Abort;
                }
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  LIVE STEP UPDATE SUBSCRIPTIONS
        // ═════════════════════════════════════════════════════════════════

        private void SubscribeToLiveStepUpdates(List<WorkflowStepState> steps)
        {
            foreach (var step in steps)
            {
                step.PropertyChanged += OnLiveStepPropertyChanged;

                var currentStep = step;
                NotifyCollectionChangedEventHandler handler =
                    (s, e) => OnLiveDetailRowsChanged(currentStep, e);

                step.DetailRows.CollectionChanged += handler;
                _liveDetailSubscriptions[step] = handler;
            }
        }

        private void UnsubscribeFromLiveStepUpdates(List<WorkflowStepState> steps)
        {
            foreach (var step in steps)
            {
                step.PropertyChanged -= OnLiveStepPropertyChanged;

                if (_liveDetailSubscriptions.TryGetValue(step, out var handler))
                    step.DetailRows.CollectionChanged -= handler;
            }
            _liveDetailSubscriptions.Clear();
        }

        private void OnLiveStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not WorkflowStepState step) return;
            if (e.PropertyName != nameof(WorkflowStepState.Status)) return;

            RecalculateLiveProgress();

            if (step.IsRunning)
            {
                LiveAddLogEntry(LogSeverity.Section,
                    $"═══ Step {(int)step.Step}: {step.Title} ═══");
            }
            else if (step.IsComplete && !string.IsNullOrWhiteSpace(step.CurrentMessage))
            {
                var severity = step.Status switch
                {
                    StepStatus.Success => LogSeverity.Success,
                    StepStatus.Warning => LogSeverity.Warning,
                    StepStatus.Blocked => LogSeverity.Error,
                    StepStatus.Skipped => LogSeverity.Info,
                    _ => LogSeverity.Info
                };
                LiveAddLogEntry(severity,
                    $"› {step.StatusIcon} {step.CurrentMessage}");
            }
        }

        private void OnLiveDetailRowsChanged(
            WorkflowStepState step,
            NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add) return;
            if (e.NewItems == null) return;

            foreach (var item in e.NewItems.OfType<string>())
            {
                var severity = StatementLogHelper.InferLogSeverity(item);
                LiveAddLogEntry(severity, "› " + item);

                if (severity == LogSeverity.Info)
                    InfoCount++;
            }
        }

        private void RecalculateLiveProgress()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                int totalSteps = LastWorkflowSteps.Count;
                if (totalSteps == 0)
                {
                    LastWorkflowProgressPercent = 0;
                    return;
                }

                int completedSteps = LastWorkflowSteps.Count(s => s.IsComplete);
                int runningSteps = LastWorkflowSteps.Count(s => s.IsRunning);

                double progress = (completedSteps * 100.0 + runningSteps * 50.0) / totalSteps;
                LastWorkflowProgressPercent = Math.Min(100, progress);
            });
        }

        private void LiveAddLogEntry(LogSeverity severity, string text)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                AppLogService.Debug($"[LOG-DEBUG] NO DISPATCHER — dropping: {text}");
                return;
            }

            dispatcher.Invoke(() =>
            {
                ProcessingLog.Add(new ProGlassAutomation.Models.Statement.Workflow.LogEntry
                {
                    Severity = severity,
                    Text = text
                });
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  POST-COMPLETION LIFT
        // ═════════════════════════════════════════════════════════════════

        private void LiftWorkflowResultAfterCompletion(
            WorkflowResult wf,
            string pdfPath,
            double elapsedSec)
        {
            if (wf == null) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                LastWorkflowStatusText = wf.Outcome switch
                {
                    WorkflowOutcome.Success => "Pipeline Complete",
                    WorkflowOutcome.Warning => "Completed with Warnings",
                    WorkflowOutcome.Blocked => "Pipeline Blocked",
                    WorkflowOutcome.Cancelled => "Pipeline Cancelled",
                    _ => "Ready"
                };

                IsLastWorkflowComplete =
                    wf.Outcome == WorkflowOutcome.Success ||
                    wf.Outcome == WorkflowOutcome.Warning;

                OrderedQtyMetric = wf.OrderedQty;
                DeliveredQtyMetric = wf.DeliveredQty;
                BalanceQtyMetric = wf.PendingProductionQty;

                DeliveryRatePercent = wf.OrderedQty > 0
                    ? Math.Round((double)wf.DeliveredQty / wf.OrderedQty * 100.0, 1)
                    : 0;

                BuildWorkflowWarnings(wf);

                LastPdfPath = pdfPath;
                LastPdfGeneratedAt = DateTime.Now;
                LastPdfGenerationSec = elapsedSec;

                if (!string.IsNullOrWhiteSpace(pdfPath) && File.Exists(pdfPath))
                {
                    try
                    {
                        var fi = new FileInfo(pdfPath);
                        LastPdfSize = FormatFileSize(fi.Length);
                    }
                    catch { LastPdfSize = ""; }

                    try
                    {
                        using var doc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.InformationOnly);
                        LastPdfPageCount = doc.PageCount;
                    }
                    catch { LastPdfPageCount = 0; }
                }
                else
                {
                    LastPdfSize = "";
                    LastPdfPageCount = 0;
                }

                OnPropertyChanged(nameof(HasLastPdf));
                // [CHAT 22 v3] Notify navigation gates now that state is final
                NotifyAllNavigationStates();

                BuildPdfSections(wf);
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  BUILD WARNINGS FEED
        // ═════════════════════════════════════════════════════════════════

        private void BuildWorkflowWarnings(WorkflowResult wf)
        {
            WorkflowWarnings.Clear();
            int errCount = 0;
            int warnCount = 0;

            if (wf.Snapshot != null)
            {
                var overdueRows = wf.Snapshot.InvoiceRows
                    .Where(r => r.IsOverdue)
                    .OrderByDescending(r => r.DaysOverdue)
                    .ToList();

                foreach (var row in overdueRows)
                {
                    WorkflowWarnings.Add(new WorkflowWarningItem
                    {
                        Severity = LogSeverity.Error,
                        Icon = "🔴",
                        Title = "Overdue Invoice",
                        Detail = $"{row.InvoiceNumber} is overdue by " +
                                 $"{row.DaysOverdue} days " +
                                 $"(due: {row.DueDate:MMM d, yyyy})",
                        AmountDisplay = $"AED {row.Balance:N2}",
                        ActionLabel = "→ Send reminder",
                        StepLabel = "Post-workflow · Overdue Check",
                        HasQuantities = false
                    });
                    errCount++;
                }
            }

            foreach (var udn in wf.UninvoicedDeliveryNotes)
            {
                WorkflowWarnings.Add(new WorkflowWarningItem
                {
                    Severity = LogSeverity.Warning,
                    Icon = "🟡",
                    Title = "Uninvoiced Delivery",
                    Detail = $"{udn.DeliveryNoteId} delivered {udn.DeliveredQty} units " +
                             $"on {udn.DeliveryDate:MMM d, yyyy} — no invoice yet",
                    AmountDisplay = "",
                    ActionLabel = "→ Create invoice",
                    StepLabel = "Step 6 · Coverage Check",
                    HasQuantities = true,
                    OrderedText = wf.OrderedQty.ToString("N0"),
                    DeliveredText = udn.DeliveredQty.ToString("N0"),
                    DifferenceText = $"-{Math.Max(0, wf.OrderedQty - udn.DeliveredQty):N0}"
                });
                warnCount++;
            }

            foreach (var step in wf.Steps)
            {
                foreach (var w in step.Warnings)
                {
                    WorkflowWarnings.Add(new WorkflowWarningItem
                    {
                        Severity = LogSeverity.Warning,
                        Icon = "⚠",
                        Title = "Workflow Warning",
                        Detail = w,
                        AmountDisplay = "",
                        ActionLabel = "→ Review",
                        StepLabel = $"Step {(int)step.Step} · {step.Title}",
                        HasQuantities = false
                    });
                    warnCount++;
                }
            }

            ErrorCount = errCount;
            WarningCount = warnCount;
        }

        // ═════════════════════════════════════════════════════════════════
        //  BUILD PDF SECTIONS
        // ═════════════════════════════════════════════════════════════════

        private void BuildPdfSections(WorkflowResult wf)
        {
            MergedPdfSections.Clear();
            BalancePdfSections.Clear();
            AlertsPdfSections.Clear();

            if (wf.Snapshot == null) return;

            MergedPdfSections.Add(new PdfSection
            { Icon = "📄", IconColorHex = "#4338CA", Title = "Statement Cover Page", PageInfo = "1 page" });

            MergedPdfSections.Add(new PdfSection
            { Icon = "💰", IconColorHex = "#8B5CF6", Title = "Financial Summary", PageInfo = "1 page" });

            int invoiceCount = wf.Snapshot.InvoiceRows.Count;
            if (invoiceCount > 0)
            {
                int detailPages = Math.Max(1, (invoiceCount + 9) / 10);
                MergedPdfSections.Add(new PdfSection
                {
                    Icon = "📊",
                    IconColorHex = "#3B82F6",
                    Title = "Invoice Detail Table",
                    PageInfo = $"{detailPages:N0} page{(detailPages == 1 ? "" : "s")}"
                });
            }

            if (_includeAgingChart && wf.Snapshot.Aging.Total > 0)
                MergedPdfSections.Add(new PdfSection
                { Icon = "📈", IconColorHex = "#F59E0B", Title = "Aging Analysis Chart", PageInfo = "1 page" });

            if (_includeBalanceAnalysis)
                MergedPdfSections.Add(new PdfSection
                { Icon = "⚖", IconColorHex = "#10B981", Title = "Balance Comparison", PageInfo = "1 page" });

            if (wf.SignedProformaInvoiceBytes != null && wf.SignedProformaInvoiceBytes.Length > 0)
            {
                string piName = string.IsNullOrWhiteSpace(wf.SignedProformaInvoiceFileName)
                    ? "Signed_Proforma_Invoice.pdf"
                    : wf.SignedProformaInvoiceFileName;

                MergedPdfSections.Add(new PdfSection
                { Icon = "📎", IconColorHex = "#6366F1", Title = piName, PageInfo = "1 page" });
            }

            foreach (var invPdf in wf.TaxInvoicePdfs)
            {
                if (invPdf.IsValid)
                {
                    MergedPdfSections.Add(new PdfSection
                    {
                        Icon = "🧾",
                        IconColorHex = "#4338CA",
                        Title = $"{invPdf.InvoiceNumber}.pdf",
                        PageInfo = "Auto",
                        Status = PdfSectionStatus.Normal
                    });
                }
                else
                {
                    MergedPdfSections.Add(new PdfSection
                    {
                        Icon = "⚠",
                        IconColorHex = "#B45309",
                        Title = $"{invPdf.InvoiceNumber}.pdf",
                        PageInfo = "1 page",
                        Status = PdfSectionStatus.Placeholder
                    });
                }

                foreach (var (_, fileName) in invPdf.Attachments)
                {
                    bool isImage = StatementLogHelper.IsImageFileName(fileName);
                    MergedPdfSections.Add(new PdfSection
                    {
                        Icon = "📎",
                        IconColorHex = "#6366F1",
                        Title = fileName,
                        PageInfo = "1 page",
                        Status = isImage ? PdfSectionStatus.Converted : PdfSectionStatus.Normal
                    });
                }
            }

            foreach (var dn in wf.SignedDeliveryNotes)
            {
                for (int i = 0; i < dn.AttachmentFileNames.Count; i++)
                {
                    string fileName = dn.AttachmentFileNames[i];
                    bool isImage = StatementLogHelper.IsImageFileName(fileName);

                    MergedPdfSections.Add(new PdfSection
                    {
                        Icon = "🚚",
                        IconColorHex = "#EC4899",
                        Title = fileName,
                        PageInfo = "1 page",
                        Status = isImage ? PdfSectionStatus.Converted : PdfSectionStatus.Normal
                    });
                }
            }

            BalancePdfSections.Add(new PdfSection
            { Icon = "💰", IconColorHex = "#8B5CF6", Title = "Financial Summary", PageInfo = "1 page" });

            if (wf.Snapshot.Aging.Total > 0)
                BalancePdfSections.Add(new PdfSection
                { Icon = "📈", IconColorHex = "#F59E0B", Title = "Aging Analysis Chart", PageInfo = "1 page" });

            BalancePdfSections.Add(new PdfSection
            { Icon = "⚖", IconColorHex = "#10B981", Title = "Balance Comparison", PageInfo = "1 page" });

            BalancePdfSections.Add(new PdfSection
            { Icon = "📊", IconColorHex = "#3B82F6", Title = "Outstanding Breakdown", PageInfo = "1 page" });

            BalancePdfSections.Add(new PdfSection
            { Icon = "💵", IconColorHex = "#047857", Title = "Payment History", PageInfo = "1 page" });

            if (WorkflowWarnings.Count > 0)
                AlertsPdfSections.Add(new PdfSection
                {
                    Icon = "⚠",
                    IconColorHex = "#B45309",
                    Title = "Warnings Summary",
                    PageInfo = $"{Math.Max(1, (WorkflowWarnings.Count + 4) / 5)} page(s)",
                    Status = PdfSectionStatus.Warning
                });

            if (ErrorCount > 0)
                AlertsPdfSections.Add(new PdfSection
                {
                    Icon = "🔴",
                    IconColorHex = "#B91C1C",
                    Title = "Overdue Invoices",
                    PageInfo = $"{ErrorCount:N0} entries",
                    Status = PdfSectionStatus.Warning
                });

            if (wf.UninvoicedDeliveryNotes.Count > 0)
                AlertsPdfSections.Add(new PdfSection
                {
                    Icon = "🟡",
                    IconColorHex = "#B45309",
                    Title = "Uninvoiced Deliveries",
                    PageInfo = $"{wf.UninvoicedDeliveryNotes.Count:N0} entries",
                    Status = PdfSectionStatus.Warning
                });

            AlertsPdfSections.Add(new PdfSection
            {
                Icon = "📋",
                IconColorHex = "#6366F1",
                Title = "Workflow Audit Trail",
                PageInfo = $"{wf.Steps.Count:N0} steps"
            });

            OnPropertyChanged(nameof(MergedSectionCount));
            OnPropertyChanged(nameof(BalanceSectionCount));
            OnPropertyChanged(nameof(AlertsSectionCount));
        }

        // ═════════════════════════════════════════════════════════════════
        //  PDF ACTIONS
        // ═════════════════════════════════════════════════════════════════

        private void OpenLastPdf()
        {
            if (!HasLastPdf) return;
            try { StatementPdfExporter.OpenPdf(_lastPdfPath); }
            catch (Exception ex) { StatusMessage = $"Could not open PDF: {ex.Message}"; }
        }

        private void CopyLastPdfPathToClipboard()
        {
            if (!HasLastPdf) return;
            try
            {
                Clipboard.SetText(_lastPdfPath);
                StatusMessage = "PDF path copied to clipboard.";
            }
            catch (Exception ex) { StatusMessage = $"Could not copy path: {ex.Message}"; }
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════

        private StatementFilter BuildFilter() => new()
        {
            CompanyName = TextParsing.NullIfEmpty(_filterCompany),
            CustomerName = TextParsing.NullIfEmpty(_filterCustomer),
            ProjectName = TextParsing.NullIfEmpty(_filterProject),
            SalesmanName = TextParsing.NullIfEmpty(_filterSalesman),
            DateFrom = _dateFrom,
            DateTo = _dateTo,
            Statuses = StatusItems.Where(s => s.IsChecked).Select(s => s.Status).ToList(),
            IncludeAttachments = _includeAttachments,
            IncludeDnDetails = _includeDnDetails
        };

        private void ClearFilters()
        {
            _debounceTimer.Stop();

            _filterCompany = "";
            _filterCustomer = "";
            _filterProject = "";
            _filterSalesman = "";
            _dateFrom = null;
            _dateTo = null;
            _includeAttachments = true;
            _includeDnDetails = false;
            _includeBalanceAnalysis = true;
            _includeAgingChart = true;
            _includeInsertPlaceholders = true;
            _includeWarningsSummary = true;

            foreach (var s in StatusItems)
                s.IsChecked = s.Status is "Issued" or "Paid" or "PartiallyPaid" or "Overdue";

            OnPropertyChanged(string.Empty);

            PreviewSnapshot = null;
            Application.Current?.Dispatcher.Invoke(() => PreviewVM.UpdateFromSnapshot(null));

            StatusMessage = "Filters cleared.";
        }


        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:N1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):N1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):N2} GB";
        }
    }

}
