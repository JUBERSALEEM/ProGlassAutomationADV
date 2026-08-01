// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/SettingsView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings (Chat 23 v3.1 — NEW module)
// VERSION: Chat 23 v3.1 — PasswordBox handler + on-load key restore
//
// PURPOSE:
//   PasswordBox cannot bind to a plain string property because
//   PasswordBox.Password is not a DependencyProperty (by design, for
//   security). This code-behind bridges the PasswordBox to the VM's
//   AiApiKey property.
//
//   Also: on Loaded, restore the saved API key into the PasswordBox
//   so the user sees their key is remembered.
//
// PATTERN:
//   Same as v3.0 MainWindow.xaml.cs's ApiKeyBox_PasswordChanged handler,
//   just relocated to this view.
//
// REGRESSION RISK: ZERO
//   New file. Same handler pattern proven in v3.0.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.Settings;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Settings
{
    /// <summary>
    /// Code-behind for the Settings view.
    /// Owns only the PasswordBox bridge (WPF security constraint).
    /// All other logic lives in <see cref="SettingsViewModel"/>.
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();

            // On Loaded: restore saved API key into the PasswordBox so
            // the user visually confirms their key is remembered
            Loaded += (_, __) =>
            {
                if (DataContext is SettingsViewModel vm &&
                    !string.IsNullOrEmpty(vm.AiApiKey))
                {
                    ApiKeyBox.Password = vm.AiApiKey;
                }
            };
        }

        /// <summary>
        /// Pushes the PasswordBox contents into the VM's AiApiKey
        /// property, which persists to disk via ConfigService.Save.
        /// </summary>
        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm && sender is PasswordBox pb)
            {
                vm.AiApiKey = pb.Password;
            }
        }
    }
}