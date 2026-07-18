using System.Collections.ObjectModel;

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

        // === Wizard Steps ===
        public ObservableCollection<WizardStep> WizardSteps { get; }

        private WizardStep _currentStep;
        public WizardStep CurrentStep
        {
            get => _currentStep;
            set
            {
                _currentStep = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentStepNumber));
                OnPropertyChanged(nameof(StepProgressText));
            }
        }

        public int CurrentStepNumber => CurrentStep?.StepNumber ?? 1;
        public string StepProgressText => $"STEP  {CurrentStepNumber}  OF  7";

        public ProformaInvoiceViewModel()
        {
            WizardSteps = new ObservableCollection<WizardStep>
            {
                new WizardStep
                {
                    StepNumber = 1,
                    Title = "Company details",
                    Subtitle = "TRN, location and billing",
                    IsActive = true
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
        }
    }
}