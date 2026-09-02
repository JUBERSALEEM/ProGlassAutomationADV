// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Proforma Invoice — Wizard Shell (Phase 3: Async Cleanup)
// VERSION: Phase 3 — 2026-08-21 — Removed duplicate SafeFireAsync, uses central AsyncUtility
//
// CHANGES vs Phase 2:
//   ✓ Removed private static async void SafeFireAsync (7 duplicates across app)
//   ✓ All call sites now use Utilities.AsyncUtility.SafeFireAndForget (single async void)
//   ✓ CorrelationId auto-flow via CorrelationContext
//   ✓ No .Result / .Wait() — all await
//   ✓ VSTHRD100 compliant: only event handlers + AsyncUtility have async void
//
// PRESERVED: All 9 wizard steps, S-01/S-02 swap, BUG-77/78/79/80/81, approval flow
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Step7PrintPreview.TermsAndConditions;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.ProformaInvoice
{
    public class ProformaInvoiceViewModel : ViewModelBase,
        ProGlassAutomation.Agent.Context.IAgentContextAware,
        IDisposable
    {
        public string Title { get; private set; } = "Proforma Invoice Builder";

        public string AgentContextDetail
        {
            get
            {
                try
                {
                    var id = Step1VM?.ProformaId;
                    return string.IsNullOrWhiteSpace(id) ? "" : $"editing {id}";
                }
                catch { return ""; }
            }
        }
        public string Subtitle { get; private set; } = "Nine-station industrial quotation deck";

        public const int TotalSteps = 9;

        public ObservableCollection<InvoiceItem> Invoices { get; }
            = new ObservableCollection<InvoiceItem>();

        public decimal TotalSales => 0m;
        public int InvoiceCount => 0;
        public int PendingCount => 0;
        public bool HasInvoices => false;

        public Step1CompanySetup.CompanySetupViewModel Step1VM { get; private set; }
        public Step2ClientProfile.ClientProfileViewModel Step2VM { get; private set; }
        public Step3ProjectSpecs.ProjectSpecsViewModel Step3VM { get; private set; }
        public Step4PricingEstimator.PricingEstimatorViewModel Step4VM { get; private set; }
        public Step5DataGrid.DataGridViewModel Step5VM { get; private set; }
        public Step6OtherCharges.OtherChargesViewModel Step6VM { get; private set; }
        public Step7PrintPreview.PrintPreviewViewModel Step7VM { get; private set; }
        public Step8Attachments.AttachmentsStepViewModel Step8VM { get; private set; }
        public Step9ProfitLoss.ProfitLossStepViewModel Step9VM { get; private set; }

        public ObservableCollection<WizardStep> WizardSteps { get; private set; }

        private WizardStep _currentStep;
        private int _furthestStepReached = 1;
        private int? _pendingApprovalInvoiceId;

        public WizardStep CurrentStep
        {
            get => _currentStep;
            set
            {
                if (_currentStep == value) return;
                if (_currentStep != null)
                    _currentStep.IsActive = false;
                _currentStep = value;
                if (_currentStep != null)
                    _currentStep.IsActive = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentStepNumber));
                OnPropertyChanged(nameof(CurrentStationCode));
                OnPropertyChanged(nameof(CurrentStationTitle));
                OnPropertyChanged(nameof(CurrentStationSubtitle));
                OnPropertyChanged(nameof(StepProgressText));
                OnPropertyChanged(nameof(ProgressPercent));
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(IsLastStep));
                ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
            }
        }

        public int CurrentStepNumber => CurrentStep?.StepNumber ?? 1;
        public string CurrentStationCode => CurrentStepNumber.ToString("00");
        public string CurrentStationTitle => CurrentStep?.Title ?? "";
        public string CurrentStationSubtitle => CurrentStep?.Subtitle ?? "";
        public string StepProgressText =>
            $"STATION  {CurrentStepNumber:00}  /  {TotalSteps:00}";
        public double ProgressPercent =>
            (double)CurrentStepNumber / TotalSteps;
        public bool CanGoBack => CurrentStepNumber > 1;
        public bool CanGoNext => CurrentStepNumber < TotalSteps;
        public bool IsLastStep => CurrentStepNumber == TotalSteps;

        private bool _isListView = true;
        public bool IsListView
        {
            get => _isListView;
            set
            {
                if (_isListView == value) return;
                _isListView = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsWizardView));
            }
        }

        public bool IsWizardView => !_isListView;
        public SavedInvoicesList.SavedInvoicesListViewModel SavedInvoicesListVM { get; private set; }

        public ICommand NextStepCommand { get; private set; }
        public ICommand PreviousStepCommand { get; private set; }
        public ICommand GoToStepCommand { get; private set; }
        public ICommand NewInvoiceCommand { get; private set; }
        public ICommand BackToListCommand { get; private set; }
        public ICommand NewFromInquiryCommand { get; private set; }

        // ── Cross-module callbacks (wired by MainViewModel) ─────────────
        // Ask the host to show the user a 3-way picker for how a "new PI"
        // should start. The host shows a MessageBox; we never call
        // MessageBox from the VM. Returning a value of Cancelled means
        // the user dismissed the dialog; Skip means the user wants to
        // create a blank PI without an inquiry; FromInquiry means the
        // user wants to pick an existing inquiry (or create a new one)
        // first. The default when no callback is wired is Skip, which
        // preserves the pre-fix behaviour.
        public Func<Task<NewProformaStartChoice>>? AskStartFromInquiryRequested { get; set; }

        // Navigate the shell to the Inquiry module so the user can pick
        // or create an inquiry. Set by MainViewModel.
        public Action? NavigateToInquiryForNewPiRequested { get; set; }

        // True while a "new PI from inquiry" round-trip is in flight
        // (we sent the user to the Inquiry module and they're
        // expected to come back via the inquiry's "Convert to
        // Proforma Invoice" action). Used by the PI list header to
        // show a status message.
        private bool _awaitingInquirySelection;
        public bool IsAwaitingInquirySelection
        {
            get => _awaitingInquirySelection;
            set { if (_awaitingInquirySelection == value) return; _awaitingInquirySelection = value; OnPropertyChanged(); }
        }

        public enum NewProformaStartChoice
        {
            Cancelled = 0,
            Skip = 1,
            FromInquiry = 2
        }

        private readonly IDialogService _dialogs = null!;
        private readonly IProformaInvoiceRepository _invoices = null!;
        private readonly IMasterDataRepository _masterData = null!;
        private readonly ISpecificationsRepository _specs = null!;

        
        public ProformaInvoiceViewModel(IDialogService? dialogs,
            IProformaInvoiceRepository? invoices,
            IMasterDataRepository? masterData,
            ISpecificationsRepository? specs)
        {
            _dialogs = dialogs!;
            _invoices = invoices!;
            _masterData = masterData!;
            _specs = specs!;
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }
public ProformaInvoiceViewModel() : this(
            App.ServiceProvider?.GetService<IDialogService>() ?? new DialogService(),
            App.ServiceProvider?.GetService<IProformaInvoiceRepository>()!,
            App.ServiceProvider?.GetService<IMasterDataRepository>()!,
            App.ServiceProvider?.GetService<ISpecificationsRepository>()!)
        { }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            Step1VM = new Step1CompanySetup.CompanySetupViewModel(
                _invoices, _masterData, stepNumber => GoToStep(stepNumber));

            Step2VM = new Step2ClientProfile.ClientProfileViewModel(_masterData);
            Step3VM = new Step3ProjectSpecs.ProjectSpecsViewModel(_masterData);

            Step4VM = new Step4PricingEstimator.PricingEstimatorViewModel(_dialogs, _specs);
            Step5VM = new Step5DataGrid.DataGridViewModel(_dialogs);
            Step6VM = new Step6OtherCharges.OtherChargesViewModel(Step5VM);
            Step6VM.BindStep4Pricing(() => Step4VM.CaptureSectionSnapshots());
            Step6VM.BindLaminationEditors(Step4VM.LamVM, Step4VM.DguLamVM);

            Step7VM = new Step7PrintPreview.PrintPreviewViewModel(
                _dialogs, _invoices, Step1VM, Step2VM, Step3VM, Step5VM, Step6VM);

            Step8VM = new Step8Attachments.AttachmentsStepViewModel(_dialogs, Step1VM);

            Step9VM = new Step9ProfitLoss.ProfitLossStepViewModel();
            Step9VM.Bind(
                Step5VM,
                Step4VM.DguVM,
                Step4VM.SguVM,
                () => Step7VM.InvoiceMainSummary.GrandTotalVatInclusive,
                Step4VM.LamVM,
                Step4VM.DguLamVM,
                Step6VM);

            Step7VM.PrintableInvoice.ProfitLoss = Step9VM;
            Step7VM.PrintableInvoice.Step4Pricing = Step4VM;

            WizardSteps = new ObservableCollection<WizardStep>
            {
                new WizardStep { StepNumber = 1, Title = "Company details", Subtitle = "TRN, location and billing" },
                new WizardStep { StepNumber = 2, Title = "Client profile", Subtitle = "Customer, salesman and billing" },
                new WizardStep { StepNumber = 3, Title = "Project specs", Subtitle = "Reference numbers, PO and LPO" },
                new WizardStep { StepNumber = 4, Title = "Glass type & specs", Subtitle = "Estimators, materials & treatments" },
                new WizardStep { StepNumber = 5, Title = "Data grid", Subtitle = "Multi-spec row configurations" },
                new WizardStep { StepNumber = 6, Title = "Other charges", Subtitle = "Supplementary, prep, delivery" },
                new WizardStep { StepNumber = 7, Title = "Print preview", Subtitle = "Review, terms and ERP conversion" },
                new WizardStep { StepNumber = 8, Title = "Attachments", Subtitle = "Photos, drawings & approvals" },
                new WizardStep { StepNumber = 9, Title = "Profit & Loss", Subtitle = "Real cost, margin and profitability" }
            };

            CurrentStep = WizardSteps[0];
            _furthestStepReached = 1;
            foreach (var ws in WizardSteps)
                ws.IsCompleted = false;

            NextStepCommand = new RelayCommand(
                _ => GoToStep(CurrentStepNumber + 1),
                _ => CanGoNext);

            PreviousStepCommand = new RelayCommand(
                _ => GoToStep(CurrentStepNumber - 1),
                _ => CanGoBack);

            GoToStepCommand = new RelayCommand(param =>
            {
                if (param is WizardStep step)
                    GoToStep(step.StepNumber);
            });

            // NewInvoiceCommand: the historically "direct" entry point.
            // We now route through AskStartFromInquiryRequested first so
            // the user gets a 3-way choice (Inquiry / Skip / Cancel)
            // every time, no matter which button (sidebar "+", dashboard
            // quick action, or in-list "NEW INVOICE") triggered the flow.
            // The actual dialog lives in the host (MainViewModel) so the
            // VM stays MessageBox-free.
            NewInvoiceCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(StartNewInvoiceWithPromptAsync, "PI NewInvoice"));

            // NewFromInquiryCommand: the explicit "I want to start from
            // an Inquiry" path. Skips the prompt and goes straight to
            // the Inquiry module so the user can pick or create.
            NewFromInquiryCommand = new RelayCommand(
                _ => BeginNewFromInquiryFlow(),
                _ => NavigateToInquiryForNewPiRequested != null);

            BackToListCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(ReturnToListAsync, "PI BackToList"));

            SavedInvoicesListVM = new SavedInvoicesList.SavedInvoicesListViewModel();

            SavedInvoicesListVM.OnInvoiceLoadRequested = id =>
                AsyncUtility.SafeFireAndForget(() => LoadInvoiceIntoWizardAsync(id), $"PI Load {id}");

            SavedInvoicesListVM.OnInvoiceUploadRequested = (id, uploadPdf) =>
            {
                _pendingApprovalInvoiceId = id;
                AsyncUtility.SafeFireAndForget(() => OpenInvoiceAtStepAndUploadAsync(id, uploadPdf), $"PI Upload {id}");
            };

            Step8VM.AttachmentUploaded += OnStep8AttachmentUploaded;

            SavedInvoicesListVM.OnSnapshotRestoreRequested = json =>
                AsyncUtility.SafeFireAndForget(() => RestoreSnapshotIntoWizardAsync(json), "PI RestoreSnapshot");

            Step2VM.PropertyChanged += OnStep2PropertyChanged;
        }

        // ══════════════════════════════════════════════════════════════
        //  IDISPOSABLE — event-handler cleanup (P2 fix, senior audit)
        //
        //  The two subscriptions in the ctor (Step8VM.AttachmentUploaded
        //  and Step2VM.PropertyChanged) don't leak in the currently
        //  shipping app because parent + children share the app
        //  lifetime, but they DO leak if a caller ever constructs a
        //  short-lived instance (multi-window / tests / preview panes).
        //  Dispose() unhooks them symmetrically; the child VMs stay
        //  alive on their own (they don't own the parent).
        //
        //  Safe to call more than once — the _disposed guard makes
        //  redundant Dispose() calls a no-op, matching the standard
        //  IDisposable contract.
        // ══════════════════════════════════════════════════════════════
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { Step2VM.PropertyChanged -= OnStep2PropertyChanged; }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[ProformaInvoiceViewModel] Dispose: best-effort teardown: " + ex.Message); }

            try { Step8VM.AttachmentUploaded -= OnStep8AttachmentUploaded; }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[ProformaInvoiceViewModel] Dispose: best-effort teardown: " + ex.Message); }

            // Follow-up audit pass (Aug 2026): SavedInvoicesListViewModel
            // is IDisposable and subscribes to static DocumentLifecycle
            // + SampleDataService events that outlive any short-lived
            // parent. Chain its disposal here so the whole PI tree
            // tears down cleanly on shutdown / test hosts. Best-effort:
            // a partially-constructed child never blocks teardown.
            try { Step6VM?.Dispose(); }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[ProformaInvoiceViewModel] Dispose: best-effort teardown: " + ex.Message); }

            try { SavedInvoicesListVM?.Dispose(); }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[ProformaInvoiceViewModel] Dispose: best-effort teardown: " + ex.Message); }

            GC.SuppressFinalize(this);
        }

        private void OnStep2PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Step2ClientProfile.ClientProfileViewModel.SelectedClientName))
                return;
            Step3VM.SetClientFilter(Step2VM.SelectedClientName);
        }

        private void SyncProjectFilterFromCurrentClient()
        {
            Step2VM.AdoptExternalCustomerSelection(Step2VM.CustomerName);
            Step3VM.SetClientFilter(Step2VM.CustomerName);
        }

        private void SyncStep5SelectionsFromStep4()
        {
            var sections = Step4VM.CaptureSectionSnapshots();
            int count = Math.Min(sections.Count, Step5VM.SpecGroups.Count);

            for (int i = 0; i < count; i++)
            {
                var section = sections[i];
                var group = Step5VM.SpecGroups[i];
                bool matchesCurrentSectionSpec =
                    !string.IsNullOrWhiteSpace(section.GeneratedSpecName)
                    && string.Equals(group.SelectedSpecName, section.GeneratedSpecName,
                        StringComparison.OrdinalIgnoreCase);
                bool wasLastSynced =
                    string.IsNullOrWhiteSpace(group.SelectedSpecName)
                    || matchesCurrentSectionSpec
                    || string.Equals(group.SelectedSpecName, group.LastSyncedStep4SpecName,
                        StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(section.SelectedGlassTypeCode)
                    && (string.IsNullOrWhiteSpace(group.SelectedGlassType)
                        || wasLastSynced
                        || string.Equals(group.SelectedGlassType, group.LastSyncedStep4GlassType,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    group.SelectedGlassType = section.SelectedGlassTypeCode;
                }

                if (!string.IsNullOrWhiteSpace(section.GeneratedSpecName) && wasLastSynced)
                {
                    group.SelectedSpecName = section.GeneratedSpecName;
                    group.LastSyncedStep4SpecName = section.GeneratedSpecName;
                    group.LastSyncedStep4GlassType = section.SelectedGlassTypeCode;

                    if (group.ImportedPrice <= 0m && section.EstimatedRatePerSqm > 0m)
                        group.ImportedPrice = section.EstimatedRatePerSqm;
                    if (group.BaseRate <= 0m && section.EstimatedRatePerSqm > 0m)
                        group.BaseRate = section.EstimatedRatePerSqm;
                }
            }
        }

        private void GoToStep(int stepNumber)
        {
            var step = WizardSteps.FirstOrDefault(s => s.StepNumber == stepNumber);
            if (step == null) return;

            CurrentStep = step;
            _furthestStepReached = Math.Max(_furthestStepReached, stepNumber);

            // Publish the invoice's identity for the Specifications Center
            // usage history. Done on every navigation so that by the time
            // Step 4 saves a spec, the PI / customer / project are current
            // no matter which order the user filled them in.
            //
            // Ambient rather than injected: Step 4 has no reference to
            // Steps 1-3, and adding constructor arguments to a DI-resolved
            // view model is precisely the change that broke construction
            // during the ERR-633EEB work.
            try
            {
                Services.ProformaInvoice.ActiveInvoiceContext.Set(
                    Step1VM?.ProformaId,
                    Step2VM?.CustomerName,
                    Step3VM?.ProjectIdentifierName);
            }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Debug(
                    $"[PI.GoToStep] invoice context publish failed: {ExceptionMessages.Deep(ex)}");
            }
            foreach (var ws in WizardSteps)
                ws.IsCompleted = ws.StepNumber < _furthestStepReached;

            // Persist step progress on every navigation so the wizard
            // state survives an app close or a back-to-list round-trip.
            // Fire-and-forget: the silent draft never blocks navigation
            // and never shows dialogs. The PrintableInvoiceViewModel's
            // SilentDraftSaveAsync already guards against empty data.
            Step7VM.PrintableInvoice.FurthestStepReached = _furthestStepReached;
            AsyncUtility.SafeFireAndForget(
                () => Step7VM.PrintableInvoice.SilentDraftSaveAsync(),
                $"PI AutoSave Step{stepNumber}");

            if (stepNumber == 4)
            {
                if (Step5VM.TotalSpecGroups > Step4VM.CalculationSectionCount)
                    Step4VM.EnsureCalculationSectionCount(Step5VM.TotalSpecGroups);

                if (!Step4VM.HasRestoredSnapshotState)
                    Step4VM.RefreshSheetStoreData();
            }
            if (stepNumber == 5)
            {
                // AUTO-SAVE ON THE WAY INTO STEP 5.
                //
                // Previously a spec only reached the Step 5 dropdown if the
                // operator remembered to press APPLY in Step 4. Continuing
                // without it left the dropdown missing the very spec they
                // had just configured.
                //
                // Fire-and-forget deliberately: navigation must not wait on
                // a database write. The dropdown does not depend on this
                // completing first -- SpecCatalogNotifier fires when the
                // save lands and every SpecGroup reloads itself, so the new
                // entry appears a moment later without a refresh.
                //
                // Silent: no-ops when there is nothing valid to save, and a
                // re-save of an unchanged spec is already an idempotent
                // update in the repository.
                AsyncUtility.SafeFireAndForget(
                    () => Step4VM.AutoSaveCurrentSpecAsync(),
                    "PI Step4 AutoSaveSpec");

                Step5VM.EnsureSpecGroupCount(Step4VM.CalculationSectionCount);
                SyncStep5SelectionsFromStep4();
                Step5VM.EnsureDefaultWorkspace();
            }
            if (stepNumber == 6)
                Step6VM.RefreshWorkspace();
            if (stepNumber == 8)
                AsyncUtility.SafeFireAndForget(() => Step8VM.LoadAsync(), "PI Step8 Load");
            if (stepNumber == 9)
                AsyncUtility.SafeFireAndForget(() => Step9VM.RecalculateAsync(), "PI Step9 Recalc");
        }

        private async Task StartNewInvoiceAsync()
        {
            Step1VM.InquiryId = null;
            Step1VM.CompanyName = "";
            Step1VM.CompanyTrn = "";
            Step1VM.CompanyLocation = "";
            Step1VM.IssueDate = DateTime.Now;
            Step1VM.ValidUntil = DateTime.Now.AddDays(7);
            Step1VM.ProformaStatus = "Draft";
            await Step1VM.GenerateUniqueProformaIdAsync();

            Step2VM.CustomerName = "";
            Step2VM.CustomerTrn = "";
            Step2VM.CustomerReferenceCode = "";
            Step2VM.SalesRepresentative = "";
            Step2VM.DocumentPreparedBy = Services.Audit.AuditService.CurrentUser?.DisplayName
                ?? Services.Audit.AuditService.CurrentUsername;
            Step2VM.BillingAddress = "";
            Step2VM.AttentionExecutiveName = "";
            Step2VM.ClientContactPhone = "";
            Step2VM.ClearExternalCustomerSelection();

            Step3VM.SetClientFilter("");
            Step3VM.ProjectIdentifierName = "";
            Step3VM.ProjectContractNo = "";
            Step3VM.ProjectReference = "";
            Step3VM.SiteDeliveryLocation = "";
            Step3VM.LpoNumber = "";

            Services.ProformaInvoice.SpecSheetRegistry.Clear();
            Step4VM.ResetForNewInvoice();
            Step5VM.ResetForNewInvoice();
            Step6VM.ResetForNewInvoice();

            await Task.WhenAll(
                Step1VM.LoadCardsAsync(),
                Step2VM.LoadCardsAsync(),
                Step2VM.LoadSalesmanOptionsAsync(),
                Step3VM.LoadCardsAsync()
            );

            await Step8VM.LoadAsync();
            CurrentStep = WizardSteps[0];
            _furthestStepReached = 1;
            foreach (var ws in WizardSteps)
                ws.IsCompleted = false;
            IsListView = false;
        }

        private async Task ReturnToListAsync()
        {
            // Sync the current wizard progress into the printable VM
            // before the silent draft save so the snapshot captures
            // the correct furthest-step-reached value.
            Step7VM.PrintableInvoice.FurthestStepReached = _furthestStepReached;
            await Step7VM.PrintableInvoice.SilentDraftSaveAsync();
            _pendingApprovalInvoiceId = null;
            IsListView = true;
            await SavedInvoicesListVM.LoadInvoicesAsync();
        }

        private async Task LoadInvoiceIntoWizardAsync(int invoiceId)
        {
            if (_invoices == null) return;
            var entity = await _invoices.GetByIdAsync(invoiceId);
            if (entity == null)
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.SelectedInvoiceCouldNotBe"), LocalizationService.Instance.T("Msg.CapLoadFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);
            Step7VM.InvoiceMainSummary.BeginBatchUpdate();
            try
            {
                ProformaInvoiceMapper.RestoreWizardFromSnapshot(
                    snapshot, Step1VM, Step2VM, Step3VM, Step5VM, Step6VM,
                    Step7VM.InvoiceMainSummary, Step7VM.Terms, Step7VM.PrintableInvoice);
                SyncProjectFilterFromCurrentClient();
            }
            finally { Step7VM.InvoiceMainSummary.EndBatchUpdate(); }

            await Task.WhenAll(
                Step1VM.LoadCardsAsync(),
                Step2VM.LoadCardsAsync(),
                Step2VM.LoadSalesmanOptionsAsync(),
                Step3VM.LoadCardsAsync()
            );
            await Step8VM.LoadAsync();

            // Restore step-completion progress from the snapshot so the
            // operator sees the same completed-step indicators they had
            // when the invoice was last saved.
            int restored = Step7VM.PrintableInvoice.FurthestStepReached;
            _furthestStepReached = Math.Max(1, restored);
            CurrentStep = WizardSteps[0];
            foreach (var ws in WizardSteps)
                ws.IsCompleted = ws.StepNumber < _furthestStepReached;
            IsListView = false;
        }

        private async Task OpenInvoiceAtStepAndUploadAsync(int invoiceId, bool uploadPdf)
        {
            await LoadInvoiceIntoWizardAsync(invoiceId);
            GoToStep(8);
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (uploadPdf) Step8VM.AddPdfCommand.Execute(null);
                else Step8VM.AddImageCommand.Execute(null);
            }));
        }

        private void OnStep8AttachmentUploaded()
        {
            int? id = _pendingApprovalInvoiceId;
            if (id == null) return;
            _pendingApprovalInvoiceId = null;
            AsyncUtility.SafeFireAndForget(() => SavedInvoicesListVM.CompleteApprovalAsync(id.Value), $"PI CompleteApproval {id}");
        }

        private async Task RestoreSnapshotIntoWizardAsync(string snapshotJson)
        {
            var wrapper = new Models.ProformaInvoice.SavedInvoice.SavedProformaInvoice
            {
                FullSnapshotJson = snapshotJson
            };
            var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(wrapper);
            if (snapshot == null)
            {
                _dialogs.Show(LocalizationService.Instance.T("Msg.VersionCouldNotBeRead"), LocalizationService.Instance.T("Msg.CapRestoreVersion"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Step7VM.InvoiceMainSummary.BeginBatchUpdate();
            try
            {
                ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository.ProformaInvoiceMapper.RestoreWizardFromSnapshot(
                    snapshot, Step1VM, Step2VM, Step3VM, Step5VM, Step6VM,
                    Step7VM.InvoiceMainSummary, Step7VM.Terms, Step7VM.PrintableInvoice);
                SyncProjectFilterFromCurrentClient();
            }
            finally { Step7VM.InvoiceMainSummary.EndBatchUpdate(); }
            await Step8VM.LoadAsync();

            int restored = Step7VM.PrintableInvoice.FurthestStepReached;
            _furthestStepReached = Math.Max(1, restored);
            CurrentStep = WizardSteps[0];
            foreach (var ws in WizardSteps)
                ws.IsCompleted = ws.StepNumber < _furthestStepReached;
            IsListView = false;
        }

        public void TriggerNewInvoice() =>
            // Same routed flow as the in-VM NewInvoiceCommand: the
            // host (MainViewModel) wires the prompt so every entry
            // point — sidebar "+", dashboard quick action, in-list
            // button, agent trigger — gives the user the same
            // "Inquiry / Skip / Cancel" choice.
            AsyncUtility.SafeFireAndForget(StartNewInvoiceWithPromptAsync, "PI TriggerNewInvoice");

        /// <summary>
        /// Public "navigate to inquiry so the user can pick" entry.
        /// Used by the explicit "New from Inquiry" command and by
        /// the prompt-driven flow. Sets the awaiting flag so the
        /// PI list header can show a "waiting for inquiry pick"
        /// status. The host wires the actual Navigate call.
        /// </summary>
        public void BeginNewFromInquiryFlow()
        {
            if (NavigateToInquiryForNewPiRequested == null)
            {
                // No host — fall back to a direct blank create. The
                // command's CanExecute guards against this in the
                // common case, but the agent / test paths can still
                // reach here.
                AsyncUtility.SafeFireAndForget(StartNewInvoiceAsync, "PI NewFromInquiry fallback");
                return;
            }
            IsAwaitingInquirySelection = true;
            NavigateToInquiryForNewPiRequested();
        }

        /// <summary>
        /// End the "awaiting inquiry" round-trip. Called by the
        /// inquiry module after the user picks a row (or the chain
        /// reaches StartFromInquiryAsync). Resets the flag and
        /// switches the shell back to the Proforma view.
        /// </summary>
        public void CompleteInquirySelectionForNewPi()
        {
            IsAwaitingInquirySelection = false;
        }

        private async Task StartNewInvoiceWithPromptAsync()
        {
            NewProformaStartChoice choice = NewProformaStartChoice.Skip;
            try
            {
                if (AskStartFromInquiryRequested != null)
                    choice = await AskStartFromInquiryRequested();
            }
            catch (Exception ex)
            {
                // The host dialog threw — log and default to Skip so
                // the user is never left without a path. The audit
                // log captures the host-side exception.
                AppLogService.Warning($"PI NewInvoice prompt failed: {ExceptionMessages.Deep(ex)}",
                    new LogContext { Module = "Proforma", Class = nameof(ProformaInvoiceViewModel), Operation = "NewInvoicePrompt" });
                choice = NewProformaStartChoice.Skip;
            }
            switch (choice)
            {
                case NewProformaStartChoice.FromInquiry:
                    BeginNewFromInquiryFlow();
                    break;
                case NewProformaStartChoice.Skip:
                    await StartNewInvoiceAsync();
                    break;
                // Cancelled: do nothing.
            }
        }

        /// <summary>
        /// Pre-fills the wizard from an Inquiry the operator picked.
        ///
        /// Senior-audit fix (Aug 2026):
        ///   • Also carries CustomerLocation → BillingAddress and
        ///     ProjectDescription → ProjectReference (fuller text),
        ///     falling back to InquiryNumber if the description is
        ///     empty. Previously only 5 of the 8 relevant fields
        ///     were copied.
        ///   • Bug #7 protection — refuses to start a new PI when
        ///     the inquiry already has a linked PI. The Inquiry list
        ///     already guards this in the UI, but the VM must guard
        ///     too because other entry points (Agent, tests, dashboard
        ///     quick-actions) reach here without going through the
        ///     list. Once InquiryLinkSync populates ProformaNumber
        ///     correctly, this check is meaningful.
        /// </summary>
        public async Task StartFromInquiryAsync(Models.Inquiry.InquiryRecord inquiry)
        {
            if (inquiry == null) throw new ArgumentNullException(nameof(inquiry));
            // Allow PI creation from Prepared or Confirmed inquiries.
            // The inquiry status is automatically advanced to "Sent"
            // when the PI is created (via InquiryLinkSync), and to
            // "Confirmed" when the PI is approved. This removes the
            // manual confirmation step that was blocking the workflow.
            if (!string.Equals(inquiry.Status, Models.Inquiry.InquiryStatuses.Confirmed, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(inquiry.Status, Models.Inquiry.InquiryStatuses.Prepared, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only a Prepared or Confirmed Inquiry is eligible for Proforma creation.");
            if (inquiry.ProformaInvoiceId.HasValue ||
                !string.IsNullOrWhiteSpace(inquiry.ProformaNumber))
            {
                throw new InvalidOperationException(
                    $"Inquiry {inquiry.InquiryNumber} already has a Proforma Invoice " +
                    $"({inquiry.ProformaNumber}). Delete the existing PI first or " +
                    $"open it via Load Invoice.");
            }

            // Clear the "awaiting inquiry pick" flag so the banner does
            // not linger while the wizard fields populate below. The
            // host performs the actual navigation after this method
            // returns.
            IsAwaitingInquirySelection = false;
            await StartNewInvoiceAsync();

            // Company / commercial context
            Step1VM.InquiryId = inquiry.Id;

            // Client identity — Customer Name is the DB-required field
            // and also the trigger for the Step 3 project filter (see
            // OnStep2PropertyChanged). Setting CustomerName alone does
            // NOT wake the SelectedClientName property; the wizard's
            // saved-clients list normally does that when the operator
            // clicks a card. We therefore also assign BillingAddress
            // from the inquiry so the operator can see a starting
            // point in Step 2 without re-typing.
            Step2VM.CustomerName = inquiry.CustomerName;
            if (!string.IsNullOrWhiteSpace(inquiry.CustomerLocation))
                Step2VM.BillingAddress = inquiry.CustomerLocation;
            Step2VM.SalesRepresentative = inquiry.SalesmanName;
            Step2VM.DocumentPreparedBy = inquiry.PreparedByName;
            Step2VM.AdoptExternalCustomerSelection(inquiry.CustomerName);
            Step3VM.SetClientFilter(inquiry.CustomerName);

            // Project reference. ProjectDescription is the fuller
            // free-text field on the inquiry; when empty we fall back
            // to InquiryNumber so the PI always carries something
            // linking back to the origin.
            Step3VM.ProjectIdentifierName = inquiry.ProjectName;
            Step3VM.ProjectReference =
                !string.IsNullOrWhiteSpace(inquiry.ProjectDescription)
                    ? inquiry.ProjectDescription
                    : inquiry.InquiryNumber;
        }

        public async Task OpenSavedInvoiceAsync(int invoiceId)
        {
            try { await LoadInvoiceIntoWizardAsync(invoiceId); }
            catch (Exception ex)
            {
                AppLogService.Warning($"Could not open proforma invoice {invoiceId}: {ExceptionMessages.Deep(ex)}",
                    new LogContext { Module = "Proforma", Class = nameof(ProformaInvoiceViewModel), Operation = "OpenFromReports" });
            }
        }
    }
}
