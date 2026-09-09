// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/UserCenter/UserCenterView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: User Center (Phase 1)
//
// Code-behind only for the pieces WPF cannot bind: PasswordBoxes.
// Passwords flow straight from the boxes into the VM methods and are
// never stored in view-model properties.
//
// The strength meters are driven here too (PasswordChanged →
// PasswordMeterHelper): the score comes from PasswordPolicy, the same
// single home AuditService enforces, so the meter and the rule cannot
// disagree. The username/display-name the policy checks against are read
// live from the VM, and the create-meter refreshes when they change.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.UserCenter;
using ProGlassAutomation.Views.Shared;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.UserCenter
{
    public partial class UserCenterView : UserControl
    {
        public UserCenterView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private UserCenterViewModel? Vm => DataContext as UserCenterViewModel;

        private void OnDataContextChanged(
            object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is UserCenterViewModel oldVm)
                oldVm.PropertyChanged -= OnVmPropertyChanged;
            if (e.NewValue is UserCenterViewModel newVm)
                newVm.PropertyChanged += OnVmPropertyChanged;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // The create-meter's identity check depends on the username /
            // display name being typed, so it refreshes as they change.
            if (e.PropertyName == nameof(UserCenterViewModel.Username) ||
                e.PropertyName == nameof(UserCenterViewModel.DisplayName))
                RefreshCreateMeter();
        }

        private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e) =>
            RefreshCreateMeter();

        private void RefreshCreateMeter()
        {
            if (Vm == null) return;
            PasswordMeterHelper.Update(
                NewPasswordMeter, NewPasswordMeterLabel, NewPasswordGuidance,
                NewPasswordBox.Password, Vm.Username, Vm.DisplayName);
        }

        private void ChangeNewPasswordBox_PasswordChanged(
            object sender, RoutedEventArgs e)
        {
            PasswordMeterHelper.Update(
                ChangePasswordMeter, ChangePasswordMeterLabel, ChangePasswordGuidance,
                ChangeNewPasswordBox.Password);
        }

        private async void CreateUser_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            await Vm.CreateUserAsync(
                NewPasswordBox.Password,
                ConfirmPasswordBox.Password);
            NewPasswordBox.Clear();
            ConfirmPasswordBox.Clear();
        }

        private async void SwitchUser_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            await Vm.SwitchToSelectedAsync(SwitchPasswordBox.Password);
            SwitchPasswordBox.Clear();
        }

        /// <summary>
        /// A-3 — rotate the selected user's password. Boxes are cleared on
        /// every path so a plaintext password is never left on screen.
        /// </summary>
        private async void ChangePassword_Click(
            object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            try
            {
                await Vm.ChangePasswordAsync(
                    CurrentPasswordBox.Password,
                    ChangeNewPasswordBox.Password,
                    ChangeConfirmPasswordBox.Password);
            }
            finally
            {
                CurrentPasswordBox.Clear();
                ChangeNewPasswordBox.Clear();
                ChangeConfirmPasswordBox.Clear();
            }
        }
    }
}
