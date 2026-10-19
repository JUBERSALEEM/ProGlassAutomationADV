// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Swiss/SwissSurfaceParts.cs
// PROJECT: ProGlassAutomation
// MODULE: Swiss — construction applied to template parts
//
// TWO BORDERS, AND THAT IS THE WHOLE POINT.
//
//   PART_SwissRule   the hairline. Horizontal, structural.
//   PART_SwissFill   flat, unmodulated, radius 0.
//
// There is no shadow part, because there is no shadow. Not a part with
// no Effect attached — no part at all, so a template author cannot
// attach one by accident and cannot be asked to remember not to.
//
// The absence is enforced at three levels: the template declares no
// shadow host, the recipe has no field to put one in, and the
// SwissStack type exposes Shadows as a permanently empty list.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.Swiss;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Swiss
{
    /// <summary>Which edge of a Swiss surface carries the hairline.</summary>
    internal enum SwissRuleEdge
    {
        /// <summary>Across the top. The default, for panels.</summary>
        Top = 0,

        /// <summary>Across the bottom. Used by the field.</summary>
        Bottom = 1,
    }

    /// <summary>
    /// The three parts a Swiss ControlTemplate must name: a ground, a
    /// hairline, and a focus ring. There is no fourth, and there will
    /// not be one.
    /// </summary>
    internal sealed class SwissSurfaceParts
    {
        /// <summary>The hairline. Horizontal and structural.</summary>
        public Border? Rule { get; private set; }

        /// <summary>The flat fill. Never rounded, never shaded.</summary>
        public Border? Fill { get; private set; }

        /// <summary>
        /// Where the hairline goes. Edged above for a panel, below for a
        /// field, because an input is written on from the left and the
        /// line belongs under the hand.
        /// </summary>
        public SwissRuleEdge Edge { get; private set; }

        /// <summary>
        /// The focus ring. NOT optional and NOT the recipe — this style's
        /// controls are flat by design, which makes them exactly as hard
        /// to focus as they look, so the ring is the only thing keeping a
        /// keyboard user oriented.
        /// </summary>
        public Border? FocusRing { get; private set; }

        public bool Complete =>
            Rule is not null && Fill is not null && FocusRing is not null;

        public void Bind(FrameworkElement root)
        {
            // The field names its rule PART_SwissRuleEdged because it puts
            // the line on the BOTTOM edge. Resolving both names here is
            // what keeps that difference invisible to the three callers.
            Rule = root.Template.FindName("PART_SwissRule", root) as Border;
            if (Rule is not null)
            {
                Edge = SwissRuleEdge.Top;
            }
            else
            {
                Rule = root.Template.FindName(
                    "PART_SwissRuleEdged", root) as Border;
                Edge = SwissRuleEdge.Bottom;
            }

            Fill = root.Template.FindName("PART_SwissFill", root) as Border;
            FocusRing =
                root.Template.FindName("PART_SwissFocusRing", root) as Border;
        }

        /// <summary>Applies a resolved Swiss construction.</summary>
        public void Apply(SwissStack stack)
        {
            if (!Complete) return;

            var c = stack.Contrast;

            // Radius zero, taken from the recipe rather than hard-coded,
            // so the template and the recipe cannot disagree.
            Fill!.CornerRadius = new CornerRadius(c.Radius);
            Fill.Background = Solid(c.Fill);

            Rule!.BorderBrush = Solid(c.Rule);
            Rule.BorderThickness = Edge == SwissRuleEdge.Top
                ? new Thickness(0, c.RuleThickness, 0, 0)
                : new Thickness(0, 0, 0, c.RuleThickness);
            Rule.CornerRadius = new CornerRadius(0);

            // Inverted, the ring takes the ink colour — the one colour
            // guaranteed to read against whatever ground it lands on.
            // Inverting the ring with the ground would leave it
            // invisible, which is the exact opposite of its job.
            FocusRing!.BorderBrush = Solid(stack.Inverted ? c.Ink : c.Fill);
            FocusRing.BorderThickness = new Thickness(
                SoftUiAccessibility.FocusRingWidth);
            FocusRing.Visibility = stack.Focused
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Falls back to a plain flat surface when Swiss is not the active
        /// family. Still zero radius and still no shadow, because those
        /// are the family's structural commitments rather than its
        /// styling, and a fallback that quietly grew corners would be a
        /// regression nobody would report.
        /// </summary>
        public void ApplyPlain(Color paper, Color ink)
        {
            if (!Complete) return;
            Fill!.CornerRadius = new CornerRadius(0);
            Fill.Background = Solid(paper);
            Rule!.BorderBrush = Solid(ink);
            Rule.BorderThickness = Edge == SwissRuleEdge.Top
                ? new Thickness(0, 1, 0, 0)
                : new Thickness(0, 0, 0, 1);
            Rule.CornerRadius = new CornerRadius(0);
            FocusRing!.BorderBrush = Solid(ink);
            FocusRing.BorderThickness = new Thickness(
                SoftUiAccessibility.FocusRingWidth);
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
        /// single entry point every Swiss control uses.
        /// </summary>
        public static void Paint(
            SwissSurfaceParts parts,
            bool dark,
            SoftDepthTier depth,
            SoftInteractionState state,
            bool interactive,
            bool selected,
            Color fallbackPaper,
            Color fallbackInk)
        {
            if (parts is null || !parts.Complete) return;

            var profile = SwissPalette.For(dark);
            if (profile is null)
            {
                parts.ApplyPlain(fallbackPaper, fallbackInk);
                return;
            }

            try
            {
                var stack = new SwissRecipe(profile, dark)
                    .Resolve(depth, state, interactive, selected);
                parts.Apply(stack);
            }
            catch (Exception)
            {
                parts.ApplyPlain(fallbackPaper, fallbackInk);
            }
        }
    }
}