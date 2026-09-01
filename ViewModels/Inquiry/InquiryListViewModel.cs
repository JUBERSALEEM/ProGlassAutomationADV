using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.MasterData.Repository;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.Inquiry
{
    /// <summary>
    /// Inquiry register — 2026 SaaS rebuild.
    ///
    /// Read path: segmented status tabs → database-side paged projection.
    /// Write path: editor dialog / drawer quick actions → workflow service
    /// (guarded transitions, audit, version snapshots).
    ///
    /// Design notes (fixes from the senior analysis):
    /// - date filter defaults to "All time" (S1: saved inquiries vanished);
    /// - cancellations return quietly (S7: log spam);
    /// - selection survives a reload (A7: drawer slammed shut after save);
    /// - no placeholder commands — every visible control does real work.
    /// </summary>
    public sealed class InquiryListViewModel : ViewModelBase, System.IDisposable
    {
        private readonly IInquiryRepository _repository;
        private readonly IInquiryWorkflowService _workflow;
        private readonly InquiryAttachmentService _attachments;
        private readonly InquiryImportExportService _io;
        private CancellationTokenSource? _loadCts;
        private CancellationTokenSource? _debounceCts;
        private CancellationTokenSource? _importCts;
        private int _reloadGeneration;

        public ObservableCollection<InquiryListRow> Inquiries { get; } = new();
        public ObservableCollection<InquiryStatusTab> StatusTabs { get; } = new();
        public ObservableCollection<InquiryPipelineTab> PipelineTabs { get; } = new();
        public ObservableCollection<string> DateRanges { get; } =
            new() { "All time", "Today", "Yesterday", "Last 7 Days", "Last 30 Days" };
        public ObservableCollection<string> Salesmen { get; } = new() { "All salesmen" };
        public ObservableCollection<string> PreparedByUsers { get; } = new() { "All prepared by" };
        public ObservableCollection<string> Priorities { get; } = new() { "All priorities", "Urgent", "High", "Normal", "Low" };
        public ObservableCollection<string> SortOptions { get; } =
            new() { "Updated newest", "Updated oldest", "Received newest", "Received oldest", "Inquiry number", "Value highest" };

        // ── Selection & drawer ─────────────────────────────────────────

        private InquiryListRow? _selectedInquiry;
        public InquiryListRow? SelectedInquiry
        {
            get => _selectedInquiry;
            set
            {
                if (ReferenceEquals(_selectedInquiry, value)) return;
                _selectedInquiry = value;
                IsDetailsDrawerOpen = value != null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(CanConvertSelected));
                if (value != null)
                    AsyncUtility.SafeFireAndForget(() => LoadSelectedDetailAsync(value), "Inquiry drawer detail");
            }
        }

        private bool _isDetailsDrawerOpen;
        public bool IsDetailsDrawerOpen { get => _isDetailsDrawerOpen; private set { _isDetailsDrawerOpen = value; OnPropertyChanged(); } }
        public bool HasSelection => SelectedInquiry != null;
        public bool CanConvertSelected => SelectedInquiry is { CanConvert: true };

        // ── KPI strip ──────────────────────────────────────────────────

        private decimal _pipelineValue;
        public decimal PipelineValue { get => _pipelineValue; private set { _pipelineValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(PipelineValueText)); } }
        public string PipelineValueText => PipelineValue == 0 ? "—" : $"AED {PipelineValue:N0}";
        private int _openCount;
        public int OpenCount { get => _openCount; private set { _openCount = value; OnPropertyChanged(); } }
        private int _awaitingCount;
        public int AwaitingCount { get => _awaitingCount; private set { _awaitingCount = value; OnPropertyChanged(); } }
        private int _confirmedCount;
        public int ConfirmedCount { get => _confirmedCount; private set { _confirmedCount = value; OnPropertyChanged(); } }

        private string _pageStatus = "Loading…";
        public string PageStatus { get => _pageStatus; private set { _pageStatus = value; OnPropertyChanged(); } }
        public bool HasInquiries => Inquiries.Count > 0;

        // ── Filters ────────────────────────────────────────────────────

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { if (_searchText == value) return; _searchText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasActiveFilters)); DebounceReload(); }
        }
        // S1 fix: "All time" default — a register must not hide rows by accident.
        private string _selectedDateRange = "All time";
        public string SelectedDateRange { get => _selectedDateRange; set { if (_selectedDateRange == value) return; _selectedDateRange = value; OnPropertyChanged(); ResetAndReload(); } }
        private string _selectedSalesman = "All salesmen";
        public string SelectedSalesman { get => _selectedSalesman; set { if (_selectedSalesman == value) return; _selectedSalesman = value; OnPropertyChanged(); ResetAndReload(); } }
        private string _selectedPreparedBy = "All prepared by";
        public string SelectedPreparedBy { get => _selectedPreparedBy; set { if (_selectedPreparedBy == value) return; _selectedPreparedBy = value; OnPropertyChanged(); ResetAndReload(); } }
        private string _selectedPriority = "All priorities";
        public string SelectedPriority { get => _selectedPriority; set { if (_selectedPriority == value) return; _selectedPriority = value; OnPropertyChanged(); ResetAndReload(); } }
        private string _selectedSort = "Updated newest";
        public string SelectedSort { get => _selectedSort; set { if (_selectedSort == value) return; _selectedSort = value; OnPropertyChanged(); ResetAndReload(); } }
        private bool _hasAttachmentsOnly;
        public bool HasAttachmentsOnly { get => _hasAttachmentsOnly; set { if (_hasAttachmentsOnly == value) return; _hasAttachmentsOnly = value; OnPropertyChanged(); ResetAndReload(); } }
        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(SearchText) || SelectedDateRange != "All time"
            || SelectedSalesman != "All salesmen" || SelectedPreparedBy != "All prepared by"
            || SelectedPriority != "All priorities" || HasAttachmentsOnly
            || SelectedStatusKey.Length > 0
            || SelectedPipelineKind.HasValue;

        /// <summary>"" = All statuses; otherwise a controlled status value.</summary>
        public string SelectedStatusKey
        {
            get => StatusTabs.FirstOrDefault(x => x.IsSelected)?.Key ?? "";
            private set
            {
                foreach (var tab in StatusTabs)
                    tab.IsSelected = tab.Key == value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActiveFilters));
            }
        }

        /// <summary>
        /// Optional pipeline-stage filter: when set, only inquiries that
        /// have reached that downstream stage are shown. Null = no filter.
        /// </summary>
        public InquiryLifecycleKind? SelectedPipelineKind
        {
            get => PipelineTabs.FirstOrDefault(x => x.IsSelected)?.Kind;
            private set
            {
                foreach (var tab in PipelineTabs)
                    tab.IsSelected = value.HasValue && tab.Kind == value.Value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActiveFilters));
            }
        }

        // ── UI callbacks (kept UI-free for testability) ────────────────

        public Func<InquiryRecord?, Task<bool>>? EditRequested { get; set; }
        public Func<InquiryRecord, Task>? CreateProformaRequested { get; set; }
        public Func<string?>? ExportPathRequested { get; set; }
        public Func<string?>? ImportPathRequested { get; set; }
        public Func<int, bool>? ConfirmImportRequested { get; set; }
        public Func<InquiryAttachment, bool>? ConfirmDeleteAttachmentRequested { get; set; }
        public Action<InquiryAttachment>? PreviewAttachmentRequested { get; set; }
        // Right-click context-menu callbacks. Wired by the view so the VM
        // stays MessageBox-free. Each one defaults to a no-op (or
        // clipboard fallback) if the view never sets it.
        public Action<InquiryListRow>? OpenAttachmentsFolderRequested { get; set; }
        // Per-stage navigation. The second argument is the row ID of
        // the downstream document (PI/JO/DN/TI). The MainViewModel
        // uses it to actually load the document, not just navigate
        // to the module. When the row ID is 0 (no downstream
        // document exists), the handler falls back to navigating to
        // the module and copying the document number to the clipboard
        // so the user can paste it into the module's own search.
        public Func<InquiryListRow, int, Task>? OpenProformaForRowRequested { get; set; }
        public Func<InquiryListRow, int, Task>? OpenJobOrderForRowRequested { get; set; }
        public Func<InquiryListRow, int, Task>? OpenDeliveryNoteForRowRequested { get; set; }
        public Func<InquiryListRow, int, Task>? OpenTaxInvoiceForRowRequested { get; set; }
        public Func<InquiryListRow, int, Task>? OpenStatementForRowRequested { get; set; }
        // Confirms a destructive delete. Returns true to proceed. The
        // view wires this to a MessageBox so the VM stays UI-free.
        // The parameter is the row that will be deleted; the message
        // box should mention the inquiry number for clarity.
        public Func<InquiryListRow, bool>? ConfirmDeleteInquiryRequested { get; set; }

        // ── Commands ───────────────────────────────────────────────────

        public ICommand NewInquiryCommand { get; }
        public ICommand EditInquiryCommand { get; }
        public ICommand SelectStatusTabCommand { get; }
        public ICommand SelectPipelineTabCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ResetFiltersCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand CloseDetailsCommand { get; }
        public ICommand OpenWorkspaceCommand { get; }
        public ICommand ConvertToPiCommand { get; }
        public ICommand ChangeStatusCommand { get; }
        public ICommand OpenAttachmentCommand { get; }
        public ICommand PreviewAttachmentCommand { get; }
        public ICommand DeleteAttachmentCommand { get; }
        // Right-click context-menu commands. All take an InquiryListRow
        // parameter (the row the user right-clicked) so the menu can
        // re-bind to whatever row fired it, not just the rail's selection.
        public ICommand OpenWorkspaceRowCommand { get; }
        public ICommand ConvertToPiRowCommand { get; }
        // Per-status row commands. Each takes an InquiryListRow parameter
        // and applies the legal transition. We can't reuse ChangeStatusRowCommand
        // for the context menu because WPF's CommandParameter is a single
        // value — binding a Tuple<> from XAML requires a MultiBinding +
        // converter, which is more code than five small commands.
        public ICommand PrepareRowCommand { get; }
        public ICommand SendRowCommand { get; }
        public ICommand ConfirmRowCommand { get; }
        public ICommand HoldRowCommand { get; }
        public ICommand CancelRowCommand { get; }
        public ICommand CopyInquiryNumberCommand { get; }
        public ICommand CopySummaryCommand { get; }
        public ICommand OpenAttachmentsFolderCommand { get; }
        // Per-stage navigation commands fired from the rail pipeline.
        public ICommand OpenProformaForRowCommand { get; }
        public ICommand OpenJobOrderForRowCommand { get; }
        public ICommand OpenDeliveryNoteForRowCommand { get; }
        public ICommand OpenTaxInvoiceForRowCommand { get; }
        public ICommand OpenStatementForRowCommand { get; }
        // Copy a pipeline-stage document number to the clipboard.
        public ICommand CopyProformaNumberCommand { get; }
        public ICommand CopyJobOrderNumberCommand { get; }
        public ICommand CopyDeliveryNoteNumberCommand { get; }
        public ICommand CopyTaxInvoiceNumberCommand { get; }
        // Destructive / structural row actions. Wired through the
        // workflow service so they go through the same permission
        // gate + audit + version-snapshot path as the existing
        // operations.
        public ICommand EditInquiryRowCommand { get; }
        public ICommand DuplicateInquiryRowCommand { get; }
        public ICommand DeleteInquiryRowCommand { get; }
        // Change-priority submenu. Each item is a separate command
        // because the XAML CommandParameter carries the priority
        // value as a plain string.
        public ICommand SetUrgentPriorityCommand { get; }
        public ICommand SetHighPriorityCommand { get; }
        public ICommand SetNormalPriorityCommand { get; }
        public ICommand SetLowPriorityCommand { get; }
        // Cancel the "you were sent here from a new-PI prompt"
        // round-trip. Sends the user back to the Proforma list
        // without starting a new invoice.
        public ICommand CancelNewPiFromInquiryCommand { get; }

        // Set by the host (MainViewModel) when the user picked
        // "Start from Inquiry" in the PI module's new-PI prompt
        // and was sent here. The view binds to this to show a
        // banner that tells the user to pick a Confirmed inquiry
        // (or create one) to continue. The flag is cleared when
        // the user picks an inquiry (CreateProformaRequested
        // fires) or when they cancel.
        private bool _isAwaitingNewPiSelection;
        public bool IsAwaitingNewPiSelection
        {
            get => _isAwaitingNewPiSelection;
            set { if (_isAwaitingNewPiSelection == value) return; _isAwaitingNewPiSelection = value; OnPropertyChanged(); OnPropertyChanged(nameof(AwaitingNewPiBannerText)); }
        }
        public string AwaitingNewPiBannerText =>
            "Pick (or create) a Confirmed inquiry to start a new Proforma Invoice. " +
            "Use 'Convert to Proforma Invoice' on the right-click menu, or Cancel to go back.";

        private readonly IDialogService _dialogs = null!;
        private readonly IMasterDataRepository _masterData = null!;

        public InquiryListViewModel() : this(
            App.ServiceProvider?.GetService<IDialogService>() ?? new DialogService(),
            App.ServiceProvider?.GetService<IMasterDataRepository>(),
            App.ServiceProvider?.GetService<IInquiryRepository>() ?? new InquiryRepository(),
            new InquiryWorkflowService(App.ServiceProvider?.GetService<IInquiryRepository>() ?? new InquiryRepository()),
            new InquiryAttachmentService(App.ServiceProvider?.GetService<IInquiryRepository>() ?? new InquiryRepository()),
            new InquiryImportExportService()) { }

        public InquiryListViewModel(IDialogService? dialogs,
            IMasterDataRepository? masterData,
            IInquiryRepository repository,
            IInquiryWorkflowService workflow, InquiryAttachmentService attachments,
            InquiryImportExportService io)
        {
            _dialogs = dialogs!;
            _masterData = masterData!;
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
            _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
            _io = io ?? throw new ArgumentNullException(nameof(io));

            StatusTabs.Add(new InquiryStatusTab { Key = "", Label = "All", Count = 0, IsSelected = true });
            StatusTabs.Add(new InquiryStatusTab { Key = InquiryStatuses.Received, Label = "Received" });
            StatusTabs.Add(new InquiryStatusTab { Key = InquiryStatuses.Prepared, Label = "Prepared" });
            StatusTabs.Add(new InquiryStatusTab { Key = InquiryStatuses.Sent, Label = "Sent" });
            StatusTabs.Add(new InquiryStatusTab { Key = InquiryStatuses.Confirmed, Label = "Confirmed" });
            StatusTabs.Add(new InquiryStatusTab { Key = InquiryStatuses.Hold, Label = "Hold" });
            StatusTabs.Add(new InquiryStatusTab { Key = InquiryStatuses.Cancelled, Label = "Cancelled" });

            // Pipeline tabs — one per downstream document kind. Counts are
            // populated by ApplyStatistics after each page load. Default 0.
            foreach (var t in InquiryPipelineTab.All) PipelineTabs.Add(t);

            NewInquiryCommand = new RelayCommand(_ =>
                AsyncUtility.SafeFireAndForget(NewInquiryAsync, "Inquiry New"));
            EditInquiryCommand = new RelayCommand(p =>
            {
                var row = p as InquiryListRow ?? SelectedInquiry;
                if (row != null)
                    AsyncUtility.SafeFireAndForget(() => EditRowAsync(row), "Inquiry Edit");
            }, _ => SelectedInquiry != null);
            SelectStatusTabCommand = new RelayCommand(p =>
            {
                SelectedStatusKey = p as string ?? "";
                ResetAndReload();
            });
            SelectPipelineTabCommand = new RelayCommand(p =>
            {
                SelectedPipelineKind = p is InquiryLifecycleKind k ? k : (InquiryLifecycleKind?)null;
                ResetAndReload();
            });
            ExportCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(ExportAsync, "Inquiry Export"));
            ImportCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(ImportAsync, "Inquiry Import"));
            ResetFiltersCommand = new RelayCommand(_ => ResetFilters());
            RefreshCommand = new RelayCommand(_ => Reload());
            PreviousPageCommand = new RelayCommand(_ => { if (CanGoPrevious) { CurrentPage--; Reload(); } }, _ => CanGoPrevious);
            NextPageCommand = new RelayCommand(_ => { if (CanGoNext) { CurrentPage++; Reload(); } }, _ => CanGoNext);
            CloseDetailsCommand = new RelayCommand(_ => IsDetailsDrawerOpen = false);
            OpenWorkspaceCommand = new RelayCommand(_ =>
            {
                if (SelectedInquiry != null)
                    AsyncUtility.SafeFireAndForget(() => EditRowAsync(SelectedInquiry), "Inquiry OpenWorkspace");
            }, _ => SelectedInquiry != null);
            ConvertToPiCommand = new RelayCommand(_ =>
            {
                IsAwaitingNewPiSelection = false;
                AsyncUtility.SafeFireAndForget(ConvertToPiAsync, "Inquiry ConvertToPi");
            }, _ => CanConvertSelected);
            ChangeStatusCommand = new RelayCommand(p =>
                AsyncUtility.SafeFireAndForget(() => ChangeSelectedStatusAsync(p as string), "Inquiry ChangeStatus"),
                p => SelectedInquiry != null && p is string);
            OpenAttachmentCommand = new RelayCommand(p =>
            {
                if (p is InquiryAttachmentItem item)
                {
                    var result = _attachments.Open(item.Attachment);
                    if (!result.IsSuccess) SetStatus(result.Error);
                }
            });
            PreviewAttachmentCommand = new RelayCommand(p =>
            {
                if (p is InquiryAttachmentItem item) PreviewAttachmentRequested?.Invoke(item.Attachment);
            });
            DeleteAttachmentCommand = new RelayCommand(p =>
            {
                if (p is InquiryAttachmentItem item && SelectedInquiry is { } row)
                    AsyncUtility.SafeFireAndForget(() => DeleteAttachmentAsync(item, row), "Inquiry DeleteAttachment");
            });

            // ── Right-click context-menu commands ───────────────────────
            // Row-targeted variants of OpenWorkspace / Convert / ChangeStatus
            // so the context menu can fire on a row other than the rail's
            // SelectedInquiry. Internally they reuse the same logic — we just
            // promote the parameter into SelectedInquiry first so the rail,
            // KPI strip and double-click handlers all stay in sync.
            OpenWorkspaceRowCommand = new RelayCommand(p =>
            {
                if (p is InquiryListRow row)
                {
                    SelectedInquiry = row;
                    AsyncUtility.SafeFireAndForget(() => EditRowAsync(row), "Inquiry OpenWorkspaceRow");
                }
            }, p => p is InquiryListRow);
            ConvertToPiRowCommand = new RelayCommand(p =>
            {
                if (p is InquiryListRow row)
                {
                    // Clear the "new PI from inquiry" banner — the
                    // round-trip is complete now that the user picked.
                    IsAwaitingNewPiSelection = false;
                    SelectedInquiry = row;
                    AsyncUtility.SafeFireAndForget(ConvertToPiAsync, "Inquiry ConvertToPiRow");
                }
            }, p => p is InquiryListRow row && row.CanConvert);
            // Per-status row commands. Each promotes the row into the
            // selection (so the rail, KPI strip etc. update too) and then
            // delegates to the standard ChangeSelectedStatusAsync path.
            PrepareRowCommand = new RelayCommand(p => ExecuteStatusRowChange(p, InquiryStatuses.Prepared, "Inquiry PrepareRow"), p => p is InquiryListRow row && row.CanPrepareWorkflow);
            SendRowCommand = new RelayCommand(p => ExecuteStatusRowChange(p, InquiryStatuses.Sent, "Inquiry SendRow"), p => p is InquiryListRow row && row.CanSendWorkflow);
            ConfirmRowCommand = new RelayCommand(p => ExecuteStatusRowChange(p, InquiryStatuses.Confirmed, "Inquiry ConfirmRow"), p => p is InquiryListRow row && row.CanConfirmWorkflow);
            HoldRowCommand = new RelayCommand(p => ExecuteStatusRowChange(p, InquiryStatuses.Hold, "Inquiry HoldRow"), p => p is InquiryListRow row && row.CanHoldWorkflow);
            CancelRowCommand = new RelayCommand(p => ExecuteStatusRowChange(p, InquiryStatuses.Cancelled, "Inquiry CancelRow"), p => p is InquiryListRow row && row.CanCancelWorkflow);
            CopyInquiryNumberCommand = new RelayCommand(p =>
            {
                if (p is InquiryListRow row && !string.IsNullOrWhiteSpace(row.InquiryNumber))
                {
                    try
                    {
                        System.Windows.Clipboard.SetText(row.InquiryNumber);
                        SetStatus($"Copied '{row.InquiryNumber}' to clipboard.");
                    }
                    catch (Exception ex) { SetStatus("Copy failed: " + ExceptionMessages.Deep(ex)); }
                }
            }, p => p is InquiryListRow);
            CopySummaryCommand = new RelayCommand(p =>
            {
                if (p is not InquiryListRow row) return;
                string summary = ComposeSummary(row);
                try
                {
                    System.Windows.Clipboard.SetText(summary);
                    SetStatus("Copied one-line summary to clipboard.");
                }
                catch (Exception ex) { SetStatus("Copy failed: " + ExceptionMessages.Deep(ex)); }
            }, p => p is InquiryListRow);
            OpenAttachmentsFolderCommand = new RelayCommand(p =>
            {
                if (p is not InquiryListRow row) return;
                if (OpenAttachmentsFolderRequested != null) OpenAttachmentsFolderRequested(row);
                else SetStatus("Attachments folder handler is not wired.");
            }, p => p is InquiryListRow row && row.AttachmentCount > 0);

            // ── Per-stage navigation (rail context menu) ────────────────
            // Each command calls the corresponding func if wired (set by
            // MainViewModel to navigate the shell to that module and open
            // the specific document by row ID); otherwise it copies the
            // document number to the clipboard as a fallback so the
            // user can paste it into the module's own search.
            OpenProformaForRowCommand = new RelayCommand(
                p => InvokeStageOpen(p, OpenProformaForRowRequested,
                    r => r.Lifecycle.ProformaRowId, r => r.Lifecycle.ProformaNumber, "PI"),
                p => p is InquiryListRow r && r.Lifecycle.HasProforma);
            OpenJobOrderForRowCommand = new RelayCommand(
                p => InvokeStageOpen(p, OpenJobOrderForRowRequested,
                    r => r.Lifecycle.JobOrderRowId, r => r.Lifecycle.JobOrderNumber, "JO"),
                p => p is InquiryListRow r && r.Lifecycle.HasJobOrder);
            OpenDeliveryNoteForRowCommand = new RelayCommand(
                p => InvokeStageOpen(p, OpenDeliveryNoteForRowRequested,
                    r => r.Lifecycle.DeliveryNoteRowId, r => r.Lifecycle.LatestDeliveryNoteNumber, "DN"),
                p => p is InquiryListRow r && r.Lifecycle.HasDeliveryNote);
            OpenTaxInvoiceForRowCommand = new RelayCommand(
                p => InvokeStageOpen(p, OpenTaxInvoiceForRowRequested,
                    r => r.Lifecycle.TaxInvoiceRowId, r => r.Lifecycle.LatestTaxInvoiceNumber, "TI"),
                p => p is InquiryListRow r && r.Lifecycle.HasTaxInvoice);
            OpenStatementForRowCommand = new RelayCommand(
                p => InvokeStageOpen(p, OpenStatementForRowRequested,
                    r => 0, r => r.Lifecycle.LatestStatementNumber, "STMT"),
                p => p is InquiryListRow r && r.Lifecycle.HasStatement);

            // ── Copy document number (always available when a doc exists) ──
            CopyProformaNumberCommand = new RelayCommand(
                p => CopyToClipboard(p, r => r.Lifecycle.ProformaNumber, "PI"),
                p => p is InquiryListRow r && r.Lifecycle.HasProforma);
            CopyJobOrderNumberCommand = new RelayCommand(
                p => CopyToClipboard(p, r => r.Lifecycle.JobOrderNumber, "JO"),
                p => p is InquiryListRow r && r.Lifecycle.HasJobOrder);
            CopyDeliveryNoteNumberCommand = new RelayCommand(
                p => CopyToClipboard(p, r => r.Lifecycle.LatestDeliveryNoteNumber, "DN"),
                p => p is InquiryListRow r && r.Lifecycle.HasDeliveryNote);
            CopyTaxInvoiceNumberCommand = new RelayCommand(
                p => CopyToClipboard(p, r => r.Lifecycle.LatestTaxInvoiceNumber, "TI"),
                p => p is InquiryListRow r && r.Lifecycle.HasTaxInvoice);

            // ── Right-click row actions: Edit / Duplicate / Delete ────
            // Each promotes the row into the selection first so the
            // rail, KPI strip and double-click handlers stay in sync.
            EditInquiryRowCommand = new RelayCommand(p =>
            {
                if (p is InquiryListRow row)
                {
                    SelectedInquiry = row;
                    AsyncUtility.SafeFireAndForget(() => EditRowAsync(row), "Inquiry EditRow");
                }
            }, p => p is InquiryListRow);
            DuplicateInquiryRowCommand = new RelayCommand(p =>
            {
                if (p is InquiryListRow row)
                    AsyncUtility.SafeFireAndForget(() => DuplicateInquiryAsync(row), "Inquiry DuplicateRow");
            }, p => p is InquiryListRow);
            DeleteInquiryRowCommand = new RelayCommand(p =>
            {
                if (p is InquiryListRow row)
                    AsyncUtility.SafeFireAndForget(() => DeleteInquiryAsync(row), "Inquiry DeleteRow");
            }, p => p is InquiryListRow);

            // ── Change-priority submenu ──────────────────────────────
            // Each command applies a single fixed priority value to
            // the parameter row. The XAML CommandParameter on each
            // MenuItem would carry the same string, so we just bake
            // the value into the command and skip CommandParameter.
            SetUrgentPriorityCommand = new RelayCommand(p =>
                AsyncUtility.SafeFireAndForget(() => ApplyPriorityAsync(p, "Urgent"), "Inquiry PriorityUrgent"),
                p => p is InquiryListRow);
            SetHighPriorityCommand = new RelayCommand(p =>
                AsyncUtility.SafeFireAndForget(() => ApplyPriorityAsync(p, "High"), "Inquiry PriorityHigh"),
                p => p is InquiryListRow);
            SetNormalPriorityCommand = new RelayCommand(p =>
                AsyncUtility.SafeFireAndForget(() => ApplyPriorityAsync(p, "Normal"), "Inquiry PriorityNormal"),
                p => p is InquiryListRow);
            SetLowPriorityCommand = new RelayCommand(p =>
                AsyncUtility.SafeFireAndForget(() => ApplyPriorityAsync(p, "Low"), "Inquiry PriorityLow"),
                p => p is InquiryListRow);

            // Cancel the "new PI from inquiry" round-trip. Cleared
            // automatically when the user picks an inquiry (via
            // CreateProformaRequested in MainViewModel) so this is
            // an explicit "I changed my mind" exit. The host wires
            // the actual navigation back to the Proforma module.
            CancelNewPiFromInquiryCommand = new RelayCommand(_ =>
            {
                IsAwaitingNewPiSelection = false;
            }, _ => IsAwaitingNewPiSelection);

            AsyncUtility.SafeFireAndForget(LoadFilterSourcesAsync, "Inquiry filter sources");
            Reload();
        }

        private async Task ApplyPriorityAsync(object? parameter, string priority)
        {
            if (parameter is not InquiryListRow row) return;
            try
            {
                bool ok = await _workflow.SetPriorityAsync(row.Id, priority);
                if (ok)
                {
                    SetStatus($"{row.InquiryNumber} priority → {priority}.");
                    Reload();
                }
            }
            catch (Services.Audit.PermissionDeniedException ex) { SetStatus(ex.UserMessage); }
            catch (Exception ex) { SetStatus("Change priority failed: " + ExceptionMessages.Deep(ex)); }
        }

        private async Task NewInquiryAsync()
        {
            IsAwaitingNewPiSelection = false;
            await EditAsync(null);
        }

        private async Task DeleteAttachmentAsync(InquiryAttachmentItem item, InquiryListRow row)
        {
            if (ConfirmDeleteAttachmentRequested?.Invoke(item.Attachment) != true) return;
            try
            {
                bool removed = await _attachments.DeleteAsync(item.Attachment);
                if (removed)
                {
                    row.Attachments.Remove(item);
                    row.AttachmentCount = Math.Max(0, row.AttachmentCount - 1);
                    SetStatus($"Deleted '{item.FileName}'.");
                    AsyncUtility.SafeFireAndForget(() => LoadSelectedDetailAsync(row), "Inquiry drawer refresh");
                }
            }
            catch (Exception ex) { SetStatus("Delete failed: " + ExceptionMessages.Deep(ex)); }
        }

        private void ExecuteStatusRowChange(object? parameter, string nextStatus, string op)
        {
            if (parameter is not InquiryListRow row) return;
            SelectedInquiry = row;
            AsyncUtility.SafeFireAndForget(() => ChangeSelectedStatusAsync(nextStatus), op);
        }

        private async Task DuplicateInquiryAsync(InquiryListRow row)
        {
            try
            {
                var clone = await _workflow.DuplicateAsync(row.Id);
                SetStatus($"Duplicated {row.InquiryNumber} → {clone.InquiryNumber}.");
                Reload();
            }
            catch (Services.Audit.PermissionDeniedException ex) { SetStatus(ex.UserMessage); }
            catch (Exception ex) { SetStatus("Duplicate failed: " + ExceptionMessages.Deep(ex)); }
        }

        private async Task DeleteInquiryAsync(InquiryListRow row)
        {
            bool proceed = ConfirmDeleteInquiryRequested?.Invoke(row) ?? false;
            if (!proceed) return;
            try
            {
                var result = await _workflow.DeleteAsync(row.Id);
                if (result.IsSuccess)
                {
                    SetStatus($"Deleted inquiry {row.InquiryNumber}.");
                    if (ReferenceEquals(SelectedInquiry, row)) SelectedInquiry = null;
                    Reload();
                }
                else if (result.Outcome == DeleteInquiryOutcome.NotFound)
                {
                    SetStatus("This inquiry no longer exists — the register will refresh.");
                    Reload();
                }
                else
                {
                    string list = result.BlockingDocuments.Count == 0
                        ? "downstream documents"
                        : string.Join(", ", result.BlockingDocuments);
                    SetStatus($"Cannot delete {row.InquiryNumber}: a {list} already exists. " +
                              "Delete is only allowed when no Proforma Invoice has been created. " +
                              "Use 'Change status → Cancel' to close the inquiry instead.");
                }
            }
            catch (Services.Audit.PermissionDeniedException ex) { SetStatus(ex.UserMessage); }
            catch (Exception ex) { SetStatus("Delete failed: " + ExceptionMessages.Deep(ex)); }
        }

        // ── Right-click helpers ──────────────────────────────────────────

        /// <summary>One-line plain-text summary used by "Copy summary".</summary>
        private static string ComposeSummary(InquiryListRow row) =>
            $"{row.InquiryNumber} · {row.CustomerName} · {row.Project} · AED {row.InquiryValue:N0} · {row.Status} · {row.ReceivedDateText}";

        private static void CopyToClipboard(object parameter,
            Func<InquiryListRow, string?> numberGetter, string kind)
        {
            if (parameter is not InquiryListRow row) return;
            string? number = numberGetter(row);
            if (string.IsNullOrWhiteSpace(number)) return;
            try
            {
                System.Windows.Clipboard.SetText(number);
            }
            catch { /* clipboard contention — ignore */ }
        }

        private void InvokeStageOpen(object parameter,
            Func<InquiryListRow, int, Task>? openFunc,
            Func<InquiryListRow, int> rowIdGetter,
            Func<InquiryListRow, string?> numberGetter,
            string kindLabel)
        {
            if (parameter is not InquiryListRow row) return;
            // Promote the target row into the rail selection so any UI
            // state tied to SelectedInquiry (KPI strip, etc.) updates too.
            SelectedInquiry = row;
            int rowId = rowIdGetter(row);
            string? number = numberGetter(row);
            if (openFunc != null)
            {
                // When a row ID is available the MainViewModel handler
                // loads the specific document. When it isn't (e.g. for
                // statements which aren't joined to the inquiry), the
                // handler still navigates to the module and the user
                // can find the document by the clipboard-copied number.
                AsyncUtility.SafeFireAndForget(() => openFunc(row, rowId),
                    $"Open {kindLabel} {number ?? rowId.ToString()} from inquiry rail");
            }
            else if (!string.IsNullOrWhiteSpace(number))
            {
                try { System.Windows.Clipboard.SetText(number); }
                catch { /* ignore */ }
            }
        }

        // ── Loading ────────────────────────────────────────────────────

        private async Task LoadFilterSourcesAsync()
        {
            try
            {
                if (_masterData != null)
                    foreach (var x in await _masterData.GetAllSalesmenAsync())
                        if (!Salesmen.Contains(x.Name)) Salesmen.Add(x.Name);
                foreach (var user in await Services.Audit.AuditService.GetUsersAsync())
                {
                    string name = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;
                    if (!PreparedByUsers.Contains(name)) PreparedByUsers.Add(name);
                }
            }
            catch (Exception ex) { SetStatus("Filter sources unavailable: " + ExceptionMessages.Deep(ex)); }
        }

        private void DebounceReload()
        {
            _debounceCts?.Cancel(); _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;
            AsyncUtility.SafeFireAndForget(async () =>
            {
                try
                {
                    await Task.Delay(300, token);
                    token.ThrowIfCancellationRequested();
                    ResetAndReload();
                }
                catch (OperationCanceledException) { /* superseded keystroke */ }
            }, "Inquiry search debounce");
        }

        private void ResetAndReload()
        {
            CurrentPage = 1;
            Reload();
        }

        private void Reload()
        {
            _loadCts?.Cancel(); _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            int generation = ++_reloadGeneration;
            AsyncUtility.SafeFireAndForget(() => LoadAsync(_loadCts.Token, generation), "Inquiry list load");
        }

        private async Task LoadAsync(CancellationToken ct, int generation)
        {
            IsLoading = true; ErrorText = ""; SetStatus("Loading inquiries…");
            try
            {
                var range = DateRange(SelectedDateRange, DateTime.Now);
                var query = new InquiryQuery
                {
                    Search = SearchText, Status = SelectedStatusKey.Length == 0 ? "All statuses" : SelectedStatusKey,
                    SalesmanName = SelectedSalesman, PreparedByName = SelectedPreparedBy,
                    From = range.From, ToExclusive = range.ToExclusive,
                    HasAttachmentsOnly = HasAttachmentsOnly, Priority = SelectedPriority,
                    Sort = SelectedSort, Page = CurrentPage, PageSize = 50
                };
                var page = await _repository.QueryAsync(query, ct);
                var stats = await _repository.GetStatisticsAsync(query, ct);
                ct.ThrowIfCancellationRequested();
                // A7 fix: keep the drawer open across reloads when the row is
                // still on (or returned to) the current page.
                int keepId = SelectedInquiry?.Id ?? 0;
                InquiryListRow? restored = null;
                Inquiries.Clear();
                var pipelineFilter = SelectedPipelineKind;
                int pipelineCount = 0;
                foreach (var x in page.Items)
                {
                    // Pipeline-stage filter: applied in-memory after the page
                    // loads, because the stage data comes from a separate
                    // service call (IInquiryLifecycleService) and is not a
                    // column on the inquiry row. A 50-row page makes this
                    // trivially fast.
                    if (pipelineFilter.HasValue
                        && x.Lifecycle.FurthestStageReached < (int)pipelineFilter.Value)
                    {
                        continue;
                    }
                    if (pipelineFilter.HasValue) pipelineCount++;
                    var row = InquiryListRow.FromProjection(x);
                    Inquiries.Add(row);
                    if (row.Id == keepId) restored = row;
                }
                OnPropertyChanged(nameof(HasInquiries));
                ApplyStatistics(stats, pipelineCount);
                TotalPages = page.TotalCount == 0 ? 0 : (int)Math.Ceiling(page.TotalCount / (double)page.PageSize);
                PageStatus = page.TotalCount == 0
                    ? "No inquiries match the current filters"
                    : $"Showing {(page.Page - 1) * page.PageSize + 1}–{Math.Min(page.TotalCount, page.Page * page.PageSize)} of {page.TotalCount}";
                SetStatus(PageStatus);
                // Clearing the collection already nulled the selection; restore
                // it (and the drawer) when the row is still on this page.
                if (restored != null && generation == _reloadGeneration)
                    SelectedInquiry = restored;
            }
            catch (OperationCanceledException)
            {
                // S7 fix: superseded load — quiet, not an error.
            }
            catch (Exception ex)
            {
                ErrorText = "Could not load inquiries: " + ExceptionMessages.Deep(ex);
                SetStatus(ErrorText);
            }
            finally { if (generation == _reloadGeneration) IsLoading = false; }
        }

        private void ApplyStatistics(InquiryStatistics stats, int pipelineTabCount = 0)
        {
            PipelineValue = stats.OpenPipelineValue;
            OpenCount = stats.Received + stats.Prepared;
            AwaitingCount = stats.Sent;
            ConfirmedCount = stats.Confirmed;
            foreach (var tab in StatusTabs)
            {
                tab.Count = tab.Key switch
                {
                    "" => stats.All,
                    InquiryStatuses.Received => stats.Received,
                    InquiryStatuses.Prepared => stats.Prepared,
                    InquiryStatuses.Sent => stats.Sent,
                    InquiryStatuses.Confirmed => stats.Confirmed,
                    InquiryStatuses.Hold => stats.Hold,
                    InquiryStatuses.Cancelled => stats.Cancelled,
                    _ => 0
                };
            }
            // Pipeline-tab counts: the "current page" count comes from the
            // filter pass (pipelineTabCount); the All tab is just the page count.
            // Per-stage totals across the whole filtered set would require a
            // second pass over the repository; the senior-dev decision is to
            // show the current-page count on each tab and let users click the
            // tab to see only that stage — same UX as the status tabs above.
            foreach (var tab in PipelineTabs)
            {
                tab.Count = tab.Kind switch
                {
                    InquiryLifecycleKind.Inquiry => Inquiries.Count,
                    _ => pipelineTabCount
                };
            }
        }

        private async Task LoadSelectedDetailAsync(InquiryListRow row)
        {
            try
            {
                var attachments = await _attachments.GetAsync(row.Id);
                var communications = await _repository.GetCommunicationsAsync(row.Id);
                var detail = await _repository.GetDetailAsync(row.Id);
                if (!ReferenceEquals(SelectedInquiry, row)) return;
                row.Attachments.Clear();
                foreach (var x in attachments)
                    row.Attachments.Add(InquiryAttachmentItem.FromModel(x));
                row.RecentActivity.Clear();
                foreach (var x in communications)
                    row.RecentActivity.Add(new InquiryActivityItem
                    {
                        Title = x.Title,
                        Description = x.Description,
                        Actor = x.ActorName,
                        OccurredAt = x.OccurredAt
                    });
                if (detail != null)
                {
                    row.Notes = detail.Notes;
                    row.NotesPreview = string.IsNullOrWhiteSpace(detail.Notes)
                        ? "" : detail.Notes.Trim().Replace("\r", "");
                }
                row.IsDetailLoaded = true;
                OnPropertyChanged(nameof(SelectedInquiry));
            }
            catch (OperationCanceledException) { /* Superseded load — quiet by design (SilentCatchAuditTests). */ }
            catch (Exception ex) { SetStatus("Detail unavailable: " + ExceptionMessages.Deep(ex)); }
        }

        // ── Actions ────────────────────────────────────────────────────

        private async Task EditRowAsync(InquiryListRow row)
        {
            var detail = await _repository.GetDetailAsync(row.Id);
            await EditAsync(detail);
        }

        private async Task EditAsync(InquiryRecord? record)
        {
            if (EditRequested == null) { SetStatus("Inquiry editor is unavailable."); return; }
            if (await EditRequested(record)) ResetAndReload();
        }

        private async Task ConvertToPiAsync()
        {
            if (SelectedInquiry == null || CreateProformaRequested == null) return;
            var detail = await _repository.GetDetailAsync(SelectedInquiry.Id);
            if (detail == null) return;
            if (detail.ProformaInvoiceId.HasValue || !string.IsNullOrWhiteSpace(detail.ProformaNumber))
            {
                SetStatus($"PI {detail.ProformaNumber} already exists for this inquiry.");
                return;
            }
            await CreateProformaRequested(detail);
        }

        private async Task ChangeSelectedStatusAsync(string? next)
        {
            var row = SelectedInquiry;
            if (row == null || string.IsNullOrWhiteSpace(next)) return;
            try
            {
                if (!InquiryWorkflowService.CanTransition(row.Status, next))
                {
                    SetStatus($"An inquiry cannot move from {row.Status} to {next}.");
                    return;
                }
                bool changed = await _workflow.ChangeStatusAsync(row.Id, row.Status, next);
                if (changed)
                {
                    SetStatus($"Inquiry {row.InquiryNumber} is now {InquiryStatuses.Normalize(next)}.");
                    Reload();
                }
            }
            catch (Exception ex) { SetStatus(ExceptionMessages.Deep(ex)); }
        }

        private async Task ExportAsync()
        {
            if (!Services.Audit.PermissionService.Require(Services.Audit.AppPermissions.ProformaExport, "export inquiries")) return;
            string? path = ExportPathRequested?.Invoke();
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                // Export every row matching the current filter (paged fetch, bounded),
                // not just the visible page.
                var range = DateRange(SelectedDateRange, DateTime.Now);
                InquiryQuery Build(int page) => new()
                {
                    Search = SearchText, Status = SelectedStatusKey.Length == 0 ? "All statuses" : SelectedStatusKey,
                    SalesmanName = SelectedSalesman, PreparedByName = SelectedPreparedBy,
                    From = range.From, ToExclusive = range.ToExclusive,
                    HasAttachmentsOnly = HasAttachmentsOnly, Priority = SelectedPriority,
                    Sort = SelectedSort, Page = page, PageSize = 200
                };
                var rows = new System.Collections.Generic.List<InquiryListProjection>();
                var result = await _repository.QueryAsync(Build(1));
                rows.AddRange(result.Items);
                while (result.HasNext && rows.Count < 5000)
                {
                    result = await _repository.QueryAsync(Build(result.Page + 1));
                    rows.AddRange(result.Items);
                }
                // Apply the same in-memory pipeline filter that LoadAsync
                // applies. Without this, a user who filtered to "PI only"
                // would export ALL inquiries regardless of the visible
                // filter — which is almost never what they want.
                var pipelineFilter = SelectedPipelineKind;
                if (pipelineFilter.HasValue)
                {
                    int minStage = (int)pipelineFilter.Value;
                    rows = rows.Where(x => x.Lifecycle.FurthestStageReached >= minStage).ToList();
                }
                await _io.ExportCsvAsync(path, rows);
                SetStatus($"Exported {rows.Count} inquiries to {System.IO.Path.GetFileName(path)}.");
            }
            catch (Exception ex) { SetStatus("Export failed: " + ExceptionMessages.Deep(ex)); }
        }

        private async Task ImportAsync()
        {
            string? path = ImportPathRequested?.Invoke();
            if (string.IsNullOrWhiteSpace(path)) return;
            // Cancel any in-flight import so a second click doesn't
            // double-parse the same file. Old token is disposed.
            _importCts?.Cancel();
            _importCts?.Dispose();
            _importCts = new CancellationTokenSource();
            var ct = _importCts.Token;
            try
            {
                // Pass the token so ParseCsvAsync can short-circuit a
                // huge / malicious file. (The service still falls back
                // to File.ReadAllLinesAsync internally; for a full
                // streaming fix see the audit notes. The token at least
                // lets a second-click user cancel an in-flight parse.)
                var parse = await _io.ParseCsvAsync(path, ct);
                if (parse.Rows.Count == 0)
                {
                    SetStatus(parse.SkippedInvalidRows > 0
                        ? $"Import contains no valid rows ({parse.SkippedInvalidRows} rows lacked customer or project)."
                        : "Import contains no rows.");
                    return;
                }
                if (ConfirmImportRequested?.Invoke(parse.Rows.Count) != true) return;
                int created = 0, failed = 0;
                foreach (var row in parse.Rows)
                {
                    if (ct.IsCancellationRequested) return;
                    try { await _workflow.CreateAsync(row); created++; }
                    catch (Services.Audit.PermissionDeniedException) { throw; }
                    catch (Exception) { failed++; }
                }
                string suffix = parse.SkippedInvalidRows > 0 ? $", {parse.SkippedInvalidRows} invalid skipped" : "";
                string dupes = parse.DuplicateNumbersRegenerated > 0
                    ? $", {parse.DuplicateNumbersRegenerated} duplicate numbers regenerated" : "";
                SetStatus($"Imported {created} inquiries{suffix}{dupes}{(failed > 0 ? $", {failed} failed" : "")}.");
                ResetAndReload();
            }
            catch (Services.Audit.PermissionDeniedException ex) { SetStatus(ex.Message); }
            catch (OperationCanceledException) { /* superseded import — user clicked again */ }
            catch (Exception ex) { SetStatus("Import failed: " + ExceptionMessages.Deep(ex)); }
        }

        private static (DateTime? From, DateTime? ToExclusive) DateRange(string value, DateTime now)
        {
            var today = now.Date;
            return value switch
            {
                "Today" => (today, today.AddDays(1)),
                "Yesterday" => (today.AddDays(-1), today),
                "Last 7 Days" => (today.AddDays(-6), today.AddDays(1)),
                "Last 30 Days" => (today.AddDays(-29), today.AddDays(1)),
                _ => (null, null) // "All time" — the honest default
            };
        }

        // ── Pagination & state ─────────────────────────────────────────

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                _currentPage = value; OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoPrevious)); OnPropertyChanged(nameof(CanGoNext));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
        private int _totalPages;
        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                _totalPages = value; OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoNext));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
        public bool CanGoPrevious => CurrentPage > 1;
        public bool CanGoNext => CurrentPage < TotalPages;

        private bool _isLoading = true;
        public bool IsLoading { get => _isLoading; private set { _isLoading = value; OnPropertyChanged(); } }
        private string _errorText = "";
        public string ErrorText { get => _errorText; private set { _errorText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); } }
        public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);
        private string _statusText = "Loading…";
        public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChanged(); } }
        private void SetStatus(string value) => StatusText = value;

        private void ResetFilters()
        {
            _searchText = ""; _selectedDateRange = "All time";
            _selectedSalesman = "All salesmen"; _selectedPreparedBy = "All prepared by";
            _selectedPriority = "All priorities"; _selectedSort = "Updated newest";
            _hasAttachmentsOnly = false;
            foreach (var tab in StatusTabs) tab.IsSelected = tab.Key.Length == 0;
            SelectedPipelineKind = null;
            OnPropertyChanged(nameof(SearchText)); OnPropertyChanged(nameof(SelectedDateRange));
            OnPropertyChanged(nameof(SelectedSalesman)); OnPropertyChanged(nameof(SelectedPreparedBy));
            OnPropertyChanged(nameof(SelectedPriority)); OnPropertyChanged(nameof(SelectedSort));
            OnPropertyChanged(nameof(HasAttachmentsOnly)); OnPropertyChanged(nameof(HasActiveFilters));
            ResetAndReload();
        }
        public void Dispose()
        {
            try { _loadCts?.Cancel(); _loadCts?.Dispose(); _loadCts = null; } catch { }
            try { _debounceCts?.Cancel(); _debounceCts?.Dispose(); _debounceCts = null; } catch { }
            try { _importCts?.Cancel(); _importCts?.Dispose(); _importCts = null; } catch { }
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // Row models
    // ────────────────────────────────────────────────────────────────────

    /// <summary>Segmented status tab with a live count.</summary>
    public sealed class InquiryStatusTab : ViewModelBase
    {
        public string Key { get; init; } = "";
        public string Label { get; init; } = "";
        private int _count;
        public int Count { get => _count; set { _count = value; OnPropertyChanged(); } }
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    }

    /// <summary>One register row. Display text is precomputed for cheap binding.</summary>
    public sealed class InquiryListRow
    {
        public int Id { get; private set; }
        public string InquiryNumber { get; private set; } = "";
        public string InquiryReference { get; private set; } = "";
        public string CustomerName { get; private set; } = "";
        public string CustomerLocation { get; private set; } = "";
        public string Project { get; private set; } = "";
        public string Subject { get; private set; } = "";
        public string Salesman { get; private set; } = "";
        public string PreparedBy { get; private set; } = "";
        public string PreparedByRole { get; private set; } = "";
        public string Status { get; internal set; } = InquiryStatuses.Received;
        public string Priority { get; private set; } = "Normal";
        public decimal InquiryValue { get; private set; }
        public string ValueText => InquiryValue == 0 ? "—" : InquiryValue.ToString("N0", CultureInfo.CurrentCulture);
        public DateTime ReceivedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public string ReceivedDateText => ReceivedAt.ToString("dd MMM yyyy", CultureInfo.CurrentCulture);
        public string ReceivedTimeText => ReceivedAt.ToString("hh:mm tt", CultureInfo.CurrentCulture);
        public string UpdatedText => UpdatedAt.ToString("dd MMM yyyy · hh:mm tt", CultureInfo.CurrentCulture);
        public string UpdatedBy { get; private set; } = "";
        public string UpdatedByDisplay => string.IsNullOrWhiteSpace(UpdatedBy) ? "" : $"by {UpdatedBy}";
        public int AttachmentCount { get; internal set; }
        public string AttachmentBadgeText { get; private set; } = "";
        public bool HasAttachments => AttachmentCount > 0;
        public string AttachmentChipText => AttachmentCount == 0 ? "None" : $"{AttachmentCount} file{(AttachmentCount == 1 ? "" : "s")}";
        public string ProformaNumber { get; private set; } = "";
        public string JobOrderNumber { get; private set; } = "";
        public bool IsConverted => !string.IsNullOrWhiteSpace(ProformaNumber);
        /// <summary>
        /// A PI can be created from a Prepared or Confirmed inquiry.
        /// The inquiry status is then automatically advanced based on
        /// the PI lifecycle: PI created → Sent, PI confirmed → Confirmed,
        /// PI rejected/cancelled → Cancelled. This removes the need for
        /// the operator to manually mark the inquiry as Confirmed before
        /// creating a PI — the PI itself IS the confirmation evidence.
        /// </summary>
        public bool CanConvert =>
            (Status == InquiryStatuses.Prepared || Status == InquiryStatuses.Confirmed)
            && !IsConverted;
        // Workflow affordances for the drawer (guarded by the same state
        // machine the workflow service enforces server-side).
        public bool CanPrepareWorkflow => Services.Inquiry.InquiryWorkflowService.CanTransition(Status, InquiryStatuses.Prepared);
        public bool CanSendWorkflow => Services.Inquiry.InquiryWorkflowService.CanTransition(Status, InquiryStatuses.Sent);
        public bool CanConfirmWorkflow => Services.Inquiry.InquiryWorkflowService.CanTransition(Status, InquiryStatuses.Confirmed);
        public bool CanHoldWorkflow => Services.Inquiry.InquiryWorkflowService.CanTransition(Status, InquiryStatuses.Hold);
        public bool CanCancelWorkflow => Services.Inquiry.InquiryWorkflowService.CanTransition(Status, InquiryStatuses.Cancelled);
        public string ChainText => IsConverted
            ? "PI " + ProformaNumber + (string.IsNullOrWhiteSpace(JobOrderNumber) ? " · JO not released" : " · JO " + JobOrderNumber)
            : "Not converted to PI yet";
        public string Notes { get; internal set; } = "";
        public string NotesPreview { get; internal set; } = "";
        public bool IsDetailLoaded { get; internal set; }
        public string ContactSummary => string.IsNullOrWhiteSpace(CustomerLocation) ? "" : CustomerLocation;
        /// <summary>Avatar initials for the row card (first letters of the first two words).</summary>
        public string CustomerInitials
        {
            get
            {
                var parts = CustomerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return "·";
                return parts.Length == 1
                    ? parts[0][..1].ToUpperInvariant()
                    : (parts[0][..1] + parts[1][..1]).ToUpperInvariant();
            }
        }
        public ObservableCollection<InquiryAttachmentItem> Attachments { get; } = new();
        public ObservableCollection<InquiryActivityItem> RecentActivity { get; } = new();

        // ── Full-lifecycle display fields ────────────────────────────
        // Bound from the InquiryLifecycleSnapshot hydrated by the repository
        // via IInquiryLifecycleService. Every property here is a precomputed
        // string the XAML can bind to directly without converters.

        public InquiryLifecycleSnapshot Lifecycle { get; private set; } = new();

        // Per-stage cell text for the row card. Empty when the stage has not started.
        public string ProformaCellText => Lifecycle.HasProforma
            ? $"{Lifecycle.ProformaNumber} · {Lifecycle.ProformaStatus}"
            : "—";
        public string JobOrderCellText => Lifecycle.HasJobOrder
            ? $"{Lifecycle.JobOrderNumber} · {Lifecycle.JobOrderStatus}"
            : "—";
        public string DeliveryNoteCellText => Lifecycle.HasDeliveryNote
            ? (Lifecycle.DeliveryNoteCount > 1
                ? $"{Lifecycle.LatestDeliveryNoteNumber} · {Lifecycle.LatestDeliveryNoteStatus} (+{Lifecycle.DeliveryNoteCount - 1})"
                : $"{Lifecycle.LatestDeliveryNoteNumber} · {Lifecycle.LatestDeliveryNoteStatus}")
            : "—";
        public string TaxInvoiceCellText => Lifecycle.HasTaxInvoice
            ? (Lifecycle.TaxInvoiceCount > 1
                ? $"{Lifecycle.LatestTaxInvoiceNumber} · {Lifecycle.LatestTaxInvoiceStatus} (+{Lifecycle.TaxInvoiceCount - 1})"
                : $"{Lifecycle.LatestTaxInvoiceNumber} · {Lifecycle.LatestTaxInvoiceStatus}")
            : "—";
        public string StatementCellText => Lifecycle.HasStatement
            ? Lifecycle.LatestStatementNumber ?? "—"
            : "—";

        // Per-stage coarse state — bound to background tints in the row card.
        public InquiryLifecycleState ProformaState =>
            Lifecycle.StateFor(InquiryLifecycleKind.ProformaInvoice);
        public InquiryLifecycleState JobOrderState =>
            Lifecycle.StateFor(InquiryLifecycleKind.JobOrder);
        public InquiryLifecycleState DeliveryNoteState =>
            Lifecycle.StateFor(InquiryLifecycleKind.DeliveryNote);
        public InquiryLifecycleState TaxInvoiceState =>
            Lifecycle.StateFor(InquiryLifecycleKind.TaxInvoice);
        public InquiryLifecycleState StatementState =>
            Lifecycle.StateFor(InquiryLifecycleKind.Statement);

        // Aggregated money fields.
        public string TotalBilledText => Lifecycle.TotalBilled == 0
            ? "—"
            : $"AED {Lifecycle.TotalBilled.ToString("N0", CultureInfo.CurrentCulture)}";
        public string CurrentBillOutstandingText => Lifecycle.CurrentBillOutstanding == 0
            ? "—"
            : $"AED {Lifecycle.CurrentBillOutstanding.ToString("N0", CultureInfo.CurrentCulture)}";
        public string TotalOutstandingText => Lifecycle.TotalOutstanding == 0
            ? "—"
            : $"AED {Lifecycle.TotalOutstanding.ToString("N0", CultureInfo.CurrentCulture)}";

        // Pipeline progress — "2/6" of stages reached, with a label.
        public string PipelineProgressText =>
            $"{Lifecycle.FurthestStageReached + 1}/{(int)InquiryLifecycleKind.Statement + 1}";
        public int PipelineProgressPercent =>
            (int)Math.Round(100.0 * (Lifecycle.FurthestStageReached + 1) /
                ((int)InquiryLifecycleKind.Statement + 1));

        public static InquiryListRow FromProjection(InquiryListProjection x) => new()
        {
            Id = x.Id, InquiryNumber = x.InquiryNumber, InquiryReference = x.InquiryReference,
            CustomerName = x.CustomerName, CustomerLocation = x.CustomerLocation,
            Project = x.ProjectName, Subject = x.ProjectDescription,
            Salesman = x.SalesmanName, PreparedBy = x.PreparedByName, PreparedByRole = x.PreparedByRole,
            Status = InquiryStatuses.Normalize(x.Status), Priority = x.Priority,
            InquiryValue = x.InquiryValue, ReceivedAt = x.ReceivedAt, UpdatedAt = x.UpdatedAt,
            UpdatedBy = x.UpdatedBy, AttachmentCount = x.AttachmentCount,
            AttachmentBadgeText = x.AttachmentBadgeText, ProformaNumber = x.ProformaNumber,
            JobOrderNumber = x.JobOrderNumber, Lifecycle = x.Lifecycle
        };
    }

    /// <summary>Attachment chip in the details drawer.</summary>
    public sealed class InquiryAttachmentItem
    {
        public long Id { get; private set; }
        public string FileName { get; private set; } = "";
        public string Extension { get; private set; } = "";
        public string TypeLabel { get; private set; } = "";
        public long FileSize { get; private set; }
        public string FileSizeDisplay => FileSize < 1024 * 1024
            ? $"{FileSize / 1024d:N0} KB" : $"{FileSize / (1024d * 1024d):N1} MB";
        public string UploadedBy { get; private set; } = "";
        public DateTime UploadedAt { get; private set; }
        public string UploadedText => UploadedAt.ToString("dd MMM yyyy", CultureInfo.CurrentCulture);
        public InquiryAttachment Attachment { get; private set; } = new();

        public static InquiryAttachmentItem FromModel(InquiryAttachment x) => new()
        {
            Id = x.Id, FileName = x.FileName, Extension = x.Extension,
            TypeLabel = (x.Extension ?? "").TrimStart('.').ToUpperInvariant(),
            FileSize = x.FileSize, UploadedBy = x.UploadedBy, UploadedAt = x.UploadedAt,
            Attachment = x
        };
    }

    /// <summary>Timeline entry for the drawer activity feed.</summary>
    public sealed class InquiryActivityItem
    {
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
        public string Actor { get; init; } = "";
        public DateTime OccurredAt { get; init; }
        public string OccurredText => OccurredAt.ToString("dd MMM yyyy · hh:mm tt", CultureInfo.CurrentCulture);
    }
}
