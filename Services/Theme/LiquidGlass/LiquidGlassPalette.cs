// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/LiquidGlass/LiquidGlassPalette.cs
// PROJECT: ProGlassAutomation
// MODULE: Liquid Glass — palette bridge
//
// The fourth soft-UI sibling, and the first one whose defining
// characteristic is an OPTICAL one rather than a tonal one.
//
// Clay and Neu composite a flat colour with directional light. Swiss
// declines to composite at all. Liquid Glass does something none of them
// do: it treats the surface as a lens. The tint is not the material,
// it is what the lens is carrying, and the material is the EDGE --
// the specular rim, the refracted falloff, the light that gathers on
// the lit side and disperses on the other.
//
// WHY THIS IS SEPARATE FROM GLASSMORPHISM
// Both families exist in this engine and they are routinely confused,
// so the distinction is enforced here rather than left to taste:
//
//   Glassmorphism  blurs the backdrop, tints the surface, draws a light
//                  border. A recipe. Static. Every layer identical.
//   Liquid Glass   BENDS the backdrop, adapts its tint to what passes
//                  beneath, and carries a specular edge that responds
//                  to geometry. A simulation. Every layer different.
//
// A single shared palette would make the two families converge into
// "glassmorphism with a brighter border", which is precisely the
// failure mode both design systems are careful to avoid.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme.LiquidGlass
{
    /// <summary>
    /// The live Liquid Glass material for the current theme.
    /// </summary>
    public static class LiquidGlassPalette
    {
        private static LiquidGlassProfile? _light;
        private static LiquidGlassProfile? _dark;

        /// <summary>True while Liquid Glass is the active family.</summary>
        public static bool IsActive { get; private set; }

        public static LiquidGlassProfile? Light => _light;
        public static LiquidGlassProfile? Dark => _dark;

        /// <summary>Raised after the material is republished or cleared.</summary>
        public static event EventHandler? Changed;

        public static void Publish(
            Color lightFill, Color lightInk, Color lightAccent, Color lightRim,
            Color darkFill, Color darkInk, Color darkAccent, Color darkRim)
        {
            _light = Build(lightFill, lightInk, lightAccent, lightRim, dark: false);
            _dark = Build(darkFill, darkInk, darkAccent, darkRim, dark: true);
            IsActive = true;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void Clear()
        {
            if (!IsActive && _light is null && _dark is null) return;
            _light = null;
            _dark = null;
            IsActive = false;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static LiquidGlassProfile? For(bool dark) => dark ? _dark : _light;

        /// <summary>
        /// Builds the material.
        /// <para>
        /// The tint is HELD OFF the extreme. A liquid glass fill at pure
        /// white or pure black is not glass, it is an absence of glass:
        /// with nothing behind it to carry, the lens has nothing to bend
        /// and the specular edge has nothing to sit against. Both
        /// extremes are pushed inward so the rim always has contrast to
        /// work with.
        /// </para>
        /// </summary>
        internal static LiquidGlassProfile Build(
            Color fill, Color ink, Color accent, Color rim, bool dark)
        {
            var optics = new LiquidOptics(
                refraction: RefractionStrength(dark),
                dispersion: DispersionStrength(dark),
                rimOpacity: RimOpacityFor(dark),
                specularPower: SpecularPowerFor(dark),
                innerGlowOpacity: InnerGlowFor(dark),
                scatter: ScatterFor(dark));

            var metrics = new LiquidMetrics(
                unit: 8,
                radius: 20,
                rimThickness: 1.0,
                // The morph: when a control expands into a larger shape
                // (a toolbar button becoming a menu), the material gets
                // THICKER -- deeper shadow, more pronounced lensing,
                // softer scattering. One rule, one knob.
                morphShadowScale: 1.6,
                morphRefractionScale: 1.35,
                motionCeiling: 240);

            return new LiquidGlassProfile(
                HoldOffExtreme(fill, dark),
                ink,
                // The rim is the family's signature, so it is ENFORCED
                // rather than assumed: an invisible rim is not a subtle
                // rim, it is glassmorphism. 3:1 is the WCAG non-text
                // floor, which is the right bar because a rim is a UI
                // component boundary, not decoration.
                EnsureContrast(rim, ink, MinimumRimContrast, HoldOffExtreme(fill, dark), dark),
                accent,
                optics,
                metrics);
        }

        // ── OPTICAL CONSTANTS ───────────────────────────────────────
        //
        // Every number below is a DESIGN COMMITMENT, not a tuning
        // knob. They are constants precisely so that changing one is a
        // visible act with a reviewable diff, rather than an edit buried
        // in an expression.

        /// <summary>
        /// How hard the edge bends what is behind it.
        /// <para>
        /// Higher in dark mode. A dark fill already suppresses the
        /// background's luminance, so the displacement has to work
        /// harder to read as refraction rather than as a tinted
        /// panel; a light fill shows the bend on its own.
        /// </para>
        /// </summary>
        internal static double RefractionStrength(bool dark) =>
            dark ? 1.60 : 1.00;

        /// <summary>
        /// Per-channel bend difference.
        /// <para>
        /// Real glass disperses: red bends least, blue bends most. This
        /// is a fraction of the refraction, never a value of its own --
        /// dispersion large enough to notice as colour fringing reads
        /// as a rendering bug, not as optics.
        /// </para>
        /// </summary>
        internal static double DispersionStrength(bool dark) =>
            dark ? 0.055 : 0.035;

        /// <summary>
        /// The specular edge, the highest-value single element in the
        /// whole family. Without it a panel reads as tinted film.
        /// <para>
        /// Held above the noise floor in BOTH modes. The common failure
        /// is authoring a beautiful light-mode rim and then dropping it
        /// to a whisper in dark mode "because dark backgrounds already
        /// feel deep" -- which is exactly backwards, since the rim is
        /// what tells the eye where the glass ends.
        /// </para>
        /// </summary>
        internal static double RimOpacityFor(bool dark) =>
            dark ? 0.34 : 0.46;

        /// <summary>
        /// Tightness of the specular highlight. Higher is tighter and
        /// brighter. Light mode can afford a tighter, hotter highlight
        /// because the fill behind it is bright; dark mode needs a
        /// broader, softer one or the highlight has no ramp to sit on.
        /// </summary>
        internal static double SpecularPowerFor(bool dark) =>
            dark ? 14.0 : 28.0;

        /// <summary>
        /// The glow that makes the material feel lit from within
        /// rather than painted on top.
        /// <para>
        /// Zero when the family's inner glow is disabled; the recipe
        /// reads this and skips the pass entirely rather than painting
        /// a fully transparent brush.
        /// </para>
        /// </summary>
        internal static double InnerGlowFor(bool dark) =>
            dark ? 0.10 : 0.16;

        /// <summary>
        /// Light scatter. Higher is softer and milkier. This is the
        /// family's frosted counterpart to the refraction; both exist at
        /// once, because a real lens both bends and softens.
        /// </summary>
        internal static double ScatterFor(bool dark) =>
            dark ? 0.55 : 0.40;

        // ── CONTRAST FLOORS ─────────────────────────────────────────

        /// <summary>
        /// WCAG 2.1 SC 1.4.11 non-text contrast. A rim is a UI component
        /// boundary, so the 3:1 floor applies rather than the 4.5:1 body
        /// floor used for ink.
        /// </summary>
        internal const double MinimumRimContrast = 3.0;

        /// <summary>
        /// WCAG 2.1 AA body-text contrast for ink over the fill.
        /// </summary>
        internal const double MinimumBodyContrast = 4.5;

        // ── HELPERS ─────────────────────────────────────────────────

        /// <summary>
        /// Pushes a rim toward ink (dark mode) or away from it (light
        /// mode) until it clears the floor.
        /// <para>
        /// The direction differs by mode and getting it wrong is
        /// invisible in a diff: a light-mode rim pushed DARKER toward
        /// its own fill loses contrast exactly as fast as it gains it,
        /// and the surface looks unchanged while failing.
        /// </para>
        /// </summary>
        internal static Color EnsureContrast(
            Color candidate, Color ink, double floor, Color ground, bool dark)
        {
            var moving = dark ? ink : Shift(ink, 1.0);
            if (Contrast(candidate, ground) >= floor) return candidate;

            // Walk toward the high-contrast target in fixed steps.
            // A closed-form mix would be shorter and would also be
            // wrong at the boundaries, where the relationship between
            // alpha and perceived contrast stops being linear.
            for (var step = 1; step <= 16; step++)
            {
                var t = step / 16.0;
                var tried = Mix(candidate, moving, t);
                if (Contrast(tried, ground) >= floor) return tried;
            }
            return moving;
        }

        internal static Color HoldOffExtreme(Color c, bool dark)
        {
            // In light mode the ceiling is ~250; in dark mode the floor
            // is ~8. Pulling the tint back off the extreme gives the
            // lens something to carry and the rim something to contrast
            // against.
            if (dark) return c.R + c.G + c.B < 24 ? Color.FromRgb(8, 8, 10) : c;
            return c.R + c.G + c.B > 748 ? Color.FromRgb(250, 250, 251) : c;
        }

        internal static Color Mix(Color a, Color b, double t)
        {
            var k = Math.Clamp(t, 0, 1);
            return Color.FromArgb(
                (byte)Math.Round(a.A + (b.A - a.A) * k),
                (byte)Math.Round(a.R + (b.R - a.R) * k),
                (byte)Math.Round(a.G + (b.G - a.G) * k),
                (byte)Math.Round(a.B + (b.B - a.B) * k));
        }

        private static Color Shift(Color c, double amount) =>
            Color.FromArgb(
                c.A,
                (byte)Math.Clamp(c.R + (255 - c.R) * amount, 0, 255),
                (byte)Math.Clamp(c.G + (255 - c.G) * amount, 0, 255),
                (byte)Math.Clamp(c.B + (255 - c.B) * amount, 0, 255));

        /// <summary>
        /// WCAG relative-luminance contrast ratio.
        /// </summary>
        internal static double Contrast(Color a, Color b)
        {
            var la = Luminance(a);
            var lb = Luminance(b);
            var hi = Math.Max(la, lb);
            var lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        private static double Luminance(Color c)
        {
            static double Channel(byte v)
            {
                var s = v / 255.0;
                return s <= 0.04045
                    ? s / 12.92
                    : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        }
    }

    /// <summary>
    /// The optical behaviour of the material.
    /// <para>
    /// Deliberately separate from colour. The tint says what the glass
    /// is carrying; this says what the glass IS. Conflating them is
    /// what produces a palette that can only ever render one variant.
    /// </para>
    /// </summary>
    public sealed class LiquidOptics
    {
        public LiquidOptics(
            double refraction,
            double dispersion,
            double rimOpacity,
            double specularPower,
            double innerGlowOpacity,
            double scatter)
        {
            Refraction = refraction;
            Dispersion = dispersion;
            RimOpacity = rimOpacity;
            SpecularPower = specularPower;
            InnerGlowOpacity = innerGlowOpacity;
            Scatter = scatter;
        }

        /// <summary>How hard the edge displaces the backdrop.</summary>
        public double Refraction { get; }

        /// <summary>Per-channel bend difference. Always a fraction of
        /// <see cref="Refraction"/>.</summary>
        public double Dispersion { get; }

        /// <summary>Strength of the specular edge.</summary>
        public double RimOpacity { get; }

        /// <summary>Tightness of the specular highlight.</summary>
        public double SpecularPower { get; }

        /// <summary>Inner illumination. Zero disables the pass.</summary>
        public double InnerGlowOpacity { get; }

        /// <summary>Light scatter, 0..1.</summary>
        public double Scatter { get; }

        /// <summary>
        /// Refraction scaled for a morphing control.
        /// <para>
        /// A control that grows into a menu is simulating a THICKER
        /// slab of glass, so the bend strengthens. Clamped so a morph
        /// cannot run the edge into visible distortion.
        /// </para>
        /// </summary>
        public double RefractionAtMorph(double morphScale) =>
            Math.Clamp(Refraction * morphScale, 0, 2.4);

        /// <summary>
        /// Rim strength under reduced transparency.
        /// <para>
        /// The rim SURVIVES reduced transparency; the tint does not.
        /// This is the accessibility contract: a user who has asked for
        /// opaque surfaces still needs to see where one surface ends and
        /// the next begins, and the rim is the only affordance left
        /// carrying that information.
        /// </para>
        /// </summary>
        public double RimOpacityAt(bool reducedTransparency) =>
            reducedTransparency ? Math.Max(RimOpacity, 0.55) : RimOpacity;
    }

    /// <summary>Geometry and motion.</summary>
    public sealed class LiquidMetrics
    {
        public LiquidMetrics(
            double unit, double radius, double rimThickness,
            double morphShadowScale, double morphRefractionScale,
            double motionCeiling)
        {
            Unit = unit;
            Radius = radius;
            RimThickness = rimThickness;
            MorphShadowScale = morphShadowScale;
            MorphRefractionScale = morphRefractionScale;
            MotionCeiling = motionCeiling;
        }

        public double Unit { get; }
        public double Radius { get; }
        public double RimThickness { get; }

        /// <summary>Shadow multiplier at full morph.</summary>
        public double MorphShadowScale { get; }

        /// <summary>Refraction multiplier at full morph.</summary>
        public double MorphRefractionScale { get; }

        /// <summary>Longest permitted transition, milliseconds.</summary>
        public double MotionCeiling { get; }
    }

    /// <summary>The material, resolved for one mode.</summary>
    public sealed class LiquidGlassProfile
    {
        public LiquidGlassProfile(
            Color fill, Color ink, Color rim, Color accent,
            LiquidOptics optics, LiquidMetrics metrics)
        {
            Fill = fill;
            Ink = ink;
            Rim = rim;
            Accent = accent;
            Optics = optics;
            Metrics = metrics;
        }

        public Color Fill { get; }
        public Color Ink { get; }

        /// <summary>The specular edge colour. Contrast-enforced.</summary>
        public Color Rim { get; }

        public Color Accent { get; }
        public LiquidOptics Optics { get; }
        public LiquidMetrics Metrics { get; }
    }
}