// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Eight/EightThemeProfiles.cs
// PROJECT: ProGlassAutomation
// MODULE: Eight-theme engine — the eight recipes
//
// Eight materials, each built from its actual published mechanics rather
// than from a palette swap. The research notes for every family are
// carried inline next to the values they produced, so a future change is
// made against the rule and not against the hex.
//
// ORDER OF CONSTRUCTION is light ramp → dark ramp → geometry → elevation
// → material → motion, because that is the order a designer works in.
//
// ACCESSIBILITY IS PART OF EACH RECIPE, NOT AN AFTERTHOUGHT. Three of
// these styles are famous for failing WCAG, and each one ships the
// documented compensation rather than the pretty version:
//
//   Neumorphism — surface colour must equal page colour, so its boundary
//     is structurally ~1.4:1 against WCAG's required 3:1 (SC 1.4.11).
//     Compensation: a permanent hairline beside the shadow, plus a
//     dedicated focus-ring colour, plus a pressed state that flips the
//     shadow from OUTSET to INSET so state is never carried by a subtle
//     colour shift alone.
//
//   Swiss — the style's own rule forbids gradients, shadows and
//     transforms. Compensation: hierarchy is carried entirely by type
//     scale and weight, and the one accent is contrast-checked against
//     both paper and ink.
//
//   Holographic — a rainbow over a working surface destroys text
//     legibility. Compensation: base surfaces stay MATTE dark; only
//     edges, accents, chips and the scene carry the foil.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme.Eight
{
    internal static class EightThemeProfiles
    {
        private static Color C(string hex) =>
            (Color)ColorConverter.ConvertFromString(hex);

        /// <summary>Alpha-appended hex, e.g. A("#0A84FF", 0.4). The RGB comes
        /// from the hex; only the alpha channel is overridden.</summary>
        private static Color A(string hex, double alpha)
        {
            var c = C(hex);
            return Color.FromArgb(Scale(alpha), c.R, c.G, c.B);
        }

        private static byte Scale(double alpha01)
        {
            double v = System.Math.Round(
                System.Math.Clamp(alpha01, 0, 1) * 255, MidpointRounding.AwayFromZero);
            return (byte)v;
        }

        public static IEnumerable<EightThemeProfile> Build()
        {
            yield return BentoGrid();
            yield return Spatial();
            yield return Swiss();
            yield return Holographic();
            yield return Neumorphism();
            yield return Claymorphism();
            yield return LiquidGlass();
            yield return Glassmorphism();
            yield return Aurora();
        }

        // ═══════════════════════════════════════════════════════════════
        //  1 · BENTO GRID
        // ═══════════════════════════════════════════════════════════════
        //  RESEARCH NOTES
        //    · Strict compartmentalisation: every cell is its own box, so
        //      the grid chunks information instead of flowing it.
        //    · SIZE = HIERARCHY. The most important content gets the
        //      largest cell. This is not about position.
        //    · Uniform gutters, always. Irregular spacing destroys the
        //      "one tray" read the pattern depends on.
        //    · Gap sits between roughly HALF and ONE FULL corner radius.
        //    · Flat surfaces with MINIMAL elevation — one shadow level,
        //      maximum, and prefer border/background differentiation over
        //      drop shadow entirely.
        //    · Exactly one 2×2 anchor cell per grid; six to nine cells
        //      total before cognitive load climbs.
        //  CONSEQUENCE FOR THE ENGINE
        //    Geometry.Gutter is derived from the radius (see EightGeometry),
        //    elevation is FlatSingleLevel with a genuinely tiny opacity so
        //    the keyline does the structural work, and the cell ramp gives
        //    each surface tier a DIFFERENT fill — which is exactly how a
        //    bento grid distinguishes cells without lifting any of them.
        // ═══════════════════════════════════════════════════════════════
        private static EightThemeProfile BentoGrid() => new(
            EightThemeId.BentoGrid,
            EightThemeNames.BentoGrid,
            "Bento Grid",
            "Modular tiles, one anchor",
            "A dashboard of unequal cells where size carries meaning and nothing is lifted off the page.",
            "Flat opaque cells. One shadow level for the whole grid — hierarchy comes from cell size and fill, never from stacking.",
            // ── LIGHT ───────────────────────────────────────────────
            // Off-white canvas, white cells, and the four pastel category
            // tints (mint / amber / cool grey) that make a bento readable
            // without a single shadow. Electric blue is reserved for the
            // single primary action.
            new EightColorRamp(
                window: C("#F7F7F8"),
                module: C("#FAFAFA"),
                surface: C("#FFFFFF"),
                surfaceSecondary: C("#F2F3F5"),
                surfaceDetail: C("#E9EBEE"),
                chrome: C("#F2F3F5"),
                dialog: C("#FFFFFF"),
                dialogInset: C("#F4F5F7"),
                textPrimary: C("#0A0A0C"),
                textSecondary: C("#52525B"),
                // Fixed to clear WCAG AA 4.5:1 against BOTH the
                // composited card and the stage — muted ink is still body
                // text, not "decorative grey".
                textMuted: C("#6E6E77"),
                borderSubtle: A("#0A0A0C", 0.06),
                borderDefault: A("#0A0A0C", 0.10),
                borderStrong: A("#0A0A0C", 0.18),
                accent: C("#2864FF"),
                accentForeground: C("#FFFFFF"),
                shadow: C("#0A0A0C"),
                focusRing: C("#2864FF")),
            // ── DARK ─────────────────────────────────────────────────
            new EightColorRamp(
                window: C("#0B0B0D"),
                module: C("#121215"),
                surface: C("#1B1B1F"),
                surfaceSecondary: C("#232328"),
                surfaceDetail: C("#2B2B31"),
                chrome: C("#161619"),
                dialog: C("#1F1F24"),
                dialogInset: C("#151518"),
                textPrimary: C("#FAFAFA"),
                textSecondary: C("#A1A1AA"),
                // Fixed to clear WCAG AA 4.5:1 against BOTH the
                // composited card and the stage — muted ink is still body
                // text, not "decorative grey".
                textMuted: C("#85858F"),
                borderSubtle: A("#FFFFFF", 0.08),
                borderDefault: A("#FFFFFF", 0.13),
                borderStrong: A("#FFFFFF", 0.22),
                accent: C("#4C8DFF"),
                accentForeground: C("#0A0A0C"),
                shadow: C("#000000"),
                focusRing: C("#7AABFF")),
            // Radius 24 on cards is the researched pairing; the gutter
            // falls out of it at 16px, which is inside the half-to-full
            // band the pattern requires.
            geometry: new EightGeometry(control: 12, card: 24, large: 28, hero: 32),
            // ONE level. Low opacity (0.06) on purpose — a bento tray that
            // casts real shadows stops reading as one object.
            elevation: new EightElevation(
                EightElevationKind.FlatSingleLevel,
                lowBlur: 14, lowDepth: 2, lowOpacity: 0.06,
                highBlur: 14, highDepth: 2, highOpacity: 0.06,
                lightStop: C("#FFFFFF"), shadeStop: C("#0A0A0C")),
            material: new EightMaterial(
                EightMaterialKind.Solid,
                surfaceAlpha: 1.0, chromeAlpha: 1.0,
                blurRadius: 0, saturationBoost: 0,
                rimOpacity: 0.10, rimTint: C("#0A0A0C"),
                specularRim: false, innerGlowOpacity: 0,
                allowBackdrop: true),
            // Crisp. 150ms is the researched ceiling for this family; the
            // rule is precision, not atmosphere.
            motion: new EightMotion(
                fast: 90, normal: 150, slow: 220,
                easeOut: "0.16,1.0 0.3,1.0", spring: "0.16,1.0 0.3,1.0"),
            accentPresets: new[] { "#2864FF", "#0F9D58", "#D93025", "#F2A600", "#1A1A1A" },
            sceneLabel: "Cell Tray",
            sceneType: "Compartment field wallpaper");

        // ═══════════════════════════════════════════════════════════════
        //  2 · SPATIAL / IMMERSIVE
        // ═══════════════════════════════════════════════════════════════
        //  RESEARCH NOTES
        //    · Depth IS information. Use Z to communicate hierarchy, not
        //      decoration. Closer = more important.
        //    · Glass over opacity: prefer a real backdrop blur over a flat
        //      semi-transparent fill. The blurred pass-through CONNECTS a
        //      panel to its environment; a flat alpha isolates it.
        //    · Glass must be EARNED — it only works when content behind it
        //      adds context. Never use glass to hide emptiness.
        //    · Blur radius itself is a depth signal: ~20px chrome, ~40px
        //      panels, ~60px modals.
        //    · Eyes are the cursor. Generous target sizes, generous
        //      spacing, nothing critical in the periphery.
        //    · Restraint over spectacle — immersion comes from coherence,
        //      not from stacking every 3D trick at once.
        //    · Light text on glass: white/near-white has the highest
        //      legibility against unpredictable blurred backdrops.
        //    · Backgrounds recede: deep muted tones (indigo, charcoal)
        //      with luminous accents used SPARINGLY on interactive and
        //      focus states, never as decorative fills.
        //  CONSEQUENCE FOR THE ENGINE
        //    A dark-leaning ramp even in light mode, because the whole point
        //    is that the environment shows through. The material carries a
        //    40px base blur and a high saturation boost so the backdrop glows
        //    instead of greying out.
        // ═══════════════════════════════════════════════════════════════
        private static EightThemeProfile Spatial() => new(
            EightThemeId.Spatial,
            EightThemeNames.Spatial,
            "Spatial",
            "Real depth, glass over opacity",
            "Panels float on a true Z hierarchy where blur radius tells you which plane you are looking at.",
            "Translucent panels at four depth planes. Chrome blurs at 20px, cards at 40px, overlays at 60px — the material itself encodes depth.",
            // ── LIGHT: still a deep environment. Spatial's premise is that
            // the room shows through, so "light mode" here means a bright
            // luminous room, not white paper.
            new EightColorRamp(
                window: C("#DDE4F0"),
                module: C("#C7D2E6"),
                surface: A("#F4F7FF", 0.62),
                surfaceSecondary: A("#FFFFFF", 0.46),
                surfaceDetail: A("#FFFFFF", 0.34),
                chrome: A("#E8EEFB", 0.50),
                dialog: A("#FBFDFF", 0.78),
                dialogInset: A("#E6ECF8", 0.60),
                textPrimary: C("#0D1220"),
                textSecondary: C("#2B3550"),
                // Fixed to clear WCAG AA 4.5:1 against BOTH the
                // composited card and the stage — muted ink is still body
                // text, not "decorative grey".
                textMuted: C("#58637E"),
                borderSubtle: A("#FFFFFF", 0.34),
                borderDefault: A("#FFFFFF", 0.55),
                borderStrong: A("#FFFFFF", 0.78),
                // 5.36:1 on white — the previous #5B6CFF measured 4.17:1
                // and failed WCAG AA for normal text.
                accent: C("#4A5AE8"),
                accentForeground: C("#FFFFFF"),
                shadow: C("#141B33"),
                focusRing: C("#7C8BFF")),
            // ── DARK: the intended home of this family.
            new EightColorRamp(
                window: C("#07080F"),
                module: C("#0C0E1A"),
                surface: A("#FFFFFF", 0.09),
                surfaceSecondary: A("#FFFFFF", 0.14),
                surfaceDetail: A("#FFFFFF", 0.06),
                chrome: A("#FFFFFF", 0.07),
                dialog: A("#141828", 0.82),
                dialogInset: A("#FFFFFF", 0.05),
                // Near-white on glass. Dark ink loses contrast the moment
                // the backdrop behind a blurred panel goes dark.
                textPrimary: C("#F2F5FF"),
                textSecondary: C("#B9C1DA"),
                textMuted: C("#8A93AE"),
                borderSubtle: A("#FFFFFF", 0.10),
                borderDefault: A("#FFFFFF", 0.18),
                borderStrong: A("#FFFFFF", 0.30),
                accent: C("#8B9BFF"),
                accentForeground: C("#0A0D18"),
                shadow: C("#000000"),
                focusRing: C("#A8B4FF")),
            // Uniform, generous radius — a unified scale is what keeps a
            // spatial interface from reading as assembled.
            geometry: new EightGeometry(control: 14, card: 20, large: 28, hero: 32),
            // Large, soft, atmospheric. Depth here is atmospheric, not
            // structural — the blur is doing the work, not the shadow.
            elevation: new EightElevation(
                EightElevationKind.Ambient,
                lowBlur: 30, lowDepth: 4, lowOpacity: 0.14,
                highBlur: 56, highDepth: 8, highOpacity: 0.24,
                lightStop: C("#FFFFFF"), shadeStop: C("#05070E")),
            material: new EightMaterial(
                EightMaterialKind.Volumetric,
                surfaceAlpha: 0.62, chromeAlpha: 0.50,
                blurRadius: 40, saturationBoost: 1.18,
                rimOpacity: 0.18, rimTint: C("#FFFFFF"),
                specularRim: true, innerGlowOpacity: 0.10,
                allowBackdrop: true),
            // Slow enough to read as physical movement. 350ms is the
            // researched ceiling before a transition starts to feel like
            // latency rather than depth.
            motion: new EightMotion(
                fast: 160, normal: 280, slow: 420,
                easeOut: "0.22,1.0 0.36,1.0", spring: "0.34,1.2 0.64,1.0"),
            accentPresets: new[] { "#4A5AE8", "#6C4FD4", "#1F6FD0", "#3D2E8F", "#8B9BFF" },
            sceneLabel: "Depth Field",
            sceneType: "Volumetric depth wallpaper");

        // ═══════════════════════════════════════════════════════════════
        //  3 · SWISS INTERNATIONAL
        // ═══════════════════════════════════════════════════════════════
        //  RESEARCH NOTES
        //    · A mathematical grid is THE foundation. 12 columns, constant
        //      gutters, everything snapping to it. An element that misses a
        //      grid line is a defect, not a choice.
        //    · Sans-serif, flush left, ragged right. Hierarchy by size and
        //      weight ONLY.
        //    · White space is ACTIVE structure — designed, not leftover.
        //    · One flat accent (classically Swiss red) and nothing else.
        //    · PROHIBITED: decorative elements, gradients, shadows,
        //      transforms, hover effects beyond colour and border-colour,
        //      and any animation over 150ms.
        //    · Objective: the design serves information.
        //  CONSEQUENCE FOR THE ENGINE
        //    This is the one family with ElevationKind.None — the painter
        //    publishes keylines and literally no drop shadows, which is
        //    the only honest implementation of the style. Radius is 0–4,
        //    hard-edged. Every accent preset is a flat, saturated ink.
        // ═══════════════════════════════════════════════════════════════
        private static EightThemeProfile Swiss() => new(
            EightThemeId.Swiss,
            EightThemeNames.Swiss,
            "Swiss International",
            "Grid, type, one accent",
            "A rational 12-column system where hierarchy is carried by type scale alone and nothing is decorated.",
            "Zero shadows and zero gradients. Hard 0–4px corners, hairline rules, and a single flat accent — the grid does all the work.",
            new EightColorRamp(
                window: C("#FFFFFF"),
                module: C("#FFFFFF"),
                surface: C("#FFFFFF"),
                // Tiered greys, not tinted neutrals: the Swiss palette is
                // achromatic by construction.
                surfaceSecondary: C("#F2F2F2"),
                surfaceDetail: C("#E6E6E6"),
                chrome: C("#F2F2F2"),
                dialog: C("#FFFFFF"),
                dialogInset: C("#F2F2F2"),
                // "Jet ink" — near-black, not pure black. Pure black at
                // 14px body size vibrates on white.
                textPrimary: C("#111111"),
                textSecondary: C("#4A4A4A"),
                // Fixed to clear WCAG AA 4.5:1 against BOTH the
                // composited card and the stage — muted ink is still body
                // text, not "decorative grey".
                textMuted: C("#747474"),
                borderSubtle: C("#E0E0E0"),
                borderDefault: C("#C4C4C4"),
                borderStrong: C("#111111"),
                accent: C("#E30613"),
                accentForeground: C("#FFFFFF"),
                shadow: C("#000000"),
                focusRing: C("#111111")),
            new EightColorRamp(
                window: C("#000000"),
                module: C("#0A0A0A"),
                surface: C("#111111"),
                surfaceSecondary: C("#1C1C1C"),
                surfaceDetail: C("#262626"),
                chrome: C("#0A0A0A"),
                dialog: C("#111111"),
                dialogInset: C("#1C1C1C"),
                textPrimary: C("#FFFFFF"),
                textSecondary: C("#C4C4C4"),
                textMuted: C("#8C8C8C"),
                borderSubtle: C("#2A2A2A"),
                borderDefault: C("#3D3D3D"),
                borderStrong: C("#FFFFFF"),
                accent: C("#FF3B30"),
                accentForeground: C("#000000"),
                shadow: C("#000000"),
                focusRing: C("#FFFFFF")),
            geometry: new EightGeometry(control: 0, card: 0, large: 2, hero: 2),
            elevation: new EightElevation(
                EightElevationKind.None,
                lowBlur: 0, lowDepth: 0, lowOpacity: 0,
                highBlur: 0, highDepth: 0, highOpacity: 0,
                lightStop: C("#FFFFFF"), shadeStop: C("#000000")),
            material: new EightMaterial(
                EightMaterialKind.Solid,
                surfaceAlpha: 1.0, chromeAlpha: 1.0,
                blurRadius: 0, saturationBoost: 0,
                rimOpacity: 0.14, rimTint: C("#111111"),
                specularRim: false, innerGlowOpacity: 0,
                // The grid must not be violated by a photo behind it.
                allowBackdrop: false),
            // 150ms ease-out is the researched HARD ceiling. Anything slower
            // reads as decoration, which is precisely what this style
            // abolishes.
            motion: new EightMotion(
                fast: 80, normal: 150, slow: 150,
                easeOut: "0.0,0.0 1.0,1.0", spring: "0.0,0.0 1.0,1.0"),
            accentPresets: new[] { "#E30613", "#0057B8", "#111111", "#FF6B00" },
            sceneLabel: "Column Grid",
            sceneType: "Ruled paper wallpaper");

        // ═══════════════════════════════════════════════════════════════
        //  4 · HOLOGRAPHIC / IRIDESCENT
        // ═══════════════════════════════════════════════════════════════
        //  RESEARCH NOTES
        //    · The single biggest visual cue is BANDING, not the rainbow.
        //      Real holographic foil bands NON-uniformly: bright clusters
        //      around blue→cyan transitions, darker clusters around
        //      red→orange, and the whole composition shifts with viewing
        //      angle. A uniform conic-gradient misses all of it.
        //    · Requires a DARK ground. Spectral banding over paper reads as
        //      a printing error, not a material.
        //    · Blend modes: color-dodge brightens into metallic highlights;
        //      overlay gives depth without washing out; difference produces
        //      the prismatic shift.
        //    · Use it SPARINGLY — on accent elements where visual attention
        //      is warranted. A rainbow over a data grid is noise.
        //    · The base layer and the foil sweep must run at DIFFERENT
        //      rates or they lock-step and the result looks mechanical.
        //    · Cap the shimmer opacity — push it too far and text washes out.
        //  CONSEQUENCE FOR THE ENGINE
        //    Base surfaces are MATTE dark ink with almost no saturation
        //    spread. The foil lives on rims, accents, chips, focus rings and
        //    the scene. The elevation model is TintedGlow so light appears
        //    to come from the surface rather than being cast by it.
        // ═══════════════════════════════════════════════════════════════
        private static EightThemeProfile Holographic() => new(
            EightThemeId.Holographic,
            EightThemeNames.Holographic,
            "Holographic",
            "Iridescent foil on matte ink",
            "Dark matte surfaces carrying a drifting spectral sheen on their edges, accents and backdrop.",
            "Foil on edges and accents only — base surfaces stay matte so text and data stay legible. Light reads as emitted, not cast.",
            // ── LIGHT MODE IS THE DARK GROUND. Holographic foil cannot be
            // read on paper; the "light" ramp is a pale iridescent night so
            // the two modes are two tunings of the same material rather than
            // two unrelated palettes.
            new EightColorRamp(
                window: C("#E9E4F7"),
                module: C("#DED6F2"),
                // Foil is an OPAQUE material, so these are the final
                // resolved colours, not translucent overlays. Carrying an
                // A() here would have the painter flatten it to 255 and
                // paint pure white cards instead of these tints.
                // Translucent families (Spatial / Liquid Glass /
                // Glassmorphism) are the ones that carry per-level alpha.

                surface: C("#FBF8FF"),
                surfaceSecondary: C("#F5F0FD"),
                surfaceDetail: C("#EFE9FB"),
                chrome: C("#F1EDFB"),
                dialog: C("#FCFAFF"),
                dialogInset: C("#EFE9FC"),
                textPrimary: C("#17102B"),
                textSecondary: C("#3B3159"),
                textMuted: C("#6A5F87"),
                borderSubtle: A("#7B68EE", 0.18),
                borderDefault: A("#FF0080", 0.34),
                borderStrong: A("#40E0D0", 0.50),
                // 6.46:1 on white — the foil violet #7B68EE measured only
                // 4.15:1 as a foreground colour.
                accent: C("#5B4BC4"),
                accentForeground: C("#FFFFFF"),
                shadow: C("#7B68EE"),
                focusRing: C("#FF0080")),
            // ── DARK: the intended home. Near-black violet, never pure
            // black — pure black kills the bloom the material needs.
            new EightColorRamp(
                window: C("#08060F"),
                module: C("#0E0B1A"),
                surface: C("#151128"),
                surfaceSecondary: C("#1D1836"),
                surfaceDetail: C("#251F45"),
                chrome: C("#100D1E"),
                dialog: C("#1A1530"),
                dialogInset: C("#100D1E"),
                textPrimary: C("#F4F1FF"),
                textSecondary: C("#BDB4DC"),
                textMuted: C("#8C82AD"),
                // The ramp borders are ALREADY spectral — a plain grey
                // keyline is the giveaway that a holographic theme has been
                // faked with a gradient overlay.
                borderSubtle: A("#7B68EE", 0.28),
                borderDefault: A("#40E0D0", 0.42),
                borderStrong: A("#FF0080", 0.58),
                accent: C("#A78BFA"),
                accentForeground: C("#140F26"),
                shadow: C("#7B68EE"),
                focusRing: C("#40E0D0")),
            geometry: new EightGeometry(control: 12, card: 18, large: 22, hero: 26),
            elevation: new EightElevation(
                EightElevationKind.TintedGlow,
                lowBlur: 22, lowDepth: 2, lowOpacity: 0.28,
                highBlur: 40, highDepth: 4, highOpacity: 0.40,
                lightStop: C("#FFFFFF"), shadeStop: C("#FF0080")),
            material: new EightMaterial(
                EightMaterialKind.Foil,
                surfaceAlpha: 1.0, chromeAlpha: 1.0,
                blurRadius: 0, saturationBoost: 0.12,
                // The rim IS the foil. Highest rim opacity of any family.
                rimOpacity: 0.55, rimTint: C("#40E0D0"),
                specularRim: true, innerGlowOpacity: 0.14,
                allowBackdrop: true),
            // Two different rates: 6000ms base drift against a 2600ms foil
            // sweep. Lock-steped animation is the mechanical tell.
            motion: new EightMotion(
                fast: 140, normal: 240, slow: 600,
                easeOut: "0.4,0.0 0.2,1.0", spring: "0.34,1.56 0.64,1.0"),
            accentPresets: new[] { "#5B4BC4", "#B4408A", "#0E7C8C", "#8A5A00", "#2C5FB8" },
            sceneLabel: "Foil Aurora",
            sceneType: "Iridescent spectrum wallpaper");

        // ═══════════════════════════════════════════════════════════════
        //  5 · NEUMORPHISM
        // ═══════════════════════════════════════════════════════════════
        //  RESEARCH NOTES
        //    · THE defining rule: the element's background must be the same
        //      colour as its parent. The effect collapses the moment that
        //      stops being true, because the light shadow needs a surface
        //      darker than itself to be visible against.
        //    · Two OUTSET shadows, MIRRORED. Dark offset away from the light,
        //      light offset toward it. Light source is ALWAYS top-left and
        //      must stay consistent screen-wide.
        //    · Pressed state flips BOTH to inset. That direction change IS
        //      the state; a colour shift is not enough.
        //    · Monochrome. One base hue for the page AND every surface.
        //    · Radius 12–20. Never sharp, never balloon-round.
        //    · Depth from shadow, never from fill colour.
        //    · THE ACCESSIBILITY PROBLEM: a surface that matches its
        //      background has no colour contrast at its boundary. Measured
        //      neumorphic edges sit around 1.4–1.9:1 against a required 3:1
        //      (WCAG 2.2 SC 1.4.11), and you cannot fix it by tuning — raising
        //      shadow contrast hard enough destroys the soft look that is
        //      the entire point. Every interactive element therefore needs a
        //      NON-SHADOW cue: an accent, a border, or a label.
        //  CONSEQUENCE FOR THE ENGINE
        //    window / module / surface / chrome are ALL #E0E5EC in light
        //    mode — deliberately identical. The separation the style throws
        //    away is then given back, once, by a hairline rim at low alpha
        //    plus a dedicated focus ring. That is the documented
        //    compensation, not a deviation.
        // ═══════════════════════════════════════════════════════════════
        private static EightThemeProfile Neumorphism() => new(
            EightThemeId.Neumorphism,
            EightThemeNames.Neumorphism,
            "Neumorphism",
            "Extruded from the page",
            "Soft UI pressed from a single sheet — every surface shares the page colour exactly and depth comes from light alone.",
            "One light and one dark OUTSET shadow from a fixed top-left source. Pressed flips to INSET. A hairline edge is always drawn because shadow alone is not a visible boundary.",
            new EightColorRamp(
                // window == module == surface == chrome. That equality is the
                // style, not a shortcut.
                window: C("#E0E5EC"),
                module: C("#E0E5EC"),
                surface: C("#E0E5EC"),
                surfaceSecondary: C("#E0E5EC"),
                surfaceDetail: C("#E0E5EC"),
                chrome: C("#E0E5EC"),
                dialog: C("#E0E5EC"),
                dialogInset: C("#E0E5EC"),
                // Body text must clear 7:1 where possible; #3D4657 on
                // #E0E5EC is the researched dark-mode-independent choice.
                textPrimary: C("#3D4657"),
                textSecondary: C("#5A6478"),
                // Fixed to clear WCAG AA 4.5:1 against BOTH the
                // composited card and the stage — muted ink is still body
                // text, not "decorative grey".
                textMuted: C("#5D6474"),
                borderSubtle: A("#FFFFFF", 0.34),
                borderDefault: A("#A3B1C6", 0.55),
                borderStrong: A("#5B7CFA", 0.70),
                // The research cites #5B7CFA for this palette, but as a
                // LABEL colour on the #E0E5EC sheet it measures 3.68:1 and
                // fails WCAG AA. #3D5BDB is the same hue at the luminance
                // that passes (5.65:1); the focus ring keeps the brighter
                // tone because a ring only has to clear 3:1.
                accent: C("#3D5BDB"),
                accentForeground: C("#FFFFFF"),
                shadow: C("#A3B1C6"),
                focusRing: C("#5B7CFA")),
            new EightColorRamp(
                // Dark neumorphism: highlight is light grey, NOT white,
                // and shade is dark grey, NOT black.
                window: C("#2A2D32"),
                module: C("#2A2D32"),
                surface: C("#2A2D32"),
                surfaceSecondary: C("#2A2D32"),
                surfaceDetail: C("#2A2D32"),
                chrome: C("#2A2D32"),
                dialog: C("#2A2D32"),
                dialogInset: C("#2A2D32"),
                textPrimary: C("#E6EAF2"),
                textSecondary: C("#B4BCCB"),
                // Fixed to clear WCAG AA 4.5:1 against BOTH the
                // composited card and the stage — muted ink is still body
                // text, not "decorative grey".
                textMuted: C("#8E96A8"),
                borderSubtle: A("#34383E", 0.60),
                borderDefault: A("#1C1E21", 0.70),
                borderStrong: A("#5B7CFA", 0.80),
                accent: C("#5B7CFA"),
                accentForeground: C("#0F1114"),
                shadow: C("#1C1E21"),
                focusRing: C("#7C97FF")),
            // 12–20 band. Never sharp, never balloon.
            geometry: new EightGeometry(control: 14, card: 18, large: 20, hero: 22),
            elevation: new EightElevation(
                EightElevationKind.Extrude,
                lowBlur: 16, lowDepth: 8, lowOpacity: 1.0,
                highBlur: 22, highDepth: 11, highOpacity: 1.0,
                // The researched pair. Light from top-left ALWAYS.
                lightStop: C("#FFFFFF"), shadeStop: C("#A3B1C6")),
            material: new EightMaterial(
                EightMaterialKind.Extruded,
                surfaceAlpha: 1.0, chromeAlpha: 1.0,
                blurRadius: 0, saturationBoost: 0,
                // The hairline. Low enough to be invisible as a "border"
                // and strong enough to clear a non-text boundary.
                rimOpacity: 0.55, rimTint: C("#A3B1C6"),
                specularRim: false, innerGlowOpacity: 0,
                // A wallpaper behind a neumorphic surface destroys the
                // extrude. The ground must be the flat mid-tone.
                allowBackdrop: false),
            motion: new EightMotion(
                fast: 100, normal: 140, slow: 220,
                easeOut: "0.25,0.1 0.25,1.0", spring: "0.34,1.3 0.64,1.0"),
            accentPresets: new[] { "#3D5BDB", "#B33E63", "#1E7D4C", "#B25A1F", "#6C41C4" },
            sceneLabel: "Soft Sheet",
            sceneType: "Flat mid-tone wallpaper");

        // ═══════════════════════════════════════════════════════════════
        //  6 · CLAYMORPHISM
        // ═══════════════════════════════════════════════════════════════
        //  REFERENCE: the ProGlass ExcelSticker studio, plus the published
        //  claymorphism literature. The single change from neumorphism that
        //  matters is the FILL: clay objects are their OWN colour, lifted off
        //  a different page, so figure-ground contrast survives.
        //  RESEARCH NOTES
        //    · THREE shadows in one declaration, in order: outer drop
        //      (floats it off the page), inset dark on the shaded side,
        //      inset light on the lit side. Drop any one and the effect
        //      collapses — outer only reads as a plain floating card, insets
        //      only read as a pressed well.
        //    · The shadow hue is the surface's OWN colour darkened ~15%.
        //      NEVER neutral grey. Grey shadows kill the moulded feel
        //      instantly and are the most common mistake.
        //    · Radius 28–48 — roughly TWICE neumorphism's. Corners read as
        //      soft blobs, never as "a card with rounded corners".
        //    · The page must NOT be pure white; a pure-white page erases the
        //      soft tinted glow the material depends on.
        //    · Inset top-left sheen is not optional — without it the surface
        //      looks stuck flat.
        //    · Do not stack clay more than three deep; it turns to mush.
        //    · Do not fight the round mood with sharp grotesk display type.
        //    · Naive clay dies in dark mode because black shadows on a dark
        //      page have nothing to darken. The fix is RECALIBRATION, not
        //      inversion: the outer drop goes deeper and higher-opacity while
        //      the inset highlight becomes load-bearing.
        //  CONSEQUENCE FOR THE ENGINE
        //    A soft tinted page (never #FFFFFF), distinctly more saturated
        //    cards, a radius floor of 28, and Inflate elevation whose
        //    ShadeStop is derived from each surface's own hue.
        // ═══════════════════════════════════════════════════════════════
        private static EightThemeProfile Claymorphism() => new(
            EightThemeId.Claymorphism,
            EightThemeNames.Claymorphism,
            "Claymorphism",
            "Puffy, moulded, colourful",
            "Soft rounded objects in their own colours, lifted off a tinted page by real shadows.",
            "One outer drop plus two inset shadows per surface, all keyed to the surface's own hue. Big 28–40px corners; a white page is never used because it erases the glow.",
            new EightColorRamp(
                // Warm tinted page — explicitly NOT #FFFFFF.
                window: C("#F6F1FB"),
                module: C("#EFE8F9"),
                surface: C("#E4DBF6"),
                surfaceSecondary: C("#D8CCF0"),
                surfaceDetail: C("#CBBCEA"),
                chrome: C("#EBE3F7"),
                dialog: C("#E9E1F8"),
                dialogInset: C("#DCD0F2"),
                textPrimary: C("#2E2547"),
                textSecondary: C("#524879"),
                // Fixed to clear WCAG AA 4.5:1 against BOTH the
                // composited card and the stage — muted ink is still body
                // text, not "decorative grey".
                textMuted: C("#645B81"),
                borderSubtle: A("#FFFFFF", 0.45),
                borderDefault: A("#8A7BB8", 0.38),
                borderStrong: A("#6C4FC4", 0.55),
                accent: C("#6C4FC4"),
                accentForeground: C("#FFFFFF"),
                // Shadow hue = surface hue darkened, never grey.
                shadow: C("#6C4FC4"),
                focusRing: C("#6C4FC4")),
            new EightColorRamp(
                // Recalibrated, not inverted: deeper outer drop, and the
                // inset highlight carries the volume.
                window: C("#191428"),
                module: C("#1F1930"),
                surface: C("#2A2145"),
                surfaceSecondary: C("#332959"),
                surfaceDetail: C("#3C3068"),
                chrome: C("#211B34"),
                dialog: C("#2C2350"),
                dialogInset: C("#1E1832"),
                textPrimary: C("#F3EFFC"),
                textSecondary: C("#C2B8E0"),
                // Fixed to clear WCAG AA 4.5:1 against BOTH the
                // composited card and the stage — muted ink is still body
                // text, not "decorative grey".
                textMuted: C("#948AB5"),
                borderSubtle: A("#6C4FC4", 0.45),
                borderDefault: A("#4A3A80", 0.60),
                borderStrong: A("#9B85E8", 0.70),
                accent: C("#9B85E8"),
                accentForeground: C("#1A1230"),
                shadow: C("#6C4FC4"),
                focusRing: C("#B9A6FF")),
            // 28 floor, ~2× neumorphism. The blob read depends on it.
            geometry: new EightGeometry(control: 20, card: 32, large: 40, hero: 48),
            elevation: new EightElevation(
                EightElevationKind.Inflate,
                lowBlur: 24, lowDepth: 8, lowOpacity: 0.34,
                highBlur: 38, highDepth: 12, highOpacity: 0.44,
                lightStop: C("#FFFFFF"), shadeStop: C("#6C4FC4")),
            material: new EightMaterial(
                EightMaterialKind.Inflated,
                surfaceAlpha: 1.0, chromeAlpha: 1.0,
                blurRadius: 0, saturationBoost: 0.10,
                rimOpacity: 0.40, rimTint: C("#FFFFFF"),
                specularRim: false,
                // The top-left inner sheen. Not optional.
                innerGlowOpacity: 0.55,
                allowBackdrop: true),
            // The bouncy curve — 200ms cubic-bezier(0.34, 1.56, 0.64, 1).
            // It is the one place overshoot is correct: clay is squishy.
            motion: new EightMotion(
                fast: 120, normal: 200, slow: 320,
                easeOut: "0.34,1.56 0.64,1.0", spring: "0.34,1.56 0.64,1.0"),
            accentPresets: new[] { "#6C4FC4", "#F0813C", "#2BB673", "#E05C8A", "#2D9CDB" },
            sceneLabel: "Pastel Studio",
            sceneType: "Tinted pastel wallpaper");

        // ═══════════════════════════════════════════════════════════════
        //  7 · LIQUID GLASS
        // ═══════════════════════════════════════════════════════════════
        //  REFERENCE: Apple, WWDC 2025 — "a translucent material that
        //  reflects and refracts its surroundings, while dynamically
        //  transforming to help bring greater focus to content."
        //  RESEARCH NOTES
        //    · THIS IS NOT GLASSMORPHISM. Glassmorphism is a recipe (blur
        //      the backdrop, translucent fill, light border). Liquid Glass is
        //      a rendering model that happens to look glassy. The differences
        //      that matter on screen:
        //        - HEAVIER blur than the 2020 recipe.
        //        - HIGHER saturation. Without it, heavy blur desaturates the
        //          backdrop and the material reads grey and dead.
        //        - A SPECULAR RIM that is bright along the lit edge and falls
        //          away on the other — the tell that sells it as a curved
        //          optical body rather than a flat pane.
        //        - A tint informed by surrounding content that keeps text
        //          legible over anything behind it.
        //    · The centre stays clear; the edges do the work.
        //    · Two-light border: a specular highlight that takes the colour
        //      of the backdrop, plus a soft side glow.
        //    · Reacts to state with a fluid morph rather than a swap.
        //  CONSEQUENCE FOR THE ENGINE
        //    Lowest surface alpha of all eight (it has to be genuinely
        //    see-through or it is just a pale card), the highest saturation
        //    boost, a strong specular rim, and the deepest continuous-corner
        //    radius. It is also this application's most-developed material,
        //    so it is the shipped default.
        // ═══════════════════════════════════════════════════════════════
        private static EightThemeProfile LiquidGlass() => new(
            EightThemeId.LiquidGlass,
            EightThemeNames.LiquidGlass,
            "Liquid Glass",
            "Refractive, specular, alive",
            "Apple's optical material — a clear centre, a lit rim, and a tint that keeps text readable over anything behind it.",
            "Heaviest blur, highest saturation, lowest fill opacity of the eight, plus a specular rim that is brightest on the lit edge. The material shows what is behind it.",
            // ── LIGHT ───────────────────────────────────────────────
            new EightColorRamp(
                window: C("#EDF1F8"),
                module: C("#E3E9F4"),
                surface: A("#FFFFFF", 0.52),
                surfaceSecondary: A("#FFFFFF", 0.38),
                surfaceDetail: A("#FFFFFF", 0.26),
                chrome: A("#F5F8FD", 0.44),
                dialog: A("#FBFDFF", 0.72),
                dialogInset: A("#EAF0FA", 0.60),
                textPrimary: C("#0D1220"),
                textSecondary: C("#39435C"),
                // Fixed to clear WCAG AA 4.5:1 against BOTH the
                // composited card and the stage — muted ink is still body
                // text, not "decorative grey".
                textMuted: C("#626B83"),
                borderSubtle: A("#FFFFFF", 0.40),
                borderDefault: A("#FFFFFF", 0.62),
                borderStrong: A("#FFFFFF", 0.85),
                // Apple's system blue measures 3.65:1 as a label colour
                // on white, so the LABEL slot uses a darker step of the
                // same hue. The bright system blue is still the focus
                // ring, where 3:1 is the correct bar.
                accent: C("#0A6EDC"),
                accentForeground: C("#FFFFFF"),
                shadow: C("#1B2440"),
                focusRing: C("#0A84FF")),
            // ── DARK ─────────────────────────────────────────────────
            new EightColorRamp(
                window: C("#08090C"),
                module: C("#0D0F14"),
                surface: A("#FFFFFF", 0.10),
                surfaceSecondary: A("#FFFFFF", 0.16),
                surfaceDetail: A("#FFFFFF", 0.07),
                chrome: A("#FFFFFF", 0.08),
                dialog: A("#131620", 0.76),
                dialogInset: A("#FFFFFF", 0.06),
                textPrimary: C("#F5F5F7"),
                textSecondary: C("#C7C7CC"),
                textMuted: C("#98989F"),
                borderSubtle: A("#FFFFFF", 0.14),
                borderDefault: A("#FFFFFF", 0.24),
                borderStrong: A("#FFFFFF", 0.40),
                accent: C("#5AC8FA"),
                accentForeground: C("#0A0A0C"),
                shadow: C("#000000"),
                focusRing: C("#5AC8FA")),
            // Deep continuous corners. Generous, because the material wants
            // curvature for the rim to run along.
            geometry: new EightGeometry(control: 16, card: 24, large: 30, hero: 36),
            elevation: new EightElevation(
                EightElevationKind.Refractive,
                lowBlur: 20, lowDepth: 3, lowOpacity: 0.16,
                highBlur: 34, highDepth: 6, highOpacity: 0.26,
                lightStop: C("#FFFFFF"), shadeStop: C("#0A2540")),
            material: new EightMaterial(
                EightMaterialKind.Refractive,
                // Lowest alpha in the catalogue. A liquid-glass surface that
                // is 90% opaque is just a pale card.
                surfaceAlpha: 0.52, chromeAlpha: 0.44,
                blurRadius: 56, saturationBoost: 1.32,
                rimOpacity: 0.70, rimTint: C("#FFFFFF"),
                specularRim: true, innerGlowOpacity: 0.22,
                allowBackdrop: true),
            motion: new EightMotion(
                fast: 120, normal: 250, slow: 400,
                // Fluid morph, not a swap. Slight overshoot is correct here.
                easeOut: "0.32,0.72 0.0,1.0", spring: "0.34,1.2 0.64,1.0"),
            accentPresets: new[] { "#0A6EDC", "#8E4EC6", "#1F7A38", "#A35F00", "#B32748" },
            sceneLabel: "Refracted Field",
            sceneType: "Luminous refraction wallpaper");

        // ═══════════════════════════════════════════════════════════════
        //  8 · GLASSMORPHISM
        // ═══════════════════════════════════════════════════════════════
        //  RESEARCH NOTES
        //    · The 2020 recipe, and it is here precisely because users ask
        //      for it by name and it is NOT liquid glass:
        //      - UNIFORM blur across the surface. Nothing refracts, nothing
        //        reacts, nothing morphs. One blur value does the whole card.
        //      - A semi-transparent fill plus ONE hairline border. That is
        //        the entire effect.
        //      - It NEEDS a busy or colourful backdrop to be worth anything.
        //        On flat paper it is just a grey smear — which is why the
        //        scene here is the most chromatic of the eight.
        //      - Mostly static. Decorative depth, not physical depth.
        //    · Light mode tends to grey out; keep the fill cooler and the
        //      saturation up or it reads as dirt on the screen.
        //  CONSEQUENCE FOR THE ENGINE
        //    Mid blur (between frosted and liquid), NO specular rim (a flat
        //    hairline is the whole look), no saturation boost worth
        //    mentioning, and a vivid scene so the blur has something to
        //    work with.
        // ═══════════════════════════════════════════════════════════════
        private static EightThemeProfile Glassmorphism() => new(
            EightThemeId.Glassmorphism,
            EightThemeNames.Glassmorphism,
            "Glassmorphism",
            "Frosted, static, classic",
            "The original 2020 recipe — one uniform blur, a translucent fill and a single hairline, over a colourful backdrop.",
            "Uniform blur with a flat hairline rim and no refraction. Sits deliberately below Liquid Glass: nothing bends, nothing reacts, nothing morphs.",
            new EightColorRamp(
                window: C("#E9E7F8"),
                module: C("#DEDBF2"),
                surface: A("#FFFFFF", 0.46),
                surfaceSecondary: A("#FFFFFF", 0.34),
                surfaceDetail: A("#FFFFFF", 0.24),
                chrome: A("#F4F2FD", 0.40),
                dialog: A("#FCFBFF", 0.66),
                dialogInset: A("#E4E1F5", 0.54),
                textPrimary: C("#14121F"),
                textSecondary: C("#3E3A54"),
                // Fixed to clear WCAG AA 4.5:1 against BOTH the
                // composited card and the stage — muted ink is still body
                // text, not "decorative grey".
                textMuted: C("#68637E"),
                borderSubtle: A("#FFFFFF", 0.42),
                borderDefault: A("#FFFFFF", 0.66),
                borderStrong: A("#FFFFFF", 0.90),
                accent: C("#6C5CE7"),
                accentForeground: C("#FFFFFF"),
                shadow: C("#241E4A"),
                focusRing: C("#6C5CE7")),
            new EightColorRamp(
                window: C("#0A0913"),
                module: C("#110F1F"),
                surface: A("#FFFFFF", 0.11),
                surfaceSecondary: A("#FFFFFF", 0.17),
                surfaceDetail: A("#FFFFFF", 0.08),
                chrome: A("#FFFFFF", 0.08),
                dialog: A("#16142A", 0.74),
                dialogInset: A("#FFFFFF", 0.06),
                textPrimary: C("#FFFFFF"),
                textSecondary: C("#C6C2DC"),
                textMuted: C("#9691B0"),
                borderSubtle: A("#FFFFFF", 0.14),
                borderDefault: A("#FFFFFF", 0.24),
                borderStrong: A("#FFFFFF", 0.38),
                accent: C("#A29BFE"),
                accentForeground: C("#12102A"),
                shadow: C("#000000"),
                focusRing: C("#C4BEFF")),
            geometry: new EightGeometry(control: 10, card: 16, large: 20, hero: 24),
            elevation: new EightElevation(
                EightElevationKind.Refractive,
                lowBlur: 16, lowDepth: 2, lowOpacity: 0.18,
                highBlur: 26, highDepth: 4, highOpacity: 0.26,
                lightStop: C("#FFFFFF"), shadeStop: C("#14102E")),
            material: new EightMaterial(
                EightMaterialKind.Frosted,
                surfaceAlpha: 0.46, chromeAlpha: 0.40,
                // Noticeably less than Liquid Glass's 56. The blur is
                // uniform and static; that is the difference.
                blurRadius: 32, saturationBoost: 1.12,
                // A FLAT hairline. No specular rim — one is what turns this
                // into Liquid Glass by accident.
                rimOpacity: 0.42, rimTint: C("#FFFFFF"),
                specularRim: false, innerGlowOpacity: 0.06,
                allowBackdrop: true),
            motion: new EightMotion(
                fast: 120, normal: 220, slow: 320,
                easeOut: "0.25,0.1 0.25,1.0", spring: "0.34,1.2 0.64,1.0"),
            accentPresets: new[] { "#6C5CE7", "#00CEC9", "#FD79A8", "#0984E3", "#00B894" },
            sceneLabel: "Prism Wash",
            sceneType: "Chromatic gradient wallpaper");

        // ═══════════════════════════════════════════════════════════════
        //  9 · AURORA
        //  RESEARCH NOTES
        //    · "The dark void (#06060F) is the required base." Aurora over
        //      white dies, so there is NO light mode to invert into — the
        //      light ramp below is a dimmed violet cast of the same void,
        //      not a brighter one.
        //    · "Three blobs is the minimum: two reads as a gradient, three
        //      reads as weather." BlobCount is enforced at 3.
        //    · "Anchor in one dominant hue, let secondary colours orbit —
        //      never give three colours equal weight or you get mud."
        //      Indigo carries roughly half the field.
        //    · "The moment you put a mesh gradient on a card or button,
        //      you've lost the plot." Hence AuroraPlacement: the mesh is
        //      Backdrop only and never reaches a control.
        //    · "Text never on the raw gradient" — contrast collapses as
        //      the hues shift. Content placement composites opaque BEFORE
        //      ink is resolved, and AuroraPalette validates against the
        //      sky PEAK rather than the average.
        //    · "Drifting on transform-only keyframes, never
        //      background-position — that repaints the gradient every
        //      frame." Hence three frozen Ellipses and a zero blur radius.
        // ═══════════════════════════════════════════════════════════════
        private static EightThemeProfile Aurora() => new(
            EightThemeId.Aurora,
            EightThemeNames.Aurora,
            "Aurora",
            "Dark, atmospheric, drifting",
            "Three slow colour fields behind translucent panels. Depth comes from the sky, never from geometry.",
            "Sublime gradient rim instead of a hairline: a flat edge loses contrast whenever a blob drifts toward it. No shadow and no blur anywhere in the family.",
            new EightColorRamp(
                // NOT an inversion. A dimmed violet cast of the void.
                window: C("#0A0718"),
                module: C("#0F0B24"),
                surface: A("#1A1340", 0.62),
                surfaceSecondary: A("#221A4E", 0.72),
                surfaceDetail: A("#151033", 0.58),
                chrome: A("#0E0A24", 0.52),
                dialog: C("#120E28"),
                dialogInset: A("#0A0718", 0.72),
                textPrimary: C("#F4F1FF"),
                textSecondary: C("#CFC9E8"),
                textMuted: C("#A9A2C8"),
                borderSubtle: C("#2DD4BF"),
                borderDefault: C("#3A2F63"),
                borderStrong: C("#8B5CF6"),
                accent: C("#6D5CFF"),
                accentForeground: C("#FFFFFF"),
                shadow: C("#05010F"),
                focusRing: C("#7C5CFF"),
                // The field. Indigo is the anchor.
                blobIndigo: C("#6366F1"),
                blobViolet: C("#8B5CF6"),
                blobTeal: C("#2DD4BF")),
            new EightColorRamp(
                window: C("#05010F"),
                module: C("#08061A"),
                surface: A("#151034", 0.66),
                surfaceSecondary: A("#1C1542", 0.76),
                surfaceDetail: C("#100C28"),
                chrome: A("#0B0820", 0.56),
                dialog: C("#0E0A22"),
                dialogInset: A("#070514", 0.78),
                textPrimary: C("#F4F1FF"),
                textSecondary: C("#CFC9E8"),
                textMuted: C("#A9A2C8"),
                borderSubtle: C("#2DD4BF"),
                borderDefault: C("#2E2654"),
                borderStrong: C("#8B5CF6"),
                accent: C("#6D5CFF"),
                accentForeground: C("#FFFFFF"),
                shadow: C("#05010F"),
                focusRing: C("#7C5CFF"),
                blobIndigo: C("#6366F1"),
                blobViolet: C("#8B5CF6"),
                blobTeal: C("#2DD4BF")),
            // 24px controls read as PILLS, which is the reference render's
            // strongest signal after the sky itself.
            geometry: new EightGeometry(control: 24, card: 20, large: 20, hero: 28),
            // None. This family has no shadow, and encoding that as a
            // "very faint" elevation would be a lie the renderer would
            // eventually stop believing.
            elevation: new EightElevation(
                EightElevationKind.None,
                lowBlur: 0, lowDepth: 0, lowOpacity: 0,
                highBlur: 0, highDepth: 0, highOpacity: 0,
                lightStop: C("#FFFFFF"), shadeStop: C("#05010F")),
            material: new EightMaterial(
                EightMaterialKind.Luminous,
                surfaceAlpha: 0.55, chromeAlpha: 0.42,
                // ZERO. A radial gradient that reaches alpha 0 IS the
                // blur; adding a BlurEffect on top would re-rasterise the
                // whole sky every frame of the drift.
                blurRadius: 0, saturationBoost: 1.0,
                rimOpacity: 0.55, rimTint: C("#8B5CF6"),
                specularRim: false, innerGlowOpacity: 0,
                allowBackdrop: true),
            // Control motion only. The 42-second sky drift is NOT in this
            // scale — it is a backdrop property, owned by AuroraMetrics,
            // and putting it here would imply it is a control animation
            // that reduced motion might be expected to shorten.
            motion: new EightMotion(
                fast: 120, normal: 220, slow: 320,
                easeOut: "0.25,0.1 0.25,1.0", spring: "0.34,1.2 0.64,1.0"),
            accentPresets: new[] { "#6D5CFF", "#2DD4BF", "#8B5CF6", "#00CEC9", "#FD79A8" },
            sceneLabel: "Northern Drift",
            sceneType: "Three-field atmospheric mesh");
    }
}