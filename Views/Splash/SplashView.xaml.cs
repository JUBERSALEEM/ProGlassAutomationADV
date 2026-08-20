// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Splash/SplashView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Splash Screen — professional enterprise loading gate
//
// CHANGES vs "Aurora 2050" v3.x:
//   ✓ Removed the 9 animated module feature chips and their stagger
//     logic (AnimateFeatureBadges / FeatureItem / BuildFeatures) —
//     marketing clutter has no place on an enterprise loading gate.
//   ✓ Removed the rotating fake loading messages (timer + list) — the
//     splash now shows one honest status line with animated dots.
//   ✓ Removed the elastic BackEase logo bounce and the shimmer sweep
//     (StartShimmerAnimation) — replaced with a gentle settle-in.
//   ✓ Removed top trust-badge animations (TopLeftBadge / TopCenterPill
//     / TopRightBadge are gone from the XAML too).
//   ✓ Choreography tightened to ~2.3s so it finishes inside App.xaml.cs
//     startup delay (2800ms) with margin.
//
// PRESERVED (zero regression — App.xaml.cs depends on these):
//   • FadeOutAndClose(Action), RunLoginAsync(), ShowTermsGate()
//   • TermsAccepted / TermsResolved / SignedIn / HasAcceptedCurrentTerms
//   • TermsVersion, RecordAcceptance, AcceptTerms_Changed,
//     ContinueFromTerms_Click, DeclineTerms_Click
//   • SplashView_Loaded / SplashView_Unloaded timers + cleanup
//   • Animated loading dots (kept — subtle, not gimmicky)
//
// PERFORMANCE:
//   • One DispatcherTimer (dots) instead of three (messages + dots).
//   • No per-badge animations; no shimmer storyboard; no static arrays.
//   • All brushes frozen; animations one-shot (never repeat).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ProGlassAutomation.Views.Splash
{
    /// <summary>
    /// Splash window shown at application startup.
    /// Animates in over ~2.3 seconds while App.xaml.cs performs
    /// background initialization. When the main window is ready,
    /// App.xaml.cs calls <see cref="FadeOutAndClose(Action)"/> to
    /// dismiss the splash and reveal the main window.
    /// </summary>
    public partial class SplashView : Window
    {
        // ── Animated loading dots (⋯) ────────────────────────────────
        private DispatcherTimer? _loadingDotsTimer;
        private int _loadingDotsCount;

        public SplashView()
        {
            InitializeComponent();

            Loaded += SplashView_Loaded;
            Unloaded += SplashView_Unloaded;
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOADED — start animation sequence (calm, ~2.3s)
        // ══════════════════════════════════════════════════════════════════

        private void SplashView_Loaded(object sender, RoutedEventArgs e)
        {
            // ─── PHASE 1: Logo settles in (fade + gentle scale) ──────
            var logoScale = new DoubleAnimation
            {
                From = 0.92,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(100),
                Duration = TimeSpan.FromMilliseconds(450),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            LogoScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, logoScale);
            LogoScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, logoScale.Clone());

            var logoFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(100),
                Duration = TimeSpan.FromMilliseconds(450),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            LogoBorder.BeginAnimation(OpacityProperty, logoFade);

            // ─── PHASE 2: App name fade + slide up (450–850ms) ───────
            var nameFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                BeginTime = TimeSpan.FromMilliseconds(450)
            };
            var nameSlide = new DoubleAnimation
            {
                From = 12,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(400),
                BeginTime = TimeSpan.FromMilliseconds(450),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            AppNameText.BeginAnimation(OpacityProperty, nameFade);
            AppNameTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, nameSlide);

            // ─── PHASE 3: Tagline fade (750–1150ms) ──────────────────
            FadeIn(TaglineText, fromMs: 750, durationMs: 400);

            // ─── PHASE 4: Progress bar fill (700–2600ms) ─────────────
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

            // ─── PHASE 5: Status line fades in + dots start ──────────
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
        /// Replaces the loading strip with the sign-in form on THIS window.
        /// Never opens a second maximized window — that is what kept
        /// desyncing splash and login (Topmost fight + desktop flash).
        /// </summary>
        public Task<bool> RunLoginAsync()
        {
            var done = new TaskCompletionSource<bool>();

            void Finish(bool signedIn)
            {
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

            Dispatcher.BeginInvoke(new Action(() => LoginGate.FocusInitialField()),
                DispatcherPriority.Input);

            return done.Task;
        }

        public void FadeOutAndClose(Action onCompleted)
        {
            StopLoadingIndicators();

            // Stay Topmost for the fade. AllowsTransparency lets the
            // window underneath show through — releasing Topmost first
            // is what flashed the desktop between splash and login.
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
