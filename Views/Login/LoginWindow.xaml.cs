// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Login/LoginWindow.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Authentication
//
// Local identity gate. Reuses AuditService.SwitchUserAsync.
// Username may be remembered. Password is NEVER persisted.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Login
{
    public partial class LoginWindow : Window
    {
        /// <summary>True only after a verified sign-in.</summary>
        public bool SignedIn { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                bool remember = App.Config?.RememberUsername ?? true;
                string saved = (App.Config?.LastUsername ?? "").Trim();
                RememberUserCheck.IsChecked = remember;

                if (remember && saved.Length > 0)
                {
                    UsernameBox.Text = saved;
                    PasswordBox.Focus();
                }
                else
                {
                    UsernameBox.Focus();
                }

                // First-run hint only while the seeded credential still works.
                if (await AuditService.IsUsingDefaultPasswordAsync())
                {
                    DefaultCredWarning.Visibility = Visibility.Visible;
                    if (UsernameBox.Text.Length == 0)
                        UsernameBox.Text = AuditService.DefaultAdminUsername;
                    PasswordBox.Focus();
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not restore last username: " + ex.Message,
                    new LogContext { Module = "Auth", Class = nameof(LoginWindow) });
                UsernameBox.Focus();
            }
        }

        private void Field_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) _ = TrySignInAsync();
        }

        private void SignIn_Click(object sender, RoutedEventArgs e)
            => _ = TrySignInAsync();

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            SignedIn = false;
            Close();
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            var reset = new ResetPasswordWindow { Owner = this };
            reset.ShowDialog();
            PasswordBox.Focus();
        }

        private async Task TrySignInAsync()
        {
            string username = UsernameBox.Text?.Trim() ?? "";
            string password = PasswordBox.Password ?? "";

            if (username.Length == 0)
            {
                ShowError("Enter your username.");
                UsernameBox.Focus();
                return;
            }

            int remaining = await AuditService.LockoutRemainingSecondsAsync(username);
            if (remaining > 0)
            {
                ShowError($"Too many attempts. Try again in {remaining}s.");
                return;
            }

            SignInButton.IsEnabled = false;
            SignInButton.Content = "SIGNING IN…";

            try
            {
                var (ok, error) = await AuditService.SwitchUserAsync(
                    username, password);

                if (!ok)
                {
                    if (error != null &&
                        error.StartsWith("Account temporarily locked.",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        ShowError(error);
                    }
                    else
                    {
                        ShowError("Incorrect username or password.");
                    }

                    AppLogService.Warning(
                        $"Failed sign-in attempt for '{username}': {error}",
                        new LogContext
                        {
                            Module = "Auth",
                            Class = nameof(LoginWindow),
                            Operation = "SignIn"
                        });

                    PasswordBox.Clear();
                    PasswordBox.Focus();
                    return;
                }

                PersistRememberedUsername(username);

                bool usingDefault =
                    await AuditService.IsUsingDefaultPasswordAsync(username);
                if (usingDefault)
                {
                    var force = new ForcePasswordChangeWindow(username);
                    bool? changed = force.ShowDialog();
                    if (changed != true)
                    {
                        AuditService.LogSessionBoundary(login: false);
                        SignedIn = false;
                        PasswordBox.Clear();
                        PasswordBox.Focus();
                        ShowError("You must change the default password before continuing.");
                        return;
                    }
                }

                AuditService.LogSessionBoundary(login: true);
                SignedIn = true;
                PasswordBox.Clear();
                Close();
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "Sign-in failed unexpectedly.", ex,
                    new LogContext
                    {
                        Module = "Auth",
                        Class = nameof(LoginWindow),
                        Operation = "SignIn"
                    });

                ShowError("Sign-in failed. See the log for details.");
            }
            finally
            {
                SignInButton.IsEnabled = true;
                SignInButton.Content = "SIGN IN";
            }
        }

        /// <summary>
        /// Stores the username only. Password is never written to disk.
        /// </summary>
        private void PersistRememberedUsername(string username)
        {
            try
            {
                if (App.Config == null) return;
                bool remember = RememberUserCheck.IsChecked == true;
                App.Config.RememberUsername = remember;
                App.Config.LastUsername = remember ? username : "";
                ConfigService.Save(App.Config);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not save last username: " + ex.Message,
                    new LogContext { Module = "Auth", Class = nameof(LoginWindow) });
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
        }
    }
}
