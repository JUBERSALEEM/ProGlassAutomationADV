// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Login/LoginPanel.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Authentication
//
// Sign-in UI hosted ON the splash window so splash and login never
// fight z-order as two maximized windows. LoginWindow also hosts this
// panel if a standalone window is needed.
//
// Behaviour contract:
//   • ONE action on the page — the SIGN IN button. Enter in either
//     field triggers the identical path.
//   • The instant the user clicks SIGN IN or presses Enter, the button
//     flips to "SIGNING IN…" and the form locks — BEFORE any await, so
//     the feedback is immediate even on a slow database.
//   • A re-entrancy guard prevents double submission (Enter + click
//     racing, or Enter held down).
//   • The status strip only appears while signing in / after success;
//     it is hidden while idle (no permanent "Ready to sign in" text).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Login
{
    public partial class LoginPanel : UserControl
    {
        /// <summary>True only after a verified sign-in.</summary>
        public bool SignedIn { get; private set; }

        /// <summary>Fired once the user signs in (true) or exits (false).</summary>
        public event Action<bool>? Completed;

        /// <summary>Prevents double submission (Enter + click racing).</summary>
        private bool _signingIn;

        public LoginPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public void FocusInitialField()
        {
            if (UsernameBox.Text.Length > 0)
                PasswordBox.Focus();
            else
                UsernameBox.Focus();
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
                    new LogContext { Module = "Auth", Class = nameof(LoginPanel) });
                UsernameBox.Focus();
            }
        }

        /// <summary>Enter in either field = the same single sign-in path.</summary>
        private void Field_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !_signingIn)
                _ = TrySignInAsync();
        }

        private void SignIn_Click(object sender, RoutedEventArgs e)
        {
            if (!_signingIn)
                _ = TrySignInAsync();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            SignedIn = false;
            Completed?.Invoke(false);
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            var reset = new ResetPasswordWindow { Owner = Window.GetWindow(this) };
            reset.ShowDialog();
            PasswordBox.Focus();
        }

        private async Task TrySignInAsync()
        {
            if (_signingIn) return;
            _signingIn = true;

            string username = UsernameBox.Text?.Trim() ?? "";
            string password = PasswordBox.Password ?? "";

            if (username.Length == 0)
            {
                _signingIn = false;
                ShowError("Enter your username.");
                UsernameBox.Focus();
                return;
            }

            // INSTANT feedback: flip the button + lock the form BEFORE any
            // await, so "SIGN IN" becomes "SIGNING IN…" the same frame the
            // user clicks or presses Enter — never after a database round-trip.
            SetSigningState(true, "Signing in…");

            try
            {
                int remaining = await AuditService.LockoutRemainingSecondsAsync(username);
                if (remaining > 0)
                {
                    ShowError($"Too many attempts. Try again in {remaining}s.");
                    return;
                }

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
                            Class = nameof(LoginPanel),
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
                    var force = new ForcePasswordChangeWindow(username)
                    {
                        Owner = Window.GetWindow(this)
                    };
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
                SetSigningState(false, "Signed in — opening workspace…");
                PasswordBox.Clear();
                Completed?.Invoke(true);
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "Sign-in failed unexpectedly.", ex,
                    new LogContext
                    {
                        Module = "Auth",
                        Class = nameof(LoginPanel),
                        Operation = "SignIn"
                    });

                ShowError("Sign-in failed. See the log for details.");
            }
            finally
            {
                if (!SignedIn)
                    SetSigningState(false, "");   // idle — status strip hidden
                _signingIn = false;
            }
        }

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
                    new LogContext { Module = "Auth", Class = nameof(LoginPanel) });
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Updates the button + status strip and locks the form while
        /// credentials are being checked. The status strip is shown ONLY
        /// while signing in / after success — idle shows nothing.
        /// </summary>
        private void SetSigningState(bool busy, string status)
        {
            bool showStatus = busy || SignedIn;
            StatusStrip.Visibility = showStatus
                ? Visibility.Visible
                : Visibility.Collapsed;
            SignInStatusText.Text = status ?? string.Empty;
            SignInBusyDot.Visibility = showStatus
                ? Visibility.Visible
                : Visibility.Collapsed;

            bool enable = !busy && !SignedIn;
            UsernameBox.IsEnabled = enable;
            PasswordBox.IsEnabled = enable;
            RememberUserCheck.IsEnabled = enable;
            ForgotPasswordButton.IsEnabled = enable;
            ExitButton.IsEnabled = enable;
            SignInButton.IsEnabled = enable;
            SignInButton.Content = busy ? "SIGNING IN…" : "SIGN IN";

            if (busy)
                ErrorPanel.Visibility = Visibility.Collapsed;
        }
    }
}
