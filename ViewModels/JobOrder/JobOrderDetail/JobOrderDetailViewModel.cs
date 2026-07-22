using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.JobOrder.Common.JobPriority;
using ProGlassAutomation.Models.JobOrder.ProductionDetails;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.JobOrder.Mapper;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.JobOrder.JobOrderDetail
{
    /// <summary>
    /// ViewModel for the Job Order detail view.
    /// Left column shows the embedded (read-only) Proforma Invoice snapshot.
    /// Right column shows the editable ProductionDetails.
    ///
    /// LoadAsync(jobOrderId) fetches the entity, deserialises the snapshot,
    /// and populates all display + editable properties.
    ///
    /// SaveCommand persists changes to production fields without touching
    /// the embedded invoice (it's immutable historical record).
    /// </summary>
    public class JobOrderDetailViewModel : ViewModelBase
    {
        // ── reentrancy guard ──────────────────────────────────────────────
        private int _loadingFlag = 0;

        // ── callback wired by shell VM ────────────────────────────────────
        public Action? OnRequestBackToList { get; set; }

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
            }
        }

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

        public ICommand SaveCommand { get; }
        public ICommand BackToListCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public JobOrderDetailViewModel()
        {
            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            BackToListCommand = new RelayCommand(_ => OnRequestBackToList?.Invoke());
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

                if (App.Jobs == null)
                {
                    HasError = true;
                    ErrorMessage = "Job Order database not initialised.";
                    return;
                }

                _entity = await App.Jobs.GetByIdAsync(jobOrderId);
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
                ErrorMessage = $"Load failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                Interlocked.Exchange(ref _loadingFlag, 0);
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

        private async Task SaveAsync()
        {
            if (_entity == null || _snapshot == null)
            {
                MessageBox.Show(
                    "Nothing loaded to save.",
                    "Save Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Update production fields on the snapshot
                _snapshot.Production ??= new ProductionDetails();
                _snapshot.Production.Status = SelectedStatus;
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
                _snapshot.Production.LastEditedBy = Environment.UserName;

                // Push snapshot back into entity (updates indexed columns + JSON blob)
                JobOrderMapper.UpdateEntityFromSnapshot(_entity, _snapshot);

                await App.Jobs.UpdateAsync(_entity);

                HasUnsavedChanges = false;

                MessageBox.Show(
                    $"Job order '{_snapshot.JobOrderId}' saved successfully.",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Refresh audit line
                LastEditedDisplay = $"Last edited {_snapshot.Production.LastEditedAt:dd MMM yyyy HH:mm} " +
                                    $"by {_snapshot.Production.LastEditedBy}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to save:\n\n{ex.Message}",
                    "Save Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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