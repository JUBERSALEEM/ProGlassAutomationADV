// ═══════════════════════════════════════════════════════════════════
// FILE: Views/Splash/SplashView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Splash Screen — "Midnight Console" (v2 full redesign,
//        2026-09-19)
//
// ENTRANCE CHOREOGRAPHY (v2, ~2.3s, finishes inside App startup delay):
//   0ms     header hairline — brand / enterprise / security (staggered)
//   200ms   logo settles (fade + scale 0.9→1) — dotted ring already
//           revolving (30s loop, ambient)
//   450ms   wordmark fades + slides up (14px drift)
//   700ms   tagline fades in
//   850ms   16-chip module grid fades in as ONE panel (professional,
//           no marketing stagger)
//   400ms   luminous console sweep begins — 2px fill + LIVE PERCENT
//           readout counting 0→100 over ~1.9s (smoothstep)
//   0ms     console status line + animated dots (350ms cycle)
//
// PRESERVED (zero regression — App.StartupCore.cs depends on these):
//   • FadeOutAndClose(Action), RunLoginAsync(), RunFirstRunSetupAsync(),
//     ShowTermsGate()
//   • TermsAccepted / TermsResolved / SignedIn / HasAcceptedCurrentTerms
//   • TermsVersion, RecordAcceptance
//   • SplashView_Loaded / SplashView_Unloaded timers + cleanup
//   • Animated loading dots (one DispatcherTimer)
//
// PERFORMANCE:
//   • Feature list is a static readonly array — allocated once.
//   • One shared DataTemplate; the panel fades as one element.
//   • Progress sweep = ONE 30ms DispatcherTimer driving width + label
//     (no Storyboard/TextBlock animation machinery, no allocations in
//     the tick beyond the percent string).
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
using System.Collections.Generic;
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
    /// Animates in over ~2.3 seconds while App performs background
    /// initialization. When the main window is ready, App calls
    /// <see cref="FadeOutAndClose"/> to dismiss the splash.
    /// </summary>
    public partial class SplashView : Window
    {
        // ── Animated loading dots (⋯) ────────────────────────────────
        private DispatcherTimer? _loadingDotsTimer;
        private int _loadingDotsCount;

        // ── Console progress sweep (width + percent readout) ─────────
        private DispatcherTimer? _progressSweepTimer;
        private int _sweepTicks;
        private double _progressTrackWidth = 280;

        // 30ms tick · ~13 tick lead-in (~390ms hold at 0) · 63 ticks
        // of motion (~1890ms) → total ≈ 2.28s, matches the v1 sweep
        // window (700–2600ms of v1 ≈ 400–2300ms of v2).
        private const int SweepLeadTicks = 13;
        private const int SweepMotionTicks = 63;

        // ══════════════════════════════════════════════════════════════
        //  FEATURE MODULE DATA — 16 uniform glass chips (2×8 grid)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Immutable module descriptor rendered by FeatureChipTemplate.
        /// Icon = Segoe MDL2 glyph, Label = module name, BorderBrush is
        /// reserved for per-chip tinting.
        /// </summary>
        private sealed record FeatureItem(string Icon, string Label, Brush BorderBrush);

        // Static — allocated once at type init, referenced by all instances.
        private static readonly IReadOnlyList<FeatureItem> _features = BuildFeatures();

        private static IReadOnlyList<FeatureItem> BuildFeatures()
        {
            static SolidColorBrush Frozen(byte r, byte g, byte b)
            {
                var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
                brush.Freeze();
                return brush;
            }

            // Module glyphs follow the ACTIVE accent instead of a
            // hard-coded blue ramp, so the splash adapts to whatever
            // theme/accent the operator chose. The type is first touched
            // when App builds the splash (after themes apply) — safe.
            static System.Windows.Media.Brush AccentGlyph()
            {
                if (System.Windows.Application.Current?.TryFindResource("AccentBrush")
                        is System.Windows.Media.Brush b)
                {
                    if (b.IsFrozen) return b;
                    if (b.CanFreeze)
                    {
                        var c = b.Clone();
                        c.Freeze();
                        return c;
                    }
                    return b;
                }
                return Frozen(0x60, 0xA5, 0xFA); // brand blue fallback
            }

            var glyph = AccentGlyph();

            // Splash module showcase — mirrors the REAL application module
            // set (MainWindow sidebar), not a hard-coded list. Labels are
            // resolved from the live Nav.* localization resources so the
            // splash is correct in every shipped language; glyphs match
            // the sidebar's Segoe MDL2 icons.
            static string Nav(string key, string fallback)
            {
                if (System.Windows.Application.Current?
                        .TryFindResource("Nav." + key) is string s
                    && !string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }
                return fallback;
            }

            return new[]
            {
                new FeatureItem("\uE80F", Nav("Dashboard",          "Dashboard"),     glyph),
                new FeatureItem("\uE7B8", Nav("SheetStore",         "Sheet Store"),   glyph),
                new FeatureItem("\uE8F1", Nav("Specifications",     "Specifications"),glyph),
                new FeatureItem("\uE721", Nav("Inquiry",            "Inquiry"),       glyph),
                new FeatureItem("\uE8A5", Nav("ProformaInvoice",    "Proforma"),      glyph),
                new FeatureItem("\uE9D9", Nav("JobOrder",           "Job Order"),     glyph),
                new FeatureItem("\uE8C7", Nav("TaxInvoice",         "Tax Invoice"),   glyph),
                new FeatureItem("\uE8C6", Nav("ProductionItems",    "Production"),    glyph),
                new FeatureItem("\uE9CE", Nav("Optimization",       "Optimization"),  glyph),
                new FeatureItem("\uE7F4", Nav("Dispatch",           "Dispatch"),      glyph),
                new FeatureItem("\uE8F7", Nav("StatementGenerator", "Statements"),    glyph),
                new FeatureItem("\uE9F9", Nav("Reports",            "Reports"),       glyph),
                new FeatureItem("\uE90E", Nav("JobProfitability",   "Profitability"), glyph),
                new FeatureItem("\uE8C8", Nav("VatSubmission",      "VAT"),           glyph),
                new FeatureItem("\uE822", Nav("MasterDataItems",    "Master Data"),   glyph),
                new FeatureItem("\uE713", Nav("System",             "System"),        glyph),
            };
        }

        public SplashView()
        {
            InitializeComponent();

            // Populate the module chip grid once.
            FeaturePanel.ItemsSource = _features;

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
            // Console sweep needs the track's real width (fixed 280 in
            // v2 XAML; the guard keeps it safe on odd DPI rounds).
            double track = ProgressTrack.ActualWidth;
            _progressTrackWidth = track < 80 ? 280 : track;

            // ─── 1) HAIRLINE HEADER — quiet chrome (0–540ms) ────────
            FadeIn(TopLeftBadge, fromMs: 0, durationMs: 300);
            FadeIn(TopCenterPill, fromMs: 120, durationMs: 300);
            FadeIn(TopRightBadge, fromMs: 240, durationMs: 300);

            // ─── 2) LOGO settles in (200–700ms) ─────────────────────
            var logoScale = new DoubleAnimation
            {
                From = 0.9,
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

            // ─── 3) WORDMARK fade + slide up (450–950ms) ────────────
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

            // ─── 4) TAGLINE fade (700–1100ms) ───────────────────────
            FadeIn(TaglineText, fromMs: 700, durationMs: 400);

            // ─── 5) MODULE GRID — one uniform fade (850–1350ms) ─────
            FadeIn(FeaturePanel, fromMs: 850, durationMs: 500);

            // ─── 6) CONSOLE — status + dots now, sweep from ~400ms ──
            StartLoadingDotsAnimation();
            StartProgressSweep();
        }

        // ══════════════════════════════════════════════════════════════
        //  UNLOADED — cleanup safety net
        // ══════════════════════════════════════════════════════════════

        private void SplashView_Unloaded(object sender, RoutedEventArgs e)
        {
            StopLoadingIndicators();
        }

        // ══════════════════════════════════════════════════════════════
        //  ANIMATION HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Convenience: fade a UIElement from 0→1 with CubicEase.
        /// </summary>
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

        /// <summary>
        /// Animates the "⋯" dots after the status text: cycles
        /// ""→"."→".."→"..." every 350ms.
        /// </summary>
        private void StartLoadingDotsAnimation()
        {
            _loadingDotsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _loadingDotsTimer.Tick += LoadingDotsTimer_Tick;
            _loadingDotsTimer.Start();
        }

        private void LoadingDotsTimer_Tick(object? sender, EventArgs e)
        {
            _loadingDotsCount = (_loadingDotsCount + 1) % 4;
            LoadingDots.Text = _loadingDotsCount switch
            {
                0 => "",
                1 => ".",
                2 => "..",
                _ => "..."
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  CONSOLE PROGRESS SWEEP — 2px luminous fill + live percent
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Drives ProgressFill.Width AND the PercentText readout from a
        /// single 30ms timer (one allocation-free path per tick).
        /// Lead-in (~390ms) holds at 0 so the sweep starts as the
        /// wordmark lands; motion phase is a smoothstep curve.
        /// </summary>
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
                ProgressFill.Width = _progressTrackWidth;
                PercentText.Text = "100%";
                _progressSweepTimer?.Stop();
                _progressSweepTimer = null;
                return;
            }

            // smoothstep — gentle acceleration, gentle landing
            double eased = p * p * (3.0 - 2.0 * p);
            ProgressFill.Width = _progressTrackWidth * eased;
            PercentText.Text = ((int)Math.Round(eased * 100)).ToString() + "%";
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

        /// <summary>
        /// True once the user has ticked the box and pressed Continue, or
        /// when a previous acceptance of THIS version was already recorded.
        /// App must not proceed to sign-in unless this is true.
        /// </summary>
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
            StopLoadingIndicators();

            LoadingText.Text = "Please review the terms to continue";
            LoadingDots.Text = "";

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
        /// update" pill in LoginPanel. (Unchanged in v2.)
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
        /// Never opens a second maximized window. (Flow unchanged in v2.)
        /// </summary>
        public async Task<bool> RunLoginAsync()
        {
            // ── AUTO-SIGN-IN FAST PATH ────────────────────────────────
            StopLoadingIndicators();
            LoadingText.Text = "Signing you in…";
            LoadingDots.Text = "";

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

            StopLoadingIndicators();
            LoadingText.Text = "Sign in to continue";
            LoadingDots.Text = "";

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
        /// First-run setup flow (flow unchanged in v2). UX CONTRACT:
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
                LoadingText.Text = "Opening workspace…";

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
                        LoadingText.Text = "Please sign in to continue.";
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

            StopLoadingIndicators();
            LoadingText.Text = "Setting up the first administrator";
            LoadingDots.Text = "";

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
            StopLoadingIndicators();

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

        /// <summary>
        /// Stops the dots timer AND the progress sweep, resets the dots.
        /// Shared by ShowTermsGate / RunLoginAsync / FadeOutAndClose /
        /// Unloaded.
        /// </summary>
        private void StopLoadingIndicators()
        {
            _loadingDotsTimer?.Stop();
            _loadingDotsTimer = null;

            _progressSweepTimer?.Stop();
            _progressSweepTimer = null;
        }
    }
}
