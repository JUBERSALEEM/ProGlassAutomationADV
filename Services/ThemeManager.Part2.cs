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
            resources["AboutCardPadding"] = new Thickness(18 + extra, 14 + extra, 18 + extra, 14 + extra);
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

            // ── MERIDIAN RADIUS SCALE (2026-09-18): the Pg* dashboards
            // consume {DynamicResource Pg*Radius} (dynamic flip, 2026-09-18).
            // The old kit's cards followed Surface.Primary.CornerRadius from
            // this same authority; Meridian authored a 12/9/8/10/7
            // card/tile/control/icon/icon-sm scale. Republish it
            // PROPORTIONALLY off the slider (factor = radius/12) so the
            // Appearance slider keeps affecting the Meridian views while the
            // authored hierarchy is preserved — at the default 12 every value
            // is exactly the authored value (zero visual change).
            // 2026-09-19 owner correction: the PgChipRadius 999 capsule was
            // removed — chips now follow this same authority (1:1), and the
            // Glass Blush pill token follows it too, so no 999/full-pill
            // radius remains in either experience.
            double rFactor = radius / 12.0;
            resources["PgCardRadius"] = new CornerRadius(radius);
            resources["PgChipRadius"] = cr;
            resources["GlassBlush.PillRadius"] = cr;
            resources["PgTileRadius"] = new CornerRadius(Math.Max(3, Math.Round(9 * rFactor)));
            resources["PgCtrlRadius"] = new CornerRadius(Math.Max(3, Math.Round(8 * rFactor)));
            resources["PgIconRadius"] = new CornerRadius(Math.Max(3, Math.Round(10 * rFactor)));
            resources["PgIconSmRadius"] = new CornerRadius(Math.Max(2, Math.Round(7 * rFactor)));
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

            // ── MERIDIAN TYPE SCALE (2026-09-18): the Pg* dashboards consume
            // {DynamicResource PgFont*}/{PgTopFont*} (the density flip in
            // ProfessionalCore/Shell + the 9 rewritten views). Scale that
            // family off the pristine authored values with the same typeScale
            // as FontSize.*, so dashboard typography reflows with Density
            // exactly like the old kit's DynamicResource FontSize.* did.
            // Pristine capture happens once (see PgFontDefaults) so repeated
            // density changes never compound.
            foreach (var pair in PgFontDefaults(resources))
                resources[pair.Key] = Math.Round(pair.Value * typeScale * 2.0) / 2.0;

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

        /// <summary>
        /// MERIDIAN (2026-09-18) typography tokens published by
        /// <c>Themes/Professional/ProfessionalCore.xaml</c> (PgFont*) and
        /// <c>ProfessionalShell.xaml</c> (PgTopFont*). Listed explicitly so a
        /// new token shows up as a review-time gap rather than silently
        /// staying at one fixed size. <c>ApplyDisplayDensity</c> scales this
        /// family by <c>typeScale</c> so the Pg dashboards' type reflows with
        /// Density (they read the keys via DynamicResource after the 2026-09-18
        /// dynamic flip).
        /// </summary>
        private static readonly string[] PgFontTokenKeys =
        {
            "PgFontHero", "PgFontHeroSub", "PgFontCardTitle", "PgFontCardSub",
            "PgFontKpiValue", "PgFontKpiLabel", "PgFontKpiFoot",
            "PgFontStatValue", "PgFontStatLabel", "PgFontStatFoot",
            "PgFontStageValue", "PgFontStageName", "PgFontStageFoot",
            "PgFontBody", "PgFontBodySm", "PgFontMeta", "PgFontBtn",
            "PgFontGlyph", "PgFontGlyphSm",
            "PgTopFont", "PgTopFontBtn", "PgTopFontCap",
        };

        private static Dictionary<string, double>? _pgFontDefaults;

        /// <summary>
        /// Pristine MERIDIAN <c>PgFont*</c>/<c>PgTopFont*</c> values, captured
        /// on first density application and cached, so repeated density or
        /// theme changes scale from the authored value and never compound.
        /// </summary>
        private static Dictionary<string, double> PgFontDefaults(ResourceDictionary resources)
        {
            if (_pgFontDefaults is not null) return _pgFontDefaults;

            var captured = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var key in PgFontTokenKeys)
                if (resources[key] is double value) captured[key] = value;

            _pgFontDefaults = captured;
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
            // Border brushes are written by PublishSurfaceEdgeBrushes after
            // the family publish. GlassBorderSoft is a low-alpha stroke, and
            // a corner arc of that brush reads as "the corner refused the
            // colour" even when the straight edge is still visible.
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

            // Elevation is published by PublishLiveSurfaceFills. Writing
            // null here first wiped the shadow whenever the fill cache
            // correctly skipped a duplicate tick.
            ApplyModuleSkinBlur(resources, appearance);

            // Family publish copies GlassUltraClearBrush back onto
            // SurfacePrimaryBrush. Appearance has to win that write, or
            // every slider tick paints the plate and the engine erases it
            // in the same method. That was the dead-control loop. An
            // unchanged family skips the copy — the slider hitch was that
            // full publish running on every opacity tick.
            PublishFamilyIfNeeded(appearance);
            PublishLiveSurfaceFills(resources, appearance, material);
            PublishSurfaceEdgeBrushes(resources);
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
                // A shared zero-radius effect, not null. The shell binds
                // this key; assigning null detaches the listener's effect
                // in a way that the next real effect does not always
                // reattach on a frozen Freezable slot.
                if (_cachedWallpaperBlur == null || _cachedWallpaperBlurRadius > 0.05)
                {
                    var off = new System.Windows.Media.Effects.BlurEffect
                    {
                        Radius = 0,
                        KernelType = System.Windows.Media.Effects.KernelType.Box,
                        RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance
                    };
                    if (off.CanFreeze) off.Freeze();
                    _cachedWallpaperBlur = off;
                    _cachedWallpaperBlurRadius = 0;
                }
                resources["ModuleSkinBlurEffect"] = _cachedWallpaperBlur;
                return;
            }

            // Box kernel is the cheap blur. Gaussian on a full-window
            // wallpaper is the frame hitch the slider used to cause.
            // Cap at 8. A radius of 18 on a maximized window is a
            // full-screen shader large enough to time out the display
            // driver on the first shell frame.
            double radius = Math.Min(8.0, intensity * 16.0);
            if (_cachedWallpaperBlur != null && Math.Abs(_cachedWallpaperBlurRadius - radius) < 0.05)
            {
                resources["ModuleSkinBlurEffect"] = _cachedWallpaperBlur;
                return;
            }

            var blur = new System.Windows.Media.Effects.BlurEffect
            {
                Radius = radius,
                KernelType = System.Windows.Media.Effects.KernelType.Box,
                RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance
            };
            if (blur.CanFreeze) blur.Freeze();
            _cachedWallpaperBlur = blur;
            _cachedWallpaperBlurRadius = radius;
            resources["ModuleSkinBlurEffect"] = blur;
        }

        /// <summary>
        /// Dual-writes the ProGlass Design System semantic keys from the
        /// live engine-managed brushes so Industrial / Monet / Crystal and
        /// Light / Dark all resolve through one vocabulary without views
        /// referencing Crystal* or LiquidGlass* names.
        /// Appearance is reattached afterwards. Callers (theme pipeline,
        /// accent rebuild) publish again after the surface pass, and that
        /// second publish was putting the stock palette back on top of
        /// the plate the operator had just chosen.
        /// </summary>
        public static void PublishProGlassDesignSystem()
        {
            ProGlassThemeEngine.Publish();
            ReattachAppearanceWins();
            _familyPublishKey = FamilyPublishKey(App.Config?.Appearance);
        }

        private static string? _familyPublishKey;

        private static string FamilyPublishKey(AppearanceSettings? appearance)
            => string.Join("|",
                appearance?.VisualTheme ?? "",
                _currentTheme.ToString(),
                appearance?.SurfaceMaterialMode ?? "",
                appearance?.DisplayDensity ?? "");

        private static void PublishFamilyIfNeeded(AppearanceSettings? appearance)
        {
            string key = FamilyPublishKey(appearance);
            if (key == _familyPublishKey) return;
            _familyPublishKey = key;
            ProGlassThemeEngine.Publish();
            ReattachAppearanceWins();
        }

        private static void ReattachAppearanceWins()
        {
            var resources = Application.Current?.Resources;
            if (resources == null) return;
            ReattachCachedFills(resources);
            PublishSurfaceEdgeBrushes(resources, force: true);
        }

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

        private static System.Windows.Media.Effects.BlurEffect? _cachedWallpaperBlur;
        private static double _cachedWallpaperBlurRadius = -1;
        private static string? _liveFillSignature;
        private static readonly Dictionary<string, Brush> _cachedFillBrushes = new(StringComparer.Ordinal);
        private static System.Windows.Media.Effects.DropShadowEffect? _cachedSurfaceShadow;
        private static bool _cachedElevation;
        private static Color? _publishedEdgeColor;
        private static readonly HashSet<string> _localBrushOverrides = new(StringComparer.Ordinal);

        /// <summary>
        /// Settings cards paint their rounded stroke, corner included, from
        /// SettingsCardEdgeBrush. OutlineBrush is a different token. While
        /// the defined edge is on, both card-edge brushes become the chosen
        /// colour. While off, the theme's own edge brush is restored so a
        /// Light/Dark gradient is not stuck as a flat solid.
        /// </summary>
        private static void PublishDefinedCardEdge(
            ResourceDictionary resources, bool defined, Color edge)
        {
            if (!defined)
            {
                _publishedEdgeColor = null;
                RestoreThemeBrush(resources, "SettingsCardEdgeBrush");
                RestoreThemeBrush(resources, "SettingsInnerEdgeBrush");
                return;
            }

            // Opaque stroke. A low-alpha theme gradient is why a corner
            // looked like it "refused" the plate: the arc has fewer pixels
            // than the straight edge, so a 20% white gradient disappears
            // there first. The plate colour is the stroke, full alpha.
            var stroke = edge.A < 255
                ? Color.FromArgb(255, edge.R, edge.G, edge.B)
                : edge;
            _publishedEdgeColor = stroke;
            SetOverrideBrush(resources, "SettingsCardEdgeBrush", stroke);
            SetOverrideBrush(resources, "SettingsInnerEdgeBrush", stroke);
        }

        /// <summary>
        /// Builds the three surface fills the Appearance sliders actually
        /// move, plus the Settings card face when the operator has left
        /// the factory glass. The previous path cloned GlassSurfaceBrush
        /// (a translucent gradient) and then refused to tint it because
        /// it was not a SolidColorBrush. A plate click and an opacity
        /// drag therefore changed nothing the eye could see.
        /// </summary>
        private static void PublishLiveSurfaceFills(
            ResourceDictionary resources,
            AppearanceSettings? appearance,
            ProGlassMaterial material)
        {
            var tint = AccentPresets.TryParseHex(appearance?.CardFrostTint);
            double primaryOpacity = Clamp01(appearance?.SurfacePrimaryOpacity ?? 0.82, 0.30, 1.0);
            double secondaryOpacity = Clamp01(appearance?.SurfaceSecondaryOpacity ?? 0.94, 0.25, 1.0);
            double detailOpacity = Clamp01(appearance?.SurfaceDetailOpacity ?? 0.86, 0.15, 1.0);
            double overlay = Clamp01(appearance?.PrimarySurfaceOverlayOpacity ?? 0.0, 0.0, 0.35);
            // 0 = white, 100 = the exact picked RGB. 0 = clear, 100 = opaque.
            // Defaults are 100 so a pick is not washed into grey.
            double plateStrength = Clamp01((appearance?.CardFrostStrength ?? 100) / 100.0, 0, 1);
            double plateOpacity = Clamp01((appearance?.CardFrostOpacity ?? 100) / 100.0, 0, 1);
            if (material == ProGlassMaterial.Solid)
            {
                primaryOpacity = 1.0;
                secondaryOpacity = 1.0;
                detailOpacity = Math.Max(detailOpacity, 0.96);
            }

            var baseColor = OpaqueCardBase(resources);
            var accent = SampleBrushColor(LiveBrush(resources, "AccentBrush"));
            string signature = string.Join("|",
                baseColor.ToString(),
                tint?.ToString() ?? "",
                accent?.ToString() ?? "",
                primaryOpacity.ToString("0.###"),
                secondaryOpacity.ToString("0.###"),
                detailOpacity.ToString("0.###"),
                overlay.ToString("0.###"),
                plateStrength.ToString("0.###"),
                plateOpacity.ToString("0.###"),
                material.ToString(),
                appearance?.UsePrimarySurfaceElevation == true ? "1" : "0");
            // Family publish, which ran just above, copies the stock palette
            // back over SurfacePrimaryBrush on every tick. A signature hit
            // must reattach the cached brushes or that copy wins and the
            // plate looks dead again. Reattach is a dictionary write, not
            // a new brush.
            if (signature == _liveFillSignature)
            {
                ReattachCachedFills(resources);
                return;
            }
            _liveFillSignature = signature;
            _cachedFillBrushes.Clear();

            // A picked plate is mixed from white to the swatch, then given
            // the transparency handle's alpha. It is not mixed through the
            // page grey, and it does not inherit SurfacePrimaryOpacity
            // (0.82). Those two washes are why black painted as dark grey.
            bool plateOn = tint != null;
            bool clearPlate = plateOpacity < 0.999;
            Color plateFace = plateOn
                ? PlateColor(tint!.Value, plateStrength, plateOpacity)
                : Color.FromArgb(
                    (byte)Math.Round(plateOpacity * 255.0),
                    baseColor.R, baseColor.G, baseColor.B);
            SetOverrideBrush(resources, "Surface.Primary.LiveFill",
                plateOn || clearPlate
                    ? plateFace
                    : MixFill(baseColor, null, 0, accent, overlay, primaryOpacity));
            SetOverrideBrush(resources, "Surface.Secondary.LiveFill",
                plateOn || clearPlate
                    ? plateFace
                    : MixFill(baseColor, null, 0, null, 0, secondaryOpacity));
            SetOverrideBrush(resources, "Surface.Detail.LiveFill",
                plateOn || clearPlate
                    ? plateFace
                    : MixFill(baseColor, null, 0, null, 0, detailOpacity));

            // Factory glass stays the authored DrawingBrush. A plate, a
            // moved opacity, or Solid material is an explicit edit and
            // must repaint the card the operator is looking at. The inner
            // tile is a second face; leaving it on the theme solid is why
            // a plate click looked like it had done nothing.
            bool customFace = plateOn
                || clearPlate
                || material == ProGlassMaterial.Solid
                || Math.Abs(primaryOpacity - 0.82) > 0.01
                || overlay > 0.01;
            if (plateOn || clearPlate)
            {
                PublishPlateFace(resources, plateFace);
            }
            else if (customFace)
            {
                var face = MixFill(baseColor, null, 0, accent, overlay, primaryOpacity);
                SetOverrideBrush(resources, "SettingsCardFaceBrush", face);
                SetOverrideBrush(resources, "SettingsInnerFaceBrush",
                    MixFill(baseColor, null, 0, null, 0, Math.Max(primaryOpacity, 0.92)));
                SetOverrideBrush(resources, "SurfacePrimaryBrush", face);
                SetOverrideBrush(resources, "SurfaceSecondaryBrush",
                    MixFill(baseColor, null, 0, null, 0, secondaryOpacity));
                SetOverrideBrush(resources, "SurfaceTertiaryBrush",
                    MixFill(baseColor, null, 0, null, 0, detailOpacity));
                PublishStudioColor(resources, "Appearance.StudioFaceColor", "Appearance.StudioFace", face);
            }
            else
            {
                RestoreThemeBrush(resources, "SettingsCardFaceBrush");
                RestoreThemeBrush(resources, "SettingsInnerFaceBrush");
                RestoreThemeBrush(resources, "SurfacePrimaryBrush");
                RestoreThemeBrush(resources, "SurfaceSecondaryBrush");
                RestoreThemeBrush(resources, "SurfaceTertiaryBrush");
                PublishStudioColor(resources, "Appearance.StudioFaceColor", "Appearance.StudioFace", StudioFaceRest);
            }

            PublishSurfaceShadow(resources, appearance?.UsePrimarySurfaceElevation == true);
        }

        private static void PublishSurfaceShadow(ResourceDictionary resources, bool elevate)
        {
            // Elevation off keeps a soft shadow so the toggle is a change
            // of weight, not a disappear. A null effect was the previous
            // "off" and made the control look dead. Reuse the frozen
            // effect when only the plate colour moved.
            if (_cachedSurfaceShadow == null || _cachedElevation != elevate)
            {
                var shadow = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = elevate ? 28 : 8,
                    ShadowDepth = elevate ? 2 : 0,
                    Opacity = elevate ? 0.34 : 0.08,
                    Direction = 270,
                    Color = Color.FromRgb(0x0F, 0x17, 0x2A),
                    RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance
                };
                if (shadow.CanFreeze) shadow.Freeze();
                _cachedSurfaceShadow = shadow;
                _cachedElevation = elevate;
            }

            resources["Surface.Primary.Effect"] = _cachedSurfaceShadow;
        }

        /// <summary>
        /// Opaque stroke for the three surface specimens and the module
        /// cards that read Surface.*.BorderBrush. A cloned glass hairline
        /// has so little alpha that raising its thickness changes nothing
        /// the eye can see.
        /// </summary>
        private static Color? _publishedSurfaceStroke;

        private static void PublishSurfaceEdgeBrushes(ResourceDictionary resources, bool force = false)
        {
            Color stroke;
            bool definedStroke = _definedOutline && _publishedEdgeColor is Color;
            if (definedStroke && _publishedEdgeColor is Color edge)
                stroke = edge;
            else
            {
                var sampled = SampleBrushColor(LiveBrush(resources, "BorderDefaultBrush"));
                stroke = sampled is Color color
                    ? Color.FromArgb(255, color.R, color.G, color.B)
                    : Color.FromRgb(0xCB, 0xD5, 0xE1);
            }

            // Slider ticks call this after the fill cache has already
            // decided nothing moved. Skip the brush allocations unless a
            // family publish just copied the stock keyline back.
            if (!force && _publishedSurfaceStroke is Color previous && previous == stroke)
                return;
            _publishedSurfaceStroke = stroke;

            if (definedStroke)
            {
                FreezeAndSet(resources, "GlassBorderSoft", stroke);
                FreezeAndSet(resources, "GlassBorderGradientCrystal", stroke);
                FreezeAndSet(resources, "OutlineBrush", stroke);
            }

            FreezeAndSet(resources, "Surface.Primary.BorderBrush", stroke);
            FreezeAndSet(resources, "Surface.Secondary.BorderBrush", stroke);
            FreezeAndSet(resources, "Surface.Detail.BorderBrush", stroke);
            Color studioEdge = _definedOutline && _publishedEdgeColor is Color defined
                ? defined
                : StudioEdgeRest;
            PublishStudioColor(resources, "Appearance.StudioEdgeColor", "Appearance.StudioEdge", studioEdge);
        }

        private static readonly Color StudioFaceRest = Color.FromRgb(0xFF, 0xFE, 0xFC);
        private static readonly Color StudioEdgeRest = Color.FromRgb(0xE2, 0xDB, 0xEF);

        private static void PublishStudioColor(
            ResourceDictionary resources, string colorKey, string brushKey, Color color)
        {
            resources[colorKey] = color;
            FreezeAndSet(resources, brushKey, color);
        }

        private static void ReattachCachedFills(ResourceDictionary resources)
        {
            foreach (var pair in _cachedFillBrushes)
                resources[pair.Key] = pair.Value;
            if (_cachedSurfaceShadow != null)
                resources["Surface.Primary.Effect"] = _cachedSurfaceShadow;
        }

        private static Color OpaqueCardBase(ResourceDictionary resources)
        {
            var sampled = SampleBrushColor(LiveBrush(resources, "BackgroundBrush"));
            if (sampled is Color color && color.A > 200)
                return Color.FromRgb(color.R, color.G, color.B);
            return _currentTheme == AppTheme.Dark
                ? Color.FromRgb(0x11, 0x18, 0x27)
                : Color.FromRgb(0xF8, 0xFA, 0xFC);
        }

        private static Color MixFill(
            Color baseColor, Color? tint, double tintAmount,
            Color? accent, double accentAmount, double opacity)
        {
            var rgb = baseColor;
            if (tint is Color plate && tintAmount > 0)
                rgb = MixRgb(rgb, plate, tintAmount);
            if (accent is Color accentColor && accentAmount > 0)
                rgb = MixRgb(rgb, accentColor, accentAmount);
            byte alpha = (byte)Math.Round(Clamp01(opacity, 0, 1) * 255.0);
            return Color.FromArgb(alpha, rgb.R, rgb.G, rgb.B);
        }

        /// <summary>
        /// Exact plate colour. Strength 1 is the swatch RGB, strength 0 is
        /// white. Opacity is the alpha, not a second mix into the page.
        /// </summary>
        private static Color PlateColor(Color tint, double strength, double opacity)
        {
            var rgb = MixRgb(Color.FromRgb(255, 255, 255),
                Color.FromRgb(tint.R, tint.G, tint.B), strength);
            byte alpha = (byte)Math.Round(Clamp01(opacity, 0, 1) * 255.0);
            return Color.FromArgb(alpha, rgb.R, rgb.G, rgb.B);
        }

        private static void PublishPlateFace(ResourceDictionary resources, Color face)
        {
            SetOverrideBrush(resources, "SettingsCardFaceBrush", face);
            SetOverrideBrush(resources, "SettingsInnerFaceBrush", face);
            SetOverrideBrush(resources, "SurfacePrimaryBrush", face);
            SetOverrideBrush(resources, "SurfaceSecondaryBrush", face);
            SetOverrideBrush(resources, "SurfaceTertiaryBrush", face);
            PublishStudioColor(resources, "Appearance.StudioFaceColor", "Appearance.StudioFace", face);
        }

        private static Color MixRgb(Color from, Color to, double amount)
        {
            byte Mix(byte a, byte b)
                => (byte)Math.Round(a * (1.0 - amount) + b * amount);
            return Color.FromRgb(Mix(from.R, to.R), Mix(from.G, to.G), Mix(from.B, to.B));
        }

        private static Color? SampleBrushColor(Brush? brush)
        {
            if (brush is SolidColorBrush solid)
                return solid.Color;
            if (brush is GradientBrush gradient)
            {
                var stop = gradient.GradientStops
                    .OrderByDescending(s => s.Color.A)
                    .FirstOrDefault();
                if (stop != null) return stop.Color;
            }
            return null;
        }

        private static void SetOverrideBrush(ResourceDictionary resources, string key, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[key] = brush;
            _cachedFillBrushes[key] = brush;
            _localBrushOverrides.Add(key);
        }

        private static void RestoreThemeBrush(ResourceDictionary resources, string key)
        {
            _cachedFillBrushes.Remove(key);
            if (!_localBrushOverrides.Remove(key)) return;
            // Assign the active theme instance. Cloning the merged
            // dictionary wrote the Light face over a Dark overlay, then
            // dropped the key from the override set so the next swap
            // could not remove that clone.
            var theme = ActiveThemeBrush(key);
            if (theme == null && _currentTheme != AppTheme.Dark)
                theme = FindMergedBrush(resources, key);
            if (theme != null)
            {
                resources[key] = theme;
                return;
            }

            _localBrushOverrides.Add(key);
        }

        private static Brush? ActiveThemeBrush(string key)
        {
            var dict = _currentTheme == AppTheme.Dark ? _darkDict : _lightDict;
            try
            {
                if (dict != null && dict.Contains(key) && dict[key] is Brush brush)
                    return brush;
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        private static Brush? LiveBrush(ResourceDictionary resources, string key)
        {
            var themed = ActiveThemeBrush(key);
            if (themed != null) return themed;
            try
            {
                if (resources.Contains(key) && resources[key] is Brush brush)
                    return brush;
            }
            catch (Exception)
            {
                return null;
            }

            return FindMergedBrush(resources, key);
        }

        private static Brush? FindMergedBrush(ResourceDictionary root, string key)
        {
            Brush? found = null;
            foreach (var dict in root.MergedDictionaries)
            {
                try
                {
                    if (dict.Contains(key) && dict[key] is Brush brush)
                        found = brush;
                }
                catch (Exception)
                {
                    // A dictionary can throw if a key's value is a deferred
                    // reference that has not been realized yet. Skip it.
                }
            }
            return found;
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
