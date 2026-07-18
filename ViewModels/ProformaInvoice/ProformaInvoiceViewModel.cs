using ProGlassAutomation.Commands;
using System.Collections.ObjectModel;
using System.Linq;
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
    /// Hosts the 7-step wizard for building new invoices.
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
            = new Step1CompanySetup.CompanySetupViewModel();

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

                // Ensure Back/Continue enable/disable updates immediately
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public int CurrentStepNumber => CurrentStep?.StepNumber ?? 1;
        public string StepProgressText => $"STEP  {CurrentStepNumber}  OF  7";

        // Still useful for future (if we later switch to smooth progress bar)
        public double ProgressPercent => (double)CurrentStepNumber / 7.0;

        public bool CanGoBack => CurrentStepNumber > 1;
        public bool CanGoNext => CurrentStepNumber < 7;
        public bool IsLastStep => CurrentStepNumber == 7;

        // === Commands ===
        public ICommand NextStepCommand { get; }
        public ICommand PreviousStepCommand { get; }
        public ICommand GoToStepCommand { get; }

        public ProformaInvoiceViewModel()
        {
            WizardSteps = new ObservableCollection<WizardStep>
            {
                new WizardStep
                {
                    StepNumber = 1,
                    Title = "Company details",
                    Subtitle = "TRN, location and billing"
                },
                new WizardStep
                {
                    StepNumber = 2,
                    Title = "Project",
                    Subtitle = "Reference numbers, PO and l..."
                },
                new WizardStep
                {
                    StepNumber = 3,
                    Title = "Other references",
                    Subtitle = "Clients, salesman and attention"
                },
                new WizardStep
                {
                    StepNumber = 4,
                    Title = "Glass type & specs",
                    Subtitle = "Estimators, materials & treatm..."
                },
                new WizardStep
                {
                    StepNumber = 5,
                    Title = "Data grid",
                    Subtitle = "Multi-spec row configurations"
                },
                new WizardStep
                {
                    StepNumber = 6,
                    Title = "Other charges",
                    Subtitle = "Supplementary, prep, delivery"
                },
                new WizardStep
                {
                    StepNumber = 7,
                    Title = "Print preview",
                    Subtitle = "Review, terms and ERP conve..."
                }
            };

            CurrentStep = WizardSteps[0];

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
        }

        private void GoToStep(int stepNumber)
        {
            var step = WizardSteps.FirstOrDefault(s => s.StepNumber == stepNumber);
            if (step != null)
                CurrentStep = step;
        }
    }
}