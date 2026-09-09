// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Login/ForcePasswordChangeWindow.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Authentication
//
// PHASE 13 — forced first-run password rotation.
//
// Shown by LoginWindow when the authenticated user still signs in with the
// seeded default credential (admin/admin). The app does NOT open until the
// password has been rotated to a real one. "Sign out" keeps the user on the
// login screen (SignedIn stays false).
//
// Passwords are read straight from the PasswordBoxes and never stored in a
// field — they are passed directly to AuditService.ChangePasswordAsync.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Utilities;
using System.Threading.Tasks;
using System.Windows;

namespace ProGlassAutomation.Views.Login
{
    public partial class ForcePasswordChangeWindow : Window
    {
        private readonly string _username;

        /// <summary>True only after a successful password rotation.</summary>
        public bool PasswordChanged { get; private set; }

        public ForcePasswordChangeWindow(string username)
        {
            InitializeComponent();
            _username = username ?? "";
            UsernameBox.Text = _username;
            // Read from the policy so the hint number can never drift from
            // the enforced rule again (it said 8 while the rule was 6).
            PasswordHintText.Text =
                ProGlassAutomation.Services.Security.PasswordPolicy.HintText;
            Loaded += (s, e) => CurrentPasswordBox.Focus();
        }

        private void NewPasswordBox_PasswordChanged(
            object sender, RoutedEventArgs e) =>
            Shared.PasswordMeterHelper.Update(
                PasswordMeter, PasswordMeterLabel, PasswordGuidance,
                NewPasswordBox.Password, _username);

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string current = CurrentPasswordBox.Password ?? "";
            string newPassword = NewPasswordBox.Password ?? "";
            string confirm = ConfirmPasswordBox.Password ?? "";

            // Local validation mirrors AuditService.ChangePasswordAsync via the
            // same single home (PasswordPolicy) so the user gets immediate
            // feedback without an extra DB round-trip — and without drift.
            var policy = ProGlassAutomation.Services.Security.PasswordPolicy
                .ValidateNewPassword(newPassword, _username);
            if (!policy.Accepted)
            {
                ShowError(policy.Error);
                NewPasswordBox.Focus();
                return;
            }

            if (newPassword != confirm)
            {
                ShowError("New password and confirmation do not match.");
                ConfirmPasswordBox.Focus();
                return;
            }

            if (current == newPassword)
            {
                ShowError("New password must differ from the current one.");
                NewPasswordBox.Focus();
                return;
            }

            SaveButton.IsEnabled = false;
            SaveButton.Content = "SAVING…";

            try
            {
                var (ok, error) = await AuditService.ChangePasswordAsync(
                    _username, current, newPassword,
                    reason: "Forced first-run rotation (default credential).");

                if (ok)
                {
                    PasswordChanged = true;
                    DialogResult = true;
                    return;
                }

                ShowError(error);
                PasswordBox_Reset();
            }
            catch (System.Exception ex)
            {
                ShowError("Could not change password: " + ExceptionMessages.Deep(ex));
                PasswordBox_Reset();
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveButton.Content = "SAVE & CONTINUE";
            }
        }

        private void PasswordBox_Reset()
        {
            NewPasswordBox.Clear();
            ConfirmPasswordBox.Clear();
            NewPasswordBox.Focus();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
        }
    }
}
