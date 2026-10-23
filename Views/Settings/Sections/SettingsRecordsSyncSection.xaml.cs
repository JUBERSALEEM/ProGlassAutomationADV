using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ProGlassAutomation.ViewModels.Settings.Pages;

namespace ProGlassAutomation.Views.Settings.Sections
{
    /// <summary>Presentation-only wiring for the non-bindable PasswordBox.</summary>
    public partial class SettingsRecordsSyncSection : UserControl
    {
        private SettingsRecordsSyncPageViewModel? _viewModel;

        public SettingsRecordsSyncSection()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += (_, _) => Attach(DataContext as SettingsRecordsSyncPageViewModel);
            Unloaded += (_, _) => Attach(null);
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsLoaded)
                Attach(e.NewValue as SettingsRecordsSyncPageViewModel);
        }

        private void Attach(SettingsRecordsSyncPageViewModel? viewModel)
        {
            if (_viewModel == viewModel)
                return;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel.ApiKeyInput = string.Empty;
                _viewModel.PasswordInput = string.Empty;
            }

            _viewModel = viewModel;
            PasswordInputBox.Password = string.Empty;
            if (_viewModel != null)
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsRecordsSyncPageViewModel.PasswordInput) &&
                _viewModel != null &&
                string.IsNullOrEmpty(_viewModel.PasswordInput) &&
                !string.IsNullOrEmpty(PasswordInputBox.Password))
            {
                PasswordInputBox.Password = string.Empty;
            }
        }

        private void PasswordInputBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null && sender is PasswordBox passwordBox)
                _viewModel.PasswordInput = passwordBox.Password;
        }
    }
}
