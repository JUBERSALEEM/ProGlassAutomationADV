using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice
{
    /// <summary>
    /// Temporary list item — will be replaced with real ProformaInvoice entity later.
    /// Kept here so Dashboard bindings don't break.
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
    /// Phase 7.19: List-first architecture — module landing page shows
    /// SavedInvoicesList. User creates new or edits existing from the list.
    /// Wizard is shown on demand, with "Back to List" navigation.
    /// Phase 8.2: Added TriggerNewInvoice() public method so Dashboard
    /// quick action can open the new invoice wizard via MainViewModel callback.
    /// </summary>
    public class ProformaInvoiceViewModel : ViewModelBase
    {
        public string Title { get; } = "Proforma Invoice Builder";
        public string Subtitle { get; } = "Create professional glass invoices in 7 simple steps";

        // === Dashboard Contract (preserved so DashboardViewModel doesn't break) ===
        public ObservableCollection<InvoiceItem> Invoices { get; }
            = new ObservableCollection<InvoiceItem>();

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
        //  When IsListView = true, the SavedInvoicesList is shown.
        //  When false, the 7-step wizard is shown.
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

            // Phase 7.19: List ↔ Wizard navigation
            NewInvoiceCommand = new RelayCommand(_ => StartNewInvoice());
            BackToListCommand = new RelayCommand(_ => ReturnToList());

            // Saved Invoices List VM — the module landing page
            SavedInvoicesListVM = new SavedInvoicesList.SavedInvoicesListViewModel();
            SavedInvoicesListVM.OnInvoiceLoadRequested = LoadInvoiceIntoWizard;
        }

        private void GoToStep(int stepNumber)
        {
            var step = WizardSteps.FirstOrDefault(s => s.StepNumber == stepNumber);
            if (step != null)
                CurrentStep = step;
        }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 7.19 — List ↔ Wizard navigation methods
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a fresh wizard (resets all steps) and switches to wizard view.
        /// Called when user clicks "+ NEW INVOICE" on the list page.
        /// </summary>
        private void StartNewInvoice()
        {
            // Reset Step 1
            Step1VM.CompanyName = "";
            Step1VM.CompanyTrn = "";
            Step1VM.CompanyLocation = "";
            Step1VM.ProformaId = $"PI-{DateTime.Now.Year}-{(DateTime.Now.Ticks % 1000):000}";
            Step1VM.IssueDate = DateTime.Now;
            Step1VM.ValidUntil = DateTime.Now.AddDays(7);
            Step1VM.ProformaStatus = "Draft";

            // Reset Step 2
            Step2VM.ProjectIdentifierName = "";
            Step2VM.ProjectContractNo = "";
            Step2VM.SiteDeliveryLocation = "";
            Step2VM.LpoNumber = "";

            // Reset Step 3
            Step3VM.CustomerName = "";
            Step3VM.CustomerTrn = "";
            Step3VM.CustomerReferenceCode = "";
            Step3VM.SalesRepresentative = "";
            Step3VM.DocumentPreparedBy = "";
            Step3VM.BillingAddress = "";
            Step3VM.AttentionExecutiveName = "";
            Step3VM.ClientContactPhone = "";

            // Reset Step 5 — clear all spec groups and add a fresh one
            Step5VM.SpecGroups.Clear();
            Step5VM.SpecGroups.Add(new Step5DataGrid.SpecGroup
            {
                GroupNumber = 1,
                GroupName = "Spec Group 1",
                BaseRate = 150m,
                SurchargePercent = 0m,
                SelectedGlassType = "SGU"
            });

            // Navigate to Step 1 and show wizard
            CurrentStep = WizardSteps[0];
            IsListView = false;
        }

        /// <summary>
        /// Returns to the saved invoices list view.
        /// Called when user clicks "Back to List" in the wizard bottom nav.
        /// </summary>
        private void ReturnToList()
        {
            IsListView = true;
            // Refresh the list in case user saved something while in wizard
            _ = SavedInvoicesListVM.LoadInvoicesAsync();
        }

        /// <summary>
        /// Called when user clicks LOAD on a row in the SavedInvoicesList.
        /// Fetches the full entity, restores the wizard from the JSON snapshot,
        /// and switches to wizard view at Step 1 for review.
        /// </summary>
        private async void LoadInvoiceIntoWizard(int invoiceId)
        {
            if (App.Invoices == null) return;

            try
            {
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

                ProformaInvoiceMapper.RestoreWizardFromSnapshot(
                    snapshot,
                    Step1VM, Step2VM, Step3VM, Step5VM, Step6VM,
                    Step7VM.InvoiceMainSummary, Step7VM.Terms, Step7VM.PrintableInvoice);

                // Switch to wizard at Step 1 for review
                CurrentStep = WizardSteps[0];
                IsListView = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load invoice:\n\n{ex.Message}",
                    "Load Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 8.2 — Public trigger for Dashboard quick action
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by MainViewModel when the Dashboard "New Invoice" quick
        /// action is clicked. Delegates to StartNewInvoice() so the wizard
        /// resets and opens — same behaviour as clicking "+ NEW INVOICE"
        /// on the list page directly.
        /// </summary>
        public void TriggerNewInvoice() => StartNewInvoice();
    }
}