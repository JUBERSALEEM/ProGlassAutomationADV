// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Bento/BentoSurfaceParts.cs
// PROJECT: ProGlassAutomation
// MODULE: Bento Grid — construction applied to template parts
//
// THREE PARTS, AND THE ABSENCE OF A FOURTH IS THE POINT.
//
//   PART_BentoFill    the cell: rounded fill plus a real 1px edge.
//   PART_BentoFocus   the focus ring, drawn OUTSIDE the cell.
//   (content host)    whatever the cell contains.
//
// There is no shadow host. Not a host with no Effect attached -- no host
// at all, so a template author cannot attach one by accident and cannot
// be asked to remember not to. The other families each have a shadow
// budget to stay inside; this one has no shadow vocabulary, because in a
// bento the gutter does the separating and that is a layout property
// rather than a material one.
//
// ── WHY THE EDGE IS NOT A SEPARATE PART ───────────────────────────────
//
// Every other family gives the hairline its own host because the hairline
// is structural and separate from the fill -- a Swiss rule sits above a
// flat ground and is drawn independently of it. A bento edge is not like
// that: it is the cell's own outline, it shares the cell's radius, and
// it thickens for selection on the same element. Splitting it would mean
// keeping two widths in step for no expressive gain.
//
// ── THE EDGE IS ALWAYS DRAWN ──────────────────────────────────────────
//
// Even at rest, and this is a deliberate choice against the usual
// instinct. In a grid of nine similar tiles, the edge is what holds the
// composition together in the absence of a shadow. A tile whose outline
// only appeared on hover would make the resting grid read as a field
// with gaps rather than as a set of cells.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.Bento;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Bento
{
    /// <summary>
    /// The parts a Bento ControlTemplate must name. There is no fourth,
    /// and there will not be one.
    /// </summary>
    internal sealed class BentoSurfaceParts
    {
        /// <summary>The cell itself: fill, radius and edge.</summary>
        public Border? Fill { get; private set; }

        /// <summary>
        /// The focus ring. NOT optional and NOT the recipe's to omit.
        /// <para>
        /// A bento cell is large, and size is itself an affordance -- a
        /// big target reads as clickable before anything else does. That
        /// makes the ring more important here, not less: it is the only
        /// thing confirming the cell is genuinely focusable rather than
        /// merely big. It is drawn outside the cell and against the
        /// CANVAS, so its contrast never depends on how similar the tile
        /// happens to be to its own background.
        /// </para>
        /// </summary>
        public Border? FocusRing { get; private set; }

        /// <summary>True once every required part has been found.</summary>
        public bool Complete =>
            Fill is not null && FocusRing is not null;

        public void Bind(Control root)
        {
            Fill = root.Template.FindName("PART_BentoFill", root) as Border;
            FocusRing =
                root.Template.FindName("PART_BentoFocus", root) as Border;
        }

        /// <summary>Applies a resolved construction.</summary>
        public void Apply(BentoStack stack)
        {
            var m = stack.Cell;

            if (Fill is not null)
            {
                var brush = new SolidColorBrush(m.Fill);
                brush.Freeze();
                Fill.Background = brush;

                Fill.BorderBrush = new SolidColorBrush(m.Edge);
                Fill.BorderThickness = new Thickness(m.EdgeThickness);

                // One radius for every cell, always. A grid whose tiles
                // disagree about their corners stops reading as one
                // object, which is the entire job of the gutter.
                Fill.CornerRadius = new CornerRadius(
                    RadiusFor(stack.Tier));

                // The ring is hidden by default and shown by Repaint.
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
                    RadiusFor(stack.Tier) + SoftUiAccessibility.FocusRingOffset);

                FocusRing.Visibility = stack.FocusRing.HasValue
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                // The ring is a real signal, so it is suppressed entirely
                // when the user has asked for reduced motion rather than
                // merely made faster. The ring still APPEARS -- it is not
                // conditional on animation -- but it snaps rather than
                // fades.
                FocusRing.Opacity = 1.0;
            }
        }

        /// <summary>
        /// A plain surface for when no recipe is available, so a Bento
        /// control placed in a non-Bento theme still renders as a
        /// readable cell instead of an invisible one.
        /// </summary>
        public void ApplyPlain(Color fill, Color ink)
        {
            if (Fill is not null)
            {
                var brush = new SolidColorBrush(fill);
                brush.Freeze();
                Fill.Background = brush;

                var edgeBrush = new SolidColorBrush(ink);
                edgeBrush.Freeze();
                Fill.BorderBrush = edgeBrush;
                Fill.BorderThickness = new Thickness(1);
                Fill.CornerRadius = new CornerRadius(RadiusFor(SoftDepthTier.Resting));
            }

            if (FocusRing is not null)
                FocusRing.Visibility = Visibility.Collapsed;
        }

        private static double RadiusFor(SoftDepthTier tier)
        {
            var metrics = BentoPalette.Light?.Metrics
                ?? BentoPalette.Dark?.Metrics
                ?? new BentoMetrics();

            // One radius everywhere. The tier is accepted so the engine's
            // vocabulary is uniform, and deliberately declines to change
            // the result: a bento cell that grew corners at a higher tier
            // would stop matching the cells beside it, and matching is
            // the only thing keeping the grid coherent.
            _ = tier;
            return metrics.Radius;
        }

        /// <summary>
        /// Paints one surface, falling back to a plain construction when
        /// no Bento profile is published.
        /// </summary>
        public static void Paint(
            BentoSurfaceParts parts,
            bool dark,
            BentoSpan span,
            SoftDepthTier tier,
            SoftInteractionState state,
            bool interactive,
            bool selected,
            Color fallbackFill,
            Color fallbackInk)
        {
            if (!parts.Complete) return;

            var profile = BentoPalette.For(dark);
            if (profile is null)
            {
                parts.ApplyPlain(fallbackFill, fallbackInk);
                return;
            }

            var recipe = new BentoRecipe(profile, dark);
            var stack = recipe.Resolve(span, tier, state, interactive, selected);
            parts.Apply(stack);
        }
    }
}
