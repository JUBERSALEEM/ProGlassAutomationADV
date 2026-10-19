using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ProGlassAutomation.Services.Theme
{
    public static partial class VisualThemeEngine
    {
        /// <summary>
        /// Applies the approved eight-family material layer after the legacy
        /// painter has seeded every required resource. This keeps the existing
        /// theme lifecycle, wallpaper pipeline, and DynamicResource contract
        /// intact while giving each approved family its own visual language.
        /// </summary>
        private static void ApplyEightThemeMaterial(ResourceDictionary resources, string family)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var palette = ResolveEightThemePalette(family, dark);
            if (palette == null) return;

            Brush canvas = palette.Atmospheric
                ? Gradient(palette.Canvas, palette.CanvasMid, palette.CanvasEnd)
                : Solid(palette.Canvas);
            Brush module = palette.Glass
                ? Alpha(palette.Surface, (byte)(dark ? 0xD9 : 0xE8))
                : Solid(palette.Surface);
            Brush raised = palette.Glass
                ? Alpha(palette.Raised, (byte)(dark ? 0xE8 : 0xF0))
                : Solid(palette.Raised);
            Brush inset = Solid(palette.Inset);
            Brush edge = Alpha(palette.Edge, (byte)(palette.Glass ? 0xB8 : 0xFF));

            resources["BackgroundBrush"] = canvas;
            resources["WindowBackgroundBrush"] = canvas;
            resources["ModuleBackgroundBrush"] = module;
            resources["ElevatedSurfaceBrush"] = raised;
            resources["SideMenuBackgroundBrush"] = raised;
            resources["MenuHoverBrush"] = Alpha(palette.Accent, (byte)0x28);
            resources["DialogSurfaceBrush"] = raised;
            resources["DialogInsetBrush"] = inset;
            resources["PrimaryForegroundBrush"] = Solid(palette.Ink);
            resources["SecondaryForegroundBrush"] = Solid(palette.SecondaryInk);
            resources["MutedForegroundBrush"] = Solid(palette.MutedInk);
            resources["BorderBrush"] = edge;
            resources["OutlineBrush"] = family == HolographicIridescent
                ? Gradient(palette.Edge, palette.Accent, palette.SecondAccent)
                : edge;
            resources["OutlineDefaultBrush"] = edge;
            resources["DividerBrush"] = Alpha(palette.Edge, (byte)0x9A);
            resources["GlassBorderSoft"] = Alpha(palette.Edge, (byte)0x75);
            resources["CardCornerRadius"] = new CornerRadius(palette.CardRadius);
            resources["CardCornerRadiusLarge"] = new CornerRadius(palette.CardRadius + 4);
            resources["ControlCornerRadius"] = new CornerRadius(palette.ControlRadius);
            // These resources feed Border.BorderThickness (a System.Windows.Thickness),
            // not a scalar Double. Publishing a Double here can survive theme setup and
            // crash later during Border.MeasureOverride when WPF unboxes the property.
            // Keep the family-specific values typed correctly at the source.
            double cardBorderWeight = family == SwissInternational ? 1.5 :
                family == Neumorphism || family == Claymorphism ? 0.0 : 1.0;
            double cardEdgeWeight = family == SwissInternational ? 1.5 : 1.0;
            resources["CardBorderThickness"] = new Thickness(cardBorderWeight);
            resources["CardEdgeThickness"] = new Thickness(cardEdgeWeight);

            // Reuse frozen, shared effect resources instead of creating a
            // per-card animation/effect. Material families get different
            // elevation profiles while retaining the existing performance gates.
            Color shadowColor = family == SpatialImmersive ||
                                family == HolographicIridescent ||
                                family == LiquidGlass ||
                                family == Glassmorphism
                ? palette.Accent
                : dark ? Rgb(0, 0, 0) : Rgb(35, 48, 68);
            double shadowOpacity = family switch
            {
                SwissInternational => 0.025,
                Neumorphism => dark ? 0.30 : 0.14,
                Claymorphism => 0.16,
                SpatialImmersive => 0.18,
                HolographicIridescent => 0.16,
                LiquidGlass => 0.13,
                Glassmorphism => 0.12,
                _ => 0.09
            };
            double shadowBlur = family switch
            {
                SwissInternational => 2,
                BentoGrid => 12,
                Neumorphism => 16,
                Claymorphism => 22,
                SpatialImmersive => 26,
                HolographicIridescent => 18,
                LiquidGlass => 20,
                Glassmorphism => 18,
                _ => 12
            };
            double shadowDepth = family switch
            {
                SwissInternational => 1,
                BentoGrid => 4,
                Neumorphism => 6,
                Claymorphism => 7,
                SpatialImmersive => 9,
                HolographicIridescent => 5,
                LiquidGlass => 7,
                Glassmorphism => 6,
                _ => 4
            };
            resources["ShadowSoft"] = CreateThemeShadow(shadowColor, shadowOpacity * 0.65, shadowBlur * 0.7, shadowDepth * 0.65);
            resources["ShadowElevated"] = CreateThemeShadow(shadowColor, shadowOpacity, shadowBlur, shadowDepth);
            resources["ShadowDeep"] = CreateThemeShadow(shadowColor, shadowOpacity * 0.8, shadowBlur * 1.2, shadowDepth * 1.25);
            resources["DashCardShadowEffect"] = CreateThemeShadow(shadowColor, shadowOpacity, shadowBlur, shadowDepth);
            resources["DashSubtleShadowEffect"] = CreateThemeShadow(shadowColor, shadowOpacity * 0.55, shadowBlur * 0.7, shadowDepth * 0.55);
            resources["DashInteractiveShadowEffect"] = CreateThemeShadow(palette.Accent, Math.Min(0.2, shadowOpacity), shadowBlur * 0.8, shadowDepth * 0.6);

            // The shared liquid-glass resource keys remain safe for every
            // theme. Only Liquid Glass receives translucent liquid fills;
            // glassmorphism gets a more opaque frost, and solid families keep
            // their own surfaces without accidentally inheriting blur.
            // Family material surfaces must be published into the legacy keys
            // consumed by the existing ERP card, dialog, dashboard and input styles.
            // Writing only WindowBackgroundBrush/ModuleBackgroundBrush made the
            // preview swatches change while most real cards continued to read the
            // identical surfaces seeded by ApplyIndustrial().
            Brush cardFace = palette.Glass
                ? Alpha(palette.Surface, (byte)(dark ? 0xC8 : 0xE0))
                : Solid(palette.Surface);
            Brush cardRaised = palette.Glass
                ? Alpha(palette.Raised, (byte)(dark ? 0xD8 : 0xE8))
                : Solid(palette.Raised);
            Brush cardInset = palette.Glass
                ? Alpha(palette.Inset, (byte)(dark ? 0xB8 : 0xD8))
                : Solid(palette.Inset);
            Brush cardEdge = family == HolographicIridescent
                ? Gradient(palette.Edge, palette.Accent, palette.SecondAccent)
                : edge;

            // These are the actual source tokens used by ThemeManager's semantic
            // surface composer. Populate them BEFORE that composer runs in
            // ThemePipeline.RebuildFamily, so its dynamic surface aliases inherit
            // the selected family instead of the generic industrial seed.
            resources["GlassUltraClearBrush"] = cardFace;
            resources["GlassSurfaceBrush"] = cardFace;
            resources["GlassCrystalBrush"] = cardRaised;
            resources["GlassFrostedBrush"] = cardFace;
            resources["GlassSurfaceStandard"] = cardInset;
            resources["GlassSurfaceUltra"] = cardRaised;
            resources["GlassSurfaceElevated"] = cardRaised;
            resources["GlassWaterBrush"] = family == LiquidGlass
                ? Alpha(palette.SecondAccent, (byte)(dark ? 0x55 : 0x35))
                : Solid(Colors.Transparent);
            resources["GlassBorderGradientCrystal"] = cardEdge;
            resources["GlassBorderSoft"] = Alpha(palette.Edge, (byte)(palette.Glass ? 0xB0 : 0xFF));
            resources["GlassBorderPrimary"] = edge;
            resources["GlassBorderBright"] = family == HolographicIridescent
                ? Gradient(Colors.White, palette.Accent, palette.SecondAccent)
                : edge;

            // Shell and dashboard styles still consume these compatibility keys.
            resources["ShellLiquidBrush"] = raised;
            resources["SidebarCardBrush"] = cardRaised;
            resources["DashLiquidSurfaceBrush"] = cardFace;
            resources["DashAmbientBrush"] = palette.Atmospheric
                ? Alpha(palette.Accent, (byte)(dark ? 0x25 : 0x18))
                : Solid(Colors.Transparent);
            resources["DashAmbientBlob1Brush"] = palette.Atmospheric
                ? Alpha(palette.SecondAccent, (byte)(dark ? 0x20 : 0x14))
                : Solid(Colors.Transparent);
            resources["DashAmbientBlob2Brush"] = palette.Atmospheric
                ? Alpha(palette.Accent, (byte)(dark ? 0x18 : 0x10))
                : Solid(Colors.Transparent);

            // The status-tint family is also visible across dashboards and lists.
            resources["TintIndigoBrush"] = Alpha(palette.Accent, (byte)(dark ? 0x38 : 0x20));
            resources["TintBlueBrush"] = Alpha(palette.SecondAccent, (byte)(dark ? 0x38 : 0x20));
            resources["TintGreenBrush"] = Alpha(Rgb(46, 184, 124), (byte)(dark ? 0x38 : 0x20));
            resources["TintRedBrush"] = Alpha(Rgb(225, 72, 77), (byte)(dark ? 0x38 : 0x20));
            resources["TintAmberBrush"] = Alpha(Rgb(230, 159, 58), (byte)(dark ? 0x38 : 0x20));
            resources["TintPurpleBrush"] = Alpha(palette.SecondAccent, (byte)(dark ? 0x38 : 0x20));
            resources["TintGrayBrush"] = Alpha(palette.SecondaryInk, (byte)(dark ? 0x38 : 0x20));

            // REAL SETTINGS / ABOUT CARD MATERIALS.
            // SettingsCardStyles.xaml does not consume the generic
            // Surface.Primary tokens; it binds these dedicated legacy keys.
            // Leaving them at the LightTheme/DarkTheme defaults was why the
            // theme preview changed while the actual Settings cards did not.
            // Publish family-owned resources directly so all Settings pages
            // repaint on every family or Light/Dark change.
            Brush settingsFace = family switch
            {
                SwissInternational => Solid(palette.Surface),
                BentoGrid => Solid(palette.Surface),
                Neumorphism => Solid(palette.Surface),
                Claymorphism => Gradient(palette.Surface, palette.Raised, palette.Surface),
                SpatialImmersive => Gradient(
                    Color.FromArgb(dark ? (byte)0xE8 : (byte)0xF3, palette.Surface.R, palette.Surface.G, palette.Surface.B),
                    Color.FromArgb(dark ? (byte)0xDA : (byte)0xE8, palette.Raised.R, palette.Raised.G, palette.Raised.B),
                    Color.FromArgb(dark ? (byte)0xE8 : (byte)0xF3, palette.Surface.R, palette.Surface.G, palette.Surface.B)),
                HolographicIridescent => Gradient(palette.Surface, palette.Raised, palette.Inset),
                LiquidGlass => Gradient(
                    Color.FromArgb(dark ? (byte)0xC8 : (byte)0xD8, palette.Surface.R, palette.Surface.G, palette.Surface.B),
                    Color.FromArgb(dark ? (byte)0xA8 : (byte)0xC0, Colors.White.R, Colors.White.G, Colors.White.B),
                    Color.FromArgb(dark ? (byte)0xD0 : (byte)0xE5, palette.Raised.R, palette.Raised.G, palette.Raised.B)),
                Glassmorphism => Gradient(
                    Color.FromArgb(dark ? (byte)0xC4 : (byte)0xD8, palette.Surface.R, palette.Surface.G, palette.Surface.B),
                    Color.FromArgb(dark ? (byte)0xB8 : (byte)0xCC, palette.Raised.R, palette.Raised.G, palette.Raised.B),
                    Color.FromArgb(dark ? (byte)0xC8 : (byte)0xDC, palette.Surface.R, palette.Surface.G, palette.Surface.B)),
                _ => Solid(palette.Surface)
            };
            Brush settingsEdge = family == HolographicIridescent
                ? Gradient(Colors.White, palette.Accent, palette.SecondAccent)
                : family == SwissInternational
                    ? Solid(palette.Accent)
                    : Gradient(palette.Edge, palette.Accent, palette.Edge);
            Brush settingsInnerEdge = family == SwissInternational
                ? Alpha(palette.Edge, (byte)0xB0)
                : family == Neumorphism
                    ? Alpha(palette.Edge, (byte)0x55)
                    : Alpha(palette.Edge, (byte)(palette.Glass ? 0x95 : 0xC0));
            resources["SettingsCardFaceBrush"] = settingsFace;
            resources["SettingsCardEdgeBrush"] = settingsEdge;
            resources["SettingsInnerEdgeBrush"] = settingsInnerEdge;
            resources["SettingsCardShadow"] = CreateThemeShadow(
                family == HolographicIridescent || family == LiquidGlass || family == Glassmorphism
                    ? palette.Accent
                    : dark ? Rgb(0, 0, 0) : palette.Edge,
                family == SwissInternational ? 0.025 :
                    family == Neumorphism ? (dark ? 0.16 : 0.09) :
                    family == Claymorphism ? 0.13 :
                    family == SpatialImmersive ? 0.12 :
                    family == HolographicIridescent ? 0.10 :
                    family == LiquidGlass ? 0.09 :
                    family == Glassmorphism ? 0.08 : 0.07,
                family == SwissInternational ? 2 :
                    family == BentoGrid ? 8 :
                    family == Neumorphism ? 12 :
                    family == Claymorphism ? 15 :
                    family == SpatialImmersive ? 16 :
                    family == HolographicIridescent ? 13 :
                    family == LiquidGlass ? 12 :
                    family == Glassmorphism ? 11 : 8,
                family == SwissInternational ? 1 :
                    family == BentoGrid ? 2 :
                    family == Neumorphism ? 4 :
                    family == Claymorphism ? 5 :
                    family == SpatialImmersive ? 5 :
                    family == HolographicIridescent ? 4 :
                    family == LiquidGlass ? 4 :
                    family == Glassmorphism ? 3 : 2);
            resources["Ios26CardShadow"] = resources["SettingsCardShadow"];
            resources["Ios26RadiusCard"] = new CornerRadius(palette.CardRadius);
            resources["Ios26RadiusInner"] = new CornerRadius(Math.Max(4, palette.CardRadius - 5));
            resources["Ios26RadiusTile"] = new CornerRadius(Math.Max(4, palette.CardRadius - 7));
            resources["Ios26RadiusChip"] = new CornerRadius(Math.Min(14, palette.ControlRadius));
            resources["Ios26RadiusSmall"] = new CornerRadius(Math.Min(8, palette.ControlRadius));
            resources["Ios26CardBrush"] = settingsFace;
            resources["Ios26TileBrush"] = cardRaised;
            resources["Ios26RailBrush"] = raised;
            resources["Ios26CardBorderBrush"] = settingsEdge;
            resources["Ios26TileBorderBrush"] = settingsInnerEdge;
            resources["Ios26HairlineBrush"] = Alpha(palette.Edge, (byte)0x75);
            resources["Ios26HoverBrush"] = Alpha(palette.Accent, (byte)(dark ? 0x35 : 0x22));
            resources["Ios26AccentBrush"] = Solid(palette.Accent);
            resources["Ios26AccentDeepBrush"] = Solid(palette.Edge);
            resources["Ios26AccentSoftBrush"] = Alpha(palette.Accent, (byte)(dark ? 0x50 : 0x30));
            resources["Ios26AccentSoftTextBrush"] = Solid(palette.Accent);
            resources["Ios26TextTitleBrush"] = Solid(palette.Ink);
            resources["Ios26TextBodyBrush"] = Solid(palette.Ink);
            resources["Ios26TextSecondaryBrush"] = Solid(palette.SecondaryInk);
            resources["Ios26TextMutedBrush"] = Solid(palette.MutedInk);

            resources["LiquidGlassBrush"] = family == LiquidGlass
                ? Gradient(
                    Color.FromArgb(dark ? (byte)0xA6 : (byte)0xC7,
                        palette.Raised.R, palette.Raised.G, palette.Raised.B),
                    Color.FromArgb(dark ? (byte)0x1A : (byte)0x76,
                        Colors.White.R, Colors.White.G, Colors.White.B))
                : Solid(palette.Raised);
            resources["LiquidGlassSubtleBrush"] = family == LiquidGlass || family == Glassmorphism
                ? Alpha(palette.Raised, (byte)(dark ? 0x92 : 0xD0))
                : Solid(palette.Surface);
            resources["LiquidGlassStrongBrush"] = family == LiquidGlass || family == Glassmorphism
                ? Alpha(palette.Raised, (byte)(dark ? 0xEA : 0xF2))
                : Solid(palette.Raised);
            resources["LiquidEdgeBrush"] = edge;
            resources["LiquidInnerHighlightBrush"] = Alpha(Colors.White, palette.Glass ? (byte)0x55 : (byte)0x00);
            resources["LiquidReflectionBrush"] = Alpha(palette.SecondAccent, palette.Glass ? (byte)0x35 : (byte)0x00);
            resources["LiquidControlHoverBrush"] = Alpha(palette.Accent, (byte)0x30);
            resources["LiquidControlPressedBrush"] = Alpha(palette.Accent, (byte)0x50);
            resources["LiquidControlDisabledBrush"] = Alpha(palette.SecondaryInk, (byte)0x35);
            resources["ProGlass.ThemeFamily"] = family;
            resources["ProGlass.ThemeMode"] = dark ? "Dark" : "Light";
        }

        private static EightThemePalette? ResolveEightThemePalette(string family, bool dark)
        {
            if (family != BentoGrid && family != SpatialImmersive &&
                family != SwissInternational && family != HolographicIridescent &&
                family != Neumorphism && family != Claymorphism &&
                family != LiquidGlass && family != Glassmorphism)
                return null;

            return family switch
            {
                BentoGrid => dark
                    ? new(Rgb(12, 18, 34), Rgb(20, 29, 51), Rgb(27, 39, 66), Rgb(15, 23, 42), Rgb(246, 249, 255), Rgb(188, 202, 225), Rgb(100, 120, 242), Rgb(64, 83, 127), Rgb(53, 184, 201), Rgb(34, 48, 76), 16, 12, false, false)
                    : new(Rgb(245, 247, 252), Rgb(255, 255, 255), Rgb(239, 243, 251), Rgb(229, 235, 247), Rgb(22, 30, 51), Rgb(78, 91, 119), Rgb(65, 84, 197), Rgb(172, 187, 219), Rgb(53, 184, 201), Rgb(215, 224, 242), 16, 12, false, false),
                SpatialImmersive => dark
                    ? new(Rgb(5, 16, 28), Rgb(10, 28, 43), Rgb(16, 42, 61), Rgb(7, 22, 35), Rgb(241, 251, 255), Rgb(171, 202, 215), Rgb(53, 184, 201), Rgb(48, 112, 135), Rgb(110, 125, 255), Rgb(23, 65, 86), 24, 15, true, true)
                    : new(Rgb(230, 243, 248), Rgb(248, 253, 255), Rgb(235, 247, 250), Rgb(219, 238, 244), Rgb(16, 42, 56), Rgb(69, 101, 116), Rgb(17, 120, 142), Rgb(139, 190, 205), Rgb(88, 111, 242), Rgb(202, 228, 236), 24, 15, true, true),
                SwissInternational => dark
                    ? new(Rgb(17, 18, 20), Rgb(25, 26, 28), Rgb(34, 35, 37), Rgb(21, 22, 24), Rgb(250, 249, 247), Rgb(194, 191, 185), Rgb(229, 72, 77), Rgb(99, 99, 100), Rgb(225, 225, 220), Rgb(43, 44, 46), 5, 4, false, false)
                    : new(Rgb(250, 249, 247), Rgb(255, 255, 255), Rgb(244, 242, 239), Rgb(235, 232, 228), Rgb(20, 22, 25), Rgb(83, 82, 79), Rgb(190, 45, 52), Rgb(178, 170, 160), Rgb(70, 70, 70), Rgb(222, 218, 213), 5, 4, false, false),
                HolographicIridescent => dark
                    ? new(Rgb(15, 10, 28), Rgb(25, 17, 43), Rgb(37, 24, 61), Rgb(20, 13, 36), Rgb(253, 248, 255), Rgb(197, 177, 219), Rgb(166, 108, 255), Rgb(127, 80, 175), Rgb(80, 205, 255), Rgb(60, 36, 92), 18, 13, true, true)
                    : new(Rgb(247, 243, 253), Rgb(255, 255, 255), Rgb(244, 237, 253), Rgb(235, 225, 250), Rgb(36, 23, 58), Rgb(91, 74, 115), Rgb(111, 55, 191), Rgb(186, 153, 225), Rgb(80, 205, 255), Rgb(223, 210, 245), 18, 13, true, true),
                Neumorphism => dark
                    ? new(Rgb(24, 31, 43), Rgb(27, 35, 48), Rgb(31, 40, 54), Rgb(22, 29, 40), Rgb(244, 247, 252), Rgb(181, 191, 207), Rgb(129, 149, 180), Rgb(52, 66, 87), Rgb(129, 149, 180), Rgb(18, 24, 34), 18, 14, false, false)
                    : new(Rgb(226, 232, 240), Rgb(226, 232, 240), Rgb(219, 226, 235), Rgb(210, 219, 231), Rgb(31, 41, 55), Rgb(71, 85, 105), Rgb(76, 98, 133), Rgb(190, 202, 218), Rgb(129, 149, 180), Rgb(198, 209, 224), 18, 14, false, false),
                Claymorphism => dark
                    ? new(Rgb(30, 24, 48), Rgb(39, 31, 61), Rgb(49, 38, 74), Rgb(34, 27, 54), Rgb(255, 250, 255), Rgb(204, 187, 225), Rgb(184, 166, 255), Rgb(111, 86, 151), Rgb(255, 191, 214), Rgb(72, 55, 103), 26, 18, false, false)
                    : new(Rgb(244, 240, 255), Rgb(249, 244, 255), Rgb(238, 231, 255), Rgb(229, 219, 250), Rgb(45, 33, 75), Rgb(102, 86, 135), Rgb(119, 94, 200), Rgb(195, 177, 235), Rgb(255, 191, 214), Rgb(217, 205, 244), 26, 18, false, false),
                LiquidGlass => dark
                    ? new(Rgb(6, 19, 32), Rgb(10, 29, 46), Rgb(17, 43, 62), Rgb(9, 25, 40), Rgb(244, 252, 255), Rgb(172, 205, 219), Rgb(26, 197, 226), Rgb(62, 123, 145), Rgb(34, 205, 189), Rgb(27, 65, 88), 26, 16, true, true)
                    : new(Rgb(222, 240, 248), Rgb(248, 253, 255), Rgb(233, 246, 252), Rgb(222, 240, 248), Rgb(13, 39, 53), Rgb(63, 94, 109), Rgb(12, 110, 138), Rgb(153, 204, 220), Rgb(34, 205, 189), Rgb(207, 231, 242), 26, 16, true, true),
                Glassmorphism => dark
                    ? new(Rgb(8, 18, 34), Rgb(15, 30, 50), Rgb(23, 43, 66), Rgb(10, 23, 40), Rgb(244, 250, 255), Rgb(177, 201, 221), Rgb(72, 169, 232), Rgb(65, 109, 145), Rgb(139, 111, 241), Rgb(39, 69, 97), 22, 15, true, true)
                    : new(Rgb(231, 241, 250), Rgb(249, 252, 255), Rgb(235, 245, 252), Rgb(222, 237, 248), Rgb(15, 41, 64), Rgb(66, 95, 117), Rgb(29, 107, 166), Rgb(148, 190, 216), Rgb(139, 111, 241), Rgb(207, 228, 242), 22, 15, true, true),
                _ => null
            };
        }

        private static DropShadowEffect CreateThemeShadow(Color color, double opacity, double blur, double depth)
        {
            var effect = new DropShadowEffect
            {
                Color = color,
                Opacity = Math.Clamp(opacity, 0, 0.35),
                BlurRadius = Math.Clamp(blur, 0, 28),
                ShadowDepth = Math.Clamp(depth, 0, 12),
                Direction = 270,
                RenderingBias = RenderingBias.Performance
            };
            effect.Freeze();
            return effect;
        }

        private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

        private static SolidColorBrush Solid(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static SolidColorBrush Alpha(Color color, byte alpha)
        {
            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            brush.Freeze();
            return brush;
        }

        private static LinearGradientBrush Gradient(Color start, Color end) =>
            Gradient(start, end, end);

        private static LinearGradientBrush Gradient(Color start, Color middle, Color end)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(start, 0),
                    new GradientStop(middle, 0.52),
                    new GradientStop(end, 1)
                }
            };
            brush.Freeze();
            return brush;
        }

        private sealed record EightThemePalette(
            Color Canvas, Color Surface, Color Raised, Color Inset,
            Color Ink, Color SecondaryInk, Color Accent, Color Edge,
            Color SecondAccent, Color CanvasEnd, double CardRadius,
            double ControlRadius, bool Atmospheric, bool Glass)
        {
            public Color MutedInk => ColorMath.Mix(SecondaryInk, Canvas, 0.22);

            public Color CanvasMid => Color.FromRgb(
                (byte)((Canvas.R + Surface.R) / 2),
                (byte)((Canvas.G + Surface.G) / 2),
                (byte)((Canvas.B + Surface.B) / 2));
        }
    }
}
