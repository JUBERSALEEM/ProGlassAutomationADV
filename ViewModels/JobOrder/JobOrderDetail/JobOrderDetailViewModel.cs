using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.JobOrder.Common.JobPriority;
using ProGlassAutomation.Models.JobOrder.ProductionDetails;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.JobOrder.Export;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.Sharing;
using System;
using ProGlassAutomation.Utilities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Workflow;

using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.JobOrder.Repository;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.JobOrder.JobOrderDetail
{
    /// <summary>
    /// ViewModel for the Job Order detail view.
    /// Left column shows the embedded (read-only) Proforma Invoice snapshot.
    /// Right column shows the editable ProductionDetails.
    ///
    /// Phase 10.5: Added CreateDeliveryNoteCommand — creates a new Draft DN
    /// from this JO and fires OnRequestOpenDeliveryNote callback so the
    /// shell VM can navigate to the DN module and open the new DN.
    /// </summary>
    public class JobOrderDetailViewModel : ViewModelBase
    {
        private readonly IDeliveryNoteRepository _deliveryNotes = null!;
        private readonly IDialogService _dialogs = null!;
        private readonly IJobOrderRepository _jobs = null!;
        private readonly IStatusService _status = null!;

        public JobOrderDetailViewModel(IDeliveryNoteRepository? deliveryNotes, IDialogService? dialogs, IJobOrderRepository? jobs, IStatusService? status)
        {
            _deliveryNotes = deliveryNotes!;
            _dialogs = dialogs!;
            _jobs = jobs!;
            _status = status!;
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

        // ── reentrancy guard ──────────────────────────────────────────────
        private int _loadingFlag = 0;

        // ── callbacks wired by shell VM ───────────────────────────────────
        public Action? OnRequestBackToList { get; set; }

        /// <summary>
        /// Phase 10.5: Fired when user creates a new DN from this JO.
        /// Parameter = the new DN's database Id. Shell VM should navigate
        /// to Delivery Note module and load this DN in detail view.
        /// </summary>
        public Action<int>? OnRequestOpenDeliveryNote { get; set; }

        // ── loaded snapshot + entity reference ────────────────────────────
        private SavedJobOrder? _entity;
        private JobOrderSnapshot? _snapshot;

        // ── job order header (read-only display) ──────────────────────────

        private string _jobOrderId = string.Empty;
        public string JobOrderId
        {
            get => _jobOrderId;
            private set { if (_jobOrderId == value) return; _jobOrderId = value; OnPropertyChanged(); }
        }

        private string _sourceProformaId = string.Empty;
        public string SourceProformaId
        {
            get => _sourceProformaId;
            private set { if (_sourceProformaId == value) return; _sourceProformaId = value; OnPropertyChanged(); }
        }

        private string _convertedDisplay = string.Empty;
        public string ConvertedDisplay
        {
            get => _convertedDisplay;
            private set { if (_convertedDisplay == value) return; _convertedDisplay = value; OnPropertyChanged(); }
        }

        private string _grandTotalDisplay = "AED 0";
        public string GrandTotalDisplay
        {
            get => _grandTotalDisplay;
            private set { if (_grandTotalDisplay == value) return; _grandTotalDisplay = value; OnPropertyChanged(); }
        }

        // ── read-only PI snapshot exposed for XAML binding ────────────────

        private ProformaInvoiceSnapshot _invoiceSnapshot = new();
        public ProformaInvoiceSnapshot InvoiceSnapshot
        {
            get => _invoiceSnapshot;
            private set { if (_invoiceSnapshot == value) return; _invoiceSnapshot = value; OnPropertyChanged(); }
        }

        // ── editable production fields ────────────────────────────────────

        public List<string> StatusOptions { get; } = new()
        {
            "Draft", "Released", "InProduction", "Completed", "Delivered"
        };

        private string _selectedStatus = "Draft";
        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (_selectedStatus == value) return;
                _selectedStatus = value;
                OnPropertyChanged();
                UpdateStatusMeta();
                // ERP hardening (Phase 2): editability follows the status.
                OnPropertyChanged(nameof(IsBodyEditable));
                OnPropertyChanged(nameof(IsBodyLocked));
                OnPropertyChanged(nameof(BodyLockBannerVisibility));
            }
        }

        /// <summary>
        /// ERP lifecycle hardening (Phase 2): true while the job order's
        /// production body (site/delivery details, instructions, drawing ref,
        /// notes, priority, delivery date) may still be edited. Once the job
        /// is Delivered or Rejected the body freezes; the status ComboBox
        /// itself stays enabled so a legitimate transition (e.g. Delivered →
        /// Rejected) is still possible.
        /// </summary>
        public bool IsBodyEditable =>
            ProGlassAutomation.Services.Workflow.DocumentLifecyclePolicy.CanEditBody(
                ProGlassAutomation.Services.Workflow.StatusTransitions.Kind.JobOrder,
                _selectedStatus);

        /// <summary>Inverse of <see cref="IsBodyEditable"/>, for XAML.</summary>
        public bool IsBodyLocked => !IsBodyEditable;

        /// <summary>Shows the "body locked" banner only when locked.</summary>
        public System.Windows.Visibility BodyLockBannerVisibility =>
            IsBodyLocked
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

        public List<string> PriorityOptions { get; } = new()
        {
            "High", "Medium", "Low"
        };

        private string _selectedPriority = "Medium";
        public string SelectedPriority
        {
            get => _selectedPriority;
            set
            {
                if (_selectedPriority == value) return;
                _selectedPriority = value;
                OnPropertyChanged();
                UpdatePriorityMeta();
            }
        }

        // Status meta (for pills)
        private string _statusLabel = "Draft";
        public string StatusLabel
        {
            get => _statusLabel;
            private set { if (_statusLabel == value) return; _statusLabel = value; OnPropertyChanged(); }
        }

        private string _statusBrushKey = "NeutralBrush";
        public string StatusBrushKey
        {
            get => _statusBrushKey;
            private set { if (_statusBrushKey == value) return; _statusBrushKey = value; OnPropertyChanged(); }
        }

        // Priority meta (for pills)
        private string _priorityLabel = "Medium";
        public string PriorityLabel
        {
            get => _priorityLabel;
            private set { if (_priorityLabel == value) return; _priorityLabel = value; OnPropertyChanged(); }
        }

        private string _priorityBrushKey = "SuccessBrush";
        public string PriorityBrushKey
        {
            get => _priorityBrushKey;
            private set { if (_priorityBrushKey == value) return; _priorityBrushKey = value; OnPropertyChanged(); }
        }

        // Delivery date + site info
        private DateTime? _deliveryDate;
        public DateTime? DeliveryDate
        {
            get => _deliveryDate;
            set { if (_deliveryDate == value) return; _deliveryDate = value; OnPropertyChanged(); }
        }

        private string _siteDeliveryContact = string.Empty;
        public string SiteDeliveryContact
        {
            get => _siteDeliveryContact;
            set { if (_siteDeliveryContact == value) return; _siteDeliveryContact = value; OnPropertyChanged(); }
        }

        private string _siteDeliveryPhone = string.Empty;
        public string SiteDeliveryPhone
        {
            get => _siteDeliveryPhone;
            set { if (_siteDeliveryPhone == value) return; _siteDeliveryPhone = value; OnPropertyChanged(); }
        }

        private string _siteDeliveryAddress = string.Empty;
        public string SiteDeliveryAddress
        {
            get => _siteDeliveryAddress;
            set { if (_siteDeliveryAddress == value) return; _siteDeliveryAddress = value; OnPropertyChanged(); }
        }

        // Assignment + factory floor
        private string _assignedTo = string.Empty;
        public string AssignedTo
        {
            get => _assignedTo;
            set { if (_assignedTo == value) return; _assignedTo = value; OnPropertyChanged(); }
        }

        private string _cuttingInstructions = string.Empty;
        public string CuttingInstructions
        {
            get => _cuttingInstructions;
            set { if (_cuttingInstructions == value) return; _cuttingInstructions = value; OnPropertyChanged(); }
        }

        private string _drawingReference = string.Empty;
        public string DrawingReference
        {
            get => _drawingReference;
            set { if (_drawingReference == value) return; _drawingReference = value; OnPropertyChanged(); }
        }

        private string _internalNotes = string.Empty;
        public string InternalNotes
        {
            get => _internalNotes;
            set { if (_internalNotes == value) return; _internalNotes = value; OnPropertyChanged(); }
        }

        // Audit
        private string _lastEditedDisplay = string.Empty;
        public string LastEditedDisplay
        {
            get => _lastEditedDisplay;
            private set { if (_lastEditedDisplay == value) return; _lastEditedDisplay = value; OnPropertyChanged(); }
        }

        // ── loading + error state ─────────────────────────────────────────

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); }
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            private set { if (_hasError == value) return; _hasError = value; OnPropertyChanged(); }
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            private set { if (_errorMessage == value) return; _errorMessage = value; OnPropertyChanged(); }
        }

        private bool _hasUnsavedChanges;
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            private set { if (_hasUnsavedChanges == value) return; _hasUnsavedChanges = value; OnPropertyChanged(); }
        }

        // ── commands ──────────────────────────────────────────────────────

        private string _detailTab = "Overview";
        public string DetailTab
        {
            get => _detailTab;
            set
            {
                if (_detailTab == value) return;
                _detailTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOverviewTab));
                OnPropertyChanged(nameof(IsProductionTab));
            }
        }
        public bool IsOverviewTab => DetailTab == "Overview";
        public bool IsProductionTab => DetailTab == "Production";
        public ICommand SwitchDetailTabCommand { get; private set; }

        public ICommand SaveCommand { get; private set; }
        /// <summary>
        /// Deep-sweep fix: exports must NEVER crash the app. Any failure
        /// surfaces the standard error dialog instead of an unhandled
        /// exception on the UI thread.
        /// </summary>
        private async Task GuardedExportAsync(Func<Task> export, string kind)
        {
            try { await export(); }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.ExportFailedX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapXExportFailed"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        public ICommand BackToListCommand { get; private set; }

        // Phase 9.5a-r3 — export
        public ICommand ShareCommand { get; private set; } = null!;
        public ICommand ExportPdfCommand { get; private set; }
        public ICommand ExportExcelCommand { get; private set; }
        public ICommand ExportCsvCommand { get; private set; }

        // Phase 10.5 — create DN from this JO
        public ICommand CreateDeliveryNoteCommand { get; private set; }

        /// <summary>Opens the Master DN created from this Job Order.</summary>
        public ICommand OpenMasterDnCommand { get; private set; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public JobOrderDetailViewModel()
         : this(
            App.ServiceProvider?.GetService<IDeliveryNoteRepository>()!,
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<IJobOrderRepository>()!,
            App.ServiceProvider?.GetService<IStatusService>()!){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            SwitchDetailTabCommand = new RelayCommand(p2 =>
                DetailTab = p2 as string ?? "Overview");

            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            BackToListCommand = new RelayCommand(_ => OnRequestBackToList?.Invoke());

            ExportPdfCommand = new RelayCommand(async _ => await GuardedExportAsync(ExportPdfAsync, "PDF"));
            ExportExcelCommand = new RelayCommand(async _ => await GuardedExportAsync(ExportExcelAsync, "Excel"));
            ExportCsvCommand = new RelayCommand(async _ => await GuardedExportAsync(ExportCsvAsync, "CSV"));
            ShareCommand = new RelayCommand(async _ => await GuardedExportAsync(ShareAsync, "Share"));

            CreateDeliveryNoteCommand = new RelayCommand(async _ => await CreateDeliveryNoteAsync());
            OpenMasterDnCommand = new RelayCommand(async _ => await OpenMasterDnAsync());
        }

        // ═════════════════════════════════════════════════════════════════
        //  LOAD
        // ═════════════════════════════════════════════════════════════════

        public async Task LoadAsync(int jobOrderId)
        {
            if (Interlocked.CompareExchange(ref _loadingFlag, 1, 0) != 0)
                return;

            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = string.Empty;

                if (_jobs == null)
                {
                    HasError = true;
                    ErrorMessage = "Job Order database not initialised.";
                    return;
                }

                _entity = await _jobs.GetByIdAsync(jobOrderId);
                if (_entity == null)
                {
                    HasError = true;
                    ErrorMessage = $"Job order id {jobOrderId} not found.";
                    return;
                }

                _snapshot = JobOrderMapper.DeserializeSnapshot(_entity);
                if (_snapshot == null)
                {
                    HasError = true;
                    ErrorMessage = $"Failed to load job order snapshot (corrupt data).";
                    return;
                }

                PopulateDisplayFields();
                HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Load failed: {ExceptionMessages.Deep(ex)}";
            }
            finally
            {
                IsLoading = false;
                Interlocked.Exchange(ref _loadingFlag, 0);
            }
        }

        /// <summary>
        /// v2 §24: loads an old version's snapshot into the editor.
        /// The database row is untouched until the user saves — saving
        /// then appends a NEW version (history is never erased).
        /// Returns false when the version cannot be applied.
        /// </summary>
        public async Task<bool> RestoreVersionAsync(int versionId)
        {
            try
            {
                var version = await ProGlassAutomation.Services.Audit
                    .VersionHistoryService.GetByIdAsync(versionId);

                if (version == null ||
                    string.IsNullOrEmpty(version.SnapshotJson))
                    return false;

                if (_entity == null)
                {
                    if (_jobs == null) return false;
                    _entity = await _jobs
                        .GetByJobOrderIdAsync(version.EntityId);
                    if (_entity == null) return false;
                }

                // Swap in the old snapshot and re-hydrate. On any
                // failure the current snapshot is rolled back.
                string savedJson = _entity.FullSnapshotJson;
                _entity.FullSnapshotJson = version.SnapshotJson;

                var snap = JobOrderMapper.DeserializeSnapshot(_entity);
                if (snap == null)
                {
                    _entity.FullSnapshotJson = savedJson;
                    return false;
                }

                _snapshot = snap;
                PopulateDisplayFields();
                HasUnsavedChanges = true;   // save to persist the restore

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.JobOrder,
                    "Version restored",
                    $"{_entity.JobOrderId} → version {version.VersionNo} " +
                    "(pending save)",
                    "JO", _entity.JobOrderId);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void PopulateDisplayFields()
        {
            if (_entity == null || _snapshot == null) return;

            // Header
            JobOrderId = _snapshot.JobOrderId;
            SourceProformaId = _snapshot.SourceProformaId;
            ConvertedDisplay = $"Converted {_snapshot.ConvertedAt:dd MMM yyyy}";
            GrandTotalDisplay = $"AED {_entity.GrandTotalVatInclusive:N0}";

            // Read-only invoice snapshot
            InvoiceSnapshot = _snapshot.EmbeddedInvoice ?? new ProformaInvoiceSnapshot();

            // Editable production fields
            var prod = _snapshot.Production ?? new ProductionDetails();

            _selectedStatus = string.IsNullOrWhiteSpace(prod.Status) ? "Draft" : prod.Status;
            _selectedPriority = string.IsNullOrWhiteSpace(prod.Priority) ? "Medium" : prod.Priority;
            _deliveryDate = prod.DeliveryDate;
            _siteDeliveryContact = prod.SiteDeliveryContact ?? string.Empty;
            _siteDeliveryPhone = prod.SiteDeliveryPhone ?? string.Empty;
            _siteDeliveryAddress = prod.SiteDeliveryAddress ?? string.Empty;
            _assignedTo = prod.AssignedTo ?? string.Empty;
            _cuttingInstructions = prod.CuttingInstructions ?? string.Empty;
            _drawingReference = prod.DrawingReference ?? string.Empty;
            _internalNotes = prod.InternalNotes ?? string.Empty;

            OnPropertyChanged(nameof(SelectedStatus));
            // ERP hardening (Phase 2): refresh the body-edit lock after load.
            OnPropertyChanged(nameof(IsBodyEditable));
            OnPropertyChanged(nameof(IsBodyLocked));
            OnPropertyChanged(nameof(BodyLockBannerVisibility));
            OnPropertyChanged(nameof(SelectedPriority));
            OnPropertyChanged(nameof(DeliveryDate));
            OnPropertyChanged(nameof(SiteDeliveryContact));
            OnPropertyChanged(nameof(SiteDeliveryPhone));
            OnPropertyChanged(nameof(SiteDeliveryAddress));
            OnPropertyChanged(nameof(AssignedTo));
            OnPropertyChanged(nameof(CuttingInstructions));
            OnPropertyChanged(nameof(DrawingReference));
            OnPropertyChanged(nameof(InternalNotes));

            UpdateStatusMeta();
            UpdatePriorityMeta();

            LastEditedDisplay = prod.LastEditedAt != default
                                    ? $"Last edited {prod.LastEditedAt:dd MMM yyyy HH:mm} by {prod.LastEditedBy}"
                                    : string.Empty;
        }

        // ═════════════════════════════════════════════════════════════════
        //  SAVE
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// v2 §28: public save entry for the unsaved-changes guard.
        /// Returns true when the record is clean afterwards.
        /// </summary>
        public async Task<bool> TrySaveAsync()
        {
            await SaveAsync();
            return !HasUnsavedChanges;
        }

        private async Task SaveAsync()
        {

            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.JobOrderEdit, "save a job order"))
                return;

            if (_entity == null || _snapshot == null)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.NothingLoadedToSave"),
                    LocalizationService.Instance.T("Msg.CapSaveFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var previousStatus = _entity.Status ?? _snapshot?.Production?.Status ?? "";

            try
            {
                // Enforce the workflow table on user status changes. New
                // orders (no prior status yet) skip validation — first save.
                if (!string.IsNullOrWhiteSpace(previousStatus))
                {
                    var statusTransition = _status.Validate(
                        StatusTransitions.Kind.JobOrder,
                        previousStatus, SelectedStatus);
                    if (!statusTransition.IsValid)
                    {
                        _dialogs.Show(statusTransition.Reason ?? "Invalid status change.",
                            LocalizationService.Instance.T("Msg.CapStatusChangeNotAllowed"),
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                bool statusChanged = !string.Equals(
                    previousStatus, SelectedStatus, StringComparison.OrdinalIgnoreCase);
                bool bodyEditable = !_entity.IsVoid
                    && ProGlassAutomation.Services.Workflow.DocumentLifecyclePolicy.CanEditBody(
                        StatusTransitions.Kind.JobOrder, previousStatus);

                if (bodyEditable)
                {
                    // Body save is deliberately status-neutral. Status writes
                    // use the dedicated repository transition below so a
                    // locked record can still take a legal status-only edge.
                    _snapshot.Production ??= new ProductionDetails();
                    _snapshot.Production.Status = previousStatus;
                    _snapshot.Production.Priority = SelectedPriority;
                    _snapshot.Production.DeliveryDate = DeliveryDate;
                    _snapshot.Production.SiteDeliveryContact = SiteDeliveryContact;
                    _snapshot.Production.SiteDeliveryPhone = SiteDeliveryPhone;
                    _snapshot.Production.SiteDeliveryAddress = SiteDeliveryAddress;
                    _snapshot.Production.AssignedTo = AssignedTo;
                    _snapshot.Production.CuttingInstructions = CuttingInstructions;
                    _snapshot.Production.DrawingReference = DrawingReference;
                    _snapshot.Production.InternalNotes = InternalNotes;
                    _snapshot.Production.LastEditedAt = DateTime.Now;
                    _snapshot.Production.LastEditedBy = AuditService.CurrentUsername;

                    JobOrderMapper.UpdateEntityFromSnapshot(_entity, _snapshot);
                    await _jobs.UpdateAsync(_entity, _entity.RowVersion);
                }

                if (statusChanged)
                {
                    var updated = await _jobs.UpdateStatusAsync(
                        _entity.Id, SelectedStatus, _entity.RowVersion);
                    if (updated == null)
                        throw new InvalidOperationException("Job Order no longer exists.");
                    _entity = updated;
                    _snapshot.Production ??= new ProductionDetails();
                    _snapshot.Production.Status = updated.Status;
                    _snapshot.Production.LastEditedAt = updated.UpdatedAt;
                    _snapshot.Production.LastEditedBy = AuditService.CurrentUsername;
                }
                else if (!bodyEditable)
                {
                    // No legal status transition and the body is locked: do not
                    // serialize stale detached fields back over the retained row.
                    HasUnsavedChanges = false;
                    return;
                }

                HasUnsavedChanges = false;

                // v2 §24: append a restorable version of every save.
                _ = ProGlassAutomation.Services.Audit.VersionHistoryService
                    .AddVersionAsync(
                        "JO", _entity.JobOrderId,
                        $"Status {SelectedStatus}, priority {SelectedPriority}",
                        _entity.FullSnapshotJson);

                var diff = new System.Collections.Generic.List<AuditChange>();
                if (!string.Equals(previousStatus, SelectedStatus))
                    diff.Add(AuditChangeSerializer.Modified(
                        "Status", previousStatus, SelectedStatus));

                if (!statusChanged)
                {
                    AuditService.LogWithChanges(
                        AuditSections.JobOrder, "Job order saved",
                        $"{_snapshot.JobOrderId} — status {SelectedStatus}",
                        "JO", _snapshot.JobOrderId, diff);
                }

                await RecordMetadataService.StampUpdatedAsync(
                    "JO", _snapshot.JobOrderId);

                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.JobOrderXSavedSuccessfully"), _snapshot.JobOrderId),
                    LocalizationService.Instance.T("ProformaInvoice.Saved"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                LastEditedDisplay = $"Last edited {_snapshot.Production.LastEditedAt:dd MMM yyyy HH:mm} " +
                                    $"by {_snapshot.Production.LastEditedBy}";
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToSaveX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapSaveFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  PHASE 10.5 — CREATE DELIVERY NOTE FROM THIS JO
        // ═════════════════════════════════════════════════════════════════

        private async Task OpenMasterDnAsync()
        {
            if (_entity == null || string.IsNullOrWhiteSpace(_entity.JobOrderId))
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.LoadAJobOrderFirst"),
                    LocalizationService.Instance.T("JobOrder.MasterDN"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_deliveryNotes == null)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.DeliveryNotesDatabaseNotInitialized"),
                    LocalizationService.Instance.T("JobOrder.MasterDN"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            try
            {
                var master = await _deliveryNotes
                    .GetMasterDnForJobOrderAsync(_entity.JobOrderId);
                if (master == null)
                {
                    try
                    {
                        master = await ProGlassAutomation.Services.DeliveryNote
                            .MasterDeliveryNoteService
                            .EnsureForJobOrderAsync(_entity);
                    }
                    catch (Exception ex)
                    {
                        _dialogs.Show(
                            "Could not create Master DN.\n\n" + ExceptionMessages.Deep(ex),
                            LocalizationService.Instance.T("JobOrder.MasterDN"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }

                if (master == null)
                {
                    _dialogs.Show(
                        LocalizationService.Instance.T("Msg.MasterDNExistsForThis"),
                        LocalizationService.Instance.T("JobOrder.MasterDN"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                OnRequestOpenDeliveryNote?.Invoke(master.Id);
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToOpenMasterDN"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("JobOrder.MasterDN"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task CreateDeliveryNoteAsync()
        {
            if (_entity == null || _snapshot == null)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.LoadAJobOrderFirst"),
                    LocalizationService.Instance.T("Msg.CapCannotCreateDN"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                var result = await ProGlassAutomation.Services.DeliveryNote
                    .DeliveryNoteCreateService
                    .CreateOrOpenDispatchingAsync(_entity);

                switch (result.Outcome)
                {
                    case ProGlassAutomation.Services.DeliveryNote.DnCreateResult.Kind.OpenExisting:
                    case ProGlassAutomation.Services.DeliveryNote.DnCreateResult.Kind.Created:
                        OnRequestOpenDeliveryNote?.Invoke(result.DeliveryNoteDbId);
                        break;

                    case ProGlassAutomation.Services.DeliveryNote.DnCreateResult.Kind.FullyDelivered:
                        _dialogs.Show(
                            result.Message,
                            LocalizationService.Instance.T("Msg.CapFullyDelivered"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        break;

                    default:
                        _dialogs.Show(
                            string.IsNullOrWhiteSpace(result.Message)
                                ? "Failed to create delivery note."
                                : result.Message,
                            LocalizationService.Instance.T("Msg.CapCreateFailed"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        break;
                }
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToCreateDeliveryNote"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapCreateFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  EXPORT (Phase 9.5a-r3 — shared engine consumer)
        // ═════════════════════════════════════════════════════════════════

        private async Task ExportPdfAsync()
        {
            if (!EnsureLoaded()) return;

            var layout = new JobOrderPdfLayout(_entity!, _snapshot!);
            var engine = new PdfExportEngine();
            var request = new ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave = true
            };

            var result = await engine.ExportAsync(layout, request);
            ShowExportResult(result, "PDF");
        }

        private async Task ExportExcelAsync()
        {
            if (!EnsureLoaded()) return;

            var layout = new JobOrderExcelLayout(_entity!, _snapshot!);
            var engine = new ExcelExportEngine();
            var request = new ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave = true
            };

            var result = await engine.ExportAsync(layout, request);
            ShowExportResult(result, "Excel");
        }

        private async Task ExportCsvAsync()
        {
            if (!EnsureLoaded()) return;

            var layout = new JobOrderCsvLayout(_entity!, _snapshot!);
            var engine = new CsvExportEngine();
            var request = new ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave = true
            };

            var result = await engine.ExportAsync(layout, request);
            ShowExportResult(result, "CSV");
        }

        // Sharing Center — same layouts as the exports above, staged once
        // into the share temp folder (rule's default format), then composer.
        private async Task ShareAsync()
        {
            if (!EnsureLoaded()) return;

            var entity = _entity!;
            var snapshot = _snapshot!;
            var context = ShareContextFactory.FromJobOrder(entity);

            await ShareLauncher.GenerateAndShareAsync(context, async folder =>
            {
                string ruleFormat = App.Config.Sharing
                    .RuleFor("JO").DefaultFormat ?? "Pdf";
                IDocumentLayout layout;
                ExportFormat format;
                if (string.Equals(ruleFormat, "Excel",
                        StringComparison.OrdinalIgnoreCase))
                {
                    layout = new JobOrderExcelLayout(entity, snapshot);
                    format = ExportFormat.Excel;
                }
                else if (string.Equals(ruleFormat, "Csv",
                        StringComparison.OrdinalIgnoreCase))
                {
                    layout = new JobOrderCsvLayout(entity, snapshot);
                    format = ExportFormat.Csv;
                }
                else
                {
                    layout = new JobOrderPdfLayout(entity, snapshot);
                    format = ExportFormat.Pdf;
                }

                var staged = await ShareLauncher.ExportLayoutToFolderAsync(
                    layout, format, folder);
                if (!staged.Success)
                    _dialogs.Error(
                        staged.ErrorMessage ?? "Export failed.", "Share");
                return staged.SavedPath;
            }, _dialogs);
        }

        private bool EnsureLoaded()
        {
            if (_entity != null && _snapshot != null) return true;

            _dialogs.Show(
                LocalizationService.Instance.T("Msg.LoadAJobOrderBefore"),
                LocalizationService.Instance.T("Msg.CapNothingToExport"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        private void ShowExportResult(ExportResult result, string formatLabel)
        {
            if (result.Success) return;
            if (result.Cancelled) return;

            _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.XExportFailedY"), formatLabel, result.ErrorMessage),
                LocalizationService.Instance.T("Msg.CapExportFailed"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════

        private void UpdateStatusMeta()
        {
            if (Enum.TryParse<JobOrderStatus>(SelectedStatus, ignoreCase: true, out var s))
            {
                StatusLabel = JobOrderStatusMeta.GetLabel(s);
                StatusBrushKey = JobOrderStatusMeta.GetBrushKey(s);
            }
            else
            {
                StatusLabel = SelectedStatus;
                StatusBrushKey = "NeutralBrush";
            }
        }

        private void UpdatePriorityMeta()
        {
            if (Enum.TryParse<JobPriority>(SelectedPriority, ignoreCase: true, out var p))
            {
                PriorityLabel = JobPriorityMeta.GetLabel(p);
                PriorityBrushKey = JobPriorityMeta.GetBrushKey(p);
            }
            else
            {
                PriorityLabel = SelectedPriority;
                PriorityBrushKey = "NeutralBrush";
            }
        }
    }
}