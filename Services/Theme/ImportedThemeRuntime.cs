using System.IO;
using System.Windows;
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
        CopyBrush(engine, resources, "ApplicationBackground", "BackgroundBrush", "WindowBackgroundBrush");
        CopyBrush(engine, resources, "SurfacePrimary", "ModuleBackgroundBrush");
        CopyBrush(engine, resources, "SurfaceSecondary", "DialogInsetBrush");
        CopyBrush(engine, resources, "SurfaceElevated", "ElevatedSurfaceBrush", "SideMenuBackgroundBrush", "DialogSurfaceBrush");
        CopyBrush(engine, resources, "TextPrimary", "PrimaryForegroundBrush", "TextPrimaryBrush");
        CopyBrush(engine, resources, "TextSecondary", "SecondaryForegroundBrush", "TextSecondaryBrush");
        CopyBrush(engine, resources, "TextMuted", "MutedForegroundBrush");
        CopyBrush(engine, resources, "AccentHover", "AccentHoverBrush");
        CopyBrush(engine, resources, "AccentPressed", "AccentPressedBrush");
        CopyBrush(engine, resources, "FocusIndicator", "FocusIndicatorBrush");
        CopyBrush(engine, resources, "BorderSubtle", "BorderBrush", "OutlineDefaultBrush", "DividerBrush");
        CopyBrush(engine, resources, "StatusSuccess", "StatusSuccessBrush");
        CopyBrush(engine, resources, "StatusWarning", "StatusWarningBrush");
        CopyBrush(engine, resources, "StatusError", "StatusErrorBrush");
        CopyBrush(engine, resources, "StatusInformation", "StatusInformationBrush");
        for (int i = 1; i <= 4; i++) CopyBrush(engine, resources, $"ChartSeries{i}", $"ChartSeries{i}Brush");
        CopyTyped(engine, resources, "FontFamilyBody", "AppFontFamily", "FontFamily.Body");
        CopyTyped(engine, resources, "FontSizeBody", "FontSize.Body");
        CopyTyped(engine, resources, "FontSizeTitle", "FontSize.Title");
        CopyTyped(engine, resources, "FontSizeSection", "FontSize.Section");
        CopyTyped(engine, resources, "FontSizeCaption", "FontSize.Caption");
        CopyTyped(engine, resources, "CardCornerRadius", "CardCornerRadius", "Radius.Card");
        CopyTyped(engine, resources, "ControlCornerRadius", "ControlCornerRadius", "Radius.Control");
    }

    private static void CopyBrush(ProGlassAutomation.Theming.ThemeService source, ResourceDictionary target, string sourceKey, params string[] targetKeys)
    {
        if (source.GetToken(sourceKey) is not Brush brush) return;
        foreach (string key in targetKeys)
        {
            if (!target.Contains(key)) continue;
            if (target[key] is Brush) target[key] = brush;
        }
    }

    private static void CopyTyped(ProGlassAutomation.Theming.ThemeService source, ResourceDictionary target, string sourceKey, params string[] targetKeys)
    {
        object value = source.GetToken(sourceKey);
        foreach (string key in targetKeys)
        {
            if (!target.Contains(key)) continue;
            object existing = target[key];
            if (existing is not null && existing.GetType() == value.GetType()) target[key] = value;
        }
    }
}
