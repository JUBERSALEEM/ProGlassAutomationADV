// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Aurora/AuroraSurfaceParts.cs
// PROJECT: ProGlassAutomation
// MODULE: Aurora — construction applied to template parts
//
// TWO PARTS PER CONTROL, AND ONE OF THEM IS THE FAMILY'S SIGNATURE
//
//   PART_AuroraFill     the translucent panel (carries the glow)
//   PART_AuroraRim      the GRADIENT rim -- teal into violet
//   PART_AuroraBlobHost the sky itself (AuroraSurface only)
//
// ── THE RIM IS A GRADIENT, AND THAT IS THE DIFFERENCE ─────────────────
//
// Every other family in this engine draws a flat 1px rim in a single
// colour. Aurora draws a LinearGradientBrush from RimLow to RimHigh,
// because the rim has a job no other family's rim has: the sky behind the
// panel is MOVING, so a flat 1px edge loses and re-finds contrast as the
// blobs drift past it. A two-stop gradient keeps a bright edge on both
// sides of the panel at once, which is also what the reference render
// shows -- teal on the low edge, violet on the high one.
//
// WPF does this natively: Border.BorderBrush is a Brush, not a Colour.
// No shader, no clip, no geometry. Rim is a SEPARATE Border from Fill
// (rather than Fill's own BorderBrush) so that rim thickness, rim
// brightness and fill alpha can move independently -- press and hover
// here change fill alpha only, and must never touch the edge.
//
// ── GLOW IS NOT A SHADOW, AND THE DISTINCTION IS LOAD-BEARING ─────────
//
// AuroraStack.HasShadow is false. This family has no elevation ladder,
// exactly as Glassmorphism does not -- depth is the aurora's job.
//
// The glow is a reserved channel: the primary action and the active nav
// item, and nothing else. It is a DropShadowEffect with ShadowDepth 0 --
// which renders as a symmetric halo rather than a directional cast --
// applied to the Fill part itself, so it is cast from the panel's real
// silhouette. It is never animated: a blurred effect re-rasterises every
// frame, and AuroraMetrics.MotionCeiling is 120ms precisely so nothing
// here can be tempted to.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ProGlassAutomation.Services.Theme.Aurora;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Aurora
{
    /// <summary>The parts an Aurora ControlTemplate must name.</summary>
    internal sealed class AuroraSurfaceParts
    {
        /// <summary>The translucent panel.</summary>
        public Border? Fill { get; private set; }

        /// <summary>The gradient rim.</summary>
        public Border? Rim { get; private set; }

        /// <summary>The sky, on AuroraSurface only.</summary>
        public Canvas? BlobHost { get; private set; }

        /// <summary>
        /// What an ordinary control needs. BlobHost is excluded because
        /// only the backdrop control owns the sky.
        /// </summary>
        public bool Complete => Fill is not null && Rim is not null;

        public void Bind(FrameworkElement root)
        {
            Fill = root.Template.FindName("PART_AuroraFill", root) as Border;
            Rim = root.Template.FindName("PART_AuroraRim", root) as Border;
            BlobHost =
                root.Template.FindName("PART_AuroraBlobHost", root) as Canvas;
        }

        public void Apply(AuroraStack stack)
        {
            var c = stack.Cell;

            if (Fill is not null)
            {
                var brush = new SolidColorBrush(c.Fill);
                brush.Freeze();
                Fill.Background = brush;
                Fill.CornerRadius = new CornerRadius(RadiusFor());

                // The glow is cast FROM THE PANEL'S OWN SILHOUETTE, by
                // giving Fill itself a symmetric effect. It was on a
                // separate PART_AuroraGlow border until this was measured:
                // a border with a transparent Background renders nothing,
                // and WPF casts a DropShadowEffect from what an element
                // actually draws. A dedicated glow host therefore cast
                // NO halo at all -- which is the quietest possible
                // failure, because the element still existed and still
                // satisfied every name check.
                //
                // Direction 0 and ShadowDepth 0 keep it symmetric, so it
                // reads as a halo rather than an elevation shadow. This
                // family has no elevation.
                if (c.GlowOpacity > 0.001)
                {
                    var glow = new DropShadowEffect
                    {
                        Color = c.Glow,
                        BlurRadius = GlowRadiusFor(),
                        Direction = 0,
                        ShadowDepth = 0,
                        Opacity = c.GlowOpacity,
                        RenderingBias = RenderingBias.Quality,
                    };
                    glow.Freeze();
                    Fill.Effect = glow;
                }
                else
                {
                    Fill.Effect = null;
                }
            }

            if (Rim is not null)
            {
                // RimLow -> RimHigh, on the diagonal. The angle is fixed
                // and matches the field's own drift direction, so the
                // edge and the sky agree about which way is up.
                var rimBrush = new LinearGradientBrush(
                    Color.FromArgb(255, c.RimLow.R, c.RimLow.G, c.RimLow.B),
                    Color.FromArgb(255, c.RimHigh.R, c.RimHigh.G, c.RimHigh.B),
                    new Point(0.0, 0.0),
                    new Point(1.0, 1.0));
                rimBrush.Freeze();
                Rim.BorderBrush = rimBrush;
                Rim.CornerRadius = new CornerRadius(RadiusFor());

                // Never zero. A panel with no edge over a moving sky is a
                // panel the user cannot find.
                Rim.BorderThickness = new Thickness(
                    Math.Max(1, c.RimThickness));
            }
        }

        /// <summary>
        /// A plain surface for when no Aurora profile is published, so an
        /// Aurora control in a non-Aurora theme still renders as a
        /// readable pane rather than an invisible one.
        /// </summary>
        public void ApplyPlain(Color fill, Color ink)
        {
            if (Fill is not null)
            {
                var brush = new SolidColorBrush(fill);
                brush.Freeze();
                Fill.Background = brush;
                Fill.CornerRadius = new CornerRadius(RadiusFor());
                Fill.Effect = null;
            }

            if (Rim is not null)
            {
                var rim = new SolidColorBrush(ink);
                rim.Freeze();
                Rim.BorderBrush = rim;
                Rim.BorderThickness = new Thickness(1);
                Rim.CornerRadius = new CornerRadius(RadiusFor());
            }
        }

        private static AuroraMetrics MetricsFor() =>
            AuroraPalette.Light?.Metrics
            ?? AuroraPalette.Dark?.Metrics
            ?? new AuroraMetrics();

        private static double RadiusFor() => MetricsFor().Radius;

        private static double GlowRadiusFor() => MetricsFor().GlowRadius;

        /// <summary>
        /// The single entry point. Every Aurora control paints through
        /// here, so there is exactly one place that knows how a family
        /// surface is built and exactly one place that can fall back.
        /// </summary>
        public static void Paint(
            AuroraSurfaceParts parts,
            bool dark,
            AuroraPlacement placement,
            SoftDepthTier tier,
            SoftInteractionState state,
            bool interactive,
            bool selected,
            Color fallbackFill,
            Color fallbackInk)
        {
            if (!parts.Complete) return;

            var profile = AuroraPalette.For(dark);
            if (profile is null)
            {
                parts.ApplyPlain(fallbackFill, fallbackInk);
                return;
            }

            var recipe = new AuroraRecipe(profile, dark);
            var stack = recipe.Resolve(
                placement, tier, state, interactive, selected);
            parts.Apply(stack);
        }
    }
}
