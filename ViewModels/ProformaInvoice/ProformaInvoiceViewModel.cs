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

namespace ProGlassAutomation.ViewModels.ProformaInvoice
{
    public class ProformaInvoiceViewModel : ViewModelBase,
        ProGlassAutomation.Agent.Context.IAgentContextAware
    {
        public string Title { get; } = "Proforma Invoice Builder";

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
        public string Subtitle { get; } = "Nine-station industrial quotation deck";

        public const int TotalSteps = 9;

        public ObservableCollection<InvoiceItem> Invoices { get; }
            = new ObservableCollection<InvoiceItem>();

        public decimal TotalSales => 0m;
        public int InvoiceCount => 0;
        public int PendingCount => 0;
        public bool HasInvoices => false;

        public Step1CompanySetup.CompanySetupViewModel Step1VM { get; }
        public Step2ClientProfile.ClientProfileViewModel Step2VM { get; }
        public Step3ProjectSpecs.ProjectSpecsViewModel Step3VM { get; }
        public Step4PricingEstimator.PricingEstimatorViewModel Step4VM { get; }
        public Step5DataGrid.DataGridViewModel Step5VM { get; }
        public Step6OtherCharges.OtherChargesViewModel Step6VM { get; }
        public Step7PrintPreview.PrintPreviewViewModel Step7VM { get; }
        public Step8Attachments.AttachmentsStepViewModel Step8VM { get; }
        public Step9ProfitLoss.ProfitLossStepViewModel Step9VM { get; }

        public ObservableCollection<WizardStep> WizardSteps { get; }

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
                CommandManager.InvalidateRequerySuggested();
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
        public SavedInvoicesList.SavedInvoicesListViewModel SavedInvoicesListVM { get; }

        public ICommand NextStepCommand { get; }
        public ICommand PreviousStepCommand { get; }
        public ICommand GoToStepCommand { get; }
        public ICommand NewInvoiceCommand { get; }
        public ICommand BackToListCommand { get; }

        public ProformaInvoiceViewModel()
        {
            Step1VM = new Step1CompanySetup.CompanySetupViewModel(
                stepNumber => GoToStep(stepNumber));

            Step2VM = new Step2ClientProfile.ClientProfileViewModel();
            Step3VM = new Step3ProjectSpecs.ProjectSpecsViewModel();

            Step4VM = new Step4PricingEstimator.PricingEstimatorViewModel();
            Step5VM = new Step5DataGrid.DataGridViewModel();
            Step6VM = new Step6OtherCharges.OtherChargesViewModel(Step5VM);
            Step6VM.BindLaminationEditors(Step4VM.LamVM, Step4VM.DguLamVM);

            Step7VM = new Step7PrintPreview.PrintPreviewViewModel(
                Step1VM, Step2VM, Step3VM, Step5VM, Step6VM);

            Step8VM = new Step8Attachments.AttachmentsStepViewModel(Step1VM);

            Step9VM = new Step9ProfitLoss.ProfitLossStepViewModel();
            Step9VM.Bind(
                Step5VM,
                Step4VM.DguVM,
                Step4VM.SguVM,
                () => Step7VM.InvoiceMainSummary.GrandTotalVatInclusive,
                Step4VM.LamVM,
                Step4VM.DguLamVM);

            Step7VM.PrintableInvoice.ProfitLoss = Step9VM;

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

            NewInvoiceCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(StartNewInvoiceAsync, "PI NewInvoice"));

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

        private void OnStep2PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Step2ClientProfile.ClientProfileViewModel.SelectedClientName))
                return;
            Step3VM.SetClientFilter(Step2VM.SelectedClientName);
        }

        private void GoToStep(int stepNumber)
        {
            var step = WizardSteps.FirstOrDefault(s => s.StepNumber == stepNumber);
            if (step == null) return;

            CurrentStep = step;
            _furthestStepReached = Math.Max(_furthestStepReached, stepNumber);
            foreach (var ws in WizardSteps)
                ws.IsCompleted = ws.StepNumber < _furthestStepReached;

            if (stepNumber == 4)
                Step4VM.RefreshSheetStoreData();
            if (stepNumber == 5)
                Step5VM.EnsureDefaultWorkspace();
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
            Step2VM.DocumentPreparedBy = "";
            Step2VM.BillingAddress = "";
            Step2VM.AttentionExecutiveName = "";
            Step2VM.ClientContactPhone = "";

            Step3VM.ProjectIdentifierName = "";
            Step3VM.ProjectContractNo = "";
            Step3VM.ProjectReference = "";
            Step3VM.SiteDeliveryLocation = "";
            Step3VM.LpoNumber = "";

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
            await Step7VM.PrintableInvoice.SilentDraftSaveAsync();
            _pendingApprovalInvoiceId = null;
            IsListView = true;
            await SavedInvoicesListVM.LoadInvoicesAsync();
        }

        private async Task LoadInvoiceIntoWizardAsync(int invoiceId)
        {
            if (App.Invoices == null) return;
            var entity = await App.Invoices.GetByIdAsync(invoiceId);
            if (entity == null)
            {
                MessageBox.Show("Selected invoice could not be found.", "Load Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);
            Step7VM.InvoiceMainSummary.BeginBatchUpdate();
            try
            {
                ProformaInvoiceMapper.RestoreWizardFromSnapshot(
                    snapshot, Step1VM, Step2VM, Step3VM, Step5VM, Step6VM,
                    Step7VM.InvoiceMainSummary, Step7VM.Terms, Step7VM.PrintableInvoice);
            }
            finally { Step7VM.InvoiceMainSummary.EndBatchUpdate(); }

            await Task.WhenAll(
                Step1VM.LoadCardsAsync(),
                Step2VM.LoadCardsAsync(),
                Step2VM.LoadSalesmanOptionsAsync(),
                Step3VM.LoadCardsAsync()
            );
            await Step8VM.LoadAsync();
            CurrentStep = WizardSteps[0];
            _furthestStepReached = 1;
            foreach (var ws in WizardSteps) ws.IsCompleted = false;
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
                MessageBox.Show("This version could not be read.", "Restore Version", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Step7VM.InvoiceMainSummary.BeginBatchUpdate();
            try
            {
                ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository.ProformaInvoiceMapper.RestoreWizardFromSnapshot(
                    snapshot, Step1VM, Step2VM, Step3VM, Step5VM, Step6VM,
                    Step7VM.InvoiceMainSummary, Step7VM.Terms, Step7VM.PrintableInvoice);
            }
            finally { Step7VM.InvoiceMainSummary.EndBatchUpdate(); }
            await Step8VM.LoadAsync();
            CurrentStep = WizardSteps[0];
            _furthestStepReached = 1;
            foreach (var ws in WizardSteps) ws.IsCompleted = false;
            IsListView = false;
        }

        public void TriggerNewInvoice() =>
            AsyncUtility.SafeFireAndForget(StartNewInvoiceAsync, "PI TriggerNewInvoice");

        public async Task StartFromInquiryAsync(Models.Inquiry.InquiryRecord inquiry)
        {
            if (inquiry == null) throw new ArgumentNullException(nameof(inquiry));
            if (!string.Equals(inquiry.Status, Models.Inquiry.InquiryStatuses.Confirmed, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only a confirmed Inquiry is eligible for Proforma creation.");
            await StartNewInvoiceAsync();
            Step1VM.InquiryId = inquiry.Id;
            Step2VM.CustomerName = inquiry.CustomerName;
            Step2VM.SalesRepresentative = inquiry.SalesmanName;
            Step2VM.DocumentPreparedBy = inquiry.PreparedByName;
            Step3VM.ProjectIdentifierName = inquiry.ProjectName;
            Step3VM.ProjectReference = inquiry.InquiryNumber;
        }

        public async Task OpenSavedInvoiceAsync(int invoiceId)
        {
            try { await LoadInvoiceIntoWizardAsync(invoiceId); }
            catch (Exception ex)
            {
                AppLogService.Warning($"Could not open proforma invoice {invoiceId}: {ex.Message}",
                    new LogContext { Module = "Proforma", Class = nameof(ProformaInvoiceViewModel), Operation = "OpenFromReports" });
            }
        }
    }
}
