using ProGlassAutomation.Commands;
using ProGlassAutomation.Services;
using ProGlassAutomation.ViewModels.About;
using ProGlassAutomation.ViewModels.Dashboard;
using ProGlassAutomation.ViewModels.ProformaInvoice;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentViewModel;
        private bool _isMenuOpen;
        private bool _isSettingsOpen;

        public DashboardViewModel DashboardVM { get; } = new DashboardViewModel();
        public ProformaInvoiceViewModel ProformaInvoiceVM { get; } = new ProformaInvoiceViewModel();
        public AboutViewModel AboutVM { get; } = new AboutViewModel();

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

            // Show About as a module (inside main window)
            ShowAboutCommand = new RelayCommand(_ =>
            {
                CurrentViewModel = AboutVM;
                IsMenuOpen = false;
                IsSettingsOpen = false;
            });

            // Default module = Dashboard
            CurrentViewModel = DashboardVM;
        }

        private void Navigate(object parameter)
        {
            switch (parameter?.ToString())
            {
                case "Dashboard":
                    CurrentViewModel = DashboardVM;
                    break;
                case "ProformaInvoice":
                    CurrentViewModel = ProformaInvoiceVM;
                    break;
                case "About":
                    CurrentViewModel = AboutVM;
                    break;
            }
            IsMenuOpen = false;
        }
    }
}