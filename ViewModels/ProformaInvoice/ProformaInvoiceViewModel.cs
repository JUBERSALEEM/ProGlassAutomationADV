using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Step7PrintPreview.TermsAndConditions;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice
{
    /// <summary>
    /// Temporary list item — will be replaced with real ProformaInvoice entity later.
    /// Kept here so Dashboard bindings don't break.
    ///
    /// BUG-79 [MEDIUM]: Documented as Dashboard-contract placeholder.
    /// Real data comes from Phase 20+ Dashboard analytics service.
    /// </summary>
    public class InvoiceItem : ViewModelBase
    {
        public string InvoiceNo { get; set; }
        public string CustomerName { get; set; }
        public string Date { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Pending";
    }

    /// <summary>
    /// Represents a single step in the wizard sidebar.
    ///
    /// BUG-81: Kept in shell file to match existing project structure.
    /// Splitting would require updating XAML "clr-namespace" bindings.
    /// </summary>
    public class WizardStep : ViewModelBase
    {
        public int StepNumber { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        private bool _isCompleted;
        public bool IsCompleted
        {
            get => _isCompleted;
            set { _isCompleted = value; OnPropertyChanged(); }
        }
    }

    /// <summary>
    /// Main ViewModel for the Proforma Invoice module.
    /// Phase 7.19 : List-first architecture — module landing = SavedInvoicesList.
    /// Phase 8.2  : TriggerNewInvoice() public method for Dashboard quick action.
    ///
    /// BUG-77 [HIGH]: LoadInvoiceIntoWizard was async void — now async Task
    ///                fired via SafeFireAsync so exceptions surface properly.
    /// BUG-78 [HIGH]: StartNewInvoice rebuilds spec groups without leaking
    ///                event handlers hooked by OtherChargesViewModel on the
    ///                prior spec-group instances. Uses full async path so
    ///                unique ProformaId is DB-verified (see BUG-33 in
    ///                CompanySetupViewModel).
    /// BUG-79 [MEDIUM]: InvoiceItem documented as Dashboard placeholder.
    /// BUG-80 [LOW]: Dashboard totals return 0 — Phase 20+ implementation.
    /// BUG-81 [COSMETIC]: File structure preserved — no split.
    /// </summary>
    public class ProformaInvoiceViewModel : ViewModelBase
    {
        public string Title { get; } = "Proforma Invoice Builder";
        public string Subtitle { get; } = "Create professional glass invoices in 7 simple steps";

        // === Dashboard Contract (preserved so DashboardViewModel doesn't break) ===

        public ObservableCollection<InvoiceItem> Invoices { get; }
            = new ObservableCollection<InvoiceItem>();

        // BUG-80 [LOW]: Real values come from Phase 20+ Dashboard service.
        // Kept as 0 hardcoded so Dashboard binding paths do not crash.
        public decimal TotalSales => 0m;
        public int InvoiceCount => 0;
        public int PendingCount => 0;
        public bool HasInvoices => false;

        // === Step ViewModels ===
        public Step1CompanySetup.CompanySetupViewModel Step1VM { get; }
        public Step2ProjectSpecs.ProjectSpecsViewModel Step2VM { get; }
        public Step3ClientProfile.ClientProfileViewModel Step3VM { get; }
        public Step4PricingEstimator.PricingEstimatorViewModel Step4VM { get; }
        public Step5DataGrid.DataGridViewModel Step5VM { get; }
        public Step6OtherCharges.OtherChargesViewModel Step6VM { get; }
        public Step7PrintPreview.PrintPreviewViewModel Step7VM { get; }

        // === Wizard Steps ===
        public ObservableCollection<WizardStep> WizardSteps { get; }

        private WizardStep _currentStep;
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
                OnPropertyChanged(nameof(StepProgressText));
                OnPropertyChanged(nameof(ProgressPercent));
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(IsLastStep));

                CommandManager.InvalidateRequerySuggested();
            }
        }

        public int CurrentStepNumber => CurrentStep?.StepNumber ?? 1;
        public string StepProgressText => $"STEP  {CurrentStepNumber}  OF  7";
        public double ProgressPercent => (double)CurrentStepNumber / 7.0;
        public bool CanGoBack => CurrentStepNumber > 1;
        public bool CanGoNext => CurrentStepNumber < 7;
        public bool IsLastStep => CurrentStepNumber == 7;

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 7.19 — LIST ↔ WIZARD navigation state
        // ══════════════════════════════════════════════════════════════════

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

        /// <summary>The saved invoices list VM — shown as the module landing page.</summary>
        public SavedInvoicesList.SavedInvoicesListViewModel SavedInvoicesListVM { get; }

        // === Commands ===
        public ICommand NextStepCommand { get; }
        public ICommand PreviousStepCommand { get; }
        public ICommand GoToStepCommand { get; }
        public ICommand NewInvoiceCommand { get; }
        public ICommand BackToListCommand { get; }

        public ProformaInvoiceViewModel()
        {
            // Initialize Step VMs
            Step1VM = new Step1CompanySetup.CompanySetupViewModel(
                stepNumber => GoToStep(stepNumber));

            Step2VM = new Step2ProjectSpecs.ProjectSpecsViewModel();
            Step3VM = new Step3ClientProfile.ClientProfileViewModel();
            Step4VM = new Step4PricingEstimator.PricingEstimatorViewModel();
            Step5VM = new Step5DataGrid.DataGridViewModel();

            Step6VM = new Step6OtherCharges.OtherChargesViewModel(Step5VM);

            Step7VM = new Step7PrintPreview.PrintPreviewViewModel(
                Step1VM, Step2VM, Step3VM, Step5VM, Step6VM);

            WizardSteps = new ObservableCollection<WizardStep>
            {
                new WizardStep { StepNumber = 1, Title = "Company details",    Subtitle = "TRN, location and billing" },
                new WizardStep { StepNumber = 2, Title = "Project",            Subtitle = "Reference numbers, PO and l..." },
                new WizardStep { StepNumber = 3, Title = "Other references",   Subtitle = "Clients, salesman and attention" },
                new WizardStep { StepNumber = 4, Title = "Glass type & specs", Subtitle = "Estimators, materials & treatm..." },
                new WizardStep { StepNumber = 5, Title = "Data grid",          Subtitle = "Multi-spec row configurations" },
                new WizardStep { StepNumber = 6, Title = "Other charges",      Subtitle = "Supplementary, prep, delivery" },
                new WizardStep { StepNumber = 7, Title = "Print preview",      Subtitle = "Review, terms and ERP conve..." }
            };

            CurrentStep = WizardSteps[0];

            // Wizard navigation commands
            NextStepCommand = new RelayCommand(
                _ => GoToStep(CurrentStepNumber + 1),
                _ => CanGoNext);

            PreviousStepCommand = new RelayCommand(
                _ => GoToStep(CurrentStepNumber - 1),
                _ => CanGoBack);

            GoToStepCommand = new RelayCommand(param =>
            {
                if (param is WizardStep step)
                    CurrentStep = step;
            });

            // BUG-77/78: SafeFireAsync wrappers
            NewInvoiceCommand = new RelayCommand(
                _ => SafeFireAsync(StartNewInvoiceAsync));
            BackToListCommand = new RelayCommand(
                _ => SafeFireAsync(ReturnToListAsync));

            // Saved Invoices List VM — the module landing page
            SavedInvoicesListVM = new SavedInvoicesList.SavedInvoicesListViewModel();

            // BUG-77: safe wrapper for the load callback
            SavedInvoicesListVM.OnInvoiceLoadRequested = id =>
                SafeFireAsync(() => LoadInvoiceIntoWizardAsync(id));
        }

        // ══════════════════════════════════════════════════════════════════
        //  BUG-77 [HIGH]: SafeFireAsync wrapper
        //  Replaces every "async void" method in the file.
        // ══════════════════════════════════════════════════════════════════

        private static async void SafeFireAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unexpected error:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void GoToStep(int stepNumber)
        {
            var step = WizardSteps.FirstOrDefault(s => s.StepNumber == stepNumber);
            if (step != null)
                CurrentStep = step;
        }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 7.19 — LIST ↔ WIZARD navigation
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// BUG-78 [HIGH]: Rebuilds spec groups cleanly so event handlers
        ///                hooked by OtherChargesViewModel on the OLD groups
        ///                are released before we add new ones.
        /// BUG-33 integration: uses async unique-ID generator from
        ///                      CompanySetupViewModel to guarantee no
        ///                      collision on ProformaId.
        /// </summary>
        private async Task StartNewInvoiceAsync()
        {
            // ── Reset Step 1 — request a real DB-verified unique ID ──────
            Step1VM.CompanyName = "";
            Step1VM.CompanyTrn = "";
            Step1VM.CompanyLocation = "";
            Step1VM.IssueDate = DateTime.Now;
            Step1VM.ValidUntil = DateTime.Now.AddDays(7);
            Step1VM.ProformaStatus = "Draft";

            // BUG-33: async DB check for next unique Proforma ID
            await Step1VM.GenerateUniqueProformaIdAsync();

            // ── Reset Step 2 ──────────────────────────────────────────────
            Step2VM.ProjectIdentifierName = "";
            Step2VM.ProjectContractNo = "";
            Step2VM.SiteDeliveryLocation = "";
            Step2VM.LpoNumber = "";

            // ── Reset Step 3 ──────────────────────────────────────────────
            Step3VM.CustomerName = "";
            Step3VM.CustomerTrn = "";
            Step3VM.CustomerReferenceCode = "";
            Step3VM.SalesRepresentative = "";
            Step3VM.DocumentPreparedBy = "";
            Step3VM.BillingAddress = "";
            Step3VM.AttentionExecutiveName = "";
            Step3VM.ClientContactPhone = "";

            // ── Reset Step 5 — clean spec-group rebuild (BUG-78) ─────────
            // Clearing SpecGroups fires OtherChargesViewModel's
            // OnSpecGroupsCollectionChanged which unhooks handlers from
            // each old group via UnhookSpecGroup(). This prevents the
            // handler leak. Then adding the new default group re-hooks
            // handlers cleanly via HookSpecGroup().
            Step5VM.SpecGroups.Clear();
            Step5VM.SpecGroups.Add(new Step5DataGrid.SpecGroup
            {
                GroupNumber = 1,
                GroupName = "Spec Group 1",
                BaseRate = 150m,
                SurchargePercent = 0m,
                SelectedGlassType = "SGU"
            });

            // ── Navigate to Step 1 and show wizard ────────────────────────
            CurrentStep = WizardSteps[0];
            IsListView = false;
        }

        /// <summary>
        /// Returns to the saved invoices list view.
        /// BUG-77: async Task, called via SafeFireAsync so any refresh
        ///          exception surfaces to the user.
        /// </summary>
        private async Task ReturnToListAsync()
        {
            IsListView = true;
            await SavedInvoicesListVM.LoadInvoicesAsync();
        }

        /// <summary>
        /// Called when user clicks LOAD on a row in SavedInvoicesList.
        /// BUG-77 [HIGH]: was async void — now async Task fired via
        ///                SafeFireAsync so DB errors surface.
        /// </summary>
        private async Task LoadInvoiceIntoWizardAsync(int invoiceId)
        {
            if (App.Invoices == null) return;

            var entity = await App.Invoices.GetByIdAsync(invoiceId);
            if (entity == null)
            {
                MessageBox.Show(
                    "Selected invoice could not be found.",
                    "Load Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);

            // BUG-57 integration: use batch update on MainSummary so
            // Recalculate fires ONCE instead of per-property.
            Step7VM.InvoiceMainSummary.BeginBatchUpdate();
            try
            {
                ProformaInvoiceMapper.RestoreWizardFromSnapshot(
                    snapshot,
                    Step1VM, Step2VM, Step3VM, Step5VM, Step6VM,
                    Step7VM.InvoiceMainSummary,
                    Step7VM.Terms,
                    Step7VM.PrintableInvoice);
            }
            finally
            {
                Step7VM.InvoiceMainSummary.EndBatchUpdate();
            }

            // Switch to wizard at Step 1 for review
            CurrentStep = WizardSteps[0];
            IsListView = false;
        }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 8.2 — Public trigger for Dashboard quick action
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by MainViewModel when the Dashboard "New Invoice" quick
        /// action is clicked. Delegates to StartNewInvoiceAsync() via the
        /// SafeFireAsync wrapper (BUG-77) so exceptions surface.
        /// </summary>
        public void TriggerNewInvoice() =>
            SafeFireAsync(StartNewInvoiceAsync);
    }
}