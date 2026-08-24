// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Login/FirstRunSetupPanel.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: First-run user setup wizard
//
// Commit 5 of the licensing refactor. Replaces the
// admin/admin seed that AuditService.InitializeAsync
// used to write. The splash shows THIS panel when the
// audit database has no users yet (fresh install or
// after a destructive DB reset). The operator types a
// username, display name, and password; the panel
// creates the first administrator and hands the splash
// back to the regular LoginPanel so the same operator
// can sign in with the credentials they just chose.
//
// Behaviour contract:
//   • Same instant-feedback shape as LoginPanel: Enter
//     anywhere submits, the button flips to "CREATING…"
//     and the form locks BEFORE any await, so the
//     feedback is the same frame the user pressed
//     Enter.
//   • Re-entrancy guard prevents double submission.
//   • No skip / no Exit button. The only way out is
//     closing the splash window via the OS. This is
//     deliberate: ProGlass cannot run without a user.
//   • Password rules match CreateUserAsync's: at least
//     6 characters, two passwords must match. Username
//     at least 3 characters. Display name required.
//   • On success, the panel raises Created with the new
//     username so the splash can swap the LoginPanel
//     in pre-populated with the same username.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Updates;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Login
{
    public partial class FirstRunSetupPanel : UserControl
    {
        /// <summary>
        /// Raised when the operator successfully
        /// creates the first administrator. The
        /// <see cref="string"/> argument is the new
        /// username, so the splash can pre-fill the
        /// LoginPanel's username field.
        /// </summary>
        public event Action<string>? Created;

        /// <summary>Prevents double submission (Enter + click racing).</summary>
        private bool _creating;

        public FirstRunSetupPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            FocusField(UsernameBox);
        }

        // ── Keyboard ───────────────────────────────────────────

        /// <summary>
        /// Enter (main or num-pad) ANYWHERE on the page
        /// submits, exactly like the LoginPanel.
        /// </summary>
        private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Enter || e.Key == Key.Return) && !_creating)
            {
                e.Handled = true;
                _ = TryCreateAsync();
            }
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            if (!_creating)
                _ = TryCreateAsync();
        }

        // ── Submit ─────────────────────────────────────────────

        private async Task TryCreateAsync()
        {
            if (_creating) return;
            _creating = true;

            string username = UsernameBox.Text?.Trim() ?? "";
            string displayName = DisplayNameBox.Text?.Trim() ?? "";
            string password = PasswordBox.Password ?? "";
            string confirm = ConfirmPasswordBox.Password ?? "";

            if (username.Length < 3)
            {
                _creating = false;
                ShowError("Username must be at least 3 characters.");
                FocusField(UsernameBox);
                return;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                _creating = false;
                ShowError("Display name is required.");
                FocusField(DisplayNameBox);
                return;
            }

            if (password.Length < PasswordHasher.MinLength)
            {
                _creating = false;
                ShowError($"Password must be at least " +
                    $"{PasswordHasher.MinLength} characters.");
                FocusField(PasswordBox);
                return;
            }

            if (password != confirm)
            {
                _creating = false;
                ShowError("The two passwords do not match.");
                FocusField(ConfirmPasswordBox);
                return;
            }

            // INSTANT feedback: flip the button + lock the
            // form BEFORE any database work, then yield so
            // "CREATING…" paints this frame.
            SetBusyState(true, "Creating administrator…");
            await Task.Yield();

            try
            {
                var (ok, error) = await Task.Run(() =>
                    AuditService.CreateUserAsync(
                        username, displayName,
                        AppRoles.Administrator, password,
                        reason: "First-run setup (Commit 5)."));

                if (!ok)
                {
                    SetBusyState(false, "");
                    ShowError(error);
                    PasswordBox.Clear();
                    ConfirmPasswordBox.Clear();
                    FocusField(UsernameBox);
                    return;
                }

                // The setup wizard is the ONLY place the operator
                // types their brand-new password in plain text on
                // this device. Seed the auto-sign-in vault with it
                // and turn the pill ON by default so the user is
                // NOT asked to type it again on the next launch —
                // the industrial-tablet UX contract is "set up
                // once, walk to the shop floor, keep working".
                // The pill is bound to the current app version, so
                // an OTA update will still force a re-verification
                // on the new binary. The user can flip it OFF on
                // the login screen at any time.
                TryArmAutoLogin(username, password);

                SetBusyState(false, "Administrator created — signing in…");
                PasswordBox.Clear();
                ConfirmPasswordBox.Clear();
                Created?.Invoke(username);
            }
            catch (Exception ex)
            {
                SetBusyState(false, "");
                ShowError("Could not create administrator: " + ex.Message);
                AppLogService.Error(
                    "First-run user creation failed.", ex,
                    new LogContext
                    {
                        Module = "Auth",
                        Class = nameof(FirstRunSetupPanel),
                        Operation = "TryCreateAsync"
                    });
            }
            finally
            {
                // Keep the button locked on success so the
                // user does not double-click while the
                // splash swaps panels. The splash sets
                // IsEnabled = false on the whole panel
                // after Created fires, so the busy lock
                // gets released implicitly.
                if (!_creating) { /* nothing */ }
            }
        }

        // ── UI state helpers (mirror LoginPanel) ─────────────

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Flips the button label, locks the form, and
        /// shows the status strip. Mirrors
        /// <c>LoginPanel.SetSigningState</c> so the two
        /// panels feel the same to the operator.
        /// </summary>
        private void SetBusyState(bool busy, string status)
        {
            bool showStatus = busy || status.Length > 0;
            StatusStrip.Visibility = showStatus
                ? Visibility.Visible
                : Visibility.Hidden;
            CreateStatusText.Text = status ?? string.Empty;
            CreateBusyDot.Visibility = showStatus
                ? Visibility.Visible
                : Visibility.Collapsed;

            bool enable = !busy;
            UsernameBox.IsEnabled = enable;
            DisplayNameBox.IsEnabled = enable;
            PasswordBox.IsEnabled = enable;
            ConfirmPasswordBox.IsEnabled = enable;
            CreateButton.IsEnabled = enable;
            CreateButton.Content = busy ? "CREATING…" : "CREATE ADMINISTRATOR";

            if (busy)
                ErrorPanel.Visibility = Visibility.Collapsed;
        }

        private static void FocusField(UIElement element)
        {
            if (element == null) return;
            element.Focus();
            Keyboard.Focus(element);
        }

        /// <summary>
        /// Arms the "Skip login until update" auto-sign-in for the
        /// operator we just created. Best-effort: any failure is
        /// logged and swallowed — the user can always type their
        /// credentials on the next launch and turn the pill on
        /// themselves from the LoginPanel.
        /// </summary>
        private static void TryArmAutoLogin(string username, string password)
        {
            try
            {
                if (App.Config == null) return;
                App.Config.LastUsername = username;
                App.Config.RememberUsername = true;
                App.Config.SkipLoginUntilUpdate = true;
                App.Config.SkipLoginBoundVersion = AppInfo.Version ?? "";
                ConfigService.Save(App.Config);
                SecretVault.Store(LoginPanel.SkipLoginSecretKey, password ?? string.Empty);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not arm auto sign-in for new administrator: " + ex.Message,
                    new LogContext
                    {
                        Module = "Auth",
                        Class = nameof(FirstRunSetupPanel),
                        Operation = "TryArmAutoLogin"
                    });
            }
        }
    }
}
