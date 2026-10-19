// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Glassmorphism/GlassButton.cs
// PROJECT: ProGlassAutomation
// MODULE: Glassmorphism — the interactive pane
//
// A button is CHROME by default here, and that default is worth
// explaining because it is the opposite of GlassSurface's.
//
// GlassSurface defaults to Content, because a panel is usually a place
// text lives and the safe placement should win by default. A BUTTON is
// not a place text lives — it is a target, and the research's own
// per-element table puts buttons at 0.10-0.20 alpha, in the band where
// the glass is visible. So the button starts in Chrome and a caller who
// wants the opaque treatment has to ask for it.
//
// That asymmetry is the family's design working as intended: the
// translucency is spent on the things the eye lands on, and text
// surfaces stay readable.
//
// ── PRESS DEEPENS ─────────────────────────────────────────────────────
//
// Matched to Spatial's "press recedes" rather than to the opaque
// families' "press toward the ink". In the two translucent families a
// press deepens the frost — less of the backdrop, more of the surface —
// which is the same gesture expressed in the only channel a translucent
// control actually has. Nothing moves: a glass pane that slid on press
// would slide the content behind it, and that content is not ours.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Glassmorphism;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Glassmorphism
{
    /// <summary>A glass pane that can be activated.</summary>
    public class GlassButton : ButtonBase
    {
        public static readonly DependencyProperty GlassPlacementProperty =
            DependencyProperty.Register(
                nameof(GlassPlacement), typeof(GlassPlacement),
                typeof(GlassButton),
                new FrameworkPropertyMetadata(
                    GlassPlacement.Chrome, OnGlassChanged));

        public GlassPlacement GlassPlacement
        {
            get => (GlassPlacement)GetValue(GlassPlacementProperty);
            set => SetValue(GlassPlacementProperty, value);
        }

        public static readonly DependencyProperty GlassDepthProperty =
            DependencyProperty.Register(
                nameof(GlassDepth), typeof(SoftDepthTier),
                typeof(GlassButton),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Raised, OnGlassChanged));

        /// <summary>Inert here. The family has a uniform blur and no
        /// elevation ladder for a tier to select.</summary>
        public SoftDepthTier GlassDepth
        {
            get => (SoftDepthTier)GetValue(GlassDepthProperty);
            set => SetValue(GlassDepthProperty, value);
        }

        public static readonly DependencyProperty GlassSelectedProperty =
            DependencyProperty.Register(
                nameof(GlassSelected), typeof(bool),
                typeof(GlassButton),
                new FrameworkPropertyMetadata(false, OnGlassChanged));

        public bool GlassSelected
        {
            get => (bool)GetValue(GlassSelectedProperty);
            set => SetValue(GlassSelectedProperty, value);
        }

        public static readonly DependencyProperty GlassStateProperty =
            DependencyProperty.Register(
                nameof(GlassState), typeof(SoftInteractionState),
                typeof(GlassButton),
                new PropertyMetadata(
                    SoftInteractionState.Rest, OnGlassChanged));

        public SoftInteractionState GlassState
        {
            get => (SoftInteractionState)GetValue(GlassStateProperty);
            set => SetValue(GlassStateProperty, value);
        }

        private readonly GlassSurfaceParts _parts = new GlassSurfaceParts();
        private bool _bound;
        private bool _wired;

        public GlassButton()
        {
            HorizontalContentAlignment = HorizontalAlignment.Left;
            VerticalContentAlignment = VerticalAlignment.Center;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            FocusVisualStyle = null;
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

        private static void OnGlassChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GlassButton b) b.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
            GlassPalette.Changed += (_, _) => Repaint();

            DependencyPropertyDescriptor.FromProperty(
                IsPressedProperty, typeof(ButtonBase))
                .AddValueChanged(this, (_, _) => Repaint());

            DependencyPropertyDescriptor.FromProperty(
                IsKeyboardFocusedProperty, typeof(KeyboardNavigation))
                .AddValueChanged(this, (_, _) => Repaint());

            DependencyPropertyDescriptor.FromProperty(
                IsMouseOverProperty, typeof(ButtonBase))
                .AddValueChanged(this, (_, _) => Repaint());
        }

        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            GlassSurfaceParts.Paint(
                _parts, dark, GlassPlacement, GlassDepth,
                ResolveState(), interactive: true,
                selected: GlassSelected,
                GlassSurface.Fill(), GlassSurface.Ink());
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;

            if (GlassState == SoftInteractionState.Pressed || IsPressed)
                return SoftInteractionState.Pressed;
            if (GlassState == SoftInteractionState.Focused || IsKeyboardFocused)
                return SoftInteractionState.Focused;
            if (GlassState == SoftInteractionState.Hover || IsMouseOver)
                return SoftInteractionState.Hover;

            return SoftInteractionState.Rest;
        }

        /// <summary>WCAG 2.2 SC 2.5.8. Frosted surfaces read smaller than
        /// they are, so the target is enforced rather than assumed.</summary>
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
