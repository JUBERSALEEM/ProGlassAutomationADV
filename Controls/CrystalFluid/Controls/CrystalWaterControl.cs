// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/CrystalFluid/Controls/CrystalWaterControl.cs
// PROJECT: ProGlassAutomation
//
// IOS-17PM AUTHORED WATER CONTROL (P2 2026-09-05) — integrated port of
// the CrystalFluid reference implementation (incoming file 04), with the
// integration fix register applied. This control SUPERSEDES the interim
// CrystalWater UserControl (P4 migrates hosts and tombstones it).
//
// Defect fixes applied (register ids):
//   D1  Storyboards begun with Begin() are NOT controllable — Stop() on
//       them throws InvalidOperationException on unload. Every storyboard
//       now starts via Begin(this, true) and stops via Stop(this).
//   D3  Caustic To=ActualWidth*0.5 and bubble rise distance went stale
//       after host resize. OnRenderSizeChanged now repositions the wave
//       and restarts caustic/bubble storyboards against fresh geometry.
//   D4  WaterOpacity DP added (spec §61 axis was missing); the template
//       binds the root canvas opacity to it and the style feeds it from
//       the CrystalWaterOpacity token (Theme Studio live).
//   D5  ReducedMotion/WaterSpeed/BubbleOpacity default FROM TOKENS via
//       style setters (DynamicResource) — engine tests live; any host
//       may still pin a local value (local always wins).
//   D6  WaterSpeed clamp raised: coerce to [0.1, 3.0] (matches the
//       settings surface range; authored code only clamped downward).
//   D7  Wave was pinned to Canvas.Top=0 (top of the container, floating
//       over empty glass). The wave is now positioned at the WATERLINE
//       (ActualHeight*(1-WaterLevel) - half wave height) and follows
//       water-level animation.
//   D14 PART_SurfaceLine field removed — dead (template child did not
//       exist and the field was never read).
//   D15 WaveAmplitude was a dead DP (animation hardcoded ±3). The wave
//       translation now uses ±WaveAmplitude (coerced [0,10]).
//   +  Removed unused System.Windows.Threading import; shared static
//       Random (authored per-instance seeds landed identical bubble
//       streams when several cards are created in the same tick).
//       BubbleOpacity DP restored (interim-control studio parity):
//       CrystalBubbleOpacity token dims bubbles live.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ProGlassAutomation.Controls.CrystalFluid
{
    /// <summary>
    /// Renders animated water inside a crystal container: translucent body,
    /// surface wave at the waterline, caustic light, rising bubbles.
    /// Continuous motion runs on transforms only (composition thread —
    /// no timers, no layout churn); everything pauses on unload/hidden.
    /// </summary>
    public class CrystalWaterControl : Control
    {
        #region ── Dependency Properties ──

        public static readonly DependencyProperty WaterLevelProperty =
            DependencyProperty.Register(
                nameof(WaterLevel),
                typeof(double),
                typeof(CrystalWaterControl),
                new FrameworkPropertyMetadata(
                    0.30,   // mirrors the CrystalDefaultWaterLevel token
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnWaterLevelChanged,
                    CoerceUnitInterval));

        public static readonly DependencyProperty WaterEnabledProperty =
            DependencyProperty.Register(
                nameof(WaterEnabled),
                typeof(bool),
                typeof(CrystalWaterControl),
                // 2026-09-07: default FALSE — water is opt-IN via the
                // family gate (CrystalWaterDefaultEnabled), never on by
                // accident. The old TRUE default let the animated fill
                // render outside Ios-17pm whenever the gate resource was
                // missing (e.g. after a family switch removed it).
                new PropertyMetadata(false, OnWaterEnabledChanged));

        public static readonly DependencyProperty WaterOpacityProperty =
            DependencyProperty.Register(
                nameof(WaterOpacity),
                typeof(double),
                typeof(CrystalWaterControl),
                new FrameworkPropertyMetadata(
                    1.0, FrameworkPropertyMetadataOptions.AffectsRender,
                    null, CoerceUnitInterval));

        public static readonly DependencyProperty WaterSpeedProperty =
            DependencyProperty.Register(
                nameof(WaterSpeed),
                typeof(double),
                typeof(CrystalWaterControl),
                new PropertyMetadata(1.0, null, CoerceWaterSpeed));

        public static readonly DependencyProperty BubbleEnabledProperty =
            DependencyProperty.Register(
                nameof(BubbleEnabled),
                typeof(bool),
                typeof(CrystalWaterControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty BubbleCountProperty =
            DependencyProperty.Register(
                nameof(BubbleCount),
                typeof(int),
                typeof(CrystalWaterControl),
                new PropertyMetadata(3, OnBubbleCountChanged),
                ValidateBubbleCount);

        public static readonly DependencyProperty BubbleOpacityProperty =
            DependencyProperty.Register(
                nameof(BubbleOpacity),
                typeof(double),
                typeof(CrystalWaterControl),
                new PropertyMetadata(1.0, OnBubbleOpacityChanged,
                    CoerceUnitInterval));

        public static readonly DependencyProperty CausticsEnabledProperty =
            DependencyProperty.Register(
                nameof(CausticsEnabled),
                typeof(bool),
                typeof(CrystalWaterControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty WaveAmplitudeProperty =
            DependencyProperty.Register(
                nameof(WaveAmplitude),
                typeof(double),
                typeof(CrystalWaterControl),
                new PropertyMetadata(3.0, OnWaveAmplitudeChanged,
                    CoerceWaveAmplitude));

        public static readonly DependencyProperty ReducedMotionProperty =
            DependencyProperty.Register(
                nameof(ReducedMotion),
                typeof(bool),
                typeof(CrystalWaterControl),
                new PropertyMetadata(false, OnReducedMotionChanged));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(CrystalWaterControl),
                new PropertyMetadata(new CornerRadius(30)));

        #endregion

        #region ── CLR Properties ──

        public double WaterLevel
        {
            get => (double)GetValue(WaterLevelProperty);
            set => SetValue(WaterLevelProperty, value);
        }

        public bool WaterEnabled
        {
            get => (bool)GetValue(WaterEnabledProperty);
            set => SetValue(WaterEnabledProperty, value);
        }

        /// <summary>Whole-water opacity (D4). Fed by the CrystalWaterOpacity
        /// token through the style; the template binds the canvas to it.</summary>
        public double WaterOpacity
        {
            get => (double)GetValue(WaterOpacityProperty);
            set => SetValue(WaterOpacityProperty, value);
        }

        public double WaterSpeed
        {
            get => (double)GetValue(WaterSpeedProperty);
            set => SetValue(WaterSpeedProperty, value);
        }

        public bool BubbleEnabled
        {
            get => (bool)GetValue(BubbleEnabledProperty);
            set => SetValue(BubbleEnabledProperty, value);
        }

        public int BubbleCount
        {
            get => (int)GetValue(BubbleCountProperty);
            set => SetValue(BubbleCountProperty, value);
        }

        /// <summary>Live bubble dimmer (studio WaterFlow parity with the
        /// interim control). Multiplies bubble peak opacity.</summary>
        public double BubbleOpacity
        {
            get => (double)GetValue(BubbleOpacityProperty);
            set => SetValue(BubbleOpacityProperty, value);
        }

        public bool CausticsEnabled
        {
            get => (bool)GetValue(CausticsEnabledProperty);
            set => SetValue(CausticsEnabledProperty, value);
        }

        public double WaveAmplitude
        {
            get => (double)GetValue(WaveAmplitudeProperty);
            set => SetValue(WaveAmplitudeProperty, value);
        }

        public bool ReducedMotion
        {
            get => (bool)GetValue(ReducedMotionProperty);
            set => SetValue(ReducedMotionProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        #endregion

        #region ── Internal State ──

        private Canvas _waterCanvas;
        private Border _waterBody;
        private Path _wavePath;
        private Rectangle _causticLayer;
        private readonly List<Ellipse> _bubbles = new();
        // Shared generator: per-instance `new Random()` seeds collide when
        // several water controls are created in the same tick (dashboard
        // KPI rows), producing identical bubble choreography.
        private static readonly Random SharedRng = new();
        private Storyboard _waveStoryboard;
        private Storyboard _causticStoryboard;
        private readonly List<Storyboard> _bubbleStoryboards = new();
        private bool _isLoaded;
        private bool _animationsRunning;

        // 20 px is the wave path height declared in the template; the wave
        // is centered on the waterline at half of it.
        private const double WaveHeight = 20.0;

        #endregion

        #region ── Constructor ──

        static CrystalWaterControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(CrystalWaterControl),
                new FrameworkPropertyMetadata(typeof(CrystalWaterControl)));
        }

        public CrystalWaterControl()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += OnVisibilityChanged;
        }

        #endregion

        #region ── Template (template-part contract) ──

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _waterCanvas = GetTemplateChild("PART_WaterCanvas") as Canvas;
            _waterBody = GetTemplateChild("PART_WaterBody") as Border;
            _wavePath = GetTemplateChild("PART_WavePath") as Path;
            _causticLayer = GetTemplateChild("PART_CausticLayer") as Rectangle;

            UpdateWaterLevel(false);
        }

        #endregion

        #region ── Lifecycle ──

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            BuildBubbles();
            UpdateWaterLevel(false);
            StartAnimations();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            StopAnimations();   // D1: Stop(this) — controllable, no throw
        }

        private void OnVisibilityChanged(object sender,
            DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue && _isLoaded)
                StartAnimations();
            else
                StopAnimations();
        }

        #endregion

        #region ── Property Changed Callbacks ──

        private static object CoerceUnitInterval(DependencyObject d, object value)
        {
            double v = (double)value;
            return Math.Max(0.0, Math.Min(1.0, v));
        }

        // D6: settings surface allows up to 3.0; the authored code only
        // clamped the lower edge, letting absurd speeds through.
        private static object CoerceWaterSpeed(DependencyObject d, object value)
        {
            double v = (double)value;
            return Math.Max(0.1, Math.Min(3.0, v));
        }

        private static object CoerceWaveAmplitude(DependencyObject d, object value)
        {
            double v = (double)value;
            return Math.Max(0.0, Math.Min(10.0, v));
        }

        private static void OnWaterLevelChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is CrystalWaterControl ctrl && ctrl._isLoaded)
                ctrl.UpdateWaterLevel(true);
        }

        private static void OnWaterEnabledChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is CrystalWaterControl ctrl)
            {
                if ((bool)e.NewValue)
                {
                    ctrl.UpdateWaterLevel(false);
                    ctrl.StartAnimations();
                }
                else
                {
                    ctrl.StopAnimations();
                }

                if (ctrl._waterCanvas != null)
                    ctrl._waterCanvas.Visibility =
                        (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static void OnBubbleCountChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is CrystalWaterControl ctrl && ctrl._isLoaded)
                ctrl.BuildBubbles();
        }

        private static void OnBubbleOpacityChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            // Next bubble cycle picks the multiplier up; restart now so a
            // studio WaterFlow drag reads live (interim-control parity).
            if (d is CrystalWaterControl ctrl && ctrl._animationsRunning)
            {
                ctrl.StopBubbleAnimations();
                ctrl.StartBubbleAnimations();
            }
        }

        private static void OnWaveAmplitudeChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is CrystalWaterControl ctrl && ctrl._animationsRunning)
            {
                ctrl.StopWaveAnimation();
                ctrl.StartWaveAnimation();
            }
        }

        private static void OnReducedMotionChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is CrystalWaterControl ctrl && ctrl._isLoaded)
            {
                ctrl.StopAnimations();
                if (!(bool)e.NewValue)
                    ctrl.StartAnimations();
            }
        }

        private static bool ValidateBubbleCount(object value)
        {
            int v = (int)value;
            return v >= 0 && v <= 10;
        }



        #endregion

        #region ── Water Level & Wave Position (D7) ──

        private void UpdateWaterLevel(bool animate)
        {
            if (_waterBody == null || _waterCanvas == null)
                return;

            double targetHeight = ActualHeight * WaterLevel;
            double waveTop = WaveTopForCurrentLevel();

            if (animate && !ReducedMotion)
            {
                var duration = new Duration(TimeSpan.FromSeconds(1.5));
                var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

                _waterBody.BeginAnimation(HeightProperty,
                    new DoubleAnimation { To = targetHeight, Duration = duration, EasingFunction = easing });

                if (_wavePath != null)
                {
                    _wavePath.BeginAnimation(Canvas.TopProperty,
                        new DoubleAnimation { To = waveTop, Duration = duration, EasingFunction = easing });
                }
            }
            else
            {
                _waterBody.BeginAnimation(HeightProperty, null);
                _waterBody.Height = targetHeight;

                if (_wavePath != null)
                {
                    _wavePath.BeginAnimation(Canvas.TopProperty, null);
                    Canvas.SetTop(_wavePath, waveTop);
                }
            }
        }

        // The wave rides the waterline: waterline sits at
        // ActualHeight - body height, measured from the canvas top.
        private double WaveTopForCurrentLevel()
            => (ActualHeight * (1.0 - WaterLevel)) - (WaveHeight / 2.0);

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateWaterLevel(false);
            UpdateClip();

            // D3: caustic travel and bubble rise are computed from live
            // geometry — restart them so a resize doesn't leave the
            // choreography stale (matches the authored ambient background's
            // rebuild-on-resize posture).
            if (_isLoaded && _animationsRunning &&
                (sizeInfo.WidthChanged || sizeInfo.HeightChanged))
            {
                StopCausticAnimation();
                StartCausticAnimation();
                StopBubbleAnimations();
                RescatterBubbles();
                StartBubbleAnimations();
            }
        }

        private void UpdateClip()
        {
            if (_waterCanvas == null)
                return;

            double r = CornerRadius.TopLeft;
            _waterCanvas.Clip = new RectangleGeometry(
                new Rect(0, 0, ActualWidth, ActualHeight),
                r, r);
        }

        #endregion

        #region ── Animation Orchestration (D1: controllable pattern) ──

        private void StartAnimations()
        {
            if (!WaterEnabled || ReducedMotion || !_isLoaded || !IsVisible)
                return;

            StartWaveAnimation();
            StartCausticAnimation();
            StartBubbleAnimations();
            _animationsRunning = true;
        }

        private void StopAnimations()
        {
            StopWaveAnimation();
            StopCausticAnimation();
            StopBubbleAnimations();
            _animationsRunning = false;
        }

        #endregion

        #region ── Wave Animation (D15: WaveAmplitude consumed) ──

        private void StartWaveAnimation()
        {
            if (_wavePath == null)
                return;

            double speed = Math.Max(0.1, WaterSpeed);
            double duration = 9.0 / speed;   // CrystalWaterWaveDuration (9 s) / speed
            double amp = WaveAmplitude;

            var translateAnim = new DoubleAnimation
            {
                From = -amp,
                To = amp,
                Duration = TimeSpan.FromSeconds(duration),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            var scaleAnim = new DoubleAnimation
            {
                From = 1.0,
                To = 1.035,
                Duration = TimeSpan.FromSeconds(duration),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            var transform = new TransformGroup();
            transform.Children.Add(new TranslateTransform());
            transform.Children.Add(new ScaleTransform(1, 1));
            _wavePath.RenderTransform = transform;
            _wavePath.RenderTransformOrigin = new Point(0.5, 0.5);

            _waveStoryboard = new Storyboard();

            Storyboard.SetTarget(translateAnim, _wavePath);
            Storyboard.SetTargetProperty(translateAnim,
                new PropertyPath("RenderTransform.Children[0].X"));

            Storyboard.SetTarget(scaleAnim, _wavePath);
            Storyboard.SetTargetProperty(scaleAnim,
                new PropertyPath("RenderTransform.Children[1].ScaleX"));

            _waveStoryboard.Children.Add(translateAnim);
            _waveStoryboard.Children.Add(scaleAnim);
            _waveStoryboard.Begin(this, true);   // D1: controllable
        }

        private void StopWaveAnimation()
        {
            _waveStoryboard?.Stop(this);         // D1: safe, never throws
            _waveStoryboard = null;
        }

        #endregion

        #region ── Caustic Animation ──

        private void StartCausticAnimation()
        {
            if (_causticLayer == null || !CausticsEnabled)
                return;

            var transform = new TranslateTransform(0, 0);
            _causticLayer.RenderTransform = transform;

            var anim = new DoubleAnimation
            {
                From = 0,
                To = ActualWidth * 0.5,
                Duration = TimeSpan.FromSeconds(11),   // CrystalCausticDuration (11 s)
                RepeatBehavior = RepeatBehavior.Forever
            };

            _causticStoryboard = new Storyboard();
            Storyboard.SetTarget(anim, _causticLayer);
            Storyboard.SetTargetProperty(anim,
                new PropertyPath("RenderTransform.X"));
            _causticStoryboard.Children.Add(anim);
            _causticStoryboard.Begin(this, true);      // D1: controllable
        }

        private void StopCausticAnimation()
        {
            _causticStoryboard?.Stop(this);
            _causticStoryboard = null;
        }

        #endregion

        #region ── Bubble System ──

        private void BuildBubbles()
        {
            if (_waterCanvas == null)
                return;

            foreach (var bubble in _bubbles)
                _waterCanvas.Children.Remove(bubble);
            _bubbles.Clear();

            StopBubbleAnimations();

            if (!BubbleEnabled || BubbleCount <= 0)
                return;

            for (int i = 0; i < BubbleCount; i++)
            {
                var bubble = CreateBubble();
                _bubbles.Add(bubble);
                _waterCanvas.Children.Add(bubble);
            }

            if (!ReducedMotion && _isLoaded && IsVisible)
                StartBubbleAnimations();
        }

        private Ellipse CreateBubble()
        {
            double size = 4 + SharedRng.NextDouble() * 4; // 4–8 px

            var bubble = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = TryFindResource("CrystalBubbleFillBrush") as Brush
                       ?? Brushes.Transparent,
                Stroke = TryFindResource("CrystalBubbleBorderBrush") as Brush
                         ?? Brushes.Transparent,
                StrokeThickness = 0.8,
                Opacity = 0,
                IsHitTestVisible = false
            };

            double left = SharedRng.NextDouble() * 0.8 + 0.1; // 10–90 %
            Canvas.SetLeft(bubble, ActualWidth * left);
            Canvas.SetBottom(bubble, -10);

            return bubble;
        }

        // Re-randomize horizontal lanes on resize so a wider card doesn't
        // keep bubbles clustered at stale coordinates (D3 companion).
        private void RescatterBubbles()
        {
            foreach (var bubble in _bubbles)
            {
                double left = SharedRng.NextDouble() * 0.8 + 0.1;
                Canvas.SetLeft(bubble, ActualWidth * left);
            }
        }

        private void StartBubbleAnimations()
        {
            if (!BubbleEnabled || _bubbles.Count == 0)
                return;

            foreach (var bubble in _bubbles)
            {
                double delay = SharedRng.NextDouble() * 6;          // staggered
                double duration = 6 + SharedRng.NextDouble() * 4;   // 6–10 s
                double maxRise = ActualHeight * WaterLevel + 20;
                double horizontalDrift = (SharedRng.NextDouble() - 0.5) * 40;

                var sb = CreateBubbleStoryboard(
                    bubble, duration, delay, maxRise, horizontalDrift);
                _bubbleStoryboards.Add(sb);
                sb.Begin(this, true);                // D1: controllable
            }
        }

        private void StopBubbleAnimations()
        {
            foreach (var sb in _bubbleStoryboards)
                sb.Stop(this);                       // D1: safe
            _bubbleStoryboards.Clear();
        }

        private Storyboard CreateBubbleStoryboard(
            Ellipse bubble,
            double durationSecs,
            double delaySecs,
            double riseDistance,
            double drift)
        {
            var duration = new Duration(TimeSpan.FromSeconds(durationSecs));
            var delay = TimeSpan.FromSeconds(delaySecs);
            // P3 (absolute BubbleIntensity axis): the token/engine/studio
            // express the AUTHORED peak opacity directly (baseline 0.70);
            // no multiplier math here.
            double peakOpacity = Math.Max(0.0, Math.Min(1.0, BubbleOpacity));

            var transform = new TransformGroup();
            transform.Children.Add(new TranslateTransform(0, 0));
            transform.Children.Add(new ScaleTransform(0.5, 0.5));
            bubble.RenderTransform = transform;
            bubble.RenderTransformOrigin = new Point(0.5, 0.5);

            var riseAnim = new DoubleAnimation
            {
                From = 0,
                To = -riseDistance,
                Duration = duration,
                BeginTime = delay,
                RepeatBehavior = RepeatBehavior.Forever
            };

            var driftAnim = new DoubleAnimation
            {
                From = 0,
                To = drift,
                Duration = duration,
                BeginTime = delay,
                RepeatBehavior = RepeatBehavior.Forever
            };

            var scaleXAnim = new DoubleAnimation
            {
                From = 0.5,
                To = 1.15,
                Duration = duration,
                BeginTime = delay,
                RepeatBehavior = RepeatBehavior.Forever
            };

            var scaleYAnim = new DoubleAnimation
            {
                From = 0.5,
                To = 1.15,
                Duration = duration,
                BeginTime = delay,
                RepeatBehavior = RepeatBehavior.Forever
            };

            // Opacity: 0 → peak (at 15 %) → 0
            var opacityAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = duration,
                BeginTime = delay,
                RepeatBehavior = RepeatBehavior.Forever
            };
            opacityAnim.KeyFrames.Add(
                new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
            opacityAnim.KeyFrames.Add(
                new LinearDoubleKeyFrame(peakOpacity, KeyTime.FromPercent(0.15)));
            opacityAnim.KeyFrames.Add(
                new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));

            var sb = new Storyboard();

            Storyboard.SetTarget(riseAnim, bubble);
            Storyboard.SetTargetProperty(riseAnim,
                new PropertyPath("RenderTransform.Children[0].Y"));

            Storyboard.SetTarget(driftAnim, bubble);
            Storyboard.SetTargetProperty(driftAnim,
                new PropertyPath("RenderTransform.Children[0].X"));

            Storyboard.SetTarget(scaleXAnim, bubble);
            Storyboard.SetTargetProperty(scaleXAnim,
                new PropertyPath("RenderTransform.Children[1].ScaleX"));

            Storyboard.SetTarget(scaleYAnim, bubble);
            Storyboard.SetTargetProperty(scaleYAnim,
                new PropertyPath("RenderTransform.Children[1].ScaleY"));

            Storyboard.SetTarget(opacityAnim, bubble);
            Storyboard.SetTargetProperty(opacityAnim,
                new PropertyPath("Opacity"));

            sb.Children.Add(riseAnim);
            sb.Children.Add(driftAnim);
            sb.Children.Add(scaleXAnim);
            sb.Children.Add(scaleYAnim);
            sb.Children.Add(opacityAnim);

            return sb;
        }

        #endregion
    }
}
