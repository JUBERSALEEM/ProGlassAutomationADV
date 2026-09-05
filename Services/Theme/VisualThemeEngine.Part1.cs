// ═══════════════════════════════════════════════════════════════════════
// FILE: VisualThemeEngine.Part1.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProGlassAutomation.Services.Theme
{
        public static partial class VisualThemeEngine
    {
            public const string Normal = "Normal";
        public const string Monet = "Monet";
        public const string Professional = "Professional";
        /// <summary>The default family — iOS Liquid Glass (2026-08-29).</summary>
        public const string LiquidGlass = "iOS Liquid Glass";
        /// <summary>CRYSTAL FLUID (2026-09-05) — iOS water-glass design
        /// system; REPLACES "Ultra Transparent" (retired). Surfaces are
        /// translucent crystal (.43→.14 white @135°) containing animated
        /// water, over a pale luminous environment. Light-only family
        /// (ModePairFor → false). See Themes/CrystalFluid/.</summary>
        public const string CrystalFluid = "Crystal Fluid";

        /// <summary>User-facing family label. Identity strings (the consts)
        /// are PERSISTED keys — configs, profiles and window state store
        /// them verbatim, so they can never be renamed in place. Display
        /// names are a pure view concern and live only here.
        /// Ios-17pm (2026-09-05) is the authored design-system name for
        /// the Crystal Fluid family.</summary>
        public static string FamilyDisplayName(string family) =>
            family == CrystalFluid ? "Ios-17pm" : family;

        /// <summary>LEGACY ALIAS — saved configs / old row references that
        /// still say "Ultra Transparent" land on Crystal Fluid. Do not use
        /// in new code; kept so the retired identity remains RESOLVABLE
        /// (Normalize() also accepts the old display strings).</summary>
        public const string UltraTransparent = CrystalFluid;
        public const string Ocean = "Ocean";

        /// <summary>Crystal Fluid's tunable water tokens (2026-09-05):
        /// the tokens file carries the defaults; family paint writes
        /// root-level baselines; the studio may put its mapped values on
        /// top. Removed at EVERY family switch so other families and the
        /// static defaults always reign (never a stale studio leak).
        /// CrystalWaterDefaultEnabled is handled inline beside this list
        /// because removal must ALSO happen where no loop exists.</summary>
        private static readonly string[] CrystalFluidTokenKeys =
        {
            "CrystalWaterOpacity", "CrystalWaterSpeed",
            "CrystalCausticOpacity", "CrystalBubbleOpacity",
            "CrystalReflectionOpacity", "CrystalReducedMotion",
            // Ios-17pm P3: wave amplitude joined the live axis set.
            "CrystalWaveAmplitude",
        };

        /// <summary>Keys the engine may override (removed on Normal).</summary>
        private static readonly string[] ManagedKeys =
        {
            "WindowBackgroundBrush", "ModuleBackgroundBrush",
            "ElevatedSurfaceBrush", "SideMenuBackgroundBrush",
            "MenuHoverBrush",
            "SecondaryForegroundBrush", "MutedForegroundBrush",
            "CardCornerRadius", "CardCornerRadiusLarge",
            "ControlCornerRadius",
            "WallpaperOpacityValue",
            "ModuleWallpaperBrush",

            // Status-tint cards/chips (InfoCard + the BrushKeyConverter
            // chips in dashboards/lists). The iOS Liquid Glass family
            // renders these as see-through hue frost (hue kept, alpha
            // dropped), so the overrides must be cleaned up on family
            // switch exactly like the surfaces above.
            "TintIndigoBrush", "TintBlueBrush", "TintGreenBrush",
            "TintRedBrush", "TintAmberBrush", "TintPurpleBrush",
            "TintGrayBrush",

            // Dashboard surface + ambient layer, and the shell (top bar)
            // — the iOS family re-skins these to the aurora stage.
            "DashLiquidSurfaceBrush", "DashAmbientBrush",
            "DashAmbientBlob1Brush", "DashAmbientBlob2Brush",
            "ShellLiquidBrush", "SidebarCardBrush",

            // Liquid Glass design system tokens (2026-08-29): the
            // iOS Liquid Glass family writes accent-tinted crystal values;
            // every other family restores the theme-file defaults.
            "LiquidGlassBrush", "LiquidGlassSubtleBrush",
            "LiquidGlassStrongBrush",
            "LiquidEdgeBrush", "LiquidInnerHighlightBrush",
            "LiquidReflectionBrush",
            "LiquidControlHoverBrush", "LiquidControlPressedBrush",
            "LiquidControlDisabledBrush",
            "LiquidFocusBrush", "LiquidSelectionBrush",
            "LiquidPrimaryButtonBrush", "LiquidOverlayBrush",
            "LiquidShadowColor",

            // Glass surface brushes — Crystal Fluid overrides
            // these to the crystal fill; they must be cleaned up
            // when switching back to other families.
            "GlassUltraClearBrush", "GlassSurfaceBrush",
            "GlassSurfaceElevated", "GlassSurfaceUltra",
            "GlassSurfaceStandard", "GlassCrystalBrush",
            "GlassWaterBrush", "GlassFrostedBrush",
            "GlassBorderGradientCrystal", "GlassBorderSoft",
            "ShadowSoft", "ShadowElevated",
            "GlassShadowSoft", "GlassShadowSoftBorder",
            "GlassShadowSoftButton", "GlassShadowSoftCheckBox",
            "GlassShadowSoftTextBox", "GlassShadowSoftToggle",
            "LiquidCard",
            "AboutInteractiveBrush", "AboutInteractiveHoverBrush",
            "AboutInteractiveBorderBrush", "AboutInteractiveBorderStrongBrush",
            "AboutCardBorderBrush", "AboutDividerBrush",

            // Liquid typography: the About page's text palette is
            // applied so text auto-adapts when every card goes
            // transparent.
            "PrimaryForegroundBrush",

            // TextPrimaryBrush / TextSecondaryBrush (2026-09-01): these
            // app-scope keys are written by ThemeManager.PublishLiquidReference
            // on EVERY Light/Dark swap. They were NOT managed, so even in
            // Normal the cool-slate secondary (#991E2832 Light) survived
            // ClearOverrides and permanently masked the warm theme-file
            // value — the "by-default bluish system theme". Normal now
            // restores the theme dictionary's own warm ink.
            "TextPrimaryBrush", "TextSecondaryBrush",

            // The card keyline. Monet and Professional recolour every
            // surface, so a keyline fixed by the base theme stops
            // relating to the surface it outlines and the cards read as
            // flat. These families now derive it from their own palette
            // and it is removed again on Normal, exactly like the
            // surfaces above.

            // NOTE: the four keyline keys - OutlineBrush,
            // OutlineDefaultBrush, BorderBrush, DividerBrush - are
            // deliberately NOT listed here.
            //
            // ThemeManager.ApplyAccent derives them from the accent hue
            // and writes them into the same application-level dictionary.
            // Listing them would make ClearOverrides delete the
            // accent-derived values every time a visual family is applied
            // - including on Normal, which is the family most users run -
            // and the cards would drop back to the base theme grey. That
            // is precisely the bug this whole sequence of changes was
            // chasing. Leave them out.
        };

        /// <summary>Currently active family.</summary>
        public static string Current { get; private set; } = Normal;

        /// <summary>Module whose wallpaper is currently layered (or "").</summary>
        public static string CurrentModule { get; private set; } = "";

        /// <summary>
        /// Last module key the engine was asked to render — preserved
        /// across re-applies (accent change, opacity drag, theme swap)
        /// so that module's own wallpaper override is restored instead
        /// of being forgotten.
        /// </summary>
        public static string LastRequestedModule { get; private set; } = "";

        // Last wallpaper the engine was asked to use — lets the accent
        // re-apply (Monet) rebuild the exact same scene.
        private static string _lastWallpaperPath = "";
        private static double _lastWallpaperOpacity = 0.85;

        // ── Scene de-duplication (2026-09-02, splash colour-bounce fix) ──
        // Fingerprint of the last scene ApplyModule actually rendered.
        // See the guard at the top of ApplyModule for the full rationale.
        private static string _lastAppliedSignature = "";
        private static bool _hasAppliedOnce;

        /// <summary>
        /// Fingerprints everything ApplyModule's output depends on:
        /// colour mode, family, module key, system wallpaper + opacity and
        /// that module's own wallpaper override. Equal signature ⇒ pixel-
        /// identical result ⇒ safe (and necessary) to skip.
        /// </summary>
        private static string BuildSceneSignature(
            string? moduleKey, AI.AIConfig.AppearanceSettings? appearance)
        {
            string mod = moduleKey ?? "";
            string family = Normalize(appearance?.VisualTheme ?? Normal);
            string wall = appearance?.WallpaperPath ?? "";
            double op = ClampOpacity(appearance?.WallpaperOpacity ?? 0.85);

            string moduleWall = "";
            if (!string.IsNullOrEmpty(mod) &&
                appearance?.ModuleWallpapers != null &&
                appearance.ModuleWallpapers.TryGetValue(mod, out var mw) &&
                mw != null)
            {
                moduleWall = $"{mw.Enabled}|{mw.Path}|{ClampOpacity(mw.Opacity):F3}";
            }

            // ThemeManager's Light/Dark mode and the accent are inputs too:
            // the family derives its surfaces from them.
            return string.Join("¦",
                ThemeManager.CurrentTheme,
                ThemeManager.AccentColor.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                family, mod, wall, op.ToString("F3",
                    System.Globalization.CultureInfo.InvariantCulture),
                moduleWall,
                FontPreferenceSignature());
        }

        /// <summary>Global font-colour preference participates in the
        /// scene, because ApplyFontPreference is the last write of every
        /// apply pass.</summary>
        private static string FontPreferenceSignature()
        {
            return App.Config?.Appearance?.FontColor ?? "";
        }

        /// <summary>
        /// Drop the de-duplication fingerprint so the very next
        /// ApplyModule re-renders unconditionally. Called by everything
        /// that mutates theme state OUTSIDE ApplyModule (family swap via
        /// Apply, Light/Dark toggle, accent change, Theme Studio).
        /// </summary>
        public static void InvalidateSceneCache()
        {
            _lastAppliedSignature = "";
        }

        // Phase 4: Named handler for static event — detachable, no lambda leak
        private static readonly EventHandler _accentChangedHandler = OnAccentChanged;

        /// <summary>
        /// Re-derive Monet's accent-tinted palette whenever the user
        /// changes the accent colour, then re-layer the wallpaper of
        /// the module on screen so nothing disappears. Subscribed
        /// exactly once (static engine lives for the whole process).
        /// Phase 4: Now named handler + WeakEventManager option.
        /// </summary>
        static VisualThemeEngine()
        {
            ThemeManager.AccentChanged += _accentChangedHandler;
        }

        public static void Unsubscribe()
        {
            try { ThemeManager.AccentChanged -= _accentChangedHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[VisualThemeEngine] AccentChanged detach failed: " + ex.Message); }
        }

        private static void OnAccentChanged(object? sender, EventArgs e)
        {
            // Monet and iOS Liquid Glass derive their surfaces and
            // window stage from the active accent colour, so both must
            // re-derive when the user changes the accent.
            if (Current != Monet && Current != LiquidGlass && Current != CrystalFluid)
                return;
            InvalidateSceneCache();
            Apply(Current, _lastWallpaperPath, _lastWallpaperOpacity);
            ApplyModule(LastRequestedModule, App.Config?.Appearance);
        }

        /// <summary>
        /// Applies one family. wallpaperPath/opacity feed the Monet
        /// window background and the system-wide wallpaper layer.
        /// Never throws.
        /// </summary>
        public static void Apply(
            string family, string wallpaperPath = "",
            double wallpaperOpacity = 0.85)
        {
            var resources = Application.Current?.Resources;
            if (resources == null) return;

            // Apply() writes the family baseline directly; the module
            // fingerprint no longer describes what is on screen, so the
            // next ApplyModule must run for real.
            InvalidateSceneCache();

            try
            {
                _lastWallpaperPath = wallpaperPath ?? "";
                _lastWallpaperOpacity = ClampOpacity(wallpaperOpacity);

                ClearOverrides(resources);
                // Crystal Fluid gate (non-managed key by design —
                // NOT in ManagedKeys; see tokens file). Removed at
                // every family switch so the static tokens-file
                // default (False) reigns for non-crystal families.
                resources.Remove("CrystalWaterDefaultEnabled");
                // Ios-17pm P4a: family-gated shell ambient host
                // (MainWindow wallpaper layer) — removed with the
                // same contract as the water gate.
                resources.Remove("CrystalAmbientHostVisibility");
                foreach (var ck in CrystalFluidTokenKeys)
                    resources.Remove(ck);
                // PRE-SEED (2026-09-05 — ERR-50472F): ClearOverrides just
                // opened a window where managed keys are ABSENT. A
                // template inflating against this dictionary in the same
                // dispatcher turn then dies with
                // ResourceReferenceKeyNotFoundException ('DashAmbientBrush'
                // crashed a customer's app mid-switch). Seeding the
                // critical set now means every key transitions
                // old → fallback → family value — never through ABSENT.
                GuaranteeCriticalResources(resources, silent: true);
                Current = Normalize(family);

                switch (Current)
                {
                    case Monet:
                        ApplyMonet(resources, _lastWallpaperPath,
                            _lastWallpaperOpacity);
                        break;

                    case Professional:
                        ApplyProfessional(resources);
                        break;

                    case LiquidGlass:
                        ApplyLiquidGlass(resources);
                        break;

                    case CrystalFluid:
                        ApplyCrystalFluid(resources);
                        break;

                    case Ocean:
                        ApplyOcean(resources);
                        break;

                    // Industrial family (2026-09-05): plain solid ERP look,
                    // three shade layouts.
                    case IndustrialWhite:
                        ApplyIndustrialWhite(resources);
                        break;
                    case IndustrialGrey:
                        ApplyIndustrialGrey(resources);
                        break;
                    case IndustrialDark:
                        ApplyIndustrialDark(resources);
                        break;
                }

                // System-wide wallpaper chrome: paint the window
                // background for non-Monet families (Monet handles it
                // above). The module-area layer is owned by
                // ApplyModule, which callers invoke right after this —
                // that keeps module-specific wallpapers authoritative.
                if (Current != Monet &&
                    !string.IsNullOrWhiteSpace(_lastWallpaperPath) &&
                    File.Exists(_lastWallpaperPath))
                {
                    var windowBrush = BuildWallpaperBrush(
                        _lastWallpaperPath, _lastWallpaperOpacity);
                    if (windowBrush != null)
                        resources["WindowBackgroundBrush"] = windowBrush;
                }

                // Last: the global Font Color preference (or its Auto
                // restore) so a user-chosen text palette always wins
                // over whatever the family/wallpaper just wrote.
                ApplyFontPreference(resources, false);

                AppLogService.Info(
                    $"Visual theme applied: {Current}",
                    new LogContext
                    {
                        Module = "Themes",
                        Class = nameof(VisualThemeEngine),
                        Operation = "Apply"
                    });
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Visual theme apply failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Themes",
                        Class = nameof(VisualThemeEngine)
                    });
            }
            finally
            {
                // Last line of defense — the UI must never be left with
                // unresolved surfaces, text or window stage.
                GuaranteeCriticalResources(resources);
            }
        }

        // ── PER-MODULE WALLPAPER (§9) ────────────────────────────────────

        /// <summary>
        /// Called on every module navigation. Rebuilds the base family
        /// first, then layers that module's wallpaper (when configured)
        /// — otherwise the system-wide wallpaper — so every screen can
        /// carry a background image.
        /// </summary>
        public static void ApplyModule(
            string moduleKey,
            AI.AIConfig.AppearanceSettings appearance)
        {
            var resources = Application.Current?.Resources;
            if (resources == null) return;

            // ── IDEMPOTENCE GUARD (2026-09-02) ───────────────────────────
            // ApplyModule tears the family down (ClearOverrides removes ~60
            // brush keys from the application resource scope) and rebuilds
            // it. Every DynamicResource-bound surface therefore repaints
            // TWICE — once to the theme-file fallback, once to the rebuilt
            // family value. That double repaint is the visible colour
            // "bounce" users reported over the splash and on navigation.
            //
            // During startup alone this ran 3-4 times with byte-identical
            // inputs (extra XAML-constructed MainViewModel, DI MainViewModel,
            // Settings' ApplyVisualTheme, MainWindow retry). Re-applying an
            // identical scene is a pure no-op visually but an expensive,
            // FLASHING no-op — so skip it.
            //
            // The signature covers every input the render depends on. Any
            // real change (family, module, wallpaper path/opacity, per-module
            // override) produces a different signature and applies normally.
            string signature = BuildSceneSignature(moduleKey, appearance);
            if (signature == _lastAppliedSignature && _hasAppliedOnce)
                return;

            try
            {
                LastRequestedModule = moduleKey ?? "";

                _lastWallpaperPath = appearance?.WallpaperPath ?? "";
                _lastWallpaperOpacity =
                    ClampOpacity(appearance?.WallpaperOpacity ?? 0.85);

                // Rebuild the family baseline (removes the previous
                // module's wallpaper layer).
                ClearOverrides(resources);
                // Crystal Fluid gate (non-managed key by design —
                // NOT in ManagedKeys; see tokens file). Removed at
                // every family switch so the static tokens-file
                // default (False) reigns for non-crystal families.
                resources.Remove("CrystalWaterDefaultEnabled");
                // Ios-17pm P4a: family-gated shell ambient host
                // (MainWindow wallpaper layer) — removed with the
                // same contract as the water gate.
                resources.Remove("CrystalAmbientHostVisibility");
                foreach (var ck in CrystalFluidTokenKeys)
                    resources.Remove(ck);
                // PRE-SEED (2026-09-05 — ERR-50472F): never leave the
                // dictionary with absent keys between teardown and the
                // family republish — see Apply() for the full rationale.
                GuaranteeCriticalResources(resources, silent: true);
                Current = Normalize(appearance?.VisualTheme ?? Normal);

                switch (Current)
                {
                    case Monet:
                        ApplyMonet(resources, _lastWallpaperPath,
                            _lastWallpaperOpacity);
                        break;
                    case Professional:
                        ApplyProfessional(resources);
                        break;
                    case LiquidGlass:
                        ApplyLiquidGlass(resources);
                        break;
                    case CrystalFluid:
                        ApplyCrystalFluid(resources);
                        break;
                    case Ocean:
                        ApplyOcean(resources);
                        break;

                    // Industrial family (2026-09-05): plain solid ERP look,
                    // three shade layouts.
                    case IndustrialWhite:
                        ApplyIndustrialWhite(resources);
                        break;
                    case IndustrialGrey:
                        ApplyIndustrialGrey(resources);
                        break;
                    case IndustrialDark:
                        ApplyIndustrialDark(resources);
                        break;
                }

                if (Current != Monet &&
                    !string.IsNullOrWhiteSpace(_lastWallpaperPath) &&
                    File.Exists(_lastWallpaperPath))
                {
                    var windowBrush = BuildWallpaperBrush(
                        _lastWallpaperPath, _lastWallpaperOpacity);
                    if (windowBrush != null)
                        resources["WindowBackgroundBrush"] = windowBrush;
                }

                CurrentModule = "";

                // 1) Module-specific wallpaper wins when enabled.
                if (!string.IsNullOrEmpty(moduleKey) && appearance != null &&
                    appearance.ModuleWallpapers.TryGetValue(
                        moduleKey, out var moduleWallpaper) &&
                    moduleWallpaper != null &&
                    moduleWallpaper.Enabled &&
                    File.Exists(moduleWallpaper.Path))
                {
                    var brush = BuildWallpaperBrush(
                        moduleWallpaper.Path,
                        ClampOpacity(moduleWallpaper.Opacity));

                    if (brush != null)
                    {
                        resources["ModuleWallpaperBrush"] = brush;
                        CurrentModule = moduleKey;

                        // Let the image read through the module surface.
                        SetTranslucent(resources, "ModuleBackgroundBrush",
                            ThemeManager.GetThemeColor(
                                "ModuleBackgroundBrush", Colors.White), 0.78);
                        AdaptTypographyForWallpaper(resources);

                        // Global font colour preference still wins last.
                        ApplyFontPreference(resources, true);

                        return;
                    }
                }

                // 2) Otherwise the system-wide wallpaper shows in
                //    every view.
                bool systemWallpaperLayered = false;
                if (!string.IsNullOrWhiteSpace(_lastWallpaperPath) &&
                    File.Exists(_lastWallpaperPath))
                {
                    LayerWallpaperBehindContent(
                        resources, _lastWallpaperPath,
                        _lastWallpaperOpacity, translucentSurface: 0.82);
                    systemWallpaperLayered = true;
                }

                // Last: the global Font Color preference (or its Auto
                // restore) runs after the wallpaper typography, so a
                // user-chosen text palette always wins.
                ApplyFontPreference(resources, systemWallpaperLayered);

            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Module wallpaper apply failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Themes",
                        Class = nameof(VisualThemeEngine)
                    });
            }
            finally
            {
                // Last line of defense — see GuaranteeCriticalResources.
                GuaranteeCriticalResources(resources);

                // ── Theme-linked / pinned built-in wallpaper scene
                // (2026-09-05 v2 — ROOT-CAUSE FIX). v1 wrote ONLY
                // "ModuleWallpaperBrush" — a key consumed by exactly ONE
                // view (About): every module root paints
                // "ModuleBackgroundBrush" over MainWindow's
                // WindowBackgroundBrush stage, so the scene existed but
                // stayed invisible = the owner's "all fake/broken". The
                // scene now follows the exact contract the FILE-wallpaper
                // path established:
                //   WindowBackgroundBrush ← the scene (app-wide stage)
                //   ModuleBackgroundBrush ← translucent (scene reads
                //       through every module)
                //   wallpaper typography strengthened, font preference
                //       re-asserted, About's direct consumer set.
                // Explicit per-module image wallpapers still WIN
                // (CurrentModule is non-empty only when one painted in
                // this very pass). An active OS backdrop (Mica/Acrylic)
                // keeps the window stage translucent instead.
                if (CurrentModule == ""
                    && WallpaperOrchestrator.TryGetLinkedScene(
                           out var sceneBrush, out double sceneOpacity)
                    && sceneBrush != null)
                {
                    try
                    {
                        sceneBrush.Opacity = sceneOpacity;
                        bool osBackdrop = WallpaperOrchestrator.BackdropActive;
                        if (!osBackdrop)
                            resources["WindowBackgroundBrush"] = sceneBrush;

                        SetTranslucent(resources, "ModuleBackgroundBrush",
                            ThemeManager.GetThemeColor(
                                "ModuleBackgroundBrush", Colors.White),
                            osBackdrop ? 0.55 : 0.80);
                        AdaptTypographyForWallpaper(resources);
                        ApplyFontPreference(resources, true);

                        resources["ModuleWallpaperBrush"] = sceneBrush;

                        // The studio snapshots must describe what is
                        // actually on screen (snapshot may have run
                        // before this overlay).
                        ThemeStudioService.UpdateBase(
                            "WindowBackgroundBrush",
                            resources["WindowBackgroundBrush"]!);
                        ThemeStudioService.UpdateBase(
                            "ModuleBackgroundBrush",
                            resources["ModuleBackgroundBrush"]!);
                        ThemeStudioService.UpdateBase(
                            "ModuleWallpaperBrush", sceneBrush);
                    }
                    catch (Exception sceneEx)
                    {
                        AppLogService.Warning(
                            $"Wallpaper scene overlay skipped: {sceneEx.Message}",
                            new LogContext
                            {
                                Module = "Themes",
                                Class = nameof(VisualThemeEngine)
                            });
                    }
                }

                // ── Theme Studio layer must SURVIVE navigation
                // (2026-09-05 ROOT-CAUSE: clicking Dashboard — or any
                // module — rebuilt the family here WITHOUT re-applying
                // the studio profile, so every customization visibly
                // reset on every navigation). A real rebuild re-snapshots
                // the fresh family/base state and re-applies the active
                // 0–100 profile; the signature-guard's skipped repaints
                // never reach this point, so no-op navigations stay free.
                ThemeStudioService.OnFamilyApplied();

                // Record the rendered scene LAST (in finally, because the
                // module-wallpaper branch returns early). A pass that threw
                // still records: the catch already restored a guaranteed
                // scene, and re-running the same failing input would only
                // flash again.
                _lastAppliedSignature = signature;
                _hasAppliedOnce = true;
            }
        }

        // ── THEME INTEGRITY GUARANTEE (2026-08-29) ──────────────────────
        //
        // Last line of defense after every Apply/ApplyModule: if any
        // resource the UI depends on fails to resolve on a given
        // machine, the screen would silently lose its surfaces, text or
        // window stage. This pass verifies the effective application
        // resource scope and injects a safe, opaque, theme-correct
        // fallback — AND names the missing key in the log, so a
        // machine-specific failure is diagnosable instead of invisible.

        private static void GuaranteeCriticalResources(
            ResourceDictionary resources, bool silent = false)
        {
            try
            {
                var app = Application.Current;
                if (app == null) return;

                bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;

                foreach (var (key, fallback) in CriticalFallbacks(dark))
                {
                    bool present;
                    try
                    {
                        present = app.FindResource(key) != null;
                    }
                    catch (ResourceReferenceKeyNotFoundException)
                    {
                        present = false;
                    }
                    catch (Exception)
                    {
                        present = false;
                    }

                    if (present) continue;

                    // Silent when pre-seeding (the family painter is
                    // about to overwrite the seed anyway); loud when the
                    // FINAL pass finds a hole — that means a family
                    // painter forgot part of the managed contract.
                    if (!silent)
                    {
                        AppLogService.Warning(
                            $"Critical theme resource missing — injected " +
                            $"fallback: {key}",
                            new LogContext
                            {
                                Module = "Themes",
                                Class = nameof(VisualThemeEngine),
                                Operation = "GuaranteeCriticalResources"
                            });
                    }

                    resources[key] = fallback;
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Theme integrity pass failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Themes",
                        Class = nameof(VisualThemeEngine)
                    });
            }
        }

        private static IEnumerable<(string Key, object Value)>
            CriticalFallbacks(bool dark)
        {
            // 2026-09-01: the injected fallbacks must MATCH the current
            // theme palettes — they were still the old icy-blue slate
            // (#D7E5F9 window, #334155 ink, #C7D2E1 outline), so any
            // machine where a key failed to resolve got a BLUE canvas
            // injected into the app scope (and it stayed there, winning
            // over every later family). Cream/warm now.
            Color windowColor = dark
                ? Color.FromRgb(0x0A, 0x12, 0x26)
                : Color.FromRgb(0xF6, 0xF1, 0xE6);
            Color moduleColor = dark
                ? Color.FromRgb(0x10, 0x1A, 0x2E)
                : Color.FromRgb(0xFE, 0xFC, 0xF8);
            Color elevatedColor = dark
                ? Color.FromRgb(0x16, 0x22, 0x38)
                : Color.FromRgb(0xFE, 0xFD, 0xFA);
            Color outlineColor = dark
                ? Color.FromRgb(0x5C, 0x57, 0x4C)
                : Color.FromRgb(0xD8, 0xD0, 0xC2);
            Color primaryColor = dark
                ? Color.FromRgb(0xF5, 0xF5, 0xF7)
                : Color.FromRgb(0x1C, 0x19, 0x17);
            Color secondaryColor = dark
                ? Color.FromRgb(0xA9, 0xB8, 0xCC)
                : Color.FromRgb(0x57, 0x53, 0x4E);
            Color mutedColor = dark
                ? Color.FromRgb(0x7C, 0x8C, 0xA3)
                : Color.FromRgb(0x78, 0x71, 0x6C);

            foreach (var (key, color) in new (string, Color)[]
                     {
                         ("WindowBackgroundBrush", windowColor),
                         ("ModuleBackgroundBrush", moduleColor),
                         ("ElevatedSurfaceBrush", elevatedColor),
                         ("SideMenuBackgroundBrush", elevatedColor),
                         ("MenuHoverBrush", elevatedColor),
                         ("SidebarCardBrush", elevatedColor),
                         ("ShellLiquidBrush", elevatedColor),
                         ("DashLiquidSurfaceBrush", elevatedColor),
                         ("DashAmbientBrush", moduleColor),
                         ("OutlineBrush", outlineColor),
                         ("BorderBrush", outlineColor),
                         ("DividerBrush", outlineColor),
                         ("PrimaryForegroundBrush", primaryColor),
                         ("SecondaryForegroundBrush", secondaryColor),
                         ("MutedForegroundBrush", mutedColor),
                         ("AccentBrush", Color.FromRgb(0x03, 0x69, 0xA1)),
                         ("LiquidGlassBrush", moduleColor),
                         ("LiquidGlassSubtleBrush", elevatedColor),
                         ("LiquidGlassStrongBrush", elevatedColor),
                         ("TintIndigoBrush", dark
                             ? Color.FromRgb(0x27, 0x2F, 0x3A)
                             : Color.FromRgb(0xED, 0xE7, 0xDA)),
                         ("TintBlueBrush", dark
                             ? Color.FromRgb(0x23, 0x2B, 0x34)
                             : Color.FromRgb(0xF2, 0xED, 0xE3)),
                         ("TintGreenBrush", dark
                             ? Color.FromRgb(0x14, 0x30, 0x1F)
                             : Color.FromRgb(0xE8, 0xF6, 0xEC)),
                         ("TintRedBrush", dark
                             ? Color.FromRgb(0x3B, 0x1F, 0x22)
                             : Color.FromRgb(0xFE, 0xF2, 0xF2)),
                         ("TintAmberBrush", dark
                             ? Color.FromRgb(0x3B, 0x2F, 0x1E)
                             : Color.FromRgb(0xFF, 0xFB, 0xEB)),
                         ("TintPurpleBrush", dark
                             ? Color.FromRgb(0x2A, 0x25, 0x40)
                             : Color.FromRgb(0xF5, 0xF3, 0xFF)),
                         ("TintGrayBrush", dark
                             ? Color.FromRgb(0x24, 0x2B, 0x3A)
                             : Color.FromRgb(0xF6, 0xF3, 0xEE))
                     })
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                yield return (key, brush);
            }

            yield return ("AccentColor", Color.FromRgb(0x03, 0x69, 0xA1));
            yield return ("AccentHoverColor", Color.FromRgb(0x11, 0x86, 0xC4));
            yield return ("CardCornerRadius", new CornerRadius(12));
            yield return ("CardCornerRadiusLarge", new CornerRadius(16));
            yield return ("ControlCornerRadius", new CornerRadius(10));
        }

        // ── SYSTEM-WIDE WALLPAPER LAYER ─────────────────────────────────

        /// <summary>
        /// Puts one image behind the content of the framed module area
        /// and lets it read through: the module surface becomes
        /// translucent and typography contrast is strengthened so text
        /// stays professional and readable over the picture.
        /// </summary>
        private static void LayerWallpaperBehindContent(
            ResourceDictionary resources, string path, double opacity,
            double translucentSurface)
        {
            var brush = BuildWallpaperBrush(path, opacity);
            if (brush == null) return;

            resources["ModuleWallpaperBrush"] = brush;

            SetTranslucent(resources, "ModuleBackgroundBrush",
                ThemeManager.GetThemeColor(
                    "ModuleBackgroundBrush", Colors.White),
                translucentSurface);

            AdaptTypographyForWallpaper(resources);
        }

        /// <summary>
        /// Text over a wallpaper loses contrast once surfaces turn
        /// translucent. Strengthen the secondary/muted foregrounds
        /// (theme-aware) so captions and hints stay crisp. Removed
        /// again by ClearOverrides when no wallpaper shows.
        /// </summary>
        private static void AdaptTypographyForWallpaper(
            ResourceDictionary resources)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            var secondary = new SolidColorBrush(dark
                ? Color.FromRgb(0xB8, 0xC4, 0xD6)   // stronger on dark
                : Color.FromRgb(0x4B, 0x55, 0x63)); // stronger on light
            secondary.Freeze();
            resources["SecondaryForegroundBrush"] = secondary;

            var muted = new SolidColorBrush(dark
                ? Color.FromRgb(0x93, 0xA2, 0xB8)
                : Color.FromRgb(0x6B, 0x72, 0x80));
            muted.Freeze();
            resources["MutedForegroundBrush"] = muted;
        }

        private static ImageBrush? BuildWallpaperBrush(
            string path, double opacity)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();

                var brush = new ImageBrush(image)
                {
                    Stretch = Stretch.UniformToFill,
                    Opacity = opacity
                };
                brush.Freeze();
                return brush;
            }
            catch
            {
                return null;
            }
        }

        // ── MONET — colour-adaptive glass (Material-You style) ──────────

        private static void ApplyMonet(
            ResourceDictionary resources, string wallpaperPath,
            double opacity)
        {
            // The whole palette derives from the active accent colour.
            var accent = ThemeManager.AccentColor;
            var (h, s, _) = ColorMath.ToHsl(accent);
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            // Window background = wallpaper, else an accent-hue
            // gradient (no more hard-coded blue).
            if (!string.IsNullOrWhiteSpace(wallpaperPath) &&
                File.Exists(wallpaperPath))
            {
                var wallpaper = BuildWallpaperBrush(wallpaperPath, opacity);
                if (wallpaper != null)
                    resources["WindowBackgroundBrush"] = wallpaper;
            }
            else
            {
                // 2026-08-25: saturation caps eased up a touch so the
                // background visibly belongs to the chosen accent.
                Color from = dark
                    ? ColorMath.FromHsl(h, Math.Min(s, 0.46), 0.14)
                    : ColorMath.FromHsl(h, Math.Min(s, 0.78), 0.94);
                Color to = dark
                    ? ColorMath.FromHsl(h + 26, Math.Min(s, 0.44), 0.10)
                    : ColorMath.FromHsl(h + 26, Math.Min(s, 0.70), 0.88);

                if (ThemeManager.UseGradients)
                {
                    var soft = new LinearGradientBrush(
                        from, to, new Point(0, 0), new Point(1, 1));
                    soft.Freeze();
                    resources["WindowBackgroundBrush"] = soft;
                }
                else
                {
                    var flat = new SolidColorBrush(from);
                    flat.Freeze();
                    resources["WindowBackgroundBrush"] = flat;
                }
            }

            // Frosted surfaces — the active base theme's colours tinted
            // toward the accent hue, then made translucent so the
            // wallpaper reads through. Tint strength is deliberately
            // visible so the colour adaptation is unmistakable.
            resources["ModuleBackgroundBrush"] = FrostSurface(
                resources, "ModuleBackgroundBrush", accent,
                Colors.White, 0.74, dark);
            // Elevated surfaces — popups, dialogs, floating panels
            // Increased opacity to 0.95 for better readability (was 0.82)
            resources["ElevatedSurfaceBrush"] = FrostSurface(
                resources, "ElevatedSurfaceBrush", accent,
                Colors.White, 0.95, dark);
            resources["SideMenuBackgroundBrush"] = FrostSurface(
                resources, "SideMenuBackgroundBrush", accent,
                Colors.White, 0.92, dark);
            resources["MenuHoverBrush"] = FrostSurface(
                resources, "MenuHoverBrush", accent,
                Color.FromRgb(0xF3, 0xF4, 0xF6), 0.62, dark);

            // Shell sidebar — opaque, accent-tinted surface (not transparent).
            // The base theme defines ShellLiquidBrush with ~5% alpha which
            // makes the sidebar see-through. Override with an opaque brush.
            resources["ShellLiquidBrush"] = FrostSurface(
                resources, "ShellLiquidBrush", accent,
                Colors.White, 0.94, dark);

            // Dashboard ambient and liquid surfaces — Monet needs these too.
            resources["SidebarCardBrush"] = FrostSurface(
                resources, "SidebarCardBrush", accent,
                Colors.White, 0.90, dark);
            resources["DashLiquidSurfaceBrush"] = FrostSurface(
                resources, "DashLiquidSurfaceBrush", accent,
                Colors.White, 0.86, dark);
            resources["DashAmbientBrush"] = FrostSurface(
                resources, "DashAmbientBrush", accent,
                Colors.White, 0.16, dark);
            resources["DashAmbientBlob1Brush"] = FrostSurface(
                resources, "DashAmbientBlob1Brush", accent,
                Colors.White, 0.12, dark);
            resources["DashAmbientBlob2Brush"] = FrostSurface(
                resources, "DashAmbientBlob2Brush", accent,
                Colors.White, 0.10, dark);

            // Sections accept Monet too: every shared glass surface /
            // border key mirrors the Monet material set.
            PublishSectionGlass(resources, accent, dark);

            // Softer, rounder cards fit the artistic look.
            resources["CardCornerRadius"] = new CornerRadius(18);
            resources["CardCornerRadiusLarge"] = new CornerRadius(24);
            resources["ControlCornerRadius"] = new CornerRadius(12);

            // Keyline derived from the SAME accent hue as the frosted
            // surfaces, so the outline belongs to the material instead
            // of sitting on it as a foreign grey. Lightness is pinned to
            // a value that keeps roughly 2-3:1 against the card it
            // outlines: visible as a hairline, never a heavy box.
            ApplyKeyline(resources,
                ColorMath.FromHsl(h, Math.Min(s, 0.28),
                    dark ? 0.46 : 0.62));
        }

        /// <summary>
        /// Writes the card keyline for a visual family.
        /// <para>
        /// Sets BOTH OutlineBrush (what every card binds) and
        /// OutlineDefaultBrush (the restore value the Defined Card Edges
        /// toggle reads). Without the second one, turning the toggle off
        /// would snap the keyline back to the base theme's grey and lose
        /// the family's derived colour.
        /// </para>
        /// </summary>
        private static void ApplyKeyline(
            ResourceDictionary resources, Color keyline)
        {
            // Intentionally a no-op for the outline keys.
            //
            // Monet and Professional used to derive their own neutral
            // keyline here. ThemeManager.ApplyAccent now owns all four
            // keyline keys and derives them from the accent hue, which is
            // what makes every card match the Settings "Appearance" cards.
            // Both run on every theme change, and ApplyAccent runs LAST,
            // so anything written here would simply be overwritten - and
            // when it was not (module wallpaper re-layer, which calls the
            // engine on its own) it produced a grey edge on one screen and
            // an accent edge on the next.
            //
            // The parameter is kept so the two call sites still document
            // which family they belong to, and so restoring a
            // family-specific tweak later is a one-line change.
            _ = resources;
            _ = keyline;
        }

        /// <summary>
        /// Builds one Monet glass surface: theme colour → tinted with
        /// the accent hue → translucent.
        /// <para>
        /// 2026-08-25 (richness fix): the tint ratios were raised from
        /// 0.12/0.18 to 0.18/0.26 — at the old values the glass barely
        /// carried the chosen colour and Monet read as "too light".
        /// 2026-09-01: the LIGHT-mode ratio is 0.16 (dark 0.26) so
        /// Monet's colour adaptation stays clearly visible on the pale
        /// light surfaces without flattening them.
        /// When gradient surfaces are enabled (the Appearance toggle)
        /// the surface is a diagonal gradient from the tint to a lifted
        /// tint, which gives Material-You style depth without any
        /// hard-coded colour.
        /// </para>
        /// </summary>
        private static Brush FrostSurface(
            ResourceDictionary resources, string key, Color accent,
            Color fallback, double opacity, bool dark)
        {
            // Use an opaque base color instead of reading from theme
            // (DarkTheme defines ModuleBackgroundBrush as #00000000 which
            // stays transparent even after tinting). For Monet we want
            // solid, opaque surfaces that derive from the accent.
            Color baseColor = dark
                ? Color.FromRgb(0x0F, 0x17, 0x2A)  // Dark slate
                : Color.FromRgb(0xF8, 0xFA, 0xFC); // Light grey
            
            // Tint the opaque base with the accent color
            Color tinted = ColorMath.Tint(accent, baseColor,
                dark ? 0.26 : 0.16);

            if (ThemeManager.UseGradients)
            {
                Color lifted = ColorMath.Lighten(tinted, dark ? 0.04 : 0.05);
                var gradient = new LinearGradientBrush(
                    tinted, lifted, new Point(0, 0), new Point(1, 1))
                {
                    Opacity = opacity,
                };
                gradient.Freeze();
                return gradient;
            }

            var brush = new SolidColorBrush(tinted) { Opacity = opacity };
            brush.Freeze();
            return brush;
        }

        // ── PROFESSIONAL — premium corporate ───────────────────────────

        private static void ApplyProfessional(ResourceDictionary resources)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            // Cool slate window gradient — immediately distinguishes
            // Professional from Normal.
            var window = new LinearGradientBrush(
                dark ? Color.FromRgb(0x0A, 0x13, 0x26)
                     : Color.FromRgb(0xEC, 0xF2, 0xF9),
                dark ? Color.FromRgb(0x16, 0x23, 0x3E)
                     : Color.FromRgb(0xD9, 0xE5, 0xF2),
                new Point(0, 0), new Point(1, 1));
            window.Freeze();
            resources["WindowBackgroundBrush"] = window;

            // Crisp, clearly cooler surfaces (all views consume these
            // via DynamicResource, so the change is global). The mix
            // ratios are strong enough to be seen at a glance — this
            // is what separates Professional from Normal.
            Color cool = dark
                ? Color.FromRgb(0x18, 0x25, 0x3D)
                : Color.FromRgb(0xE2, 0xEA, 0xF4);

            Color moduleBg = ThemeManager.GetThemeColor(
                "ModuleBackgroundBrush", Colors.White);
            var moduleBrush = new SolidColorBrush(
                ColorMath.Mix(moduleBg, cool, 0.62));
            moduleBrush.Freeze();
            resources["ModuleBackgroundBrush"] = moduleBrush;

            Color elevated = ThemeManager.GetThemeColor(
                "ElevatedSurfaceBrush", Colors.White);
            var elevatedBrush = new SolidColorBrush(
                ColorMath.Mix(elevated, cool, 0.52));
            elevatedBrush.Freeze();
            resources["ElevatedSurfaceBrush"] = elevatedBrush;

            Color sideMenu = ThemeManager.GetThemeColor(
                "SideMenuBackgroundBrush", Colors.White);
            var sideMenuBrush = new SolidColorBrush(
                ColorMath.Mix(sideMenu, cool, 0.46));
            sideMenuBrush.Freeze();
            resources["SideMenuBackgroundBrush"] = sideMenuBrush;

            // Tighter radii + restrained hover tint.
            resources["CardCornerRadius"] = new CornerRadius(10);
            resources["CardCornerRadiusLarge"] = new CornerRadius(14);
            resources["ControlCornerRadius"] = new CornerRadius(8);

            var hover = new SolidColorBrush(dark
                ? Color.FromRgb(0x2E, 0x40, 0x5E)
                : Color.FromRgb(0xE4, 0xEC, 0xF6));
            hover.Freeze();
            resources["MenuHoverBrush"] = hover;

            // Sections accept Professional too: the shared glass set
            // mirrors the steel-blue material (Settings, wizards,
            // sidebars — everything follows the industrial look).
            PublishSectionGlass(resources, cool, dark);

            // Cool slate keyline to match the cooled surfaces above.
            // Professional is the "premium corporate" family, so the
            // edge is a touch crisper than Monet's.
            ApplyKeyline(resources, dark
                ? Color.FromRgb(0x5A, 0x6B, 0x82)
                : Color.FromRgb(0x8C, 0x9C, 0xB0));
        }
    }
}
