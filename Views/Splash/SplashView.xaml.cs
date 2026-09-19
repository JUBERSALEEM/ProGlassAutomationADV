// ═══════════════════════════════════════════════════════════════════
// FILE: Views/Splash/SplashView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Splash Screen — "Fluid Crystal Blob" (v4, 2026-09-19)
//
// ENTRANCE CHOREOGRAPHY (v4, ~2.5s, iOS fluid):
//   0ms     MADE IN INDIA caption fade (350ms)
//   150ms   droplets start bouncy circling (infinite)
//   200ms   hexagon logo settles (fade + scale 0.92→1, CubicEase)
//   250ms   fluid blob behind scales 0.85→1 with BounceEase (bouncy)
//   350ms   background blobs start floating bouncy (X/Y with BounceEase)
//   450ms   wordmark fades + slides up 14px (CubicEase)
//   850ms   circular progress ring fades in + starts rotating
//           (360° over 1.2s, linear, infinite) + sweep grows via
//           StrokeDashOffset if needed — here we rotate the arc
//
// BOUNCY ANIMATIONS (iOS style):
//   • 4 large blobs: TranslateTransform X/Y with BounceEase / ElasticEase,
//     AutoReverse, RepeatBehavior.Forever, different durations (3-5s)
//   • 6 small droplets: circling around center with Translate + Bounce,
//     durations 2-4s, different delays for organic feel
//   • fluid blob: Scale 0.85→1 with BounceEase, plus subtle Translate Y
//     floating (Yoyo)
//   • progress: RotateTransform 0→360 linear infinite (spinner)
//
// PRESERVED (zero regression — App.StartupCore.cs depends on these):
//   • FadeOutAndClose(Action), RunLoginAsync(), RunFirstRunSetupAsync(),
//     ShowTermsGate()
//   • TermsAccepted / TermsResolved / SignedIn / HasAcceptedCurrentTerms
//   • TermsVersion, RecordAcceptance
//   • Auto sign-in (skip-login pill) fast path, first-run flow
//   • x:Name: PosterHost, LogoBorder, LogoScale, AppNameText,
//     AppNameTranslate, ProgressTrack, ProgressFill, CaptionMadeIn,
//     CaptionEnterprise, CaptionSecure, TermsPanel, TermsBar, etc.
//
// PERFORMANCE:
//   • One DispatcherTimer for progress rotation (30ms) + Storyboards for
//     bouncy blobs (WPF composition thread) — no per-frame allocations
//   • BlurEffects are cached (cheap — 4 large + 6 small)
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
    /// Splash window — Fluid Crystal Blob (owner-approved 12-fluid-crystal-blob.png).
    /// iOS transparent fluid glass light theme, bouncy floating blobs.
    /// </summary>
    public partial class SplashView : Window
    {
        // ── Circular progress rotation ─────────────────────────────────
        private DispatcherTimer? _progressRotateTimer;
        private double _progressAngle;

        // ── Bouncy blob timers (for cleanup) ─────────────────────────────
        private readonly System.Collections.Generic.List<Storyboard> _bouncyBoards = new();

        public SplashView()
        {
            InitializeComponent();

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
        //  LOADED — start fluid entrance + bouncy loops
        // ══════════════════════════════════════════════════════════════

        private void SplashView_Loaded(object sender, RoutedEventArgs e)
        {
            // ─── 1) CAPTION — MADE IN INDIA quiet fade ─────────────────
            FadeIn(CaptionMadeIn, fromMs: 0, durationMs: 400);
            // Enterprise/Secure are collapsed in v4 but keep fade for compat
            FadeIn(CaptionEnterprise, fromMs: 150, durationMs: 350);
            FadeIn(CaptionSecure, fromMs: 300, durationMs: 350);

            // ─── 2) FLUID BLOB — bouncy scale 0.85→1 ───────────────────
            var fluidScaleX = new DoubleAnimation
            {
                From = 0.85,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(900),
                BeginTime = TimeSpan.FromMilliseconds(250),
                EasingFunction = new BounceEase { Bounces = 2, Bounciness = 2.5, EasingMode = EasingMode.EaseOut }
            };
            var fluidScaleY = fluidScaleX.Clone();
            FluidBlobScale.BeginAnimation(ScaleTransform.ScaleXProperty, fluidScaleX);
            FluidBlobScale.BeginAnimation(ScaleTransform.ScaleYProperty, fluidScaleY);

            // Subtle floating Y for fluid blob (infinite bouncy)
            StartBouncyTranslate(FluidBlobTranslate, ampX: 6, ampY: 10, durationMs: 3200, beginMs: 600, isFluid: true);

            // ─── 3) HEXAGON LOGO — settle + fade ───────────────────────
            var logoScale = new DoubleAnimation
            {
                From = 0.92,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(200),
                Duration = TimeSpan.FromMilliseconds(600),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            LogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, logoScale);
            LogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, logoScale.Clone());

            var logoFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(200),
                Duration = TimeSpan.FromMilliseconds(600),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            LogoBorder.BeginAnimation(OpacityProperty, logoFade);

            // ─── 4) WORDMARK — fade + slide up 14px ────────────────────
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

            // ─── 5) PROGRESS RING — fade + rotation ────────────────────
            FadeIn(ProgressTrack, fromMs: 850, durationMs: 400);
            StartProgressRotation();

            // ─── 6) LARGE BLOBS — bouncy floating (infinite) ───────────
            // Each blob gets X and Y with different amps/durations for organic feel
            StartBouncyTranslate(BgBlob1Translate, ampX: 18, ampY: 22, durationMs: 3800, beginMs: 350);
            StartBouncyTranslate(BgBlob2Translate, ampX: 22, ampY: 18, durationMs: 4200, beginMs: 400);
            StartBouncyTranslate(BgBlob3Translate, ampX: 16, ampY: 20, durationMs: 3600, beginMs: 500);
            StartBouncyTranslate(BgBlob4Translate, ampX: 12, ampY: 14, durationMs: 3000, beginMs: 600);

            // ─── 7) SMALL DROPLETS — circling bouncy orbit ─────────────
            // Position droplets initially around center, then animate in orbit
            try
            {
                var centerX = 0.0;
                var centerY = 0.0;
                // We position via Canvas — but we animate via TranslateTransform
                // relative to 0,0; we set initial Canvas positions in code-behind
                // based on window size for better centering.
                double winW = ActualWidth > 100 ? ActualWidth : 1200;
                double winH = ActualHeight > 100 ? ActualHeight : 800;
                double cx = winW / 2;
                double cy = winH / 2 - 20; // slightly above center (logo)

                // Droplet orbit radii and angles
                PositionDroplet(Droplet1, Droplet1Translate, cx + 220, cy - 180, amp: 18, dur: 2600, begin: 150);
                PositionDroplet(Droplet2, Droplet2Translate, cx - 260, cy + 40, amp: 14, dur: 3000, begin: 250);
                PositionDroplet(Droplet3, Droplet3Translate, cx + 280, cy + 60, amp: 12, dur: 2800, begin: 350);
                PositionDroplet(Droplet4, Droplet4Translate, cx - 180, cy + 160, amp: 10, dur: 3200, begin: 400);
                PositionDroplet(Droplet5, Droplet5Translate, cx + 200, cy + 200, amp: 16, dur: 3600, begin: 500);
                PositionDroplet(Droplet6, Droplet6Translate, cx - 200, cy - 120, amp: 12, dur: 3400, begin: 600);
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[SplashView] Droplet positioning failed: " + ExceptionMessages.Deep(ex));
            }
        }

        private void PositionDroplet(System.Windows.Shapes.Ellipse droplet, TranslateTransform trans, double absX, double absY, double amp, int dur, int begin)
        {
            try
            {
                // Canvas.SetLeft/Top to absolute, then animate Translate for bouncy orbit
                System.Windows.Controls.Canvas.SetLeft(droplet, absX);
                System.Windows.Controls.Canvas.SetTop(droplet, absY);
                StartBouncyTranslate(trans, ampX: amp, ampY: amp * 0.8, durationMs: dur, beginMs: begin);
            }
            catch { }
        }

        private void StartBouncyTranslate(TranslateTransform target, double ampX, double ampY, int durationMs, int beginMs, bool isFluid = false)
        {
            try
            {
                var board = new Storyboard();

                var animX = new DoubleAnimation
                {
                    From = -ampX,
                    To = ampX,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    BeginTime = TimeSpan.FromMilliseconds(beginMs),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = isFluid
                        ? (IEasingFunction)new SineEase { EasingMode = EasingMode.EaseInOut }
                        : new BounceEase { Bounces = 1, Bounciness = 1.8, EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(animX, target);
                Storyboard.SetTargetProperty(animX, new PropertyPath(TranslateTransform.XProperty));
                board.Children.Add(animX);

                var animY = new DoubleAnimation
                {
                    From = -ampY,
                    To = ampY,
                    Duration = TimeSpan.FromMilliseconds(durationMs + 400),
                    BeginTime = TimeSpan.FromMilliseconds(beginMs + 150),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new BounceEase { Bounces = 2, Bounciness = 2.2, EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(animY, target);
                Storyboard.SetTargetProperty(animY, new PropertyPath(TranslateTransform.YProperty));
                board.Children.Add(animY);

                board.Begin();
                _bouncyBoards.Add(board);
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[SplashView] Bouncy translate failed: " + ExceptionMessages.Deep(ex));
            }
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
            try
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
            catch { }
        }

        // ══════════════════════════════════════════════════════════════
        //  CIRCULAR PROGRESS — rotating arc (iOS spinner style)
        // ══════════════════════════════════════════════════════════════

        private void StartProgressRotation()
        {
            _progressAngle = 0;
            _progressRotateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60fps
            };
            _progressRotateTimer.Tick += (s, e) =>
            {
                _progressAngle += 4.2; // ~1.2s per 360°
                if (_progressAngle >= 360) _progressAngle -= 360;
                try { ProgressRotate.Angle = _progressAngle; } catch { }
            };
            _progressRotateTimer.Start();
        }

        // ══════════════════════════════════════════════════════════════
        //  TERMS GATE (UAE)
        // ══════════════════════════════════════════════════════════════

        public const string TermsVersion = "2026-08-UAE-1";

        private readonly TermsGateViewModel _termsVm = new();

        public bool TermsAccepted => _termsVm.TermsAccepted;

        public event Action? TermsResolved;

        public bool SignedIn { get; private set; }

        private static string TermsMarkerPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "terms-accepted.txt");

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

                File.WriteAllText(
                    TermsMarkerPath,
                    $"version={TermsVersion}" + Environment.NewLine +
                    $"acceptedUtc={DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z" + Environment.NewLine +
                    $"machine={Environment.MachineName}" + Environment.NewLine +
                    $"windowsUser={Environment.UserName}" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not record terms acceptance: " + ExceptionMessages.Deep(ex),
                    new LogContext { Module = "Startup", Class = nameof(SplashView), Operation = "RecordAcceptance" });
            }
        }

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

        private static async Task<bool> TryAutoLoginAsync()
        {
            var cfg = App.Config;
            if (cfg == null) return false;
            if (!cfg.SkipLoginUntilUpdate) return false;

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

        public async Task<bool> RunLoginAsync()
        {
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

            try { Activate(); }
            catch (Exception ex)
            {
                AppLogService.Debug("[SplashView] RunLoginAsync: window may be closing: " + ExceptionMessages.Deep(ex));
            }

            Dispatcher.BeginInvoke(new Action(() => LoginGate.FocusInitialFieldDeferred()),
                DispatcherPriority.Input);

            return await done.Task;
        }

        public Task<bool> RunFirstRunSetupAsync()
        {
            var done = new TaskCompletionSource<bool>();

            async void OnCreated(string newUsername, string newPassword)
            {
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
            try
            {
                _progressRotateTimer?.Stop();
                _progressRotateTimer = null;
            }
            catch { }

            try
            {
                foreach (var board in _bouncyBoards)
                {
                    board.Stop();
                }
                _bouncyBoards.Clear();
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[SplashView] Stop bouncy boards failed: " + ExceptionMessages.Deep(ex));
            }
        }
    }
}
