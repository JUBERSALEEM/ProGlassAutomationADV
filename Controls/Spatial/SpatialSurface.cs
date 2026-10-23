// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Spatial/SpatialSurface.cs
// PROJECT: ProGlassAutomation
// MODULE: Spatial / Immersive — the plane
//
// A Spatial surface is a translucent panel sitting on a known plane of a
// known Z hierarchy. Two things follow from that, and both are unusual
// for this engine:
//
//   1. IT REVEALS WHATEVER IS BEHIND IT. Which means its apparent
//      colour depends on its backdrop, and a control cannot know its
//      backdrop. SpatialPalette resolves this by validating every colour
//      against the COMPOSITED result rather than against the fill's own
//      alpha — see the note on SpatialPalette.Composited.
//
//   2. IT IS TRANSLUCENT BY DEFAULT AND OPAQUE ON REQUEST. Transparency
//      is not a fallback for when something has gone wrong; it is a
//      supported operating mode of the family that the user can turn
//      off, and Windows itself turns acrylic off under battery saver
//      and on constrained hardware.
//
// ── WHY THE PLANE IS AN ATTACHED-STYLE CONCEPT AND NOT JUST A TIER ────
//
// Five planes, four tiers. SpatialSurface takes an explicit plane so a
// caller that genuinely knows its Z can say so -- the environment layer
// maps to no tier at all -- and falls back to the tier otherwise.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme.Spatial;

namespace ProGlassAutomation.Controls.Spatial
{
    /// <summary>A translucent panel on a known plane.</summary>
    public class SpatialSurface : ContentControl
    {
        /// <summary>
        /// Which plane of the Z hierarchy this surface sits on. Null means
        /// "derive it from <see cref="SpatialDepth"/>".
        /// </summary>
        public static readonly DependencyProperty SpatialPlaneProperty =
            DependencyProperty.Register(
                nameof(SpatialPlane), typeof(SpatialPlane),
                typeof(SpatialSurface),
                new FrameworkPropertyMetadata(null, OnSpatialChanged));

        /// <summary>Null when the plane is derived from the tier.</summary>
        public SpatialPlane? SpatialPlane
        {
            get => (SpatialPlane?)GetValue(SpatialPlaneProperty);
            set => SetValue(SpatialPlaneProperty, value);
        }

        /// <summary>
        /// The depth tier, which selects the plane when no explicit plane
        /// is given. The tier decides nothing else.
        /// </summary>
        public static readonly DependencyProperty SpatialDepthProperty =
            DependencyProperty.Register(
                nameof(SpatialDepth), typeof(SoftDepthTier),
                typeof(SpatialSurface),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Resting, OnSpatialChanged));

        public SoftDepthTier SpatialDepth
        {
            get => (SoftDepthTier)GetValue(SpatialDepthProperty);
            set => SetValue(SpatialDepthProperty, value);
        }

        /// <summary>
        /// True for surfaces the user can act on. A non-interactive
        /// surface does not react to the pointer at all -- and here that
        /// matters more than usual, because the hover effect is a
        /// change in OPACITY, which is the same channel the translucency
        /// uses. A non-interactive panel that lit up on hover would be
        /// indistinguishable from one that can be clicked.
        /// </summary>
        public static readonly DependencyProperty SpatialInteractiveProperty =
            DependencyProperty.Register(
                nameof(SpatialInteractive), typeof(bool),
                typeof(SpatialSurface),
                new FrameworkPropertyMetadata(false, OnSpatialChanged));

        public bool SpatialInteractive
        {
            get => (bool)GetValue(SpatialInteractiveProperty);
            set => SetValue(SpatialInteractiveProperty, value);
        }

        public static readonly DependencyProperty SpatialSelectedProperty =
            DependencyProperty.Register(
                nameof(SpatialSelected), typeof(bool),
                typeof(SpatialSurface),
                new FrameworkPropertyMetadata(false, OnSpatialChanged));

        public bool SpatialSelected
        {
            get => (bool)GetValue(SpatialSelectedProperty);
            set => SetValue(SpatialSelectedProperty, value);
        }

        public static readonly DependencyProperty SpatialStateProperty =
            DependencyProperty.Register(
                nameof(SpatialState), typeof(SoftInteractionState),
                typeof(SpatialSurface),
                new PropertyMetadata(
                    SoftInteractionState.Rest, OnSpatialChanged));

        public SoftInteractionState SpatialState
        {
            get => (SoftInteractionState)GetValue(SpatialStateProperty);
            set => SetValue(SpatialStateProperty, value);
        }

        private readonly SpatialSurfaceParts _parts = new SpatialSurfaceParts();
        private bool _bound;
        private bool _wired;

        public SpatialSurface()
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

        private static void OnSpatialChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SpatialSurface s) s.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());

            // Transparency can be toggled at runtime -- it is a supported
            // mode, not just a startup decision -- so the controls have
            // to follow it while they are open. This is why Spatial
            // palette publishes a Changed event at all; the other
            // families repaint on theme change only.
            SpatialPalette.Changed += (_, _) => Repaint();
        }

        /// <summary>Recomputes and applies the construction.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            SpatialSurfaceParts.Paint(
                _parts, dark, SpatialPlane, SpatialDepth,
                ResolveState(), SpatialInteractive, SpatialSelected,
                Fill(), Ink());
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (!SpatialInteractive) return SoftInteractionState.Rest;
            return SpatialState;
        }

        /// <summary>
        /// The accessible name for this surface.
        /// <para>
        /// Set on the surface, not on the content inside it. A panel
        /// showing a figure means nothing to a screen reader without
        /// context, and a label on the inner TextBlock is announced as
        /// part of the text while the panel itself still reads as an
        /// unlabelled group.
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
