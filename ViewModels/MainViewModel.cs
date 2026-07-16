using ProGlassAutomation.Commands;
using ProGlassAutomation.Services;
using ProGlassAutomation.ViewModels.About;
using ProGlassAutomation.ViewModels.Dashboard;
using ProGlassAutomation.ViewModels.DeliveryNote;
using ProGlassAutomation.ViewModels.JobOrder;
using ProGlassAutomation.ViewModels.Optimization;
using ProGlassAutomation.ViewModels.ProformaInvoice;
using ProGlassAutomation.ViewModels.Production;
using ProGlassAutomation.ViewModels.Reports;
using ProGlassAutomation.ViewModels.SheetStore;
using ProGlassAutomation.ViewModels.TaxInvoice;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentViewModel;
        private bool _isMenuOpen;
        private bool _isSettingsOpen;

        // Existing modules
        public ProformaInvoiceViewModel ProformaInvoiceVM { get; }
        public DashboardViewModel DashboardVM { get; }
        public AboutViewModel AboutVM { get; } = new AboutViewModel();

        // New placeholder modules
        public SheetStoreViewModel SheetStoreVM { get; } = new SheetStoreViewModel();
        public JobOrderViewModel JobOrderVM { get; } = new JobOrderViewModel();
        public OptimizationViewModel OptimizationVM { get; } = new OptimizationViewModel();

        public CuttingViewModel CuttingVM { get; } = new CuttingViewModel();
        public HolesCutoutsViewModel HolesCutoutsVM { get; } = new HolesCutoutsViewModel();
        public PolishViewModel PolishVM { get; } = new PolishViewModel();
        public EdgeDeletionViewModel EdgeDeletionVM { get; } = new EdgeDeletionViewModel();
        public MitringViewModel MitringVM { get; } = new MitringViewModel();
        public TemperingViewModel TemperingVM { get; } = new TemperingViewModel();
        public PairingViewModel PairingVM { get; } = new PairingViewModel();
        public DoublingLaminationViewModel DoublingLaminationVM { get; } = new DoublingLaminationViewModel();

        public DeliveryNoteViewModel DeliveryNoteVM { get; } = new DeliveryNoteViewModel();
        public TaxInvoiceViewModel TaxInvoiceVM { get; } = new TaxInvoiceViewModel();
        public SalesReportViewModel SalesReportVM { get; } = new SalesReportViewModel();

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set { _currentViewModel = value; OnPropertyChanged(); }
        }

        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set { _isMenuOpen = value; OnPropertyChanged(); }
        }

        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set { _isSettingsOpen = value; OnPropertyChanged(); }
        }

        public bool IsDarkTheme
        {
            get => ThemeManager.CurrentTheme == AppTheme.Dark;
            set
            {
                ThemeManager.ApplyTheme(value ? AppTheme.Dark : AppTheme.Light);
                OnPropertyChanged();
            }
        }

        public ICommand ToggleMenuCommand { get; }
        public ICommand ToggleSettingsCommand { get; }
        public ICommand NavigateCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        public ICommand ShowAboutCommand { get; }

        public MainViewModel()
        {
            // Existing dependency wiring
            ProformaInvoiceVM = new ProformaInvoiceViewModel();
            DashboardVM = new DashboardViewModel(ProformaInvoiceVM);

            ToggleMenuCommand = new RelayCommand(_ =>
            {
                IsMenuOpen = !IsMenuOpen;
                if (IsMenuOpen) IsSettingsOpen = false;
            });

            ToggleSettingsCommand = new RelayCommand(_ =>
            {
                IsSettingsOpen = !IsSettingsOpen;
                if (IsSettingsOpen) IsMenuOpen = false;
            });

            NavigateCommand = new RelayCommand(Navigate);
            ToggleThemeCommand = new RelayCommand(_ => { IsDarkTheme = !IsDarkTheme; });

            ShowAboutCommand = new RelayCommand(_ =>
            {
                CurrentViewModel = AboutVM;
                IsMenuOpen = false;
                IsSettingsOpen = false;
            });

            // Default = Dashboard
            CurrentViewModel = DashboardVM;
        }

        private void Navigate(object parameter)
        {
            switch (parameter?.ToString())
            {
                case "Dashboard": CurrentViewModel = DashboardVM; break;
                case "SheetStore": CurrentViewModel = SheetStoreVM; break;
                case "ProformaInvoice": CurrentViewModel = ProformaInvoiceVM; break;
                case "JobOrder": CurrentViewModel = JobOrderVM; break;
                case "Optimization": CurrentViewModel = OptimizationVM; break;
                case "Cutting": CurrentViewModel = CuttingVM; break;
                case "HolesCutouts": CurrentViewModel = HolesCutoutsVM; break;
                case "Polish": CurrentViewModel = PolishVM; break;
                case "EdgeDeletion": CurrentViewModel = EdgeDeletionVM; break;
                case "Mitring": CurrentViewModel = MitringVM; break;
                case "Tempering": CurrentViewModel = TemperingVM; break;
                case "Pairing": CurrentViewModel = PairingVM; break;
                case "DoublingLamination": CurrentViewModel = DoublingLaminationVM; break;
                case "DeliveryNote": CurrentViewModel = DeliveryNoteVM; break;
                case "TaxInvoice": CurrentViewModel = TaxInvoiceVM; break;
                case "SalesReport": CurrentViewModel = SalesReportVM; break;
                case "About": CurrentViewModel = AboutVM; break;
            }
            IsMenuOpen = false;
        }
    }
}