// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Eight/EightThemeId.cs
// PROJECT: ProGlassAutomation
// MODULE: Eight-theme engine — identity contract
//
// WHY THIS FILE EXISTS
// ───────────────────────────────────────────────────────────────────────
// The application used to carry four ad-hoc visual families whose names
// described IMPLEMENTATION details ("Normal (Industrial)", "Crystal
// Fluid", "Ios-17pm"). Those strings are persisted verbatim into every
// user's settings file, so they can never be renamed in place — and they
// say nothing to a user choosing an appearance.
//
// The eight families below are the product contract: eight DESIGN STYLES,
// each with its own material physics, geometry scale, elevation model,
// motion language and built-in scene. They are not eight colour variants
// of one layout; they are eight different answers to "how does light
// behave on a surface?".
//
// THE EIGHT, AND WHY EACH ONE IS DIFFERENT
// ───────────────────────────────────────────────────────────────────────
//  BentoGrid       Layout-first. Unequal modular cells, one anchor, uniform
//                  gutters, flat surfaces, ONE shadow level. Hierarchy is
//                  carried by SIZE, not by blur or drop shadow.
//  Spatial         Depth-first. A real Z hierarchy (environment → ambient →
//                  panel → navigation → overlay) where blur radius itself is
//                  the depth signal. Glass over opacity, always.
//  Swiss           Rational-first. A mathematical 12-column grid, hierarchy by
//                  type scale and weight alone, one flat accent, and a hard
//                  ban on gradients, shadows and transforms. Precision is the
//                  aesthetic.
//  Holographic     Iridescence-first. Dark ground so spectral banding reads;
//                  a drifting foil sheen over MATTE base surfaces, never a
//                  rainbow painted over a working surface.
//  Neumorphism     Extrusion-first. Surfaces share the page colour exactly;
//                  depth comes from ONE light and ONE dark OUTSET shadow, and
//                  pressed state flips to INSET. Ships a mandatory non-shadow
//                  edge cue because shadow alone fails WCAG 1.4.11.
//  Claymorphism    Inflation-first. A separate pastel fill, ONE outer tinted
//                  drop plus TWO inset shadows. Real figure-ground contrast,
//                  and a radius roughly twice neumorphism's.
//  LiquidGlass     Refraction-first (Apple WWDC 2025). Clear centre, heavy
//                  blur, HIGH saturation, a specular rim that catches light,
//                  and a tint that keeps text legible over any backdrop.
//  Aurora          The backdrop-driven family. The FIRST in this list whose
//                  subject is the SKY rather than the control: three slow
//                  drifting colour fields behind translucent panels whose
//                  rims are gradients. Dark substrate only; there is no
//                  light mode to invert into.
//  Glassmorphism   The 2020 recipe. Uniform backdrop blur, semi-transparent
//                  fill, one hairline border, static. Included precisely
//                  because it is NOT liquid glass — the two get confused
//                  constantly and users ask for both.
//
// IDENTITY IS THE PERSISTED KEY. Like the old family constants, these
// strings go into settings and must never be renamed in place.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Services.Theme.Eight
{
    /// <summary>The eight selectable appearance families. The numeric
    /// values are never persisted — <see cref="EightThemeNames"/> carries
    /// the stable string identity.</summary>
    public enum EightThemeId
    {
        BentoGrid = 0,
        Spatial = 1,
        Swiss = 2,
        Holographic = 3,
        Neumorphism = 4,
        Claymorphism = 5,
        LiquidGlass = 6,
        Glassmorphism = 7,
        Aurora = 8,
    }

    /// <summary>
    /// Stable, persisted identity strings. These are the values stored in
    /// <c>App.Config.Appearance.VisualTheme</c>. They are frozen forever:
    /// renaming one silently resets that user's saved appearance.
    /// </summary>
    public static class EightThemeNames
    {
        public const string BentoGrid = "Bento Grid";
        public const string Spatial = "Spatial";
        public const string Swiss = "Swiss International";
        public const string Holographic = "Holographic";
        public const string Neumorphism = "Neumorphism";
        public const string Claymorphism = "Claymorphism";
        public const string LiquidGlass = "Liquid Glass";
        public const string Glassmorphism = "Glassmorphism";
        public const string Aurora = "Aurora";

        /// <summary>The shipped default. Liquid Glass is the family's native
        /// home: it was already the most-developed material in this codebase
        /// (Themes/CrystalFluid, Themes/Styles/LiquidGlassDesign.xaml), so
        /// defaulting to it means the richest rendering is the one users see
        /// first, and the migration of the old "Crystal Fluid" profile lands
        /// on a family that looks the same rather than merely similar.</summary>
        public const string Default = LiquidGlass;

        /// <summary>Every identity, in Theme Center display order. The order
        /// is a design decision, not an accident: it walks the reader from the
        /// most structurally explicit style to the most materially explicit
        /// one, with the three "soft 3D" families (Neu / Clay / the glasses)
        /// sitting together at the end where they can be compared directly.</summary>
        public static readonly string[] All =
        {
            // Aurora leads deliberately. It is the only family here whose
            // subject is the backdrop rather than the control, and listed
            // after Bento it reads as a Bento variant rather than the
            // inversion it actually is.
            Aurora,
            BentoGrid,
            Spatial,
            Swiss,
            Holographic,
            Neumorphism,
            Claymorphism,
            LiquidGlass,
            Glassmorphism,
        };
    }

    /// <summary>Material physics — how a family makes light behave. Every
    /// painting decision in the engine hangs off this value.</summary>
    public enum EightMaterialKind
    {
        /// <summary>Opaque fill. Depth comes from keylines and one shadow
        /// level, never from translucency. (Bento, Swiss)</summary>
        Solid = 0,

        /// <summary>Translucent, uniform backdrop blur, hairline rim, static.
        /// The 2020 recipe. (Glassmorphism)</summary>
        Frosted = 1,

        /// <summary>Translucent with a specular rim and content-adaptive tint;
        /// heavier blur and higher saturation than Frosted. (Liquid Glass)</summary>
        Refractive = 2,

        /// <summary>Surface colour EQUALS the page colour; one light + one
        /// dark OUTSET shadow extrude it; pressed flips to INSET.
        /// (Neumorphism)</summary>
        Extruded = 3,

        /// <summary>Distinct pastel fill, one outer tinted drop plus two inset
        /// shadows. Real figure-ground contrast. (Claymorphism)</summary>
        Inflated = 4,

        /// <summary>Matte base surfaces carrying a drifting spectral foil
        /// sheen on edges and accents. (Holographic)</summary>
        Foil = 5,

        /// <summary>Translucent panels arranged on a real Z hierarchy where
        /// blur radius encodes depth. (Spatial)</summary>
        Volumetric = 6,

        /// <summary>Depth is ATMOSPHERIC, not geometric. The surface is a
        /// translucent panel floating over a live drifting colour field;
        /// there is no elevation ladder, no shadow, and no blur -- the
        /// sense of distance comes entirely from the sky behind it.
        /// (Aurora)</summary>
        Luminous = 7,
    }
}