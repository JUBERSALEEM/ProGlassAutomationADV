// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Eight/EightThemePainter.cs
// PROJECT: ProGlassAutomation
// MODULE: Eight-theme engine — the painter (recipe → live WPF resources)
//
// This is the ONLY place in the eight-theme engine that constructs a WPF
// object. Everything happens at APPLY time, never in a type initialiser —
// the "themes crash the app on first paint" failure mode in this codebase
// came from brushes built in cctors.
//
// THE PAINTING RULES (learned the hard way, encoded once, applied eight
// times so none of the eight can break them independently):
//
//  1. A CARD MUST BE DISTINGUISHABLE FROM ITS STAGE. The iOS 26 light ramp
//     once painted window, module, elevated, popup, dialog AND both card
//     levels the same near-white; a card whose fill equals its stage and
//     whose border is an 8% hairline has NO visible boundary and the whole
//     page reads as one empty sheet. Every ramp here keeps at least one of
//     (fill step, border step) meaningful at every level.
//
//  2. WRITE TYPED RESOURCES. Every key this file publishes appears in
//     ManagedKeys with the SAME runtime type on every path. A key that is a
//     Thickness on one family and a Double on another throws
//     "BorderThickness must be a Thickness" inside a template inflator at
//     random, which is unrecoverable from a crash report.
//
//  3. NEVER THROW. An appearance change must degrade, not crash. Every
//     entry point is wrapped and a partial paint is always better than an
//     exception escaping into a command handler.
//
//  4. SHADOWS ARE BOUNDED. Blur <= 56 and depth <= 12 everywhere. A
//     DropShadowEffect is re-rasterised by the GPU on every repaint, and a
//     96px-blur shadow on a scrolling DataGrid row will cost frames.
//
//  5. GEOMETRY IS FAMILY DATA, NOT A CONSTANT. Corner radius is the single
//     loudest identity signal in a UI: a Swiss card at 0px and a clay card
//     at 32px read as different products before any colour is compared.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.Holo;
using ProGlassAutomation.Services.Theme.Neu;
using ProGlassAutomation.Services.Theme.Swiss;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ProGlassAutomation.Services.Theme.Eight
{
    /// <summary>Turns an <see cref="EightThemeProfile"/> into the resource
    /// dictionary the rest of the application already consumes. The rest of
    /// the app has no idea these eight families exist — it reads the same
    /// SemanticKeys vocabulary it always has.</summary>
    public static class EightThemePainter
    {
        // ── PUBLISHED KEY NAMES ───────────────────────────────────────
        // These are the semantic keys the shell, Settings, About, the data
        // grid and every module view already bind to. The engine only
        // re-publishes their VALUES; it never invents a new vocabulary.

        private static readonly string[] SurfaceKeys =
        {
            "Surface.Primary.BackgroundBrush", "Surface.Secondary.BackgroundBrush",
            "Surface.Detail.BackgroundBrush",
            "Surface.Primary.BorderBrush", "Surface.Secondary.BorderBrush",
            "Surface.Detail.BorderBrush",
            "Surface.Primary.CornerRadius", "Surface.Secondary.CornerRadius",
            "Surface.Detail.CornerRadius",
        };

        private static readonly string[] StageKeys =
        {
            "BackgroundBrush", "WindowBackgroundBrush", "ModuleBackgroundBrush",
            "SideMenuBackgroundBrush", "ElevatedSurfaceBrush", "PopupSurfaceBrush",
            "DialogSurfaceBrush", "DialogInsetBrush", "SidebarCardBrush",
        };

        private static readonly string[] InkKeys =
        {
            "PrimaryForegroundBrush", "SecondaryForegroundBrush",
            "MutedForegroundBrush", "TextPrimaryBrush", "TextSecondaryBrush",
        };

        private static readonly string[] RuleKeys =
        {
            // NOTE: DividerBrush / OutlineBrush / OutlineDefaultBrush are
            // deliberately ABSENT here, and that is a contract, not an
            // oversight. ThemeManager.ApplyAccent derives those three from
            // the live accent hue into the same application dictionary, and
            // this file's token list is merged into VisualThemeEngine's
            // ManagedKeys. Listing them would make ClearOverrides delete the
            // accent-derived keylines on every family switch — which is
            // precisely the "the by-default theme came out bluish" bug the
            // ManagedKeys comment has been guarding against since the
            // accent work landed. OutlineStrongBrush is not derived by
            // ApplyAccent, so it IS safe to own here.
            "OutlineStrongBrush",
        };

        private static readonly string[] RadiusKeys =
        {
            "CardCornerRadius", "CardCornerRadiusLarge", "ControlCornerRadius",
            "Radius.Small", "Radius.Medium", "Radius.Large", "Radius.Hero",
        };

        private static readonly string[] ShadowKeys =
        {
            "ShadowSoft", "ShadowElevated", "ShadowDeep",
            "DashCardShadowEffect", "DashSubtleShadowEffect",
            "DashInteractiveShadowEffect",
            "GlassShadowPanel", "GlassShadowSoft",
            "GlassShadowSoftButton", "GlassShadowSoftCheckBox",
            "GlassShadowSoftTextBox", "GlassShadowSoftToggle",
            "GlassShadowActive", "GlassShadowActiveButton",
        };

        private static readonly string[] LiquidKeys =
        {
            "LiquidGlassBrush", "LiquidGlassFrostedBrush", "LiquidCard",
            "LiquidOverlayBrush", "LiquidGlassSubtleBrush",
            "LiquidGlassStrongBrush", "LiquidEdgeBrush",
            "ShellLiquidBrush", "ShellLiquidBorderBrush", "ShellSheenBrush",
        };

        private static readonly string[] MotionKeys =
        {
            "MotionEaseOutKeySpline", "MotionSpringKeySpline",
            "MotionFast", "MotionNormal", "MotionSlow",
            // Consumed by animated controls to switch to a cross-fade
            // instead of a movement animation. Without it a zeroed
            // duration still reads as a jump cut.
            "MotionReduced",
        };

        private static readonly string[] AccentKeys =
        {
            "AccentBrush", "AccentHoverBrush", "AccentPressedBrush",
            "AccentForegroundBrush", "FocusRingBrush", "LiquidAccentGradient",
            "DashHeroBlueBrush",
        };

        private static readonly string[] AmbientKeys =
        {
            "DashAmbientBlob1Brush", "DashAmbientBlob2Brush",
            "DashLiquidSurfaceBrush", "DashLiquidBorderBrush",
        };

        /// <summary>Every key the eight-theme engine publishes. VisualThemeEngine
        /// iterates this on switch-away so no family can leak a brush into
        /// the next one — the exact bug the iOS 26 key list was written to
        /// prevent.</summary>
        internal static readonly string[] AllTokenKeys = Combine(
            SurfaceKeys, StageKeys, InkKeys, RuleKeys, RadiusKeys,
            ShadowKeys, LiquidKeys, MotionKeys, AccentKeys, AmbientKeys,
            "GlassWaterBrush", "GlassFrostedBrush", "GlassBorderSoft",
            "GlassBorderPrimary", "GlassBorderBright", "GlassCrystalBrush",
            "GlassSurfaceBrush", "GlassSurfaceElevated", "GlassSurfaceStandard",
            "GlassSurfaceUltra", "GlassUltraClearBrush",
            "GlassBorderGradientCrystal", "CardBorderThickness",
            // Neumorphism's four-stop extrude. These MUST be listed or a
            // claymorphism switch leaves the neumorphic control templates
            // bound to a light-grey highlight they were never meant to see.
            "NeuLightEffect", "NeuShadeEffect",
            "NeuPressedLightEffect", "NeuPressedShadeEffect");

        private static string[] Combine(params string[][] groups)
        {
            var seen = new List<string>();
            foreach (var g in groups)
                foreach (var k in g)
                    if (!seen.Contains(k))
                        seen.Add(k);
            return seen.ToArray();
        }

        // ═══════════════════════════════════════════════════════════════
        //  APPLY
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Paints <paramref name="profile"/> into
        /// <paramref name="resources"/>. Never throws.</summary>
        /// <param name="forceReducedMotion">Test seam for the OS
        /// reduced-motion preference; null reads the real setting.</param>
        public static void Apply(ResourceDictionary? resources,
            EightThemeProfile profile, bool dark, Color accent,
            bool? forceReducedMotion = null)
        {
            if (resources is null || profile is null) return;
            try
            {
                Paint(resources, profile, dark, accent, forceReducedMotion);
                PublishSoftUiPalettes(profile);
                AppLogService.Debug(
                    $"[EightTheme] Painted {profile.Identity} ({(dark ? "dark" : "light")})",
                    new LogContext { Module = "Themes", Operation = "EightThemePainter" });
            }
            catch (Exception ex)
            {
                // Rule 3. Degrade loudly in the log, never in the UI.
                AppLogService.Warning(
                    $"Eight-theme paint degraded for {profile.Identity}: {ex.Message}",
                    new LogContext { Module = "Themes", Operation = "EightThemePainter" });
            }
        }

        /// <summary>
        /// Keeps the soft-UI control palettes in step with the painted
        /// family.
        /// <para>
        /// The two extruding families are the only ones whose controls
        /// have to RESOLVE a construction rather than bind a token:
        /// neumorphism needs a mirrored outset pair, clay needs an outer
        /// drop plus two inner rolls, and in both cases the objects are
        /// brush objects the control recomputes whenever its tier or
        /// interaction state changes. So they need the profile, not the
        /// flattened resource bag.
        /// </para>
        /// <para>
        /// Publishing here — immediately after the paint — means the
        /// palettes cannot drift out of step with the tokens on screen.
        /// The three Clear() branches on the other families are the half
        /// that matters: a control still holding a published palette after
        /// the user switches to Swiss keeps drawing extrusions that no
        /// longer match the theme, and it looks like the theme change
        /// half-happened.
        /// </para>
        /// </summary>
        private static void PublishSoftUiPalettes(EightThemeProfile profile)
        {
            switch (profile.Material.Kind)
            {
                case EightMaterialKind.Inflated:
                    NeuPalette.Clear();
                    HoloPalette.Clear();
                    // The elevation's two stops ARE the clay shadow pair:
                    // the highlight that catches the light, and the shade
                    // tinted to the object's own hue. Using the ramp's
                    // Shadow for the shade is what keeps it a purple
                    // shadow on a lavender card rather than a grey one.
                    ClayPalette.Publish(
                        lightSurface: profile.Light.Surface,
                        darkSurface: profile.Dark.Surface,
                        shadeHue: profile.Light.Elevation.ShadeStop,
                        lightHue: profile.Light.Elevation.LightStop);
                    break;

                case EightMaterialKind.Extruded:
                    ClayPalette.Clear();
                    HoloPalette.Clear();
                    // Neumorphism's stops are NOT the ramp's. Its whole
                    // defining rule is that the object IS the page, so
                    // the light and shade are derived from that one
                    // colour rather than taken from the accent — which is
                    // exactly why the engine cannot point this at the
                    // ramp's Shadow the way it does for clay.
                    NeuPalette.Publish(
                        lightFill: profile.Light.Module,
                        darkFill: profile.Dark.Module,
                        rampLightStop: profile.Light.Elevation.LightStop,
                        rampDarkStop: profile.Light.Elevation.ShadeStop);
                    break;

                case EightMaterialKind.Solid:
                    // Solid covers BOTH Bento and Swiss, and the two
                    // families share a material while sharing nothing
                    // else: Bento has depth tiers and a shadow level,
                    // Swiss has neither and treats any shadow as a
                    // defect. So the branch tests the identity, not the
                    // material.
                    //
                    // Getting this wrong is not a subtle drift. A Swiss
                    // control still holding no palette falls back to a
                    // plain flat surface, and a Bento control still
                    // holding one keeps drawing extrusions over a grid
                    // that has no depth axis — so the Clear() on the
                    // other arm is load-bearing, not tidiness.
                    ClayPalette.Clear();
                    NeuPalette.Clear();
                    HoloPalette.Clear();

                    if (profile.Identity == EightThemeNames.Swiss)
                    {
                        // Paper comes from Window, NOT from Surface. Swiss
                        // panels are the page — there is no separate card
                        // colour to tint, and letting a panel carry the
                        // family's module tint would reintroduce exactly
                        // the filled-box decoration the style abolishes.
                        //
                        // The RULE is BorderStrong, not BorderDefault: a
                        // 1px hairline drawn in a subtle border is a line
                        // the reader cannot see, and a rule you cannot see
                        // is not a structural device, it is a rendering
                        // artefact.
                        SwissPalette.Publish(
                            lightPaper: profile.Light.Window,
                            lightInk: profile.Light.TextPrimary,
                            lightMuted: profile.Light.TextSecondary,
                            lightRule: profile.Light.BorderStrong,
                            accent: profile.Light.Accent,
                            darkPaper: profile.Dark.Window,
                            darkInk: profile.Dark.TextPrimary,
                            darkMuted: profile.Dark.TextSecondary,
                            darkRule: profile.Dark.BorderStrong);
                    }
                    else
                    {
                        SwissPalette.Clear();
                    }
                    break;

                case EightMaterialKind.Foil:
                    ClayPalette.Clear();
                    NeuPalette.Clear();
                    SwissPalette.Clear();

                    // ── WHY THE GROUND COMES FROM Surface AND NOT Window ──
                    // Holographic inverts the other families' arrangement:
                    // the foil lives on the RIM, so the ground is a real
                    // PANEL that has to read as a panel. Window is the
                    // page behind the panels, and using it would flatten
                    // every surface into the background — which is Bento's
                    // job, not this family's.
                    HoloPalette.Publish(
                        lightGround: profile.Light.Surface,
                        lightInk: profile.Light.TextPrimary,
                        lightMuted: profile.Light.TextSecondary,
                        lightAccent: profile.Light.Accent,
                        darkGround: profile.Dark.Surface,
                        darkInk: profile.Dark.TextPrimary,
                        darkMuted: profile.Dark.TextSecondary,
                        darkAccent: profile.Dark.Accent);
                    break;

                default:
                    ClayPalette.Clear();
                    NeuPalette.Clear();
                    SwissPalette.Clear();
                    HoloPalette.Clear();
                    break;
            }
        }

        /// <summary>
        /// Publishes the family's motion tokens, honouring the OS
        /// "prefers reduced motion" preference.
        /// <para>
        /// Every one of these eight families has a signature animation —
        /// the holographic shimmer, the liquid refraction, the neumorphic
        /// spring, the clay inflate. Reduced motion is not an edge case for
        /// this engine; it is the difference between a theme that is
        /// expressive and one that makes someone physically ill, and WCAG
        /// 2.3.3 (AAA) plus the vestibular-disorder guidance in the design
        /// research both call for honouring it.
        /// </para>
        /// <para>
        /// Two mechanisms, deliberately. The DURATION tokens collapse to a
        /// single 1ms frame so anything bound to them stops travelling.
        /// And <c>MotionReduced</c> is published as a plain bool so animated
        /// controls can switch to a cross-fade instead — a zeroed duration
        /// alone makes a spring settle instantly, which still reads as a
        /// jump cut, whereas an opacity-only transition is smooth and
        /// stationary.
        /// </para>
        /// <para>
        /// Windows exposes the user's preference through
        /// SystemParameters.ClientAreaAnimation, which tracks the
        /// "Animation effects" toggle in Ease of Access. That is the
        /// signal Windows itself uses; honouring it is what lets a user
        /// who never opens our settings still get a calm app.
        /// </para>
        /// </summary>
        private static void PaintMotion(
            ResourceDictionary r, EightThemeProfile p, bool reduced)
        {
            Str(r, "MotionEaseOutKeySpline",
                reduced ? FlatEaseOut : p.Motion.EaseOut);
            Str(r, "MotionSpringKeySpline",
                reduced ? FlatEaseOut : p.Motion.Spring);

            // Published as "h:mm:ss.fff" TimeSpans because the theme
            // dictionaries already declare these keys as sys:String.
            // 1ms, not 0: WPF treats a zero duration as "not animated" in
            // some paths and as an instant cut in others, and 1ms is
            // indistinguishable from zero to the eye while still taking
            // the animated code path.
            var fast = reduced ? Suppressed : p.Motion.Fast;
            var normal = reduced ? Suppressed : p.Motion.Normal;
            var slow = reduced ? Suppressed : p.Motion.Slow;
            Str(r, "MotionFast", ToTimeSpanString(fast));
            Str(r, "MotionNormal", ToTimeSpanString(normal));
            Str(r, "MotionSlow", ToTimeSpanString(slow));

            r["MotionReduced"] = reduced;
        }

        /// <summary>The OS-level reduced-motion preference. Read once per
        /// rebuild rather than cached: a user can flip the Ease of Access
        /// toggle while the app is running, and the next theme change
        /// should respect it.</summary>
        internal static bool ReducedMotionRequested
        {
            get
            {
                try
                {
                    return !SystemParameters.ClientAreaAnimation;
                }
                catch (Exception)
                {
                    // Never let an unavailable preference stop a theme
                    // from painting. Worst case the user gets motion.
                    return false;
                }
            }
        }

        /// <summary>Linear ease for suppressed motion — no overshoot, no
        /// spring, no acceleration curve to read as movement.</summary>
        private const string FlatEaseOut = "0,0,1,1";

        private static readonly TimeSpan Suppressed =
            TimeSpan.FromMilliseconds(1);

        private static void Paint(ResourceDictionary r,
            EightThemeProfile p, bool dark, Color accent,
            bool? forceReducedMotion = null)
        {
            var ramp = p.Ramp(dark);
            var accentFg = ReadableForeground(accent);

            // ── STAGES ────────────────────────────────────────────────
            Sol(r, "BackgroundBrush", ramp.Window);
            Sol(r, "WindowBackgroundBrush", ramp.Window);
            Sol(r, "ModuleBackgroundBrush", ramp.Module);
            Sol(r, "SideMenuBackgroundBrush", ramp.Chrome);
            Sol(r, "ElevatedSurfaceBrush", ramp.SurfaceSecondary);
            Sol(r, "PopupSurfaceBrush", ramp.SurfaceSecondary);
            Sol(r, "DialogSurfaceBrush", ramp.Dialog);
            Sol(r, "DialogInsetBrush", ramp.DialogInset);

            // ── SURFACES ──────────────────────────────────────────────
            // One source of truth for alpha: the ramp's own per-level value.
            // See NormalizeAlpha for why Material.SurfaceAlpha is NOT
            // multiplied in here.
            var fill = NormalizeAlpha(ramp.Surface, p.Material.Kind);
            var fillSecondary = NormalizeAlpha(ramp.SurfaceSecondary, p.Material.Kind);
            var fillDetail = NormalizeAlpha(ramp.SurfaceDetail, p.Material.Kind);

            Brush(r, "Surface.Primary.BackgroundBrush", fill);
            Brush(r, "Surface.Secondary.BackgroundBrush", fillSecondary);
            Brush(r, "Surface.Detail.BackgroundBrush", fillDetail);

            // The family rim opacity scales the keyline. For the
            // extrude families this is the ACCESSIBILITY COMPENSATION and
            // not decoration — their boundary is defined by shadow alone,
            // which measures well under the 3:1 that WCAG 2.2 SC 1.4.11
            // requires — so the recipe ships a high rim opacity to pair
            // with it and this is where it lands.
            var subtle = Rescale(ramp.BorderSubtle, p.Material.RimOpacity);
            var normal = Rescale(ramp.BorderDefault, p.Material.RimOpacity);
            var strong = Rescale(ramp.BorderStrong, p.Material.RimOpacity);

            Brush(r, "Surface.Primary.BorderBrush", normal);
            Brush(r, "Surface.Secondary.BorderBrush", normal);
            Brush(r, "Surface.Detail.BorderBrush", subtle);

            Corner(r, "Surface.Primary.CornerRadius", p.Geometry.Card);
            Corner(r, "Surface.Secondary.CornerRadius", p.Geometry.Large);
            Corner(r, "Surface.Detail.CornerRadius", p.Geometry.Control);

            // ── INK ───────────────────────────────────────────────────
            Sol(r, "PrimaryForegroundBrush", ramp.TextPrimary);
            Sol(r, "SecondaryForegroundBrush", ramp.TextSecondary);
            Sol(r, "MutedForegroundBrush", ramp.TextMuted);
            Sol(r, "TextPrimaryBrush", ramp.TextPrimary);
            Sol(r, "TextSecondaryBrush", ramp.TextSecondary);

            // ── RULES ─────────────────────────────────────────────────
            // DividerBrush / OutlineBrush / OutlineDefaultBrush are owned
            // by ThemeManager.ApplyAccent, which runs AFTER this painter
            // in the pipeline and derives them from the live accent. Writing
            // them here would be overwritten within the same rebuild, so
            // they are deliberately not written.
            Sol(r, "OutlineStrongBrush", strong);
            // Rule 2: a typed Thickness on EVERY path. This key was the
            // subject of a real startup crash ("BorderThickness must be of
            // type Thickness") when one family published a Double here.
            Thick(r, "CardBorderThickness", 1);

            // ── GEOMETRY ──────────────────────────────────────────────
            // Rounded families scale their own scale; Swiss clamps to 0.
            Corner(r, "CardCornerRadius", p.Geometry.Card);
            Corner(r, "CardCornerRadiusLarge", p.Geometry.Large);
            Corner(r, "ControlCornerRadius", p.Geometry.Control);
            Corner(r, "Radius.Small", Math.Max(0, p.Geometry.Control - 4));
            Corner(r, "Radius.Medium", p.Geometry.Control);
            Corner(r, "Radius.Large", p.Geometry.Card);
            Corner(r, "Radius.Hero", p.Geometry.Hero);

            // ── ELEVATION ─────────────────────────────────────────────
            PaintShadows(r, p, ramp);

            // ── MATERIAL ─────────────────────────────────────────────
            PaintMaterial(r, p, ramp);

            // ── MOTION ───────────────────────────────────────────────
            // forceReducedMotion exists purely so the accessibility
            // contract is TESTABLE. SystemParameters.ClientAreaAnimation
            // is a live WPF call that a unit test cannot force, so without
            // this seam the reduced-motion branch would be asserted only by
            // inspection — and an untested accessibility branch is one that
            // silently regresses.
            PaintMotion(r, p,
                forceReducedMotion ?? ReducedMotionRequested);

            // ── ACCENT ────────────────────────────────────────────────
            Sol(r, "AccentBrush", accent);
            Sol(r, "AccentHoverBrush", Mix(accent, ramp.TextPrimary, 0.12));
            // Pressed darkens toward black in BOTH modes. On a light ramp a
            // black mix deepens the accent; on a dark ramp the accent is
            // already light, and darkening it is the direction the finger
            // is already pressing. Mixing toward white in dark mode would
            // brighten the button and read as a hover, not a press.
            Sol(r, "AccentPressedBrush", Mix(accent, Colors.Black, 0.18));
            Sol(r, "AccentForegroundBrush", accentFg);
            // A focus ring that is derived from the ramp, not from the
            // accent — neumorphism and swiss both need a ring that is
            // distinct from their calm accent to stay visible.
            Sol(r, "FocusRingBrush", ramp.FocusRing);
            Brush(r, "LiquidAccentGradient", Diagonal(accent, Mix(accent, ramp.FocusRing, 0.55)));
            Sol(r, "DashHeroBlueBrush", accent);

            // ── AMBIENT ──────────────────────────────────────────────
            Sol(r, "DashAmbientBlob1Brush", Rescale(ramp.BorderStrong, 0.42));
            Sol(r, "DashAmbientBlob2Brush", Rescale(ramp.BorderDefault, 0.38));
            Brush(r, "DashLiquidSurfaceBrush", fill);
            Sol(r, "DashLiquidBorderBrush", normal);
        }

        // ═══════════════════════════════════════════════════════════════
        //  ELEVATION
        // ═══════════════════════════════════════════════════════════════

        private static void PaintShadows(ResourceDictionary r,
            EightThemeProfile p, EightColorRamp ramp)
        {
            var e = p.Elevation;

            // Swiss declares None and the painter HONOURS it: publishing a
            // shadow for a family whose entire premise is the absence of
            // shadows would be a lie about the style.
            if (e.Kind == EightElevationKind.None)
            {
                r["ShadowSoft"] = new DropShadowEffect
                { BlurRadius = 0, ShadowDepth = 0, Opacity = 0 };
                r["ShadowElevated"] = new DropShadowEffect
                { BlurRadius = 0, ShadowDepth = 0, Opacity = 0 };
                r["ShadowDeep"] = new DropShadowEffect
                { BlurRadius = 0, ShadowDepth = 0, Opacity = 0 };
                foreach (var k in ShadowKeys)
                    if (k.StartsWith("Dash", StringComparison.Ordinal) ||
                        k.StartsWith("GlassShadow", StringComparison.Ordinal))
                        r[k] = new DropShadowEffect
                        { BlurRadius = 0, ShadowDepth = 0, Opacity = 0 };
                return;
            }

            // The shared shadow colour. Claymorphism and holographic both
            // override ramp.Shadow with their own hue, so this is already
            // family-tinted rather than neutral grey by construction.
            var shadowColor = e.Kind switch
            {
                EightElevationKind.TintedGlow => Mix(ramp.Shadow, ramp.Accent, 0.35),
                EightElevationKind.Inflate => Darken(e.ShadeStop, 0.15),
                _ => ramp.Shadow,
            };

            // Rule 4: hard bound on blur and depth.
            double lo = Math.Min(e.LowBlur, 56);
            double hi = Math.Min(e.HighBlur, 56);

            Drop(r, "ShadowSoft", lo, Math.Min(e.LowDepth, 12), e.LowOpacity, shadowColor);
            Drop(r, "ShadowElevated", hi, Math.Min(e.HighDepth, 12), e.HighOpacity, shadowColor);
            Drop(r, "ShadowDeep", hi, Math.Min(e.HighDepth, 12), e.HighOpacity, shadowColor);
            Drop(r, "DashCardShadowEffect", lo, Math.Min(e.LowDepth, 12), e.LowOpacity * 0.85, shadowColor);
            Drop(r, "DashSubtleShadowEffect", lo * 0.75, Math.Min(e.LowDepth, 6), e.LowOpacity * 0.70, shadowColor);
            Drop(r, "DashInteractiveShadowEffect", hi, Math.Min(e.HighDepth, 12), e.HighOpacity, shadowColor);
            Drop(r, "GlassShadowPanel", lo, Math.Min(e.LowDepth, 12), e.LowOpacity, shadowColor);
            Drop(r, "GlassShadowSoft", lo * 0.75, Math.Min(e.LowDepth, 8), e.LowOpacity * 0.80, shadowColor);
            Drop(r, "GlassShadowSoftButton", lo, Math.Min(e.LowDepth, 12), e.LowOpacity, shadowColor);
            Drop(r, "GlassShadowSoftCheckBox", lo, Math.Min(e.LowDepth, 12), e.LowOpacity, shadowColor);
            Drop(r, "GlassShadowSoftTextBox", lo * 0.75, Math.Min(e.LowDepth, 8), e.LowOpacity * 0.80, shadowColor);
            Drop(r, "GlassShadowSoftToggle", lo, Math.Min(e.LowDepth, 12), e.LowOpacity, shadowColor);
            Drop(r, "GlassShadowActive", hi, Math.Min(e.HighDepth, 12), e.HighOpacity, shadowColor);
            Drop(r, "GlassShadowActiveButton", hi, Math.Min(e.HighDepth, 12), e.HighOpacity, shadowColor);

            // Neumorphism's DEFINING trait is a mirrored pair with the light
            // stop toward the light source. A DropShadowEffect cannot carry
            // two shadows, so the light half is published as a separate
            // resource the neumorphic control styles consume via Effect
            // + a second pass. Exposing both stops here is what keeps the
            // extrude from collapsing back to an ordinary shadow.
            if (e.Kind == EightElevationKind.Extrude)
            {
                var light = new DropShadowEffect
                {
                    BlurRadius = lo,
                    DirectionX = -1.0,
                    DirectionY = -1.0,
                    ShadowDepth = 0,
                    Opacity = 1.0,
                    Color = e.LightStop,
                };
                var shade = new DropShadowEffect
                {
                    BlurRadius = lo,
                    DirectionX = 1.0,
                    DirectionY = 1.0,
                    ShadowDepth = 0,
                    Opacity = 1.0,
                    Color = Darken(e.ShadeStop, 0.0),
                };
                r["NeuLightEffect"] = light;
                r["NeuShadeEffect"] = shade;
                // Pressed state: the SAME pair with the direction reversed
                // and the shadows inset. Direction reversal is the state
                // signal; it is the only one neumorphism really has.
                r["NeuPressedLightEffect"] = CloneReversed(light);
                r["NeuPressedShadeEffect"] = CloneReversed(shade);
            }
        }

        private static DropShadowEffect CloneReversed(DropShadowEffect source)
        {
            var clone = (DropShadowEffect)source.Clone();
            clone.DirectionX = -source.DirectionX;
            clone.DirectionY = -source.DirectionY;
            return clone;
        }

        // ═══════════════════════════════════════════════════════════════
        //  MATERIAL
        // ═══════════════════════════════════════════════════════════════

        private static void PaintMaterial(ResourceDictionary r,
            EightThemeProfile p, EightColorRamp ramp)
        {
            var m = p.Material;
            var isTranslucent = m.Kind is EightMaterialKind.Frosted
                or EightMaterialKind.Refractive
                or EightMaterialKind.Volumetric;

            // Liquid / shell frost vocabulary. These are what the chrome,
            // the rail and the floating cards bind to, so they have to carry
            // the family material even in the opaque families — otherwise
            // switching from Liquid Glass to Bento leaves the shell glassy.
            var glass = NormalizeAlpha(ramp.Surface, m.Kind);
            var frosted = NormalizeAlpha(ramp.SurfaceDetail, m.Kind);
            var strong = NormalizeAlpha(ramp.SurfaceSecondary, m.Kind);

            Brush(r, "LiquidGlassBrush", glass);
            Brush(r, "LiquidGlassSubtleBrush", frosted);
            Brush(r, "LiquidGlassStrongBrush", strong);
            Brush(r, "LiquidGlassFrostedBrush", frosted);
            Brush(r, "LiquidCard", glass);
            Brush(r, "GlassSurfaceBrush", glass);
            Brush(r, "GlassSurfaceElevated", strong);
            Brush(r, "GlassSurfaceStandard", glass);
            Brush(r, "GlassSurfaceUltra", frosted);
            // The "ultra clear" level is deliberately thinner than the card:
            // its own alpha comes from the ramp, scaled once here.
            Brush(r, "GlassUltraClearBrush", ScaleAlpha(
                NormalizeAlpha(ramp.Surface, m.Kind), 0.7));
            Brush(r, "GlassCrystalBrush", strong);
            Brush(r, "GlassWaterBrush", frosted);
            Brush(r, "GlassFrostedBrush", frosted);

            // ── THE RIM ───────────────────────────────────────────────
            var rimBase = m.SpecularRim
                // A specular rim is BRIGHT along the lit edge and falls away
                // on the opposite side. A flat alpha ring is the thing that
                // makes a "liquid glass" theme look like a plain white card
                // with a border, so the gradient is not optional here.
                ? Diagonal(
                    Rescale(m.RimTint, m.RimOpacity),
                    Rescale(m.RimTint, m.RimOpacity * 0.22))
                : Sol(m.RimTint, m.RimOpacity);

            Brush(r, "LiquidEdgeBrush", rimBase);
            Brush(r, "GlassBorderSoft", rimBase);
            Brush(r, "GlassBorderPrimary", rimBase);
            Brush(r, "GlassBorderBright",
                m.SpecularRim ? rimBase : Sol(m.RimTint, m.RimOpacity * 0.5));
            Brush(r, "GlassBorderGradientCrystal", rimBase);
            Brush(r, "ShellLiquidBorderBrush", rimBase);
            Brush(r, "ShellSheenBrush",
                m.InnerGlowOpacity > 0
                    ? Diagonal(Sol(Colors.White, m.InnerGlowOpacity * 0.8), Sol(Colors.White, 0))
                    : Sol(Colors.White, 0));
            Brush(r, "SidebarCardBrush", NormalizeAlpha(ramp.Chrome, m.Kind));
            Brush(r, "ShellLiquidBrush", NormalizeAlpha(ramp.Chrome, m.Kind));

            // ── OVERLAY ───────────────────────────────────────────────
            // A scrim behind dialogs. Glass families get a lighter scrim
            // than solid ones, because the material is the thing that is
            // supposed to read, not the dimming behind it.
            Sol(r, "LiquidOverlayBrush",
                Rescale(ramp.Window, isTranslucent ? 0.34 : 0.46));

            // ── SATURATION ────────────────────────────────────────────
            // A translucent surface over a blurred backdrop loses chroma.
            // Pushing saturation back up is what keeps the material alive
            // instead of grey — it is the difference between "glass" and
            // "smear", and it is the single most-skipped step in
            // glassmorphism implementations.
            if (m.SaturationBoost > 0.01 && isTranslucent)
                ApplySaturationBoost(r, p, ramp, m.SaturationBoost);
        }

        /// <summary>Pushes chroma back into every translucent fill this
        /// family publishes. Only touches the keys whose fill actually
        /// carries alpha — an opaque key is left alone because boosting an
        /// opaque colour just makes it a different, wrong colour.</summary>
        private static void ApplySaturationBoost(ResourceDictionary r,
            EightThemeProfile p, EightColorRamp ramp, double boost)
        {
            foreach (var key in new[]
            {
                "LiquidGlassBrush", "LiquidGlassSubtleBrush",
                "LiquidGlassStrongBrush", "LiquidGlassFrostedBrush",
                "LiquidCard", "GlassSurfaceBrush", "GlassSurfaceElevated",
                "GlassSurfaceStandard", "GlassSurfaceUltra",
                "GlassUltraClearBrush", "GlassCrystalBrush",
                "Surface.Primary.BackgroundBrush",
                "Surface.Secondary.BackgroundBrush",
                "Surface.Detail.BackgroundBrush",
                "SidebarCardBrush", "ShellLiquidBrush",
            })
            {
                if (r[key] is not SolidColorBrush existing) continue;
                var boosted = Saturate(existing.Color, boost);
                if (boosted.A != existing.Color.A)
                    continue; // guard: never change opacity while boosting
                r[key] = FrozenSolid(boosted);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  COLOUR HELPERS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Normalises a surface fill's alpha for its family.
        /// <para>
        /// OPAQUE FAMILIES: the fill is exactly the ramp colour at full
        /// opacity. Forcing 255 here is not cosmetic — a "Solid" family
        /// that accidentally kept a ramp alpha would render as a see-
        /// through card, which is the one thing it promised never to do.
        /// </para>
        /// <para>
        /// TRANSLUCENT FAMILIES: the ramp's OWN alpha is authoritative and
        /// is passed through untouched. It is deliberately per-LEVEL — a
        /// card is more opaque than the rail behind it, a dialog more
        /// opaque than a panel — and a single Material.SurfaceAlpha cannot
        /// express that gradient.
        /// </para>
        /// <para>
        /// This method used to multiply the two together, which silently
        /// squared the opacity: a ramp carrying 0.62 and a material
        /// declaring 0.62 rendered at 0.38, and every glass family's cards
        /// came out nearly invisible against their stage. That is exactly
        /// why the rule is "one source of truth per value" and why
        /// Material.SurfaceAlpha is documented as the NOMINAL card opacity
        /// used for reporting and for the Theme Center preview — never as a
        /// second multiplier here.
        /// </para>
        /// </summary>
        private static Color NormalizeAlpha(Color c, EightMaterialKind kind)
        {
            if (kind is EightMaterialKind.Solid
                or EightMaterialKind.Extruded
                or EightMaterialKind.Inflated
                or EightMaterialKind.Foil)
                return Color.FromArgb(255, c.R, c.G, c.B);
            return c;
        }

        /// <summary>Scales an existing alpha channel. Used for the one
        /// derived level ("ultra clear"), never for a family's primary
        /// surface.</summary>
        private static Color ScaleAlpha(Color c, double factor)
        {
            if (factor >= 0.999) return c;
            return Color.FromArgb(
                ScaleByte(factor * (c.A / 255.0)), c.R, c.G, c.B);
        }

        private static Color Rescale(Color c, double factor)
        {
            double f = System.Math.Clamp(factor, 0, 1);
            if (f >= 0.999) return c;
            return Color.FromArgb(ScaleByte(f * (c.A / 255.0)), c.R, c.G, c.B);
        }

        /// <summary>Multiplies RGB toward black and drops alpha, used for
        /// derived accents and press states.</summary>
        internal static Color Darken(Color c, double amount)
        {
            double k = System.Math.Clamp(amount, 0, 1);
            return Color.FromArgb(
                ScaleByte((1 - k * 0.55) * (c.A / 255.0)),
                ScaleByte((1 - k) * c.R / 255.0),
                ScaleByte((1 - k) * c.G / 255.0),
                ScaleByte((1 - k) * c.B / 255.0));
        }

        /// <summary>Linear RGB blend. Used for press states, hover tints and
        /// the accent gradient's second stop.</summary>
        internal static Color Mix(Color a, Color b, double t)
        {
            double k = System.Math.Clamp(t, 0, 1);
            return Color.FromArgb(
                ScaleByte((a.A * (1 - k) + b.A * k) / 255.0),
                ScaleByte((a.R * (1 - k) + b.R * k) / 255.0),
                ScaleByte((a.G * (1 - k) + b.G * k) / 255.0),
                ScaleByte((a.B * (1 - k) + b.B * k) / 255.0));
        }

        /// <summary>Multiplies saturation by <paramref name="boost"/>,
        /// clamping back into gamut. A saturation boost above ~1.6 on an
        /// already-saturated colour clips to pure primaries, so this is
        /// capped rather than trusted.</summary>
        internal static Color Saturate(Color c, double boost)
        {
            if (boost <= 0.01) return c;
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = System.Math.Max(r, System.Math.Max(g, b));
            double min = System.Math.Min(r, System.Math.Min(g, b));
            if (max <= 0) return c;
            double l = (max + min) / 2.0;
            double d = max - min;
            double s = d / (1 - System.Math.Abs(2 * l - 1) + 1e-6);

            double ns = System.Math.Clamp(s * boost, 0, 1);
            double nd = ns * (1 - System.Math.Abs(2 * l - 1));
            double nr = ng = nb = 0;

            if (d <= 1e-6)
            {
                nr = ng = nb = l;
            }
            else
            {
                double hue = max switch
                {
                    _ when max == r => ((g - b) / d) % 6.0,
                    _ when max == g => (b - r) / d + 2.0,
                    _ => (r - g) / d + 4.0,
                };
                hue *= 60.0;
                if (hue < 0) hue += 360.0;

                double c1 = nd * (1 - System.Math.Abs(hue / 60.0 % 2 - 1));
                double m = l - nd / 2.0;

                (nr, ng, nb) = hue switch
                {
                    < 60 => (nd, c1, 0.0),
                    < 120 => (c1, nd, 0.0),
                    < 180 => (0.0, nd, c1),
                    < 240 => (0.0, c1, nd),
                    < 300 => (c1, 0.0, nd),
                    _ => (nd, 0.0, c1),
                };
                nr += m; ng += m; nb += m;
            }

            return Color.FromArgb(c.A, ScaleByte(nr), ScaleByte(ng), ScaleByte(nb));
        }

        /// <summary>
        /// Picks black or white text for an accent fill by relative
        /// luminance. Never returns "white" on a light accent — an accent
        /// such as neumorphism's #5B7CFA is dark enough that white label
        /// text on it drops under 4.5:1.
        /// </summary>
        internal static Color ReadableForeground(Color background)
        {
            double lum = (0.2126 * Linear(background.R)
                        + 0.7152 * Linear(background.G)
                        + 0.0722 * Linear(background.B));
            return lum > 0.45 ? Colors.Black : Colors.White;
        }

        private static double Linear(byte channel)
        {
            double c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92
                : System.Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        private static byte ScaleByte(double value01) =>
            (byte)System.Math.Round(
                System.Math.Clamp(value01, 0, 1) * 255,
                MidpointRounding.AwayFromZero);

        private static string ToTimeSpanString(double milliseconds)
        {
            var ts = TimeSpan.FromMilliseconds(System.Math.Max(0, milliseconds));
            return ts.ToString(@"h\:mm\:ss\.fff");
        }

        // ═══════════════════════════════════════════════════════════════
        //  RESOURCE WRITERS — every one keeps a single runtime type
        // ═══════════════════════════════════════════════════════════════

        private static void Sol(ResourceDictionary r, string key, Color c) =>
            r[key] = FrozenSolid(c);

        private static void Brush(ResourceDictionary r, string key, Brush b) =>
            r[key] = Freeze(b);

        private static void Str(ResourceDictionary r, string key, string v) =>
            r[key] = v;

        private static void Corner(ResourceDictionary r, string key, double v) =>
            r[key] = new CornerRadius(v, v, v, v);

        private static void Thick(ResourceDictionary r, string key, double v) =>
            r[key] = new Thickness(v);

        private static void Drop(ResourceDictionary r, string key,
            double blur, double depth, double opacity, Color color) =>
            r[key] = new DropShadowEffect
            {
                BlurRadius = System.Math.Max(0, blur),
                ShadowDepth = System.Math.Max(0, depth),
                Opacity = System.Math.Clamp(opacity, 0, 1),
                Color = color,
            };

        private static SolidColorBrush Sol(Color c, double alpha) =>
            FrozenSolid(Rescale(c, alpha));

        private static LinearGradientBrush Diagonal(Color a, Color b) =>
            Freeze(new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 1),
                GradientStops =
                {
                    new GradientStop(a, 0.0),
                    new GradientStop(b, 1.0),
                },
            });

        /// <summary>Frozen brushes everywhere. A theme dictionary that
        /// hands out an unfrozen brush to a hundred controls makes WPF
        /// re-rasterise every gradient on every repaint, which is the
        /// difference between a smooth module switch and a visible stall
        /// on every navigation.</summary>
        internal static SolidColorBrush FrozenSolid(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        internal static T Freeze<T>(T freezable) where T : Freezable
        {
            if (freezable.CanFreeze) freezable.Freeze();
            return freezable;
        }
    }
}