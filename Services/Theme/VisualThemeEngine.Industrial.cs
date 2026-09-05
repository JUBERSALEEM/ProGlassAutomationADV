// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/VisualThemeEngine.Industrial.cs
// PROJECT: ProGlassAutomation
// MODULE: INDUSTRIAL theme family (2026-09-05 — owner: "the glass UI is
//         too stylish for a factory floor; give me a plain, readable,
//         industry-grade ERP theme").
//
// DESIGN LANGUAGE (senior spec):
//   • SOLID surfaces only — zero glass gradients, zero blur theatre.
//     Cards are opaque, high-contrast slabs (the PublishSectionGlass
//     mapping turns every glass token into the same solid material).
//   • Small deterministic radii (4/6) — industry HMI/ERP convention.
//   • A real KEYLINE on every card (no glow, no shadow tricks) — the
//     boundary is a 1px neutral line you can see on a projector and on
//     a sunny shop-floor monitor.
//   • Three shade layouts, explicit (no guessing):
//       Industrial White — paper-white content on light-grey stage
//       Industrial Grey  — neutral grey content on darker grey stage
//       Industrial Dark  — HMI-style dark steel (pairs with Dark mode)
//   • Typography/colours stay theme-driven: Light/Dark base themes own
//     the ink; the VM auto-pairs Industrial Dark → Dark mode and White/
//     Grey → Light mode on selection (see SelectedVisualTheme setter),
//     so the text/surface pairing can never be mis-matched.
//
// UI/DESIGN ONLY: no formulas, no business logic touched.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme
{
    public static partial class VisualThemeEngine
    {
        // ── Family keys (registered with the engine constants) ───────
        public const string IndustrialWhite = "Industrial White";
        public const string IndustrialGrey  = "Industrial Grey";
        public const string IndustrialDark  = "Industrial Dark";

        /// <summary>True only for the three industrial families (mode
        /// coupling + a couple of engine-level special cases read this).</summary>
        public static bool IsIndustrial(string? family) =>
            family == IndustrialWhite ||
            family == IndustrialGrey ||
            family == IndustrialDark;

        /// <summary>The Light/Dark mode a family PROMISES by design —
        /// true = the family only makes sense in Dark, false = Light,
        /// null = the family respects the user's own mode choice
        /// (2026-09-05 Phase-1: this map used to live as a hand-written
        /// switch in the Settings VM — engineering data belongs to the
        /// engine so any caller, present or future, gets the same
        /// pairing and mode-mismatched ink is structurally impossible).</summary>
        public static bool? ModePairFor(string? family) => family switch
        {
            IndustrialDark => true,
            IndustrialWhite => false,
            IndustrialGrey => false,
            // 2026-09-05: Crystal Fluid is a LIGHT-ONLY family (locked
            // product decision: the Light/Dark toggle stays for the other
            // families; picking Crystal Fluid snaps to Light, same
            // mechanism as IndustrialWhite/Grey).
            CrystalFluid => false,
            _ => null,
        };

        private static void ApplyIndustrialWhite(ResourceDictionary r) =>
            ApplyIndustrial(r,
                windowTop: ParseColor("#F2F4F7"), windowBottom: ParseColor("#E4E7EB"),
                module: ParseColor("#F7F8FA"),  elevated: ParseColor("#FFFFFF"),
                sideMenu: ParseColor("#E9ECEF"), hover: ParseColor("#DCE1E6"),
                keyline: ParseColor("#8D9AA8"), dark: false,
                familyLabel: IndustrialWhite);

        private static void ApplyIndustrialGrey(ResourceDictionary r) =>
            ApplyIndustrial(r,
                windowTop: ParseColor("#DDE0E4"), windowBottom: ParseColor("#CFD3D8"),
                module: ParseColor("#E5E8EB"),  elevated: ParseColor("#F3F4F6"),
                sideMenu: ParseColor("#DADEE2"), hover: ParseColor("#CBD1D7"),
                keyline: ParseColor("#7E8A97"), dark: false,
                familyLabel: IndustrialGrey);

        private static void ApplyIndustrialDark(ResourceDictionary r) =>
            ApplyIndustrial(r,
                windowTop: ParseColor("#16181C"), windowBottom: ParseColor("#1B1E23"),
                module: ParseColor("#1F2329"),  elevated: ParseColor("#262B32"),
                sideMenu: ParseColor("#171A1F"), hover: ParseColor("#2E343D"),
                keyline: ParseColor("#4A545F"), dark: true,
                familyLabel: IndustrialDark);

        /// <summary>Core industrial painter: solid slabs, squared corners,
        /// visible neutral keyline. Every glass token maps to the same
        /// solid material (PublishSectionGlass), so Settings cards,
        /// wizards, sidebars — everything follows the industrial look,
        /// same as Professional's pattern but STRICTER (no gradients
        /// carried over at all: the window uses a dead-flat, imperceptibly
        /// darkened band rather than a decorative gradient).</summary>
        private static void ApplyIndustrial(
            ResourceDictionary resources,
            Color windowTop, Color windowBottom,
            Color module, Color elevated, Color sideMenu, Color hover,
            Color keyline, bool dark, string familyLabel)
        {
            // Window stage — flat with an almost-imperceptible vertical
            // falloff (keeps WPF from banding on large areas while reading
            // as one solid tone to the eye).
            var window = new LinearGradientBrush(
                windowTop, windowBottom, new Point(0, 0), new Point(0, 1));
            window.Freeze();
            resources["WindowBackgroundBrush"] = window;

            // Content surfaces — SOLID.
            var moduleBrush = new SolidColorBrush(module);     moduleBrush.Freeze();
            var elevatedBrush = new SolidColorBrush(elevated); elevatedBrush.Freeze();
            var sideMenuBrush = new SolidColorBrush(sideMenu); sideMenuBrush.Freeze();
            resources["ModuleBackgroundBrush"] = moduleBrush;
            resources["ElevatedSurfaceBrush"] = elevatedBrush;
            resources["SideMenuBackgroundBrush"] = sideMenuBrush;

            var hoverBrush = new SolidColorBrush(hover);       hoverBrush.Freeze();
            resources["MenuHoverBrush"] = hoverBrush;

            // Industry-standard radii: small and consistent.
            resources["CardCornerRadius"] = new CornerRadius(4);
            resources["CardCornerRadiusLarge"] = new CornerRadius(6);
            resources["ControlCornerRadius"] = new CornerRadius(3);

            // Glass tokens become the solid material (Settings sections,
            // dialogs, sidebars all consume these). borderTint drives the
            // translucency-free edge mix inside PublishSectionGlass.
            PublishSectionGlass(resources, keyline, dark);

            // Popups/menus use elevated; combo rows stay high-contrast on
            // their own palette (they already do via PopupSurfaceBrush).
            resources["PopupSurfaceBrush"] = elevatedBrush;

            // ── FAMILY CONTRACT (2026-09-05, ERR-50472F) ───────────────
            // ClearOverrides strips EVERY family-managed key from the app
            // scope before this painter runs. The theme-file statics are
            // NOT a guaranteed safety net (a machine that can no longer
            // resolve the merged theme static threw
            // ResourceReferenceKeyNotFoundException on 'DashAmbientBrush'
            // mid-switch and crashed the app). Every family MUST
            // republish the complete managed contract — dashboard, shell,
            // sidebar, liquid tokens, status tints — from its own
            // palette. Industrial publishes them all as flat slabs.

            var keylineBrush = new SolidColorBrush(keyline);
            keylineBrush.Freeze();
            var transparent = new SolidColorBrush(Colors.Transparent);
            transparent.Freeze();

            // Dashboard — hero card is a slab; the ambient layer becomes
            // a whisper of keyline over the module; ambient light blobs
            // have no place on a factory floor (transparent, not missing).
            resources["DashLiquidSurfaceBrush"] = elevatedBrush;
            var ambient = Flat(Blend(keyline, module, dark ? 0.14 : 0.09));
            resources["DashAmbientBrush"] = ambient;
            resources["DashAmbientBlob1Brush"] = transparent;
            resources["DashAmbientBlob2Brush"] = transparent;

            // Shell top strip + sidebar tiles follow the chrome palette.
            resources["ShellLiquidBrush"] = sideMenuBrush;
            resources["SidebarCardBrush"] = elevatedBrush;

            // Liquid Glass design tokens → flat industrial equivalents:
            // no crystal sheen, no reflection, no glow — a visible 1px
            // keyline is the only edge treatment.
            resources["LiquidGlassBrush"] = moduleBrush;
            resources["LiquidGlassSubtleBrush"] = moduleBrush;
            resources["LiquidGlassStrongBrush"] = elevatedBrush;
            resources["LiquidEdgeBrush"] = keylineBrush;
            resources["LiquidInnerHighlightBrush"] = transparent;
            resources["LiquidReflectionBrush"] = transparent;
            resources["LiquidControlHoverBrush"] = hoverBrush;
            resources["LiquidControlPressedBrush"] = Flat(
                Blend(keyline, hover, 0.35));
            resources["LiquidControlDisabledBrush"] = Flat(
                Blend(keyline, module, dark ? 0.22 : 0.14));
            resources["LiquidFocusBrush"] = Flat(
                Blend(keyline, elevated, 0.55));
            resources["LiquidSelectionBrush"] = Flat(
                Blend(keyline, module, dark ? 0.35 : 0.22));
            // Primary action: one dependable steel slab per mode.
            resources["LiquidPrimaryButtonBrush"] = Flat(
                Blend(keyline, dark ? Colors.White : Colors.Black,
                    dark ? 0.30 : 0.45));
            resources["LiquidOverlayBrush"] = Flat(
                Blend(Colors.Black, windowTop, dark ? 0.35 : 0.15, 0.72));
            resources["LiquidShadowColor"] = Color.FromArgb(
                dark ? (byte)0x3D : (byte)0x24, 0, 0, 0);

            // Status chips — matte, desaturated tints: hue kept, theatre
            // removed (readable next to noisy machinery, on projectors).
            PublishMatteTints(resources, module, dark);

            // Card outlines: owned by the ACCENT LAYER
            // (ThemeManager.ApplyAccent writes OutlineBrush/BorderBrush/
            // DividerBrush after every family paint — ApplyKeyline is a
            // documented no-op kept only to mark the family at this
            // point in the sequence). The industrial signature separator
            // is therefore the same accent-derived 1px keyline the rest
            // of the product uses — consistent, visible, no glow.
            ApplyKeyline(resources, keyline);

            AppLogService.Debug(
                $"[ThemeEngine] Industrial family painted: {familyLabel} " +
                $"(dark={dark})",
                new LogContext { Module = "Themes", Operation = "Industrial" });
        }

        private static Color ParseColor(string hex)
        {
            var c = ColorConverter.ConvertFromString(hex);
            return c is Color col ? col : Colors.Magenta;
        }

        /// <summary>Status chips without the glass theatre: the hue at a
        /// disciplined strength over the module slab — same idea as the
        /// liquid family's HueFrost, but an OPAQUE matte mix so text and
        /// boundaries stay contrast-true on shop-floor displays.</summary>
        private static void PublishMatteTints(
            ResourceDictionary resources, Color module, bool dark)
        {
            double t = dark ? 0.34 : 0.14;
            resources["TintIndigoBrush"] = Flat(Blend(
                ParseColor("#6366F1"), module, t));
            resources["TintBlueBrush"] = Flat(Blend(
                ParseColor("#3B82F6"), module, t));
            resources["TintGreenBrush"] = Flat(Blend(
                ParseColor("#22C55E"), module, t));
            resources["TintRedBrush"] = Flat(Blend(
                ParseColor("#EF4444"), module, t));
            resources["TintAmberBrush"] = Flat(Blend(
                ParseColor("#F59E0B"), module, t));
            resources["TintPurpleBrush"] = Flat(Blend(
                ParseColor("#A855F7"), module, t));
            resources["TintGrayBrush"] = Flat(Blend(
                ParseColor("#6B7280"), module, t));
        }

        /// <summary>Frozen solid brush for a pre-computed colour.</summary>
        private static SolidColorBrush Flat(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        /// <summary>OPAQUE colour mix: <paramref name="t"/> parts of
        /// <paramref name="a"/> over <paramref name="b"/>. Used instead of
        /// alpha-blended translucency so every industrial surface stays a
        /// true solid (compositing-free, projector-safe).</summary>
        private static Color Blend(Color a, Color b, double t, double alpha = 1.0)
        {
            if (t < 0) t = 0; else if (t > 1) t = 1;
            byte Ch(double x, double y) =>
                (byte)Math.Round(x * t + y * (1.0 - t));
            return Color.FromArgb(
                (byte)Math.Round(255.0 * alpha),
                Ch(a.R, b.R), Ch(a.G, b.G), Ch(a.B, b.B));
        }
    }
}
