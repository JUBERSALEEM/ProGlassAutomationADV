// ═══════════════════════════════════════════════════════════════════════
// FILE: Theme/V3/V3ThemeApplier.cs
// PROJECT: ProGlassAutomation (ADV)
// MODULE: V3 theme engine — apply + dual-write orchestration
//
// PURPOSE (Phase 1, adapter):
//   One canonical entry point for "activate a V3 theme package and
//   publish the legacy vocabulary". Every legacy theme entry point
//   (ThemeManager.ApplyTheme, ThemePipeline.Rebuild,
//   VisualThemeEngine.ApplyModule, AppearanceService.ApplyTheme)
//   funnels through Apply() so there is exactly ONE place that swaps
//   the package and ONE place that dual-writes. No ClearOverrides,
//   no family painters.
//
// INITIALIZATION: ThemeService.Initialize() must run once on the UI
// thread before the first Apply. Startup wiring calls EnsureInitialized().
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System.Windows;

namespace ProGlassAutomation.Theming;

/// <summary>
/// Orchestrates V3 package activation + legacy dual-write for ADV.
/// Thread-affine: every method requires the UI thread (Dispatcher).
/// </summary>
public static class V3ThemeApplier
{
    private static ThemeService? instance;
    private static bool initialized;

    /// <summary>The active V3 service (null until EnsureInitialized).</summary>
    public static ThemeService? Instance => instance;

    /// <summary>Initializes the engine once and activates the Bento package.</summary>
    public static void EnsureInitialized(Appearance appearance = Appearance.Light)
    {
        if (initialized) return;

        var resources = Application.Current?.Resources;
        if (resources == null)
        {
            AppLogService.Warning(
                "V3 theme engine skipped: Application.Current is not available yet.");
            return;
        }

        try
        {
            instance ??= new ThemeService();
            instance.Initialize(resources);
            initialized = true;

            // Activate the package and publish the legacy vocabulary in
            // one step so the first frame already speaks both languages.
            Apply(appearance);
        }
        catch (Exception ex)
        {
            // A failed V3 initialisation must not take down startup: the
            // merged legacy dictionaries still render. Log loudly so the
            // failure is diagnosable instead of a blank canvas.
            AppLogService.Warning(
                "V3 theme engine initialization failed; legacy dictionaries remain active: "
                + ex.Message,
                new LogContext { Module = "Themes", Operation = "V3Initialize" });
        }
    }

    /// <summary>
    /// Activates the given appearance on the active theme and dual-writes
    /// the legacy vocabulary. The active theme is ThemeService.ActiveTheme
    /// (Bento until Phase 4 ports the remaining packages).
    /// </summary>
    public static void Apply(Appearance appearance)
    {
        if (instance == null)
        {
            EnsureInitialized(appearance);
            if (instance == null) return;
        }

        try
        {
            instance.Apply(instance.ActiveTheme, appearance);
        }
        catch (Exception ex)
        {
            AppLogService.Warning(
                $"V3 theme apply failed ({instance.ActiveTheme} / {appearance}): {ex.Message}",
                new LogContext { Module = "Themes", Operation = "V3Apply" });
            return;
        }

        try
        {
            // The active package is the source of truth for the dual-write.
            V3CompatibilityHub.Publish(instance.ActivePackage, appearance);
        }
        catch (Exception ex)
        {
            AppLogService.Warning(
                "V3 legacy dual-write failed: " + ex.Message,
                new LogContext { Module = "Themes", Operation = "V3DualWrite" });
        }

        try
        {
            // ── User-preference overlay — Phase 1 stand-in for the Phase 3
            // customization store. The package carries THEME values; the
            // operator's persisted accent / radius / outline choices are
            // user work and must survive every apply, so they are
            // re-derived after the dual-write in the order ThemePipeline
            // used to run them (radius → accent → outline → semantics).
            ApplyUserPreferences();
        }
        catch (Exception ex)
        {
            AppLogService.Warning(
                "V3 user-preference overlay failed: " + ex.Message,
                new LogContext { Module = "Themes", Operation = "V3Overlay" });
        }
    }

    /// <summary>
    /// Re-publishes the operator's persisted appearance preferences on top
    /// of the freshly applied package. Not theme painting — restoring the
    /// user's own settings after every package swap.
    /// </summary>
    private static void ApplyUserPreferences()
    {
        var config = App.Config?.Appearance;

        ProGlassAutomation.Services.ThemeManager.ReapplyCardCornerRadius();
        ProGlassAutomation.Services.ThemeManager.ReapplyCardBorderThickness();
        ProGlassAutomation.Services.ThemeManager.ReapplyCardEdgeThickness();

        // Persisted accent — preset key or free-pick hex — resolved the
        // same way the startup path resolves it (App.StartupCore 9e).
        string preset = config?.AccentPreset ?? "GlassBlue";
        System.Windows.Media.Color? accent =
            preset == ProGlassAutomation.Services.Theme.AccentPresets.Custom
                ? ProGlassAutomation.Services.Theme.AccentPresets.TryParseHex(
                    config?.CustomAccentHex)
                : ProGlassAutomation.Services.Theme.AccentPresets.ColorFor(preset);
        ProGlassAutomation.Services.ThemeManager.ApplyAccent(accent, raiseEvent: false);

        ProGlassAutomation.Services.ThemeManager.ReapplyOutlinePreference();
        ProGlassAutomation.Services.ThemeManager.ReapplySemanticSurfaces();

        ProGlassAutomation.Services.Theme.AboutSurfaceTokens.Publish(
            Application.Current?.Resources,
            ProGlassAutomation.Services.ThemeManager.CardCornerRadius);
    }
}
