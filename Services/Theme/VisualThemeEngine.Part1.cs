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

        // ── Wallpaper-layer predicates (2026-09-11) ──────────────────
        // ONE shared decision point per layer instead of scattered family
        // checks at every paint site. iOS 26 stages its own plain-white
        // scene: the user's FILE wallpaper (window layer + module layer)
        // must never paint over it — that was the "wallpaper still copies
        // my Normal theme" bug AND a per-navigation re-layer cost.
        private static bool SystemWallpaperApplies =>
            Current != Monet && Current != Ios26;
        private static bool ModuleWallpaperApplies => Current != Ios26;

        /// <summary>The FILE wallpaper layer paints ONLY when the user's
        /// wallpaper mode actually wants a file: "File" mode, or legacy
        /// "Auto" with an existing saved path (Auto's documented contract).
        /// ThemeLinked / Scene ("force built-in scenes") must NEVER show
        /// the image — it used to paint beneath the scene and stay visible
        /// through translucent module surfaces (2026-09-11 bugfix).</summary>
        private static bool FileWallpaperApplies
        {
            get
            {
                if (!SystemWallpaperApplies) return false;
                string mode = WallpaperOrchestrator.EffectiveMode(
                    App.Config?.Appearance);
                if (mode == WallpaperOrchestrator.ModeFile) return true;
                if (mode == WallpaperOrchestrator.ModeAuto)
                    return !string.IsNullOrWhiteSpace(_lastWallpaperPath)
                        && File.Exists(_lastWallpaperPath);
                return false;
            }
        }
        /// <summary>NORMAL (INDUSTRIAL) (2026-09-07) — the pre-cream
        /// "Normal" restored as its own family beside the cream SaaS
        /// Normal. Full white + blue tone, professional industrial
        /// layout, and a defined slate card edge/corner border line.
        /// Spec: design-system/proglass-industrial/MASTER.md (background
        /// #F8FAFC, white #FFFFFF cards, border #E2E8F0, navy #0F172A
        /// ink, blue #0369A1 CTA). Solid surfaces — never frosted glass.</summary>
        public const string NormalIndustrial = "Normal (Industrial)";
        public const string Monet = "Monet";
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
        /// The strict industrial family is surfaced as "Industrial"; the
        /// Crystal Fluid family as "iPhone 17 Pro Max".</summary>
        public static string FamilyDisplayName(string family) =>
            family == NormalIndustrial ? "Industrial"
            : family == CrystalFluid ? "iPhone 17 Pro Max"
            : family;

        /// <summary>The Light/Dark mode a family PROMISES by design —
        /// true = Dark, false = Light, null = respects the user's own
        /// mode choice. Industrial (Normal Industrial), Monet and iOS 26
        /// follow the user's Light/Dark toggle; Crystal Fluid
        /// (iPhone 17 Pro Max) is LIGHT-ONLY and snaps to Light.</summary>
        public static bool? ModePairFor(string? family) =>
            family == CrystalFluid ? (bool?)false : null;

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
            "DialogSurfaceBrush", "DialogInsetBrush",
            "SecondaryForegroundBrush", "MutedForegroundBrush",
            "CardCornerRadius", "CardCornerRadiusLarge",
            "ControlCornerRadius",
            "WallpaperOpacityValue",
            "ModuleWallpaperBrush",

            // Status-tint cards/chips (InfoCard + the BrushKeyConverter
            // chips in dashboards/lists). The Crystal Fluid (Ios-17pm)
            // family renders these as see-through hue frost (hue kept,
            // alpha dropped), so the overrides must be cleaned up on
            // family switch exactly like the surfaces above.
            "TintIndigoBrush", "TintBlueBrush", "TintGreenBrush",
            "TintRedBrush", "TintAmberBrush", "TintPurpleBrush",
            "TintGrayBrush",

            // Dashboard surface + ambient layer, and the shell (top bar)
            // — Crystal Fluid re-skins these to its pale stage.
            "DashLiquidSurfaceBrush", "DashAmbientBrush",
            "DashAmbientBlob1Brush", "DashAmbientBlob2Brush",
            "ShellLiquidBrush", "SidebarCardBrush",

            // Liquid Glass design system tokens (2026-08-29): Crystal
            // Fluid (Ios-17pm) writes accent-tinted crystal values; every
            // other family restores the theme-file defaults.
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
            // Button/input focus edges. PublishSectionGlass writes these
            // for the glass families, so they must be cleaned up on
            // switch exactly like GlassBorderSoft — otherwise a slate
            // (industrial) or crystal edge leaks onto the cream Normal.
            "GlassBorderPrimary", "GlassBorderBright",
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
        public static string Current { get; private set; } = NormalIndustrial;

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

        // ── Wallpaper-only fast path (2026-09-07, per-module toggle lag) ──
        // A per-module wallpaper toggle (or its opacity slider) used to
        // rebuild the ENTIRE family — ~60 keys torn down and republished —
        // on every tick, because ApplyModule could not tell a wallpaper
        // change from a family change. These three fields cache the
        // family's post-paint, pre-wallpaper surfaces so a wallpaper-only
        // change can restore them and re-layer the wallpaper WITHOUT the
        // full teardown. _familyBaselineSignature is the family/mode/accent/
        // font fingerprint (wallpaper inputs excluded).
        private static string _familyBaselineSignature = "";
        private static object? _familyWindowBrush;
        private static object? _familyModuleBrush;

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

            // Wallpaper MODE + pinned scene are render inputs too (2026-09-11
            // bugfix): flipping "force built-in scenes" ↔ file (or pinning a
            // different scene) without changing the path left the signature
            // UNCHANGED and the idempotence guard skipped the render — the
            // old wallpaper stayed on screen ("mode not accepted" report).
            string wallMode = appearance?.WallpaperMode ?? "";
            string pinnedScene = appearance?.BuiltinScene ?? "";

            // ThemeManager's Light/Dark mode and the accent are inputs too:
            // the family derives its surfaces from them.
            return string.Join("¦",
                ThemeManager.CurrentTheme,
                ThemeManager.AccentColor.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                family, mod, wall, op.ToString("F3",
                    System.Globalization.CultureInfo.InvariantCulture),
                moduleWall, wallMode, pinnedScene,
                FontPreferenceSignature());
        }

        /// <summary>
        /// Fingerprints ONLY the family baseline (mode + family + accent +
        /// font colour) — everything the family painters derive surfaces
        /// from. Wallpaper inputs are deliberately EXCLUDED: when this
        /// signature is unchanged, the family surfaces on screen are still
        /// correct and only the wallpaper layer needs re-layering (the
        /// 2026-09-07 fast path behind the per-module wallpaper toggle).
        /// </summary>
        private static string BuildFamilySignature(
            AI.AIConfig.AppearanceSettings? appearance)
        {
            string family = Normalize(appearance?.VisualTheme ?? Normal);
            // The SYSTEM wallpaper is a family input: Monet derives its
            // window stage directly from it (ApplyMonet), so a changed
            // system wallpaper must force a real family rebuild. The
            // per-module wallpaper is NOT included — it only re-layers the
            // module area, which the wallpaper fast path handles alone.
            return string.Join("¦",
                ThemeManager.CurrentTheme,
                ThemeManager.AccentColor.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                family,
                appearance?.WallpaperPath ?? "",
                ClampOpacity(appearance?.WallpaperOpacity ?? 0.85)
                    .ToString("F3",
                        System.Globalization.CultureInfo.InvariantCulture),
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
            // Monet and Crystal Fluid derive their surfaces and
            // window stage from the active accent colour, so both must
            // re-derive when the user changes the accent.
            if (Current != Monet && Current != CrystalFluid)
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
                // NOT in ManagedKeys; see tokens file). Published as an
                // explicit FALSE at every non-crystal family switch: the
                // indexer raises ResourcesChanged, so every
                // CrystalWaterControl's DynamicResource reference
                // re-evaluates immediately. Remove() did not reliably
                // notify, which left water stuck at its TRUE DP default
                // under Normal / Monet / Industrial (water rendering
                // outside Ios-17pm).
                resources["CrystalWaterDefaultEnabled"] = false;
                // Ios-17pm P4a: family-gated shell ambient host
                // (MainWindow wallpaper layer) — removed with the
                // same contract as the water gate.
                resources["CrystalAmbientHostVisibility"] = Visibility.Collapsed;
                resources["CrystalPreviewHostVisibility"] = Visibility.Collapsed;
                SeedCrystalPipelineTokens(resources);
                // iOS 26 removal contract (2026-09-11): notify-critical gates
                // get explicit fallback SETS (the indexer raises
                // ResourcesChanged; Remove() did not reliably notify — the
                // CrystalWaterDefaultEnabled lesson); every other key is
                // removed so the theme-dictionary defaults shine through.
                foreach (var ck in Ios26TokenKeys)
                {
                    if (ck == "AboutGlassChromeVisibility")
                        resources["AboutGlassChromeVisibility"] =
                            System.Windows.Visibility.Collapsed;
                    else if (ck == "AboutWallpaperBrush")
                        resources["AboutWallpaperBrush"] = Ios26NeutralWallpaper;
                    else if (!resources.Contains(ck))
                    {
                        var fill = new SolidColorBrush(Colors.Transparent);
                        fill.Freeze();
                        resources[ck] = fill;
                    }
                }
                // Re-seed the About glass keys from the CURRENT Surface.*
                // values BEFORE the family switch (ERR-50472F lesson: no
                // absence window — iOS 26 overrides them right after in the
                // switch; every other family keeps the live aliases).
                SeedNeutralAboutTokens(resources);
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

                    case CrystalFluid:
                        ApplyCrystalFluid(resources);
                        break;

                    case NormalIndustrial:
                        ApplyIndustrial(resources);
                        break;
                }

                // Cache the family's pre-wallpaper surfaces so the next
                // ApplyModule can fast-path a wallpaper-only change (see
                // ApplyModule's familyChanged branch for the full story).
                // The signature is built from Current (the family ACTUALLY
                // painted) so it can never drift from what is on screen,
                // and uses the SAME field layout as BuildFamilySignature.
                _familyWindowBrush = resources["WindowBackgroundBrush"];
                _familyModuleBrush = resources["ModuleBackgroundBrush"];
                _familyBaselineSignature = string.Join("¦",
                    ThemeManager.CurrentTheme,
                    ThemeManager.AccentColor.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    Current,
                    _lastWallpaperPath ?? "",
                    ClampOpacity(_lastWallpaperOpacity).ToString("F3",
                        System.Globalization.CultureInfo.InvariantCulture),
                    FontPreferenceSignature());

                // System-wide wallpaper chrome: paint the window
                // background for non-Monet families (Monet handles it
                // above). The module-area layer is owned by
                // ApplyModule, which callers invoke right after this —
                // that keeps module-specific wallpapers authoritative.
                if (FileWallpaperApplies &&
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

            // 2026-09-07 fast path: a wallpaper-only change (per-module
            // toggle / opacity tick) must NOT tear the family down. If the
            // family baseline is unchanged, only the wallpaper layer is
            // re-layered below — the family surfaces stay untouched, so
            // every DynamicResource-bound card repaints ZERO times instead
            // of twice. A real family/mode/accent/font change still runs
            // the full rebuild.
            string familySignature = BuildFamilySignature(appearance);
            bool familyChanged = familySignature != _familyBaselineSignature
                                 || !_hasAppliedOnce;

            try
            {
                LastRequestedModule = moduleKey ?? "";

                _lastWallpaperPath = appearance?.WallpaperPath ?? "";
                _lastWallpaperOpacity =
                    ClampOpacity(appearance?.WallpaperOpacity ?? 0.85);

                Current = Normalize(appearance?.VisualTheme ?? Normal);

                if (familyChanged)
                {
                    // Rebuild the family baseline (removes the previous
                    // module's wallpaper layer).
                    ClearOverrides(resources);
                    // Crystal Fluid gate (non-managed key by design —
                    // NOT in ManagedKeys; see tokens file). Published as an
                    // explicit FALSE at every non-crystal family switch: the
                    // indexer raises ResourcesChanged, so every
                    // CrystalWaterControl's DynamicResource reference
                    // re-evaluates immediately. Remove() did not reliably
                    // notify, which left water stuck at its TRUE DP default
                    // under Normal / Monet / Industrial (water rendering
                    // outside Ios-17pm).
                    resources["CrystalWaterDefaultEnabled"] = false;
                    // Ios-17pm P4a: family-gated shell ambient host
                    // (MainWindow wallpaper layer) — removed with the
                    // same contract as the water gate.
                    resources["CrystalAmbientHostVisibility"] = Visibility.Collapsed;
                    resources["CrystalPreviewHostVisibility"] = Visibility.Collapsed;
                    SeedCrystalPipelineTokens(resources);
                    // PRE-SEED (2026-09-05 — ERR-50472F): never leave the
                    // dictionary with absent keys between teardown and the
                    // family republish — see Apply() for the full rationale.
                    GuaranteeCriticalResources(resources, silent: true);

                    switch (Current)
                    {
                        case Monet:
                            ApplyMonet(resources, _lastWallpaperPath,
                                _lastWallpaperOpacity);
                            break;
                        case CrystalFluid:
                            ApplyCrystalFluid(resources);
                            break;
                        case NormalIndustrial:
                            ApplyIndustrial(resources);
                            break;
                    }

                    // Cache the family's pre-wallpaper surfaces so a
                    // wallpaper-only change can restore them and skip the
                    // teardown above.
                    _familyWindowBrush = resources["WindowBackgroundBrush"];
                    _familyModuleBrush = resources["ModuleBackgroundBrush"];
                    _familyBaselineSignature = familySignature;
                }
                else
                {
                    // Wallpaper-only change: the family surfaces on screen
                    // are still correct. Restore the cached pre-wallpaper
                    // surfaces and clear the previous wallpaper layer, then
                    // fall through to the SAME wallpaper layering below.
                    if (_familyWindowBrush != null)
                        resources["WindowBackgroundBrush"] = _familyWindowBrush;
                    if (_familyModuleBrush != null)
                        resources["ModuleBackgroundBrush"] = _familyModuleBrush;
                    {
                        var none = new SolidColorBrush(Colors.Transparent);
                        none.Freeze();
                        resources["ModuleWallpaperBrush"] = none;
                    }
                }

                if (FileWallpaperApplies &&
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
                //    (iOS 26: never — the family stages plain white.)
                if (ModuleWallpaperApplies &&
                    !string.IsNullOrEmpty(moduleKey) && appearance != null &&
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
                if (FileWallpaperApplies &&
                    !string.IsNullOrWhiteSpace(_lastWallpaperPath) &&
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

        /// <summary>
        /// Never Remove Crystal pipeline keys — DynamicResource on leftover
        /// CrystalWaterControl / Settings sliders would resolve empty.
        /// Seed authored baselines instead.
        /// </summary>
        private static void SeedCrystalPipelineTokens(ResourceDictionary resources)
        {
            resources["CrystalWaterOpacity"] = 0.40;
            resources["CrystalWaterSpeed"] = 1.0;
            resources["CrystalCausticOpacity"] = 0.18;
            resources["CrystalBubbleOpacity"] = 0.70;
            resources["CrystalReflectionOpacity"] = 0.22;
            resources["CrystalReducedMotion"] = true;
            resources["CrystalWaveAmplitude"] = 3.0;
            resources["CrystalWaterDefaultEnabled"] = false;
            resources["CrystalAmbientHostVisibility"] = Visibility.Collapsed;
            resources["CrystalPreviewHostVisibility"] = Visibility.Collapsed;
        }

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
                         ("DialogSurfaceBrush", elevatedColor),
                         ("DialogInsetBrush", moduleColor),
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
                         ("TextPrimaryBrush", primaryColor),
                         ("TextSecondaryBrush", secondaryColor),
                         ("TextTertiaryBrush", mutedColor),
                         ("SurfacePrimaryBrush", moduleColor),
                         ("SurfaceSecondaryBrush", elevatedColor),
                         ("SurfaceTertiaryBrush", elevatedColor),
                         ("SurfaceInputBrush", moduleColor),
                         ("GlassUltraClearBrush", moduleColor),
                         ("GlassSurfaceBrush", moduleColor),
                         ("GlassCrystalBrush", elevatedColor),
                         ("GlassFrostedBrush", elevatedColor),
                         ("GlassSurfaceUltra", elevatedColor),
                         ("GlassSurfaceStandard", moduleColor),
                         ("GlassSurfaceElevated", elevatedColor),
                         ("GlassBorderSoft", outlineColor),
                         ("GlassBorderPrimary", outlineColor),
                         ("GlassBorderBright", outlineColor),
                         ("GlassBorderGradientCrystal", outlineColor),
                         ("InputBorderBrush", outlineColor),
                         ("BorderDefaultBrush", outlineColor),
                         ("BorderSubtleBrush", outlineColor),
                         ("PopupSurfaceBrush", elevatedColor),
                         ("LiquidOverlayBrush", elevatedColor),
                         ("LiquidFocusBrush", Color.FromRgb(0x03, 0x69, 0xA1)),
                         ("LiquidCard", moduleColor),
                         ("AccentHoverBrush", Color.FromRgb(0x11, 0x86, 0xC4)),
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

        private sealed record WallpaperCacheEntry(DateTime LastWriteUtc, BitmapImage Image);
        private static readonly Dictionary<string, WallpaperCacheEntry> _wallpaperImageCache =
            new(StringComparer.OrdinalIgnoreCase);

        private static ImageBrush? BuildWallpaperBrush(
            string path, double opacity)
        {
            try
            {
                var image = GetCachedWallpaperImage(path);
                if (image == null) return null;

                // Opacity is the only slider-tick variable, so only rebuild the
                // lightweight ImageBrush wrapper; the decoded frozen bitmap is
                // cached by path + last-write time.
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

        private static BitmapImage? GetCachedWallpaperImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            var fullPath = Path.GetFullPath(path);
            var lastWrite = File.GetLastWriteTimeUtc(fullPath);
            if (_wallpaperImageCache.TryGetValue(fullPath, out var cached)
                && cached.LastWriteUtc == lastWrite)
            {
                return cached.Image;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(fullPath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            _wallpaperImageCache[fullPath] = new WallpaperCacheEntry(lastWrite, image);
            return image;
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
    }
}
