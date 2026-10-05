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
//     field triggers the identical path (the tunneling PreviewKeyDown
//     below routes it to the same VM entry as the button command).
//   • The instant the user clicks SIGN IN or presses Enter, the button
//     flips to "SIGNING IN…" and the form locks — BEFORE any await.
//   • A re-entrancy guard (VM IsBusy) prevents double submission.
//   • The status strip only appears while signing in / after success.
//
// 2026-09-02 (Phase 3.7): sign-in state + flow moved to
// LoginPanelViewModel. This file keeps the PasswordBox flows (Password
// is not bindable — the command receives the PasswordBox via
// CommandParameter), the window-opening callbacks, focus routing
// (VM.FocusTarget), and the public host contract (Completed /
// FocusInitialField* / SkipLoginSecretKey) so SplashView and
// LoginWindow are untouched.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.Login;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Login
{
    public partial class LoginPanel : UserControl
    {
        // ══════════════════════════════════════════════════════════════
        //  AUTO-SIGN-IN CONTRACT (skip-login pill) — see the VM header
        //  for the full DPAPI / version-bound design. SplashView reads
        //  this key on the next startup; if any field fails to line up
        //  the regular login form is shown — never fall through
        //  silently to a signed-in state without a matched credential.
        // ══════════════════════════════════════════════════════════════

        /// <summary>SecretVault key for the DPAPI-encrypted saved password.</summary>
        public const string SkipLoginSecretKey = LoginPanelViewModel.SkipLoginSecretKey;

        private readonly LoginPanelViewModel _vm = new();

        /// <summary>True only after a verified sign-in.</summary>
        public bool SignedIn => _vm.SignedIn;

        /// <summary>Fired once the user signs in (true) or exits (false).</summary>
        public event Action<bool>? Completed;

        public LoginPanel()
        {
            InitializeComponent();
            DataContext = _vm;

            _vm.Completed = result => Completed?.Invoke(result);
            _vm.RequestPasswordReset = () =>
            {
                var reset = new ResetPasswordWindow { Owner = Window.GetWindow(this) };
                reset.ShowDialog();
                FocusField(PasswordBox);
            };
            _vm.RequestForcePasswordChange = username =>
            {
                var force = new ForcePasswordChangeWindow(username)
                {
                    Owner = Window.GetWindow(this)
                };
                return force.ShowDialog();
            };
            _vm.PropertyChanged += OnVmPropertyChanged;

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
            await _vm.LoadAsync();
            FocusField(_vm.FocusTarget == "Password"
                ? (UIElement)PasswordBox
                : UsernameBox);
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LoginPanelViewModel.FocusTarget))
                FocusField(_vm.FocusTarget == "Password"
                    ? (UIElement)PasswordBox
                    : UsernameBox);
        }

        /// <summary>
        /// Enter (main or num-pad) ANYWHERE on the page submits — exactly
        /// like a Facebook/Instagram login. Tunnels from the root, so it
        /// works even when no field has keyboard focus, and marks the
        /// event handled so the IsDefault button never double-fires.
        /// (By-design keep: tunneling key interception is view input
        /// handling; it routes to the same VM entry as the button.)
        /// </summary>
        private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Enter || e.Key == Key.Return) && !_vm.IsBusy)
            {
                e.Handled = true;
                _vm.SignIn(PasswordBox);
            }
        }
    }
}
