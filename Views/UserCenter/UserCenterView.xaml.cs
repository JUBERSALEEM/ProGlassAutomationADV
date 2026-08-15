// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/UserCenter/UserCenterView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: User Center (Phase 1)
//
// Code-behind only for the pieces WPF cannot bind: PasswordBoxes.
// Passwords flow straight from the boxes into the VM methods and are
// never stored in view-model properties.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.UserCenter;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.UserCenter
{
    public partial class UserCenterView : UserControl
    {
        public UserCenterView()
        {
            InitializeComponent();
        }

        private UserCenterViewModel? Vm => DataContext as UserCenterViewModel;

        private void UserCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (Vm == null) return;
            if (sender is FrameworkElement el &&
                el.Tag is UserCard card)
            {
                Vm.SelectUserCommand.Execute(card);
            }
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
    }
}
