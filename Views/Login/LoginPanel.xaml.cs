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
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Updates;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Login
{
    public partial class LoginPanel : UserControl
    {
        // ══════════════════════════════════════════════════════════════
        //  AUTO-SIGN-IN CONTRACT (skip-login pill)
        //
        //  When the user toggles "Skip login until update" ON and signs
        //  in successfully, this control:
        //    1. Stores the password DPAPI-encrypted (CurrentUser scope)
        //       in SecretVault under SkipLoginSecretKey. DPAPI means
        //       the blob is unreadable by any OTHER Windows account,
        //       even on the same machine.
        //    2. Records the current AppInfo.Version into
        //       AppConfig.SkipLoginBoundVersion. The next launch
        //       compares this to the running version; a fresh install
        //       or an OTA update invalidates the auto-sign-in so the
        //       user re-verifies on the new binary.
        //    3. Sets AppConfig.SkipLoginUntilUpdate = true and persists.
        //
        //  SplashView.TryAutoLoginAsync consumes those three fields on
        //  the next startup. If any one of them fails to line up the
        //  regular login form is shown — never fall through silently
        //  to a signed-in state without a matched credential.
        // ══════════════════════════════════════════════════════════════

        /// <summary>SecretVault key for the DPAPI-encrypted saved password.</summary>
        public const string SkipLoginSecretKey = "auth.skiploginpassword";

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
            var target = UsernameBox.Text.Length > 0
                ? (UIElement)PasswordBox
                : UsernameBox;
            FocusField(target);
        }

        /// <summary>
        /// Sets BOTH logical and keyboard focus (a control in a window that
        /// is not keyboard-active otherwise ignores Focus()). Also re-arms
        /// once layout and the login fade have settled, so the caret is
        /// guaranteed to be in the field the moment the page appears.
        /// </summary>
        public void FocusInitialFieldDeferred()
        {
            FocusInitialField();
            Dispatcher.BeginInvoke(new Action(FocusInitialField),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private static void FocusField(UIElement element)
        {
            if (element == null) return;
            element.Focus();
            Keyboard.Focus(element);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                bool remember = App.Config?.RememberUsername ?? true;
                string saved = (App.Config?.LastUsername ?? "").Trim();
                RememberUserCheck.IsChecked = remember;

                // Restore the skip-login pill state so a user who
                // previously armed it sees the same choice, even
                // when the auto-login itself was skipped (e.g. after
                // an update reset SkipLoginBoundVersion). The pill's
                // current value drives whether we RE-arm it on the
                // next successful sign-in — not whether we already
                // are.
                SkipLoginPill.IsChecked = App.Config?.SkipLoginUntilUpdate ?? false;

                if (remember && saved.Length > 0)
                {
                    UsernameBox.Text = saved;
                    FocusField(PasswordBox);
                }
                else
                {
                    FocusField(UsernameBox);
                }

                if (await AuditService.IsUsingDefaultPasswordAsync())
                {
                    DefaultCredWarning.Visibility = Visibility.Visible;
                    if (UsernameBox.Text.Length == 0)
                        UsernameBox.Text = AuditService.DefaultAdminUsername;
                    FocusField(PasswordBox);
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not restore last username: " + ex.Message,
                    new LogContext { Module = "Auth", Class = nameof(LoginPanel) });
                FocusField(UsernameBox);
            }
        }

        /// <summary>
        /// Enter (main or num-pad) ANYWHERE on the page submits — exactly
        /// like a Facebook/Instagram login. Tunnels from the root, so it
        /// works even when no field has keyboard focus.
        /// </summary>
        private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Enter || e.Key == Key.Return) && !_signingIn)
            {
                e.Handled = true;
                _ = TrySignInAsync();
            }
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
            FocusField(PasswordBox);
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
                FocusField(UsernameBox);
                return;
            }

            // INSTANT feedback: flip the button + lock the form BEFORE any
            // database work. Task.Yield() hands control back to the renderer
            // so "SIGNING IN…" is painted THIS frame. Without it, the frame
            // is swallowed: SQLite/EF 'async' calls complete synchronously,
            // so the whole sign-in ran as one UI-thread block and the button
            // never showed "SIGNING IN…" until the flow finished (the lag).
            SetSigningState(true, "Signing in…");
            await Task.Yield();

            try
            {
                // All DB + PBKDF2 hash work runs off the UI thread so the
                // button never stutters and the form stays responsive while
                // credentials verify (SQLite calls are thread-safe here —
                // each AuditService method creates its own DbContext).
                int remaining = await Task.Run(
                    () => AuditService.LockoutRemainingSecondsAsync(username));

                if (remaining > 0)
                {
                    // Return to the idle state FIRST so the error is never
                    // shown while the button still reads "SIGNING IN…" and
                    // the form is still locked.
                    SetSigningState(false, "");
                    ShowError($"Too many attempts. Try again in {remaining}s.");
                    return;
                }

                var (ok, error) = await Task.Run(
                    () => AuditService.SwitchUserAsync(username, password));

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
                    FocusField(PasswordBox);
                    return;
                }

                PersistRememberedUsername(username);
                PersistSkipLoginChoice(username, password);

                bool usingDefault = await Task.Run(
                    () => AuditService.IsUsingDefaultPasswordAsync(username));
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
                        FocusField(PasswordBox);
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

        /// <summary>
        /// Persists (or clears) the "skip login until update"
        /// auto-sign-in credentials for this device. Called AFTER
        /// AuditService has verified the username/password combo,
        /// so we never write bad credentials to disk.
        ///
        /// When the pill is OFF this method actively CLEARS any
        /// previously stored blob and unbinds the version so a
        /// user who once opted in can also cleanly opt out.
        /// </summary>
        private void PersistSkipLoginChoice(string username, string password)
        {
            try
            {
                if (App.Config == null) return;
                bool skip = SkipLoginPill.IsChecked == true;

                if (skip)
                {
                    // Bind the auto-sign-in to the running app
                    // version. An update ships new binaries, so
                    // the auto-sign-in is deliberately voided
                    // and the user re-verifies on the new build.
                    App.Config.SkipLoginUntilUpdate = true;
                    App.Config.SkipLoginBoundVersion = AppInfo.Version ?? "";
                    // Store password in the DPAPI-encrypted vault —
                    // never in the plain-text config JSON.
                    SecretVault.Store(SkipLoginSecretKey, password ?? string.Empty);
                }
                else
                {
                    App.Config.SkipLoginUntilUpdate = false;
                    App.Config.SkipLoginBoundVersion = "";
                    SecretVault.Remove(SkipLoginSecretKey);
                }

                ConfigService.Save(App.Config);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not persist skip-login choice: " + ex.Message,
                    new LogContext
                    {
                        Module = "Auth",
                        Class = nameof(LoginPanel),
                        Operation = "PersistSkipLoginChoice"
                    });
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
        /// Hidden (not Collapsed) keeps the strip's space reserved so the
        /// button / footer / version line never shift when signing starts.
        /// </summary>
        private void SetSigningState(bool busy, string status)
        {
            bool showStatus = busy || SignedIn;
            StatusStrip.Visibility = showStatus
                ? Visibility.Visible
                : Visibility.Hidden;
            SignInStatusText.Text = status ?? string.Empty;
            SignInBusyDot.Visibility = showStatus
                ? Visibility.Visible
                : Visibility.Collapsed;

            bool enable = !busy && !SignedIn;
            UsernameBox.IsEnabled = enable;
            PasswordBox.IsEnabled = enable;
            RememberUserCheck.IsEnabled = enable;
            SkipLoginPill.IsEnabled = enable;
            ForgotPasswordButton.IsEnabled = enable;
            ExitButton.IsEnabled = enable;
            SignInButton.IsEnabled = enable;
            SignInButton.Content = busy ? "SIGNING IN…" : "SIGN IN";

            if (busy)
                ErrorPanel.Visibility = Visibility.Collapsed;
        }
    }
}
