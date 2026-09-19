// ═══════════════════════════════════════════════════════════════════
// FILE: Views/Splash/SplashView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Splash Screen — "Split Diagonal" (v3, 2026-09-19)
//
// ENTRANCE CHOREOGRAPHY (v3, ~2.3s, finishes inside App startup delay):
//   0ms     captions bottom-right — MADE IN INDIA / ENTERPRISE /
//           OFFLINE · SECURE (staggered 0/150/300, 350ms each)
//   200ms   hexagon logo settles (fade + scale 0.92→1)
//   450ms   two-line wordmark fades + slides up (14px drift)
//   850ms   vertical progress track fades in on the split edge
//   390ms   sweep begins — 2px fill grows bottom→top over ~1.89s
//           (smoothstep, 30ms DispatcherTimer)
//
// PRESERVED (zero regression — App.StartupCore.cs depends on these):
//   • FadeOutAndClose(Action), RunLoginAsync(), RunFirstRunSetupAsync(),
//     ShowTermsGate()
//   • TermsAccepted / TermsResolved / SignedIn / HasAcceptedCurrentTerms
//   • TermsVersion, RecordAcceptance
//   • Auto sign-in (skip-login pill) fast path, first-run flow
//
// PERFORMANCE:
//   • One 30ms DispatcherTimer drives the sweep — no Storyboard
//     machinery, no allocations in the tick.
//   • No chips, no dots, no percent readout (approved minimal design).
// ═══════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.ViewModels.Login;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Updates;
using ProGlassAutomation.Views.Login;
using System;
using ProGlassAutomation.Utilities;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ProGlassAutomation.Views.Splash
{
    /// <summary>
    /// Splash window shown at application startup.
    /// "Split Diagonal" design (owner-approved mockup). Animates in over
    /// ~2.3 seconds while App performs background initialization. When
    /// the main window is ready, App calls <see cref="FadeOutAndClose"/>
    /// to dismiss the splash.
    /// </summary>
    public partial class SplashView : Window
    {
        // ── Vertical progress sweep (height, bottom→top) ──────────────
        private DispatcherTimer? _progressSweepTimer;
        private int _sweepTicks;
        private double _progressTrackHeight = 200;

        // 30ms tick · ~13 tick lead-in (~390ms hold at 0) · 63 ticks of
        // motion (~1890ms) → sweep window 390–2280ms.
        private const int SweepLeadTicks = 13;
        private const int SweepMotionTicks = 63;

        public SplashView()
        {
            InitializeComponent();

            // Phase 3.7: terms gate is VM-backed (state + commands);
            // the view keeps the window-side side effects.
            TermsBar.DataContext = _termsVm;
            _termsVm.RequestContinue = () =>
            {
                RecordAcceptance();
                TermsPanel.Visibility = Visibility.Collapsed;
                TermsResolved?.Invoke();
            };
            _termsVm.RequestDecline = () =>
            {
                TermsResolved?.Invoke();
                Close();
            };

            Loaded += SplashView_Loaded;
            Unloaded += SplashView_Unloaded;
        }

        // ══════════════════════════════════════════════════════════════
        //  LOADED — start animation sequence (calm, ~2.3s)
        // ══════════════════════════════════════════════════════════════

        private void SplashView_Loaded(object sender, RoutedEventArgs e)
        {
            // Sweep needs the track's real height (200 in XAML; the
            // guard keeps it safe on odd DPI rounds).
            double track = ProgressTrack.ActualHeight;
            _progressTrackHeight = track < 80 ? 200 : track;

            // ─── 1) CAPTIONS — quiet chrome, staggered (0–650ms) ──────
            FadeIn(CaptionMadeIn, fromMs: 0, durationMs: 350);
            FadeIn(CaptionEnterprise, fromMs: 150, durationMs: 350);
            FadeIn(CaptionSecure, fromMs: 300, durationMs: 350);

            // ─── 2) LOGO settles in (200–700ms) ───────────────────────
            var logoScale = new DoubleAnimation
            {
                From = 0.92,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(200),
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            LogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, logoScale);
            LogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, logoScale.Clone());

            var logoFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(200),
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            LogoBorder.BeginAnimation(OpacityProperty, logoFade);

            // ─── 3) WORDMARK fade + slide up (450–950ms) ──────────────
            var nameFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(500),
                BeginTime = TimeSpan.FromMilliseconds(450),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var nameSlide = new DoubleAnimation
            {
                From = 14,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(500),
                BeginTime = TimeSpan.FromMilliseconds(450),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            AppNameText.BeginAnimation(OpacityProperty, nameFade);
            AppNameTranslate.BeginAnimation(TranslateTransform.YProperty, nameSlide);

            // ─── 4) PROGRESS TRACK fades in (850–1250ms) ──────────────
            FadeIn(ProgressTrack, fromMs: 850, durationMs: 400);

            // ─── 5) SWEEP from ~390ms ─────────────────────────────────
            StartProgressSweep();
        }

        // ══════════════════════════════════════════════════════════════
        //  UNLOADED — cleanup safety net
        // ══════════════════════════════════════════════════════════════

        private void SplashView_Unloaded(object sender, RoutedEventArgs e)
        {
            StopProgressSweep();
        }

        // ══════════════════════════════════════════════════════════════
        //  ANIMATION HELPERS
        // ══════════════════════════════════════════════════════════════

        private static void FadeIn(UIElement target, int fromMs, int durationMs)
        {
            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                BeginTime = TimeSpan.FromMilliseconds(fromMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            target.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        // ══════════════════════════════════════════════════════════════
        //  VERTICAL PROGRESS SWEEP — 2px luminous fill, bottom→top
        // ══════════════════════════════════════════════════════════════

        private void StartProgressSweep()
        {
            _sweepTicks = 0;
            _progressSweepTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(30)
            };
            _progressSweepTimer.Tick += ProgressSweep_Tick;
            _progressSweepTimer.Start();
        }

        private void ProgressSweep_Tick(object? sender, EventArgs e)
        {
            _sweepTicks++;
            int t = _sweepTicks - SweepLeadTicks;
            if (t <= 0) return;

            double p = t / (double)SweepMotionTicks;
            if (p >= 1.0)
            {
                ProgressFill.Height = _progressTrackHeight;
                _progressSweepTimer?.Stop();
                _progressSweepTimer = null;
                return;
            }

            // smoothstep — gentle acceleration, gentle landing
            double eased = p * p * (3.0 - 2.0 * p);
            ProgressFill.Height = _progressTrackHeight * eased;
        }

        // ══════════════════════════════════════════════════════════════
        //  TERMS GATE (UAE)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Bump when the wording changes materially. A user who accepted an
        /// older version is asked again, so consent always maps to the text
        /// that was actually shown.
        /// </summary>
        public const string TermsVersion = "2026-08-UAE-1";

        private readonly TermsGateViewModel _termsVm = new();

        /// <summary>True after the user explicitly accepted the terms
        /// (Phase 3.7: forwarded from TermsGateViewModel).</summary>
        public bool TermsAccepted => _termsVm.TermsAccepted;

        /// <summary>
        /// Fired when the user accepts or declines the terms gate.
        /// Accept does not close the splash — App fades it into login.
        /// </summary>
        public event Action? TermsResolved;

        /// <summary>True only after a verified sign-in on this splash.</summary>
        public bool SignedIn { get; private set; }

        private static string TermsMarkerPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "terms-accepted.txt");

        /// <summary>
        /// Has this exact terms version already been accepted on this machine?
        /// Read failures return false: re-asking is the safe direction.
        /// </summary>
        public static bool HasAcceptedCurrentTerms()
        {
            try
            {
                if (!File.Exists(TermsMarkerPath)) return false;
                foreach (var line in File.ReadAllLines(TermsMarkerPath))
                {
                    if (line.StartsWith("version=", StringComparison.Ordinal) &&
                        line["version=".Length..].Trim() == TermsVersion)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void RecordAcceptance()
        {
            try
            {
                var dir = Path.GetDirectoryName(TermsMarkerPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Plain text on purpose: a record the owner can read (and an
                // auditor can be shown) without the app.
                File.WriteAllText(
                    TermsMarkerPath,
                    $"version={TermsVersion}" + Environment.NewLine +
                    $"acceptedUtc={DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z" + Environment.NewLine +
                    $"machine={Environment.MachineName}" + Environment.NewLine +
                    $"windowsUser={Environment.UserName}" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Never block entry because the marker could not be written.
                AppLogService.Warning(
                    "Could not record terms acceptance: " + ExceptionMessages.Deep(ex),
                    new LogContext { Module = "Startup", Class = nameof(SplashView), Operation = "RecordAcceptance" });
            }
        }

        /// <summary>
        /// Swaps the console for the terms panel. Called by App once
        /// startup work has finished.
        /// </summary>
        public void ShowTermsGate()
        {
            StopProgressSweep();

            TermsPanel.Visibility = Visibility.Visible;
            TermsPanel.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(280),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }

        /// <summary>
        /// AUTO-SIGN-IN — the runtime side of the "Skip login until
        /// update" pill in LoginPanel. (Unchanged in v3.)
        /// </summary>
        private static async Task<bool> TryAutoLoginAsync()
        {
            var cfg = App.Config;
            if (cfg == null) return false;
            if (!cfg.SkipLoginUntilUpdate) return false;

            // Version binding — an update ships new binaries; the stored
            // version is set when the pill is armed.
            string running = AppInfo.Version ?? "";
            string bound = cfg.SkipLoginBoundVersion ?? "";
            if (string.IsNullOrEmpty(bound) ||
                !string.Equals(running, bound, StringComparison.Ordinal))
            {
                AppLogService.Info(
                    "Auto sign-in skipped: app version changed " +
                    $"(bound={bound}, running={running}).",
                    new LogContext { Module = "Auth", Class = nameof(SplashView), Operation = "TryAutoLogin" });
                return false;
            }

            string username = (cfg.LastUsername ?? "").Trim();
            if (username.Length == 0) return false;

            if (!SecretVault.TryRetrieve(LoginPanel.SkipLoginSecretKey, out string password)
                || string.IsNullOrEmpty(password))
            {
                return false;
            }

            try
            {
                int remaining = await Task.Run(
                    () => AuditService.LockoutRemainingSecondsAsync(username));
                if (remaining > 0) return false;

                var (ok, error) = await Task.Run(
                    () => AuditService.SwitchUserAsync(username, password));
                if (!ok)
                {
                    AppLogService.Warning(
                        "Auto sign-in failed for saved user '" + username +
                        "': " + error + ". Clearing saved credentials.",
                        new LogContext { Module = "Auth", Class = nameof(SplashView), Operation = "TryAutoLogin" });
                    try
                    {
                        SecretVault.Remove(LoginPanel.SkipLoginSecretKey);
                    }
                    catch (Exception ex)
                    {
                        AppLogService.Debug("[SplashView] Clearing stale skip-login credential failed: " + ExceptionMessages.Deep(ex));
                    }
                    try
                    {
                        cfg.SkipLoginUntilUpdate = false;
                        cfg.SkipLoginBoundVersion = "";
                        ConfigService.Save(cfg);
                    }
                    catch (Exception ex)
                    {
                        AppLogService.Debug("[SplashView] Skip-login reset persist failed: " + ExceptionMessages.Deep(ex));
                    }
                    return false;
                }

                // Force-password-change users must NEVER auto-sign-in
                // without proving they know the new password first.
                if (await Task.Run(() => AuditService.IsUsingDefaultPasswordAsync(username)))
                {
                    AuditService.LogSessionBoundary(login: false);
                    return false;
                }

                AuditService.LogSessionBoundary(login: true);
                AppLogService.Info(
                    "Auto sign-in succeeded for '" + username + "' (skip-login pill).",
                    new LogContext { Module = "Auth", Class = nameof(SplashView), Operation = "TryAutoLogin" });
                return true;
            }
            catch (Exception ex)
            {
                AppLogService.Warning("Auto sign-in threw: " + ExceptionMessages.Deep(ex),
                    new LogContext { Module = "Auth", Class = nameof(SplashView), Operation = "TryAutoLogin" });
                return false;
            }
        }

        /// <summary>
        /// Replaces the console with the sign-in form on THIS window.
        /// Never opens a second maximized window. (Flow unchanged in v3.)
        /// </summary>
        public async Task<bool> RunLoginAsync()
        {
            // ── AUTO-SIGN-IN FAST PATH ────────────────────────────────
            StopProgressSweep();

            if (await TryAutoLoginAsync())
            {
                SignedIn = true;
                return true;
            }

            var done = new TaskCompletionSource<bool>();

            void Finish(bool signedIn)
            {
                LoginGate.Completed -= Finish;
                SignedIn = signedIn;
                done.TrySetResult(signedIn);
            }

            LoginGate.Completed += Finish;
            Closed += (_, __) => Finish(LoginGate.SignedIn);

            StopProgressSweep();

            TermsPanel.Visibility = Visibility.Collapsed;
            PosterHost.Visibility = Visibility.Collapsed;
            LoginHost.Visibility = Visibility.Visible;
            LoginHost.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(280),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

            // Bring keyboard focus back to this window — a Topmost splash
            // can lose activation; controls in an inactive window ignore
            // Focus() and the caret never lands in the field.
            try { Activate(); }
            catch (Exception ex)
            {
                AppLogService.Debug("[SplashView] RunLoginAsync: window may be closing: " + ExceptionMessages.Deep(ex));
            }

            Dispatcher.BeginInvoke(new Action(() => LoginGate.FocusInitialFieldDeferred()),
                DispatcherPriority.Input);

            return await done.Task;
        }

        /// <summary>
        /// First-run setup flow (flow unchanged in v3). UX CONTRACT:
        /// first boot ever → splash → setup → straight into the main
        /// window; every subsequent boot → splash → login (unless the
        /// "Skip login until update" pill was armed).
        /// </summary>
        public Task<bool> RunFirstRunSetupAsync()
        {
            var done = new TaskCompletionSource<bool>();

            async void OnCreated(string newUsername, string newPassword)
            {
                // Unsubscribe FIRST so a re-entrant Created cannot
                // double-complete the TCS.
                SetupGate.Created -= OnCreated;

                SetupGate.IsEnabled = false;

                try
                {
                    if (App.Config != null)
                    {
                        App.Config.LastUsername = newUsername;
                        App.Config.RememberUsername = true;
                        ConfigService.Save(App.Config);
                    }
                }
                catch (Exception ex)
                {
                    AppLogService.Debug("[SplashView] Remember-username persist failed: " + ExceptionMessages.Deep(ex));
                }

                try
                {
                    var (ok, error) = await Task.Run(
                        () => AuditService.SwitchUserAsync(newUsername, newPassword));

                    if (!ok)
                    {
                        AppLogService.Error(
                            "First-run silent sign-in failed for '" + newUsername +
                            "' immediately after CreateUserAsync: " + error, null,
                            new LogContext { Module = "Auth", Class = nameof(SplashView), Operation = "RunFirstRunSetup" });
                        SetupHost.Visibility = Visibility.Collapsed;
                        SetupGate.IsEnabled = true;
                        bool loggedIn = await RunLoginAsync();
                        done.TrySetResult(loggedIn);
                        return;
                    }

                    AuditService.LogSessionBoundary(login: true);
                    SignedIn = true;
                    done.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    AppLogService.Error(
                        "First-run silent sign-in threw unexpectedly.", ex,
                        new LogContext { Module = "Auth", Class = nameof(SplashView), Operation = "RunFirstRunSetup" });
                    done.TrySetResult(false);
                }
            }

            SetupGate.Created += OnCreated;
            Closed += (_, __) =>
            {
                done.TrySetResult(false);
            };

            StopProgressSweep();

            TermsPanel.Visibility = Visibility.Collapsed;
            PosterHost.Visibility = Visibility.Collapsed;
            SetupHost.Visibility = Visibility.Visible;
            SetupHost.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(280),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

            try { Activate(); }
            catch (Exception ex)
            {
                AppLogService.Debug("[SplashView] RunFirstRunSetupAsync: window may be closing: " + ExceptionMessages.Deep(ex));
            }
            Dispatcher.BeginInvoke(new Action(() => SetupGate.Focus()),
                DispatcherPriority.Input);

            return done.Task;
        }

        public void FadeOutAndClose(Action onCompleted)
        {
            StopProgressSweep();

            // Stay Topmost for the fade — the window underneath is only
            // revealed by the opacity animation.
            IsHitTestVisible = false;

            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                onCompleted?.Invoke();
                Close();
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }

        private void StopProgressSweep()
        {
            _progressSweepTimer?.Stop();
            _progressSweepTimer = null;
        }
    }
}
