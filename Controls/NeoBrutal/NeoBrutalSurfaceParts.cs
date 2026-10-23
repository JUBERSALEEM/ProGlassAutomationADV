// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/NeoBrutal/NeoBrutalSurfaceParts.cs
// PROJECT: ProGlassAutomation
// MODULE: Neo-Brutalism — construction applied to template parts
//
//   PART_NeoFill    the block: flat fill, thick border
//   PART_NeoShadow  the HARD OFFSET PLATE, behind it
//   PART_NeoFocus   the focus outline, OUTSIDE the border
//
// ── THE SHADOW IS AN OFFSET PLATE, NOT AN EFFECT ─────────────────────
//
// `box-shadow: 5px 5px 0 0 #000` is a translated copy of the element's
// own silhouette. So PART_NeoShadow is a plain Border behind the fill,
// carrying identical geometry, moved by a TranslateTransform.
//
// It is deliberately NOT a DropShadowEffect, for three reasons:
//
//   1. An effect goes through the GPU effect pass; an offset Border is
//      composited.
//   2. Every block here is a rectangle at radius 0, so a Border is an
//      exact silhouette. There is nothing an effect would render better.
//   3. BlurRadius is the property that makes an effect soft. The one
//      thing this family cannot have is softness, so the machinery built
//      to produce softness is the wrong tool for the job.
//
// ── THE FOCUS OUTLINE SITS OUTSIDE THE BORDER, ON PURPOSE ────────────
//
// "Focus states get eaten." A 4px black border means the default focus
// ring lands on top of an existing black outline and becomes invisible --
// the control looks unfocused while it is the only thing on screen with
// focus. So PART_NeoFocus carries a NEGATIVE margin derived from the
// border width plus NeoBrutalMetrics.FocusOffset, and draws in the
// accent, which is guaranteed to contrast with both the black border and
// the paper.
//
// ── NO ANIMATION IS EVER STARTED FROM HERE ───────────────────────────
//
// "The gesture is impact, and impact has no curve." Apply() sets the
// transforms outright. There is no Storyboard in this file, and that is
// the correct amount of code for a family whose press duration is zero.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.NeoBrutal;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.NeoBrutal
{
    /// <summary>The parts a Neo-Brutalist template must name.</summary>
    internal sealed class NeoBrutalSurfaceParts
    {
        /// <summary>The block itself.</summary>
        public Border? Fill { get; private set; }

        /// <summary>The hard offset plate behind it.</summary>
        public Border? Shadow { get; private set; }

        /// <summary>The focus outline, outside the border.</summary>
        public Border? Focus { get; private set; }

        public bool Complete => Fill is not null;

        public void Bind(Control root)
        {
            Fill = root.Template.FindName("PART_NeoFill", root) as Border;
            Shadow = root.Template.FindName("PART_NeoShadow", root) as Border;
            Focus = root.Template.FindName("PART_NeoFocus", root) as Border;
        }

        public void Apply(NeoBrutalStack stack)
        {
            var c = stack.Cell;

            if (Fill is not null)
            {
                var fill = new SolidColorBrush(c.Fill);
                fill.Freeze();
                Fill.Background = fill;

                var border = new SolidColorBrush(c.Border);
                border.Freeze();
                Fill.BorderBrush = border;

                Fill.BorderThickness = new Thickness(c.BorderWidth);

                // Explicit, not inherited. Border defaults to 0 so this
                // looks redundant, but radius 0 is RULE THREE of this
                // family and a value that is merely the absence of
                // configuration is not a value.
                Fill.CornerRadius = new CornerRadius(0);

                // Hover lift and press both travel here, as a translate.
                // Never margin, never padding: adjusting either makes the
                // whole board reflow and jump on every click.
                Fill.RenderTransform =
                    new TranslateTransform(c.Lift, c.Lift);
            }

            if (Shadow is not null)
            {
                if (c.ShadowVisible && c.ShadowOffset > 0)
                {
                    var shade = new SolidColorBrush(c.ShadowColour);
                    shade.Freeze();
                    Shadow.Background = shade;
                    Shadow.CornerRadius = new CornerRadius(0);

                    // Down and right, always. The light in this family is
                    // a fixed stamped overhead lamp, and the offset never
                    // changes direction by state -- only by magnitude.
                    Shadow.RenderTransform =
                        new TranslateTransform(c.ShadowOffset, c.ShadowOffset);
                    Shadow.Visibility = Visibility.Visible;
                }
                else
                {
                    // Disabled drops the plate entirely rather than
                    // fading it, so the block reads flat and inert.
                    Shadow.Visibility = Visibility.Collapsed;
                }
            }

            if (Focus is not null)
            {
                var m = MetricsFor();

                if (stack.Focused)
                {
                    var ring = new SolidColorBrush(c.Border);
                    ring.Freeze();
                    Focus.BorderBrush = ring;
                    Focus.BorderThickness = new Thickness(m.FocusWeight);

                    // Clear the block's own border, then sit further out
                    // again. Negative margin, because the outline is
                    // OUTSIDE the block by definition.
                    Focus.Margin = new Thickness(
                        -(c.BorderWidth + m.FocusOffset),
                        -(c.BorderWidth + m.FocusOffset),
                        -(c.BorderWidth + m.FocusOffset),
                        -(c.BorderWidth + m.FocusOffset));
                    Focus.CornerRadius = new CornerRadius(0);
                    Focus.Visibility = Visibility.Visible;
                }
                else
                {
                    Focus.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// A plain block for when no Neo-Brutalist profile is published,
        /// so a Neo-Brutalist control in another theme still renders as a
        /// readable surface rather than an invisible one.
        /// </summary>
        public void ApplyPlain(Color fill, Color ink)
        {
            if (Fill is not null)
            {
                var brush = new SolidColorBrush(fill);
                brush.Freeze();
                Fill.Background = brush;

                var border = new SolidColorBrush(ink);
                border.Freeze();
                Fill.BorderBrush = border;
                Fill.BorderThickness = new Thickness(2);
                Fill.CornerRadius = new CornerRadius(0);
            }

            if (Shadow is not null)
                Shadow.Visibility = Visibility.Collapsed;

            if (Focus is not null)
                Focus.Visibility = Visibility.Collapsed;
        }

        private static NeoBrutalMetrics MetricsFor() =>
            NeoBrutalPalette.Light?.Metrics
            ?? NeoBrutalPalette.Dark?.Metrics
            ?? new NeoBrutalMetrics();

        public static void Paint(
            NeoBrutalSurfaceParts parts,
            bool dark,
            NeoBrutalPlacement placement,
            SoftDepthTier tier,
            SoftInteractionState state,
            bool interactive,
            bool selected,
            Color fallbackFill,
            Color fallbackInk)
        {
            if (!parts.Complete) return;

            var profile = NeoBrutalPalette.For(dark);
            if (profile is null)
            {
                parts.ApplyPlain(fallbackFill, fallbackInk);
                return;
            }

            var recipe = new NeoBrutalRecipe(profile, dark);
            var stack = recipe.Resolve(
                placement, tier, state, interactive, selected);
            parts.Apply(stack);
        }
    }
}
