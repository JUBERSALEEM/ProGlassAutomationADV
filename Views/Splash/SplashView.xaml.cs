using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ProGlassAutomation.Views.Splash
{
    public partial class SplashView : Window
    {
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
                Duration = TimeSpan.FromMilliseconds(600),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
            };
            var logoScaleY = logoScaleX.Clone();
            LogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, logoScaleX);
            LogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, logoScaleY);

            // 2) App name fade + slide up (delayed)
            var nameFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                BeginTime = TimeSpan.FromMilliseconds(400)
            };
            var nameSlide = new DoubleAnimation
            {
                From = 15,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(400),
                BeginTime = TimeSpan.FromMilliseconds(400),
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
                BeginTime = TimeSpan.FromMilliseconds(700)
            };
            TaglineText.BeginAnimation(OpacityProperty, taglineFade);

            // 4) Loading dots fade + individual bounce
            var dotsFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300),
                BeginTime = TimeSpan.FromMilliseconds(1000)
            };
            LoadingDots.BeginAnimation(OpacityProperty, dotsFade);

            AnimateDot(Dot1, 1200);
            AnimateDot(Dot2, 1350);
            AnimateDot(Dot3, 1500);

            // 5) Progress bar fill
            var progressAnim = new DoubleAnimation
            {
                From = 0,
                To = 220,
                Duration = TimeSpan.FromMilliseconds(2000),
                BeginTime = TimeSpan.FromMilliseconds(600),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            ProgressFill.BeginAnimation(WidthProperty, progressAnim);
        }

        private void AnimateDot(Ellipse dot, int beginMs)
        {
            var anim = new DoubleAnimation
            {
                From = 0.3,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(500),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromMilliseconds(beginMs)
            };
            dot.BeginAnimation(OpacityProperty, anim);
        }

        // Fade out splash before closing
        public void FadeOutAndClose(Action onCompleted)
        {
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