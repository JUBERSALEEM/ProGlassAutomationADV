// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/LiquidGlassThemeSourceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Source-level invariants for the shared glass design system
//         (2026-08-29 — system-wide rewrite + completion sweep;
//          2026-09-06 — theme reduction: the iOS Liquid Glass, Ocean,
//          Professional and Industrial FAMILIES are removed, but the
//          shared Liquid* design-system tokens, the cream Normal palette
//          and the Crystal Fluid (Ios-17pm) family all remain and stay
//          pinned here.)
//
// Pins the DESIGN of the surviving surface system: the legacy
// "Glass"/"Glassy" families, their resource keys, identifiers, file
// names and UI wording must stay deleted; the opaque in-window stage
// must stay self-contained (no DWM veil); the Liquid* token surface
// must stay identical across BOTH theme dictionaries; Normal must stay
// cream (no blue cast); and Crystal Fluid must stay readable.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class LiquidGlassThemeSourceTests
    {
        private static string Read(string relative)
        {
            // Phase 3.8: god files are partial families (X.PartN.cs) —
            // source-level assertions must read the whole family.
            string root = Root();
            var sb = new System.Text.StringBuilder(
                File.ReadAllText(Path.Combine(root, relative)));
            string dir = Path.GetDirectoryName(Path.Combine(root, relative))!;
            string name = Path.GetFileNameWithoutExtension(relative);
            foreach (var part in Directory.EnumerateFiles(dir, name + ".Part*.cs")
                         .OrderBy(f => f, StringComparer.Ordinal))
                sb.Append(File.ReadAllText(part));

            // App family convention: App.xaml.cs + App.StartupCore.cs /
            // App.AuthHandlers.cs / ... — the App.*.cs parts carry the
            // startup theme chain, so assertions about App startup must
            // read them too.
            string stem = name.Contains('.')
                ? name.Substring(0, name.IndexOf('.'))   // "App" from "App.xaml"
                : name;
            if (stem == "App")
            {
                foreach (var part in Directory.EnumerateFiles(dir, "App.*.cs")
                             .Where(p => !p.EndsWith("App.xaml.cs", StringComparison.Ordinal))
                             .OrderBy(f => f, StringComparer.Ordinal))
                    sb.Append(File.ReadAllText(part));
            }
            return sb.ToString();
        }

        private static string Engine => Read(
            "Services/Theme/VisualThemeEngine.cs");

        private static string LightTheme => Read("Themes/LightTheme.xaml");

        // ══════════════════════════════════════════════════════════════
        //  Family inventory — 2026-09-06 theme reduction (owner decision)
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Eight_families_are_selectable_and_LiquidGlass_is_the_default_family()
        {
            // The selectable contract is now the eight approved material
            // families. Old industrial, Monet, Crystal Fluid, and iOS 26
            // values are migrated once by ConfigService.
            string engine = Engine;
            Assert.DoesNotContain("public const string Glass =", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("public const string Glassy =", engine, StringComparison.Ordinal);
            Assert.Contains("public const string LiquidGlass = \"Liquid Glass\";", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("public const string Ocean =", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("public const string Professional =", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("public const string Industrial", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Glass\" => LiquidGlass", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Glassy\" => LiquidGlass", engine, StringComparison.Ordinal);

            string config = Read("Services/AI/AIConfig/AppConfig.cs");
            Assert.Contains("= \"Liquid Glass\";", config, StringComparison.Ordinal);
            Assert.DoesNotContain("= \"iOS Liquid Glass\";", config, StringComparison.Ordinal);

            // App.StartupCore.cs is pulled in by the App.*.cs convention.
            string appCs = Read("App.xaml.cs");
            Assert.Contains("?? VisualThemeEngine.LiquidGlass", appCs, StringComparison.Ordinal);
            Assert.DoesNotContain("?? \"iOS Liquid Glass\"", appCs, StringComparison.Ordinal);

            // 2026-09-17 CORRECTION (owner: "I could see only two" → then
            // "Ios and iphone 17 pro max theme is broken"): this block used to
            // pin Phase 8's THREE families and assert Crystal Fluid / iOS 26
            // were absent from the picker. The picker now offers FOUR — the
            // language files promise "Industrial, Monet, iOS 26 or iPhone 17
            // Pro Max — restyles the whole app instantly" — so the two
            // DoesNotContain asserts below were contradicting the shipped UI
            // (and the theme-picker fix). They are inverted into Contains on
            // purpose. The removed-family asserts above stay: those families
            // are still gone.
            string vm = Read(
                "ViewModels/Settings/SettingsViewModel.ThemeWallpaper.cs");
            Assert.Contains("VisualThemeEngine.BentoGrid)", vm, StringComparison.Ordinal);
            Assert.Contains("VisualThemeEngine.SpatialImmersive)", vm, StringComparison.Ordinal);
            Assert.Contains("VisualThemeEngine.SwissInternational)", vm, StringComparison.Ordinal);
            Assert.Contains("VisualThemeEngine.HolographicIridescent)", vm, StringComparison.Ordinal);
            Assert.Contains("VisualThemeEngine.Neumorphism)", vm, StringComparison.Ordinal);
            Assert.Contains("VisualThemeEngine.Claymorphism)", vm, StringComparison.Ordinal);
            Assert.Contains("VisualThemeEngine.LiquidGlass)", vm, StringComparison.Ordinal);
            Assert.Contains("VisualThemeEngine.Glassmorphism)", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("VisualThemeEngine.Normal)", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("VisualThemeEngine.Ocean", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("VisualThemeEngine.Professional", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("VisualThemeEngine.Industrial", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("VisualThemeEngine.Glass,", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("VisualThemeEngine.Glassy", vm, StringComparison.Ordinal);
        }

        [Fact]
        public void Industrial_round_fixes_are_pinned()
        {
            // 2026-09-13 round: (1) saved "Normal" configs migrate once to
            // the strict industrial default "Normal (Industrial)"; (2) the
            // crystal water gate publishes an explicit FALSE outside iPhone
            // 17 Pro Max instead of removing the key; (3) the read-only
            // EditorTitle bindings are pinned OneWay so the water wrapper's
            // re-parenting can never re-activate them as TwoWay (ERR-21172E).
            string configService = Read("Services/AI/AIConfig/ConfigService.cs");
            Assert.Contains("VisualThemeMigrated", configService, StringComparison.Ordinal);
            Assert.Contains("EightThemeMigrationApplied", configService, StringComparison.Ordinal);
            Assert.Contains("VisualThemeEngine.BentoGrid", configService, StringComparison.Ordinal);
            Assert.Contains("VisualThemeEngine.LiquidGlass", configService, StringComparison.Ordinal);

            string engine = Engine;
            Assert.Contains(
                "resources[\"CrystalWaterDefaultEnabled\"] = false;",
                engine, StringComparison.Ordinal);

            string company = Read("Views/CompanyCenter/CompanyCenterView.xaml");
            Assert.Contains("EditorTitle, Mode=OneWay", company, StringComparison.Ordinal);
        }

        [Fact]
        public void Every_selectable_family_survives_normalization_and_is_dispatched()
        {
            // ROOT-CAUSE LOCK (2026-09-17). Two independent cuts made the
            // iPhone 17 Pro Max and iOS 26 families dead options:
            //   1. VisualThemeEngine.Normalize folded BOTH identity strings to
            //      NormalIndustrial, so every Apply()/ApplyModule() painted
            //      Industrial no matter what the picker saved.
            //   2. ApplyIos26 had no `case` in either dispatch switch, so even
            //      an unfaded Normalize could not have painted it.
            // A fold like that is invisible to every other test: the family
            // resolves, paints and saves — it just paints the wrong thing.
            string part3 = Read("Services/Theme/VisualThemeEngine.Part3.cs");
            Assert.Contains("CrystalFluid => CrystalFluid,", part3, StringComparison.Ordinal);
            Assert.Contains("Ios26 => Ios26,", part3, StringComparison.Ordinal);
            Assert.DoesNotContain("CrystalFluid => NormalIndustrial", part3, StringComparison.Ordinal);
            Assert.DoesNotContain("Ios26 => NormalIndustrial", part3, StringComparison.Ordinal);

            string part1 = Read("Services/Theme/VisualThemeEngine.Part1.cs");
            Assert.Equal(2, CountOccurrences(part1, "case Ios26:"));
            Assert.Equal(2, CountOccurrences(part1, "ApplyIos26(resources);"));
            Assert.Equal(2, CountOccurrences(part1, "case CrystalFluid:"));
            Assert.Equal(2, CountOccurrences(part1, "ApplyCrystalFluid(resources);"));

            // The Themes-section gate must read the saved family, never a
            // hardcoded false (that hid the crystal axes permanently).
            string customization = Read(
                "ViewModels/Settings/SettingsViewModel.ThemeCustomization.cs");
            Assert.Contains("SelectedVisualTheme == VisualThemeEngine.CrystalFluid",
                customization, StringComparison.Ordinal);
            Assert.DoesNotContain("IsCrystalThemeActive => false",
                customization, StringComparison.Ordinal);
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
            {
                count++;
                i += needle.Length;
            }
            return count;
        }

        [Fact]
        public void Engine_publishes_the_crystal_fluid_tokens()
        {
            // The Liquid* token surface is now authored by the Crystal
            // Fluid (Ios-17pm) family; the engine must keep publishing the
            // edge/selection/primary/overlay tokens the shared styles
            // consume.
            string engine = Engine;
            Assert.Contains("ApplyCrystalFluid", engine, StringComparison.Ordinal);
            Assert.Contains("\"LiquidEdgeBrush\"", engine, StringComparison.Ordinal);
            Assert.Contains("\"LiquidSelectionBrush\"", engine, StringComparison.Ordinal);
            Assert.Contains("\"LiquidPrimaryButtonBrush\"", engine, StringComparison.Ordinal);
            Assert.Contains("CrystalEdge", engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Accent_application_does_not_clobber_the_crystal_family_tints()
        {
            // ApplyAccent runs after the engine on every family switch;
            // it must not repaint the Blue/Indigo tint chips opaque over
            // the Crystal Fluid family's see-through hue frost.
            string tm = Read("Services/ThemeManager.cs");
            int guard = tm.IndexOf(
                "VisualThemeEngine.Current != VisualThemeEngine.CrystalFluid",
                StringComparison.Ordinal);
            int tintBlue = tm.IndexOf(
                "FreezeAndSet(resources, \"TintBlueBrush\"",
                StringComparison.Ordinal);
            Assert.True(guard >= 0, "tint guard missing");
            Assert.True(tintBlue >= 0, "TintBlueBrush write missing");
            Assert.True(guard < tintBlue,
                "the Crystal Fluid tint guard must come before the TintBlueBrush write");
        }

        // ══════════════════════════════════════════════════════════════
        //  Shared glass design-system invariants (kept after reduction)
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Light_glass_card_is_translucent_white_not_opaque()
        {
            // The light glass material is a translucent white ramp, never
            // an opaque white slab. The tokens live in the theme files and
            // are consumed by every surviving family's cards.
            Assert.Contains("#E4FFFFFF", LightTheme, StringComparison.Ordinal);
            Assert.Contains("#CEFFFFFF", LightTheme, StringComparison.Ordinal);
            Assert.Contains("#F6FFFFFF", LightTheme, StringComparison.Ordinal);
            Assert.Contains("#96FFFFFF", LightTheme, StringComparison.Ordinal); // specular sheen
        }

        [Fact]
        public void Light_glass_text_is_decoupled_from_surface_keys()
        {
            // Light glass text uses fixed WARM ink/stone constants — and
            // must never read the surface-named Liquid* keys again (that
            // coupling would white-out text when surfaces are
            // recoloured).
            Assert.Contains("FrozenSolid(Color.FromRgb(0x1C, 0x19, 0x17))", Engine, StringComparison.Ordinal);
            Assert.Contains("FrozenSolid(Color.FromRgb(0x57, 0x53, 0x4E))", Engine, StringComparison.Ordinal);
            Assert.Contains("FrozenSolid(Color.FromRgb(0x78, 0x71, 0x6C))", Engine, StringComparison.Ordinal);
            Assert.DoesNotContain("FrozenSolid(Color.FromRgb(0x1D, 0x4E, 0xD8))", Engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Surface_keys_are_consumed_via_dynamic_resource_so_family_overrides_reach_cards()
        {
            // Runtime overrides (Application.Resources) only reach
            // consumers bound with DynamicResource. The card styles
            // must never switch back to StaticResource, or the glass
            // surfaces would silently stay opaque.
            string cardStyles = Read("Themes/Styles/CardStyles.xaml");
            string settings = Read("Views/Settings/SettingsSectionStyles.xaml");

            Assert.Contains("{DynamicResource GlassUltraClearBrush}", cardStyles, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource GlassSurfaceElevated}", cardStyles, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource GlassSurfaceBrush}", settings, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource GlassSurfaceUltra}", settings, StringComparison.Ordinal);
            foreach (string key in new[] { "GlassSurfaceBrush", "GlassSurfaceUltra",
                     "GlassSurfaceStandard", "GlassSurfaceElevated", "GlassUltraClearBrush" })
            {
                Assert.DoesNotContain("{StaticResource " + key + "}", cardStyles, StringComparison.Ordinal);
                Assert.DoesNotContain("{StaticResource " + key + "}", settings, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Glass_design_system_is_merged_and_tokens_are_defined_in_both_themes()
        {
            // The shared glass design system is merged in App.xaml; every
            // token it consumes is defined in BOTH theme files so
            // Light/Dark switching always resolves.
            string app = Read("App.xaml");
            string light = LightTheme;
            string dark = Read("Themes/DarkTheme.xaml");
            string styles = Read("Themes/Styles/LiquidGlassStyles.xaml");

            Assert.Contains("LiquidGlassStyles.xaml", app, StringComparison.Ordinal);
            Assert.Contains("{StaticResource LiquidGlassCard}", styles, StringComparison.Ordinal);

            string[] tokens =
            {
                "LiquidGlassBrush", "LiquidGlassSubtleBrush", "LiquidGlassStrongBrush",
                "LiquidEdgeBrush", "LiquidInnerHighlightBrush", "LiquidReflectionBrush",
                "LiquidControlHoverBrush", "LiquidControlPressedBrush", "LiquidControlDisabledBrush",
                "LiquidFocusBrush", "LiquidSelectionBrush", "LiquidPrimaryButtonBrush",
                "LiquidOverlayBrush", "LiquidShadowColor"
            };
            foreach (string token in tokens)
            {
                Assert.Contains("x:Key=\"" + token + "\"", light, StringComparison.Ordinal);
                Assert.Contains("x:Key=\"" + token + "\"", dark, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Glass_styles_never_pin_theme_colours_with_static_resource()
        {
            // Dynamic switching depends on DynamicResource — the design
            // system must never StaticResource the glass MATERIALS.
            string styles = Read("Themes/Styles/LiquidGlassStyles.xaml");
            foreach (string token in new[]
                     {
                         "GlassSurfaceUltra", "GlassSurfaceStandard",
                         "GlassSurfaceElevated", "GlassBorderPrimary",
                         "GlassBorderBright", "GlassBorderSoft",
                         "GlassGradientIdle", "GlassGradientActive",
                         "GlassTopReflection", "TextPrimaryBrush",
                         "TextSecondaryBrush", "GlassUltraClearBrush"
                     })
            {
                Assert.DoesNotContain("{StaticResource " + token + "}", styles, StringComparison.Ordinal);
            }
            Assert.Contains("{DynamicResource GlassSurfaceElevated}", styles, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource GlassUltraClearBrush}", styles, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource GlassTopReflection}", styles, StringComparison.Ordinal);
        }

        [Fact]
        public void Glass_card_control_exposes_intensity_and_radius()
        {
            string control = Read("Controls/LiquidGlassCard.cs");
            Assert.Contains("LiquidIntensityProperty", control, StringComparison.Ordinal);
            Assert.Contains("enum LiquidSurface", control, StringComparison.Ordinal);
            Assert.Contains("CornerRadiusProperty", control, StringComparison.Ordinal);
        }

        [Fact]
        public void Dark_frost_ramp_untouched_by_the_light_pass()
        {
            // Dark deep-frost materials are the original About look;
            // the light-transparency pass must never change them.
            string dark = Read("Themes/DarkTheme.xaml");
            Assert.Contains("#2EFFFFFF", dark, StringComparison.Ordinal);
            Assert.Contains("#0DFFFFFF", dark, StringComparison.Ordinal);
        }

        [Fact]
        public void Theme_dictionaries_define_no_legacy_glass_family_resource_keys()
        {
            // The old Glass* FAMILY identifiers are gone. The ADOPTED
            // design surface legitimately keeps keys named GlassSurface* /
            // GlassBorder* / GlassShadow* / GlassTop* / GlassWater* —
            // these are the canonical shared materials every section
            // consumes. The check targets the LEGACY identifiers only,
            // exactly mirroring the banned-identifier list below.
            var legacy = new[]
            {
                "GlassCard", "GlassSheen", "GlassIce", "GlassFrost",
                "GlassCerulean", "GlassSnow", "GlassAccentDeep",
                "GlassAccent", "GlassMuted", "GlassOrbA", "GlassOrbB",
                "GlassPlate", "GlassGlow", "GlassEdgeBrush",
                "GlassInnerHighlightBrush", "GlassReflectionBrush",
                "GlassControlHoverBrush", "GlassControlPressedBrush",
                "GlassControlDisabledBrush", "GlassFocusBrush",
                "GlassSelectionBrush", "GlassPrimaryButtonBrush",
                "GlassOverlayBrush", "GlassShadowColor"
            };
            foreach (var file in new[]
                     {
                         LightTheme,
                         Read("Themes/DarkTheme.xaml"),
                         Read("Themes/Styles/LiquidGlassStyles.xaml"),
                         Read("Views/Login/AuthSurfaceStyles.xaml"),
                         Read("Views/Splash/SplashView.xaml"),
                         Read("Styles/DashboardStyles.xaml"),
                         Read("Views/Reports/StatementGenerator/Filter/FilterStep.xaml")
                     })
            {
                foreach (Match m in Regex.Matches(file, "x:Key=\"([^\"]+)\""))
                {
                    string key = m.Groups[1].Value;
                    foreach (string name in legacy)
                    {
                        // EXACT match only: the adopted surface family
                        // legitimately uses GlassSurface* / GlassBorder* /
                        // GlassFrostedBrush etc., which share prefixes
                        // with the old identifiers.
                        Assert.False(
                            key == name,
                            "Resource key still carries the legacy Glass family name: " + key);
                    }
                }
            }
        }

        [Fact]
        public void No_legacy_glass_family_identifiers_remain_in_source()
        {
            // 2026-08-29 completion sweep: the rewrite must not leave a
            // single legacy-family identifier behind. Word-boundary
            // matching keeps the NEW shared design system intact
            // (LiquidGlassCard, LiquidGlassBrush etc. do not contain
            // these identifiers).
            var root = Root();
            var sep = Path.DirectorySeparatorChar;
            var banned = new[]
            {
                "GlassChromeService", "ReapplyWindowChrome", "SyncGlassChrome",
                "GlassSurface", "GlassIntensity", "SolidGlass", "HueGlass",
                "AccentGlass", "FrozenGlassText", "AuthGlassStyles",
                "GlassPlate", "GlassGlow", "DangerOnGlass", "WarningOnGlass",
                "DashGlassSurfaceBrush", "DashGlassBorderBrush",
                "ShellGlassBrush", "ShellGlassBorderBrush",
                "GlassOrbA", "GlassOrbB", "GlassEdgeBrush",
                "GlassInnerHighlightBrush", "GlassReflectionBrush",
                "GlassControlHoverBrush", "GlassControlPressedBrush",
                "GlassControlDisabledBrush", "GlassFocusBrush",
                "GlassSelectionBrush", "GlassPrimaryButtonBrush",
                "GlassOverlayBrush", "GlassShadowColor",
                "GlassIce", "GlassFrost", "GlassCerulean", "GlassSnow",
                "GlassAccentDeep", "GlassAccent", "GlassMuted",
                "GlassCard", "GlassSheen"
            };

            foreach (var path in Directory.EnumerateFiles(root, "*.*",
                         SearchOption.AllDirectories)
                     .Where(p => p.EndsWith(".cs", StringComparison.Ordinal) ||
                                 p.EndsWith(".xaml", StringComparison.Ordinal))
                     .Where(p => !p.Contains(sep + "bin" + sep) &&
                                 !p.Contains(sep + "obj" + sep) &&
                                 !p.Contains(sep + ".arena" + sep) &&
                                 !p.Contains(sep + ".claude" + sep) &&
                                 !Path.GetFileName(p).Equals(
                                     "LiquidGlassThemeSourceTests.cs",
                                     StringComparison.Ordinal)))
            {
                string text = File.ReadAllText(path);
                foreach (var name in banned)
                {
                    Assert.False(
                        Regex.IsMatch(text, @"\b" + Regex.Escape(name) + @"\b"),
                        Path.GetFileName(path) + " still contains " + name);
                }
            }
        }

        [Fact]
        public void Appearance_page_contains_no_legacy_family_wording()
        {
            // The user-facing Appearance page must not mention the
            // deleted families anywhere — captions, hints or options.
            string section = Read(
                "Views/Settings/Sections/SettingsAppearanceSection.xaml");
            Assert.DoesNotContain("Glassy", section, StringComparison.Ordinal);
            Assert.DoesNotContain("over transparent glass", section, StringComparison.Ordinal);
            Assert.DoesNotContain("Glass or Glassy", section, StringComparison.Ordinal);
            Assert.DoesNotContain("Glass, Glassy", section, StringComparison.Ordinal);
            // the removed families must not appear in the caption key or
            // anywhere else on the page.
            Assert.DoesNotContain("Professional", section, StringComparison.Ordinal);
            Assert.DoesNotContain("Ocean", section, StringComparison.Ordinal);
        }

        [Fact]
        public void Every_liquid_token_is_defined_in_both_theme_dictionaries()
        {
            // Light and Dark must carry the SAME liquid token surface:
            // the design system and the engine resolve every token via
            // DynamicResource, so a missing per-mode definition would
            // silently drop a material at runtime.
            var light = ResourceKeys(LightTheme)
                .Where(k => k.StartsWith("Liquid", StringComparison.Ordinal) ||
                            k.StartsWith("DashLiquid", StringComparison.Ordinal) ||
                            k.StartsWith("ShellLiquid", StringComparison.Ordinal))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();
            var dark = ResourceKeys(Read("Themes/DarkTheme.xaml"))
                .Where(k => k.StartsWith("Liquid", StringComparison.Ordinal) ||
                            k.StartsWith("DashLiquid", StringComparison.Ordinal) ||
                            k.StartsWith("ShellLiquid", StringComparison.Ordinal))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();

            Assert.NotEmpty(light);
            Assert.Equal(light, dark);
        }

        [Fact]
        public void Filter_step_uses_liquid_key_names()
        {
            // The statement-generator filter step carried local
            // Glass*-named styles into the new family; they were
            // renamed to the Liquid* surface in the completion sweep.
            string filter = Read(
                "Views/Reports/StatementGenerator/Filter/FilterStep.xaml");
            Assert.Contains("LiquidComboBoxStyle", filter, StringComparison.Ordinal);
            Assert.Contains("LiquidDatePickerStyle", filter, StringComparison.Ordinal);
            Assert.Contains("LiquidFieldLabelStyle", filter, StringComparison.Ordinal);
            Assert.DoesNotContain("GlassComboBoxStyle", filter, StringComparison.Ordinal);
            Assert.DoesNotContain("GlassDatePickerStyle", filter, StringComparison.Ordinal);
            Assert.DoesNotContain("GlassFieldLabelStyle", filter, StringComparison.Ordinal);
        }

        [Fact]
        public void Splash_is_not_a_layered_window()
        {
            // AllowsTransparency turns the splash into a software-
            // rendered layered window: on Windows 11 that class of
            // window was the recurring source of white/frozen splash
            // screens and failed fade/foreground handoffs. The splash
            // content is fully opaque by design, so the flag must stay
            // off.
            string splash = Read("Views/Splash/SplashView.xaml");
            Assert.DoesNotContain("AllowsTransparency=\"True\"", splash, StringComparison.Ordinal);
            Assert.DoesNotContain("AllowsTransparency=\"true\"", splash, StringComparison.Ordinal);
        }

        [Fact]
        public void Theme_swap_overlays_cached_dictionaries_without_merged_add_remove()
        {
            string tm = Read("Services/ThemeManager.cs");
            Assert.Contains("OverlayThemeDictionary(palette)", tm, StringComparison.Ordinal);
            Assert.DoesNotContain("appDicts.Add(newDict)", tm, StringComparison.Ordinal);
        }

        [Fact]
        public void Card_template_draws_the_watery_meniscus_line()
        {
            // The droplet's surface-tension band: the white top
            // highlight hugging the rounded top of the card (reference
            // GlassTopReflection material).
            string styles = Read("Themes/Styles/LiquidGlassStyles.xaml");
            Assert.Contains("x:Name=\"Meniscus\"", styles, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource GlassTopReflection}", styles, StringComparison.Ordinal);
        }

        [Fact]
        public void Engine_guarantees_critical_resources_after_every_apply()
        {
            // Last line of defense: Apply and ApplyModule must run the
            // integrity pass in a finally block so an unresolved key
            // becomes a logged, self-healed fallback instead of a
            // silently broken screen.
            string engine = Engine;
            Assert.Contains("GuaranteeCriticalResources(resources)", engine, StringComparison.Ordinal);
            Assert.Contains("CriticalFallbacks(dark)", engine, StringComparison.Ordinal);
            Assert.Contains("ResourceReferenceKeyNotFoundException", engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Reference_dictionary_is_merged_and_controls_adopt_it()
        {
            // The canonical glass dictionary is merged first in App.xaml;
            // the shared button system renders the reference glass (idle
            // gradient + top glare), the app toggle is the reference
            // switch, the engine publishes the per-mode palette, and the
            // main window rides the reference app background.
            string app = Read("App.xaml");
            Assert.Contains("LiquidGlassDesign.xaml", app, StringComparison.Ordinal);

            string design = Read("Themes/Styles/LiquidGlassDesign.xaml");
            foreach (string key in new[]
                     {
                         "LiquidGlassPanel", "LiquidGlassCard",
                         "LiquidGlassButton", "LiquidGlassButtonPrimary",
                         "LiquidGlassSwitch", "LiquidGlassTextBox"
                     })
            {
                Assert.Contains("x:Key=\"" + key + "\"", design, StringComparison.Ordinal);
            }

            // …while the colour/brush tokens must exist in BOTH theme
            // dictionaries (per-mode values, resolved via DynamicResource).
            string lightTokens = LightTheme;
            string darkTokens = Read("Themes/DarkTheme.xaml");
            foreach (string key in new[]
                     {
                         "GlassSurfaceUltra", "GlassSurfaceStandard",
                         "GlassSurfaceElevated", "GlassTopReflection",
                         "GlassGradientIdle", "AppBackgroundBrush",
                         "TextPrimaryBrush", "TextSecondaryBrush"
                     })
            {
                Assert.Contains("x:Key=\"" + key + "\"", lightTokens, StringComparison.Ordinal);
                Assert.Contains("x:Key=\"" + key + "\"", darkTokens, StringComparison.Ordinal);
            }

            string buttons = Read("Styles/ButtonStyles.xaml");
            Assert.Contains("GlassTopReflectionGradient", buttons, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource GlassSurfaceBrush}", buttons, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource GlassSurfaceElevated}", buttons, StringComparison.Ordinal);

            string settings = Read("Views/Settings/SettingsSectionStyles.xaml");
            Assert.Contains("BasedOn=\"{StaticResource LiquidGlassToggleSwitch}\"",
                settings, StringComparison.Ordinal);

            string tm = Read("Services/ThemeManager.cs");
            Assert.Contains("PublishLiquidReference(", tm, StringComparison.Ordinal);
            Assert.Contains("resources[\"GlassGradientActive\"]", tm, StringComparison.Ordinal);

            string window = Read("MainWindow.xaml");
            Assert.Contains("{DynamicResource WindowBackgroundBrush}", window, StringComparison.Ordinal);
            Assert.Contains("Style=\"{StaticResource LiquidGlassWindow}\"", window, StringComparison.Ordinal);
        }

        [Fact]
        public void Adoption_layer_contains_no_hardcoded_colours()
        {
            // Rule 1 of the reference adoption: the shared control
            // layer must never pin a hex colour directly — everything
            // resolves to a defined reference brush.
            foreach (string rel in new[]
                     {
                         "Styles/ButtonStyles.xaml",
                         "Themes/Styles/LiquidGlassStyles.xaml",
                         "Themes/Styles/InputStyles.xaml"
                     })
            {
                string xaml = Read(rel);
                Assert.False(
                    Regex.IsMatch(
                        xaml, "(Background|BorderBrush|Foreground|Color|Fill)=\"#[0-9A-Fa-f]{6,8}\""),
                    rel + " contains a hardcoded colour");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  Cream/SaaS + readability passes (surviving families)
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Light_normal_canvas_is_pure_cream_white()
        {
            // "Normal" family = base LightTheme values (the engine does
            // not override the window for Normal). The light canvas must
            // be the creamy SaaS neutral, not the old icy blue-white.
            Assert.Contains("#F6F1E6", LightTheme, StringComparison.Ordinal);
            Assert.Contains("#FBF8F1", LightTheme, StringComparison.Ordinal);
            Assert.Contains("#FEFDFA", LightTheme, StringComparison.Ordinal);

            // ThemeManager re-publishes the reference light background at
            // runtime (every theme swap) — it must match the cream canvas
            // or Normal would snap back to ice blue on Light→Dark→Light.
            string tm = Read("Services/ThemeManager.cs");
            Assert.Contains("Color.FromRgb(0xF8, 0xF4, 0xEB)", tm, StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0xFE, 0xFC, 0xF8)", tm, StringComparison.Ordinal);
            Assert.DoesNotContain("0xD7, 0xED, 0xF9", tm, StringComparison.Ordinal);
        }

        [Fact]
        public void Status_tint_green_is_actually_green_in_both_themes()
        {
            // BUG FIX: TintGreenColor was a BLUE tint (#EFF6FF light,
            // #1E2B44 dark) — success chips/cards rendered blue.
            string light = LightTheme;
            Assert.Contains("#E8F6EC", light, StringComparison.Ordinal);
            Assert.DoesNotContain("<Color x:Key=\"TintGreenColor\">#EFF6FF", light, StringComparison.Ordinal);

            string dark = Read("Themes/DarkTheme.xaml");
            Assert.Contains("#14301F", dark, StringComparison.Ordinal);
            Assert.DoesNotContain("<Color x:Key=\"TintGreenColor\">#1E2B44", dark, StringComparison.Ordinal);
            // The dark blue tint is now a real BLUE, not the same navy
            // the green placeholder used.
            Assert.Contains("#16324C", dark, StringComparison.Ordinal);
        }

        [Fact]
        public void Frosted_glass_is_more_saturated_after_the_richness_pass()
        {
            // The surviving frost carries a visible accent kiss (light
            // 0.30, dark 0.38) instead of a whisper — the whole frosted
            // system reads saturated BLUE glass, not flat white.
            string engine = Engine;
            Assert.Contains("dark ? 0.38 : 0.30", engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Crystal_fluid_keeps_card_text_readable()
        {
            // Crystal Fluid (Ios-17pm, replaces Ultra Transparent): the
            // blue environment shows behind everything, but card text must
            // never wash out: glass frost (.36/.18 on module surfaces)
            // plus the bright white crystal edge.
            string engine = Engine;
            Assert.Contains("Color.FromArgb(0x8F, 0xFF, 0xFF, 0xFF)", engine, StringComparison.Ordinal);
            Assert.Contains("CARD FROST", engine, StringComparison.Ordinal);
            Assert.Contains("FrostFill(accent, dark ? 0.20 : 0.36, dark ? 0.12 : 0.18)", engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Sections_adopt_the_active_family_material_set()
        {
            // "Sections don't accept the theme": Settings section cards,
            // wizard tiles, sidebars and every input consumed the shared
            // GlassSurface*/GlassBorder* keys, but the engine only
            // overrode Module/Elevated/SideMenu — so no matter which
            // family was active, those screens kept the BASE THEME
            // glass. Each surviving family mirrors its material into the
            // shared set (Normal restores the base values via
            // ClearOverrides; the keys are managed).
            string engine = Engine;
            Assert.Contains("PublishSectionGlass", engine, StringComparison.Ordinal);
            Assert.True(
                Regex.Matches(engine, "PublishSectionGlass\\(resources").Count >= 2,
                "PublishSectionGlass must run for Monet and Crystal Fluid");
            Assert.Contains(
                "resources[\"GlassSurfaceBrush\"] = resources[\"ModuleBackgroundBrush\"]",
                engine, StringComparison.Ordinal);
            Assert.Contains(
                "resources[\"GlassSurfaceElevated\"] = resources[\"ElevatedSurfaceBrush\"]",
                engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Normal_family_is_free_of_the_blue_cast()
        {
            // Normal = pure cream SaaS. The static chroma that used to
            // make EVERY family read blue is gone from both themes.
            string light = LightTheme;
            Assert.DoesNotContain("#185FCDFF", light, StringComparison.Ordinal);
            Assert.DoesNotContain("#1A3050", light, StringComparison.Ordinal);
            Assert.DoesNotContain("#3C78A0", light, StringComparison.Ordinal);
            Assert.DoesNotContain("#32B4FF", light, StringComparison.Ordinal);
            Assert.DoesNotContain("#10E0E8FF", light, StringComparison.Ordinal);
            Assert.DoesNotContain("#08D7F0FF", light, StringComparison.Ordinal);
            Assert.DoesNotContain("#991E2832", light, StringComparison.Ordinal);

            // Warm replacements are present.
            Assert.Contains("#1457534E", light, StringComparison.Ordinal);   // MenuHoverBrush
            Assert.Contains("#9957534E", light, StringComparison.Ordinal);   // TextSecondaryColor
            Assert.Contains("#2A2118", light, StringComparison.Ordinal);     // ShadowSoft

            string dark = Read("Themes/DarkTheme.xaml");
            Assert.DoesNotContain("#183B82F6", dark, StringComparison.Ordinal);
            Assert.Contains("#203E3A34", dark, StringComparison.Ordinal);    // dark MenuHoverBrush
            Assert.Contains("#5C574C", dark, StringComparison.Ordinal);      // dark warm outline
        }

        [Fact]
        public void Runtime_text_ink_is_warm_and_managed_in_every_family()
        {
            // ThemeManager.PublishLiquidReference writes
            // TextPrimaryBrush/TextSecondaryBrush into the APP scope on
            // every Light/Dark swap, and the engine manages those keys so
            // Normal restores the theme's own cream ink (not the cool
            // slate #991E2832 that once masked it).
            string tm = Read("Services/ThemeManager.cs");
            Assert.Contains(
                "FreezeAndSet(resources, \"TextPrimaryBrush\", Color.FromRgb(0x1C, 0x19, 0x17))",
                tm, StringComparison.Ordinal);
            Assert.Contains(
                "Color.FromArgb(0x99, 0x57, 0x53, 0x4E)",
                tm, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Color.FromArgb(0x99, 0x1E, 0x28, 0x32)",
                tm, StringComparison.Ordinal);

            string engine = Engine;
            Assert.Contains("\"TextPrimaryBrush\", \"TextSecondaryBrush\"",
                engine, StringComparison.Ordinal);

            // The custom-accent fallbacks were cool slate too
            // (#475569 / #64748B) — the warm stone equivalents now guard
            // even the missing-key path.
            Assert.DoesNotContain("Color.FromRgb(0x47, 0x55, 0x69)",
                engine, StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0x57, 0x53, 0x4E)",
                engine, StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0x78, 0x71, 0x6C)",
                engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Settings_text_uses_ink_tokens_never_border_brushes()
        {
            // 7 Settings styles painted TEXT with GlassBorderSoft — a
            // BORDER token (white at 18-40% alpha) — so headings, tile
            // icons, hero values, group headers and the SELECT label
            // rendered white-on-white across every family. Text is now
            // TextPrimaryBrush (ink) and progress fill is AccentBrush.
            string styles = Read("Views/Settings/SettingsSectionStyles.xaml");

            foreach (string styleKey in new[]
            {
                "InfoCardTitleStyle", "ProviderSelectButtonStyle",
                "DownloadProgressBarStyle", "InfoGlyphTextStyle",
                "SettingsGroupHeaderTextStyle", "SettingsTileIconStyle",
                "SettingsTileHeroStyle"
            })
            {
                string block = Regex.Match(
                    styles,
                    "x:Key=\"" + styleKey + "\".*?(?=<Style |</ResourceDictionary>)",
                    RegexOptions.Singleline).Value;
                Assert.False(string.IsNullOrWhiteSpace(block),
                    styleKey + " style block not found");
                // xunit 2.9.2 has no (pattern, input, options) overload —
                // RegexOptions must be carried by the Regex itself.
                Assert.DoesNotMatch(
                    new Regex("Foreground.*?Value=\"[{]DynamicResource GlassBorderSoft[}]\"",
                        RegexOptions.Singleline),
                    block);
                Assert.Matches(
                    new Regex("Foreground.*?Value=\"[{]DynamicResource (TextPrimaryBrush|AccentBrush)[}]\"",
                        RegexOptions.Singleline),
                    block);
            }
        }

        [Fact]
        public void Settings_navigation_rail_has_real_touch_states()
        {
            // The rail's ACTIVE pill used GlassSurfaceUltra (~5% alpha) —
            // invisible on the frosted rail; IsPressed only dimmed the
            // whole button, and the glyph/label never changed colour.
            // Now: active = the material ramp + crystal edge + accent
            // ink, pressed = accent tint, and the glyph/label light up
            // accent via the parent button's Tag.
            string styles = Read("Views/Settings/SettingsSectionStyles.xaml");

            string navButton = Regex.Match(
                styles, "x:Key=\"SettingsNavButtonStyle\".*?(?=<Style )",
                RegexOptions.Singleline).Value;
            Assert.Contains(
                "Value=\"{DynamicResource GlassSurfaceBrush}\"",
                navButton, StringComparison.Ordinal);           // active pill
            string navTriggers = navButton.Substring(
                navButton.IndexOf("ControlTemplate.Triggers>"));
            Assert.DoesNotContain("GlassSurfaceUltra}",
                navTriggers, StringComparison.Ordinal);         // no 5% pill
            Assert.Contains(
                "Value=\"{DynamicResource TintIndigoBrush}\"",
                navButton, StringComparison.Ordinal);           // pressed
            Assert.Contains(
                "Value=\"{DynamicResource GlassBorderGradientCrystal}\"",
                navButton, StringComparison.Ordinal);           // active edge

            string glyph = Regex.Match(
                styles, "x:Key=\"NavGlyphStyle\".*?(?=<Style )",
                RegexOptions.Singleline).Value;
            string label = Regex.Match(
                styles, "x:Key=\"NavLabelStyle\".*?(?=<Style )",
                RegexOptions.Singleline).Value;
            Assert.Contains("AncestorType=Button", glyph, StringComparison.Ordinal);
            Assert.Contains("Value=\"Active\"", glyph, StringComparison.Ordinal);
            Assert.Contains("AncestorType=Button", label, StringComparison.Ordinal);
            Assert.Contains("Value=\"Active\"", label, StringComparison.Ordinal);
        }

        [Fact]
        public void Normal_drawer_frost_and_module_touch_are_visible()
        {
            // Normal has NO engine case, so the drawer's only frost is
            // the base theme's ShellLiquidBrush — which was ~5% alpha in
            // Light (#14F6F1E6) and ~4% in Dark: module buttons washed
            // out over the content card. Both themes now ship a readable
            // warm-cream / deep-slate frost.
            Assert.Contains("#E6F6F1E6", LightTheme, StringComparison.Ordinal);
            Assert.Contains("#D9FFFCF5", LightTheme, StringComparison.Ordinal);
            string dark = Read("Themes/DarkTheme.xaml");
            Assert.Contains("#D90F2847", dark, StringComparison.Ordinal);

            // Module nav "touch": the active item fill was the same
            // GlassSurfaceElevated as hover — the open module never read
            // as selected on the frosted drawer. The active module and
            // active sub-item now use the full material ramp.
            string shell = Read("Themes/Styles/ShellStyles.xaml");
            Assert.True(
                Regex.Matches(
                    shell,
                    "Value=\"[{]DynamicResource GlassSurfaceBrush[}]\"").Count >= 3,
                "SidebarSubMenuCard + active module + active sub-item must all use GlassSurfaceBrush");
        }

        [Fact]
        public void Normal_keyline_and_tints_stay_warm_neutrals()
        {
            // ApplyAccent used to derive the card keyline + Blue/Indigo
            // tints from the ACCENT hue in every family — with the
            // default blue accent the whole Normal canvas read blue.
            // In Normal the keyline is the theme's warm hairline and the
            // tints are warm neutrals; the accent still drives CTAs and
            // the colourful families still derive their own keylines.
            string tm = Read("Services/ThemeManager.cs");
            Assert.Contains("bool normalFamily", tm, StringComparison.Ordinal);
            Assert.Contains("keyline = ThemeManager.GetThemeColor(\"OutlineBrush\"",
                tm, StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0xD8, 0xD0, 0xC2)", tm, StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0xF2, 0xED, 0xE3)", tm, StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0xED, 0xE7, 0xDA)", tm, StringComparison.Ordinal);
        }

        [Fact]
        public void Engine_fallbacks_match_the_cream_normal_palette()
        {
            // GuaranteeCriticalResources injects a fallback when a key
            // fails to resolve — the old fallbacks were icy blue
            // (#D7E5F9 window, #334155 ink, #C7D2E1 outline). Now
            // cream/warm.
            string engine = Engine;
            Assert.Contains("Color.FromRgb(0xF6, 0xF1, 0xE6)", engine, StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0xFE, 0xFC, 0xF8)", engine, StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0xD8, 0xD0, 0xC2)", engine, StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0x1C, 0x19, 0x17)", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("Color.FromRgb(0x33, 0x41, 0x55)", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("Color.FromRgb(0xC7, 0xD2, 0xE1)", engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Crystal_fluid_covers_rail_dialogs_and_sections()
        {
            // Crystal Fluid (Ios-17pm): the settings rail is a FLOATING
            // crystal panel (.58/.32 blue-white frost) and dialogs/dropdown
            // popups sit one shelf stronger (.64/.42), so they stay
            // readable over the blue environment.
            string engine = Engine;
            Assert.Contains(
                "FrostFill(accent, dark ? 0.28 : 0.58, dark ? 0.16 : 0.32)",
                engine, StringComparison.Ordinal);
            Assert.Contains(
                "FrostFill(accent, dark ? 0.34 : 0.64, dark ? 0.20 : 0.42)",
                engine, StringComparison.Ordinal);
        }

        private static string[] ResourceKeys(string xaml) =>
            Regex.Matches(xaml, "x:Key=\"([^\"]+)\"")
                .Select(m => m.Groups[1].Value)
                .ToArray();

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Themes", "LightTheme.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate repository root.");
        }
    }
}
