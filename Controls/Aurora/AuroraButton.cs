// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Aurora/AuroraButton.cs
// PROJECT: ProGlassAutomation
// MODULE: Aurora — the primary action
//
// ── PRESS IS A DAMPED SCALE, AND NOTHING ELSE MOVES ───────────────────
//
// Two rules meet here and they are not negotiable between them:
//
//   1. The SKY must not move in response to input. The backdrop drifts
//      on a 42-second cycle; if a button also nudged the blobs the two
//      motions would beat against each other and the whole screen would
//      read as unsteady. So this control never touches the mesh, and no
//      hover or press state animates a background position.
//
//   2. The BUTTON must visibly move, or it is not a control. WCAG 2.2 SC
//      1.4.1 asks for state that is not the only thing colour conveys,
//      and depth-tier alone would fail it anyway -- Aurora has no tiers.
//
// So: the fill ALPHA changes (hover lightens, press deepens -- pressed
// things recede, shared across every translucent family here), and the
// control DIPS. The dip is the only transform in the family, and it is
// deliberately the same 0.98 the Clay press host uses, so the gesture is
// identical across themes.
//
// ── REDUCED MOTION KILLS THE DIP, NOT THE ALPHA ──────────────────────
//
// The dip is ambience-adjacent, so it is suppressed when motion is off.
// The alpha change is not: it is the state signal itself, and removing it
// would leave a control whose pressed state is carried by a 6% alpha
// change alone, which is exactly the colour-only cue WCAG rules out.
// AuroraMetrics.MotionCeiling is 120ms so this can never become a float.
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Aurora;
using ProGlassAutomation.Services.Theme.Eight;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Aurora
{
    /// <summary>A panel in the sky that can be activated.</summary>
    public class AuroraButton : ButtonBase
    {
        public static readonly DependencyProperty AuroraPlacementProperty =
            DependencyProperty.Register(
                nameof(AuroraPlacement), typeof(AuroraPlacement),
                typeof(AuroraButton),
                new FrameworkPropertyMetadata(
                    AuroraPlacement.Panel,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnAuroraPropertyChanged),
                OnPlacementCoerce);

        public static readonly DependencyProperty AuroraDepthProperty =
            DependencyProperty.Register(
                nameof(AuroraDepth), typeof(SoftDepthTier),
                typeof(AuroraButton),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Raised,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnAuroraPropertyChanged));

        public static readonly DependencyProperty AuroraStateProperty =
            DependencyProperty.Register(
                nameof(AuroraState), typeof(SoftInteractionState),
                typeof(AuroraButton),
                new FrameworkPropertyMetadata(
                    SoftInteractionState.Rest,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnAuroraPropertyChanged));

        public static readonly DependencyProperty AuroraSelectedProperty =
            DependencyProperty.Register(
                nameof(AuroraSelected), typeof(bool),
                typeof(AuroraButton),
                new FrameworkPropertyMetadata(
                    false,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnAuroraPropertyChanged));

        /// <summary>
        /// Marks the primary action. This is the ONLY state that gets the
        /// accent glow -- see AuroraMetrics.GlowOpacity for why one glow
        /// per screen is a hierarchy and three is a haze.
        /// </summary>
        public static readonly DependencyProperty AuroraPrimaryProperty =
            DependencyProperty.Register(
                nameof(AuroraPrimary), typeof(bool),
                typeof(AuroraButton),
                new FrameworkPropertyMetadata(
                    false,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnAuroraPropertyChanged));

        public AuroraPlacement AuroraPlacement
        {
            get => (AuroraPlacement)GetValue(AuroraPlacementProperty);
            set => SetValue(AuroraPlacementProperty, value);
        }

        public SoftDepthTier AuroraDepth
        {
            get => (SoftDepthTier)GetValue(AuroraDepthProperty);
            set => SetValue(AuroraDepthProperty, value);
        }

        public SoftInteractionState AuroraState
        {
            get => (SoftInteractionState)GetValue(AuroraStateProperty);
            set => SetValue(AuroraStateProperty, value);
        }

        public bool AuroraSelected
        {
            get => (bool)GetValue(AuroraSelectedProperty);
            set => SetValue(AuroraSelectedProperty, value);
        }

        public bool AuroraPrimary
        {
            get => (bool)GetValue(AuroraPrimaryProperty);
            set => SetValue(AuroraPrimaryProperty, value);
        }

        /// <summary>
        /// A Button that claimed to be a Backdrop would be a panel with
        /// no ink and no rim -- unclickable and invisible at once.
        /// </summary>
        private static object OnPlacementCoerce(
            DependencyObject d, object baseValue)
        {
            return (AuroraPlacement)baseValue == AuroraPlacement.Backdrop
                ? AuroraPlacement.Panel
                : baseValue;
        }

        private static void OnAuroraPropertyChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AuroraButton b) b.Repaint();
        }

        private readonly AuroraSurfaceParts _parts = new AuroraSurfaceParts();
        private readonly ScaleTransform _scale = new ScaleTransform(1, 1);
        private bool _bound;
        private bool _wired;

        public AuroraButton()
        {
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            FocusVisualStyle = null;

            var group = new TransformGroup();
            group.Children.Add(_scale);
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = group;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (!_bound)
            {
                _parts.Bind(this);
                _bound = true;
                Wire();
            }

            Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
            AuroraPalette.Changed += (_, _) => Repaint();

            DependencyPropertyDescriptor.FromProperty(
                IsPressedProperty, typeof(ButtonBase))
                .AddValueChanged(this, (_, _) =>
                {
                    Repaint();
                    Dip(IsPressed);
                });

            DependencyPropertyDescriptor.FromProperty(
                IsKeyboardFocusedProperty, typeof(KeyboardNavigation))
                .AddValueChanged(this, (_, _) => Repaint());

            DependencyPropertyDescriptor.FromProperty(
                IsMouseOverProperty, typeof(ButtonBase))
                .AddValueChanged(this, (_, _) => Repaint());
        }

        /// <summary>
        /// The dip. A short scale on the control's own transform group --
        /// never on the mesh, never on a gradient stop.
        /// </summary>
        private void Dip(bool pressed)
        {
            double metrics = SafePressScale();
            if (metrics <= 0.0) return;

            double target = pressed ? metrics : 1.0;

            // Reduced MOTION, not reduced TRANSPARENCY. These are
            // different settings and were being conflated:
            // TransparencySuppressed comes from Windows high contrast and
            // from the user forcing opaque surfaces. A user in high
            // contrast has not asked for less animation, and a user who
            // has asked for less animation is very often NOT in high
            // contrast. Gating the dip on the wrong one meant the press
            // animation kept running for people who had specifically
            // turned motion down, and vanished for people who had not.
            if (!MotionAllowed())
            {
                // Clear any in-flight animation rather than merely
                // skipping the new one, or a half-finished dip is left
                // stranded mid-scale.
                _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                _scale.ScaleX = 1.0;
                _scale.ScaleY = 1.0;
                return;
            }

            var anim = new DoubleAnimation(
                target, new Duration(TimeSpan.FromMilliseconds(120)))
            {
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut,
                },
                FillBehavior = FillBehavior.HoldEnd,
            };

            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        /// <summary>
        /// Whether the control may animate. Motion is the user's
        /// accessibility setting, so it is read the same way every other
        /// family reads it and never guessed from an unrelated one.
        /// </summary>
        /// <summary>
        /// Which ramp this control belongs to. Read live so the dip
        /// tracks the same profile the fill was painted from.
        /// </summary>
        private static bool IsDark()
        {
            try { return ThemeManager.CurrentTheme == AppTheme.Dark; }
            catch (Exception) { return false; }
        }

        private static bool MotionAllowed()
        {
            try { return !EightThemePainter.ReducedMotionRequested; }
            catch (Exception) { return false; }
        }

        private static double SafePressScale()
        {
            try
            {
                var profile = AuroraPalette.For(IsDark());
                return profile?.Metrics.PressScale ?? 0.98;
            }
            catch (Exception) { return 0.98; }
        }

        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var state = ResolveState();

            AuroraSurfaceParts.Paint(
                _parts, dark, AuroraPlacement, AuroraDepth,
                state, interactive: true,
                selected: AuroraSelected || AuroraPrimary,
                AuroraSurface.FallbackFill(),
                AuroraSurface.FallbackInk());

            // PRIMARY FILLS WITH THE ACCENT.
            //
            // Done here rather than in the recipe because the accent is a
            // property of the control's ROLE, not of its state: a primary
            // button is primary in every state including rest, and the
            // recipe resolves state. Doing it in XAML instead would mean a
            // Style setter fighting Apply() on every Repaint, and the
            // winner would depend on template-apply order.
            if (AuroraPrimary && IsEnabled && _parts.Fill is not null)
            {
                var profile = AuroraPalette.For(dark);
                Color fill = profile?.Accent ?? AuroraSurface.FallbackInk();
                if (state != SoftInteractionState.Hover)
                {
                    // Hover LIGHTENS the panel. Applied to the accent too,
                    // so primary and ghost hover identically.
                    fill = Color.FromArgb(
                        255,
                        (byte)Math.Min(255, fill.R + 26),
                        (byte)Math.Min(255, fill.G + 26),
                        (byte)Math.Min(255, fill.B + 26));
                }

                var accent = new SolidColorBrush(fill);
                accent.Freeze();
                _parts.Fill.Background = accent;
            }
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;

            if (AuroraState == SoftInteractionState.Pressed || IsPressed)
                return SoftInteractionState.Pressed;
            if (AuroraState == SoftInteractionState.Focused
                || IsKeyboardFocused)
                return SoftInteractionState.Focused;
            if (AuroraState == SoftInteractionState.Hover || IsMouseOver)
                return SoftInteractionState.Hover;

            return SoftInteractionState.Rest;
        }

        /// <summary>
        /// WCAG 2.2 SC 2.5.8. A translucent pill reads smaller than it is,
        /// so the 44px floor is enforced rather than assumed.
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);

            double floor = SoftUiAccessibility.MinHitTarget;
            if (size.Height < floor) size.Height = floor;
            if (size.Width < floor) size.Width = floor;

            return size;
        }
    }
}
