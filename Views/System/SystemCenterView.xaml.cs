using ProGlassAutomation.ViewModels.System;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.System
{
    /// <summary>
    /// System Center — Users + Activity Log.
    ///
    /// PasswordBox cannot be data-bound in WPF (by design, for
    /// security), so the two boxes push their values into the VM via
    /// tiny code-behind handlers. Everything else is pure MVVM.
    /// </summary>
    public partial class SystemCenterView : UserControl
    {
        public SystemCenterView()
        {
            InitializeComponent();
        }

        private void NewPasswordBox_PasswordChanged(
            object sender, RoutedEventArgs e)
        {
            if (DataContext is SystemCenterViewModel vm &&
                sender is PasswordBox box)
            {
                vm.NewPassword = box.Password;
            }
        }

        private void SwitchPasswordBox_PasswordChanged(
            object sender, RoutedEventArgs e)
        {
            if (DataContext is SystemCenterViewModel vm &&
                sender is PasswordBox box)
            {
                vm.SwitchPassword = box.Password;
            }
        }
    }
}
