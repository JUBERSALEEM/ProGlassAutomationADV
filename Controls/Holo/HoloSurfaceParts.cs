// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Holo/HoloSurfaceParts.cs
// PROJECT: ProGlassAutomation
// MODULE: Holographic / Iridescent — construction applied to template parts
//
// FOUR PARTS, AND THE ORDER IS THE ACCESSIBILITY STRATEGY.
//
//   PART_HoloGround   the opaque matte. Text goes here. Always.
//   PART_HoloRim      the spectrum. Decoration only.
//   PART_HoloSheen    the travelling specular band
//   PART_HoloFocusRing  from the RAMP, never from the spectrum
//
// The rim is drawn as a Border over the ground rather than as the
// ground's own border, because the foil is a film ON the surface and
// has to be able to be brighter than any colour the ground could be.
// A border painted into the ground's own brush cannot do that.
//
// The focus ring being excluded from the spectrum is not an oversight.
// A ring drawn in a spectral colour is invisible against the adjacent
// band of its own gradient — the one place where a colourful focus
// indicator reliably fails, and the reason this family cannot use the
// foil for focus however tempting it looks.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Holo;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Holo
{
    /// <summary>The four borders a holographic ControlTemplate names.</summary>
    internal sealed class HoloSurfaceParts
    {
        public Border? Ground { get; private set; }
        public Border? Rim { get; private set; }
        public Border? Sheen { get; private set; }
        public Border? FocusRing { get; private set; }

        public bool Complete =>
            Ground is not null && Rim is not null &&
            Sheen is not null && FocusRing is not null;

        public void Bind(FrameworkElement root)
        {
            Ground = root.Template.FindName("PART_HoloGround", root) as Border;
            Rim = root.Template.FindName("PART_HoloRim", root) as Border;
            Sheen = root.Template.FindName("PART_HoloSheen", root) as Border;
            FocusRing =
                root.Template.FindName("PART_HoloFocusRing", root) as Border;
        }

        /// <summary>Applies a resolved construction.</summary>
        public void Apply(HoloStack stack)
        {
            if (!Complete) return;

            Ground!.Background = Solid(stack.Ground);
            Ground.CornerRadius = new CornerRadius(stack.Radius);

            // ── THE RIM ──
            // Built here rather than handed over by the recipe, because a
            // Brush is a render-time Freezable and a recipe that
            // produced one could not be unit-tested without a
            // dispatcher. HoloStack carries plain colours; this is the
            // only place the gradient exists.
            Rim!.Background = Spectrum(stack.Rim, stack.Motion.RimAngle);
            Rim.CornerRadius = new CornerRadius(stack.Radius);
            Rim.Opacity = stack.RimOpacity;

            // ── THE SHEEN ──
            // Clipped to the rim's ring rather than laid across the
            // ground. A specular band that crosses the content area is
            // the single most effective way to make a label unreadable
            // in a moving family, and it is the reason this one is
            // confined to the edge it belongs on.
            // Straight product, no fudge factor. 0.55 rim x 0.42 sheen
            // is 0.23 at rest, which is bright enough to catch the eye
            // on a 1px rim and far too faint to reach the content.
            Sheen!.Opacity = stack.RimOpacity * stack.Motion.SheenOpacity;
            Sheen.Background = SheenBrush(stack.Motion);
            Sheen.Visibility = stack.RimOpacity > 0.05
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Ink, not a spectral colour. Ink is guaranteed to clear
            // 4.5:1 against Ground by construction (HoloPalette.Build
            // enforces it), so the ring is always visible. Any colour
            // taken from the rim is guaranteed NOT to be — it is
            // guaranteed to match the band it sits next to.
            FocusRing!.BorderBrush = Solid(stack.Ink);
            FocusRing.BorderThickness = new Thickness(
                SoftUiAccessibility.FocusRingWidth);
            FocusRing.Visibility = stack.Focused
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// The flat fallback when holographic is not the active family.
        /// <para>
        /// Deliberately NOT a miniature version of the family: it is a
        /// plain rounded surface with a plain hairline. A control that
        /// shimmered dimly while the app was on claymorphism would be
        /// announcing itself, and the whole point of the fallback is to
        /// be invisible.
        /// </para>
        /// </summary>
        public void ApplyPlain(Color paper, Color ink)
        {
            if (!Complete) return;
            Ground!.Background = Solid(paper);
            Ground.CornerRadius = new CornerRadius(0);
            Rim!.Background = Solid(ink);
            Rim.CornerRadius = new CornerRadius(0);
            Rim.Opacity = 0.18;
            Sheen!.Visibility = Visibility.Collapsed;
            FocusRing!.BorderBrush = Solid(ink);
            FocusRing.BorderThickness = new Thickness(
                SoftUiAccessibility.FocusRingWidth);
            FocusRing.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// The spectral rim gradient.
        /// <para>
        /// One stop per band, no interpolation trickery: the whole point
        /// of the grating is that the band EDGES are visible, and
        /// WPF's default linear interpolation between neighbouring
        /// stops would soften exactly the boundaries the recipe worked
        /// to make uneven.
        /// </para>
        /// </summary>
        private static Brush Spectrum(IReadOnlyList<Color> bands, double angle)
        {
            var g = new LinearGradientBrush();
            g.StartPoint = new Point(0, 0);
            g.EndPoint = new Point(1, 1);
            g.Transform = new RotateTransform(angle);

            // Offsets are the band CENTRES, (i+0.5)/count, never 0..1.
            // A stop at an exact 0 or 1 has no room to its side, which
            // would squeeze the first and last bands toward zero width
            // — and those two are precisely the ones the eye reads as
            // "the edge of the foil".
            for (int i = 0; i < bands.Count; i++)
            {
                g.GradientStops.Add(new GradientStop(
                    bands[i], (i + 0.5) / bands.Count));
            }

            g.Freeze();
            return g;
        }

        /// <summary>
        /// The travelling specular band: bright at its centre, transparent
        /// at both ends, so it reads as a highlight sweeping the rim
        /// rather than as a second gradient lying across the surface.
        /// </summary>
        private static Brush SheenBrush(HoloMotion motion)
        {
            var g = new LinearGradientBrush();
            g.StartPoint = new Point(0, 0.5);
            g.EndPoint = new Point(1, 0.5);

            double w = Math.Clamp(motion.SheenWidth, 0.05, 0.9);
            var white = Colors.White;

            g.GradientStops.Add(
                new GradientStop(Transparent(white, 0.0), 0.0));
            g.GradientStops.Add(
                new GradientStop(Transparent(white, 0.85), Math.Clamp(0.5 - w / 2, 0.0, 1.0)));
            g.GradientStops.Add(
                new GradientStop(Transparent(white, 1.0), 0.5));
            g.GradientStops.Add(
                new GradientStop(Transparent(white, 0.85), Math.Clamp(0.5 + w / 2, 0.0, 1.0)));
            g.GradientStops.Add(
                new GradientStop(Transparent(white, 0.0), 1.0));

            g.Freeze();
            return g;
        }

        private static Color Transparent(Color c, double alpha)
        {
            return Color.FromArgb(
                (byte)Math.Round(Math.Clamp(alpha, 0.0, 1.0) * 255.0),
                c.R, c.G, c.B);
        }

        private static SolidColorBrush Solid(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>
        /// The single entry point every holographic control uses.
        /// Never throws: a template that has not been applied, or a
        /// family that is not active, both degrade to the plain
        /// fallback rather than taking the visual tree down.
        /// </summary>
        public static void Paint(
            HoloSurfaceParts parts,
            bool dark,
            bool reducedMotion,
            SoftDepthTier tier,
            SoftInteractionState state,
            bool selected,
            Color fallbackPaper,
            Color fallbackInk)
        {
            if (parts is null || !parts.Complete) return;

            var profile = HoloPalette.For(dark);
            if (profile is null)
            {
                parts.ApplyPlain(fallbackPaper, fallbackInk);
                return;
            }

            try
            {
                parts.Apply(new HoloRecipe(profile, dark, reducedMotion)
                    .Resolve(tier, state, selected));
            }
            catch (Exception)
            {
                parts.ApplyPlain(fallbackPaper, fallbackInk);
            }
        }
    }
}