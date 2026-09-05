// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/ThemeStudioSettings.cs
// PROJECT: ProGlassAutomation
// MODULE: Theme Studio — centralized appearance customization model
//
// 2026-09-02 — Theme Studio (master prompt). Every property is 0–100
// with an explicit UI meaning; ThemeStudioService maps each range to
// concrete WPF resource values. Persisted as AppearanceSettings.Studio
// inside the existing config.json — no second settings database.
//
// UI meaning of each slider (0 = left, 100 = right):
//   Frostness          0 = crystal/clear ... 100 = dense frosted
//   Transparency       0 = solid ... 100 = maximum transparency
//   BlurIntensity      0 = crisp edges ... 100 = heavy glass blur
//   GlassTint          0 = neutral ... 100 = accent-tinted glass
//   CornerRoundness    0 = square ... 50 = balanced ... 100 = pill
//   CardPadding        0 = tight ... 100 = generous
//   ShadowIntensity    0 = no shadow ... 100 = strong dramatic
//   ShadowSoftness     0 = hard edge ... 100 = very soft
//   ShadowDepth        0 = flat ... 100 = deep lift
//   BorderVisibility   0 = borderless ... 100 = strong keyline
//   BorderOpacity      0 = invisible border ... 100 = opaque border
//   BorderThickness    0 = hairline ... 100 = heavy frame
//   BackgroundVisibility 0 = hidden background ... 100 = solid
//   BackgroundGlow     0 = none ... 100 = strong ambient glow
//   HighlightStrength  0 = none ... 100 = strong top highlight
//   AnimationIntensity 0 = minimal ... 100 = lively
//
// 2026-09-05 — RICH COLOUR SYSTEM (master request). Two new axes:
//   ColorRichness    0 = muted pastel ... 50 = family hue ... 100 =
//                    deep saturated rich (saturation ×0.45–×1.85 plus a
//                    gentle depth lift; true greys never go muddy)
//   TextStrength     0 = deliberately soft ... 55 = family ink ... 100 =
//                    maximum-contrast ink, stage-aware (bright on dark
//                    stages, deep on light stages). An automatic
//                    CONTRAST GUARD forces 100-equivalent ink whenever
//                    transparency pushes cards to near-invisible, so the
//                    "text outside cards disappears" case is impossible.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>
    /// 0–100 appearance profile for one visual family. Each existing
    /// family (Normal, Monet, Professional, iOS Liquid Glass, Ultra
    /// Transparent, Ocean) has its own <see cref="DefaultsFor"/> profile
    /// so "Reset Current Theme" restores exactly that family's look.
    /// </summary>
    public sealed class ThemeStudioSettings
    {
        public double Frostness { get; set; } = 50;
        public double Transparency { get; set; } = 35;
        public double BlurIntensity { get; set; } = 50;
        public double GlassTint { get; set; } = 40;

        // 2026-09-05 rich colour system. Defaults keep a pre-upgrade
        // config visually identical (50 ≈ no richness shift, 55 ≈
        // family ink untouched) — old saved profiles upgrade in place.
        public double ColorRichness { get; set; } = 50;
        public double TextStrength { get; set; } = 55;

        /// <summary>0 = flat frost … 100 = strong WATER-FILL: card and
        /// glass surfaces become liquid crystal — bright specular sheen
        /// at the top edge, crystal body, a waterline glint, and denser
        /// colour settled at the bottom like filled water (2026-09-05).
        /// Default 0 keeps every family exactly as designed.</summary>
        public double WaterFlow { get; set; } = 0;

        public double CornerRoundness { get; set; } = 50;
        public double CardPadding { get; set; } = 50;

        public double ShadowIntensity { get; set; } = 50;
        public double ShadowSoftness { get; set; } = 50;
        public double ShadowDepth { get; set; } = 50;

        public double BorderVisibility { get; set; } = 50;
        public double BorderOpacity { get; set; } = 60;
        public double BorderThickness { get; set; } = 35;

        public double BackgroundVisibility { get; set; } = 70;
        public double BackgroundGlow { get; set; } = 30;
        public double HighlightStrength { get; set; } = 40;

        public double AnimationIntensity { get; set; } = 50;

        /// <summary>
        /// True once the user customizes anything (or applies a preset).
        /// False = the family's own defaults rule (no studio layer).
        /// </summary>
        public bool IsCustomized { get; set; }

        /// <summary>Clones the profile (independent copy for edit/reset).</summary>
        public ThemeStudioSettings Clone() =>
            (ThemeStudioSettings)MemberwiseClone();

        /// <summary>True when every property matches another profile.</summary>
        public bool Matches(ThemeStudioSettings other)
        {
            if (other == null) return false;
            return Frostness == other.Frostness
                && Transparency == other.Transparency
                && BlurIntensity == other.BlurIntensity
                && GlassTint == other.GlassTint
                && CornerRoundness == other.CornerRoundness
                && CardPadding == other.CardPadding
                && ShadowIntensity == other.ShadowIntensity
                && ShadowSoftness == other.ShadowSoftness
                && ShadowDepth == other.ShadowDepth
                && BorderVisibility == other.BorderVisibility
                && BorderOpacity == other.BorderOpacity
                && BorderThickness == other.BorderThickness
                && BackgroundVisibility == other.BackgroundVisibility
                && BackgroundGlow == other.BackgroundGlow
                && HighlightStrength == other.HighlightStrength
                && AnimationIntensity == other.AnimationIntensity
                && ColorRichness == other.ColorRichness
                && TextStrength == other.TextStrength
                && WaterFlow == other.WaterFlow;
        }

        // ── Per-family defaults ──────────────────────────────────────
        // Glass families start transparent/frosted; solid families
        // start opaque with stronger borders. Values are the family's
        // "as designed" appearance expressed in studio space.

        public static ThemeStudioSettings DefaultsFor(string family)
        {
            var s = new ThemeStudioSettings();
            switch (family)
            {
                case VisualThemeEngine.LiquidGlass:
                    s.Frostness = 55; s.Transparency = 45;
                    s.BlurIntensity = 60; s.GlassTint = 45;
                    s.CornerRoundness = 50; s.CardPadding = 45;
                    s.ShadowIntensity = 55; s.ShadowSoftness = 70;
                    s.ShadowDepth = 35;
                    s.BorderVisibility = 55; s.BorderOpacity = 60;
                    s.BorderThickness = 30;
                    s.BackgroundVisibility = 60; s.BackgroundGlow = 50;
                    s.HighlightStrength = 55;
                    s.AnimationIntensity = 55;
                                        s.ColorRichness = 60; s.TextStrength = 60;
                                        s.WaterFlow = 45;
                    break;

                case VisualThemeEngine.UltraTransparent:
                    s.Frostness = 25; s.Transparency = 85;
                    s.BlurIntensity = 40; s.GlassTint = 25;
                    s.CornerRoundness = 40; s.CardPadding = 40;
                    s.ShadowIntensity = 35; s.ShadowSoftness = 55;
                    s.ShadowDepth = 20;
                    s.BorderVisibility = 70; s.BorderOpacity = 50;
                    s.BorderThickness = 25;
                    s.BackgroundVisibility = 30; s.BackgroundGlow = 60;
                    s.HighlightStrength = 60;
                    s.AnimationIntensity = 50;
                                        s.ColorRichness = 55; s.TextStrength = 85;
                                        s.WaterFlow = 75;
                    break;

                case VisualThemeEngine.Ocean:
                    s.Frostness = 60; s.Transparency = 40;
                    s.BlurIntensity = 55; s.GlassTint = 70;
                    s.CornerRoundness = 55; s.CardPadding = 45;
                    s.ShadowIntensity = 50; s.ShadowSoftness = 65;
                    s.ShadowDepth = 35;
                    s.BorderVisibility = 60; s.BorderOpacity = 55;
                    s.BorderThickness = 30;
                    s.BackgroundVisibility = 55; s.BackgroundGlow = 55;
                    s.HighlightStrength = 50;
                    s.AnimationIntensity = 50;
                                        s.ColorRichness = 70; s.TextStrength = 60;
                                        s.WaterFlow = 35;
                    break;

                case VisualThemeEngine.Monet:
                    s.Frostness = 40; s.Transparency = 50;
                    s.BlurIntensity = 45; s.GlassTint = 55;
                    s.CornerRoundness = 45; s.CardPadding = 45;
                    s.ShadowIntensity = 45; s.ShadowSoftness = 60;
                    s.ShadowDepth = 30;
                    s.BorderVisibility = 45; s.BorderOpacity = 45;
                    s.BorderThickness = 25;
                    s.BackgroundVisibility = 55; s.BackgroundGlow = 45;
                    s.HighlightStrength = 45;
                    s.AnimationIntensity = 50;
                                        s.ColorRichness = 75; s.TextStrength = 60;
                                        s.WaterFlow = 25;
                    break;

                case VisualThemeEngine.Professional:
                    s.Frostness = 15; s.Transparency = 15;
                    s.BlurIntensity = 15; s.GlassTint = 10;
                    s.CornerRoundness = 30; s.CardPadding = 55;
                    s.ShadowIntensity = 40; s.ShadowSoftness = 45;
                    s.ShadowDepth = 30;
                    s.BorderVisibility = 60; s.BorderOpacity = 70;
                    s.BorderThickness = 45;
                    s.BackgroundVisibility = 85; s.BackgroundGlow = 15;
                    s.HighlightStrength = 25;
                    s.AnimationIntensity = 30;
                                        s.ColorRichness = 45; s.TextStrength = 70;
                                        s.WaterFlow = 0;
                    break;

                // Industrial family (2026-09-05): plain ERP look — nearly
                // opaque, strong keyline, no water, restrained shadows.
                // All three shade layouts share the studio profile.
                case VisualThemeEngine.IndustrialWhite:
                case VisualThemeEngine.IndustrialGrey:
                case VisualThemeEngine.IndustrialDark:
                    s.Frostness = 5; s.Transparency = 5;
                    s.BlurIntensity = 10; s.GlassTint = 5;
                    s.CornerRoundness = 15; s.CardPadding = 60;
                    s.ShadowIntensity = 25; s.ShadowSoftness = 40;
                    s.ShadowDepth = 20;
                    s.BorderVisibility = 70; s.BorderOpacity = 65;
                    s.BorderThickness = 40;
                    s.BackgroundVisibility = 90; s.BackgroundGlow = 10;
                    s.HighlightStrength = 20;
                    s.AnimationIntensity = 35;
                    s.ColorRichness = 50; s.TextStrength = 70;
                    s.WaterFlow = 0;
                    break;

                default: // Normal
                    s.Frostness = 30; s.Transparency = 25;
                    s.BlurIntensity = 35; s.GlassTint = 25;
                    s.CornerRoundness = 40; s.CardPadding = 50;
                    s.ShadowIntensity = 45; s.ShadowSoftness = 55;
                    s.ShadowDepth = 35;
                    s.BorderVisibility = 55; s.BorderOpacity = 55;
                    s.BorderThickness = 35;
                    s.BackgroundVisibility = 75; s.BackgroundGlow = 25;
                    s.HighlightStrength = 35;
                    s.AnimationIntensity = 45;
                                        s.ColorRichness = 55; s.TextStrength = 55;
                                        s.WaterFlow = 0;
                    break;
            }
            s.IsCustomized = false;
            return s;
        }

        // ── Presets (centralized profiles, applied on top of ANY family) ──

        /// <summary>THE authoritative preset registry — the Settings VM
        /// combo lists exactly these names, in this order, and
        /// <see cref="Preset"/> implements every one of them (2026-09-05
        /// Phase-1 audit: keep ONE source so a listed preset can never
        /// again be a phantom that silently falls to the default arm —
        /// see F-03 in SETTINGS_MODULE_AUDIT.md).</summary>
        public static IReadOnlyList<string> PresetNames { get; } = new[]
        {
            "Crystal",
            "LightGlass",
            "Balanced",
            "Frosted",
            "DenseGlass",
            "RoyalRich",
        };

        public static ThemeStudioSettings Preset(string name)
        {
            var s = new ThemeStudioSettings();
            switch (name)
            {
                case "Crystal":           // 💎 clear, highly transparent
                    s.Frostness = 15; s.Transparency = 75;
                    s.BlurIntensity = 30; s.GlassTint = 20;
                    s.CornerRoundness = 35; s.CardPadding = 45;
                    s.ShadowIntensity = 25; s.ShadowSoftness = 50;
                    s.ShadowDepth = 15;
                    s.BorderVisibility = 65; s.BorderOpacity = 40;
                    s.BorderThickness = 20;
                    s.BackgroundVisibility = 35; s.BackgroundGlow = 55;
                    s.HighlightStrength = 60;
                    s.AnimationIntensity = 50;
                                        s.ColorRichness = 65; s.TextStrength = 80;
                                        s.WaterFlow = 65;
                    break;

                case "LightGlass":        // 🫧 airy, bright glass
                    s.Frostness = 35; s.Transparency = 60;
                    s.BlurIntensity = 45; s.GlassTint = 35;
                    s.CornerRoundness = 45; s.CardPadding = 45;
                    s.ShadowIntensity = 35; s.ShadowSoftness = 60;
                    s.ShadowDepth = 25;
                    s.BorderVisibility = 60; s.BorderOpacity = 50;
                    s.BorderThickness = 25;
                    s.BackgroundVisibility = 45; s.BackgroundGlow = 50;
                    s.HighlightStrength = 55;
                    s.AnimationIntensity = 55;
                                        s.ColorRichness = 60; s.TextStrength = 70;
                                        s.WaterFlow = 50;
                    break;

                case "Balanced":          // ❄️ modern default
                    s.Frostness = 50; s.Transparency = 40;
                    s.BlurIntensity = 55; s.GlassTint = 40;
                    s.CornerRoundness = 50; s.CardPadding = 50;
                    s.ShadowIntensity = 50; s.ShadowSoftness = 60;
                    s.ShadowDepth = 35;
                    s.BorderVisibility = 55; s.BorderOpacity = 55;
                    s.BorderThickness = 30;
                    s.BackgroundVisibility = 60; s.BackgroundGlow = 40;
                    s.HighlightStrength = 45;
                    s.AnimationIntensity = 50;
                                        s.ColorRichness = 60; s.TextStrength = 60;
                                        s.WaterFlow = 35;
                    break;

                case "Frosted":           // 🌫️ dense readable frost
                    s.Frostness = 80; s.Transparency = 30;
                    s.BlurIntensity = 75; s.GlassTint = 45;
                    s.CornerRoundness = 55; s.CardPadding = 50;
                    s.ShadowIntensity = 55; s.ShadowSoftness = 65;
                    s.ShadowDepth = 35;
                    s.BorderVisibility = 50; s.BorderOpacity = 50;
                    s.BorderThickness = 30;
                    s.BackgroundVisibility = 70; s.BackgroundGlow = 35;
                    s.HighlightStrength = 45;
                    s.AnimationIntensity = 45;
                                        s.ColorRichness = 55; s.TextStrength = 65;
                                        s.WaterFlow = 20;
                    break;

                case "RoyalRich":         // 👑 deep saturated 2026 look
                    s.Frostness = 50; s.Transparency = 30;
                    s.BlurIntensity = 55; s.GlassTint = 50;
                    s.CornerRoundness = 50; s.CardPadding = 50;
                    s.ShadowIntensity = 60; s.ShadowSoftness = 60;
                    s.ShadowDepth = 45;
                    s.BorderVisibility = 55; s.BorderOpacity = 60;
                    s.BorderThickness = 30;
                    s.BackgroundVisibility = 70; s.BackgroundGlow = 60;
                    s.HighlightStrength = 55;
                    s.AnimationIntensity = 50;
                    s.ColorRichness = 90; s.TextStrength = 75;
                                        s.WaterFlow = 40;
                    break;

                case "DenseGlass":        // 🧊 dense glass — max readability
                default:                  // unknown/persisted-legacy names
                                          // land here too, but the combo
                                          // itself only ever lists
                                          // PresetNames (no more phantoms).
                    s.Frostness = 90; s.Transparency = 15;
                    s.BlurIntensity = 70; s.GlassTint = 40;
                    s.CornerRoundness = 50; s.CardPadding = 50;
                    s.ShadowIntensity = 60; s.ShadowSoftness = 60;
                    s.ShadowDepth = 40;
                    s.BorderVisibility = 55; s.BorderOpacity = 65;
                    s.BorderThickness = 35;
                    s.BackgroundVisibility = 85; s.BackgroundGlow = 25;
                    s.HighlightStrength = 40;
                    s.AnimationIntensity = 40;
                                        s.ColorRichness = 50; s.TextStrength = 80;
                                        s.WaterFlow = 5;
                    break;
            }
            s.IsCustomized = true;
            return s;
        }
    }
}
