// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/CrystalFluid/CrystalWater.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Crystal Fluid — water component logic (spec §14–§20, §43, §62,
//         §68, §70, §71)
//
// Governing laws implemented here:
//   §15  WaterLevel is 0..1; changes animate over CrystalWaterLevelDuration
//        (1.5s) with cubic easing.
//   §20  Continuous motion = transform storyboards only. The only layout
//        animation is the 1.5s HEIGHT tween on level change.
//   §43  CrystalReducedMotion=True (design token) → no storyboard ever
//        starts; the static body/surface still reads as water.
//   §62  Root clipped to CornerRadius so water NEVER escapes the card.
//   §68  No timers. One storyboard per instance, paused when hidden (§71),
//        stopped on Unload (§70: no leak through long-lived references).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ProGlassAutomation.Controls.CrystalFluid
{
    public partial class CrystalWater : UserControl
    {
        // ── Dependency properties (§61: clean reusable API) ──

        public static readonly DependencyProperty WaterEnabledProperty =
            DependencyProperty.Register(nameof(WaterEnabled), typeof(bool),
                typeof(CrystalWater),
                new FrameworkPropertyMetadata(false, OnWaterEnabledChanged));

        /// <summary>Master switch — false renders nothing (Collapsed).</summary>
        public bool WaterEnabled
        {
            get => (bool)GetValue(WaterEnabledProperty);
            set => SetValue(WaterEnabledProperty, value);
        }

        public static readonly DependencyProperty WaterLevelProperty =
            DependencyProperty.Register(nameof(WaterLevel), typeof(double),
                typeof(CrystalWater),
                new FrameworkPropertyMetadata(0.42, OnWaterLevelChanged)
                { CoerceValueCallback = CoerceUnitInterval });

        /// <summary>Normalized fill: 0 = empty … 1 = completely full (§15).</summary>
        public double WaterLevel
        {
            get => (double)GetValue(WaterLevelProperty);
            set => SetValue(WaterLevelProperty, value);
        }

        public static readonly DependencyProperty WaterOpacityProperty =
            DependencyProperty.Register(nameof(WaterOpacity), typeof(double),
                typeof(CrystalWater), new FrameworkPropertyMetadata(1.0)
                { CoerceValueCallback = CoerceUnitInterval });

        public double WaterOpacity
        {
            get => (double)GetValue(WaterOpacityProperty);
            set => SetValue(WaterOpacityProperty, value);
        }

        public static readonly DependencyProperty WaterSpeedProperty =
            DependencyProperty.Register(nameof(WaterSpeed), typeof(double),
                typeof(CrystalWater),
                new FrameworkPropertyMetadata(1.0, OnWaterSpeedChanged));

        /// <summary>Storyboard speed multiplier (sanity-range 0.25–3).</summary>
        public double WaterSpeed
        {
            get => (double)GetValue(WaterSpeedProperty);
            set => SetValue(WaterSpeedProperty, value);
        }

        public static readonly DependencyProperty WaveAmplitudeProperty =
            DependencyProperty.Register(nameof(WaveAmplitude), typeof(double),
                typeof(CrystalWater), new FrameworkPropertyMetadata(10.0));

        /// <summary>Surface-highlight band height in px (4–20 sensible).</summary>
        public double WaveAmplitude
        {
            get => (double)GetValue(WaveAmplitudeProperty);
            set => SetValue(WaveAmplitudeProperty, value);
        }

        public static readonly DependencyProperty BubbleEnabledProperty =
            DependencyProperty.Register(nameof(BubbleEnabled), typeof(bool),
                typeof(CrystalWater), new FrameworkPropertyMetadata(true));

        public bool BubbleEnabled
        {
            get => (bool)GetValue(BubbleEnabledProperty);
            set => SetValue(BubbleEnabledProperty, value);
        }

        public static readonly DependencyProperty CausticsEnabledProperty =
            DependencyProperty.Register(nameof(CausticsEnabled), typeof(bool),
                typeof(CrystalWater), new FrameworkPropertyMetadata(true));

        public bool CausticsEnabled
        {
            get => (bool)GetValue(CausticsEnabledProperty);
            set => SetValue(CausticsEnabledProperty, value);
        }

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius),
                typeof(CrystalWater),
                new FrameworkPropertyMetadata(new CornerRadius(0), OnClipInputsChanged));

        /// <summary>Host card's corner radius — the water clip (§62).</summary>
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        // WaterEnabled ≠ WaterOpacity: it also gates the storyboard.
        private static void OnWaterEnabledChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (CrystalWater)d;
            self.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            self.SyncAmbientState();
        }

        private static object CoerceUnitInterval(
            DependencyObject d, object value) =>
            Math.Max(0.0, Math.Min(1.0, (double)value));

        // ── Instance state ────────────────────────────────────────────

        private readonly Storyboard _ambient;
        private bool _ambientStarted;
        private bool _reducedMotion;
        private double _renderedLevel = -1; // <0 = not yet painted

        public CrystalWater()
        {
            InitializeComponent();
            _ambient = (Storyboard)Resources["AmbientStoryboard"];

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += OnIsVisibleChanged;
            SizeChanged += OnSizeChanged;

            // Start enabled-state honest: hidden until WaterEnabled is set.
            Visibility = WaterEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // §43: the global token is consulted AT LOAD (theme-time), not
            // polled — a studio toggle repaint re-creates the surface.
            _reducedMotion = TryFindResource("CrystalReducedMotion") is bool b && b;
            ApplyLevel(instant: true);
            LayoutBubbles();
            SyncAmbientState();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // §70: release the clock tree so the control can be collected.
            try { _ambient.Stop(this); } catch { /* never throw on unload */ }
            _ambientStarted = false;
        }

        private void OnIsVisibleChanged(object sender,
            DependencyPropertyChangedEventArgs e)
        {
            // §71: hidden cards/pages must not burn composition cycles.
            if (!_ambientStarted) return;
            if ((bool)e.NewValue)
                _ambient.Resume(this);
            else
                _ambient.Pause(this);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyClip();
            ApplyLevel(instant: true); // resize re-derives pixel height
            LayoutBubbles();
        }

        private static void OnClipInputsChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((CrystalWater)d).ApplyClip();

        /// <summary>§62 — the water may never escape the host card; the
        /// clip follows BOTH the live size and the card's corner radius.</summary>
        private void ApplyClip()
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;
            RootGrid.Clip = new RectangleGeometry(
                new Rect(0, 0, w, h), CornerRadius.TopLeft, CornerRadius.TopLeft);
        }

        private void LayoutBubbles()
        {
            double w = ActualWidth;
            if (w <= 0) return;
            Canvas.SetLeft(BubbleDot1, w * 0.22);
            Canvas.SetLeft(BubbleDot2, w * 0.55);
            Canvas.SetLeft(BubbleDot3, w * 0.80);
        }

        private static void OnWaterLevelChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (CrystalWater)d;
            if (!self.IsLoaded)
            {
                self._renderedLevel = (double)e.NewValue;
                return; // OnLoaded paints it instantly
            }
            self.ApplyLevel(instant: false);
        }

        /// <summary>§15 — level changes ease over ~1.5s (cubic). The
        /// animation targets a private DP so the pixel height stays a
        /// pure function of level × live height (resize-safe).</summary>
        private void ApplyLevel(bool instant)
        {
            double target = WaterLevel;
            if (instant || _renderedLevel < 0 || _reducedMotion)
            {
                BeginAnimation(AnimatedLevelProperty, null);
                AnimatedLevel = target;
                return;
            }

            var duration = TryFindResource("CrystalWaterLevelDuration") is Duration d
                ? d : new Duration(TimeSpan.FromSeconds(1.5));
            var anim = new DoubleAnimation(target, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(AnimatedLevelProperty, anim);
        }

        public static readonly DependencyProperty AnimatedLevelProperty =
            DependencyProperty.Register(nameof(AnimatedLevel), typeof(double),
                typeof(CrystalWater),
                new FrameworkPropertyMetadata(0.0, OnAnimatedLevelChanged));

        /// <summary>Internal: the eased level actually being painted.</summary>
        public double AnimatedLevel
        {
            get => (double)GetValue(AnimatedLevelProperty);
            set => SetValue(AnimatedLevelProperty, value);
        }

        private static void OnAnimatedLevelChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (CrystalWater)d;
            self._renderedLevel = (double)e.NewValue;
            double h = self.RootGrid?.ActualHeight ?? 0;
            self.WaterArea.Height = Math.Max(0, h * self._renderedLevel);
        }

        private static void OnWaterSpeedChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (CrystalWater)d;
            if (self._ambientStarted)
                self._ambient.SetSpeedRatio(self,
                    Math.Max(0.25, Math.Min(3.0, self.WaterSpeed)));
        }

        /// <summary>Starts/resumes the ambient choreography unless reduced
        /// motion, disabled, or already running — and pauses it when the
        /// water is not actually on screen.</summary>
        private void SyncAmbientState()
        {
            if (!IsLoaded) return;

            bool shouldRun = WaterEnabled && !_reducedMotion && IsVisible;
            if (shouldRun && !_ambientStarted)
            {
                _ambient.SetSpeedRatio(this,
                    Math.Max(0.25, Math.Min(3.0, WaterSpeed)));
                _ambient.Begin(this, isControllable: true);
                _ambientStarted = true;
            }
            else if (!shouldRun && _ambientStarted)
            {
                _ambient.Pause(this);
            }
            else if (shouldRun && _ambientStarted && IsVisible)
            {
                _ambient.Resume(this);
            }
        }
    }
}
