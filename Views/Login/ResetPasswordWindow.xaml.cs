// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Login/ResetPasswordWindow.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Authentication
//
// PHASE 13 / forgot-password — admin-mediated password reset.
//
// This is the honest recovery path for a local, offline ERP: there is no
// email address to mail a reset link to, so the gate is the local
// Administrator's live credential. The Administrator proves their own
// password, then sets a fresh one for the target account (and any persisted
// lockout on that account is cleared).
//
// Passwords are read straight from the PasswordBoxes and never stored in a
// field — they are passed directly to AuditService.AdminResetPasswordAsync.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Audit;
using System.Windows;

namespace ProGlassAutomation.Views.Login
{
    public partial class ResetPasswordWindow : Window
    {
        public ResetPasswordWindow()
        {
            InitializeComponent();
            AdminUsernameBox.Text = AuditService.DefaultAdminUsername;
            Loaded += (s, e) => AdminPasswordBox.Focus();
        }

        private async void Reset_Click(object sender, RoutedEventArgs e)
        {
            string adminUsername = AdminUsernameBox.Text?.Trim() ?? "";
            string adminPassword = AdminPasswordBox.Password ?? "";
            string targetUsername = TargetUsernameBox.Text?.Trim() ?? "";
            string newPassword = NewPasswordBox.Password ?? "";
            string confirm = ConfirmPasswordBox.Password ?? "";

            if (adminUsername.Length == 0)
            {
                ShowError("Enter the Administrator username.");
                AdminUsernameBox.Focus();
                return;
            }
            if (targetUsername.Length == 0)
            {
                ShowError("Enter the account to reset.");
                TargetUsernameBox.Focus();
                return;
            }

            int minLength =
                ProGlassAutomation.Services.Security.PasswordHasher.MinLength;
            if (newPassword.Length < minLength)
            {
                ShowError($"Password must be at least {minLength} characters.");
                NewPasswordBox.Focus();
                return;
            }
            if (newPassword != confirm)
            {
                ShowError("New password and confirmation do not match.");
                ConfirmPasswordBox.Focus();
                return;
            }

            ResetButton.IsEnabled = false;
            ResetButton.Content = "RESETTING…";

            try
            {
                var (ok, error) = await AuditService.AdminResetPasswordAsync(
                    adminUsername, adminPassword, targetUsername, newPassword,
                    reason: "Login screen forgot-password reset.");

                if (ok)
                {
                    DialogResult = true;
                    return;
                }

                ShowError(error);
                AdminPasswordBox.Clear();
                NewPasswordBox.Clear();
                ConfirmPasswordBox.Clear();
            }
            catch (System.Exception ex)
            {
                ShowError("Could not reset password: " + ex.Message);
            }
            finally
            {
                ResetButton.IsEnabled = true;
                ResetButton.Content = "RESET PASSWORD";
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
        }
    }
}
