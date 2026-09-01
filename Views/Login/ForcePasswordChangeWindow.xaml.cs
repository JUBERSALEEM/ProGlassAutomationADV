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
            Loaded += (s, e) => CurrentPasswordBox.Focus();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string current = CurrentPasswordBox.Password ?? "";
            string newPassword = NewPasswordBox.Password ?? "";
            string confirm = ConfirmPasswordBox.Password ?? "";

            // Local validation mirrors AuditService.ChangePasswordAsync so the
            // user gets immediate feedback without an extra DB round-trip.
            int minLength =
                ProGlassAutomation.Services.Security.PasswordHasher.MinLength;
            if (newPassword.Length < minLength)
            {
                ShowError(
                    $"Password must be at least {minLength} characters.");
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
                ShowError("Could not change password: " + ex.Message);
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
