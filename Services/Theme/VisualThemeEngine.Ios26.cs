// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/VisualThemeEngine.Ios26.cs
// PROJECT: ProGlassAutomation
//
// iOS 26 LIQUID GLASS family (2026-09-10, owner directive):
//   "include a new theme ios 26 so current theme restore to normal and
//    all these redesign move to ios 26 theme — match cards background
//    shadow wallpaper everything same in ios26 theme."
//
// The About redesign (frosted slabs, pastel/aurora wallpaper, floating
// capsules, vivid icon gradients, stat chips, shadows) is owned by THIS
// family. Every About.* token has its NORMAL value in the Light/Dark
// theme dictionaries; ApplyIos26 overrides them here, and the switch-away
// removal loop in Part1 deletes them — so selecting any other family
// restores the classic look EXACTLY.
//
// Pattern-matched on the established family hooks (Crystal Fluid gate +
// removal contract). Never throws; brushes are frozen.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme
{
    public static partial class VisualThemeEngine
    {
        /// <summary>iOS 26 Liquid Glass — the full About redesign family.</summary>
        public const string Ios26 = "iOS 26";

        /// <summary>Keys ApplyIos26 publishes; removed on every family
        /// switch (see Part1) so non-iOS26 families see their own defaults.</summary>
        private static readonly string[] Ios26TokenKeys =
        {
            "AboutWallpaperBrush", "AboutGlassSlabBrush", "AboutGlassPanelBrush",
            "AboutGlassSlabStroke", "AboutGlassHairlineStroke",
            "AboutGlassCapsuleBrush", "AboutGlassCaptionBrush",
            "AboutGlassGlowBrush", "AboutCardCornerRadius",
            "AboutPanelCornerRadius", "AboutGlassChromeVisibility",
            "AboutCardEffect",
        };

        private static void ApplyIos26(ResourceDictionary resources)
        {
            bool dark = ProGlassAutomation.Services.Theme.ThemeManager
                .CurrentTheme.ToString().Contains("Dark");

            static LinearGradientBrush Lin(string c0, string c1, string? c2 = null)
            {
                var b = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1)
                };
                b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c0), 0));
                b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c1), 1));
                if (c2 != null)
                {
                    b.GradientStops[1].Offset = 0.55;
                    b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c2), 1));
                }
                b.Freeze();
                return b;
            }

            static SolidColorBrush Sol(string hex)
            {
                var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                b.Freeze();
                return b;
            }

            // Stage wallpaper — light: electric pastel mesh; dark: charcoal aurora.
            resources["AboutWallpaperBrush"] = dark
                ? Lin("#0B0D13", "#141322", "#1D1032")
                : Lin("#C7DBFF", "#E4D6FF", "#FFD9E8");

            // Frosted slabs (light: white frost / dark: smoked glass).
            resources["AboutGlassSlabBrush"] = dark
                ? Lin("#33FFFFFF", "#17FFFFFF")
                : Lin("#F8FFFFFF", "#D2FFFFFF");
            resources["AboutGlassPanelBrush"] = dark
                ? Lin("#2BFFFFFF", "#12FFFFFF")
                : Lin("#EAFFFFFF", "#C8FFFFFF");
            resources["AboutGlassCapsuleBrush"] = dark
                ? Lin("#38FFFFFF", "#1FFFFFFF")
                : Lin("#FBFFFFFF", "#E0FFFFFF");

            // Rim light + hairlines.
            resources["AboutGlassSlabStroke"] = Sol(dark ? "#3DF2F6FF" : "#EAFFFFFF");
            resources["AboutGlassHairlineStroke"] = Sol(dark ? "#24F2F6FF" : "#B3F0F4FF");

            // Text + glows.
            resources["AboutGlassCaptionBrush"] = Sol(dark ? "#AEB7C4" : "#6E6E73");
            resources["AboutGlassGlowBrush"] = Sol(dark ? "#1AFFFFFF" : "#66FFFFFF");

            // Geometry — the iOS rounded-20 slab language.
            resources["AboutCardCornerRadius"] = new CornerRadius(20);
            resources["AboutPanelCornerRadius"] = new CornerRadius(16);

            // iOS chrome (capsule wrapper, chevrons, stat chips) visible ONLY here.
            resources["AboutGlassChromeVisibility"] = Visibility.Visible;

            // Soft card shadow — SMALL radius (12): the 2026-08-25 freeze was
            // BlurRadius 28 across ~20 cards; 12 at Depth 3 is ~3× cheaper and
            // scoped to the iOS 26 family only.
            var shadow = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12,
                ShadowDepth = 3,
                Direction = 270,
                Opacity = dark ? 0.45 : 0.18,
                Color = dark
                    ? Colors.Black
                    : (Color)ColorConverter.ConvertFromString("#1E293B")
            };
            shadow.Freeze();
            resources["AboutCardEffect"] = shadow;
        }
    }
}
