// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Swiss/SwissSurface.cs
// PROJECT: ProGlassAutomation
// MODULE: Swiss — the panel
//
// A Swiss card is NOT a box. The style's own guidance is unusually
// specific about this: a hairline top rule, a heading, real text, and a
// right-aligned data row sharing the same baseline.
//
// So this control is a rule and a ground, with the content between them.
// There is no box, no shadow, and no radius to hide behind — which
// means the STRUCTURE of the type is the only thing carrying hierarchy.
// If the type scale is wrong, there is nothing left to fall back on,
// and that is exactly the point: it is why the style produces the most
// accessible family in the engine. It cannot be made to look soft,
// because it has no mechanism for it.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Swiss
{
    /// <summary>A Swiss panel: a hairline, a ground, and typography.</summary>
    public class SwissSurface : ContentControl
    {
        /// <summary>Which depth tier. Present for vocabulary uniformity;
        /// Swiss renders every tier identically, because it has no depth
        /// axis. See SwissStack.</summary>
        public static readonly DependencyProperty SwissDepthProperty =
            DependencyProperty.Register(
                nameof(SwissDepth), typeof(SoftDepthTier),
                typeof(SwissSurface),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Resting, OnSwissChanged));

        public SoftDepthTier SwissDepth
        {
            get => (SoftDepthTier)GetValue(SwissDepthProperty);
            set => SetValue(SwissDepthProperty, value);
        }

        /// <summary>
        /// True for objects the user can act on. A non-interactive panel
        /// does not react to the pointer at all — a panel that lights up
        /// on hover is claiming to be a control, and in a style whose
        /// whole vocabulary is structure that is the expensive kind of
        /// lie.
        /// </summary>
        public static readonly DependencyProperty SwissInteractiveProperty =
            DependencyProperty.Register(
                nameof(SwissInteractive), typeof(bool),
                typeof(SwissSurface),
                new FrameworkPropertyMetadata(false, OnSwissChanged));

        public bool SwissInteractive
        {
            get => (bool)GetValue(SwissInteractiveProperty);
            set => SetValue(SwissInteractiveProperty, value);
        }

        /// <summary>Selected state, carried by a heavier rule and weight.</summary>
        public static readonly DependencyProperty SwissSelectedProperty =
            DependencyProperty.Register(
                nameof(SwissSelected), typeof(bool),
                typeof(SwissSurface),
                new FrameworkPropertyMetadata(false, OnSwissChanged));

        public bool SwissSelected
        {
            get => (bool)GetValue(SwissSelectedProperty);
            set => SetValue(SwissSelectedProperty, value);
        }

        /// <summary>Current interaction state, readable by tests.</summary>
        public static readonly DependencyProperty SwissStateProperty =
            DependencyProperty.Register(
                nameof(SwissState), typeof(SoftInteractionState),
                typeof(SwissSurface),
                new PropertyMetadata(
                    SoftInteractionState.Rest, OnSwissStateChanged));

        public SoftInteractionState SwissState
        {
            get => (SoftInteractionState)GetValue(SwissStateProperty);
            set => SetValue(SwissStateProperty, value);
        }

        private readonly SwissSurfaceParts _parts = new SwissSurfaceParts();
        private bool _bound;
        private bool _wired;

        public SwissSurface()
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

        private static void OnSwissChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SwissSurface s) s.Repaint();
        }

        private static void OnSwissStateChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SwissSurface s) s.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
        }

        /// <summary>Recomputes and applies the construction.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            SwissSurfaceParts.Paint(
                _parts, dark, SwissDepth, ResolveState(),
                SwissInteractive, SwissSelected,
                Paper(dark), Ink(dark));
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (!SwissInteractive) return SoftInteractionState.Rest;
            return SwissState;
        }

        private static Color Paper(bool dark) =>
            Token("WindowBackgroundBrush");

        private static Color Ink(bool dark) =>
            Token("PrimaryForegroundBrush");

        /// <summary>
        /// Reads a host brush for the FALLBACK path only.
        /// <para>
        /// When Swiss is the active family the recipe decides every
        /// colour and this is never consulted — the fallback exists so
        /// that a Swiss control placed inside a non-Swiss theme renders
        /// as a readable flat surface instead of throwing. The key is
        /// the same in both modes because the host dictionary follows the
        /// app theme; <paramref name="dark"/> is therefore deliberately
        /// unused rather than branching to a second key that may not
        /// exist.
        /// </para>
        /// </summary>
        internal static Color Token(string key) =>
            (Application.Current?.TryFindResource(key)
                as SolidColorBrush)?.Color ?? Colors.Black;
    }
}