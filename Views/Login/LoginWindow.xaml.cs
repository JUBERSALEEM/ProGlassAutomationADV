// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Login/LoginWindow.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Authentication
//
// AUTH-1. Closes the gap recorded as Remaining Risks #1: the app had no
// sign-in screen at all. It started as the lowest-Id active user, which
// on a fresh install is an Administrator, so the A-1 permission guards
// authorized an identity that was never established.
//
// DESIGN NOTES (deliberate, and each one is a trade-off):
//
//  • Reuses AuditService.SwitchUserAsync. That method already verifies
//    the password against both hash schemes, migrates legacy SHA-256 on
//    success, sets CurrentUser and writes the audit entry. Writing a
//    second authentication path would have meant two places to get
//    right, so this calls the existing one.
//
//  • Offline-first, single-operator reality: this is a LOCAL identity
//    gate, not a security boundary against an attacker with the machine.
//    Anyone with the SQLite files can read the data regardless. What it
//    buys is an accurate audit trail and real per-role permissions.
//    Claiming more than that would be dishonest.
//
//  • Lockout is in-memory and per-process only. It slows down guessing
//    at the UI; it does not survive a restart. Persisting it would let
//    a mistyped password lock a user out of their own offline data with
//    no recovery path, which is the worse failure for this product.
// ═══════════════════════════════════════════════════════════════════════

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

        private int _failedAttempts;
        private DateTime _lockedUntilUtc = DateTime.MinValue;

        private const int MaxAttemptsBeforeDelay = 3;
        private const int LockoutSeconds = 15;

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            UsernameBox.Focus();

            // Show the first-run hint only while the seeded credential is
            // genuinely still in force, so it disappears once rotated and
            // never advertises a password that no longer works.
            try
            {
                if (await AuditService.IsUsingDefaultPasswordAsync())
                {
                    DefaultCredWarning.Visibility = Visibility.Visible;
                    UsernameBox.Text = AuditService.DefaultAdminUsername;
                    PasswordBox.Focus();
                }
            }
            catch (Exception ex)
            {
                // Never block sign-in because the hint could not be resolved.
                AppLogService.Warning(
                    "Could not determine default-credential state: " + ex.Message,
                    new LogContext { Module = "Auth", Class = nameof(LoginWindow) });
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

        private async Task TrySignInAsync()
        {
            if (DateTime.UtcNow < _lockedUntilUtc)
            {
                int wait = (int)Math.Ceiling(
                    (_lockedUntilUtc - DateTime.UtcNow).TotalSeconds);
                ShowError($"Too many attempts. Try again in {wait}s.");
                return;
            }

            string username = UsernameBox.Text?.Trim() ?? "";
            string password = PasswordBox.Password ?? "";

            if (username.Length == 0)
            {
                ShowError("Enter your username.");
                UsernameBox.Focus();
                return;
            }

            SignInButton.IsEnabled = false;
            SignInButton.Content = "SIGNING IN…";

            try
            {
                var (ok, error) = await AuditService.SwitchUserAsync(
                    username, password);

                if (ok)
                {
                    AuditService.LogSessionBoundary(login: true);
                    SignedIn = true;
                    PasswordBox.Clear();
                    Close();
                    return;
                }

                _failedAttempts++;

                // The message is intentionally the same for an unknown user
                // and a wrong password, so the dialog cannot be used to
                // enumerate valid usernames. The precise reason is in the
                // audit trail, which only an administrator can read.
                ShowError("Incorrect username or password.");

                AppLogService.Warning(
                    $"Failed sign-in attempt for '{username}' "
                    + $"(attempt {_failedAttempts}): {error}",
                    new LogContext
                    {
                        Module = "Auth",
                        Class = nameof(LoginWindow),
                        Operation = "SignIn"
                    });

                if (_failedAttempts >= MaxAttemptsBeforeDelay)
                {
                    _lockedUntilUtc =
                        DateTime.UtcNow.AddSeconds(LockoutSeconds);
                    _failedAttempts = 0;

                    ShowError(
                        $"Too many attempts. Try again in {LockoutSeconds}s.");
                }

                PasswordBox.Clear();
                PasswordBox.Focus();
            }
            catch (Exception ex)
            {
                // A crash here must not become an authentication bypass:
                // SignedIn stays false and the window stays open.
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

        private void ShowError(string message)
        {
            // The message lives inside ErrorPanel (icon + text), so the
            // PANEL is what gets shown — setting the TextBlock's own
            // Visibility would leave an empty bordered box on screen.
            ErrorText.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
        }
    }
}
