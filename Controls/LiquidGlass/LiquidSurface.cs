// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/LiquidGlass/LiquidSurface.cs
// PROJECT: ProGlassAutomation
// MODULE: Liquid Glass — the panel
//
// A Liquid Glass panel is a lens lying on top of content. That single
// sentence decides almost everything about this control:
//
//   - it is TRANSLUCENT, so the panel must not hide what is behind it;
//   - it has a SPECULAR EDGE, so the gradient rim is mandatory;
//   - it FLOATS, so it carries a shadow and sits on the Resting or
//     Floating tier rather than the Well tier.
//
// The tier is exposed rather than hard-coded because the whole point of
// the material is depth: a panel and a floating sheet use the same
// optics but at very different depths.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.LiquidGlass
{
    /// <summary>A Liquid Glass panel: a floating lens over content.</summary>
    public class LiquidSurface : ContentControl
    {
        /// <summary>Which depth tier the lens floats at.</summary>
        public static readonly DependencyProperty LiquidDepthProperty =
            DependencyProperty.Register(
                nameof(LiquidDepth), typeof(SoftDepthTier),
                typeof(LiquidSurface),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Resting, OnLiquidChanged));

        public SoftDepthTier LiquidDepth
        {
            get => (SoftDepthTier)GetValue(LiquidDepthProperty);
            set => SetValue(LiquidDepthProperty, value);
        }

        /// <summary>
        /// True for objects the user can act on.
        /// <para>
        /// A non-interactive panel does not react to the pointer. This
        /// matters more here than in the opaque families: a glass panel
        /// that lights up on hover looks like a button even when it is
        /// not one, and on a translucent surface the hover state is far
        /// more conspicuous than it would be on a solid card.
        /// </para>
        /// </summary>
        public static readonly DependencyProperty LiquidInteractiveProperty =
            DependencyProperty.Register(
                nameof(LiquidInteractive), typeof(bool),
                typeof(LiquidSurface),
                new FrameworkPropertyMetadata(false, OnLiquidChanged));

        public bool LiquidInteractive
        {
            get => (bool)GetValue(LiquidInteractiveProperty);
            set => SetValue(LiquidInteractiveProperty, value);
        }

        /// <summary>Current interaction state, readable by tests.</summary>
        public static readonly DependencyProperty LiquidStateProperty =
            DependencyProperty.Register(
                nameof(LiquidState), typeof(SoftInteractionState),
                typeof(LiquidSurface),
                new PropertyMetadata(
                    SoftInteractionState.Rest, OnLiquidStateChanged));

        public SoftInteractionState LiquidState
        {
            get => (SoftInteractionState)GetValue(LiquidStateProperty);
            set => SetValue(LiquidStateProperty, value);
        }

        /// <summary>
        /// Honour the OS "reduce transparency" setting.
        /// <para>
        /// Exposed as a dependency property rather than read from the
        /// system directly so that the Theme Center can preview the
        /// accessible appearance, and so that tests can assert it. A
        /// setting nobody can see is a setting nobody verifies.
        /// </para>
        /// </summary>
        public static readonly DependencyProperty LiquidReducedTransparencyProperty =
            DependencyProperty.Register(
                nameof(LiquidReducedTransparency), typeof(bool),
                typeof(LiquidSurface),
                new FrameworkPropertyMetadata(false, OnLiquidChanged));

        public bool LiquidReducedTransparency
        {
            get => (bool)GetValue(LiquidReducedTransparencyProperty);
            set => SetValue(LiquidReducedTransparencyProperty, value);
        }

        private readonly LiquidSurfaceParts _parts = new LiquidSurfaceParts();
        private bool _bound;
        private bool _wired;

        public LiquidSurface()
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

        private static void OnLiquidChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LiquidSurface s) s.Repaint();
        }

        private static void OnLiquidStateChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LiquidSurface s) s.Repaint();
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
            LiquidSurfaceParts.Paint(
                _parts, dark, LiquidDepth, ResolveState(),
                FallbackFill(), FallbackInk(),
                reducedTransparency: LiquidReducedTransparency);
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (!LiquidInteractive) return SoftInteractionState.Rest;
            return LiquidState;
        }

        private static Color FallbackFill() =>
            Token("ElevatedSurfaceBrush");

        private static Color FallbackInk() =>
            Token("PrimaryForegroundBrush");

        /// <summary>
        /// Reads a host brush for the FALLBACK path only.
        /// <para>
        /// When Liquid Glass is the active family the recipe decides
        /// every colour and this is never consulted. It exists so a
        /// Liquid Glass control placed inside another family's theme
        /// renders as a readable card instead of throwing. The key is
        /// the same in both modes because the host dictionary already
        /// follows the app theme.
        /// </para>
        /// </summary>
        internal static Color Token(string key) =>
            (Application.Current?.TryFindResource(key)
                as SolidColorBrush)?.Color ?? Colors.Black;
    }
}