// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Splash/SplashView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Splash Screen — industrial glass identity
//
// Professional glass choreography (~2.5s, finishes inside App.xaml.cs
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

using ProGlassAutomation.Services.Logging;
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
        //  FEATURE MODULE DATA — frosted glass chips
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

            var frost = Frozen(0xBF, 0xDB, 0xFE); // #BFDBFE
            var cerulean = Frozen(0x93, 0xC5, 0xFD); // #93C5FD
            var azure = Frozen(0x60, 0xA5, 0xFA); // #60A5FA
            var sapphire = Frozen(0x3B, 0x82, 0xF6); // #3B82F6
            var royal = Frozen(0x1D, 0x4E, 0xD8); // #1D4ED8
            var navy = Frozen(0x1E, 0x3A, 0x8A); // #1E3A8A

            return new[]
            {
                new FeatureItem("\uE9F9", "Dashboard",    frost),
                new FeatureItem("\uE8A5", "Proforma",     cerulean),
                new FeatureItem("\uEA37", "Sheet Store",  azure),
                new FeatureItem("\uE9D9", "Job Orders",   sapphire),
                new FeatureItem("\uE7B8", "Delivery",     royal),
                new FeatureItem("\uE8C7", "Tax Invoice",  navy),
                new FeatureItem("\uE8F7", "Statements",   sapphire),
                new FeatureItem("\uE8C8", "Reports",      azure),
                new FeatureItem("\uE8F2", "AI Assistant", royal),
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
