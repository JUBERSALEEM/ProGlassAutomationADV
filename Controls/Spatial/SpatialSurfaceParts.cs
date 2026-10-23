// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Spatial/SpatialSurfaceParts.cs
// PROJECT: ProGlassAutomation
// MODULE: Spatial / Immersive — construction applied to template parts
//
// FOUR PARTS, AND THE FIRST TWO ARE THE SAME SHADOW TWICE.
//
//   PART_SpatialAmbient   wide, soft, low opacity   (the far one)
//   PART_SpatialKey       sharp, directional       (the near one)
//   PART_SpatialFill      the translucent fill plus its 1px rim
//   PART_SpatialFocus     the focus ring
//
// Fluent builds elevation from a PAIR of shadows, not one:
//
//   "Shadows are generated using a combination of sharp, directional
//    key shadows for nearby objects and soft, diffused ambient shadows
//    for distant ones — for instance, a low-elevation shadow might use
//    2px blur, while high-elevation ones reach up to 64px."
//
// WPF allows exactly one Effect per element, so the pair has to be two
// hosts, nested. That is the whole reason this family has two shadow
// parts where Swiss and Bento have none and neumorphism has two nested
// borders for a different reason again (composite fill, not two shadows).
//
// The nesting order is not arbitrary. AMBIENT is outermost so its very
// wide blur lands behind everything, KEY is inside it so the tight
// contact cue draws over the soft one, and FILL is innermost so neither
// shadow tints the translucent surface the user is reading through.
//
// ── WHY THERE IS NO BACKDROP HERE ──────────────────────────────────────
//
// This is the honest limitation of the family in WPF, and the templates
// state it rather than hiding it. Real Mica/Acrylic needs DWM or
// WinAppSDK; neither is reachable from a control template. WPF's
// BlurEffect blurs the ELEMENT'S OWN CONTENT, not the backdrop behind
// it, so it cannot stand in for one.
//
// So the translucency here is genuine alpha over whatever the window
// already has, and the DEPTH is carried by the shadow ladder plus the
// per-plane opacity. That is a simulation of depth, not acrylic, and
// Services/Theme/Spatial/README.md §3 records why the DWM P/Invoke is
// deliberately not shipped here.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme.Spatial;

namespace ProGlassAutomation.Controls.Spatial
{
    /// <summary>
    /// The parts a Spatial ControlTemplate must name. Two shadow hosts,
    /// one fill, one ring.
    /// </summary>
    internal sealed class SpatialSurfaceParts
    {
        /// <summary>Outermost: the soft, wide, low-opacity shadow.</summary>
        public Border? Ambient { get; private set; }

        /// <summary>Inside it: the sharp, directional contact shadow.</summary>
        public Border? Key { get; private set; }

        /// <summary>Innermost: the translucent fill and its rim.</summary>
        public Border? Fill { get; private set; }

        /// <summary>The focus ring, outside all three.</summary>
        public Border? FocusRing { get; private set; }

        public bool Complete =>
            Ambient is not null && Key is not null
            && Fill is not null && FocusRing is not null;

        public void Bind(Control root)
        {
            Ambient = root.Template.FindName(
                "PART_SpatialAmbient", root) as Border;
            Key = root.Template.FindName(
                "PART_SpatialKey", root) as Border;
            Fill = root.Template.FindName(
                "PART_SpatialFill", root) as Border;
            FocusRing = root.Template.FindName(
                "PART_SpatialFocus", root) as Border;
        }

        /// <summary>Applies a resolved construction.</summary>
        public void Apply(SpatialStack stack)
        {
            var m = stack.Cell;

            if (Fill is not null)
            {
                var fillBrush = new SolidColorBrush(m.Fill);
                fillBrush.Freeze();
                Fill.Background = fillBrush;

                var rimBrush = new SolidColorBrush(m.Rim);
                rimBrush.Freeze();
                Fill.BorderBrush = rimBrush;

                // A rim of zero thickness on the environment plane. The
                // room has no edge; describing one would draw a border
                // around the entire app.
                Fill.BorderThickness = m.RimThickness > 0
                    ? new Thickness(m.RimThickness)
                    : new Thickness(0);

                Fill.CornerRadius = new CornerRadius(RadiusFor());
            }

            ApplyShadow(Ambient, stack.Ambient);
            ApplyShadow(Key, stack.Key);

            if (FocusRing is not null)
            {
                var ring = new SolidColorBrush(
                    stack.FocusRing ?? Colors.Transparent);
                ring.Freeze();

                FocusRing.BorderBrush = ring;
                FocusRing.BorderThickness =
                    new Thickness(SoftUiAccessibility.FocusRingWidth);
                FocusRing.CornerRadius =
                    new CornerRadius(
                        RadiusFor() + SoftUiAccessibility.FocusRingOffset);
                FocusRing.Visibility = stack.FocusRing.HasValue
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private static void ApplyShadow(Border? host, SpatialShadow? shadow)
        {
            if (host is null) return;

            // A null shadow means this plane casts nothing -- only the
            // environment does not. The Effect is CLEARED rather than
            // left on, because a stale shadow on a repainted plane is
            // exactly the "switching themes leaves the old material
            // behind" failure this engine keeps guarding against.
            if (shadow is null)
            {
                host.Effect = null;
                return;
            }

            var effect = new DropShadowEffect
            {
                BlurRadius = shadow.Blur,
                // Offsets are y-only and positive: a shadow falls.
                Direction = 90,
                ShadowDepth = shadow.OffsetY,
                Opacity = shadow.Opacity,
                Color = shadow.Color,
                RenderingBias = RenderingBias.Quality,
            };
            effect.Freeze();

            host.Effect = effect;
        }

        /// <summary>A plain surface for when no Spatial profile is
        /// published, so a Spatial control dropped into a non-Spatial
        /// theme renders as a readable plane rather than an invisible
        /// one.</summary>
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

            ApplyShadow(Ambient, null);
            ApplyShadow(Key, null);

            if (FocusRing is not null)
                FocusRing.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// One radius for every plane.
        /// <para>
        /// Accepted even though translucency makes the edge less certain
        /// than it is for an opaque surface. A plane whose corners
        /// disagreed with the plane behind it would read as a rendering
        /// fault rather than as depth, so this stays uniform.
        /// </para>
        /// </summary>
        private static double RadiusFor()
        {
            var metrics = SpatialPalette.Light?.Metrics
                ?? SpatialPalette.Dark?.Metrics
                ?? new SpatialMetrics();
            return metrics.Radius;
        }

        /// <summary>Paints one plane, falling back when no profile is
        /// published.</summary>
        public static void Paint(
            SpatialSurfaceParts parts,
            bool dark,
            SpatialPlane? plane,
            SoftDepthTier tier,
            SoftInteractionState state,
            bool interactive,
            bool selected,
            Color fallbackFill,
            Color fallbackInk)
        {
            if (!parts.Complete) return;

            var profile = SpatialPalette.For(dark);
            if (profile is null)
            {
                parts.ApplyPlain(fallbackFill, fallbackInk);
                return;
            }

            var recipe = new SpatialRecipe(profile, dark);
            var stack = recipe.Resolve(
                plane, tier, state, interactive, selected);
            parts.Apply(stack);
        }
    }
}
