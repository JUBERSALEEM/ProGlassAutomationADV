// ═══════════════════════════════════════════════════════════════════════
// FILE: ThemeManager.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Engine;
using ProGlassAutomation.Services.AI.AIConfig;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Services
{
        public static partial class ThemeManager
    {
            public static double CardBorderThickness => _cardBorderThickness;

        /// <summary>
        /// Applies a uniform border thickness to every card / panel via the
        /// shared CardBorderThickness theme resource. Cards reference it with
        /// {StaticResource CardBorderThickness}, so one call restyles the whole
        /// app instead of editing hundreds of individual card declarations.
        /// </summary>
        public static void ApplyCardBorderThickness(double thickness)
        {
            if (thickness < 0.0) thickness = 0.0;
            _cardBorderThickness = thickness;

            var resources = Application.Current?.Resources;
            if (resources == null) return;

            resources["CardBorderThickness"] = new Thickness(thickness);
            ApplyDynamicBorderPrimitives(resources, thickness);
            if (!_definedOutline)
            {
                resources["CardEdgeThickness"] = new Thickness(thickness);
                ApplyCardEdgePaddingResources(thickness);
            }
        }

        /// <summary>
        /// Re-applies the saved card border thickness. Called after a theme
        /// swap because loading a new dictionary restores that theme's default.
        /// </summary>
        public static void ReapplyCardBorderThickness()
            => ApplyCardBorderThickness(_cardBorderThickness);

        private static double _cardEdgeThickness = 2.0;

        /// <summary>Current global card EDGE weight in DIPs (1–100).</summary>
        public static double CardEdgeThickness => _cardEdgeThickness;

        /// <summary>
        /// Applies the defined card-edge weight (Settings → Appearance →
        /// Define Card Edges) to every card surface via the CardEdgeThickness
        /// theme resource. Separate from CardBorderThickness (the subtle
        /// input/chrome hairline) so a bold industrial edge never inflates
        /// text boxes and gridlines.
        /// </summary>
        public static void ApplyCardEdgeThickness(double thickness)
        {
            if (thickness < 0.0) thickness = 0.0;
            _cardEdgeThickness = thickness;

            var resources = Application.Current?.Resources;
            if (resources == null) return;

            var effective = _definedOutline ? thickness : _cardBorderThickness;
            resources["CardEdgeThickness"] = new Thickness(effective);
            resources["Border.StrongThickness"] = new Thickness(Math.Max(1.0, effective));
            ApplyCardEdgePaddingResources(effective);
        }

        private static void ApplyCardEdgePaddingResources(double effectiveEdgeThickness)
        {
            var resources = Application.Current?.Resources;
            if (resources == null) return;

            double extra = Math.Max(0, effectiveEdgeThickness - 2.0);
            resources["CardEdgePaddingBase"] = new Thickness(24 + extra);
            resources["CardEdgePaddingStat"] = new Thickness(18 + extra, 16 + extra, 18 + extra, 16 + extra);
            resources["CardEdgePaddingTask"] = new Thickness(18 + extra, 14 + extra, 18 + extra, 14 + extra);
            resources["CardEdgePaddingList"] = new Thickness(16 + extra, 12 + extra, 16 + extra, 12 + extra);
        }

        /// <summary>Re-applies the saved card-edge weight after a theme swap.</summary>
        public static void ReapplyCardEdgeThickness()
            => ApplyCardEdgeThickness(_cardEdgeThickness);

        private static double _cardCornerRadius = 12;

        /// <summary>Current global card corner radius in DIPs.</summary>
        public static double CardCornerRadius => _cardCornerRadius;

        /// <summary>
        /// Applies one corner radius to every card, input and button via
        /// CardCornerRadius / ControlCornerRadius / CardCornerRadiusLarge.
        /// All three are the SAME value so nothing looks "more round"
        /// than the Appearance slider (default 12).
        /// </summary>
        public static void ApplyCardCornerRadius(double radius)
        {
            // 2026-09-16 (owner report: "cards corner isn't applying in all
            // Settings module cards, it's only getting applied in the sidebar
            // open/close bar"). This method is the app's ONE radius authority:
            // it writes CardCornerRadius and then republishes the derived
            // Ios26Radius{Card,Inner,Tile,Chip,Small} scale, which is what
            // every Settings card and inner tile actually reads. The
            // Appearance slider allowed 0, and 0 propagated to the whole
            // scale — so every card went square while the one element that
            // used a STATIC radius token (the rail grab-tab's
            // Radius.Right.Medium) stayed rounded. That contrast is what made
            // it look like the corners "weren't applying" to cards at all.
            // A card with no radius and a 1.5px edge reads as an unstyled box,
            // not as a design choice, so 4 is the floor: the smallest radius
            // that still reads as deliberately rounded at 1x DPI, and it
            // yields Ios26RadiusCard >= 9. Negative and above-28 values are
            // clamped as before.
            if (radius < 4.0) radius = 4.0;
            if (radius > 28.0) radius = 28.0;
            _cardCornerRadius = radius;

            var resources = Application.Current?.Resources;
            if (resources == null) return;

            var cr = new CornerRadius(radius);
            resources["CardCornerRadius"] = cr;
            resources["ControlCornerRadius"] = cr;
            resources["CardCornerRadiusLarge"] = cr;
            ApplyDynamicRadiusPrimitives(resources, radius);

            // 2026-09-16 (owner report: "about page cards still not accepting
            // dynamic card corner … from monet engine"): the About module's
            // radii were local constants inside Views/About, so the cards
            // stayed at the mockup's 26 px no matter what this slider — the
            // ONE radius authority in the app — was set to. Republish the
            // About scale from the value just written; the ratios are the
            // mockup's own, so the default still renders the approved design.
            AboutSurfaceTokens.Publish(resources, radius);
        }

        /// <summary>
        /// Re-applies the saved card corner radius after a theme swap.
        /// </summary>
        public static void ReapplyCardCornerRadius()
            => ApplyCardCornerRadius(_cardCornerRadius);

        private static string _displayDensity = "Compact";

        public static string DisplayDensity => _displayDensity;

        /// <summary>
        /// One switch restyles shared chrome for laptop vs desktop:
        /// input/button height, title size, KPI size, card padding.
        /// Views bind these tokens — do not edit each screen.
        /// </summary>
        public static void ApplyDisplayDensity(string density)
        {
            string key = (density ?? "Compact").Trim();
            if (key is not ("Compact" or "Normal" or "Comfortable" or "Spacious"))
                key = "Compact";
            _displayDensity = key;

            var resources = Application.Current?.Resources;
            if (resources == null) return;

            double inputH, buttonH, rowH, title, kpi, body, buttonMin;
            Thickness cardPad, stripPad, pageMargin;

            switch (key)
            {
                case "Spacious":
                    inputH = 42; buttonH = 44; rowH = 38;
                    title = 24; kpi = 22; body = 14; buttonMin = 120;
                    cardPad = new Thickness(20, 14, 20, 14);
                    stripPad = new Thickness(22, 14, 22, 14);
                    pageMargin = new Thickness(22, 16, 22, 16);
                    break;
                case "Comfortable":
                    inputH = 38; buttonH = 40; rowH = 34;
                    title = 22; kpi = 20; body = 14; buttonMin = 110;
                    cardPad = new Thickness(16, 12, 16, 12);
                    stripPad = new Thickness(18, 12, 18, 12);
                    pageMargin = new Thickness(18, 12, 18, 12);
                    break;
                case "Normal":
                    inputH = 34; buttonH = 36; rowH = 30;
                    title = 20; kpi = 18; body = 13; buttonMin = 96;
                    cardPad = new Thickness(12, 9, 12, 9);
                    stripPad = new Thickness(14, 9, 14, 9);
                    pageMargin = new Thickness(14, 10, 14, 10);
                    break;
                default: // Compact — default, slightly tighter than before
                    inputH = 28; buttonH = 30; rowH = 26;
                    title = 17; kpi = 16; body = 13; buttonMin = 84;
                    cardPad = new Thickness(8, 6, 8, 6);
                    stripPad = new Thickness(10, 6, 10, 6);
                    pageMargin = new Thickness(10, 6, 10, 6);
                    break;
            }

            resources["DensityInputHeight"] = inputH;
            resources["DensityButtonHeight"] = buttonH;
            resources["DensityRowHeight"] = rowH;
            resources["DensityTitleFontSize"] = title;
            resources["DensityKpiValueFontSize"] = kpi;
            resources["DensityBodyFontSize"] = body;
            resources["DensityButtonMinWidth"] = buttonMin;
            resources["DensityCardPadding"] = cardPad;
            resources["DensityStripPadding"] = stripPad;
            resources["DensityPageMargin"] = pageMargin;
            resources["Density.ControlHeight"] = inputH;
            resources["Density.RowHeight"] = rowH;
            resources["Density.TitleSize"] = title;
            resources["Density.CardPadding"] = cardPad;
            resources["Density.PagePadding"] = pageMargin;
            resources["ControlHeightSmall"] = inputH;
            resources["ControlHeightMedium"] = buttonH;
            resources["ControlHeightLarge"] = buttonH + 4;
            resources["TypographyBodySize"] = body;
            resources["TypographyKpiValueSize"] = kpi;
            resources["TypographyTitleSize"] = title;
            resources["CardPadding"] = cardPad;
            resources["PagePadding"] = pageMargin;

            // ── DASHBOARD DENSITY (2026-09-13): Db* tokens now follow Density
            // so dashboards scale with Compact/Normal/Comfortable/Spacious.
            // Previously DbPageMargin=20,16,20,20 was fixed, so Compact mode
            // had no effect on dashboards.
            double gutter;
            double kpiMinH;
            double chartMinH;
            switch (key)
            {
                case "Spacious":
                    gutter = 14; kpiMinH = 110; chartMinH = 240; break;
                case "Comfortable":
                    gutter = 12; kpiMinH = 100; chartMinH = 212; break;
                case "Normal":
                    gutter = 10; kpiMinH = 90; chartMinH = 200; break;
                default:
                    gutter = 8; kpiMinH = 80; chartMinH = 180; break;
            }
            // Dashboard cards are intentionally a touch larger than module cards
            var dbCardPad = new Thickness(cardPad.Left + 6, cardPad.Top + 7, cardPad.Right + 6, cardPad.Bottom + 7);
            var dbTilePad = new Thickness(stripPad.Left, stripPad.Top + 2, stripPad.Right, stripPad.Bottom + 2);
            resources["DbPageMargin"] = pageMargin;
            resources["DbCardPadding"] = dbCardPad;
            resources["DbTilePadding"] = dbTilePad;
            resources["DbGutter"] = gutter;
            resources["DbGutterHalfMargin"] = new Thickness(gutter / 2.0);
            resources["DbKpiTileMinHeight"] = kpiMinH;
            resources["DbChartMinHeight"] = chartMinH;

            // ProGlassLayout.* canonical density vocabulary (LayoutTokens.xaml
            // holds only concrete fallbacks — live density must come from here
            // so new modules consuming the canonical names still reflow).
            resources["ProGlassLayout.RowHeight"] = rowH;
            resources["ProGlassLayout.InputHeight"] = inputH;
            resources["ProGlassLayout.ButtonHeight"] = buttonH;
            resources["ProGlassLayout.ButtonMinWidth"] = buttonMin;
            resources["ProGlassLayout.CardPadding"] = cardPad;
            resources["ProGlassLayout.SectionPadding"] = stripPad;
            resources["ProGlassLayout.PageMargin"] = pageMargin;

            // ── ABOUT TYPE SCALE (2026-09-15): the About module consumes
            // {DynamicResource Ios26FontSize*} for every size on the page.
            // Those tokens are declared at APPLICATION level in
            // Themes/Styles/AboutTypeScale.xaml — deliberately not inside
            // Views/About, because a locally merged key shadows
            // Application.Resources and cannot be rewritten from here. This
            // is what makes the About typography follow density instead of
            // being frozen to the Compact ladder.
            //
            // Compact is the default profile and reproduces the approved
            // mockup scale exactly (lift 0 → nothing moves on screen);
            // Normal / Comfortable / Spacious lift the sizes >= 13 px by
            // 1–2 whole pixels. See Services/Theme/AboutDensityTypeScale.cs.
            AboutDensityTypeScale.Publish(resources, key);

            ApplySemanticSurfacePadding(resources, cardPad, stripPad);
            PublishProGlassDesignSystem();
        }


        private static void ApplyDynamicBorderPrimitives(ResourceDictionary resources, double thickness)
        {
            var hair = new Thickness(Math.Max(0.5, Math.Min(4.0, thickness)));
            resources["Border.HairlineThickness"] = hair;
            resources["Border.TopHairlineThickness"] = new Thickness(0, hair.Top, 0, 0);
            resources["Border.BottomHairlineThickness"] = new Thickness(0, 0, 0, hair.Bottom);
            resources["Border.LeftHairlineThickness"] = new Thickness(hair.Left, 0, 0, 0);
            resources["Border.RightHairlineThickness"] = new Thickness(0, 0, hair.Right, 0);
            resources["Border.VerticalHairlineThickness"] = new Thickness(hair.Left, 0, hair.Right, 0);
            resources["Border.HorizontalHairlineThickness"] = new Thickness(0, hair.Top, 0, hair.Bottom);
            resources["Border.BoxNoLeftThickness"] = new Thickness(0, hair.Top, hair.Right, hair.Bottom);
            resources["Border.BoxNoTopThickness"] = new Thickness(hair.Left, 0, hair.Right, hair.Bottom);
            resources["Border.RightBottomHairlineThickness"] = new Thickness(0, 0, hair.Right, hair.Bottom);
        }

        private static void ApplyDynamicRadiusPrimitives(ResourceDictionary resources, double radius)
        {
            double medium = Math.Max(0, Math.Min(48, radius));
            double small = Math.Max(0, Math.Round(medium * 0.55));
            double xsmall = Math.Max(0, Math.Round(medium * 0.30));
            double large = Math.Max(medium, Math.Round(medium * 1.20));
            double hero = Math.Max(large, Math.Round(medium * 1.50));

            resources["Radius.None"] = new CornerRadius(0);
            resources["Radius.XSmall"] = new CornerRadius(xsmall);
            resources["Radius.Small"] = new CornerRadius(small);
            resources["Radius.Medium"] = new CornerRadius(medium);
            resources["Radius.Large"] = new CornerRadius(large);
            resources["Radius.Hero"] = new CornerRadius(hero);
            resources["Radius.Top.Medium"] = new CornerRadius(medium, medium, 0, 0);
            resources["Radius.Bottom.Medium"] = new CornerRadius(0, 0, medium, medium);
            resources["Radius.Left.Medium"] = new CornerRadius(medium, 0, 0, medium);
            resources["Radius.Right.Medium"] = new CornerRadius(0, medium, medium, 0);
            resources["Radius.Asymmetric.Brand"] = new CornerRadius(medium, medium, medium, small);
        }

        private static void ApplySemanticSurfacePadding(
            ResourceDictionary resources, Thickness cardPad, Thickness stripPad)
        {
            resources["Surface.Primary.Padding"] = cardPad;
            resources["Surface.Secondary.Padding"] = stripPad;
            resources["Surface.Detail.Padding"] = new Thickness(
                Math.Max(8, cardPad.Left - 2),
                Math.Max(5, cardPad.Top - 2),
                Math.Max(8, cardPad.Right - 2),
                Math.Max(5, cardPad.Bottom - 2));
        }

        /// <summary>
        /// Applies the semantic three-level ERP surface layer. This sits on
        /// top of the existing Light/Dark + visual-family engine; it publishes
        /// resources consumed by SurfacePrimary/Secondary/Detail styles and by
        /// migrated legacy card styles. Missing persisted values fall back to
        /// the existing Card* settings, so old config files remain valid.
        /// </summary>
        public static void ApplySemanticSurfaces(AppearanceSettings? appearance = null)
        {
            var resources = Application.Current?.Resources;
            if (resources == null) return;

            appearance ??= App.Config?.Appearance;

            var material = ProGlassThemeEngine.ParseMaterial(appearance?.SurfaceMaterialMode);

            double primaryOpacity = Clamp01(appearance?.SurfacePrimaryOpacity ?? 0.82, 0.30, 1.0);
            double secondaryOpacity = Clamp01(appearance?.SurfaceSecondaryOpacity ?? 0.94, 0.25, 1.0);
            double detailOpacity = Clamp01(appearance?.SurfaceDetailOpacity ?? 0.86, 0.15, 1.0);

            if (material == ProGlassMaterial.Solid)
            {
                primaryOpacity = 1.0;
                secondaryOpacity = 1.0;
                detailOpacity = Math.Max(detailOpacity, 0.96);
            }

            string primaryKey = material switch
            {
                ProGlassMaterial.Solid => "ElevatedSurfaceBrush",
                ProGlassMaterial.Soft => "ModuleBackgroundBrush",
                ProGlassMaterial.Crystal => "GlassCrystalBrush",
                _ => "GlassSurfaceBrush"
            };
            string secondaryKey = material switch
            {
                ProGlassMaterial.Solid => "WindowBackgroundBrush",
                ProGlassMaterial.Soft => "GlassSurfaceUltra",
                ProGlassMaterial.Crystal => "GlassSurfaceUltra",
                _ => "GlassSurfaceUltra"
            };
            string detailKey = material switch
            {
                ProGlassMaterial.Solid => "MenuHoverBrush",
                ProGlassMaterial.Soft => "ModuleBackgroundBrush",
                _ => "ModuleBackgroundBrush"
            };

            SetBrushResource(resources, "Surface.Primary.BackgroundBrush", primaryKey, primaryOpacity);
            ApplyPrimarySurfaceOverlay(resources,
                Clamp01(appearance?.PrimarySurfaceOverlayOpacity ?? 0.0, 0.0, 0.35));
            SetBrushResource(resources, "Surface.Secondary.BackgroundBrush", secondaryKey, secondaryOpacity);
            SetBrushResource(resources, "Surface.Detail.BackgroundBrush", detailKey, detailOpacity);
            SetBrushResource(resources, "Surface.Primary.BorderBrush", "GlassBorderSoft", 1.0);
            SetBrushResource(resources, "Surface.Secondary.BorderBrush", "OutlineBrush", 1.0);
            SetBrushResource(resources, "Surface.Detail.BorderBrush", "OutlineBrush", 0.92);
            SetBrushResource(resources, "Surface.Primary.OverlayBrush", "AccentBrush",
                Clamp01(appearance?.PrimarySurfaceOverlayOpacity ?? 0.0, 0.0, 0.35));

            double effectiveCardEdge = _definedOutline ? _cardEdgeThickness : _cardBorderThickness;
            SetThickness(resources, "Surface.Primary.BorderThickness",
                appearance?.SurfacePrimaryBorderThickness ?? effectiveCardEdge);
            SetThickness(resources, "Surface.Secondary.BorderThickness",
                appearance?.SurfaceSecondaryBorderThickness ?? _cardBorderThickness);
            SetThickness(resources, "Surface.Detail.BorderThickness",
                appearance?.SurfaceDetailBorderThickness ?? 1.0);

            bool rounded = appearance?.UseRoundedSurfaces ?? true;
            SetRadius(resources, "Surface.Primary.CornerRadius",
                rounded ? appearance?.SurfacePrimaryRadius ?? _cardCornerRadius : 0);
            SetRadius(resources, "Surface.Secondary.CornerRadius",
                rounded ? appearance?.SurfaceSecondaryRadius ?? _cardCornerRadius : 0);
            SetRadius(resources, "Surface.Detail.CornerRadius",
                rounded ? appearance?.SurfaceDetailRadius ?? _cardCornerRadius : 0);

            if (appearance?.UsePrimarySurfaceElevation == true)
            {
                var fx = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 18,
                    ShadowDepth = 0,
                    Opacity = 0.12,
                    Color = Color.FromArgb(0xFF, 0x0F, 0x17, 0x2A)
                };
                if (fx.CanFreeze) fx.Freeze();
                resources["Surface.Primary.Effect"] = fx;
            }
            else
            {
                resources["Surface.Primary.Effect"] = null;
            }

            ApplyModuleSkinBlur(resources, appearance);
            ApplyCardFrostTint(resources, appearance);

            PublishProGlassDesignSystem();
        }

        /// <summary>
        /// Blurs the module wallpaper layer (not card text). Intensity 1–100.
        /// Reduced motion or Solid material → no blur.
        /// </summary>
        private static void ApplyModuleSkinBlur(
            ResourceDictionary resources, AppearanceSettings? appearance)
        {
            double intensity = Clamp01(
                (appearance?.CardBlurIntensity ?? 28) / 100.0, 0.01, 1.0);
            bool reduced = appearance?.ReducedMotion == true;
            var material = ProGlassThemeEngine.ParseMaterial(appearance?.SurfaceMaterialMode);
            if (reduced || material == ProGlassMaterial.Solid)
            {
                resources["ModuleSkinBlurEffect"] = null;
                return;
            }

            var blur = new System.Windows.Media.Effects.BlurEffect
            {
                Radius = intensity * 36.0,
                KernelType = System.Windows.Media.Effects.KernelType.Gaussian,
                RenderingBias = System.Windows.Media.Effects.RenderingBias.Quality
            };
            resources["ModuleSkinBlurEffect"] = blur;
        }

        /// <summary>
        /// Mixes the operator-chosen frost plate into semantic surfaces.
        /// Empty tint leaves the theme fill (opacity already applied).
        /// </summary>
        private static void ApplyCardFrostTint(
            ResourceDictionary resources, AppearanceSettings? appearance)
        {
            var tint = AccentPresets.TryParseHex(appearance?.CardFrostTint);
            if (tint == null) return;

            MixSurfaceTint(resources, "Surface.Primary.BackgroundBrush", tint.Value, 0.22);
            MixSurfaceTint(resources, "Surface.Secondary.BackgroundBrush", tint.Value, 0.16);
            MixSurfaceTint(resources, "Surface.Detail.BackgroundBrush", tint.Value, 0.10);
        }

        private static void MixSurfaceTint(
            ResourceDictionary resources, string key, Color tint, double amount)
        {
            SolidColorBrush? surface = null;
            try
            {
                if (resources.Contains(key))
                    surface = resources[key] as SolidColorBrush;
            }
            catch (Exception)
            {
                return;
            }
            if (surface == null) return;

            byte Mix(byte a, byte b)
                => (byte)Math.Round(a * (1.0 - amount) + b * amount);

            var mixed = new SolidColorBrush(Color.FromArgb(
                surface.Color.A,
                Mix(surface.Color.R, tint.R),
                Mix(surface.Color.G, tint.G),
                Mix(surface.Color.B, tint.B)));
            mixed.Opacity = surface.Opacity;
            if (mixed.CanFreeze) mixed.Freeze();
            resources[key] = mixed;
        }

        /// <summary>
        /// Dual-writes the ProGlass Design System semantic keys from the
        /// live engine-managed brushes so Industrial / Monet / Crystal and
        /// Light / Dark all resolve through one vocabulary without views
        /// referencing Crystal* or LiquidGlass* names.
        /// </summary>
        public static void PublishProGlassDesignSystem()
            => ProGlassThemeEngine.Publish();

        public static void ReapplySemanticSurfaces()
            => ApplySemanticSurfaces(App.Config?.Appearance);

        private static double Clamp01(double value, double min, double max)
            => value < min ? min : (value > max ? max : value);

        private static double ClampNonNegative(double value, double max)
            => value < 0 ? 0 : (value > max ? max : value);

        private static void SetThickness(ResourceDictionary resources, string key, double value)
            => resources[key] = new Thickness(ClampNonNegative(value, 100));

        private static void SetRadius(ResourceDictionary resources, string key, double value)
            => resources[key] = new CornerRadius(ClampNonNegative(value, 48));

        private static void ApplyPrimarySurfaceOverlay(
            ResourceDictionary resources, double overlayOpacity)
        {
            if (overlayOpacity <= 0.001) return;
            SolidColorBrush? surface = null;
            SolidColorBrush? accent = null;
            try
            {
                if (resources.Contains("Surface.Primary.BackgroundBrush"))
                    surface = resources["Surface.Primary.BackgroundBrush"] as SolidColorBrush;
                if (resources.Contains("AccentBrush"))
                    accent = resources["AccentBrush"] as SolidColorBrush;
            }
            catch (Exception)
            {
                return;
            }
            if (surface == null || accent == null) return;

            byte Mix(byte a, byte b)
                => (byte)Math.Round(a * (1.0 - overlayOpacity) + b * overlayOpacity);

            var mixed = new SolidColorBrush(Color.FromArgb(
                surface.Color.A,
                Mix(surface.Color.R, accent.Color.R),
                Mix(surface.Color.G, accent.Color.G),
                Mix(surface.Color.B, accent.Color.B)));
            mixed.Opacity = surface.Opacity;
            mixed.Freeze();
            resources["Surface.Primary.BackgroundBrush"] = mixed;
        }

        private static void SetBrushResource(
            ResourceDictionary resources, string targetKey, string sourceKey, double opacity)
        {
            opacity = Clamp01(opacity, 0.0, 1.0);
            if (opacity <= 0.001)
            {
                var transparent = new SolidColorBrush(Colors.Transparent);
                transparent.Freeze();
                resources[targetKey] = transparent;
                return;
            }

            Brush? brush = null;
            try
            {
                if (resources.Contains(sourceKey))
                    brush = resources[sourceKey] as Brush;
            }
            catch (Exception)
            {
                brush = null;
            }

            if (brush == null)
            {
                try
                {
                    if (resources.Contains("ElevatedSurfaceBrush"))
                        brush = resources["ElevatedSurfaceBrush"] as Brush;
                }
                catch (Exception)
                {
                    brush = null;
                }
            }

            if (brush == null) return;

            var clone = brush.CloneCurrentValue();
            clone.Opacity = Math.Min(clone.Opacity, opacity);
            if (clone.CanFreeze) clone.Freeze();
            resources[targetKey] = clone;
        }

        public static void ReapplyDisplayDensity()
            => ApplyDisplayDensity(_displayDensity);

        private static double _uiScale = 1.0;

        /// <summary>User zoom 0.80–1.25. 1.0 = native layout (reflow).</summary>
        public static double UiScale => _uiScale;

        public static void ApplyUiScale(double scale)
        {
            scale = Viewport.ViewportService.ClampUiScale(scale);
            if (Math.Abs(_uiScale - scale) < 0.001) return;
            _uiScale = scale;

            var resources = Application.Current?.Resources;
            if (resources != null)
                resources["UiScale"] = scale;

            UiScaleChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void FreezeAndSet(
            ResourceDictionary resources, string key, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();                 // frozen = thread-safe + fast
            resources[key] = brush;
        }

        private static void FreezeAndSetBrush(
            ResourceDictionary resources, string key, Brush brush)
        {
            if (brush.CanFreeze) brush.Freeze();
            resources[key] = brush;
        }
    }
}
