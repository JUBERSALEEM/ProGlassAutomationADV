// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/CrystalFluid/Controls/CrystalAmbientBackground.cs
// PROJECT: ProGlassAutomation
//
// IOS-17PM AUTHORED AMBIENT BACKGROUND (P2 2026-09-05) — integrated port
// of the CrystalFluid reference implementation (incoming file 09). The
// "living environment": two radial light blobs drifting on sine paths
// over the static scene radials (composition per the authored dashboard
// reference: static environment brushes UNDER, this motion layer OVER).
//
// Defect fixes applied:
//   D9  OnUnloaded now stops the blob clocks. The authored handler only
//       flipped a flag, leaving two forever-storyboards animating an
//       off-tree control (composition-CPU leak, blobs kept alive).
//   D5  ReducedMotion defaults from the CrystalReducedMotion token
//       (SetResourceReference — live link — when no local value/binding
//       was supplied). A host override always wins.
//   +   18 s blob ride aligns with the CrystalAmbientDuration token;
//       23 s is its authored counter-rhythm (composition constants,
//       kept inline on purpose — they are choreography, not nouns).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ProGlassAutomation.Controls.CrystalFluid
{
    /// <summary>
    /// The living crystal environment background.
    /// Provides slow-drifting radial light blobs over the base gradient.
    /// </summary>
    public class CrystalAmbientBackground : Canvas
    {
        private Ellipse _blob1;
        private Ellipse _blob2;
        private bool _isLoaded;

        public static readonly DependencyProperty ReducedMotionProperty =
            DependencyProperty.Register(
                nameof(ReducedMotion),
                typeof(bool),
                typeof(CrystalAmbientBackground),
                new PropertyMetadata(false, OnReducedMotionChanged));

        public bool ReducedMotion
        {
            get => (bool)GetValue(ReducedMotionProperty);
            set => SetValue(ReducedMotionProperty, value);
        }

        public CrystalAmbientBackground()
        {
            ClipToBounds = true;
            IsHitTestVisible = false;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // D5: inherit the app reduced-motion token LIVE, unless the
            // host pinned its own local value or binding (local wins).
            if (ReadLocalValue(ReducedMotionProperty) == DependencyProperty.UnsetValue)
                SetResourceReference(ReducedMotionProperty, "CrystalReducedMotion");

            _isLoaded = true;
            BuildBlobs();
            StartAnimation();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            StopAnimation();   // D9: clocks stop with the tree
        }

        private static void OnReducedMotionChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is CrystalAmbientBackground bg && bg._isLoaded)
            {
                if ((bool)e.NewValue)
                    bg.StopAnimation();
                else
                    bg.StartAnimation();
            }
        }

        private void BuildBlobs()
        {
            Children.Clear();

            double w = ActualWidth > 0 ? ActualWidth : 1400;
            double h = ActualHeight > 0 ? ActualHeight : 900;

            // Blob 1: cyan breath over the upper-left environment highlight
            _blob1 = new Ellipse
            {
                Width = w * 0.6,
                Height = h * 0.6,
                IsHitTestVisible = false,
                Fill = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(
                            Color.FromArgb(51, 93, 207, 241), 0),
                        new GradientStop(
                            Color.FromArgb(0, 93, 207, 241), 1)
                    }
                },
                Opacity = 0.6
            };
            Canvas.SetLeft(_blob1, w * 0.05);
            Canvas.SetTop(_blob1, h * 0.15);
            Children.Add(_blob1);

            // Blob 2: white sheen drifting over the lower-right
            _blob2 = new Ellipse
            {
                Width = w * 0.5,
                Height = h * 0.5,
                IsHitTestVisible = false,
                Fill = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(
                            Color.FromArgb(166, 255, 255, 255), 0),
                        new GradientStop(
                            Color.FromArgb(0, 255, 255, 255), 1)
                    }
                },
                Opacity = 0.5
            };
            Canvas.SetLeft(_blob2, w * 0.55);
            Canvas.SetTop(_blob2, h * 0.45);
            Children.Add(_blob2);
        }

        private void StartAnimation()
        {
            if (ReducedMotion || _blob1 == null || _blob2 == null)
                return;

            double w = ActualWidth > 0 ? ActualWidth : 1400;
            double h = ActualHeight > 0 ? ActualHeight : 900;

            // Blob 1: drift right and up — 18 s rides CrystalAmbientDuration
            AnimateBlob(_blob1,
                w * 0.05, w * 0.05 + w * 0.08,
                h * 0.15, h * 0.15 - h * 0.05,
                18);

            // Blob 2: drift left and down — 23 s authored counter-rhythm
            AnimateBlob(_blob2,
                w * 0.55, w * 0.55 - w * 0.07,
                h * 0.45, h * 0.45 + h * 0.05,
                23);
        }

        private void AnimateBlob(Ellipse blob,
            double fromX, double toX,
            double fromY, double toY,
            double durationSec)
        {
            var dur = new Duration(TimeSpan.FromSeconds(durationSec));
            var ease = new SineEase { EasingMode = EasingMode.EaseInOut };

            blob.BeginAnimation(Canvas.LeftProperty,
                new DoubleAnimation(fromX, toX, dur)
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = ease
                });

            blob.BeginAnimation(Canvas.TopProperty,
                new DoubleAnimation(fromY, toY, dur)
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = ease
                });
        }

        private void StopAnimation()
        {
            _blob1?.BeginAnimation(Canvas.LeftProperty, null);
            _blob1?.BeginAnimation(Canvas.TopProperty, null);
            _blob2?.BeginAnimation(Canvas.LeftProperty, null);
            _blob2?.BeginAnimation(Canvas.TopProperty, null);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (_isLoaded)
            {
                StopAnimation();
                BuildBlobs();
                StartAnimation();
            }
        }
    }
}
