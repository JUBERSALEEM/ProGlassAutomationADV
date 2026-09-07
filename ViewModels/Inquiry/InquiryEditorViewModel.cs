using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
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
        private readonly IMasterDataRepository? _masterData;
        // Full list of saved clients (read once at load). We keep this around so
        // we can auto-fill CustomerLocation when a known customer is picked,
        // and we can also write a new / updated client back to master data
        // when the user saves the inquiry. Empty when the master-data service
        // is unavailable — auto-fill is silently skipped in that case.
        private List<SavedClient> _savedClients = new();
        // Tracks whether the user has manually edited CustomerLocation so we
        // don't stomp their value when they type a customer name that has a
        // different address in master data.
        private bool _locationManuallyEdited;
        // Tracks whether the user has manually edited SalesmanName so the
        // auto-suggest flow doesn't keep fighting the user's typing.
        private bool _salesmanManuallyEdited;
        // Set by the window's OnEditorClosed when the editor is dismissed.
        // The fire-and-forget LoadMastersAsync / LoadAttachmentsAsync tasks
        // check this before mutating VM state, so they short-circuit when
        // the window is gone. Without this, the Task's captured `this`
        // keeps the VM alive past the window's lifetime (minor leak) and
        // the tasks can land their results in a UI thread that's already
        // torn down.
        private bool _isDisposed;

        public InquiryRecord Record { get; }
        public bool IsNewRecord => Record.Id == 0;
        public string HeaderTitle => IsNewRecord ? "New inquiry" : Record.InquiryNumber;
        public string SaveCommandText => IsNewRecord ? "Save inquiry" : "Save changes";
        public string HeaderSubtitle => IsNewRecord
            ? "Capture the commercial intake — attachments can be added right away"
            : $"Received {Record.ReceivedAt:dd MMM yyyy} · last updated {Record.UpdatedAt:dd MMM yyyy}";

        public ObservableCollection<InquiryAttachment> AttachmentItems { get; } = new();
        public bool HasAttachments => AttachmentItems.Count > 0;

        // ── PHASE 2: Scope of supply (BOQ) ──────────────────────────────
        // Editable rows — what the customer actually asked for, dimension-
        // first (like the PI grid). ConvertToPi imports these into Step 5
        // spec groups so quoted shapes are never re-typed by the estimator.
        public ObservableCollection<InquiryLineItemRow> LineItems { get; } = new();
        public bool HasLineItems => LineItems.Count > 0;
        /// <summary>Glass builds allowed in BOQ rows — mirrors PI builds.</summary>
        public static IReadOnlyList<string> BoqGlassTypes { get; } =
            new[] { "SGU", "DGU", "LAM", "DGU+LAM" };

        public ICommand AddLineItemCommand { get; }
        public ICommand RemoveLineItemCommand { get; }

        private void AddLineItem()
        {
            var row = new InquiryLineItemRow();
            row.DirtyChanged += OnLineItemDirtyChanged;
            LineItems.Add(row);
            IsDirty = true;
            OnPropertyChanged(nameof(HasLineItems));
            OnPropertyChanged(nameof(CanSave));
            ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
        }

        private void RemoveLineItem(object? parameter)
        {
            if (parameter is not InquiryLineItemRow row) return;
            row.DirtyChanged -= OnLineItemDirtyChanged;
            LineItems.Remove(row);
            IsDirty = true;
            OnPropertyChanged(nameof(HasLineItems));
            OnPropertyChanged(nameof(CanSave));
            ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
        }

        private void OnLineItemDirtyChanged(object? sender, EventArgs e)
        {
            IsDirty = true;
            OnPropertyChanged(nameof(CanSave));
            ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
        }

        public Func<string[]?>? SelectFilesRequested { get; set; }
        public Func<bool>? ConfirmDiscardRequested { get; set; }
        public Func<InquiryAttachment, bool>? ConfirmDeleteRequested { get; set; }
        public Action<InquiryAttachment>? PreviewRequested { get; set; }
        // Right-click context-menu callbacks. The view wires these so the
        // VM stays MessageBox-free. Each defaults to a no-op if the view
        // never sets it (the menu items just no-op).
        public Action? OpenMasterDataRequested { get; set; }
        public Action? ViewCustomerHistoryRequested { get; set; }
        // Module-level navigation callback for the right-click menu's
        // "Open in master data" / "View customer history" items. Wired by
        // MainViewModel to the shell's NavigateFromAgent so the editor
        // can switch modules without holding a hard reference to it.
        public Func<string, Task>? NavigateRequested { get; set; }

        public ObservableCollection<string> Priorities { get; } = new() { "Urgent", "High", "Normal", "Low" };
        public ObservableCollection<string> Salesmen { get; } = new();
        public ObservableCollection<string> Customers { get; } = new();
        // Distinct saved addresses for the current customer. The bound ComboBox
        // shows them as suggestions but the user can still type a new value.
        // Refreshed whenever the customer name changes.
        public ObservableCollection<string> CustomerLocations { get; } = new();

        // ── Form fields (each setter marks the form dirty) ─────────────

        private string _reference = "";
        public string Reference { get => _reference; set => SetField(ref _reference, value); }
        private string _customerName = "";
        public string CustomerName
        {
            get => _customerName;
            set
            {
                if (!SetField(ref _customerName, value)) return;
                // Refresh the location suggestions for the new customer name.
                RefreshCustomerLocations();
                // Auto-fill the location the first time the user picks a known
                // customer — but never clobber a value they typed themselves.
                if (!_locationManuallyEdited)
                {
                    var match = FindClientByName(_customerName);
                    if (match != null && !string.IsNullOrWhiteSpace(match.BillingAddress))
                    {
                        SetLocation(match.BillingAddress, markManual: false);
                    }
                }
            }
        }
        private string _customerLocation = "";
        public string CustomerLocation
        {
            get => _customerLocation;
            set
            {
                if (!SetField(ref _customerLocation, value)) return;
                // Any time the user touches the location box, mark it manually
                // edited so future customer-name auto-fills don't overwrite it.
                _locationManuallyEdited = !string.IsNullOrWhiteSpace(_customerLocation)
                    || _locationManuallyEdited;
            }
        }
        private string _projectName = "";
        public string ProjectName { get => _projectName; set => SetField(ref _projectName, value); }
        private string _projectDescription = "";
        public string ProjectDescription { get => _projectDescription; set => SetField(ref _projectDescription, value); }
        private string _salesmanName = "";
        public string SalesmanName
        {
            get => _salesmanName;
            set
            {
                if (SetField(ref _salesmanName, value))
                {
                    // Track manual edits so the master-data auto-add only
                    // fires for genuinely new names (not while the user is
                    // still typing one that's already in the list).
                    if (!string.IsNullOrWhiteSpace(_salesmanName)
                        && !Salesmen.Any(s => string.Equals(s, _salesmanName, StringComparison.OrdinalIgnoreCase)))
                    {
                        _salesmanManuallyEdited = true;
                    }
                }
            }
        }
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
        public bool HasValueError
        {
            get
            {
                var text = _valueText.Trim();
                if (text.Length == 0) return false;
                if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value))
                    return true;
                return value < 0m || value > InquiryValidationRules.MaxInquiryValue;
            }
        }
        public string ValueHint => HasValueError
            ? $"Enter an AED amount from 0 to {InquiryValidationRules.MaxInquiryValue:N0}."
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
        // Right-click context-menu commands on editor fields.
        public ICommand ClearCustomerCommand { get; }
        public ICommand OpenCustomerInMasterDataCommand { get; }
        public ICommand ViewCustomerHistoryCommand { get; }

        /// <summary>Raised with true when the record was saved and the editor should close.</summary>
        public event Action<bool>? CloseRequested;

        public InquiryEditorViewModel(IInquiryWorkflowService workflow,
            InquiryAttachmentService attachments, InquiryRecord? source = null,
            IMasterDataRepository? masterData = null)
        {
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
            _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
            // Master data is optional. The caller may inject a test fake via
            // the `masterData` parameter, otherwise we use the App.* static
            // singleton that's already initialised at app startup. The
            // dropdowns work either way — auto-fill is silently skipped when
            // master data is unavailable.
            _masterData = masterData!;
            Record = source ?? new InquiryRecord
            {
                InquiryReference = "Customer Inquiry", Status = InquiryStatuses.Received,
                Priority = "Normal", ReceivedAt = DateTime.Now
            };
            _reference = Record.InquiryReference;
            _customerName = Record.CustomerName;
            _customerLocation = Record.CustomerLocation;
            _locationManuallyEdited = !string.IsNullOrWhiteSpace(_customerLocation);
            _projectName = Record.ProjectName;
            _projectDescription = Record.ProjectDescription;
            _salesmanName = Record.SalesmanName;
            _salesmanManuallyEdited = !string.IsNullOrWhiteSpace(_salesmanName);
            _notes = Record.Notes;
            _priority = string.IsNullOrWhiteSpace(Record.Priority) ? "Normal" : Record.Priority;
            _receivedDate = Record.ReceivedAt == default ? DateTime.Today : Record.ReceivedAt.Date;
            _receivedTime = Record.ReceivedAt == default ? DateTime.Now.TimeOfDay : Record.ReceivedAt.TimeOfDay;
            _valueText = Record.InquiryValue == 0 ? "" : Record.InquiryValue.ToString("N2", CultureInfo.CurrentCulture);
            _isDirty = false;

            // PHASE 2: hydrate the scope of supply. Detail loads Include
            // LineItems; a fresh record starts empty. Hydration must NOT
            // mark the form dirty — the event is wired after construction.
            foreach (var item in (Record.LineItems ?? new List<InquiryLineItem>())
                     .OrderBy(x => x.SortOrder))
            {
                LineItems.Add(InquiryLineItemRow.FromEntity(item));
            }
            foreach (var row in LineItems)
                row.DirtyChanged += OnLineItemDirtyChanged;

            AddLineItemCommand = new RelayCommand(_ => AddLineItem(), _ => !IsBusy);
            RemoveLineItemCommand = new RelayCommand(p => RemoveLineItem(p), _ => !IsBusy);

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
                    catch (Exception ex) { SetMessage("Delete failed: " + ExceptionMessages.Deep(ex), isError: true); }
                    finally { IsBusy = false; }
                }
            }, _ => !IsBusy);
            PreviewAttachmentCommand = new RelayCommand(p =>
            {
                if (p is InquiryAttachment file) PreviewRequested?.Invoke(file);
            });
            CancelCommand = new RelayCommand(_ => CloseOrDiscard());

            // ── Right-click field commands ──────────────────────────────
            ClearCustomerCommand = new RelayCommand(_ =>
            {
                CustomerName = "";
                CustomerLocation = "";
                _locationManuallyEdited = false;
            }, _ => !string.IsNullOrWhiteSpace(CustomerName) || !string.IsNullOrWhiteSpace(CustomerLocation));
            OpenCustomerInMasterDataCommand = new RelayCommand(_ =>
            {
                if (NavigateRequested != null)
                    AsyncUtility.SafeFireAndForget(() => NavigateRequested("CompanyCenter"), "Open master data from inquiry");
                else OpenMasterDataRequested?.Invoke();
            }, _ => !string.IsNullOrWhiteSpace(CustomerName));
            ViewCustomerHistoryCommand = new RelayCommand(_ =>
            {
                // "View customer history" is the same shell action for now:
                // jump to the Company Center which lists every saved client
                // (the inquiry's own history is already shown in the rail).
                if (NavigateRequested != null)
                    AsyncUtility.SafeFireAndForget(() => NavigateRequested("CompanyCenter"), "View customer history from inquiry");
                else ViewCustomerHistoryRequested?.Invoke();
            }, _ => !string.IsNullOrWhiteSpace(CustomerName));

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
            && !HasValueError
            && InquiryValidationRules.ValidateLineItems(
                LineItems.Select(r => r.ToEntity()).ToList()).Count == 0;

        // Customer-context-menu gate: true when the typed customer exists in
        // master data (so "Open in master data" is a real action, not a
        // stub) and when we have a customer name to act on.
        public bool CanOpenCustomerInMasterData =>
            !string.IsNullOrWhiteSpace(CustomerName)
            && FindClientByName(CustomerName) != null;
        public bool CanViewCustomerHistory => !string.IsNullOrWhiteSpace(CustomerName);

        private bool SetField<T>(ref T field, T value,
            [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName ?? "");
            IsDirty = true;
            OnPropertyChanged(nameof(CanSave));
            // Customer-context-menu gates depend on CustomerName. We
            // notify them on every field change so the menu re-evaluates
            // its IsEnabled state correctly. (Cheap — these are
            // computed-property reads, not DB calls.)
            if (propertyName == nameof(CustomerName))
            {
                OnPropertyChanged(nameof(CanOpenCustomerInMasterData));
                OnPropertyChanged(nameof(CanViewCustomerHistory));
            }
            // Force a re-evaluation of every Command's CanExecute. Without
            // this the Save button only updates when the user moves focus
            // (WPF's default requery cadence is "sometimes"). Every field
            // change can flip CanSave, so we explicitly invalidate.
            ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
            return true;
        }

        private async Task LoadMastersAsync()
        {
            // Short-circuit if the editor was closed while we were
            // pending in the DI queue. The captured `this` would
            // otherwise keep the VM alive past the window's lifetime.
            if (_isDisposed) return;
            if (_masterData == null) return;
            try
            {
                // Pull the full client records (not just names) so we can
                // auto-fill the location and upsert the client on save.
                _savedClients = await _masterData.GetAllClientsAsync();
                if (_isDisposed) return;
                foreach (var x in _savedClients)
                    if (!string.IsNullOrWhiteSpace(x.CustomerName) && !Customers.Contains(x.CustomerName))
                        Customers.Add(x.CustomerName);
                var salesmen = await _masterData.GetAllSalesmenAsync();
                if (_isDisposed) return;
                foreach (var x in salesmen)
                    if (!string.IsNullOrWhiteSpace(x.Name) && !Salesmen.Contains(x.Name))
                        Salesmen.Add(x.Name);
                // Populate the location dropdown with whatever we have for the
                // currently-selected customer (may be empty for new records).
                RefreshCustomerLocations();
                // Master data just arrived — the right-click menu's
                // "Open in master data" / "View customer history" gates
                // depend on this list, so re-notify them. (CustomerName
                // alone isn't enough: a customer typed before masters
                // loaded would show IsEnabled=false forever otherwise.)
                OnPropertyChanged(nameof(CanOpenCustomerInMasterData));
                OnPropertyChanged(nameof(CanViewCustomerHistory));
                ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
            }
            catch (Exception ex) { SetMessage("Master data unavailable: " + ExceptionMessages.Deep(ex), isError: true); }
        }

        // ── Master-data helpers ───────────────────────────────────────

        /// <summary>
        /// Returns the most-recently-used saved client whose name matches the
        /// supplied value (case-insensitive). Null when the customer isn't
        /// known to master data or master data is unavailable.
        /// </summary>
        private SavedClient? FindClientByName(string? name)
        {
            if (_masterData == null || string.IsNullOrWhiteSpace(name)) return null;
            return _savedClients.FirstOrDefault(c =>
                string.Equals(c.CustomerName, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Rebuilds the location-suggestion list for the currently-typed
        /// customer name. We surface every distinct billing address we've
        /// ever seen for that customer (so the user can pick "Site A" or
        /// "Site B" rather than re-typing). Empty when the customer is
        /// unknown.
        /// </summary>
        private void RefreshCustomerLocations()
        {
            CustomerLocations.Clear();
            if (_masterData == null || string.IsNullOrWhiteSpace(_customerName)) return;
            var addresses = _savedClients
                .Where(c => string.Equals(c.CustomerName, _customerName, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.BillingAddress)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var a in addresses) CustomerLocations.Add(a);
            // Make sure the currently-displayed location still shows up in the
            // dropdown even if master data doesn't list it.
            if (!string.IsNullOrWhiteSpace(_customerLocation)
                && !CustomerLocations.Contains(_customerLocation))
            {
                CustomerLocations.Insert(0, _customerLocation);
            }
        }

        /// <summary>
        /// Internal setter for CustomerLocation that lets us bypass the
        /// "mark as manually edited" side-effect when we're auto-filling
        /// from master data.
        /// </summary>
        private void SetLocation(string value, bool markManual)
        {
            if (_customerLocation == value) return;
            _customerLocation = value;
            OnPropertyChanged(nameof(CustomerLocation));
            if (markManual) _locationManuallyEdited = true;
            IsDirty = true;
            OnPropertyChanged(nameof(CanSave));
        }

        private async Task LoadAttachmentsAsync()
        {
            if (_isDisposed) return;
            if (Record.Id <= 0) return;
            try
            {
                // Manifest-backed (PI Step 8 pattern) — no EF read, immune to
                // legacy child-table drift; legacy rows migrate on first call.
                var rows = await _attachments.GetAsync(Record.Id);
                if (_isDisposed) return;
                AttachmentItems.Clear();
                foreach (var x in rows) AttachmentItems.Add(x);
                OnPropertyChanged(nameof(HasAttachments));
            }
            catch (Exception ex) { SetMessage("Attachments unavailable: " + ExceptionMessages.Deep(ex), isError: true); }
        }

        /// <summary>
        /// Called by the window's OnEditorClosed. Marks the VM as disposed
        /// so the fire-and-forget loaders short-circuit on their next
        /// await, releases the loaded-clients cache (no other consumer),
        /// and lets the GC collect the VM + the closures that captured
        /// `this`. Without this, the loaders' captured `this` keeps the
        /// VM alive for the full app lifetime.
        /// </summary>
        public void CancelLoaders()
        {
            _isDisposed = true;
            // The Task closures may still complete and try to mutate
            // ObservableCollections on the UI thread. The _isDisposed
            // check inside each loader handles that; the assignment
            // above is enough.
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
            // PHASE 2: build the scope-of-supply entities once, then run the
            // shared industrial gate. The button CanSave uses the same rules,
            // but SaveAsync is the non-bypassable boundary for shortcuts,
            // attachment auto-save, agent actions and tests.
            var boqToPersist = LineItems.Select(r => r.ToEntity()).ToList();
            var blockingIssues = InquiryValidationRules.ValidateForSave(Record, boqToPersist);
            if (blockingIssues.Count > 0)
            {
                SetMessage(string.Join(" ", blockingIssues.Take(3)), isError: true);
                return null;
            }
            // A fresh record must go to CreateAsync HEADER-ONLY: EF would
            // otherwise graph-insert the rows via the navigation, and the
            // replace-all call below would then re-write them (doubling
            // the statements and losing nothing — but doing it twice).
            var wasNewRecord = Record.Id == 0;
            Record.LineItems = new List<InquiryLineItem>();
            // Gates — scope persistence must never break a header save:
            //   • roles without InquiryEdit can fill the scope but their
            //     save contract stays "header only" (same as before this
            //     feature) rather than the whole save erroring out
            bool persistBoq = App.Inquiries != null
                && (boqToPersist.Count > 0 || !wasNewRecord)
                && Services.Audit.PermissionService.Can(
                    Services.Audit.AppPermissions.InquiryEdit);
            var saved = wasNewRecord
                ? await _workflow.CreateAsync(Record)
                : await _workflow.UpdateAsync(Record);
            Record.LineItems = boqToPersist;
            if (persistBoq)
            {
                await App.Inquiries!.SaveLineItemsAsync(
                    saved?.Id ?? Record.Id,
                    boqToPersist,
                    Services.Audit.AuditService.CurrentUser?.DisplayName ?? "").ConfigureAwait(false);
            }
            else if (boqToPersist.Count > 0)
            {
                Services.Logging.AppLogService.Debug(
                    "[InquiryEditor] scope of supply not persisted — " +
                    "repository unavailable or InquiryEdit permission missing.");
            }
            // After a successful save, mirror the new/updated client + salesman
            // back to master data so the next inquiry (and the dropdowns on
            // any open editor) see them. Failures here are non-fatal: the
            // inquiry is already saved, the dropdowns just won't be richer.
            await TryUpsertMasterDataAsync().ConfigureAwait(false);
            IsDirty = false;
            OnPropertyChanged(nameof(IsNewRecord));
            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderSubtitle));
            OnPropertyChanged(nameof(SaveCommandText));
            return saved;
        }

        /// <summary>
        /// Best-effort write-back of the new customer location and new
        /// salesman name to the master-data database. Only fires for values
        /// the user typed themselves (i.e. the auto-fill didn't push them
        /// in from existing master data). All failures are swallowed — the
        /// inquiry save itself already succeeded.
        /// </summary>
        private async Task TryUpsertMasterDataAsync()
        {
            if (_masterData == null) return;
            try
            {
                // ── Customer / location ──
                if (!string.IsNullOrWhiteSpace(Record.CustomerName))
                {
                    var existing = _savedClients.FirstOrDefault(c =>
                        string.Equals(c.CustomerName, Record.CustomerName, StringComparison.OrdinalIgnoreCase));
                    string existingAddress = existing?.BillingAddress ?? "";
                    if (existing == null)
                    {
                        // Brand new customer — create a SavedClient with whatever
                        // location the user supplied.
                        var created = await _masterData.SaveOrUpdateClientAsync(new SavedClient
                        {
                            CustomerName = Record.CustomerName,
                            BillingAddress = Record.CustomerLocation ?? "",
                            CreatedAt = DateTime.Now,
                            LastUsedAt = DateTime.Now
                        }).ConfigureAwait(false);
                        _savedClients.Add(created);
                        if (!Customers.Contains(created.CustomerName)) Customers.Add(created.CustomerName);
                    }
                    else if (!string.IsNullOrWhiteSpace(Record.CustomerLocation)
                        && !string.Equals(existingAddress, Record.CustomerLocation, StringComparison.OrdinalIgnoreCase))
                    {
                        // Known customer, new/updated address — update master data
                        // so the next inquiry auto-fills this address.
                        existing.BillingAddress = Record.CustomerLocation;
                        existing.LastUsedAt = DateTime.Now;
                        await _masterData.SaveOrUpdateClientAsync(existing).ConfigureAwait(false);
                    }
                }
                // ── Salesman ──
                if (!string.IsNullOrWhiteSpace(Record.SalesmanName)
                    && !Salesmen.Any(s => string.Equals(s, Record.SalesmanName, StringComparison.OrdinalIgnoreCase)))
                {
                    var sm = await _masterData.SaveOrUpdateSalesmanAsync(new SavedSalesman
                    {
                        Name = Record.SalesmanName,
                        CreatedAt = DateTime.Now,
                        LastUsedAt = DateTime.Now
                    }).ConfigureAwait(false);
                    Salesmen.Add(sm.Name);
                }
            }
            catch (Exception ex)
            {
                // master-data write-back is best-effort. The inquiry is already
                // saved; a failure here should not roll that back. But it
                // should be visible: log to Debug so an attached debugger
                // or the in-app log panel sees the exception type and
                // message. The end-user sees nothing because the master-
                // data write isn't part of the inquiry-save contract.
                System.Diagnostics.Debug.WriteLine(
                    $"TryUpsertMasterDataAsync: master-data write-back failed: {ex}");
            }
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
            catch (Exception ex) { SetMessage(ExceptionMessages.Deep(ex), isError: true); }
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
                        SetMessage($"'{System.IO.Path.GetFileName(path)}' failed: {ExceptionMessages.Deep(ex)}", isError: true);
                    }
                }
                if (added > 0)
                {
                    SetMessage($"{added} attachment{(added == 1 ? "" : "s")} added.");
                    OnPropertyChanged(nameof(HasAttachments));
                }
            }
            catch (Exception ex) { SetMessage("Attachment failed: " + ExceptionMessages.Deep(ex), isError: true); }
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
            catch (Exception ex) { SetMessage(ExceptionMessages.Deep(ex), isError: true); }
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
            ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
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

    /// <summary>
    /// PHASE 2 — one editable scope-of-supply row in the inquiry editor.
    /// INPC wrapper around the persistence entity so grid edits mark the
    /// form dirty AND re-render (the entity itself is deliberately plain
    /// for EF use). Raised <see cref="DirtyChanged"/> on any field edit;
    /// hydration via <see cref="FromEntity"/> never raises it.
    /// </summary>
    public sealed class InquiryLineItemRow : ViewModelBase
    {
        /// <summary>Raised when any editable field changes.</summary>
        public event EventHandler? DirtyChanged;

        public static InquiryLineItemRow FromEntity(InquiryLineItem e) => new()
        {
            GlassType = e.GlassType,
            SpecName = e.SpecName,
            OuterGlassColor = e.OuterGlassColor,
            InnerGlassColor = e.InnerGlassColor,
            WidthMm = e.WidthMm,
            HeightMm = e.HeightMm,
            Qty = e.Qty,
            RatePerSqm = e.RatePerSqm,
            Notes = e.Notes
        };

        public InquiryLineItem ToEntity() => new()
        {
            GlassType = (GlassType ?? "").Trim(),
            SpecName = (SpecName ?? "").Trim(),
            OuterGlassColor = (OuterGlassColor ?? "").Trim(),
            InnerGlassColor = (InnerGlassColor ?? "").Trim(),
            WidthMm = WidthMm,
            HeightMm = HeightMm,
            Qty = Qty,
            RatePerSqm = RatePerSqm,
            Notes = Notes ?? ""
        };

        /// <summary>True when the row can produce a PI grid line.</summary>
        public bool IsImportable =>
            !string.IsNullOrWhiteSpace(SpecName) && WidthMm > 0 && HeightMm > 0 && Qty > 0;

        private string _glassType = "SGU";
        public string GlassType
        {
            get => _glassType;
            set { if (_glassType == value) return; _glassType = value; Notify(); }
        }

        private string _specName = "";
        public string SpecName
        {
            get => _specName;
            set { if (_specName == value) return; _specName = value; OnPropertyChanged(nameof(IsImportable)); Notify(); }
        }

        private string _outerGlassColor = "";
        public string OuterGlassColor
        {
            get => _outerGlassColor;
            set { if (_outerGlassColor == value) return; _outerGlassColor = value; Notify(); }
        }

        private string _innerGlassColor = "";
        public string InnerGlassColor
        {
            get => _innerGlassColor;
            set { if (_innerGlassColor == value) return; _innerGlassColor = value; Notify(); }
        }

        private int _widthMm;
        public int WidthMm
        {
            get => _widthMm;
            set { if (_widthMm == value) return; _widthMm = value; OnPropertyChanged(nameof(IsImportable)); Notify(); }
        }

        private int _heightMm;
        public int HeightMm
        {
            get => _heightMm;
            set { if (_heightMm == value) return; _heightMm = value; OnPropertyChanged(nameof(IsImportable)); Notify(); }
        }

        private int _qty = 1;
        public int Qty
        {
            get => _qty;
            set { if (_qty == value) return; _qty = value; OnPropertyChanged(nameof(IsImportable)); Notify(); }
        }

        private decimal _ratePerSqm;
        public decimal RatePerSqm
        {
            get => _ratePerSqm;
            set { if (_ratePerSqm == value) return; _ratePerSqm = value; Notify(); }
        }

        private string _notes = "";
        public string Notes
        {
            get => _notes;
            set { if (_notes == value) return; _notes = value; OnPropertyChanged(); DirtyChanged?.Invoke(this, EventArgs.Empty); }
        }

        private void Notify([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        {
            OnPropertyChanged(name ?? "");
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
