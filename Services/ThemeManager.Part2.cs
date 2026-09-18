// ═══════════════════════════════════════════════════════════════════════
// FILE: ThemeManager.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Engine;
using ProGlassAutomation.Services.AI.AIConfig;
using System;
using System.Collections.Generic;
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

            double inputH, buttonH, rowH, title, kpi, body, buttonMin, typeScale;
            Thickness cardPad, stripPad, pageMargin;

            // 2026-09-16 owner report: "current ui view is too big for even
            // normal and compact and spacious should have the size which
            // currently compact having." Two independent defects caused that.
            //
            //   (a) The ladder was anchored far too large — Compact was 28px
            //       inputs and 10,6 page margins, which is what most apps call
            //       "default", so no step felt compact on a laptop. Spacious
            //       now takes the OLD Compact numbers exactly, and the three
            //       steps below it descend from there.
            //
            //   (b) The type scale was never wired up at all. Every FontSize.*
            //       token is a static <sys:Double> in GlobalDesignSystem.xaml
            //       and nothing ever rewrote it, while views read
            //       {DynamicResource FontSize.Body} 1747 times — 2460 FontSize.*
            //       references in total and ZERO StaticResource ones. The switch
            //       moved the 6 Density*FontSize aliases and left the real
            //       consumers untouched, so Compact and Spacious rendered
            //       identical type. typeScale below closes that gap.
            // 2026-09-17 owner mandate:
            // "Normal is still big in display density and other are also too big.
            //  Current normal apply to the biggest and rest three do more small display sizes accordingly"
            //
            // Ladder re-anchoring:
            //   • Spacious: receives what was previously "Normal" (input 24, button 26, row 22, body 12)
            //   • Comfortable: stepped down to compact professional sizing (input 22, button 24, row 20, body 11.5)
            //   • Normal: stepped down further to crisp high-density desktop scale (input 20, button 22, row 18, body 11)
            //   • Compact: ultra-efficient, dense shop-floor terminal fit (input 18, button 20, row 16, body 10.5)
            switch (key)
            {
                case "Spacious":   // anchored to the previous "Normal" baseline
                    inputH = 24; buttonH = 26; rowH = 22;
                    title = 15; kpi = 14; body = 12; buttonMin = 72;
                    cardPad = new Thickness(6, 5, 6, 5);
                    stripPad = new Thickness(8, 5, 8, 5);
                    pageMargin = new Thickness(8, 5, 8, 5);
                    typeScale = 0.92;
                    break;
                case "Comfortable": // compact step 1
                    inputH = 22; buttonH = 24; rowH = 20;
                    title = 14; kpi = 13; body = 11.5; buttonMin = 66;
                    cardPad = new Thickness(5, 4, 5, 4);
                    stripPad = new Thickness(7, 4, 7, 4);
                    pageMargin = new Thickness(7, 4, 7, 4);
                    typeScale = 0.88;
                    break;
                case "Normal": // compact step 2: crisp, sleek, and tight
                    inputH = 20; buttonH = 22; rowH = 18;
                    title = 13; kpi = 12.5; body = 11; buttonMin = 60;
                    cardPad = new Thickness(4, 3, 4, 3);
                    stripPad = new Thickness(6, 3, 6, 3);
                    pageMargin = new Thickness(6, 3, 6, 3);
                    typeScale = 0.84;
                    break;
                default: // Compact — ultra-dense, maximum data visibility
                    inputH = 18; buttonH = 20; rowH = 16;
                    title = 12; kpi = 11.5; body = 10.5; buttonMin = 54;
                    cardPad = new Thickness(3, 2, 3, 2);
                    stripPad = new Thickness(5, 2, 5, 2);
                    pageMargin = new Thickness(5, 2, 5, 2);
                    typeScale = 0.80;
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

            // ── TYPE SCALE ── scale the whole FontSize.* family off the values
            // the design dictionary published, rounded to the nearest half point
            // so steps stay crisp. This is the write that was missing: without it
            // the 2460 {DynamicResource FontSize.*} consumers never moved.
            foreach (var pair in FontSizeDefaults(resources))
                resources[pair.Key] = Math.Round(pair.Value * typeScale * 2.0) / 2.0;

            // ── DASHBOARD DENSITY (2026-09-18, MERIDIAN): the seven
            // dashboards now scale through the Pg* tokens. The old Db*
            // ladder was retired with the old kit (Phase 7) — its keys had
            // zero consumers left. Re-anchored to the Meridian base scale
            // (gutter 16, KPI min 92, chart min 212 at Spacious); the
            // relative steps follow the old ladder. Views read these with
            // DynamicResource, so the saved density always wins.
            double pgGutter;
            double pgKpiMinH;
            double pgChartMinH;
            Thickness pgPageMargin;
            switch (key)
            {
                case "Spacious":
                    pgGutter = 16; pgKpiMinH = 92; pgChartMinH = 212;
                    pgPageMargin = new Thickness(24, 20, 24, 24);
                    break;
                case "Comfortable":
                    pgGutter = 14; pgKpiMinH = 84; pgChartMinH = 192;
                    pgPageMargin = new Thickness(20, 16, 20, 20);
                    break;
                case "Normal":
                    pgGutter = 12; pgKpiMinH = 76; pgChartMinH = 172;
                    pgPageMargin = new Thickness(16, 12, 16, 16);
                    break;
                default: // Compact
                    pgGutter = 10; pgKpiMinH = 68; pgChartMinH = 152;
                    pgPageMargin = new Thickness(12, 10, 12, 12);
                    break;
            }
            resources["PgPageMargin"] = pgPageMargin;
            resources["PgGutter"] = new Thickness(pgGutter);
            resources["PgGutterHalfMargin"] = new Thickness(pgGutter / 2.0);
            resources["PgKpiTileMinHeight"] = pgKpiMinH;
            resources["PgChartMinHeight"] = pgChartMinH;

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

        /// <summary>
        /// Every <c>FontSize.*</c> design token published by
        /// <c>Themes/Styles/GlobalDesignSystem.xaml</c>. Listed explicitly rather
        /// than discovered by enumerating <c>ResourceDictionary.Keys</c>, so a
        /// token added to the dictionary shows up as a review-time gap instead of
        /// silently rendering at one fixed size forever. Keep in step with it.
        /// </summary>
        private static readonly string[] FontSizeTokenKeys =
        {
            "FontSize.Display", "FontSize.H1", "FontSize.H2", "FontSize.H3",
            "FontSize.Title", "FontSize.Subtitle", "FontSize.Body", "FontSize.BodySmall",
            "FontSize.Label", "FontSize.Caption", "FontSize.Kpi", "FontSize.Button",
            "FontSize.Navigation", "FontSize.DataGrid", "FontSize.Money",
            "FontSize.Icon", "FontSize.Input", "FontSize.Tab", "FontSize.Chip",
            "FontSize.Badge",
        };

        /// <summary>
        /// Pristine <c>FontSize.*</c> defaults, captured from the application
        /// resources the first time density is applied and cached thereafter.
        /// Scaling must always start from the design value — scaling the live
        /// value would compound on every theme or density change.
        /// </summary>
        private static Dictionary<string, double>? _fontSizeDefaults;

        private static Dictionary<string, double> FontSizeDefaults(ResourceDictionary resources)
        {
            if (_fontSizeDefaults is not null) return _fontSizeDefaults;

            var captured = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var key in FontSizeTokenKeys)
                if (resources[key] is double value) captured[key] = value;

            _fontSizeDefaults = captured;
            return captured;
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

            // NOTE: ModuleBackgroundBrush must never be a surface source - it is
            // #00FFFFFF (light) / #00000000 (dark), i.e. alpha 0.00 in both themes.
            // It is a see-through module backdrop, not a card face; composing from it
            // produced permanently invisible surfaces (the dead Primary channel of the
            // "Transparent" material and the dead Detail channel of the default "Glass").
            string primaryKey = material switch
            {
                ProGlassMaterial.Solid => "ElevatedSurfaceBrush",
                ProGlassMaterial.Soft => "GlassSurfaceBrush",
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
                ProGlassMaterial.Soft => "MenuHoverBrush",
                _ => "MenuHoverBrush"
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
