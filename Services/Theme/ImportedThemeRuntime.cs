using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Customization;
using ProGlassAutomation.ThemeClaymorphism;
using ProGlassAutomation.ThemeLiquidGlass;
using ProGlassAutomation.ThemeNeumorphism;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Theming;

namespace ProGlassAutomation.Services.Theme;

/// <summary>
/// Adapts the imported nine-package theme engine to the existing ERP resource
/// vocabulary. The ERP ThemePipeline remains the sole entry point for app theme
/// changes; this engine owns only its appended, validated package slot. The
/// adapter publishes a small, typed set of imported tokens to legacy ERP keys.
/// </summary>
public static class ImportedThemeRuntime
{
    private static readonly ProGlassAutomation.Theming.ThemeService engine = new();
    private static SavedCustomization saved = new();
    private static bool initialized;
    private static bool applyingFromHost;
    private static bool syncingStudio;
    private static bool persistenceEnabled = true;
    private static readonly Dictionary<string, Style> hostBaseStyles = new(StringComparer.Ordinal);

    public static ProGlassAutomation.Theming.ThemeService Engine => engine;
    public static bool IsInitialized => initialized;

    public static void Initialize(ResourceDictionary resources, string? hostFamily, bool dark, bool persistCustomization = true)
    {
        Application.Current.Dispatcher.VerifyAccess();
        persistenceEnabled = persistCustomization;
        if (initialized) return;
        try
        {
            saved = persistenceEnabled ? CustomizationStore.Load() : new SavedCustomization();
            string activeTheme = ToImportedTheme(hostFamily);
            var activeMode = dark ? Appearance.Dark : Appearance.Light;
            saved = saved with { Theme = activeTheme, Appearance = activeMode };
            if (persistenceEnabled)
            {
                try { CustomizationStore.Save(saved); }
                catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException)
                { Logging.AppLogService.Warning("Could not synchronize imported customization snapshot: " + ex.Message); }
            }
            engine.Initialize(resources, activeTheme, activeMode,
                Configure(saved, activeTheme));
            engine.PackageChanged += OnPackageChanged;
            initialized = true;
            IconService.Apply(saved.Icons ?? new IconCustomizationState());
            PublishToHost();
            if (persistenceEnabled && !string.IsNullOrWhiteSpace(CustomizationStore.LoadWarning))
                Logging.AppLogService.Warning("Theme customization recovered with defaults: " + CustomizationStore.LoadWarning);
        }
        catch (Exception ex)
        {
            applyingFromHost = false;
            Logging.AppLogService.Warning("Imported theme engine initialization failed; existing ERP theme remains active: " + ex.Message);
        }
    }

    /// <summary>Called as the final theme-pipeline layer after the ERP painter.</summary>
    public static void ApplyFromHost(string? hostFamily, bool dark)
    {
        if (!initialized) return;
        Application.Current.Dispatcher.VerifyAccess();
        string theme = ToImportedTheme(hostFamily);
        var appearance = dark ? Appearance.Dark : Appearance.Light;
        applyingFromHost = true;
        try
        {
            bool packageChanges = engine.ActiveTheme != theme || engine.ActiveAppearance != appearance;
            SavedCustomization latest = packageChanges && persistenceEnabled
                ? CustomizationStore.Load()
                : saved;
            SavedCustomization nextSaved = latest with { Theme = theme, Appearance = appearance };
            if (packageChanges)
                engine.Apply(theme, appearance, Configure(nextSaved, theme));
            saved = nextSaved;
            if (persistenceEnabled)
            {
                try { CustomizationStore.Save(saved); }
                catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException)
                { Logging.AppLogService.Warning("Could not save imported theme selection: " + ex.Message); }
            }
            PublishToHost();
        }
        catch (Exception ex)
        {
            Logging.AppLogService.Warning("Imported theme package was rejected; retaining last valid package: " + ex.Message);
            string fallbackFamily = ToHostTheme(engine.ActiveTheme);
            bool fallbackDark = engine.ActiveAppearance == Appearance.Dark;
            if (App.Config?.Appearance is not null &&
                (App.Config.Appearance.VisualTheme != fallbackFamily ||
                 (ThemeManager.CurrentTheme == AppTheme.Dark) != fallbackDark))
            {
                App.Config.Appearance.VisualTheme = fallbackFamily;
                App.Config.Appearance.Theme = fallbackDark ? "Dark" : "Light";
                try { ConfigService.Save(App.Config); } catch { }
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    ProGlassAutomation.Services.Settings.AppearanceService.ApplyTheme(fallbackDark)));
            }
        }
        finally { applyingFromHost = false; }
    }

    private static Action<ResourceDictionary, Appearance> Configure(SavedCustomization state, string theme) =>
        SharedGeometry.Compose(theme,
            state.Preferences ?? new BentoCustomizationState(),
            state.Clay ?? new ClayCustomizationState(),
            state.Geometry ?? new SharedGeometryState(),
            state.Neumorph ?? new NeumorphCustomizationState(),
            state.Font ?? new FontCustomizationState(),
            state.Colors ?? new ColorPlateState(),
            state.Liquid ?? new LiquidCustomizationState());

    private static string ToImportedTheme(string? family) => family switch
    {
        VisualThemeEngine.BentoGrid => ThemeService.Bento,
        VisualThemeEngine.Fluent => ThemeService.Fluent,
        VisualThemeEngine.Claymorphism => ThemeService.Clay,
        VisualThemeEngine.Neumorphism => ThemeService.Neumorphism,
        VisualThemeEngine.LiquidGlass => ThemeService.LiquidGlass,
        VisualThemeEngine.Glassmorphism => ThemeService.Glassmorphism,
        VisualThemeEngine.SpatialImmersive => ThemeService.Spatial,
        VisualThemeEngine.SwissInternational => ThemeService.Swiss,
        VisualThemeEngine.HolographicIridescent => ThemeService.Holographic,
        _ => ThemeService.Bento
    };

    private static string ToHostTheme(string imported) => imported switch
    {
        ThemeService.Bento => VisualThemeEngine.BentoGrid,
        ThemeService.Fluent => VisualThemeEngine.Fluent,
        ThemeService.Clay => VisualThemeEngine.Claymorphism,
        ThemeService.Neumorphism => VisualThemeEngine.Neumorphism,
        ThemeService.LiquidGlass => VisualThemeEngine.LiquidGlass,
        ThemeService.Glassmorphism => VisualThemeEngine.Glassmorphism,
        ThemeService.Spatial => VisualThemeEngine.SpatialImmersive,
        ThemeService.Swiss => VisualThemeEngine.SwissInternational,
        ThemeService.Holographic => VisualThemeEngine.HolographicIridescent,
        _ => VisualThemeEngine.BentoGrid
    };

    private static void OnPackageChanged(object? sender, EventArgs e)
    {
        try { PublishToHost(); }
        catch (Exception ex) { Logging.AppLogService.Warning("Imported theme token bridge failed: " + ex.Message); }
        if (!initialized || applyingFromHost || syncingStudio || App.Config?.Appearance is null) return;
        string hostFamily = ToHostTheme(engine.ActiveTheme);
        bool dark = engine.ActiveAppearance == Appearance.Dark;
        bool familyChanged = App.Config.Appearance.VisualTheme != hostFamily;
        bool modeChanged = (ThemeManager.CurrentTheme == AppTheme.Dark) != dark;
        if (!familyChanged && !modeChanged) return;

        syncingStudio = true;
        try
        {
            App.Config.Appearance.VisualTheme = hostFamily;
            ConfigService.Save(App.Config);
            if (modeChanged)
                ProGlassAutomation.Services.Settings.AppearanceService.ApplyTheme(dark);
            else
                ThemePipeline.RebuildFamily("theme studio apply");
        }
        catch (Exception ex)
        {
            Logging.AppLogService.Warning("Could not synchronize Theme Studio selection to app settings: " + ex.Message);
        }
        finally { syncingStudio = false; }
    }

    private static void PublishToHost()
    {
        if (Application.Current is not { } app) return;
        if (engine.GetToken("AccentPrimary") is SolidColorBrush accent)
            ThemeManager.ApplyAccent(accent.Color, raiseEvent: false);

        var resources = app.Resources;
        // Publish into the application's root as typed overrides. Most ERP keys
        // live in MergedDictionaries, so ResourceDictionary.Contains on the root
        // is not a reliable existence test; HasHostResource searches the full
        // application resource graph before deliberately shadowing at the root.
        CopyBrush(engine, resources, "HeroBackground", "HeroBackgroundBrush", "PgHeroBackgroundBrush");
        CopyBrush(engine, resources, "HeroForeground", "HeroForegroundBrush", "PgHeroForegroundBrush");
        CopyBrush(engine, resources, "HeroSecondaryText", "HeroSecondaryTextBrush");
        CopyBrush(engine, resources, "HeroAccent", "HeroAccentBrush");
        CopyBrush(engine, resources, "ApplicationBackground", "BackgroundBrush", "WindowBackgroundBrush", "WindowBackgroundBrush.Semantic", "PgPageBrush", "PgModuleBgBrush");
        CopyBrush(engine, resources, "SurfacePrimary", "ModuleBackgroundBrush", "SurfacePrimaryBrush", "SurfaceInputBrush", "Surface.WorkspaceBrush", "ReportPrimaryBrush", "PgSurfaceBrush", "PgPorcelainSurfaceBrush");
        CopyBrush(engine, resources, "SurfaceSecondary", "DialogInsetBrush", "SurfaceSecondaryBrush", "Surface.InputBrush", "ReportSecondaryBrush", "PgSurface2Brush", "PgLavenderRibbonBrush");
        CopyBrush(engine, resources, "SurfaceElevated", "ElevatedSurfaceBrush", "SideMenuBackgroundBrush", "DialogSurfaceBrush", "SurfaceTertiaryBrush", "SurfaceOverlayBrush", "PgSurface3Brush");
        CopyBrush(engine, resources, "TextPrimary", "PrimaryForegroundBrush", "TextPrimaryBrush", "ReportTextBrush", "ReportHeaderBrush", "PgText1Brush", "PgVioletInkBrush");
        CopyBrush(engine, resources, "TextSecondary", "SecondaryForegroundBrush", "TextSecondaryBrush", "PgText2Brush");
        CopyBrush(engine, resources, "TextMuted", "MutedForegroundBrush", "TextTertiaryBrush", "PgText3Brush", "PgTextMutedBrush");
        CopyBrush(engine, resources, "TextOnAccent", "OnAccentForegroundBrush", "SelectionForegroundBrush", "PgAccentInkBrush", "PgWhiteBrush");
        CopyBrush(engine, resources, "AccentPrimary", "AccentBrush", "AccentColorBrush", "ReportAccentBrush", "TabActiveBrush", "PgAccentBrush", "PgSelectedRowBrush", "PgFocusRimBrush");
        CopyBrush(engine, resources, "AccentSecondary", "AccentSecondaryBrush");
        CopyBrush(engine, resources, "AccentSoft", "AccentSoftBrush", "SelectionBrush", "TabHoverBrush", "PgAccentSoftBrush");
        CopyBrush(engine, resources, "AccentHover", "AccentHoverBrush", "PgAccentHoverBrush", "PgLavenderHoverBrush");
        CopyBrush(engine, resources, "AccentPressed", "AccentPressedBrush", "TabPressedBrush", "PgLavenderPressedBrush");
        CopyBrush(engine, resources, "FocusIndicator", "FocusIndicatorBrush", "BorderFocusBrush", "InputFocusBorderBrush", "Input.FocusBrush", "TabFocusBrush");
        CopyBrush(engine, resources, "BorderSubtle", "BorderBrush", "OutlineDefaultBrush", "DividerBrush", "BorderDefaultBrush", "ReportBorderBrush", "PgHairlineBrush", "PgKeylineBrush");
        CopyBrush(engine, resources, "BorderStrong", "BorderStrongBrush", "OutlineStrongBrush", "InputBorderBrush", "PgHairlineStrongBrush");
        CopyBrush(engine, resources, "StateHover", "StateHoverBrush");
        CopyBrush(engine, resources, "StatePressed", "StatePressedBrush");
        CopyBrush(engine, resources, "StateSelected", "StateSelectedBrush");
        CopyBrush(engine, resources, "DisabledSurface", "DisabledSurfaceBrush");
        CopyBrush(engine, resources, "DisabledText", "DisabledTextBrush", "TextDisabledBrush");
        CopyBrush(engine, resources, "StatusSuccess", "StatusSuccessBrush", "SuccessBrush", "PgOkBrush");
        CopyBrush(engine, resources, "StatusSuccessSurface", "StatusSuccessSurfaceBrush", "SuccessSoftBrush", "PgOkSoftBrush");
        CopyBrush(engine, resources, "StatusWarning", "StatusWarningBrush", "WarningBrush", "PgWarnBrush");
        CopyBrush(engine, resources, "StatusWarningSurface", "StatusWarningSurfaceBrush", "WarningSoftBrush", "PgWarnSoftBrush");
        CopyBrush(engine, resources, "StatusError", "StatusErrorBrush", "DangerBrush", "PgDangerBrush");
        CopyBrush(engine, resources, "StatusErrorSurface", "StatusErrorSurfaceBrush", "DangerSoftBrush", "PgDangerSoftBrush");
        CopyBrush(engine, resources, "StatusInformation", "StatusInformationBrush", "InfoBrush");
        CopyBrush(engine, resources, "StatusInformationSurface", "StatusInformationSurfaceBrush", "InfoSoftBrush");
        CopyBrush(engine, resources, "OverlayBrush", "OverlayBrush");
        CopyBrush(engine, resources, "ChartAxis", "ChartAxisBrush");
        CopyBrush(engine, resources, "ChartGrid", "ChartGridBrush");
        CopyBrush(engine, resources, "ChartTooltipSurface", "ChartTooltipSurfaceBrush");
        CopyBrush(engine, resources, "ChartTooltipText", "ChartTooltipTextBrush");
        for (int i = 1; i <= 4; i++) CopyBrush(engine, resources, $"ChartSeries{i}", $"ChartSeries{i}Brush");

        CopyTyped(engine, resources, "FontFamilyBody", "AppFontFamily", "FontFamily.Body", "FontFamily.App");
        CopyTyped(engine, resources, "FontFamilyMono", "FontFamily.Mono");
        CopyTyped(engine, resources, "FontSizeDisplay", "FontSize.Display", "TypographyDisplaySize", "PgFontHero");
        CopyTyped(engine, resources, "FontSizeTitle", "FontSize.Title", "TypographyTitleSize", "TypographyH1Size", "PgFontCardTitle", "PgFontKpiValue", "PgFontStatValue", "PgFontStageValue");
        CopyTyped(engine, resources, "FontSizeSection", "FontSize.Section", "TypographyH2Size", "TypographySubtitleSize", "PgFontHeroSub", "PgFontCardSub", "PgFontKpiLabel", "PgFontStatLabel", "PgFontStageName");
        CopyTyped(engine, resources, "FontSizeBody", "FontSize.Body", "FontSize.Input", "FontSize.DataGrid", "TypographyBodySize", "PgFontBody", "PgFontBtn", "PgTopFont", "PgTopFontBtn");
        CopyTyped(engine, resources, "FontSizeCaption", "FontSize.Caption", "TypographyCaptionSize", "TypographyLabelSize", "PgFontBodySm", "PgFontMeta", "PgFontStatFoot", "PgFontKpiFoot", "PgFontStageFoot", "PgFontGlyphSm", "PgTopFontCap");
        CopyTyped(engine, resources, "FontWeightHeading", "Weight.SemiBold", "TypographyHeadingWeight", "TypographyTitleWeight");
        CopyTyped(engine, resources, "FontWeightBody", "Weight.Regular", "TypographyBodyWeight");
        CopyTyped(engine, resources, "CardCornerRadius", "CardCornerRadius", "Radius.Card", "Radius.Panel", "PgCardRadius", "PgTileRadius");
        CopyTyped(engine, resources, "ControlCornerRadius", "ControlCornerRadius", "Radius.Control", "Radius.Small", "PgCtrlRadius", "PgIconRadius", "PgIconSmRadius");
        CopyTyped(engine, resources, "ChipCornerRadius", "Radius.Chip", "Radius.Badge", "Radius.Tab", "PgChipRadius");
        CopyTyped(engine, resources, "ControlMinHeight", "Density.ControlHeight", "ControlHeight.Medium", "PgTopCtrlHeight", "PgSidebarItemHeight", "PgSidebarSubItemHeight");
        CopyTyped(engine, resources, "BentoRowHeight", "Density.RowHeight");
        CopyTyped(engine, resources, "SpacingSmall", "Spacing.XS", "SpacingXS", "Space.Compact");
        CopyTyped(engine, resources, "SpacingMedium", "Spacing.MD", "SpacingMD", "Space.Medium");
        CopyTyped(engine, resources, "SpacingLarge", "Spacing.LG", "SpacingLG", "Space.Large");
        CopyTyped(engine, resources, "SpacingSection", "Spacing.Section");
        CopyTyped(engine, resources, "SpacingPage", "Spacing.Page");
        CopyTyped(engine, resources, "PaddingControl", "Space.Control", "ControlPadding", "PgTilePadding");
        CopyTyped(engine, resources, "PaddingCard", "Space.Card", "CardPadding", "Density.CardPadding", "PgCardPadding");
        CopyTyped(engine, resources, "PaddingPage", "Space.Page", "PagePadding", "Density.PagePadding", "PgPageMargin");
        CopyTyped(engine, resources, "PaddingCompact", "Space.CompactPadding", "PgRowPadding");
        CopyTyped(engine, resources, "MarginSection", "PgGutter");
        CopyTyped(engine, resources, "MarginItem", "PgGutterHalfMargin");
        CopyTyped(engine, resources, "BorderThicknessControl", "Border.Default", "BorderThickness.Control");
        CopyTyped(engine, resources, "FocusThickness", "Border.Focus", "FocusThickness");
        CopyTyped(engine, resources, "DisabledOpacity", "Opacity.Disabled");
        CopyTyped(engine, resources, "ChartPlotHeight", "Chart.PlotHeight");
        CopyTyped(engine, resources, "ChartLineWidth", "Chart.LineWidth");
        CopyTyped(engine, resources, "ChartPointSize", "Chart.PointSize");
        PublishHostStyleAliases(resources);
    }

    /// <summary>
    /// Route the application's canonical explicit styles through the active
    /// imported package. These are intentionally narrow aliases: TextBoxes,
    /// editable ComboBoxes, DataGrids, and business-specific templates keep
    /// their host contracts rather than being replaced with demo templates.
    /// </summary>
    private static void PublishHostStyleAliases(ResourceDictionary resources)
    {
        // Explicit host styles opt controls out of WPF's implicit style lookup.
        // Preserve each ERP style as the BasedOn contract, then overlay only
        // presentation properties from the active package's type styles. This
        // intentionally leaves templates, bindings, validation, selection,
        // editing and command behavior owned by the ERP.
        (string HostKey, object PackageKey, bool FullPackageStyle)[] aliases =
        [
            ("PrimaryButtonStyle", "PrimaryButton", true),
            ("ModuleToolbarPrimaryButtonStyle", "PrimaryButton", true),
            ("Button.Primary", "PrimaryButton", true),
            ("SecondaryButtonStyle", typeof(Button), true),
            ("Button.Secondary", typeof(Button), true),
            ("TextBoxStyle", typeof(TextBox), false),
            ("Input.TextBox", typeof(TextBox), false),
            ("SurfaceTextBox", typeof(TextBox), false),
            ("Step4InputStyle", typeof(TextBox), false),
            ("ComboBoxStyle", typeof(ComboBox), false),
            ("ModernComboBoxStyle", typeof(ComboBox), false),
            ("Step4ComboStyle", typeof(ComboBox), false),
            ("SurfaceDataGrid", typeof(DataGrid), false),
            ("GridHeaderCell", typeof(DataGridColumnHeader), false)
        ];

        foreach ((string hostKey, object packageKey, bool fullPackageStyle) in aliases)
        {
            if (Application.Current?.TryFindResource(hostKey) is not Style currentHostStyle)
                continue;
            if (!hostBaseStyles.TryGetValue(hostKey, out Style? baseStyle))
                hostBaseStyles[hostKey] = baseStyle = currentHostStyle;
            if (engine.GetResource(packageKey) is not Style packageStyle ||
                packageStyle.TargetType != baseStyle.TargetType)
                continue;

            if (fullPackageStyle)
            {
                resources[hostKey] = packageStyle;
                continue;
            }

            var adapted = new Style(baseStyle.TargetType, baseStyle);
            foreach (SetterBase setterBase in packageStyle.Setters)
            {
                if (setterBase is not Setter setter || !IsSafeThemeSetter(setter.Property))
                    continue;
                adapted.Setters.Add(new Setter(setter.Property, setter.Value));
            }
            resources[hostKey] = adapted;
        }
    }

    private static bool IsSafeThemeSetter(DependencyProperty property) =>
        property == Control.BackgroundProperty ||
        property == Control.ForegroundProperty ||
        property == Control.BorderBrushProperty ||
        property == Control.BorderThicknessProperty ||
        property == Control.FontFamilyProperty ||
        property == Control.FontSizeProperty ||
        property == Control.FontWeightProperty ||
        property == Control.MinHeightProperty ||
        property == Control.PaddingProperty ||
        property == Control.HorizontalContentAlignmentProperty ||
        property == Control.VerticalContentAlignmentProperty;

    private static bool HasHostResource(string key) =>
        Application.Current?.TryFindResource(key) is not null;

    private static void CopyBrush(ProGlassAutomation.Theming.ThemeService source, ResourceDictionary target, string sourceKey, params string[] targetKeys)
    {
        if (source.GetToken(sourceKey) is not Brush brush) return;
        foreach (string key in targetKeys)
        {
            if (!HasHostResource(key)) continue;
            object? existing = Application.Current?.TryFindResource(key);
            if (existing is Brush) target[key] = brush;
        }
    }

    private static void CopyTyped(ProGlassAutomation.Theming.ThemeService source, ResourceDictionary target, string sourceKey, params string[] targetKeys)
    {
        object value = source.GetToken(sourceKey);
        foreach (string key in targetKeys)
        {
            if (!HasHostResource(key)) continue;
            object? existing = Application.Current?.TryFindResource(key);
            if (existing is not null && existing.GetType() == value.GetType()) target[key] = value;
        }
    }
}
