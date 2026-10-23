// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/LiquidGlass/LiquidButton.cs
// PROJECT: ProGlassAutomation
// MODULE: Liquid Glass — the button
//
// THE MOST IMPORTANT FILE IN THE FAMILY.
//
// A button is where the material is judged, because a button is the
// smallest thing a user looks at and the thing they interact with most.
// If the lens does not read at 40 pixels tall, nothing else in the
// family matters.
//
// Two decisions carry the design:
//
//  1. PRESS DOES NOT DARKEN. The intuitive treatment is wrong on a
//     lens. Darkening means less light entering the material, which
//     reads as the glass THICKENING rather than as being pushed. The
//     press signal here is the shadow collapsing and the refraction
//     relaxing — which is what a finger pressing a slab of glass
//     actually does to it. See LiquidGlassRecipe.
//
//  2. MORPH IS A REAL STATE. When the button expands into a menu, the
//     material gets THICKER: deeper shadow, stronger refraction, softer
//     scatter. LiquidMorph carries that, and the recipe scales it in
//     one place so a template author cannot get half of it right.
// ═══════════════════════════════════════════════════════════════════════

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.LiquidGlass
{
    /// <summary>A Liquid Glass button.</summary>
    public class LiquidButton : ButtonBase
    {
        /// <summary>
        /// The label. Kept as a dependency property rather than relying
        /// on Content so the template can place it inside the lens
        /// without the content inheriting the surface's own padding.
        /// </summary>
        public static readonly DependencyProperty LiquidLabelProperty =
            DependencyProperty.Register(
                nameof(LiquidLabel), typeof(object),
                typeof(LiquidButton),
                new FrameworkPropertyMetadata(null, OnLiquidChanged));

        public object? LiquidLabel
        {
            get => GetValue(LiquidLabelProperty);
            set => SetValue(LiquidLabelProperty, value);
        }

        /// <summary>Depth tier. A button is Raised by definition.</summary>
        public static readonly DependencyProperty LiquidDepthProperty =
            DependencyProperty.Register(
                nameof(LiquidDepth), typeof(SoftDepthTier),
                typeof(LiquidButton),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Raised, OnLiquidChanged));

        public SoftDepthTier LiquidDepth
        {
            get => (SoftDepthTier)GetValue(LiquidDepthProperty);
            set => SetValue(LiquidDepthProperty, value);
        }

        /// <summary>
        /// How far the control has morphed into a larger shape, 0..1.
        /// <para>
        /// Not decoration. A morphing control is simulating a thicker
        /// slab of glass, and the recipe scales shadow and refraction
        /// together from this one value. Exposing it as a property is
        /// what lets a template animate it and lets a test assert it
        /// without reaching into private state.
        /// </para>
        /// </summary>
        public static readonly DependencyProperty LiquidMorphProperty =
            DependencyProperty.Register(
                nameof(LiquidMorph), typeof(double),
                typeof(LiquidButton),
                new FrameworkPropertyMetadata(0.0, OnLiquidChanged));

        public double LiquidMorph
        {
            get => (double)GetValue(LiquidMorphProperty);
            set => SetValue(LiquidMorphProperty, value);
        }

        /// <summary>Current interaction state, readable by tests.</summary>
        public static readonly DependencyProperty LiquidStateProperty =
            DependencyProperty.Register(
                nameof(LiquidState), typeof(SoftInteractionState),
                typeof(LiquidButton),
                new PropertyMetadata(
                    SoftInteractionState.Rest, OnLiquidStateChanged));

        public SoftInteractionState LiquidState
        {
            get => (SoftInteractionState)GetValue(LiquidStateProperty);
            set => SetValue(LiquidStateProperty, value);
        }

        /// <summary>Honour the OS "reduce transparency" setting.</summary>
        public static readonly DependencyProperty LiquidReducedTransparencyProperty =
            DependencyProperty.Register(
                nameof(LiquidReducedTransparency), typeof(bool),
                typeof(LiquidButton),
                new FrameworkPropertyMetadata(false, OnLiquidChanged));

        public bool LiquidReducedTransparency
        {
            get => (bool)GetValue(LiquidReducedTransparencyProperty);
            set => SetValue(LiquidReducedTransparencyProperty, value);
        }

        private readonly LiquidSurfaceParts _parts = new LiquidSurfaceParts();
        private bool _bound;
        private bool _wired;

        public LiquidButton()
        {
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            // The family's own minimum hit target. Set here rather than
            // in the template so it holds for any template a caller
            // supplies — a template that forgets it still produces a
            // reachable control.
            MinHeight = SoftUiAccessibility.MinHitTarget;
            MinWidth = SoftUiAccessibility.MinHitTarget;
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
            if (d is LiquidButton b) b.Repaint();
        }

        private static void OnLiquidStateChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LiquidButton b) b.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());

            // IsPressed, IsKeyboardFocused and IsMouseOver raise NO
            // routed event, and ButtonBase exposes no virtual
            // OnIsPressedChanged / OnIsKeyboardFocusedChanged to
            // override -- both of those were CS0115. The repaint is
            // hooked to the properties themselves, which is what
            // ClayButton, NeuButton, SwissButton and HoloButton all
            // do. Keeping one approach across five families matters
            // more than it sounds: five different mechanisms for the
            // same notification is five places for the next person to
            // look when a button stops repainting.
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

        /// <summary>Recomputes and applies the construction.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            LiquidSurfaceParts.Paint(
                _parts, dark, LiquidDepth, ResolveState(),
                FallbackFill(), FallbackInk(),
                reducedTransparency: LiquidReducedTransparency,
                morph: LiquidMorph);
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (IsKeyboardFocused) return SoftInteractionState.Focused;
            if (IsPressed) return SoftInteractionState.Pressed;
            if (IsMouseOver) return SoftInteractionState.Hover;
            return SoftInteractionState.Rest;
        }

        private static Color FallbackFill() =>
            (Application.Current?.TryFindResource("ElevatedSurfaceBrush")
                as SolidColorBrush)?.Color ?? Colors.White;

        private static Color FallbackInk() =>
            (Application.Current?.TryFindResource("PrimaryForegroundBrush")
                as SolidColorBrush)?.Color ?? Colors.Black;
    }
}