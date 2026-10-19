// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Glassmorphism/GlassSurfaceParts.cs
// PROJECT: ProGlassAutomation
// MODULE: Glassmorphism — construction applied to template parts
//
// TWO PARTS, AND THE RIM IS NOT ONE OF THE OPTIONAL ONES.
//
//   PART_GlassFill     the frost: a rounded translucent fill
//   PART_GlassFocus    the focus ring
//
// ── NO SHADOW HOST, AND THAT IS A DIFFERENCE FROM EVERY FAMILY BUT SWISS ─
//
// Spatial has two (the Fluent key/ambient pair). Clay and neumorphism
// have one. Glassmorphism has none, and the reason is not asceticism:
// the 2020 recipe has no elevation ladder to encode. Depth here comes
// from the material being frosted and lit from behind, not from an
// object sitting at a height — that is Spatial's job. The rim is what
// keeps the pane from reading as a smudge, which is the whole job a
// shadow would otherwise be doing.
//
// ── THE RIM IS THE POINT ──────────────────────────────────────────────
//
// "The 1px light border is an accessibility feature, not decoration:
//  Windows forced-colors mode strips transparency and leaves that
//  border as the only visible edge."
//
// This is a WPF app, so that mode is SystemParameters.HighContrast and
// it is not a hypothetical — one property read away, and users toggle
// it for real reasons. When it is on, the frost is gone and the rim is
// the only thing describing the panel. So it is always drawn, at full
// strength, in every state.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.Glassmorphism;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Glassmorphism
{
    /// <summary>The parts a Glassmorphism ControlTemplate must name.</summary>
    internal sealed class GlassSurfaceParts
    {
        /// <summary>The frost.</summary>
        public Border? Fill { get; private set; }

        /// <summary>The focus ring.</summary>
        public Border? FocusRing { get; private set; }

        public bool Complete => Fill is not null && FocusRing is not null;

        public void Bind(Control root)
        {
            Fill = root.Template.FindName("PART_GlassFill", root) as Border;
            FocusRing =
                root.Template.FindName("PART_GlassFocus", root) as Border;
        }

        /// <summary>Applies a resolved construction.</summary>
        public void Apply(GlassStack stack)
        {
            var m = stack.Cell;

            if (Fill is not null)
            {
                var brush = new SolidColorBrush(m.Fill);
                brush.Freeze();
                Fill.Background = brush;

                var rimBrush = new SolidColorBrush(m.Rim);
                rimBrush.Freeze();
                Fill.BorderBrush = rimBrush;

                // Never zero. Even when selected thickness is being
                // applied, the floor is GlassMetrics.RimThickness --
                // a selected glass pane with no edge is a pane you
                // cannot find.
                Fill.BorderThickness = new Thickness(
                    Math.Max(1, m.RimThickness));
                Fill.CornerRadius = new CornerRadius(RadiusFor());

                if (FocusRing is not null)
                    FocusRing.Visibility = Visibility.Collapsed;
            }

            if (FocusRing is not null)
            {
                var ringBrush = new SolidColorBrush(
                    stack.FocusRing ?? Colors.Transparent);
                ringBrush.Freeze();

                FocusRing.BorderBrush = ringBrush;
                FocusRing.BorderThickness =
                    new Thickness(SoftUiAccessibility.FocusRingWidth);
                FocusRing.CornerRadius = new CornerRadius(
                    RadiusFor() + SoftUiAccessibility.FocusRingOffset);
                FocusRing.Visibility = stack.FocusRing.HasValue
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        /// <summary>A plain surface for when no Glass profile is
        /// published, so a Glass control in a non-Glass theme renders as
        /// a readable pane rather than an invisible one.</summary>
        public void ApplyPlain(Color fill, Color ink)
        {
            if (Fill is not null)
            {
                var brush = new SolidColorBrush(fill);
                brush.Freeze();
                Fill.Background = brush;

                var rim = new SolidColorBrush(ink);
                rim.Freeze();
                Fill.BorderBrush = rim;
                Fill.BorderThickness = new Thickness(1);
                Fill.CornerRadius = new CornerRadius(RadiusFor());
            }

            if (FocusRing is not null)
                FocusRing.Visibility = Visibility.Collapsed;
        }

        private static double RadiusFor()
        {
            var metrics = GlassPalette.Light?.Metrics
                ?? GlassPalette.Dark?.Metrics
                ?? new GlassMetrics();
            return metrics.Radius;
        }

        public static void Paint(
            GlassSurfaceParts parts,
            bool dark,
            GlassPlacement placement,
            SoftDepthTier tier,
            SoftInteractionState state,
            bool interactive,
            bool selected,
            Color fallbackFill,
            Color fallbackInk)
        {
            if (!parts.Complete) return;

            var profile = GlassPalette.For(dark);
            if (profile is null)
            {
                parts.ApplyPlain(fallbackFill, fallbackInk);
                return;
            }

            var recipe = new GlassRecipe(profile, dark);
            var stack = recipe.Resolve(
                placement, tier, state, interactive, selected);
            parts.Apply(stack);
        }
    }
}
