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

            // The visible 1px card keyline (no shadows, no glow — this is
            // the industrial signature separator).
            ApplyKeyline(resources, keyline);

            AppLogService.Debug(
                $"[ThemeEngine] Industrial family painted: {familyLabel} " +
                $"(dark={dark})",
                new Logging.LogContext { Module = "Themes", Operation = "Industrial" });
        }

        private static Color ParseColor(string hex)
        {
            var c = ColorConverter.ConvertFromString(hex);
            return c is Color col ? col : Colors.Magenta;
        }
    }
}
