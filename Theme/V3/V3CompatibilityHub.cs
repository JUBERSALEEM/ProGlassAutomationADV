// ═══════════════════════════════════════════════════════════════════════
// FILE: Theme/V3/V3CompatibilityHub.cs
// PROJECT: ProGlassAutomation (ADV)
// MODULE: V3 theme engine — legacy dual-write bridge
//
// PURPOSE (Phase 1, adapter):
//   V3 ThemeService.Apply() swaps ONE complete theme package. Every
//   ADV view that still speaks the legacy vocabulary (Ov.*, Pg*,
//   Glass*, Liquid*, Ios26*, the 72 VisualThemeEngine ManagedKeys and
//   the v2 semantic layer) must keep resolving after every Apply —
//   otherwise the ERP goes blank. This hub is the "other way" bridge:
//   V3 contract → ADV legacy keys. After every Apply it freezes the
//   active package's token values into Application.Current.Resources
//   under the legacy key names, so DynamicResource consumers re-tint
//   live on Light/Dark and theme switches.
//
// SOURCE OF TRUTH: the ACTIVE V3 PACKAGE passed to Publish(). The hub
// never reads legacy values back — it only forwards contract values.
//
// COVERAGE RULE: every key below was verified by rg against actual
// view references (Ov.* 18 keys across 60+ views; the 72 ManagedKeys;
// the v2 semantic layer the old engine overwrote at runtime; the
// runtime-only palette keys published by screen-local dictionaries).
// Do not add an entry on a hunch — and do not delete one because a
// sweep "looks clean": the keys are referenced from views that are
// not merged globally, so a merged-dictionary sweep alone misses them.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Theme;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Theming;

/// <summary>
/// Frozen-value bridge from the active V3 theme package to the ADV
/// legacy resource vocabulary. Pure forwarding + derivation — no
/// painting, no dictionary swaps.
/// </summary>
public static class V3CompatibilityHub
{
    // ── Direct token forwarding (legacy key → V3 contract token) ────────
    // Values are read from the active package and written as frozen
    // clones so shared brush instances can never be mutated through one
    // dictionary and corrupt another.
    private static readonly (string Legacy, string Token)[] DirectMap =
    {
        // Canvas / window / module backgrounds
        ("BackgroundBrush", "ApplicationBackground"),
        ("WindowBackgroundBrush", "ApplicationBackground"),
        ("ModuleBackgroundBrush", "ApplicationBackground"),
        ("Ov.Canvas", "ApplicationBackground"),
        ("SurfaceWindowBrush", "ApplicationBackground"),
        ("Ios26CanvasBrush", "ApplicationBackground"),

        // Surfaces
        ("SurfacePrimaryBrush", "SurfacePrimary"),
        ("SurfaceSecondaryBrush", "SurfaceSecondary"),
        ("SurfaceTertiaryBrush", "SurfaceSecondary"),
        ("SurfaceInputBrush", "SurfacePrimary"),
        ("SurfaceElevatedBrush", "SurfaceElevated"),
        ("ElevatedSurfaceBrush", "SurfaceSecondary"), // spec SurfaceSecondary row
        ("SurfaceWorkspaceBrush", "SurfacePrimary"),
        ("SurfaceCardBrush", "SurfacePrimary"),
        ("SurfacePopupBrush", "SurfaceElevated"),
        ("SurfaceSidebarBrush", "SurfaceElevated"),
        ("GlassUltraClearBrush", "SurfacePrimary"),
        ("GlassSurfaceBrush", "SurfacePrimary"),
        ("GlassCrystalBrush", "SurfacePrimary"),
        ("GlassSurfaceStandard", "SurfacePrimary"),
        ("GlassFrostedBrush", "SurfaceSecondary"),
        ("GlassSurfaceElevated", "SurfaceSecondary"),
        ("GlassSurfaceUltra", "SurfaceElevated"),
        ("LiquidCard", "SurfacePrimary"),
        ("LiquidGlassBrush", "SurfacePrimary"),
        ("LiquidGlassSubtleBrush", "SurfaceSecondary"),
        ("LiquidGlassStrongBrush", "SurfaceElevated"),
        ("SideMenuBackgroundBrush", "SurfaceElevated"),
        ("DialogSurfaceBrush", "SurfaceElevated"),
        ("DialogInsetBrush", "SurfacePrimary"),
        ("SidebarCardBrush", "SurfaceElevated"),
        ("ShellLiquidBrush", "SurfaceElevated"),
        ("DashLiquidSurfaceBrush", "SurfaceElevated"),
        ("DashAmbientBrush", "SurfacePrimary"),
        ("DashAmbientBlob1Brush", "SurfaceSecondary"),
        ("DashAmbientBlob2Brush", "SurfaceSecondary"),
        ("Ov.Surface", "SurfacePrimary"),
        ("Ov.Soft", "SurfaceSecondary"),
        ("Ios26CardBrush", "SurfacePrimary"),
        ("Ios26TileBrush", "SurfaceSecondary"),
        ("Ios26RailBrush", "SurfaceSecondary"),
        ("Ios26HoverBrush", "SurfaceSecondary"),
        ("Ios26SearchBrush", "SurfacePrimary"),
        ("Ios26ValuePillBrush", "SurfaceSecondary"),
        ("Ios26OrbBlueBrush", "AccentSoft"),
        ("Ios26OrbTealBrush", "AccentSoft"),
        ("Ios26OrbLavenderBrush", "AccentSoft"),
        ("Ios26OrbGreenBrush", "StatusSuccessSurface"),
        ("Ios26TileBadgeBrush", "AccentSoft"),
        ("ModuleWallpaperBrush", "TransparentBrush"),

        // Overlay / scrim
        ("SurfaceOverlayBrush", "OverlayBrush"),
        ("LiquidOverlayBrush", "OverlayBrush"),

        // Text
        ("TextPrimaryBrush", "TextPrimary"),
        ("PrimaryForegroundBrush", "TextPrimary"),
        ("Ov.Ink", "TextPrimary"),
        ("Ios26TextTitleBrush", "TextPrimary"),
        ("Ios26TextBodyBrush", "TextPrimary"),
        ("TextSecondaryBrush", "TextSecondary"),
        ("SecondaryForegroundBrush", "TextSecondary"),
        ("Ios26TextSecondaryBrush", "TextSecondary"),
        ("TextTertiaryBrush", "TextMuted"),
        ("MutedForegroundBrush", "TextMuted"),
        ("Ov.Muted", "TextMuted"),
        ("Ios26TextMutedBrush", "TextMuted"),
        ("Ios26TextFaintBrush", "TextMuted"),
        ("TextDisabledBrush", "DisabledText"),
        ("LiquidControlDisabledBrush", "DisabledText"),
        ("TextPlaceholderBrush", "TextMuted"),
        ("TextOnAccentBrush", "TextOnAccent"),
        ("Ov.OnAccent", "TextOnAccent"),
        ("Ios26OnAccentBrush", "TextOnAccent"),
        ("Ios26OnAccentChipBrush", "TextOnAccent"),
        ("SelectionForegroundBrush", "TextOnAccent"),

        // Borders / keylines / focus
        ("BorderDefaultBrush", "BorderSubtle"),
        ("OutlineBrush", "BorderSubtle"),
        ("OutlineDefaultBrush", "BorderSubtle"),
        ("BorderBrush", "BorderSubtle"),
        ("DividerBrush", "BorderSubtle"),
        ("Ov.Line", "BorderSubtle"),
        ("GlassBorderSoft", "BorderSubtle"),
        ("GlassBorderPrimary", "BorderSubtle"),
        ("GlassBorderGradientCrystal", "BorderSubtle"),
        ("InputBorderBrush", "BorderSubtle"),
        ("Ios26CardBorderBrush", "BorderSubtle"),
        ("Ios26TileBorderBrush", "BorderSubtle"),
        ("Ios26HairlineBrush", "BorderSubtle"),
        ("Ios26TrackBrush", "BorderSubtle"),
        ("ChevronBrush", "BorderSubtle"),
        ("ClearHoverBorder", "BorderSubtle"),
        ("BorderFocusBrush", "FocusIndicator"),
        ("InputFocusBorderBrush", "FocusIndicator"),
        ("LiquidFocusBrush", "FocusIndicator"),

        // Selection / interactive states
        ("SelectionBrush", "StateSelected"),
        ("LiquidSelectionBrush", "StateSelected"),
        ("MenuHoverBrush", "StateHover"),
        ("LiquidControlHoverBrush", "StateHover"),
        ("LiquidControlPressedBrush", "StatePressed"),
        ("InputValidationBrush", "StatusError"),

        // Status colors
        ("SuccessBrush", "StatusSuccess"),
        ("SuccessSoftBrush", "StatusSuccessSurface"),
        ("WarningBrush", "StatusWarning"),
        ("WarningSoftBrush", "StatusWarningSurface"),
        ("DangerBrush", "StatusError"),
        ("DangerSoftBrush", "StatusErrorSurface"),
        ("InfoBrush", "StatusInformation"),
        ("InfoSoftBrush", "StatusInformationSurface"),
        ("TintGreenBrush", "StatusSuccessSurface"),
        ("TintRedBrush", "StatusErrorSurface"),
        ("TintAmberBrush", "StatusWarningSurface"),
        ("TintBlueBrush", "AccentSoft"),
        ("TintIndigoBrush", "AccentSoft"),
        ("TintPurpleBrush", "AccentSoft"),
        ("TintGrayBrush", "SurfaceSecondary"),
        ("Ov.Success", "StatusSuccess"),
        ("Ov.SuccessSoft", "StatusSuccessSurface"),
        ("Ov.Danger", "StatusError"),
        ("Ov.DangerSoft", "StatusErrorSurface"),
        ("Ov.Warning", "StatusWarning"),
        ("Ov.WarningSoft", "StatusWarningSurface"),
        ("Ios26SuccessBrush", "StatusSuccess"),
        ("Ios26SuccessSoftBrush", "StatusSuccessSurface"),
        ("Ios26WarningBrush", "StatusWarning"),
        ("Ios26DangerDotBrush", "StatusError"),

        // Accent (non-derived members; the family is derived below)
        ("AccentSoftBrush", "AccentSoft"),
        ("AccentLightBrush", "AccentSoft"),
        ("Ios26AccentSoftBrush", "AccentSoft"),
        ("Ios26AccentBrush", "AccentPrimary"),
        ("Ios26AccentSoftTextBrush", "AccentPrimary"),
        ("Ios26NumberBrush", "AccentPrimary"),
        ("ChatBrand", "AccentPrimary"),
        ("Ov.Accent", "AccentPrimary"),

        // Spec TextOnAccent row (54 view files) and BorderSubtle row (7).
        ("OnAccentForegroundBrush", "TextOnAccent"),
        ("BorderSubtleBrush", "BorderSubtle"),

        // Ov.Hero (10 dashboard/pipeline view files) — flat V3 hero token
        // replaces the retired violet gradient.
        ("Ov.Hero", "HeroBackground"),

        // ── Restored eight-theme stack (de3b1b4): keys the eight-family
        // painter used to publish every switch. Audited by consumer count:
        // AccentForegroundBrush alone feeds 89 files, CardBorderThickness 33,
        // PopupSurfaceBrush 10 — none is defined in an App-merged dictionary,
        // so without these rows they resolve null once the painter is thin.
        ("AccentForegroundBrush", "TextOnAccent"),
        ("FocusRingBrush", "FocusIndicator"),
        ("CardBorderThickness", "BorderThicknessControl"),
        ("PopupSurfaceBrush", "SurfaceElevated"),   // opaque elevated (14 documented consumers)
        ("OutlineStrongBrush", "BorderStrong"),
        ("ShellLiquidBorderBrush", "BorderSubtle"),
        ("ShellSheenBrush", "SurfaceElevated"),

        // Remaining VisualThemeEngine ManagedKeys (brush-typed only):
        // Liquid*/Glass*/About* keys consumed by merged style files and
        // About sections — mapped so they follow Light/Dark like the rest.
        ("LiquidPrimaryButtonBrush", "AccentPrimary"),
        ("LiquidEdgeBrush", "BorderStrong"),
        ("LiquidInnerHighlightBrush", "BorderSubtle"),
        ("LiquidReflectionBrush", "SurfaceElevated"),
        ("GlassWaterBrush", "SurfaceSecondary"),
        ("GlassBorderBright", "BorderStrong"),
        ("AboutInteractiveBrush", "AccentSoft"),
        ("AboutInteractiveHoverBrush", "AccentHover"),
        ("AboutInteractiveBorderBrush", "BorderSubtle"),
        ("AboutInteractiveBorderStrongBrush", "BorderStrong"),
        ("AboutCardBorderBrush", "BorderSubtle"),
        ("AboutDividerBrush", "BorderSubtle"),

        // Runtime-only screen palettes (login, statement wizard,
        // dashboards, providers) — rg-verified references, no merged
        // dictionary definition, so they must be published here.
        ("AmberAccent", "StatusWarning"),
        ("Cerulean", "AccentPrimary"),
        ("CyanAccent", "AccentPrimary"),
        ("CyanAccentBar", "AccentPrimary"),
        ("IndigoAccentBar", "AccentPrimary"),
        ("IndigoPrimary", "AccentPrimary"),
        ("IndigoIconGradient", "AccentPrimary"),
        ("VioletPinkGradient", "AccentPrimary"),
        ("PrimaryGradient", "AccentPrimary"),
        ("PrimaryButtonShadow", "AccentPrimary"),
        ("GreenLed", "StatusSuccess"),
        ("MetricBalance", "StatusSuccess"),
        ("MetricDelivered", "StatusSuccess"),
        ("MetricOrdered", "AccentPrimary"),
        ("MetricRate", "TextSecondary"),
        ("CountBadgeBg", "SurfaceSecondary"),
        ("CountBadgeText", "TextSecondary"),
        ("TerminalBg", "SurfacePrimary"),
        ("TerminalTitle", "TextPrimary"),
        ("TextPrimary", "TextPrimary"),
        ("TextSecondary", "TextSecondary"),
        ("TextMuted", "TextMuted"),
        ("WarnBadgeBg", "StatusWarningSurface"),
        ("ProviderDotAmber", "StatusWarning"),
        ("ProviderDotBlue", "AccentPrimary"),
        ("ProviderDotViolet", "AccentPrimary"),
        ("SuccessGradient", "StatusSuccess"),
        ("SuccessButtonShadow", "StatusSuccess"),
        ("ToolTipShadow", "TextMuted"),
        ("DangerOnLiquid", "StatusError"),
        ("WarningOnLiquid", "StatusWarning"),
        ("Ice", "SurfacePrimary"),
        ("Frost", "SurfaceSecondary"),
        ("PageBg", "ApplicationBackground"),
        ("OrbLeft", "AccentSoft"),
        ("OrbRight", "AccentSoft"),
        ("NameGradient", "AccentPrimary"),
    };

    // ── Type-scale forwarding (Ios26* font sizes → V3 scale) ────────────
    private static readonly (string Legacy, string Token)[] TypeScaleMap =
    {
        ("Ios26FontSizeDisplay", "FontSizeDisplay"),
        ("Ios26FontSizeH1", "FontSizeDisplay"),
        ("Ios26FontSizeH2", "FontSizeTitle"),
        ("Ios26FontSizeSectionTitle", "FontSizeSection"),
        ("Ios26FontSizeCardTitle", "FontSizeTitle"),
        ("Ios26FontSizeCardTitleSm", "FontSizeSection"),
        ("Ios26FontSizeSubtitle", "FontSizeSection"),
        ("Ios26FontSizeValue", "FontSizeDisplay"),
        ("Ios26FontSizeStat", "FontSizeDisplay"),
        ("Ios26FontSizeMetric", "FontSizeDisplay"),
        ("Ios26FontSizeBody", "FontSizeBody"),
        ("Ios26FontSizeLabel", "FontSizeCaption"),
        ("Ios26FontSizeSmall", "FontSizeCaption"),
        ("Ios26FontSizeGlyph", "FontSizeCaption"),
        ("Ios26FontSizeCaption", "FontSizeCaption"),
    };

    // ── Geometry forwarding ──────────────────────────────────────────────
    // Radius hierarchy mirrors the old CopyCorner contract: the user's
    // card radius (V3 CardCornerRadius) drives every Radius.* alias.
    private static void PublishGeometry(
        ResourceDictionary package, ResourceDictionary app)
    {
        double CardRadius(ResourceDictionary source)
            => source.TryGetValue("CardCornerRadius", out var card) && card is CornerRadius c
                ? c.TopLeft
                : 12d;

        double ControlRadius(ResourceDictionary source)
            => source.TryGetValue("ControlCornerRadius", out var control) && control is CornerRadius cc
                ? cc.TopLeft
                : CardRadius(source) / 2;

        double card = CardRadius(package);
        double large = Math.Round(card * 4d / 3d, 1);
        double control = ControlRadius(package);

        SetValue(app, "CardCornerRadius", package, "CardCornerRadius");
        SetValue(app, "ControlCornerRadius", package, "ControlCornerRadius");
        SetCorner(app, "CardCornerRadiusLarge", large);
        SetCorner(app, "Radius.None", 0d);
        SetCorner(app, "RadiusCompact", 6d);
        SetCorner(app, "RadiusControl", control);
        SetCorner(app, "RadiusCard", card);
        SetCorner(app, "RadiusLarge", large);
        SetCorner(app, "RadiusDialog", large);
        SetCorner(app, "Ios26RadiusCard", card);
        SetCorner(app, "Ios26RadiusTile", card);
        SetCorner(app, "Ios26RadiusChip",
            package.TryGetValue("ChipCornerRadius", out var chip) && chip is CornerRadius chirp
                ? chirp.TopLeft : control);
        SetCorner(app, "Ios26RadiusSmall", control);
        SetCorner(app, "Ios26RadiusInner", control / 2);
        SetCorner(app, "Ios26RadiusBar", card);
    }

    // ── Accent family derivation ────────────────────────────────────────
    // Replicates the old ThemeManager.ApplyAccent derivation contract
    // (hover lighter, pressed darker, tints, on-accent contrast) from
    // the V3 AccentPrimary token, so the legacy accent keys stay a
    // coherent family instead of flat copies.
    private static void PublishAccentFamily(
        ResourceDictionary package, ResourceDictionary app, Appearance appearance)
    {
        if (!package.TryGetValue("AccentPrimary", out var raw) || raw is not SolidColorBrush primary)
            return;

        Color core = primary.Color;
        bool dark = appearance == Appearance.Dark;
        var (h, s, l) = ColorMath.ToHsl(core);

        Color hover = ColorMath.Lighten(core, dark ? 0.08 : 0.06);
        Color pressed = ColorMath.FromHsl(h, Math.Min(1, s + 0.05), Math.Max(0.16, l - 0.18));
        Color light = ColorMath.Lighten(core, 0.12);
        Color areaEnd = ColorMath.Lighten(core, 0.30);
        Color onAccentMuted = ColorMath.Lighten(core, 0.50);

        SetBrush(app, "AccentBrush", core);
        SetBrush(app, "AccentHoverBrush", hover);
        SetBrush(app, "AccentDarkBrush", pressed);
        SetBrush(app, "AccentPressedBrush", pressed);
        SetBrush(app, "AccentLightBrush", light);
        SetBrush(app, "OverlayButtonBackgroundBrush", core);
        SetBrush(app, "Ov.AccentHover", hover);
        SetBrush(app, "Ov.AreaEnd", areaEnd);
        SetBrush(app, "Ov.OnAccentMuted", onAccentMuted);
        SetBrush(app, "Ios26AccentDeepBrush", pressed);

        // Color-typed counterparts for gradient stops / DropShadowEffect.
        app["AccentColor"] = core;
        app["AccentHoverColor"] = hover;
        app["AccentDarkColor"] = pressed;
        app["AccentLightColor"] = light;
    }

    /// <summary>
    /// Publishes the frozen legacy vocabulary for the ACTIVE package.
    /// Called after every ThemeService.Apply (startup, Light/Dark toggle,
    /// theme switch). Never throws: a broken token degrades to a logged
    /// warning, because the hub runs inside the theme-apply path and must
    /// not take the app down.
    /// </summary>
    public static void Publish(ResourceDictionary package, Appearance appearance)
    {
        ArgumentNullException.ThrowIfNull(package);
        var app = Application.Current?.Resources;
        if (app == null) return;

        int written = 0;
        int failures = 0;

        foreach (var (legacy, token) in DirectMap)
        {
            if (Forward(package, app, legacy, token)) written++;
            else failures++;
        }

        foreach (var (legacy, token) in TypeScaleMap)
        {
            if (Forward(package, app, legacy, token)) written++;
            else failures++;
        }

        try { PublishAccentFamily(package, app, appearance); }
        catch (Exception ex)
        {
            AppLogService.Debug("[V3CompatibilityHub] accent family degraded: " + ex.Message);
        }

        try { PublishGeometry(package, app); }
        catch (Exception ex)
        {
            AppLogService.Debug("[V3CompatibilityHub] geometry degraded: " + ex.Message);
        }

        // LiquidShadowColor — a Color (not a brush), read by 18 view files
        // for DropShadowEffect.Color. V3's contract has no Color tokens, so
        // this one keeps the legacy per-mode values verbatim: the two are
        // the only definitions ever shipped, and inventing a derived colour
        // would change every shadow in the app.
        var shadow = appearance == Appearance.Dark
            ? Color.FromArgb(0xFF, 0x00, 0x04, 0x10)
            : Color.FromArgb(0x33, 0x1E, 0x29, 0x3B);
        app["LiquidShadowColor"] = shadow;

        // Crystal Fluid water/ambient contracts: Bento has no water
        // stage. The keys must EXIST (CrystalWaterControl reads them)
        // but stay OFF — the same contract VisualThemeEngine published
        // for every non-Crystal family.
        app["CrystalWaterDefaultEnabled"] = false;
        app["CrystalAmbientHostVisibility"] = Visibility.Collapsed;
        app["CrystalPreviewHostVisibility"] = Visibility.Collapsed;

        // Wallpaper contract: Phase 1 thin wrappers no longer paint
        // module wallpapers; publish the opacity knob so the Settings
        // slider keeps its saved value without a dead write.
        if (App.Config?.Appearance != null)
            app["WallpaperOpacityValue"] = App.Config.Appearance.WallpaperOpacity;

        if (failures > 0)
        {
            AppLogService.Debug(
                $"[V3CompatibilityHub] published {written} legacy keys, " +
                $"{failures} unresolved (token missing in package)");
        }
    }

    // ── Forwarding helpers ───────────────────────────────────────────────
    private static bool Forward(
        ResourceDictionary package, ResourceDictionary app, string legacy, string token)
    {
        if (!package.TryGetValue(token, out var value) || value is null)
            return false;

        switch (value)
        {
            case SolidColorBrush solid:
                SetBrush(app, legacy, solid.Color);
                return true;
            case Brush brush:
                // Non-solid brushes (gradients) are cloned and frozen
                // as-is; only SolidColorBrush values are re-derived so
                // the legacy key always holds a frozen, theme-owned copy.
                var clone = brush.CloneCurrentValue();
                if (clone.CanFreeze) clone.Freeze();
                app[legacy] = clone;
                return true;
            case double number:
                app[legacy] = number;
                return true;
            case Thickness thickness:
                app[legacy] = thickness;
                return true;
            case CornerRadius corner:
                SetCorner(app, legacy, corner.TopLeft);
                return true;
            case FontWeight weight:
                app[legacy] = weight;
                return true;
            case FontStyle style:
                app[legacy] = style;
                return true;
            default:
                // Anything else (Color, geometry, custom) is forwarded
                // as-is — the legacy keys that need a conversion are
                // handled by the derivation methods above.
                app[legacy] = value;
                return true;
        }
    }

    private static void SetBrush(ResourceDictionary app, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        app[key] = brush;
    }

    private static void SetCorner(ResourceDictionary app, string key, double radius)
        => app[key] = new CornerRadius(radius);

    private static void SetValue(
        ResourceDictionary app, string legacy, ResourceDictionary package, string token)
    {
        if (package.TryGetValue(token, out var value) && value is not null)
            app[legacy] = value;
    }
}
