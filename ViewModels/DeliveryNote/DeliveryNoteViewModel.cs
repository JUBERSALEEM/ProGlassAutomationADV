using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Services.DeliveryNote.Mapper;
using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail;
using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList;
using ProGlassAutomation.ViewModels.DeliveryNote.GodView;
using ProGlassAutomation.ViewModels.Shared;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.DeliveryNote
{
    /// <summary>
    /// Shell ViewModel for the Delivery Note module.
    ///
    /// CHAT 14: Tax Invoice callbacks.
    ///
    /// CHAT 19 FINAL:
    ///   • OnOpenMasterDnRequested removed — Tab 6 removed
    ///   • OnRequestCreateDnFromMaster removed — Master DN accessed
    ///     through JO Picker flow only
    ///   • CreateDnFromJobOrderAsync updated:
    ///     When JO has balance remaining (Master DN exists with balance),
    ///     user is offered the option to create next partial DN from
    ///     Master DN balance — no need to open Master DN separately.
    ///     Flow:
    ///       a) No deliveries yet → create first DN from JO snapshot
    ///       b) All delivered → inform user, nothing to create
    ///       c) Partial balance → show Master DN balance, offer to
    ///          create next DN pre-filled with balance quantities
    /// </summary>
    public class DeliveryNoteViewModel : ViewModelBase,
        ProGlassAutomation.Agent.Context.IAgentContextAware
    {
        // Phase 4: Named handler for static event — detachable
        private Func<Task>? _dataSeededHandler;
        private bool _disposed;

        public string Title { get; } = "Delivery Notes";

        // ══════════════════════════════════════════════════════════════
        //  AGENT CONTEXT (opt-in, read-only) — see JobOrderViewModel for
        //  the reasoning. Detail view only; identifier only.
        // ══════════════════════════════════════════════════════════════
        public string AgentContextDetail
        {
            get
            {
                try
                {
                    if (!IsDetailView) return "";

                    var id = DetailVM?.DeliveryNoteId;
                    return string.IsNullOrWhiteSpace(id) ? "" : $"editing {id}";
                }
                catch { return ""; }
            }
        }
        public string Subtitle { get; } =
            "Track glass deliveries, partial shipments and balance quantities";

        // ── Tab strip keys (Workspace | God View | Activity) ─────────────
        public const string TabWorkspace = "Workspace";
        public const string TabGodView = "GodView";
        public const string TabActivity = "Activity";

        public DeliveryNoteListViewModel ListVM { get; }
        public DeliveryNoteDetailViewModel DetailVM { get; }

        /// <summary>God View tab — every delivery note in the system.</summary>
        public DeliveryNoteGodViewViewModel GodViewVM { get; }

        /// <summary>Activity tab — live audit feed for this section.</summary>
        public SectionActivityViewModel ActivityVM { get; }

        // ── Cross-module callbacks ────────────────────────────────────────

        public Func<string, Task>? OnRequestOpenTaxInvoiceForDeliveryNote
        { get; set; }
        public Func<string, Task>? OnRequestOpenTaxInvoiceForMasterFromProforma
        { get; set; }
        public Func<string, Task<string>>? OnRequestAutoCreateTaxInvoice
        { get; set; }

        // ── Navigation state ──────────────────────────────────────────────

        private bool _isListView = true;
        public bool IsListView
        {
            get => _isListView;
            set
            {
                if (_isListView == value) return;
                _isListView = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDetailView));
            }
        }

        public bool IsDetailView => !_isListView;

        // ── Tab strip state ──────────────────────────────────────────────

        private string _activeTab = TabWorkspace;

        /// <summary>Which tab is active: Workspace / GodView / Activity.</summary>
        public string ActiveTab
        {
            get => _activeTab;
            set
            {
                if (_activeTab == value) return;
                _activeTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsWorkspaceTab));
                OnPropertyChanged(nameof(IsGodViewTab));
                OnPropertyChanged(nameof(IsActivityTab));
            }
        }

        public bool IsWorkspaceTab => ActiveTab == TabWorkspace;
        public bool IsGodViewTab => ActiveTab == TabGodView;
        public bool IsActivityTab => ActiveTab == TabActivity;

        public ICommand BackToListCommand { get; }

        /// <summary>Switches tabs. Parameter = tab key constant.</summary>
        public ICommand SwitchTabCommand { get; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // Detach the static subscribers under this VM and dispose the
            // children, or they leak and re-run after the module is gone.
            // (The previous code unsubscribed DataSeeded twice — a bug — and
            // never disposed ListVM/ActivityVM.)
            try { ProGlassAutomation.Services.SampleData.SampleDataService.DataSeeded -= _dataSeededHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DeliveryNoteViewModel] Dispose: DataSeeded detach failed: " + ex.Message); }
            try { ListVM?.Dispose(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DeliveryNoteViewModel] Dispose: ListVM teardown failed: " + ex.Message); }
            try { ActivityVM?.Dispose(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DeliveryNoteViewModel] Dispose: ActivityVM teardown failed: " + ex.Message); }
            GC.SuppressFinalize(this);
        }

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public DeliveryNoteViewModel()
        {
            ListVM = new DeliveryNoteListViewModel();
                        _dataSeededHandler = async () => { try { await ListVM.LoadAsync(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); } };
            ProGlassAutomation.Services.SampleData.SampleDataService.DataSeeded += _dataSeededHandler;
            DetailVM = new DeliveryNoteDetailViewModel();

            // ── List → open real DN detail ────────────────────────────────
            ListVM.OnDeliveryNoteOpenRequested = id => AsyncUtility.SafeFireAndForget(
                () => LoadDetailAsync(id), "DeliveryNote");

            // ── List → create new DN from JO picker ──────────────────────
            // CHAT 19: Updated flow handles Master DN balance automatically
            ListVM.OnCreateDnFromJobOrder = jo => AsyncUtility.SafeFireAndForget(
                () => CreateDnFromJobOrderAsync(jo), "DeliveryNote");

            // ── Chat 14: DN List Master Invoice ───────────────────────────
            ListVM.OnRequestOpenTaxInvoiceForProforma = async piId =>
            {
                try
                {
                    await RelayMasterInvoiceRequestAsync(piId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to open Master Invoice:\n\n{ex.Message}",
                        "Navigation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };

            // ── Detail → back to list ─────────────────────────────────────
            DetailVM.OnRequestBackToList = () => AsyncUtility.SafeFireAndForget(ReturnToList, "DeliveryNote");

            // CHAT 19: After dispatch with balance remaining, user chose
            // "Yes — create next DN". Shell builds next DN from Master DN balance.
            DetailVM.OnRequestCreateDnFromMaster = masterDn => AsyncUtility.SafeFireAndForget(
                () => CreateDnFromMasterBalanceAsync(
                    new Models.JobOrder.SavedJobOrder.SavedJobOrder
                    {
                        JobOrderId = masterDn.JobOrderId,
                        SourceProformaId = masterDn.ProformaId,
                        CustomerName = masterDn.CustomerName,
                        ProjectName = masterDn.ProjectName,
                        FullSnapshotJson = "{}"
                    },
                    masterDn), "DeliveryNote");

            DetailVM.OnRequestAutoCreateTaxInvoice = async dnId =>
            {
                if (OnRequestAutoCreateTaxInvoice == null) return "";
                return await OnRequestAutoCreateTaxInvoice(dnId);
            };

            DetailVM.OnRequestOpenMasterDn = jobOrderId => AsyncUtility.SafeFireAndForget(
                () => OpenMasterForJobOrderAsync(jobOrderId), "DeliveryNote");

            ListVM.OnRequestOpenMaster = jobOrderId => AsyncUtility.SafeFireAndForget(
                () => OpenMasterForJobOrderAsync(jobOrderId), "DeliveryNote");

            // ── Chat 14: DN Detail Convert → single-DN invoice ────────────
            DetailVM.OnRequestOpenTaxInvoiceForDeliveryNote = async dnId =>
            {
                try
                {
                    await RelayConvertDnRequestAsync(dnId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to convert DN to Tax Invoice:\n\n{ex.Message}",
                        "Navigation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };

            BackToListCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(ReturnToList, "DeliveryNote"));

            // ── God View tab: row-open hands off to detail flow ───────
            GodViewVM = new DeliveryNoteGodViewViewModel();
            GodViewVM.OnOpenRequested = id =>
                AsyncUtility.SafeFireAndForget(() => LoadDetailAsync(id), "DeliveryNote");

            // ── Activity tab: live audit feed for this section ────────
            ActivityVM = new SectionActivityViewModel(
                AuditSections.DeliveryNote);

            SwitchTabCommand = new RelayCommand(param =>
                SwitchTab(param as string ?? TabWorkspace));
        }

        // ═════════════════════════════════════════════════════════════════
        //  TAB SWITCHING
        // ═════════════════════════════════════════════════════════════════

        private void SwitchTab(string tabKey)
        {
            ActiveTab = tabKey;

            switch (tabKey)
            {
                case TabGodView:
                    GodViewVM.LoadAsync();
                    break;
                case TabActivity:
                    ActivityVM.LoadAsync();
                    break;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  TAX INVOICE RELAYS
        // ═════════════════════════════════════════════════════════════════

        private async Task RelayMasterInvoiceRequestAsync(string proformaId)
        {
            if (string.IsNullOrWhiteSpace(proformaId))
            {
                MessageBox.Show(
                    "Cannot create Master Invoice — no PI reference.",
                    "No PI Reference",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (OnRequestOpenTaxInvoiceForMasterFromProforma == null)
            {
                MessageBox.Show(
                    "Tax Invoice navigation is not wired up.\n\n" +
                    "Restart the app.",
                    "Not Wired",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            await OnRequestOpenTaxInvoiceForMasterFromProforma(proformaId);
        }

        private async Task RelayConvertDnRequestAsync(string deliveryNoteId)
        {
            if (string.IsNullOrWhiteSpace(deliveryNoteId))
            {
                MessageBox.Show(
                    "Cannot convert to Tax Invoice — no DN reference.",
                    "No DN Reference",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (OnRequestOpenTaxInvoiceForDeliveryNote == null)
            {
                MessageBox.Show(
                    "Tax Invoice navigation is not wired up.\n\n" +
                    "Restart the app.",
                    "Not Wired",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            await OnRequestOpenTaxInvoiceForDeliveryNote(deliveryNoteId);
        }

        // ═════════════════════════════════════════════════════════════════
        //  SAFE ASYNC FIRE
        // ═════════════════════════════════════════════════════════════════

        

        // ═════════════════════════════════════════════════════════════════
        //  NAVIGATION
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadDetailAsync(int deliveryNoteId)
        {
            // v2 §28: switching records while edits are pending → confirm.
            if (IsDetailView &&
                !await ProGlassAutomation.Services.Safety
                    .UnsavedChangesGuard.ConfirmLeaveAsync(
                        () => DetailVM.HasUnsavedChanges,
                        () => DetailVM.TrySaveAsync(),
                        "delivery note"))
                return;

            await DetailVM.LoadAsync(deliveryNoteId);
            IsListView = false;
        }

        private async Task ReturnToList()
        {
            // v2 §28: confirm before discarding unsaved DN edits.
            if (!await ProGlassAutomation.Services.Safety
                .UnsavedChangesGuard.ConfirmLeaveAsync(
                    () => DetailVM.HasUnsavedChanges,
                    () => DetailVM.TrySaveAsync(),
                    "delivery note"))
                return;

            IsListView = true;
            await SafeLoadListAsync();
        }

        public async Task OpenDeliveryNoteAsync(int deliveryNoteId)
        {
            await DetailVM.LoadAsync(deliveryNoteId);
            IsListView = false;
            await SafeLoadListAsync();
        }

        /// <summary>
        /// Opens the Master DN created from this Job Order. Remaining
        /// balance is the default grid; the user can toggle the original
        /// full JO set on the Master itself.
        /// </summary>
        public async Task OpenMasterForJobOrderAsync(string jobOrderId)
        {
            if (App.DeliveryNotes == null || string.IsNullOrWhiteSpace(jobOrderId))
                return;

            var master = await App.DeliveryNotes
                .GetMasterDnForJobOrderAsync(jobOrderId);

            if (master == null && App.Jobs != null)
            {
                var jo = await App.Jobs.GetByJobOrderIdAsync(jobOrderId);
                if (jo != null)
                {
                    try
                    {
                        master = await Services.DeliveryNote
                            .MasterDeliveryNoteService
                            .EnsureForJobOrderAsync(jo);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            "Could not open Master DN.\n\n" + ex.Message,
                            "Master DN",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }
            }

            if (master == null)
            {
                MessageBox.Show(
                    $"No Master DN exists for '{jobOrderId}'.\n\n" +
                    "Approve the Proforma / create the Job Order first.",
                    "Master DN Not Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            await DetailVM.LoadAsync(master.Id);
            IsListView = false;
            await SafeLoadListAsync();
        }

        private async Task SafeLoadListAsync()
        {
            try
            {
                await ListVM.LoadAsync();
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[DeliveryNoteViewModel] ListVM.LoadAsync failed: {ex.Message}");
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  CHAT 19 — CREATE DN FROM JO PICKER (updated flow)
        //
        //  Three scenarios when user picks a JO:
        //
        //  A) No Master DN exists yet (brand new JO, no deliveries):
        //     → Create first DN from JO snapshot directly
        //
        //  B) Master DN exists, balance = 0 (fully delivered):
        //     → Inform user everything is delivered
        //     → Offer to view latest DN
        //
        //  C) Master DN exists, balance > 0 (partial deliveries done):
        //     → Show balance summary from Master DN
        //     → Offer to create next DN pre-filled with balance qty
        //     → User can still SELECT ALL REMAINING or enter manually
        //
        //  This replaces the old flow which only checked for Draft DN.
        //  Master DN is the single source of truth for balance tracking.
        // ═════════════════════════════════════════════════════════════════

        private async Task CreateDnFromJobOrderAsync(SavedJobOrder jobOrder)
        {
            if (App.DeliveryNotes == null) return;

            try
            {
                try
                {
                    await App.DeliveryNotes.UpdateMasterDnAsync(
                        jobOrder.JobOrderId, "");
                }
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DeliveryNoteViewModel] Master-DN clear during heal failed; display still works from heal: " + ex.Message); }

                var masterDn = await App.DeliveryNotes
                    .GetMasterDnForJobOrderAsync(jobOrder.JobOrderId);

                // ── SCENARIO A: No Master DN — first delivery ─────────────
                if (masterDn == null)
                {
                    try
                    {
                        masterDn = await Services.DeliveryNote
                            .MasterDeliveryNoteService
                            .EnsureForJobOrderAsync(jobOrder);
                    }
                    catch (Exception masterEx)
                    {
                        Services.Logging.AppLogService.Error(
                            "Could not auto-create Master DN before first delivery.",
                            masterEx,
                            new Services.Logging.LogContext
                            {
                                Module = "DeliveryNote",
                                Class = nameof(DeliveryNoteViewModel),
                                Operation = "EnsureMasterDn"
                            });
                    }

                    await CreateFirstDnAsync(jobOrder);
                    return;
                }

                // ── SCENARIO B: Fully delivered ───────────────────────────
                if (masterDn.TotalBalanceQty <= 0)
                {
                    var viewLatest = MessageBox.Show(
                        $"All quantities have been delivered for this Job Order.\n\n" +
                        $"Job Order : {jobOrder.JobOrderId}\n" +
                        $"Customer  : {jobOrder.CustomerName}\n" +
                        $"Ordered   : {masterDn.TotalOrderedQty} pcs\n" +
                        $"Delivered : {masterDn.TotalDeliveredQty} pcs\n" +
                        $"Balance   : 0 pcs\n\n" +
                        "Would you like to view the latest delivery note?",
                        "Fully Delivered",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (viewLatest == MessageBoxResult.Yes)
                    {
                        var latestDns = await App.DeliveryNotes
                            .GetByJobOrderIdAsync(jobOrder.JobOrderId);
                        var latest = latestDns
                            .OrderByDescending(d => d.SequenceNumber)
                            .FirstOrDefault();

                        if (latest != null)
                        {
                            await DetailVM.LoadAsync(latest.Id);
                            IsListView = false;
                            await SafeLoadListAsync();
                        }
                    }
                    return;
                }

                // ── SCENARIO C: Partial balance remaining ─────────────────
                // Calculate balance items for display
                var balanceItems = masterDn.Items
                    .Where(i => i.BalanceQty > 0)
                    .ToList();

                int totalBalance = balanceItems.Sum(i => i.BalanceQty);
                int totalOrdered = masterDn.TotalOrderedQty;
                int totalDelivered = masterDn.TotalDeliveredQty;

                var choice = MessageBox.Show(
                    $"This Job Order has partial deliveries remaining.\n\n" +
                    $"Job Order : {jobOrder.JobOrderId}\n" +
                    $"Customer  : {jobOrder.CustomerName}\n\n" +
                    $"  Ordered   : {totalOrdered} pcs\n" +
                    $"  Delivered : {totalDelivered} pcs\n" +
                    $"  Balance   : {totalBalance} pcs " +
                    $"({balanceItems.Count} items)\n\n" +
                    "Create a new delivery note for the remaining balance?\n\n" +
                    "  Yes    → Create next DN (pre-filled with balance)\n" +
                    "  No     → Cancel\n\n" +
                    "Tip: Use 'SELECT ALL REMAINING' in the DN to fill\n" +
                    $"all {totalBalance} pcs instantly.",
                    "Remaining Balance Found",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (choice != MessageBoxResult.Yes) return;

                await CreateDnFromMasterBalanceAsync(jobOrder, masterDn);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to create delivery note:\n\n{ex.Message}",
                    "Create Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  SCENARIO A — Create FIRST DN from JO snapshot
        //  Called when no Master DN exists yet for this JO.
        // ═════════════════════════════════════════════════════════════════

        private async Task CreateFirstDnAsync(SavedJobOrder jobOrder)
        {
            if (App.DeliveryNotes == null) return;

            // Also check for an existing Draft (edge case)
            var existingDns = await App.DeliveryNotes
                .GetByJobOrderIdAsync(jobOrder.JobOrderId);

            var existingDraft = existingDns
                .FirstOrDefault(d =>
                    string.Equals(d.Status, "Draft",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(d.Status, "Dispatching",
                        StringComparison.OrdinalIgnoreCase));

            if (existingDraft != null)
            {
                var openDraft = MessageBox.Show(
                    $"A Draft delivery note already exists:\n\n" +
                    $"   📋  {existingDraft.DeliveryNoteId}\n" +
                    $"   📦  {existingDraft.TotalOrderedQty} ordered  ·  " +
                    $"{existingDraft.TotalBalanceQty} balance\n\n" +
                    "Open the existing Draft?",
                    "Draft Already Exists",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (openDraft == MessageBoxResult.Yes)
                {
                    await DetailVM.LoadAsync(existingDraft.Id);
                    IsListView = false;
                    await SafeLoadListAsync();
                    return;
                }
            }

            // Deserialize JO snapshot
            JobOrderSnapshot? joSnapshot = null;

            if (!string.IsNullOrWhiteSpace(jobOrder.FullSnapshotJson)
                && jobOrder.FullSnapshotJson != "{}")
            {
                try
                {
                    joSnapshot = JsonSerializer.Deserialize<JobOrderSnapshot>(
                        jobOrder.FullSnapshotJson, _jsonOptions);
                }
                catch (Exception ex)
                {
                    // S-5: the user already gets a "snapshot corrupted"
                    // dialog below (joSnapshot stays null), so this is not
                    // hidden from them — but the PARSE ERROR itself was
                    // discarded, which made the corruption impossible to
                    // diagnose afterwards. Behaviour is unchanged; the
                    // cause is now recoverable from the log.
                    AppLogService.Warning(
                        $"Job Order snapshot for '{jobOrder.JobOrderId}' "
                        + $"could not be deserialized: {ex.Message}",
                        new LogContext
                        {
                            Module = "DeliveryNote",
                            Class = nameof(DeliveryNoteViewModel),
                            Operation = "CreateFirstDnAsync"
                        });
                }
            }

            if (joSnapshot == null)
            {
                MessageBox.Show(
                    $"Could not read Job Order data for " +
                    $"'{jobOrder.JobOrderId}'.\n\n" +
                    "The snapshot may be missing or corrupted.",
                    "Snapshot Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            int nextSeq = await App.DeliveryNotes
                .GetNextSequenceAsync(jobOrder.JobOrderId);

            string dnId = await App.DeliveryNotes
                .BuildDeliveryNoteIdAsync(jobOrder.JobOrderId, nextSeq);

            bool exists = await App.DeliveryNotes
                .ExistsByDeliveryNoteIdAsync(dnId);

            if (exists)
            {
                MessageBox.Show(
                    $"Delivery note '{dnId}' already exists.",
                    "Already Exists",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var dnEntity = DeliveryNoteMapper.FromJobOrder(
                jobOrder, joSnapshot, dnId, nextSeq);

            foreach (var item in dnEntity.Items)
            {
                int previouslyDelivered = await App.DeliveryNotes
                    .GetPreviouslyDeliveredQtyAsync(
                        jobOrder.JobOrderId,
                        item.SpecGroupNumber,
                        item.SerialNumber);
                item.PreviouslyDeliveredQty = previouslyDelivered;
                item.BalanceQty = Math.Max(0,
                    item.OrderedQty - previouslyDelivered - item.DeliveredQty);
            }
            dnEntity.Items = dnEntity.Items.Where(i => i.BalanceQty > 0).ToList();
            dnEntity.TotalPreviouslyDeliveredQty =
                dnEntity.Items.Sum(i => i.PreviouslyDeliveredQty);
            dnEntity.TotalBalanceQty = dnEntity.Items.Sum(i => i.BalanceQty);
            dnEntity.TotalOrderedQty = dnEntity.Items.Sum(i => i.OrderedQty);

            if (dnEntity.Items.Count == 0)
            {
                MessageBox.Show(
                    "Nothing left to deliver on this Job Order.\n\n" +
                    "Master DN already shows zero remaining balance.",
                    "Fully Delivered",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var saved = await App.DeliveryNotes.InsertAsync(dnEntity);

            await DetailVM.LoadAsync(saved.Id);
            IsListView = false;
            await SafeLoadListAsync();
        }

        // ═════════════════════════════════════════════════════════════════
        //  SCENARIO C — Create next DN from Master DN balance
        //
        //  Builds a new Draft DN pre-filled with balance quantities.
        //  PreviouslyDeliveredQty = what Master DN shows as delivered.
        //  DeliveredQty = 0 (user enters or clicks SELECT ALL REMAINING).
        //  BalanceQty = full remaining balance pcs.
        //
        //  REMINDER: SELECT ALL REMAINING fills DeliveredQty = BalanceQty
        //  for ALL items where balance > 0 (all remaining pcs at once).
        // ═════════════════════════════════════════════════════════════════

        private async Task CreateDnFromMasterBalanceAsync(
            SavedJobOrder jobOrder,
            Models.DeliveryNote.SavedDeliveryNote.SavedDeliveryNote masterDn)
        {
            if (App.DeliveryNotes == null) return;

            try
            {
                await App.DeliveryNotes.UpdateMasterDnAsync(
                    jobOrder.JobOrderId, masterDn.DeliveryNoteId);
                masterDn = await App.DeliveryNotes
                    .GetMasterDnForJobOrderAsync(jobOrder.JobOrderId)
                    ?? masterDn;
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DeliveryNoteViewModel] Master-DN persist denied; using in-memory Master: " + ex.Message); }

            int nextSeq = await App.DeliveryNotes
                .GetNextSequenceAsync(jobOrder.JobOrderId);

            string dnId = await App.DeliveryNotes
                .BuildDeliveryNoteIdAsync(jobOrder.JobOrderId, nextSeq);

            bool exists = await App.DeliveryNotes
                .ExistsByDeliveryNoteIdAsync(dnId);

            if (exists)
            {
                MessageBox.Show(
                    $"Delivery note '{dnId}' already exists.\n\n" +
                    "Please refresh and try again.",
                    "Already Exists",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var now = DateTime.Now;

            var newDn =
                new Models.DeliveryNote.SavedDeliveryNote.SavedDeliveryNote
                {
                    DeliveryNoteId = dnId,
                    JobOrderId = masterDn.JobOrderId,
                    ProformaId = masterDn.ProformaId,
                    SequenceNumber = nextSeq,
                    CustomerName = masterDn.CustomerName,
                    ProjectName = masterDn.ProjectName,
                    Status = "Dispatching",
                    DeliveryType = "Original",
                    IsMasterDn = false,
                    IsFoc = false,
                    DriverName = "",
                    VehicleNumber = "",
                    ReceiverName = "",
                    DeliveryDate = now,
                    DeliveryTime = now.ToString("HH:mm"),
                    Notes = "",
                    FullSnapshotJson = "{}",
                    CreatedAt = now,
                    UpdatedAt = now
                };

            // Build items from Master DN balance items only
            // PreviouslyDeliveredQty = already delivered total
            // DeliveredQty = 0 → user fills or clicks SELECT ALL REMAINING
            // BalanceQty = full remaining balance pcs
            var items = new System.Collections.Generic.List<
                Models.DeliveryNote.SavedDeliveryNote.DeliveryNoteItem>();

            foreach (var masterItem in masterDn.Items
                .Where(i => i.BalanceQty > 0)
                .OrderBy(i => i.SpecGroupNumber)
                .ThenBy(i => i.SerialNumber))
            {
                items.Add(
                    new Models.DeliveryNote.SavedDeliveryNote.DeliveryNoteItem
                    {
                        DeliveryNoteId = dnId,
                        JobOrderId = masterDn.JobOrderId,
                        SpecGroupNumber = masterItem.SpecGroupNumber,
                        SpecGroupName = masterItem.SpecGroupName,
                        SerialNumber = masterItem.SerialNumber,
                        GlassRef = masterItem.GlassRef,
                        W1 = masterItem.W1,
                        H1 = masterItem.H1,
                        W2 = masterItem.W2,
                        H2 = masterItem.H2,
                        OrderedQty = masterItem.OrderedQty,
                        PreviouslyDeliveredQty =
                            masterItem.PreviouslyDeliveredQty,
                        DeliveredQty = 0,
                        BalanceQty = masterItem.BalanceQty,
                        ReworkQty = 0,
                        ReturnedQty = 0,
                        Notes = ""
                    });
            }

            if (items.Count == 0)
            {
                MessageBox.Show(
                    "Master DN has no remaining balance to deliver.",
                    "Nothing to Create",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            newDn.Items = items;
            newDn.TotalOrderedQty = items.Sum(i => i.OrderedQty);
            newDn.TotalPreviouslyDeliveredQty =
                items.Sum(i => i.PreviouslyDeliveredQty);
            newDn.TotalDeliveredQty = 0;
            newDn.TotalBalanceQty = items.Sum(i => i.BalanceQty);

            // Refresh snapshot from Master DN snapshot context
            var masterSnapshot = DeliveryNoteMapper
                .DeserializeSnapshot(masterDn);
            DeliveryNoteMapper.RefreshSnapshot(
                newDn, masterSnapshot?.JobOrderRef);

            var saved = await App.DeliveryNotes.InsertAsync(newDn);

            await DetailVM.LoadAsync(saved.Id);
            IsListView = false;
            await SafeLoadListAsync();
        }
    }
}