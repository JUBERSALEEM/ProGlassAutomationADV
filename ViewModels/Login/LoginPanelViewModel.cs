// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Login/LoginPanelViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Authentication — sign-in panel logic.
//
// 2026-09-02 (Phase 3.7): the sign-in controller logic moved out of
// Views/Login/LoginPanel.xaml.cs into this VM. The view keeps the
// PasswordBox flows (Password is not bindable — the command receives the
// PasswordBox via CommandParameter and the kept Enter-tunnel handler calls
// the same entry point), window-opening callbacks, and focus routing.
//
// Behaviour contract preserved:
//   • ONE action — SIGN IN (button click or Enter anywhere; Enter is
//     intercepted by the view's tunneling PreviewKeyDown and routed to the
//     same SignIn entry, so IsDefault + tunnel cannot double-submit — the
//     IsBusy guard also rejects re-entry).
//   • The instant sign-in starts, the button flips to "SIGNING IN…" and
//     the form locks BEFORE any await (Task.Yield paints the frame).
//   • Status strip only while signing in / after success (Hidden — never
//     Collapsed — so the layout never shifts).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
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

namespace ProGlassAutomation.ViewModels.Login
{
    /// <summary>Sign-in form state + authentication flow.</summary>
    public sealed class LoginPanelViewModel : ViewModelBase
    {
        /// <summary>SecretVault key for the DPAPI-encrypted saved password
        /// (shared with the view's public const for SplashView).</summary>
        public const string SkipLoginSecretKey = "auth.skiploginpassword";

        // ── Results / wiring ───────────────────────────────────────────
        /// <summary>True only after a verified sign-in.</summary>
        public bool SignedIn { get; private set; }

        /// <summary>View wiring: forwards to the panel's Completed event
        /// (true = signed in, false = exit).</summary>
        public Action<bool>? Completed { get; set; }

        /// <summary>View wiring: opens ResetPasswordWindow and refocuses
        /// the password field on close.</summary>
        public Action? RequestPasswordReset { get; set; }

        /// <summary>View wiring: opens ForcePasswordChangeWindow for a
        /// default-password sign-in; returns the dialog result.</summary>
        public Func<string, bool?>? RequestForcePasswordChange { get; set; }

        /// <summary>Which field the view should focus next
        /// ("Username" or "Password"). Raised on change.</summary>
        public string FocusTarget { get; private set; } = "Username";

        // ── Bound form state ───────────────────────────────────────────
        private string _username = "";
        public string Username
        {
            get => _username;
            set { if (value == _username) return; _username = value; OnPropertyChanged(); }
        }

        private bool _rememberUsername = true;
        public bool RememberUsername
        {
            get => _rememberUsername;
            set { if (value == _rememberUsername) return; _rememberUsername = value; OnPropertyChanged(); }
        }

        private bool _skipLogin;
        public bool SkipLogin
        {
            get => _skipLogin;
            set { if (value == _skipLogin) return; _skipLogin = value; OnPropertyChanged(); }
        }

        /// <summary>False while signing in or after success (locks the
        /// whole form).</summary>
        public bool IsFormEnabled { get; private set; } = true;

        public string SignInButtonText { get; private set; } =
            ProGlassAutomation.Services.Localization.LocalizationService
                .Instance.T("Login.SignIn");
        public string StatusText { get; private set; } = "";
        public Visibility StatusVisibility { get; private set; } = Visibility.Hidden;
        public Visibility BusyDotVisibility { get; private set; } = Visibility.Collapsed;
        public string ErrorText { get; private set; } = "";
        public Visibility ErrorVisibility { get; private set; } = Visibility.Collapsed;
        public Visibility DefaultCredWarningVisibility { get; private set; } = Visibility.Collapsed;

        /// <summary>True while a sign-in is in flight (re-entrancy guard).</summary>
        public bool IsBusy { get; private set; }

        public ICommand SignInCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand ForgotPasswordCommand { get; }

        public LoginPanelViewModel()
        {
            SignInCommand = new RelayCommand(p => SignIn(p as PasswordBox));
            ExitCommand = new RelayCommand(_ => Exit());
            ForgotPasswordCommand = new RelayCommand(_ =>
                RequestPasswordReset?.Invoke());
        }

        /// <summary>Single sign-in entry point — used by both the
        /// SignInCommand (button + IsDefault) and the view's Enter-tunnel
        /// handler, so every path shares the guard and the flow.</summary>
        public void SignIn(PasswordBox? passwordBox)
        {
            if (IsBusy) return;
            _ = TrySignInAsync(Username, passwordBox);
        }

        // ── Load (view's Loaded) ────────────────────────────────────────

        /// <summary>Restores remembered username + skip-login pill state,
        /// surfaces the default-credential warning, and sets FocusTarget.
        /// </summary>
        public async Task LoadAsync()
        {
            try
            {
                bool remember = App.Config?.RememberUsername ?? true;
                string saved = (App.Config?.LastUsername ?? "").Trim();
                RememberUsername = remember;
                SkipLogin = App.Config?.SkipLoginUntilUpdate ?? false;
                Username = saved;

                if (await AuditService.IsUsingDefaultPasswordAsync())
                {
                    DefaultCredWarningVisibility = Visibility.Visible;
                    OnPropertyChanged(nameof(DefaultCredWarningVisibility));
                    if (Username.Length == 0)
                        Username = AuditService.DefaultAdminUsername;
                    SetFocusTarget("Password");
                }
                else
                {
                    SetFocusTarget(
                        remember && saved.Length > 0 ? "Password" : "Username");
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not restore last username: " + ex.Message,
                    new LogContext { Module = "Auth", Class = nameof(LoginPanelViewModel) });
                SetFocusTarget("Username");
            }
        }

        // ── Sign-in flow ────────────────────────────────────────────────

        private async Task TrySignInAsync(string username, PasswordBox? passwordBox)
        {
            if (IsBusy) return;
            IsBusy = true;

            username = username?.Trim() ?? "";
            string password = passwordBox?.Password ?? "";

            if (username.Length == 0)
            {
                IsBusy = false;
                ShowError("Enter your username.");
                SetFocusTarget("Username");
                return;
            }

            // INSTANT feedback: flip the button + lock the form BEFORE any
            // database work. Task.Yield() hands control back to the renderer
            // so "SIGNING IN…" is painted THIS frame (SQLite/EF 'async'
            // calls complete synchronously, so without it the button never
            // showed the busy state until the flow finished).
            SetSigningState(true, "Signing in…");
            await Task.Yield();

            try
            {
                // All DB + PBKDF2 hash work runs off the UI thread so the
                // button never stutters (SQLite calls are thread-safe here —
                // each AuditService method creates its own DbContext).
                int remaining = await Task.Run(
                    () => AuditService.LockoutRemainingSecondsAsync(username));

                if (remaining > 0)
                {
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
                            Class = nameof(LoginPanelViewModel),
                            Operation = "SignIn"
                        });

                    passwordBox?.Clear();
                    SetFocusTarget("Password");
                    return;
                }

                PersistRememberedUsername(username);
                PersistSkipLoginChoice(password);

                bool usingDefault = await Task.Run(
                    () => AuditService.IsUsingDefaultPasswordAsync(username));
                if (usingDefault)
                {
                    bool? changed =
                        RequestForcePasswordChange?.Invoke(username);
                    if (changed != true)
                    {
                        AuditService.LogSessionBoundary(login: false);
                        SignedIn = false;
                        passwordBox?.Clear();
                        SetFocusTarget("Password");
                        ShowError("You must change the default password before continuing.");
                        return;
                    }
                }

                AuditService.LogSessionBoundary(login: true);
                SignedIn = true;
                SetSigningState(false, "Signed in — opening workspace…");
                passwordBox?.Clear();
                Completed?.Invoke(true);
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "Sign-in failed unexpectedly.", ex,
                    new LogContext
                    {
                        Module = "Auth",
                        Class = nameof(LoginPanelViewModel),
                        Operation = "SignIn"
                    });

                ShowError("Sign-in failed. See the log for details.");
            }
            finally
            {
                if (!SignedIn)
                    SetSigningState(false, "");   // idle — status strip hidden
                IsBusy = false;
            }
        }

        private void Exit()
        {
            SignedIn = false;
            Completed?.Invoke(false);
        }

        private void PersistRememberedUsername(string username)
        {
            try
            {
                if (App.Config == null) return;
                App.Config.RememberUsername = RememberUsername;
                App.Config.LastUsername = RememberUsername ? username : "";
                ConfigService.Save(App.Config);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not save last username: " + ex.Message,
                    new LogContext { Module = "Auth", Class = nameof(LoginPanelViewModel) });
            }
        }

        /// <summary>Persists (or clears) the "skip login until update"
        /// auto-sign-in credentials. Called AFTER AuditService verified the
        /// combo, so bad credentials are never written to disk. When the
        /// pill is OFF this actively CLEARS any previously stored blob and
        /// unbinds the version so opting out is clean.</summary>
        private void PersistSkipLoginChoice(string password)
        {
            try
            {
                if (App.Config == null) return;

                if (SkipLogin)
                {
                    // Bind the auto-sign-in to the running app version. An
                    // update ships new binaries, so the auto-sign-in is
                    // deliberately voided and the user re-verifies.
                    App.Config.SkipLoginUntilUpdate = true;
                    App.Config.SkipLoginBoundVersion = AppInfo.Version ?? "";
                    // Store password in the DPAPI-encrypted vault — never
                    // in the plain-text config JSON.
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
                        Class = nameof(LoginPanelViewModel),
                        Operation = "PersistSkipLoginChoice"
                    });
            }
        }

        private void ShowError(string message)
        {
            ErrorText = message;
            ErrorVisibility = Visibility.Visible;
            OnPropertyChanged(nameof(ErrorText));
            OnPropertyChanged(nameof(ErrorVisibility));
        }

        /// <summary>Updates the button + status strip and locks the form
        /// while credentials are being checked. Status strip shows ONLY
        /// while signing in / after success — Hidden (never Collapsed)
        /// keeps its space reserved so the layout never shifts.</summary>
        private void SetSigningState(bool busy, string status)
        {
            bool showStatus = busy || SignedIn;
            StatusVisibility = showStatus ? Visibility.Visible : Visibility.Hidden;
            StatusText = status ?? string.Empty;
            BusyDotVisibility = showStatus ? Visibility.Visible : Visibility.Collapsed;

            IsFormEnabled = !busy && !SignedIn;
            SignInButtonText = busy
                ? "SIGNING IN…"
                : ProGlassAutomation.Services.Localization.LocalizationService
                    .Instance.T("Login.SignIn");

            if (busy)
                ErrorVisibility = Visibility.Collapsed;

            OnPropertyChanged(nameof(StatusVisibility));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(BusyDotVisibility));
            OnPropertyChanged(nameof(IsFormEnabled));
            OnPropertyChanged(nameof(SignInButtonText));
            if (busy)
                OnPropertyChanged(nameof(ErrorVisibility));
        }

        private void SetFocusTarget(string target)
        {
            if (FocusTarget == target) return;
            FocusTarget = target;
            OnPropertyChanged(nameof(FocusTarget));
        }
    }
}
