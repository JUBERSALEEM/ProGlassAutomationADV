// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Glassmorphism/GlassSurface.cs
// PROJECT: ProGlassAutomation
// MODULE: Glassmorphism — the pane
//
// A glass pane. The only decision this control makes that a caller
// cannot override is whether it is CONTENT or CHROME, and that decision
// is why the family is shippable at all.
//
// ── WHY GlassPlacement IS NOT DECORATION ──────────────────────────────
//
// GlassPalette's header quotes the research that forces this: 14 tested
// glassmorphic implementations, none of them consistently WCAG 2.2
// compliant across backdrops, one component measured at 5.2:1 over a
// dark wallpaper and 1.8:1 over a light one.
//
// The two usual escapes do not work. A dark scrim behind the text means
// "you're not making accessible glass". Raising opacity to 85-90% means
// "you've solved the contrast problem by not using glass".
//
// So GlassPlacement.CHROME is where the translucency lives — navigation,
// rails, status strips — and GlassPlacement.CONTENT is opaque, always,
// with no path through the recipe that produces a translucent content
// surface. This is what the implementations that ship the style actually
// do: "use glass for only a few key elements", and Apple "puts glass on
// chrome, not content".
//
// A caller CAN put a Content surface anywhere they like. What they
// cannot do is make a Content surface translucent. That asymmetry is
// deliberate.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Glassmorphism;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Glassmorphism
{
    /// <summary>A glass pane.</summary>
    public class GlassSurface : ContentControl
    {
        /// <summary>
        /// Content or Chrome. This is the decision the family turns on —
        /// see the file header.
        /// </summary>
        public static readonly DependencyProperty GlassPlacementProperty =
            DependencyProperty.Register(
                nameof(GlassPlacement), typeof(GlassPlacement),
                typeof(GlassSurface),
                new FrameworkPropertyMetadata(
                    GlassPlacement.Content, OnGlassChanged));

        /// <summary>
        /// Defaults to Content deliberately. The safe placement is the
        /// default, and reaching for the translucent one should be a
        /// choice somebody makes on purpose.
        /// </summary>
        public GlassPlacement GlassPlacement
        {
            get => (GlassPlacement)GetValue(GlassPlacementProperty);
            set => SetValue(GlassPlacementProperty, value);
        }

        public static readonly DependencyProperty GlassDepthProperty =
            DependencyProperty.Register(
                nameof(GlassDepth), typeof(SoftDepthTier),
                typeof(GlassSurface),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Resting, OnGlassChanged));

        /// <summary>
        /// Accepted for vocabulary uniformity and otherwise inert.
        /// <para>
        /// This family has a UNIFORM blur and no elevation ladder, so
        /// there is nothing for a tier to select. That is the 2020
        /// recipe's actual character, not an omission.
        /// </para>
        /// </summary>
        public SoftDepthTier GlassDepth
        {
            get => (SoftDepthTier)GetValue(GlassDepthProperty);
            set => SetValue(GlassDepthProperty, value);
        }

        public static readonly DependencyProperty GlassInteractiveProperty =
            DependencyProperty.Register(
                nameof(GlassInteractive), typeof(bool),
                typeof(GlassSurface),
                new FrameworkPropertyMetadata(false, OnGlassChanged));

        public bool GlassInteractive
        {
            get => (bool)GetValue(GlassInteractiveProperty);
            set => SetValue(GlassInteractiveProperty, value);
        }

        public static readonly DependencyProperty GlassSelectedProperty =
            DependencyProperty.Register(
                nameof(GlassSelected), typeof(bool),
                typeof(GlassSurface),
                new FrameworkPropertyMetadata(false, OnGlassChanged));

        public bool GlassSelected
        {
            get => (bool)GetValue(GlassSelectedProperty);
            set => SetValue(GlassSelectedProperty, value);
        }

        public static readonly DependencyProperty GlassStateProperty =
            DependencyProperty.Register(
                nameof(GlassState), typeof(SoftInteractionState),
                typeof(GlassSurface),
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

        public GlassSurface()
        {
            HorizontalContentAlignment = HorizontalAlignment.Left;
            VerticalContentAlignment = VerticalAlignment.Top;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
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
            if (d is GlassSurface g) g.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());

            // Two reasons this family subscribes to the palette's event,
            // and neither is about theme changes:
            //
            //  1. Windows high contrast can be toggled while the app is
            //     running, and the rim is then the only visible edge.
            //  2. Reduced transparency is a user preference, and NN/G
            //     treats the ability to set it as a requirement rather
            //     than a nicety.
            GlassPalette.Changed += (_, _) => Repaint();
        }

        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            GlassSurfaceParts.Paint(
                _parts, dark, GlassPlacement, GlassDepth,
                ResolveState(), GlassInteractive, GlassSelected,
                Fill(), Ink());
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (!GlassInteractive) return SoftInteractionState.Rest;
            return GlassState;
        }

        /// <summary>
        /// The accessible name, set on the PANE.
        /// <para>
        /// More important in this family than in the others: a
        /// translucent surface is announced by assistive technology the
        /// same way as any other, and a glass card showing a figure is
        /// just as meaningless without context as an opaque one.
        /// </para>
        /// </summary>
        public string AccessibleName
        {
            get => AutomationProperties.GetName(this);
            set => AutomationProperties.SetName(this, value);
        }

        internal static Color Fill() => Token("WindowBackgroundBrush");
        internal static Color Ink() => Token("PrimaryForegroundBrush");

        internal static Color Token(string key) =>
            (Application.Current?.TryFindResource(key)
                as SolidColorBrush)?.Color ?? Colors.White;
    }
}
