// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Splash/SplashView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Splash Screen — industrial glass identity
//
// Professional crystal choreography (~2.5s, finishes inside App.xaml.cs
// startup delay with margin):
//   0ms   top trust chips fade in (frame effect, 100ms stagger)
//   100ms logo settles (fade + gentle scale)
//   450ms product name fades + slides up
//   750ms tagline fades in
//   1000ms frosted module chips fade in as ONE panel (no marketing
//          stagger — professional, not flashy)
//   700ms  luminous progress bar fills
//   950ms  status pill + animated dots
//
// PRESERVED (zero regression — App.xaml.cs depends on these):
//   • FadeOutAndClose(Action), RunLoginAsync(), ShowTermsGate()
//   • TermsAccepted / TermsResolved / SignedIn / HasAcceptedCurrentTerms
//   • TermsVersion, RecordAcceptance, AcceptTerms_Changed,
//     ContinueFromTerms_Click, DeclineTerms_Click
//   • SplashView_Loaded / SplashView_Unloaded timers + cleanup
//   • Animated loading dots (one DispatcherTimer)
//
// PERFORMANCE:
//   • Feature list is a static readonly array — allocated once.
//   • One shared DataTemplate; the panel fades as one element (no
//     per-badge storyboards).
//   • All brushes frozen; all animations one-shot.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Updates;
using ProGlassAutomation.Views.Login;
using System;
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
    /// Animates in over ~2.5 seconds while App.xaml.cs performs
    /// background initialization. When the main window is ready,
    /// App.xaml.cs calls <see cref="FadeOutAndClose(Action)"/> to
    /// dismiss the splash and reveal the main window.
    /// </summary>
    public partial class SplashView : Window
    {
        // ── Animated loading dots (⋯) ────────────────────────────────
        private DispatcherTimer? _loadingDotsTimer;
        private int _loadingDotsCount;

        // ══════════════════════════════════════════════════════════════════
        //  FEATURE MODULE DATA — frosted crystal chips
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Immutable module descriptor rendered by FeatureBadgeTemplate.
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

            // 2026-08-25 redesign: module glyphs follow the ACTIVE accent
            // instead of a hard-coded blue ramp, so the splash adapts to
            // whatever theme/accent the operator chose. The type is first
            // touched when App builds the splash (after themes apply), so
            // the lookup is safe here.
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

            // 12 modules — rendered as a 6×2 uniform grid (see
            // FeaturePanel in the XAML) so every chip is the same width
            // and the block stays perfectly aligned.
            return new[]
            {
                new FeatureItem("\uE9F9", "Dashboard",    glyph),
                new FeatureItem("\uE721", "Inquiry",      glyph),
                new FeatureItem("\uE8A5", "Proforma",     glyph),
                new FeatureItem("\uE9D9", "Job Orders",   glyph),
                new FeatureItem("\uE7B8", "Delivery",     glyph),
                new FeatureItem("\uE8C7", "Tax Invoice",  glyph),
                new FeatureItem("\uE8F7", "Statements",   glyph),
                new FeatureItem("\uE8C8", "Reports",      glyph),
                new FeatureItem("\uEA37", "Sheet Store",  glyph),
                new FeatureItem("\uE9CE", "Optimization", glyph),
                new FeatureItem("\uE8F2", "AI Assistant", glyph),
                new FeatureItem("\uE713", "Settings",     glyph),
            };
        }

        public SplashView()
        {
            InitializeComponent();

            // Populate the frosted module chip strip once.
            FeaturePanel.ItemsSource = _features;

            Loaded += SplashView_Loaded;
            Unloaded += SplashView_Unloaded;
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOADED — start animation sequence (calm, ~2.5s)
        // ══════════════════════════════════════════════════════════════════

        private void SplashView_Loaded(object sender, RoutedEventArgs e)
        {
            // ─── PHASE 1: Top trust chips fade in (0–600ms) — frame ──
            FadeIn(TopLeftBadge, fromMs: 0, durationMs: 400);
            FadeIn(TopCenterPill, fromMs: 100, durationMs: 400);
            FadeIn(TopRightBadge, fromMs: 200, durationMs: 400);

            // ─── PHASE 2: Logo settles in (100–600ms) ────────────────
            var logoScale = new DoubleAnimation
            {
                From = 0.92,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(100),
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            LogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, logoScale);
            LogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, logoScale.Clone());

            var logoFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(100),
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            LogoBorder.BeginAnimation(OpacityProperty, logoFade);

            // ─── PHASE 3: App name fade + slide up (450–900ms) ───────
            var nameFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(450),
                BeginTime = TimeSpan.FromMilliseconds(450)
            };
            var nameSlide = new DoubleAnimation
            {
                From = 12,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(450),
                BeginTime = TimeSpan.FromMilliseconds(450),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            AppNameText.BeginAnimation(OpacityProperty, nameFade);
            AppNameTranslate.BeginAnimation(TranslateTransform.YProperty, nameSlide);

            // ─── PHASE 4: Tagline fade (750–1150ms) ──────────────────
            FadeIn(TaglineText, fromMs: 750, durationMs: 400);

            // ─── PHASE 5: Module chips — one uniform fade (1000ms) ───
            FadeIn(FeaturePanel, fromMs: 1000, durationMs: 450);

            // ─── PHASE 6: Progress bar fill (700–2600ms) ─────────────
            double track = ProgressTrack.ActualWidth;
            if (track < 80)
                track = Math.Max(240, ActualWidth * 0.35);
            var progressAnim = new DoubleAnimation
            {
                From = 0,
                To = track,
                Duration = TimeSpan.FromMilliseconds(1900),
                BeginTime = TimeSpan.FromMilliseconds(700),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            ProgressFill.BeginAnimation(WidthProperty, progressAnim);

            // ─── PHASE 7: Status pill fades in + dots start ──────────
            FadeIn(LoadingText, fromMs: 950, durationMs: 350);
            FadeIn(LoadingDots, fromMs: 950, durationMs: 350);
            StartLoadingDotsAnimation();
        }

        // ══════════════════════════════════════════════════════════════════
        //  UNLOADED — cleanup safety net
        // ══════════════════════════════════════════════════════════════════

        private void SplashView_Unloaded(object sender, RoutedEventArgs e)
        {
            _loadingDotsTimer?.Stop();
            _loadingDotsTimer = null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  ANIMATION HELPERS
        // ══════════════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════════════
        //  TERMS GATE (UAE)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Bump when the wording changes materially. A user who accepted an
        /// older version is asked again, so consent always maps to the text
        /// that was actually shown.
        /// </summary>
        public const string TermsVersion = "2026-08-UAE-1";

        /// <summary>
        /// True once the user has ticked the box and pressed Continue, or
        /// when a previous acceptance of THIS version was already recorded.
        /// App.xaml.cs must not proceed to sign-in unless this is true.
        /// </summary>
        public bool TermsAccepted { get; private set; }

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
        /// Read failures return false: re-asking is the safe direction, since
        /// wrongly assuming consent is the outcome that must never happen.
        /// </summary>
        public static bool HasAcceptedCurrentTerms()
        {
            try
            {
                if (!File.Exists(TermsMarkerPath)) return false;
                foreach (var line in File.ReadAllLines(TermsMarkerPath))
                    if (line.StartsWith("version=", StringComparison.Ordinal) &&
                        line["version=".Length..].Trim() == TermsVersion)
                        return true;
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
                    Directory.CreateDirectory(dir);

                // Plain text on purpose: this is a record the owner can read,
                // and an auditor can be shown, without needing the app.
                File.WriteAllText(
                    TermsMarkerPath,
                    $"version={TermsVersion}" + Environment.NewLine +
                    $"acceptedUtc={DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z" + Environment.NewLine +
                    $"machine={Environment.MachineName}" + Environment.NewLine +
                    $"windowsUser={Environment.UserName}" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Never block entry because the marker could not be written -
                // the user did accept. They will simply be asked again.
                AppLogService.Warning(
                    "Could not record terms acceptance: " + ex.Message,
                    new LogContext
                    {
                        Module = "Startup",
                        Class = nameof(SplashView),
                        Operation = "RecordAcceptance"
                    });
            }
        }

        /// <summary>
        /// Swaps the loading strip for the terms panel. Called by
        /// App.xaml.cs once startup work has finished.
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

        private void AcceptTerms_Changed(object sender, RoutedEventArgs e)
            => ContinueButton.IsEnabled = AcceptTermsCheck.IsChecked == true;

        private void ContinueFromTerms_Click(object sender, RoutedEventArgs e)
        {
            // Defence in depth: the button is disabled until the box is
            // ticked, but never rely on UI state alone for a consent gate.
            if (AcceptTermsCheck.IsChecked != true) return;

            RecordAcceptance();
            TermsAccepted = true;
            TermsPanel.Visibility = Visibility.Collapsed;
            TermsResolved?.Invoke();
        }

        private void DeclineTerms_Click(object sender, RoutedEventArgs e)
        {
            TermsAccepted = false;
            TermsResolved?.Invoke();
            Close();
        }

        /// <summary>
        /// AUTO-SIGN-IN — the runtime side of the "Skip login until
        /// update" pill in LoginPanel. Returns true when the splash
        /// managed to sign the user in without showing the login
        /// form; returns false when any prerequisite is missing (no
        /// saved credentials, an app update reset the bound version,
        /// the saved password no longer verifies, etc.) — the caller
        /// then falls through to the normal login flow.
        ///
        /// Why this lives on SplashView and not App.xaml.cs: the
        /// splash is the UI surface that would otherwise show the
        /// login form, and it already owns the LoadingText / status
        /// pill we want to keep alive during the auto-sign-in
        /// round-trip.
        /// </summary>
        private static async Task<bool> TryAutoLoginAsync()
        {
            var cfg = App.Config;
            if (cfg == null) return false;
            if (!cfg.SkipLoginUntilUpdate) return false;

            // Version binding — an update ships new binaries and
            // we want the user to re-verify on the new build. The
            // stored version is set when the pill is armed; if it
            // does not equal AppInfo.Version now, one of the two
            // has moved on and we cannot auto-sign-in.
            string running = AppInfo.Version ?? "";
            string bound = cfg.SkipLoginBoundVersion ?? "";
            if (string.IsNullOrEmpty(bound) ||
                !string.Equals(running, bound, StringComparison.Ordinal))
            {
                AppLogService.Info(
                    "Auto sign-in skipped: app version changed " +
                    $"(bound={bound}, running={running}).",
                    new LogContext { Module = "Auth", Class = nameof(SplashView) });
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
                    // Saved credentials no longer verify — most
                    // likely because the operator changed their
                    // password. Clear the stale blob so we never
                    // retry it and fall through to the login form.
                    AppLogService.Warning(
                        "Auto sign-in failed for saved user '" + username +
                        "': " + error + ". Clearing saved credentials.",
                        new LogContext { Module = "Auth", Class = nameof(SplashView) });
                    try { SecretVault.Remove(LoginPanel.SkipLoginSecretKey); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[SplashView] Clearing stale skip-login credential failed: " + ex.Message); }
                    try
                    {
                        cfg.SkipLoginUntilUpdate = false;
                        cfg.SkipLoginBoundVersion = "";
                        ConfigService.Save(cfg);
                    }
                    catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[SplashView] Skip-login reset persist failed: " + ex.Message); }
                    return false;
                }

                // Force-password-change users must NEVER auto-sign-in
                // without proving they know the new password first.
                if (await Task.Run(
                    () => AuditService.IsUsingDefaultPasswordAsync(username)))
                {
                    AuditService.LogSessionBoundary(login: false);
                    return false;
                }

                AuditService.LogSessionBoundary(login: true);
                AppLogService.Info(
                    "Auto sign-in succeeded for '" + username + "' (skip-login pill).",
                    new LogContext { Module = "Auth", Class = nameof(SplashView) });
                return true;
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Auto sign-in threw: " + ex.Message,
                    new LogContext { Module = "Auth", Class = nameof(SplashView) });
                return false;
            }
        }

        /// <summary>
        /// Replaces the loading strip with the sign-in form on THIS window.
        /// Never opens a second maximized window — that is what kept
        /// desyncing splash and login (Topmost fight + desktop flash).
        ///
        /// First tries <see cref="TryAutoLoginAsync"/>; if that succeeds
        /// the login form is never even mounted and the splash proceeds
        /// straight to the "opening workspace" fade. If it returns false
        /// the regular sign-in form is shown as before.
        /// </summary>
        public async Task<bool> RunLoginAsync()
        {
            // ── AUTO-SIGN-IN FAST PATH ────────────────────────────────
            //
            // A returning user who armed the "Skip login until update"
            // pill last session never sees the sign-in form again on
            // this device until we push an update. LoadingText stays
            // as "Signing you in…" for the microsecond it takes to
            // verify, then App.xaml.cs takes over.
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
                // Unsubscribe immediately so a subsequent Completed
                // raise (e.g. the user tapping Exit after already
                // signing in) cannot re-enter this handler for the
                // same TCS. TrySetResult would then be a no-op, but
                // symmetric unsubscribe is cleaner and keeps the
                // event invocation list from growing across nested
                // RunLoginAsync calls (first-run failure fallback).
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
            // can lose activation (e.g. the user clicked the desktop), and
            // controls in an inactive window ignore Focus(). Without this
            // the caret never lands in the field, so typing/Enter does
            // nothing until the user clicks.
            try { Activate(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SplashView.xaml] RunLoginAsync: window may be closing: " + ex.Message); }

            // Focus now, then re-arm once the fade + layout settle so the
            // caret is guaranteed in the field the moment the page appears.
            Dispatcher.BeginInvoke(new Action(() => LoginGate.FocusInitialFieldDeferred()),
                DispatcherPriority.Input);

            return await done.Task;
        }

        /// <summary>
        /// First-run setup flow. Shown when the audit
        /// database has no users yet.
        ///
        /// UX CONTRACT (industrial ERP norm — SAP / Odoo /
        /// Dynamics all behave this way):
        ///   • First boot ever   →  Splash → this setup panel
        ///     → straight into the main window. The operator
        ///     JUST typed their new password; forcing them to
        ///     re-type it in a login form on the same boot is
        ///     amateur UX.
        ///   • Every subsequent boot →  Splash → login form
        ///     → main window (unless the "Skip login until
        ///     update" pill was armed).
        ///
        /// After the operator creates the first administrator
        /// we silently call AuditService.SwitchUserAsync with
        /// the freshly-typed credentials and return true so
        /// App.xaml.cs proceeds to show the main window. The
        /// auto-login pill is deliberately NOT armed here —
        /// the operator has not seen that consent surface
        /// yet, so the next boot presents the login form and
        /// only writes the DPAPI-vault entry if the user
        /// explicitly opts in on that screen.
        ///
        /// Call this INSTEAD of <see cref="RunLoginAsync"/>
        /// on a fresh install. The choice is driven by
        /// App.xaml.cs based on
        /// <see cref="AuditService.HasAnyUsersAsync"/>.
        /// </summary>
        public Task<bool> RunFirstRunSetupAsync()
        {
            var done = new TaskCompletionSource<bool>();

            // The setup panel raises Created with the
            // (username, password) it just persisted. We
            // establish the session ourselves and complete
            // the task true so App.xaml.cs shows the main
            // window without any further prompting.
            async void OnCreated(string newUsername, string newPassword)
            {
                // Unsubscribe FIRST so a re-entrant Created (e.g. the
                // panel raised it twice due to a fast double-click that
                // slipped the re-entrancy guard) cannot double-invoke
                // this handler and complete `done` twice with different
                // results.
                SetupGate.Created -= OnCreated;

                // Lock the setup panel so the operator cannot
                // double-submit while the sign-in is in progress.
                SetupGate.IsEnabled = false;
                LoadingText.Text = "Opening workspace…";

                // Seed the remembered username so the
                // NEXT boot's login form pre-fills it.
                // The auto-login pill (armed by
                // FirstRunSetupPanel.TryArmAutoLogin) is
                // what lets the user skip that form until
                // the next update — until then, subsequent
                // boots follow the standard splash → login
                // → main path.
                try
                {
                    if (App.Config != null)
                    {
                        App.Config.LastUsername = newUsername;
                        App.Config.RememberUsername = true;
                        ConfigService.Save(App.Config);
                    }
                }
                catch (System.Exception ex)
                {
                    // Best effort — the login form on any
                    // future boot tolerates an empty
                    // pre-filled username.
                    ProGlassAutomation.Services.Logging.AppLogService.Debug("[SplashView] Remember-username persist failed: " + ex.Message);
                }

                try
                {
                    // Establish the audit session for the
                    // freshly-created administrator. We
                    // just wrote their credentials in
                    // CreateUserAsync, so this cannot fail
                    // for a wrong-password reason — but we
                    // still surface a failure cleanly so
                    // App.xaml.cs never opens the main
                    // window with an unauthenticated
                    // context.
                    var (ok, error) = await Task.Run(
                        () => AuditService.SwitchUserAsync(newUsername, newPassword));

                    if (!ok)
                    {
                        AppLogService.Error(
                            "First-run silent sign-in failed for '" + newUsername +
                            "' immediately after CreateUserAsync: " + error, null,
                            new LogContext
                            {
                                Module = "Auth",
                                Class = nameof(SplashView),
                                Operation = "RunFirstRunSetupAsync"
                            });
                        // Fall back to the standard login form on
                        // this same boot so the operator can retry
                        // rather than being kicked back to the OS.
                        // LoadingText is set explicitly here so the
                        // brief "Signing you in…" flicker inside
                        // RunLoginAsync (its own auto-login probe,
                        // which will trivially fail because the
                        // vault is empty right after first-run) does
                        // not surface confusing status swaps.
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
                        new LogContext
                        {
                            Module = "Auth",
                            Class = nameof(SplashView),
                            Operation = "RunFirstRunSetupAsync"
                        });
                    done.TrySetResult(false);
                }
            }

            SetupGate.Created += OnCreated;
            Closed += (_, __) =>
            {
                // If the splash is closed while the
                // setup is running (the operator
                // clicked the X), the login never
                // happens, so report not-signed-in so
                // App.xaml.cs exits cleanly.
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

            try { Activate(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SplashView.xaml] RunFirstRunSetupAsync: window may be closing: " + ex.Message); }
            Dispatcher.BeginInvoke(new Action(() => SetupGate.Focus()),
                DispatcherPriority.Input);

            return done.Task;
        }

        public void FadeOutAndClose(Action onCompleted)
        {
            StopLoadingIndicators();

            // Stay Topmost for the fade. The window underneath is only
            // revealed by the opacity animation — releasing Topmost
            // first is what flashed the desktop between splash and
            // login. (2026-08-29: the splash is NOT a layered window —
            // AllowsTransparency removed; an opaque stage renders it
            // identically on every machine without the Win11
            // software-rendered layered-window failure class.)
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
        /// Stops the dots timer and resets the dots text. Shared by
        /// ShowTermsGate / RunLoginAsync / FadeOutAndClose.
        /// </summary>
        private void StopLoadingIndicators()
        {
            _loadingDotsTimer?.Stop();
            _loadingDotsTimer = null;
        }
    }
}
