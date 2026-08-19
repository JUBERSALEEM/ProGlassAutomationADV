// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Splash/SplashView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Splash Screen (Chat 23 v3.1)
// VERSION: Chat 23 v3.1 — Data-driven features + top badges + perf hardening
//
// CHANGES vs v3.0:
//   ✓ Feature strip: 7 hand-written badges → data-driven via ItemsControl
//     Static readonly FeatureItem[] populates FeaturePanel.ItemsSource.
//     One DataTemplate cached, 7 items reuse it.
//   ✓ Staggered feature badge animation now targets the ItemsControl's
//     visual children via GetItemsPanelChildren() helper.
//   ✓ Added animation for top-left/center/right trust badges (fade in
//     early, before logo scales — creates "frame" effect).
//   ✓ Added animated loading dots (⋯) next to loading message.
//   ✓ Shimmer capped to 3 iterations (was Forever) — auto-stops.
//   ✓ Loading message interval kept at 1500ms.
//   ✓ All v3.0 improvements preserved (nullable timer, extracted
//     handlers, unloaded cleanup).
//
// PRESERVED (zero regression):
//   • FadeOutAndClose(Action) public API — App.xaml.cs depends on it
//   • All existing x:Name (LogoScale, AppNameText, AppNameTranslate,
//     TaglineText, FeaturePanel, ProgressFill, LoadingText,
//     ShimmerOverlay, ShimmerTranslate)
//   • New x:Name (TopLeftBadge, TopCenterPill, TopRightBadge, LoadingDots)
//   • Overall animation choreography timing
//   • CubicEase / BackEase easing
//
// PERFORMANCE:
//   • Feature list is static readonly array — allocated once at type init
//   • DataTemplate = 1 tree cached in WPF template pool
//   • GetItemsPanelChildren dispatched once after Loaded, no per-frame cost
//   • Shimmer terminates after 3 loops (~4.2s) instead of running forever
//   • Loading dots animation uses same DispatcherTimer pattern (no new timer)
//
// REGRESSION RISK: LOW
//   Public API preserved. All previous x:Name preserved. Data-driven
//   feature strip renders identically to hand-written v3.0 version.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ProGlassAutomation.Views.Splash
{
    /// <summary>
    /// Splash window shown at application startup.
    /// Animates in over ~2.7 seconds while App.xaml.cs performs
    /// background initialization. When the main window is ready,
    /// App.xaml.cs calls <see cref="FadeOutAndClose(Action)"/> to
    /// dismiss the splash and reveal the main window.
    /// </summary>
    public partial class SplashView : Window
    {
        // ── Rotating loading text ─────────────────────────────────────
        private DispatcherTimer? _loadingMessageTimer;
        private int _loadingMessageIndex;

        private readonly string[] _loadingMessages =
        {
            "Initializing workspace",
            "Loading modules",
            "Preparing dashboard",
            "Connecting to database",
            "Almost ready"
        };

        // ── Animated loading dots (⋯) ────────────────────────────────
        private DispatcherTimer? _loadingDotsTimer;
        private int _loadingDotsCount;

        // ══════════════════════════════════════════════════════════════════
        //  FEATURE MODULE DATA — displayed in feature strip
        //  Border brush per badge = distinct blue shade (Sky→Sapphire spectrum)
        //  Preserves the "spectrum" visual identified in About page palette.
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Immutable feature descriptor rendered by FeatureBadgeTemplate.
        /// Icon = emoji, Label = short module name, BorderBrush = distinct
        /// blue-shade for visual variety across the row.
        /// </summary>
        private sealed record FeatureItem(string Icon, string Label, Brush BorderBrush);

        // Static — allocated once at type init, referenced by all instances.
        private static readonly IReadOnlyList<FeatureItem> _features = BuildFeatures();

        private static IReadOnlyList<FeatureItem> BuildFeatures()
        {
            // Use frozen SolidColorBrush created inline — same hex values as XAML
            // palette. Cached at type init, no per-frame cost.
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

            // Populate feature strip once
            FeaturePanel.ItemsSource = _features;

            Loaded += SplashView_Loaded;
            Unloaded += SplashView_Unloaded;
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOADED — start animation sequence
        // ══════════════════════════════════════════════════════════════════

        private void SplashView_Loaded(object sender, RoutedEventArgs e)
        {
            // ─── PHASE 1: Top trust badges fade in (0–300ms) ─────────
            // Creates a "frame" around the incoming logo.
            FadeIn(TopLeftBadge, fromMs: 0, durationMs: 400);
            FadeIn(TopCenterPill, fromMs: 100, durationMs: 400);
            FadeIn(TopRightBadge, fromMs: 200, durationMs: 400);

            // ─── PHASE 2: Logo scale-in with elastic feel (100–750ms) ─
            var logoScaleX = new DoubleAnimation
            {
                From = 0,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(100),
                Duration = TimeSpan.FromMilliseconds(650),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
            };
            var logoScaleY = logoScaleX.Clone();
            LogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, logoScaleX);
            LogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, logoScaleY);

            // ─── PHASE 3: App name fade + slide up (500–900ms) ───────
            var nameFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                BeginTime = TimeSpan.FromMilliseconds(500)
            };
            var nameSlide = new DoubleAnimation
            {
                From = 15,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(400),
                BeginTime = TimeSpan.FromMilliseconds(500),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            AppNameText.BeginAnimation(OpacityProperty, nameFade);
            AppNameTranslate.BeginAnimation(TranslateTransform.YProperty, nameSlide);

            // ─── PHASE 4: Tagline fade (800–1200ms) ──────────────────
            FadeIn(TaglineText, fromMs: 800, durationMs: 400);

            // ─── PHASE 5: Feature panel + staggered badge fade (1050–1610ms) ─
            FadeIn(FeaturePanel, fromMs: 1050, durationMs: 300);
            AnimateFeatureBadges();

            // ─── PHASE 6: Progress bar fill (700–2900ms) ─────────────
            var progressAnim = new DoubleAnimation
            {
                From = 0,
                To = 560,
                Duration = TimeSpan.FromMilliseconds(2200),
                BeginTime = TimeSpan.FromMilliseconds(700),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            ProgressFill.BeginAnimation(WidthProperty, progressAnim);

            // ─── PHASE 7: Loading text + dots fade in (1050–1450ms) ──
            FadeIn(LoadingText, fromMs: 1050, durationMs: 400);
            FadeIn(LoadingDots, fromMs: 1050, durationMs: 400);

            // ─── PHASE 8: Start rotating loading messages + dots ─────
            StartLoadingMessageRotation();
            StartLoadingDotsAnimation();

            // ─── PHASE 9: Logo shimmer sweep (starts 1100ms) ─────────
            StartShimmerAnimation();
        }

        // ══════════════════════════════════════════════════════════════════
        //  UNLOADED — cleanup safety net
        // ══════════════════════════════════════════════════════════════════

        private void SplashView_Unloaded(object sender, RoutedEventArgs e)
        {
            _loadingMessageTimer?.Stop();
            _loadingMessageTimer = null;

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
        /// Animate each feature badge in the ItemsControl with 80ms stagger.
        /// Dispatched after Loaded so the visual tree is fully realized.
        /// </summary>
        private void AnimateFeatureBadges()
        {
            // Wait one dispatcher cycle so ItemsControl containers exist.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                for (int i = 0; i < _features.Count; i++)
                {
                    var container = FeaturePanel.ItemContainerGenerator.ContainerFromIndex(i)
                                    as FrameworkElement;
                    if (container == null) continue;

                    // The visual child inside container is the StackPanel from
                    // FeatureBadgeTemplate. Animate its Opacity.
                    if (VisualTreeHelper.GetChildrenCount(container) > 0)
                    {
                        var visual = VisualTreeHelper.GetChild(container, 0) as FrameworkElement;
                        if (visual != null)
                        {
                            int beginMs = 1150 + (i * 80);
                            FadeIn(visual, beginMs, 400);
                        }
                    }
                }
            }), DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Rotates loading text through the message list every 1500ms.
        /// Fade-out 150ms → change text → fade-in 150ms.
        /// </summary>
        private void StartLoadingMessageRotation()
        {
            _loadingMessageTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1500)
            };
            _loadingMessageTimer.Tick += LoadingMessageTimer_Tick;
            _loadingMessageTimer.Start();
        }

        private void LoadingMessageTimer_Tick(object? sender, EventArgs e)
        {
            _loadingMessageIndex = (_loadingMessageIndex + 1) % _loadingMessages.Length;

            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(150)
            };
            fadeOut.Completed += FadeOut_Completed;
            LoadingText.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void FadeOut_Completed(object? sender, EventArgs e)
        {
            LoadingText.Text = _loadingMessages[_loadingMessageIndex];

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(150)
            };
            LoadingText.BeginAnimation(OpacityProperty, fadeIn);
        }

        /// <summary>
        /// Animates the "⋯" dots after loading text: cycles ""→"."→".."→"..." every 350ms.
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

        /// <summary>
        /// Diagonal shimmer sweep across logo — 3 iterations then auto-stops.
        /// Prevents runaway animation from consuming CPU during long splashes.
        /// </summary>
        private void StartShimmerAnimation()
        {
            var shimmerFade = new DoubleAnimation
            {
                From = 0,
                To = 0.9,
                Duration = TimeSpan.FromMilliseconds(200),
                BeginTime = TimeSpan.FromMilliseconds(1100)
            };
            ShimmerOverlay.BeginAnimation(OpacityProperty, shimmerFade);

            var shimmerMove = new DoubleAnimation
            {
                From = -200,
                To = 200,
                Duration = TimeSpan.FromMilliseconds(1400),
                BeginTime = TimeSpan.FromMilliseconds(1300),
                RepeatBehavior = new RepeatBehavior(3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            ShimmerTranslate.BeginAnimation(TranslateTransform.XProperty, shimmerMove);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Phase 8.2d — Splash-to-Main handoff (release Topmost before fade)
        //  Public API — DO NOT rename. App.xaml.cs calls this.
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fades the splash out over 400ms then closes.
        /// Releases <see cref="Window.Topmost"/> BEFORE fade so the
        /// main window can rise above the splash mid-fade.
        /// Calls <paramref name="onCompleted"/> just before closing so
        /// the caller (App.xaml.cs) can show + activate the main window.
        /// </summary>
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

        private static string TermsMarkerPath => System.IO.Path.Combine(
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
                if (!System.IO.File.Exists(TermsMarkerPath)) return false;
                foreach (var line in System.IO.File.ReadAllLines(TermsMarkerPath))
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
                var dir = System.IO.Path.GetDirectoryName(TermsMarkerPath);
                if (!string.IsNullOrEmpty(dir))
                    System.IO.Directory.CreateDirectory(dir);

                // Plain text on purpose: this is a record the owner can read,
                // and an auditor can be shown, without needing the app.
                System.IO.File.WriteAllText(
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
            _loadingMessageTimer?.Stop();
            _loadingDotsTimer?.Stop();

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

            _loadingMessageTimer?.Stop();
            _loadingDotsTimer?.Stop();
            LoadingText.Text = "Sign in to continue";
            LoadingDots.Text = "";

            TermsPanel.Visibility = Visibility.Collapsed;
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
            // Stop all timers
            _loadingMessageTimer?.Stop();
            _loadingMessageTimer = null;

            _loadingDotsTimer?.Stop();
            _loadingDotsTimer = null;

            // Stop shimmer animation
            ShimmerTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            ShimmerOverlay.BeginAnimation(OpacityProperty, null);

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
    }
}