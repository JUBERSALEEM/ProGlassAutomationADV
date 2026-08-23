using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Inquiry
{
    /// <summary>
    /// Inquiry workspace — 2026 SaaS rebuild.
    ///
    /// The previous editor had four behavioural defects, fixed here:
    /// - S4: "attach before save" dead end → the draft is auto-saved before the
    ///   first attachment so the user's action always completes;
    /// - S6: Cancel silently discarded work → dirty-state tracking + discard
    ///   confirmation callback;
    /// - S8: silent decimal binding failures → explicit ValueText parsing with
    ///   inline validation;
    /// - A5: unobserved fire-and-forget loads → SafeFireAndForget with guards.
    ///
    /// Form state lives in bindable VM properties (UpdateSourceTrigger=
    /// PropertyChanged in the view), so a button click can never commit a
    /// half-updated record.
    /// </summary>
    public sealed class InquiryEditorViewModel : ViewModelBase
    {
        private readonly IInquiryWorkflowService _workflow;
        private readonly InquiryAttachmentService _attachments;

        public InquiryRecord Record { get; }
        public bool IsNewRecord => Record.Id == 0;
        public string HeaderTitle => IsNewRecord ? "New inquiry" : Record.InquiryNumber;
        public string SaveCommandText => IsNewRecord ? "Save inquiry" : "Save changes";
        public string HeaderSubtitle => IsNewRecord
            ? "Capture the commercial intake — attachments can be added right away"
            : $"Received {Record.ReceivedAt:dd MMM yyyy} · last updated {Record.UpdatedAt:dd MMM yyyy}";

        public ObservableCollection<InquiryAttachment> AttachmentItems { get; } = new();
        public bool HasAttachments => AttachmentItems.Count > 0;

        public Func<string[]?>? SelectFilesRequested { get; set; }
        public Func<bool>? ConfirmDiscardRequested { get; set; }
        public Func<InquiryAttachment, bool>? ConfirmDeleteRequested { get; set; }
        public Action<InquiryAttachment>? PreviewRequested { get; set; }

        public ObservableCollection<string> Priorities { get; } = new() { "Urgent", "High", "Normal", "Low" };
        public ObservableCollection<string> Salesmen { get; } = new();
        public ObservableCollection<string> Customers { get; } = new();

        // ── Form fields (each setter marks the form dirty) ─────────────

        private string _reference = "";
        public string Reference { get => _reference; set => SetField(ref _reference, value); }
        private string _customerName = "";
        public string CustomerName { get => _customerName; set => SetField(ref _customerName, value); }
        private string _customerLocation = "";
        public string CustomerLocation { get => _customerLocation; set => SetField(ref _customerLocation, value); }
        private string _projectName = "";
        public string ProjectName { get => _projectName; set => SetField(ref _projectName, value); }
        private string _projectDescription = "";
        public string ProjectDescription { get => _projectDescription; set => SetField(ref _projectDescription, value); }
        private string _salesmanName = "";
        public string SalesmanName { get => _salesmanName; set => SetField(ref _salesmanName, value); }
        private string _notes = "";
        public string Notes { get => _notes; set => SetField(ref _notes, value); }
        private string _priority = "Normal";
        public string Priority { get => _priority; set => SetField(ref _priority, value); }
        private DateTime _receivedDate = DateTime.Today;
        public DateTime ReceivedDate { get => _receivedDate; set { if (SetField(ref _receivedDate, value.Date)) { OnPropertyChanged(nameof(ReceivedHint)); } } }
        private string _valueText = "";
        public string ValueText
        {
            get => _valueText;
            set
            {
                if (SetField(ref _valueText, value))
                {
                    OnPropertyChanged(nameof(HasValueError));
                    OnPropertyChanged(nameof(ValueHint));
                }
            }
        }
        public bool HasValueError => _valueText.Trim().Length > 0 && !decimal.TryParse(
            _valueText, NumberStyles.Number, CultureInfo.CurrentCulture, out _);
        public string ValueHint => HasValueError
            ? "Numbers only — thousands separators are allowed."
            : "Estimated inquiry value in AED.";

        // Received time is preserved from the original record; the date box only
        // changes the calendar day.
        public string ReceivedHint => $"Received at {ReceivedDate.Add(_receivedTime):hh:mm tt}";

        private readonly TimeSpan _receivedTime;

        // ── Status & commands ──────────────────────────────────────────

        public string Status => Record.Status;
        public bool CanPrepare => InquiryWorkflowService.CanTransition(Record.Status, InquiryStatuses.Prepared);
        public bool CanSend => InquiryWorkflowService.CanTransition(Record.Status, InquiryStatuses.Sent);
        public bool CanConfirm => InquiryWorkflowService.CanTransition(Record.Status, InquiryStatuses.Confirmed);
        public bool CanHold => InquiryWorkflowService.CanTransition(Record.Status, InquiryStatuses.Hold);
        public bool CanCancel => InquiryWorkflowService.CanTransition(Record.Status, InquiryStatuses.Cancelled);
        public string PreparedDateText => Record.PreparedAt?.ToString("dd MMM · hh:mm tt") ?? "";
        public string SentDateText => Record.SentAt?.ToString("dd MMM · hh:mm tt") ?? "";
        public string ConfirmedDateText => Record.ConfirmedAt?.ToString("dd MMM · hh:mm tt") ?? "";

        public ICommand SaveCommand { get; }
        public ICommand ChangeStatusCommand { get; }
        public ICommand AddAttachmentCommand { get; }
        public ICommand DeleteAttachmentCommand { get; }
        public ICommand PreviewAttachmentCommand { get; }
        public ICommand CancelCommand { get; }

        /// <summary>Raised with true when the record was saved and the editor should close.</summary>
        public event Action<bool>? CloseRequested;

        public InquiryEditorViewModel(IInquiryWorkflowService workflow,
            InquiryAttachmentService attachments, InquiryRecord? source = null)
        {
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
            _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
            Record = source ?? new InquiryRecord
            {
                InquiryReference = "Customer Inquiry", Status = InquiryStatuses.Received,
                Priority = "Normal", ReceivedAt = DateTime.Now
            };
            _reference = Record.InquiryReference;
            _customerName = Record.CustomerName;
            _customerLocation = Record.CustomerLocation;
            _projectName = Record.ProjectName;
            _projectDescription = Record.ProjectDescription;
            _salesmanName = Record.SalesmanName;
            _notes = Record.Notes;
            _priority = string.IsNullOrWhiteSpace(Record.Priority) ? "Normal" : Record.Priority;
            _receivedDate = Record.ReceivedAt == default ? DateTime.Today : Record.ReceivedAt.Date;
            _receivedTime = Record.ReceivedAt == default ? DateTime.Now.TimeOfDay : Record.ReceivedAt.TimeOfDay;
            _valueText = Record.InquiryValue == 0 ? "" : Record.InquiryValue.ToString("N2", CultureInfo.CurrentCulture);
            _isDirty = false;

            SaveCommand = new RelayCommand(async _ => await SaveAndCloseAsync(), _ => CanSave && !IsBusy);
            ChangeStatusCommand = new RelayCommand(async p => await ChangeStatusAsync(p as string), _ => !IsBusy);
            AddAttachmentCommand = new RelayCommand(async _ => await AddAttachmentsAsync(), _ => !IsBusy);
            DeleteAttachmentCommand = new RelayCommand(async p =>
            {
                if (p is InquiryAttachment file && ConfirmDeleteRequested?.Invoke(file) == true)
                {
                    IsBusy = true;
                    try
                    {
                        bool removed = await _attachments.DeleteAsync(file);
                        if (removed) AttachmentItems.Remove(file);
                        SetMessage(removed ? $"Deleted '{file.FileName}'." : "Attachment was already removed.");
                        OnPropertyChanged(nameof(HasAttachments));
                    }
                    catch (Exception ex) { SetMessage("Delete failed: " + ex.Message, isError: true); }
                    finally { IsBusy = false; }
                }
            }, _ => !IsBusy);
            PreviewAttachmentCommand = new RelayCommand(p =>
            {
                if (p is InquiryAttachment file) PreviewRequested?.Invoke(file);
            });
            CancelCommand = new RelayCommand(_ => CloseOrDiscard());

            AsyncUtility.SafeFireAndForget(LoadMastersAsync, "Inquiry editor masters");
            AsyncUtility.SafeFireAndForget(LoadAttachmentsAsync, "Inquiry editor attachments");
        }

        private bool _isDirty;
        public bool IsDirty { get => _isDirty; private set { _isDirty = value; OnPropertyChanged(); OnPropertyChanged(nameof(HeaderDotText)); } }
        public string HeaderDotText => IsDirty ? "●" : "";

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); } }

        public bool CanSave =>
            !string.IsNullOrWhiteSpace(CustomerName)
            && !string.IsNullOrWhiteSpace(ProjectName)
            && !HasValueError;

        private bool SetField<T>(ref T field, T value,
            [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName ?? "");
            IsDirty = true;
            OnPropertyChanged(nameof(CanSave));
            return true;
        }

        private async Task LoadMastersAsync()
        {
            if (App.MasterData == null) return;
            try
            {
                foreach (var x in await App.MasterData.GetAllClientsAsync())
                    if (!string.IsNullOrWhiteSpace(x.CustomerName) && !Customers.Contains(x.CustomerName))
                        Customers.Add(x.CustomerName);
                foreach (var x in await App.MasterData.GetAllSalesmenAsync())
                    if (!string.IsNullOrWhiteSpace(x.Name) && !Salesmen.Contains(x.Name))
                        Salesmen.Add(x.Name);
            }
            catch (Exception ex) { SetMessage("Master data unavailable: " + ex.Message, isError: true); }
        }

        private async Task LoadAttachmentsAsync()
        {
            if (Record.Id <= 0 || App.Inquiries == null) return;
            try
            {
                var rows = await App.Inquiries.GetAttachmentsAsync(Record.Id);
                AttachmentItems.Clear();
                foreach (var x in rows) AttachmentItems.Add(x);
                OnPropertyChanged(nameof(HasAttachments));
            }
            catch (Exception ex) { SetMessage("Attachments unavailable: " + ex.Message, isError: true); }
        }

        // ── Save ───────────────────────────────────────────────────────

        private void ApplyToRecord()
        {
            Record.InquiryReference = string.IsNullOrWhiteSpace(Reference) ? "Customer Inquiry" : Reference.Trim();
            Record.CustomerName = CustomerName.Trim();
            Record.CustomerLocation = CustomerLocation.Trim();
            Record.ProjectName = ProjectName.Trim();
            Record.ProjectDescription = ProjectDescription.Trim();
            Record.SalesmanName = SalesmanName.Trim();
            Record.Notes = Notes;
            Record.Priority = Priorities.Contains(Priority) ? Priority : "Normal";
            Record.ReceivedAt = ReceivedDate == DateTime.MinValue
                ? DateTime.Now
                : ReceivedDate.Add(_receivedTime);
            Record.InquiryValue = decimal.TryParse(ValueText, NumberStyles.Number,
                CultureInfo.CurrentCulture, out decimal value) ? decimal.Round(value, 2) : 0m;
        }

        /// <summary>Creates or updates the record. Returns the saved record or null on failure.</summary>
        private async Task<InquiryRecord?> SaveAsync()
        {
            if (!CanSave)
            {
                SetMessage(string.IsNullOrWhiteSpace(CustomerName)
                    ? "Customer is required before saving."
                    : string.IsNullOrWhiteSpace(ProjectName)
                        ? "Project is required before saving."
                        : "Fix the highlighted fields before saving.", isError: true);
                return null;
            }
            ApplyToRecord();
            var saved = Record.Id == 0
                ? await _workflow.CreateAsync(Record)
                : await _workflow.UpdateAsync(Record);
            IsDirty = false;
            OnPropertyChanged(nameof(IsNewRecord));
            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderSubtitle));
            OnPropertyChanged(nameof(SaveCommandText));
            return saved;
        }

        private async Task SaveAndCloseAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                SetMessage("Saving…");
                if (await SaveAsync() != null)
                {
                    SetMessage($"Saved {Record.InquiryNumber}.");
                    CloseRequested?.Invoke(true);
                }
            }
            catch (Exception ex) { SetMessage(ex.Message, isError: true); }
            finally { IsBusy = false; }
        }

        // ── Attachments ────────────────────────────────────────────────

        /// <summary>Public entry for drag-and-drop from the view.</summary>
        public Task AddDroppedFilesAsync(string[] files) => AddFilesAsync(files);

        private async Task AddAttachmentsAsync()
        {
            var files = SelectFilesRequested?.Invoke();
            if (files == null || files.Length == 0) return;
            await AddFilesAsync(files);
        }

        private async Task AddFilesAsync(string[] files)
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                // S4 fix: auto-save the draft before the first attachment so the
                // user's action always completes instead of dead-ending.
                if (Record.Id == 0)
                {
                    SetMessage("Saving draft before attaching…");
                    if (await SaveAsync() == null) return;
                }
                int added = 0;
                foreach (var path in files)
                {
                    try
                    {
                        AttachmentItems.Add(await _attachments.AddAsync(Record.Id, path));
                        added++;
                    }
                    catch (Exception ex)
                    {
                        SetMessage($"'{System.IO.Path.GetFileName(path)}' failed: {ex.Message}", isError: true);
                    }
                }
                if (added > 0)
                {
                    SetMessage($"{added} attachment{(added == 1 ? "" : "s")} added.");
                    OnPropertyChanged(nameof(HasAttachments));
                }
            }
            catch (Exception ex) { SetMessage("Attachment failed: " + ex.Message, isError: true); }
            finally { IsBusy = false; }
        }

        // ── Status workflow ────────────────────────────────────────────

        private async Task ChangeStatusAsync(string? next)
        {
            if (string.IsNullOrWhiteSpace(next) || Record.Id <= 0 || IsBusy) return;
            // Guard the record before a workflow transition (dirty edits belong
            // in the save, not silently in a status change).
            if (IsDirty)
            {
                SetMessage("Save your changes before changing the status.", isError: true);
                return;
            }
            if (!InquiryWorkflowService.CanTransition(Record.Status, next))
            {
                SetMessage($"Cannot move from {Record.Status} to {next}.", isError: true);
                return;
            }
            IsBusy = true;
            try
            {
                if (await _workflow.ChangeStatusAsync(Record.Id, Record.Status, next))
                {
                    Record.Status = InquiryStatuses.Normalize(next);
                    // Mirror the workflow timestamps so the stepper reflects the
                    // transition immediately (persisted by the repository).
                    if (Record.Status == InquiryStatuses.Prepared) Record.PreparedAt ??= DateTime.Now;
                    if (Record.Status == InquiryStatuses.Sent) Record.SentAt ??= DateTime.Now;
                    if (Record.Status == InquiryStatuses.Confirmed) Record.ConfirmedAt ??= DateTime.Now;
                    Record.UpdatedAt = DateTime.Now;
                    RefreshStatusProperties();
                    SetMessage($"Status changed to {Record.Status}.");
                }
            }
            catch (Exception ex) { SetMessage(ex.Message, isError: true); }
            finally { IsBusy = false; }
        }

        private void RefreshStatusProperties()
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(CanPrepare));
            OnPropertyChanged(nameof(CanSend));
            OnPropertyChanged(nameof(CanConfirm));
            OnPropertyChanged(nameof(CanHold));
            OnPropertyChanged(nameof(CanCancel));
            OnPropertyChanged(nameof(PreparedDateText));
            OnPropertyChanged(nameof(SentDateText));
            OnPropertyChanged(nameof(ConfirmedDateText));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        // ── Close ──────────────────────────────────────────────────────

        private void CloseOrDiscard()
        {
            if (IsDirty && ConfirmDiscardRequested?.Invoke() != true) return;
            CloseRequested?.Invoke(false);
        }

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsStatusError)); }
        }
        private bool _isStatusError;
        public bool IsStatusError { get => _isStatusError; private set { _isStatusError = value; OnPropertyChanged(); } }

        private void SetMessage(string text, bool isError = false)
        {
            StatusText = text;
            IsStatusError = isError;
        }
    }
}
