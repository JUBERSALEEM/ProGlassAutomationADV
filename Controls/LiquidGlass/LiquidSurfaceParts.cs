// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/LiquidGlass/LiquidSurfaceParts.cs
// PROJECT: ProGlassAutomation
// MODULE: Liquid Glass — construction applied to template parts
//
// FOUR PARTS, AND THE RIM IS NOT ONE OF THEM BY ACCIDENT.
//
//   PART_LiquidFill     the tint the lens is carrying
//   PART_LiquidRim      the specular edge   <-- the family's signature
//   PART_LiquidSheen    the inner glow
//   PART_LiquidShadow   the shadow host
//   PART_LiquidFocus    the focus ring
//
// Every other family in this engine can get by with a fill and maybe a
// rule. Glass cannot. A flat translucent rectangle with a uniform 1px
// border is the exact shape that makes people say "this is just
// glassmorphism" — and they are right. What sells the material is the
// GRADIENT along the edge: bright where the light lands, falling away
// across the diagonal.
//
// So the rim is a separate part with a gradient brush, and RimFlat is
// never used except on the fallback path where the family is inactive.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ProGlassAutomation.Services.Theme.LiquidGlass;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.LiquidGlass
{
    /// <summary>
    /// The parts a Liquid Glass ControlTemplate must name.
    /// </summary>
    internal sealed class LiquidSurfaceParts
    {
        /// <summary>The tint the lens carries.</summary>
        public Border? Fill { get; private set; }

        /// <summary>The specular edge. Gradient, never flat.</summary>
        public Border? Rim { get; private set; }

        /// <summary>The inner glow. May legitimately be absent in a
        /// template that does not want it — this one is optional.</summary>
        public Border? Sheen { get; private set; }

        /// <summary>The shadow host. Optional: a Well has no outer
        /// shadow and should not be given one.</summary>
        public Border? ShadowHost { get; private set; }

        /// <summary>
        /// The focus ring. NOT optional. A glass surface has no tonal
        /// change to fall back on — pressing it moves the shadow, not
        /// the fill — so the ring is the only unambiguous signal that
        /// a keyboard user has arrived somewhere.
        /// </summary>
        public Border? FocusRing { get; private set; }

        /// <summary>
        /// Required parts. Sheen and ShadowHost are deliberately
        /// excluded: a template with no sheen host renders a perfectly
        /// good glass surface, and a template with no shadow host is
        /// correct for a Well.
        /// </summary>
        public bool Complete =>
            Fill is not null && Rim is not null && FocusRing is not null;

        /// <summary>
        /// Resolves the parts from an applied template.
        /// <para>
        /// CONTROL, not FrameworkElement: only Control carries a
        /// Template, so every root.Template call needs this type.
        /// </para>
        /// </summary>
        public void Bind(Control root)
        {
            Fill = root.Template.FindName("PART_LiquidFill", root) as Border;
            Rim = root.Template.FindName("PART_LiquidRim", root) as Border;
            Sheen = root.Template.FindName("PART_LiquidSheen", root) as Border;
            ShadowHost =
                root.Template.FindName("PART_LiquidShadow", root) as Border;
            FocusRing =
                root.Template.FindName("PART_LiquidFocus", root) as Border;
        }

        /// <summary>Applies a resolved Liquid Glass construction.</summary>
        public void Apply(
            LiquidGlassStack stack, LiquidGlassProfile profile, bool dark)
        {
            if (!Complete) return;

            var metrics = profile.Metrics;
            var radius = new CornerRadius(metrics.Radius);

            Fill!.CornerRadius = radius;
            Fill.Background = Solid(stack.Fill);

            // The rim. Gradient in, flat only when the family is off.
            Rim!.CornerRadius = radius;
            Rim.BorderThickness = new Thickness(metrics.RimThickness);
            Rim.BorderBrush = LiquidGlassRecipe.RimGradient(profile, dark);
            Rim.Opacity = Math.Clamp(stack.RimOpacity, 0, 1);

            // The sheen is OPTIONAL and the recipe may return null for
            // it. Collapsed rather than transparent: an all-zero-alpha
            // brush still costs a render pass every frame.
            if (Sheen is not null)
            {
                var glow = LiquidGlassRecipe.InnerGlow(stack);
                Sheen.CornerRadius = radius;
                Sheen.Visibility = glow is null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                if (glow is not null) Sheen.Background = glow;
            }

            // A Well is concave: depth comes from moving inward and it
            // has no outer shadow. Giving it one would read as a floating
            // depression, which is the opposite of a recess.
            if (ShadowHost is not null)
            {
                if (stack.ShadowDepth <= 0)
                {
                    ShadowHost.Effect = null;
                }
                else
                {
                    ShadowHost.Effect = new DropShadowEffect
                    {
                        // SoftLight fixes the direction for the whole
                        // engine: the shade falls away from the light.
                        Direction = SoftLight.ShadowDirection,
                        BlurRadius = stack.Blur * 0.75,
                        ShadowDepth = stack.ShadowDepth,
                        Opacity = Math.Clamp(stack.ShadowOpacity, 0, 1),
                        Color = Colors.Black,
                    };
                }
            }

            // The ring takes the profile's rim colour and NOT the ink:
            // on a lens the ink is low-contrast by construction, since
            // the whole point is that content shows through.
            FocusRing!.CornerRadius = radius;
            // NOT Solid(...). FocusRingPen already returns a Pen, and a
            // Pen's Brush is already a brush. Wrapping it in Solid(Color)
            // asked for a Color and got a Brush, which is CS1503. The
            // brush from BuildEdge is already frozen, so there was
            // nothing left to freeze here anyway.
            FocusRing.BorderBrush =
                LiquidGlassRecipe.FocusRingPen(profile).Brush;
            FocusRing.BorderThickness =
                new Thickness(SoftUiAccessibility.FocusRingWidth);
            FocusRing.Visibility =
                stack.State == SoftInteractionState.Focused
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        /// <summary>
        /// The fallback when Liquid Glass is not the active family.
        /// <para>
        /// Still rounded and still rimmed. A control that loses its
        /// family should degrade to a plausible card, not to an
        /// invisible rectangle — the user changed nothing about the
        /// layout and should not lose the surface entirely.
        /// </para>
        /// </summary>
        public void ApplyPlain(Color fallbackFill, Color fallbackInk)
        {
            if (!Complete) return;
            var radius = new CornerRadius(16);

            Fill!.CornerRadius = radius;
            Fill.Background = Solid(fallbackFill);

            // FLAT rim here on purpose: the gradient is the signature of
            // the active family, and borrowing it into a fallback would
            // imply the material is still Liquid Glass.
            Rim!.CornerRadius = radius;
            Rim.BorderThickness = new Thickness(1);
            Rim.BorderBrush = SoftUiAccessibility.BuildEdge(
                fallbackFill, dark: false).Brush;
            Rim.Opacity = 1.0;

            if (Sheen is not null) Sheen.Visibility = Visibility.Collapsed;
            if (ShadowHost is not null) ShadowHost.Effect = null;

            FocusRing!.CornerRadius = radius;
            FocusRing.BorderBrush = Solid(fallbackInk);
            FocusRing.BorderThickness =
                new Thickness(SoftUiAccessibility.FocusRingWidth);
            FocusRing.Visibility = Visibility.Collapsed;
        }

        private static SolidColorBrush Solid(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>
        /// Resolves the palette and applies the whole construction — the
        /// single entry point every Liquid Glass control uses.
        /// </summary>
        public static void Paint(
            LiquidSurfaceParts parts,
            bool dark,
            SoftDepthTier depth,
            SoftInteractionState state,
            Color fallbackFill,
            Color fallbackInk,
            bool reducedTransparency = false,
            double morph = 0.0)
        {
            if (parts is null || !parts.Complete) return;

            var profile = LiquidGlassPalette.For(dark);
            if (profile is null)
            {
                parts.ApplyPlain(fallbackFill, fallbackInk);
                return;
            }

            try
            {
                var stack = LiquidGlassRecipe.Resolve(
                    profile, depth, state, reducedTransparency, morph: morph);
                if (stack is null)
                {
                    parts.ApplyPlain(fallbackFill, fallbackInk);
                    return;
                }
                parts.Apply(stack, profile, dark);
            }
            catch (Exception)
            {
                // A theme must never take the window down with it. The
                // fallback surface is plain but the control is still
                // there and still usable.
                parts.ApplyPlain(fallbackFill, fallbackInk);
            }
        }
    }
}