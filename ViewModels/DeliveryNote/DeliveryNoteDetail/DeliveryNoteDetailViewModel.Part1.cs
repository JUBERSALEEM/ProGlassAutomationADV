// ═══════════════════════════════════════════════════════════════════════
// FILE: DeliveryNoteDetailViewModel.Part1.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Win32;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.DeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Attachments;
using ProGlassAutomation.Services.DeliveryNote.Export;
using ProGlassAutomation.Services.DeliveryNote.Mapper;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.Views.DeliveryNote.DeliveryNoteDetail.Attachments.Rename;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Workflow;
using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus;
using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail
{
        public partial class DeliveryNoteDetailViewModel : ViewModelBase
    {
            private readonly IDeliveryNoteRepository _deliveryNotes = null!;
        private readonly IDialogService _dialogs = null!;
        private readonly IStatusService _statusService = null!;
        private readonly IDatabaseServices _database = null!;

        public DeliveryNoteDetailViewModel(IDeliveryNoteRepository? deliveryNotes, IDialogService? dialogs, IStatusService? status, IDatabaseServices? database)
        {
            _deliveryNotes = deliveryNotes!;
            _dialogs = dialogs!;
            _statusService = status!;
            _database = database!;
        }

        // ── callbacks ──────────────────────────────────────────────────────
        public Action? OnRequestBackToList { get; set; }
        public Func<string, Task>? OnRequestOpenTaxInvoiceForDeliveryNote { get; set; }

        /// <summary>
        /// CHAT 19 — fires when user clicks "Create New DN from Balance"
        /// on a Master DN detail view.
        /// </summary>
        public Action<SavedDeliveryNote>? OnRequestCreateDnFromMaster { get; set; }

        /// <summary>
        /// After dispatch, persist a Draft tax invoice for THIS delivery.
        /// Returns the invoice number or empty.
        /// </summary>
        public Func<string, Task<string>>? OnRequestAutoCreateTaxInvoice { get; set; }

        /// <summary>
        /// Opens the Master DN created from this job order (remaining
        /// balance by default; full original via the toggle).
        /// </summary>
        public Action<string>? OnRequestOpenMasterDn { get; set; }

        // ── loaded entity + snapshot ──────────────────────────────────────
        private SavedDeliveryNote? _entity;
        private DeliveryNoteSnapshot? _snapshot;
        private readonly AttachmentService _attachmentService = new();
        private bool _isNavigatingHistory;

        // ── CHAT 19 — Master DN state ─────────────────────────────────────

        private bool _isMasterDn;
        public bool IsMasterDn
        {
            get => _isMasterDn;
            private set
            {
                if (_isMasterDn == value) return;
                _isMasterDn = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotMasterDn));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsNotMasterDn => !_isMasterDn;

        private bool _showFullMaster;
        /// <summary>
        /// Master default is remaining balance only. When true, show the
        /// original Job Order set (including fully delivered lines).
        /// </summary>
        public bool ShowFullMaster
        {
            get => _showFullMaster;
            set
            {
                if (_showFullMaster == value) return;
                _showFullMaster = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsShowingRemainingOnly));
                OnPropertyChanged(nameof(MasterScopeButtonText));
                OnPropertyChanged(nameof(HasHiddenFullyDeliveredRows));
                OnPropertyChanged(nameof(HasNoPendingBalanceItems));
                OnPropertyChanged(nameof(HiddenFullyDeliveredSummary));
                RebuildGroupedView();
            }
        }

        public bool IsShowingRemainingOnly => _isMasterDn && !_showFullMaster;

        public string MasterScopeButtonText =>
            _showFullMaster
                ? "REMAINING ONLY"
                : "SHOW FULL MASTER";

        // ── loading state ─────────────────────────────────────────────────

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            private set
            {
                if (_isSaving == value) return;
                _isSaving = value;
                OnPropertyChanged();
            }
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            private set
            {
                if (_hasError == value) return;
                _hasError = value;
                OnPropertyChanged();
            }
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (_errorMessage == value) return;
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        // ── DN header fields ─────────────────────────────────────────────

        private string _deliveryNoteId = "";
        public string DeliveryNoteId
        {
            get => _deliveryNoteId;
            private set
            {
                if (_deliveryNoteId == value) return;
                _deliveryNoteId = value;
                OnPropertyChanged();
            }
        }

        private string _jobOrderId = "";
        public string JobOrderId
        {
            get => _jobOrderId;
            private set
            {
                if (_jobOrderId == value) return;
                _jobOrderId = value;
                OnPropertyChanged();
            }
        }

        private string _proformaId = "";
        public string ProformaId
        {
            get => _proformaId;
            private set
            {
                if (_proformaId == value) return;
                _proformaId = value;
                OnPropertyChanged();
            }
        }

        private string _deliveryType = "Original";
        public string DeliveryType
        {
            get => _deliveryType;
            private set
            {
                if (_deliveryType == value) return;
                _deliveryType = value;
                OnPropertyChanged();
            }
        }

        private string _deliveryDateDisplay = "";
        public string DeliveryDateDisplay
        {
            get => _deliveryDateDisplay;
            private set
            {
                if (_deliveryDateDisplay == value) return;
                _deliveryDateDisplay = value;
                OnPropertyChanged();
            }
        }

        private string _deliveryTime = "";
        public string DeliveryTime
        {
            get => _deliveryTime;
            private set
            {
                if (_deliveryTime == value) return;
                _deliveryTime = value;
                OnPropertyChanged();
            }
        }

        private int _sequenceNumber;
        public int SequenceNumber
        {
            get => _sequenceNumber;
            private set
            {
                if (_sequenceNumber == value) return;
                _sequenceNumber = value;
                OnPropertyChanged();
            }
        }

        // ── JO context ───────────────────────────────────────────────────

        private string _customerName = "";
        public string CustomerName
        {
            get => _customerName;
            private set
            {
                if (_customerName == value) return;
                _customerName = value;
                OnPropertyChanged();
            }
        }

        private string _projectName = "";
        public string ProjectName
        {
            get => _projectName;
            private set
            {
                if (_projectName == value) return;
                _projectName = value;
                OnPropertyChanged();
            }
        }

        private string _projectReference = "";
        public string ProjectReference
        {
            get => _projectReference;
            private set
            {
                if (_projectReference == value) return;
                _projectReference = value;
                OnPropertyChanged();
            }
        }

        private string _companyName = "";
        public string CompanyName
        {
            get => _companyName;
            private set
            {
                if (_companyName == value) return;
                _companyName = value;
                OnPropertyChanged();
            }
        }

        private string _companyTrn = "";
        public string CompanyTrn
        {
            get => _companyTrn;
            private set
            {
                if (_companyTrn == value) return;
                _companyTrn = value;
                OnPropertyChanged();
            }
        }

        // ── Editable fields ──────────────────────────────────────────────

        private string _status = "Dispatching";
        // ── v2 §28: unsaved-changes tracking ────────────────────────────
        private bool _hasUnsavedChanges;
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            private set
            {
                if (_hasUnsavedChanges == value) return;
                _hasUnsavedChanges = value;
                OnPropertyChanged();
            }
        }

        /// <summary>True while hydrating — setters must not mark dirty.</summary>
        private bool _suppressDirty;

        private void MarkDirty()
        {
            if (!_suppressDirty) HasUnsavedChanges = true;
        }

        /// <summary>v2 §28: public save entry for the unsaved-changes guard.</summary>
        public async Task<bool> TrySaveAsync()
        {
            await SaveAsync();
            return !HasUnsavedChanges;
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                OnPropertyChanged();
                MarkDirty();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _isFoc;
        public bool IsFoc
        {
            get => _isFoc;
            set
            {
                if (_isFoc == value) return;
                _isFoc = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }

        private string _driverName = "";
        public string DriverName
        {
            get => _driverName;
            set
            {
                if (_driverName == value) return;
                _driverName = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }

        private string _vehicleNumber = "";
        public string VehicleNumber
        {
            get => _vehicleNumber;
            set
            {
                if (_vehicleNumber == value) return;
                _vehicleNumber = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }

        private string _receiverName = "";
        public string ReceiverName
        {
            get => _receiverName;
            set
            {
                if (_receiverName == value) return;
                _receiverName = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }

        private string _notes = "";
        public string Notes
        {
            get => _notes;
            set
            {
                if (_notes == value) return;
                _notes = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }

        public System.Collections.Generic.List<string> StatusOptions { get; } = new()
        {
            "Dispatching", "Dispatched", "Signed", "Received", "Draft"
        };

        // ── Delivery items grid ───────────────────────────────────────────

        private ObservableCollection<DnItemRowViewModel> _itemRows = new();
        public ObservableCollection<DnItemRowViewModel> ItemRows
        {
            get => _itemRows;
            private set
            {
                if (_itemRows == value) return;
                _itemRows = value;
                OnPropertyChanged();
                RebuildGroupedView();
            }
        }

        private ICollectionView? _groupedItemsView;
        public ICollectionView? GroupedItemsView
        {
            get => _groupedItemsView;
            private set
            {
                if (_groupedItemsView == value) return;
                _groupedItemsView = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<DnItemRowViewModel> _visibleRows = new();

        private int _hiddenFullyDeliveredCount;
        public int HiddenFullyDeliveredCount
        {
            get => _hiddenFullyDeliveredCount;
            private set
            {
                if (_hiddenFullyDeliveredCount == value) return;
                _hiddenFullyDeliveredCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasHiddenFullyDeliveredRows));
                OnPropertyChanged(nameof(HiddenFullyDeliveredSummary));
            }
        }

        public int VisiblePendingRowCount => _visibleRows.Count;

        public bool HasHiddenFullyDeliveredRows =>
            !_showFullMaster && HiddenFullyDeliveredCount > 0;

        public bool HasNoPendingBalanceItems =>
            _itemRows.Count > 0 && _visibleRows.Count == 0 && !_showFullMaster;

        public string HiddenFullyDeliveredSummary =>
            _isMasterDn
                ? (HiddenFullyDeliveredCount == 1
                    ? "1 item fully delivered — hidden. Showing remaining balance only."
                    : $"{HiddenFullyDeliveredCount} items fully delivered — hidden. Showing remaining balance only.")
                : (HiddenFullyDeliveredCount == 1
                    ? "1 item already fully delivered — hidden. No qty left on this DN."
                    : $"{HiddenFullyDeliveredCount} items already fully delivered — hidden. No qty left on this DN.");

        /// <summary>
        /// Materialized remaining (or live) rows. Never bind the grid to
        /// ItemRows — leftover closed lines live there for KPI totals.
        /// </summary>
        public ObservableCollection<DnItemRowViewModel> VisibleItemRows => _visibleRows;

        private ObservableCollection<DnSpecGroupViewModel> _visibleGroups = new();
        public ObservableCollection<DnSpecGroupViewModel> VisibleGroups
        {
            get => _visibleGroups;
            private set
            {
                _visibleGroups = value;
                OnPropertyChanged();
            }
        }

        // ── Row selection (Sheet Store pattern) ────────────────────────────
        // Click a glass row → the side panel shows THAT row's stats; click
        // again (or the close button) → back to the totals in the top strip.

        private DnItemRowViewModel? _selectedItemRow;
        public DnItemRowViewModel? SelectedItemRow
        {
            get => _selectedItemRow;
            set
            {
                if (ReferenceEquals(_selectedItemRow, value)) return;

                if (_selectedItemRow != null)
                    _selectedItemRow.IsSelected = false;

                _selectedItemRow = value;

                if (_selectedItemRow != null)
                    _selectedItemRow.IsSelected = true;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsRowSelected));
                OnPropertyChanged(nameof(SelectionLabel));
            }
        }

        public bool IsRowSelected => SelectedItemRow != null;

        /// <summary>Header label for the slide-in panel (glass ref + serial).</summary>
        public string SelectionLabel => SelectedItemRow == null
            ? ""
            : string.IsNullOrWhiteSpace(SelectedItemRow.GlassRef)
                ? $"Sr {SelectedItemRow.SerialNumber}"
                : $"{SelectedItemRow.GlassRef} · Sr {SelectedItemRow.SerialNumber}";

        public ICommand ClearSelectionCommand { get; }

        private static bool IsOpenForDispatch(string? status) =>
            DeliveryNoteWorkflow.IsOpenForDispatch(status);

        private static bool DetectMaster(SavedDeliveryNote entity)
        {
            if (entity.IsMasterDn) return true;
            if (!string.IsNullOrWhiteSpace(entity.DeliveryNoteId) &&
                entity.DeliveryNoteId.StartsWith("MDN-", StringComparison.OrdinalIgnoreCase))
                return true;
            return entity.SequenceNumber == 0 &&
                   string.Equals(entity.DeliveryType, "Dummy",
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Master: remaining balance only (unless SHOW FULL MASTER).
        /// Real DN: hide leftover closed lines (This=0 and Balance=0).
        /// Those are JO copies whose pcs already left on a sibling DN —
        /// they look like "empty rows" and must not stay in the grid.
        /// </summary>
        private bool ShouldDisplayRow(DnItemRowViewModel row) =>
            // Phase 6: Delegated to domain service — testable, reusable
            Services.DeliveryNote.DeliveryNoteLineService.ShouldDisplayRow(row, IsMasterDn, ShowFullMaster);

        private void RebuildGroupedView()
        {
            var displayed = _itemRows.Where(ShouldDisplayRow).ToList();
            _visibleRows = new ObservableCollection<DnItemRowViewModel>(displayed);
            HiddenFullyDeliveredCount = _itemRows.Count - displayed.Count;

            VisibleGroups = new ObservableCollection<DnSpecGroupViewModel>(
                displayed
                    .GroupBy(r => r.SpecGroupName)
                    .Select(g => new DnSpecGroupViewModel(g.Key, g)));

            OnPropertyChanged(nameof(VisibleItemRows));
            OnPropertyChanged(nameof(VisiblePendingRowCount));
            OnPropertyChanged(nameof(HasHiddenFullyDeliveredRows));
            OnPropertyChanged(nameof(HasNoPendingBalanceItems));
            OnPropertyChanged(nameof(HiddenFullyDeliveredSummary));
        }

        // ── Qty summary ───────────────────────────────────────────────────

        private int _totalOrderedQty;
        public int TotalOrderedQty
        {
            get => _totalOrderedQty;
            private set
            {
                if (_totalOrderedQty == value) return;
                _totalOrderedQty = value;
                OnPropertyChanged();
            }
        }

        private int _totalPreviouslyDeliveredQty;
        public int TotalPreviouslyDeliveredQty
        {
            get => _totalPreviouslyDeliveredQty;
            private set
            {
                if (_totalPreviouslyDeliveredQty == value) return;
                _totalPreviouslyDeliveredQty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DeliveryProgressPercent));
            }
        }

        private int _totalDeliveredQty;
        public int TotalDeliveredQty
        {
            get => _totalDeliveredQty;
            private set
            {
                if (_totalDeliveredQty == value) return;
                _totalDeliveredQty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DeliveryProgressPercent));
            }
        }

        private int _totalBalanceQty;
        public int TotalBalanceQty
        {
            get => _totalBalanceQty;
            private set
            {
                if (_totalBalanceQty == value) return;
                _totalBalanceQty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DeliveryProgressPercent));
                OnPropertyChanged(nameof(IsFullyDelivered));
            }
        }

        public double DeliveryProgressPercent
        {
            get
            {
                if (TotalOrderedQty == 0) return 0;
                int delivered = TotalPreviouslyDeliveredQty + TotalDeliveredQty;
                return Math.Min(100.0,
                    (double)delivered / TotalOrderedQty * 100.0);
            }
        }

        public bool IsFullyDelivered =>
            TotalOrderedQty > 0 && TotalBalanceQty == 0;

        // ── Delivery history ──────────────────────────────────────────────

        private ObservableCollection<DnHistoryEntryViewModel> _historyEntries = new();
        public ObservableCollection<DnHistoryEntryViewModel> HistoryEntries
        {
            get => _historyEntries;
            private set
            {
                if (_historyEntries == value) return;
                _historyEntries = value;
                OnPropertyChanged();
            }
        }

        private int _historyTotalOrdered;
        public int HistoryTotalOrdered
        {
            get => _historyTotalOrdered;
            private set
            {
                if (_historyTotalOrdered == value) return;
                _historyTotalOrdered = value;
                OnPropertyChanged();
            }
        }

        private int _historyTotalDelivered;
        public int HistoryTotalDelivered
        {
            get => _historyTotalDelivered;
            private set
            {
                if (_historyTotalDelivered == value) return;
                _historyTotalDelivered = value;
                OnPropertyChanged();
            }
        }

        private int _historyTotalBalance;
        public int HistoryTotalBalance
        {
            get => _historyTotalBalance;
            private set
            {
                if (_historyTotalBalance == value) return;
                _historyTotalBalance = value;
                OnPropertyChanged();
            }
        }

        // ── Selected history entry ────────────────────────────────────────

        private DnHistoryEntryViewModel? _selectedHistoryEntry;
        public DnHistoryEntryViewModel? SelectedHistoryEntry
        {
            get => _selectedHistoryEntry;
            set
            {
                if (_selectedHistoryEntry == value) return;
                if (_selectedHistoryEntry != null)
                    _selectedHistoryEntry.IsSelected = false;
                _selectedHistoryEntry = value;
                if (_selectedHistoryEntry != null)
                    _selectedHistoryEntry.IsSelected = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedDnHeaderText));
                OnPropertyChanged(nameof(IsSelectedDnCurrent));
            }
        }

        public bool IsSelectedDnCurrent =>
            _selectedHistoryEntry == null ||
            string.Equals(
                _selectedHistoryEntry.DeliveryNoteId,
                _deliveryNoteId,
                StringComparison.OrdinalIgnoreCase);

        public string SelectedDnHeaderText =>
            _selectedHistoryEntry != null
                ? $"ATTACHMENTS ({_selectedHistoryEntry.AttachmentCount})"
                : $"ATTACHMENTS ({AttachmentsCount})";

        public ICommand SelectHistoryEntryCommand { get; }

        // ── Attachments ───────────────────────────────────────────────────

        private ObservableCollection<DnAttachmentRowViewModel> _attachments = new();
        public ObservableCollection<DnAttachmentRowViewModel> Attachments
        {
            get => _attachments;
            private set
            {
                if (_attachments == value) return;
                _attachments = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AttachmentsCount));
                OnPropertyChanged(nameof(HasNoAttachments));
                OnPropertyChanged(nameof(HasAttachments));
                OnPropertyChanged(nameof(SelectedDnHeaderText));
            }
        }

        public int AttachmentsCount => Attachments?.Count ?? 0;
        public bool HasNoAttachments => AttachmentsCount == 0;
        public bool HasAttachments => AttachmentsCount > 0;

        // ── Commands ──────────────────────────────────────────────────────

        // ═══ Detail tab state ═══
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
                OnPropertyChanged(nameof(IsAttachmentsTab));
                OnPropertyChanged(nameof(IsHistoryTab));
            }
        }
        public bool IsOverviewTab => DetailTab == "Overview";
        public bool IsAttachmentsTab => DetailTab == "Attachments";
        public bool IsHistoryTab => DetailTab == "History";
        public ICommand SwitchDetailTabCommand { get; }

        public ICommand SaveCommand { get; }

        /// <summary>v2 §24: shows this delivery note's version history.</summary>
        public ICommand ShowVersionsCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand MakeDnCommand { get; }
        public ICommand DeleteDnCommand { get; }
        public ICommand UploadImageCommand { get; }
        public ICommand UploadPdfCommand { get; }
        public ICommand OpenAttachmentCommand { get; }
        public ICommand DeleteAttachmentCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ConvertToTaxInvoiceCommand { get; }

        /// <summary>
        /// CHAT 19 — SELECT ALL REMAINING.
        /// Sets DeliveredQty = BalanceQty for every item where BalanceQty > 0.
        /// REMINDER: fills ALL remaining pcs.
        /// Hidden on Master DN.
        /// </summary>
        public ICommand FillRemainingCommand { get; }

        /// <summary>
        /// CHAT 19 — RESET QTY.
        /// Sets DeliveredQty = 0 for ALL items.
        /// Companion to FillRemainingCommand — lets user start over.
        /// Hidden on Master DN.
        /// </summary>
        public ICommand ResetQtyCommand { get; }

        /// <summary>
        /// CHAT 19 — shown only on Master DN detail view.
        /// Fires OnRequestCreateDnFromMaster → shell VM creates Draft DN.
        /// </summary>
        public ICommand CreateNewDnFromMasterCommand { get; }

        /// <summary>Master: remaining-only vs original Job Order set.</summary>
        public ICommand ToggleFullMasterCommand { get; }

        /// <summary>Opens the Master DN created from this job order.</summary>
        public ICommand OpenMasterCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════
    }
}
