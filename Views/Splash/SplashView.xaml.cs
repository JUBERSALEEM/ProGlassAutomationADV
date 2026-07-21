using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ProGlassAutomation.Views.Splash
{
    public partial class SplashView : Window
    {
        private DispatcherTimer _loadingMessageTimer;
        private int _loadingMessageIndex = 0;

        private readonly string[] _loadingMessages = new[]
        {
            "Initializing workspace…",
            "Loading modules…",
            "Preparing dashboard…",
            "Connecting to database…",
            "Almost ready…"
        };

        public SplashView()
        {
            InitializeComponent();
            Loaded += SplashView_Loaded;
        }

        private void SplashView_Loaded(object sender, RoutedEventArgs e)
        {
            // 1) Logo scale-in with elastic feel
            var logoScaleX = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(650),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
            };
            var logoScaleY = logoScaleX.Clone();
            LogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, logoScaleX);
            LogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, logoScaleY);

            // 2) App name fade + slide up
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

            // 3) Tagline fade
            var taglineFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                BeginTime = TimeSpan.FromMilliseconds(800)
            };
            TaglineText.BeginAnimation(OpacityProperty, taglineFade);

            // 4) Feature panel container fade
            var featurePanelFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300),
                BeginTime = TimeSpan.FromMilliseconds(1100)
            };
            FeaturePanel.BeginAnimation(OpacityProperty, featurePanelFade);

            // 5) Staggered feature badge fade-in (100ms apart per badge)
            AnimateFeatureBadge(Feature1, 1200);
            AnimateFeatureBadge(Feature2, 1300);
            AnimateFeatureBadge(Feature3, 1400);
            AnimateFeatureBadge(Feature4, 1500);
            AnimateFeatureBadge(Feature5, 1600);
            AnimateFeatureBadge(Feature6, 1700);
            AnimateFeatureBadge(Feature7, 1800);

            // 6) Progress bar fill (span the new 360px width)
            var progressAnim = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromMilliseconds(2400),
                BeginTime = TimeSpan.FromMilliseconds(700),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            ProgressFill.BeginAnimation(WidthProperty, progressAnim);

            // 7) Loading text fade-in
            var loadingFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                BeginTime = TimeSpan.FromMilliseconds(1100)
            };
            LoadingText.BeginAnimation(OpacityProperty, loadingFade);

            // 8) Start rotating loading messages (every 600ms)
            StartLoadingMessageRotation();

            // 9) Start logo shimmer sweep (every 2.5s)
            StartShimmerAnimation();
        }

        // Fade-in single feature badge with slight upward slide
        private void AnimateFeatureBadge(FrameworkElement badge, int beginMs)
        {
            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                BeginTime = TimeSpan.FromMilliseconds(beginMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            badge.BeginAnimation(OpacityProperty, fade);
        }

        // Rotates loading text through the message list every 600ms
        private void StartLoadingMessageRotation()
        {
            _loadingMessageTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(600)
            };
            _loadingMessageTimer.Tick += (s, e) =>
            {
                _loadingMessageIndex = (_loadingMessageIndex + 1) % _loadingMessages.Length;

                // fade out → change text → fade in
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(150)
                };
                fadeOut.Completed += (_, __) =>
                {
                    LoadingText.Text = _loadingMessages[_loadingMessageIndex];
                    var fadeIn = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(150)
                    };
                    LoadingText.BeginAnimation(OpacityProperty, fadeIn);
                };
                LoadingText.BeginAnimation(OpacityProperty, fadeOut);
            };
            _loadingMessageTimer.Start();
        }

        // Diagonal shimmer sweep across logo — loops every 2.5s
        private void StartShimmerAnimation()
        {
            var shimmerFade = new DoubleAnimation
            {
                From = 0,
                To = 0.9,
                Duration = TimeSpan.FromMilliseconds(200),
                BeginTime = TimeSpan.FromMilliseconds(1200)
            };
            ShimmerOverlay.BeginAnimation(OpacityProperty, shimmerFade);

            var shimmerMove = new DoubleAnimation
            {
                From = -200,
                To = 200,
                Duration = TimeSpan.FromMilliseconds(1400),
                BeginTime = TimeSpan.FromMilliseconds(1400),
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            ShimmerTranslate.BeginAnimation(TranslateTransform.XProperty, shimmerMove);
        }

        // ══════════════════════════════════════════════════════════════════
        //  Phase 8.2d — Splash-to-Main handoff (release Topmost before fade)
        // ══════════════════════════════════════════════════════════════════
        public void FadeOutAndClose(Action onCompleted)
        {
            // Stop the rotating loading message timer
            _loadingMessageTimer?.Stop();

            // Release topmost BEFORE fade so main window can rise above us
            Topmost = false;

            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                // Invoke callback first — App.xaml.cs shows + activates main window
                onCompleted?.Invoke();
                // Then close ourselves — main window is already active and focused
                Close();
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}