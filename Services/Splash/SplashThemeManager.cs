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
    public record SplashStyleOption(int Id, string Name, string Description, string AccentHex)
    {
        // ComboBox item templates that ignore DisplayMemberPath fall back to
        // ToString(). A record's generated ToString is the whole payload
        // ("SplashStyleOption { Id = 1, Name = ... }"), which is what the
        // splash card was showing instead of the style name.
        public override string ToString() => Name;
    }

    public record SplashStyleTheme(
        int Id,
        string Name,
        string Subtitle,
        string StatusText,
        string CanvasBgHex,
        string PlateBgHex,
        string TitleForegroundHex,
        string SubtitleForegroundHex,
        string AccentHex,
        string KeylineHex,
        bool IsSerif,
        bool ShowStar,
        bool IsDark
    );

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
            new SplashStyleOption(9, "Glass Prism Optical Bevel", "Thick architectural float glass plate with 3D optical bevels and multi-faceted light dispersions (Default)", "#0F172A"),
            new SplashStyleOption(10, "Minimalist Indigo Monogram", "Ultra-clean modern silk canvas with centered indigo monogram squircle and circular arc", "#4F46E5")
        };

        private static readonly Dictionary<int, SplashStyleTheme> Themes = new()
        {
            [1] = new SplashStyleTheme(
                1, "Cinematic Ambient Mesh", "Splash 01: Cinematic Ambient Mesh",
                "Initializing Ambient Mesh & Services... 12%",
                "#0B1120", "#1E293B", "#F8FAFC", "#94A3B8", "#38BDF8", "#334155", false, true, true),

            [2] = new SplashStyleTheme(
                2, "Crystal Glassmorphism", "Splash 02: Crystal Glassmorphism",
                "Refracting Optical Modules... 24%",
                "#0A1220", "#132238", "#F0F9FF", "#93C5FD", "#60A5FA", "#1E3A8A", false, true, true),

            [3] = new SplashStyleTheme(
                3, "Executive Minimalist", "Splash 03: Executive Minimalist",
                "Calibrating Surgical Kernels... 31%",
                "#0D1117", "#161B22", "#FFFFFF", "#8B949E", "#22D3EE", "#30363D", false, false, true),

            [4] = new SplashStyleTheme(
                4, "Industrial CAD Blueprint", "Splash 04: Industrial CAD Blueprint",
                "Loading Vector Dimension Guides... 45%",
                "#081424", "#0D1F38", "#E0F2FE", "#7DD3FC", "#0EA5E9", "#1E40AF", false, true, true),

            [5] = new SplashStyleTheme(
                5, "iOS26 Liquid Stage", "Splash 05: iOS26 Liquid Stage",
                "Synthesizing Fluid Light Fields... 58%",
                "#0F0E1C", "#1E1B38", "#FAF5FF", "#C084FC", "#818CF8", "#4C1D95", false, true, true),

            [6] = new SplashStyleTheme(
                6, "Cyber Industrial Monolith", "Splash 06: Cyber Industrial Monolith",
                "Engaging Neural Matrix... 67%",
                "#04080E", "#0B121C", "#ECFEFF", "#67E8F9", "#06B6D4", "#155E75", false, false, true),

            [7] = new SplashStyleTheme(
                7, "Nordic Frosted Slate", "Splash 07: Nordic Frosted Slate",
                "Mounting Fjord Architecture... 79%",
                "#0F171E", "#18222C", "#F1F5F9", "#94A3B8", "#38BDF8", "#334155", false, true, true),

            [8] = new SplashStyleTheme(
                8, "Dynamic Aurora Gradient", "Splash 08: Dynamic Aurora Gradient",
                "Streaming Atmospheric Caustics... 88%",
                "#06181C", "#0E282E", "#ECFDF5", "#6EE7B7", "#34D399", "#065F46", false, true, true),

            [9] = new SplashStyleTheme(
                9, "Glass Prism Optical Bevel", "Splash 09: Glass Prism Optical Bevel",
                "Initializing Precision Modules... 18%",
                "#E2E8F0", "#F4F8FC", "#0F172A", "#475569", "#0F172A", "#94A3B8", true, true, false),

            [10] = new SplashStyleTheme(
                10, "Minimalist Indigo Monogram", "Splash 10: Minimalist Indigo Monogram",
                "Synchronizing Workspace Tokens... 95%",
                "#F8FAFC", "#FFFFFF", "#1E1B4B", "#6366F1", "#4F46E5", "#CBD5E1", false, true, false)
        };

        /// <summary>
        /// Retrieves the comprehensive theme configuration for a given splash style ID (defaults to Style 9).
        /// </summary>
        public static SplashStyleTheme GetThemeConfig(int styleId)
        {
            if (Themes.TryGetValue(styleId, out var theme))
                return theme;
            return Themes[9]; // Master default: Glass Prism Optical Bevel
        }

        /// <summary>
        /// Determines the active splash style for this boot cycle,
        /// incrementing and persisting the cycle counter when Auto is selected.
        /// </summary>
        public static int ResolveActiveStyle(AppConfig? config)
        {
            if (config?.Appearance == null) return 9; // Approved default

            int style = config.Appearance.SplashStyle;
            if (style >= 1 && style <= 10)
                return style;

            // Cycle mode (0 = Auto): cycle sequentially through 1..10
            int last = config.Appearance.LastBootSplashIndex;
            int next;
            if (last < 1 || last >= 10)
            {
                next = 1;
            }
            else
            {
                next = last + 1;
            }

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
                1 => (Color.FromRgb(11, 17, 32), Color.FromRgb(30, 41, 59), Color.FromRgb(15, 23, 42), Color.FromRgb(56, 189, 248)),
                2 => (Color.FromRgb(10, 18, 32), Color.FromRgb(19, 34, 56), Color.FromRgb(24, 44, 76), Color.FromRgb(96, 165, 250)),
                3 => (Color.FromRgb(13, 17, 23), Color.FromRgb(22, 27, 34), Color.FromRgb(18, 24, 33), Color.FromRgb(34, 211, 238)),
                4 => (Color.FromRgb(8, 20, 36), Color.FromRgb(13, 31, 56), Color.FromRgb(15, 37, 65), Color.FromRgb(14, 165, 233)),
                5 => (Color.FromRgb(15, 14, 28), Color.FromRgb(30, 27, 56), Color.FromRgb(35, 30, 60), Color.FromRgb(129, 140, 248)),
                6 => (Color.FromRgb(4, 8, 14), Color.FromRgb(11, 18, 28), Color.FromRgb(10, 20, 30), Color.FromRgb(6, 182, 212)),
                7 => (Color.FromRgb(15, 23, 30), Color.FromRgb(24, 34, 44), Color.FromRgb(30, 44, 58), Color.FromRgb(56, 189, 248)),
                8 => (Color.FromRgb(6, 24, 28), Color.FromRgb(14, 40, 46), Color.FromRgb(20, 48, 54), Color.FromRgb(52, 211, 153)),
                9 => (Color.FromRgb(226, 232, 240), Color.FromRgb(244, 248, 252), Color.FromRgb(203, 213, 225), Color.FromRgb(15, 23, 42)),
                10 => (Color.FromRgb(248, 250, 252), Color.FromRgb(255, 255, 255), Color.FromRgb(226, 232, 240), Color.FromRgb(79, 70, 229)),
                _ => (Color.FromRgb(226, 232, 240), Color.FromRgb(244, 248, 252), Color.FromRgb(203, 213, 225), Color.FromRgb(15, 23, 42))
            };
        }
    }
}
