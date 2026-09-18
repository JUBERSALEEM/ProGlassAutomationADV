// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Splash/SplashThemeManager.cs
// PROJECT: ProGlassAutomation
// MODULE: Splash — Multi-Theme Visual Pipeline & Boot Rotator
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Splash
{
    public record SplashStyleOption(int Id, string Name, string Description, string AccentHex);

    public static class SplashThemeManager
    {
        public static readonly IReadOnlyList<SplashStyleOption> AvailableStyles = new[]
        {
            new SplashStyleOption(0, "Auto (Cycle on Boot)", "Rotates sequentially through all 10 corporate styles on every launch", "#38BDF8"),
            new SplashStyleOption(1, "Cinematic Ambient Mesh", "Deep dusk-navy canvas with celestial constellation lines and rotating orbit ring", "#38BDF8"),
            new SplashStyleOption(2, "Crystal Glassmorphism", "Liquid glass stage with refractive frosted acrylic panels and sapphire depth", "#60A5FA"),
            new SplashStyleOption(3, "Executive Minimalist", "Precision dark charcoal canvas with surgical typography and razor LED progress", "#22D3EE"),
            new SplashStyleOption(4, "Industrial CAD Blueprint", "Architectural blueprint wireframes with precision vector cutting dimension guides", "#0EA5E9"),
            new SplashStyleOption(5, "iOS26 Liquid Stage", "Organic fluid lighting orbs with double specular edge sheen and rounded cards", "#818CF8"),
            new SplashStyleOption(6, "Cyber Industrial Monolith", "Pitch OLED dark monolith with technical guides and terminal diagnostic meter", "#06B6D4"),
            new SplashStyleOption(7, "Nordic Frosted Slate", "Matte frosted fjord slate surfaces with crisp electric blue accents", "#38BDF8"),
            new SplashStyleOption(8, "Dynamic Aurora Gradient", "Sweeping emerald, cyan, and indigo atmospheric light ribbons across the stage", "#34D399"),
            new SplashStyleOption(9, "High-Density Operations", "Mission-critical glass plant operations chassis with real-time subsystem telemetry", "#F59E0B"),
            new SplashStyleOption(10, "Glass Mural Chassis", "Optical beveled glass shields with realistic refractive highlights", "#A78BFA")
        };

        /// <summary>
        /// Determines the active splash style for this boot cycle,
        /// incrementing and persisting the cycle counter when Auto is selected.
        /// </summary>
        public static int ResolveActiveStyle(AppConfig config)
        {
            if (config?.Appearance == null) return 1;

            int style = config.Appearance.SplashStyle;
            if (style >= 1 && style <= 10)
                return style;

            // Cycle mode: pick next 1..10
            int next = config.Appearance.LastBootSplashIndex + 1;
            if (next > 10 || next < 1) next = 1;

            config.Appearance.LastBootSplashIndex = next;
            try
            {
                ConfigService.SaveDebounced(config);
            }
            catch { /* never fail startup because of splash preference save */ }

            return next;
        }

        /// <summary>
        /// Returns gradient stops and theme brush colors tailored for the active style.
        /// </summary>
        public static (Color Top, Color Mid, Color Bottom, Color Accent) GetPaletteForStyle(int style)
        {
            return style switch
            {
                2 => (Color.FromRgb(10, 18, 32), Color.FromRgb(16, 30, 54), Color.FromRgb(24, 44, 76), Color.FromRgb(96, 165, 250)),
                3 => (Color.FromRgb(13, 17, 23), Color.FromRgb(18, 24, 33), Color.FromRgb(22, 27, 34), Color.FromRgb(34, 211, 238)),
                4 => (Color.FromRgb(8, 20, 36), Color.FromRgb(12, 28, 48), Color.FromRgb(15, 37, 65), Color.FromRgb(14, 165, 233)),
                5 => (Color.FromRgb(15, 14, 28), Color.FromRgb(24, 22, 44), Color.FromRgb(35, 30, 60), Color.FromRgb(129, 140, 248)),
                6 => (Color.FromRgb(4, 8, 14), Color.FromRgb(8, 14, 22), Color.FromRgb(10, 20, 30), Color.FromRgb(6, 182, 212)),
                7 => (Color.FromRgb(15, 23, 30), Color.FromRgb(22, 33, 44), Color.FromRgb(30, 44, 58), Color.FromRgb(56, 189, 248)),
                8 => (Color.FromRgb(6, 24, 28), Color.FromRgb(12, 36, 44), Color.FromRgb(20, 48, 54), Color.FromRgb(52, 211, 153)),
                9 => (Color.FromRgb(18, 20, 26), Color.FromRgb(24, 28, 38), Color.FromRgb(32, 36, 48), Color.FromRgb(245, 158, 11)),
                10 => (Color.FromRgb(20, 16, 32), Color.FromRgb(30, 24, 48), Color.FromRgb(42, 32, 66), Color.FromRgb(167, 139, 250)),
                _ => (Color.FromRgb(11, 17, 30), Color.FromRgb(15, 23, 42), Color.FromRgb(30, 41, 59), Color.FromRgb(56, 189, 248))
            };
        }
    }
}
