// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/LiquidGlassThemeSourceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Source-level invariants for the iOS Liquid Glass theme system
//         (2026-08-29 — system-wide rewrite + completion sweep)
//
// Pins the DESIGN of the iOS Liquid Glass family AND the COMPLETENESS of
// the system-wide rewrite: the legacy "Glass"/"Glassy" families, their
// resource keys, identifiers, file names and UI wording must stay
// deleted; the opaque in-window stage must stay self-contained (no DWM
// veil); the per-mode ramps (white water glass in Light, deep frost in
// Dark) must stay per-mode; and the Liquid* token surface must stay
// identical across BOTH theme dictionaries.
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

        [Fact]
        public void Liquid_glass_uses_white_water_glass_in_light_and_deep_frost_in_dark()
        {
            // The original defect: the light mode used the dark ramp —
            // cards vanished on the pale stage. The single iOS Liquid
            // Glass family keeps an intentional ramp per mode: white
            // water glass in Light, deep frost in Dark.
            // 2026-09-01 richness pass: both ramps got denser (richer
            // frost) so cards read as real glass instead of pale sheets.
            // 2026-09-01 UHD pass: the SETTINGS rail came back to a
            // visibly translucent 0.90/0.62 (0.96/0.76 was a flat white
            // slab on the light stage), while the MAIN shell sidebar
            // went richer (0.95/0.80) so module buttons stay readable.
            Assert.Contains("0.90, 0.64", Engine, StringComparison.Ordinal);
            Assert.Contains("0.90, 0.62", Engine, StringComparison.Ordinal); // settings rail — glass, not slab
            Assert.Contains("0.95, 0.80", Engine, StringComparison.Ordinal); // main shell sidebar — rich frost
            Assert.Contains("0.30, 0.12", Engine, StringComparison.Ordinal); // dark
            Assert.Contains("0.44, 0.20", Engine, StringComparison.Ordinal); // dark rail

            // 2026-08-29 watery upgrade: the light curve has a dip
            // under the top edge and a meniscus band — the droplet's
            // surface-tension light — instead of a linear fade.
            Assert.Contains("dip = (byte)(topA * 0.86)", Engine, StringComparison.Ordinal);
            Assert.Contains("meniscus = (byte)", Engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Liquid_glass_window_stage_is_opaque_and_self_contained()
        {
            // 2026-08-29 white-fade regression: an alpha veil over an
            // opaque WPF window surface blends toward the white clear
            // color on Windows 11 / transparency-off machines — the
            // whole app faded to a white sheet. The stage must be
            // opaque and visibly more colourful than the white panes.
            string engine = Engine;
            Assert.Contains("BuildLiquidStageBrush(", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("WithAlpha(baseA", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("WithAlpha(baseB", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("WithAlpha(stageA", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("WithAlpha(stageB", engine, StringComparison.Ordinal);
            Assert.Contains("0xD7, 0xE5, 0xF9", engine, StringComparison.Ordinal);
            Assert.Contains("0xAE, 0xCB, 0xF0", engine, StringComparison.Ordinal);

            // Watery stage (2026-08-29): a third accent bloom and
            // caustic light pools give the aurora layered water depth.
            Assert.Contains("LiquidOrbC(dark, accent)", engine, StringComparison.Ordinal);
            Assert.Contains("LiquidCaustic(dark, accent", engine, StringComparison.Ordinal);

            // 2026-08-29 completion sweep: the DWM chrome service that
            // used to veil the window surface is DELETED outright — no
            // OS-backdrop code path exists anymore.
            Assert.DoesNotContain("GlassChromeService", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("SyncGlassChrome", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("ReapplyWindowChrome", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("GlassChromeService", Read("MainWindow.xaml.cs"),
                StringComparison.Ordinal);
        }

        [Fact]
        public void Glass_and_glassy_are_removed_and_ios_liquid_glass_is_the_default_family()
        {
            // 2026-08-29 rewrite: the two confusing "Glass"/"Glassy"
            // families are GONE; one iOS Liquid Glass family replaces
            // them, adapts to Light/Dark, and is the app-wide default.
            string engine = Engine;
            Assert.DoesNotContain("public const string Glass =", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("public const string Glassy =", engine, StringComparison.Ordinal);
            Assert.Contains("public const string LiquidGlass = \"iOS Liquid Glass\";", engine, StringComparison.Ordinal);
            Assert.Contains("\"Glass\" => LiquidGlass", engine, StringComparison.Ordinal);
            Assert.Contains("\"Glassy\" => LiquidGlass", engine, StringComparison.Ordinal);

            string config = Read("Services/AI/AIConfig/AppConfig.cs");
            Assert.Contains("= \"iOS Liquid Glass\";", config, StringComparison.Ordinal);

            string appCs = Read("App.xaml.cs");
            Assert.Contains("?? \"iOS Liquid Glass\"", appCs, StringComparison.Ordinal);

            string vm = Read("ViewModels/Settings/SettingsViewModel.cs");
            Assert.Contains("VisualThemeEngine.LiquidGlass,", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("VisualThemeEngine.Glass,", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("VisualThemeEngine.Glassy,", vm, StringComparison.Ordinal);
        }

        [Fact]
        public void Light_glass_card_is_translucent_white_not_opaque()
        {
            // The second original defect: the light card was #FFFFFFFF
            // (opaque white) — a solid slab, not glass. The LiquidGlass
            // meniscus ramp is denser after the 2026-09-01 richness
            // pass but never reaches full opacity.
            Assert.Contains("#E4FFFFFF", LightTheme, StringComparison.Ordinal);
            Assert.Contains("#CEFFFFFF", LightTheme, StringComparison.Ordinal);
            Assert.Contains("#F6FFFFFF", LightTheme, StringComparison.Ordinal);
            Assert.Contains("#96FFFFFF", LightTheme, StringComparison.Ordinal); // specular sheen
        }

        [Fact]
        public void Liquid_stage_orb_stops_stay_the_reference_constants()
        {
            // The stage is OPAQUE and SELF-CONTAINED: its orb stops are
            // frozen constants inside BuildLiquidStageBrush (the 2026-08-29
            // boosted light values and the original dark values). The
            // About page's own orbs are now accent-derived at runtime
            // (ThemeManager.PublishAboutPalette), so there is no theme
            // copy to drift against — the engine IS the reference.
            // (2026-09-01 stale-pin fix: the old fact asserted theme
            // hexes that had been superseded by the runtime palette and
            // could never pass again.)
            Assert.Contains("0x66, 0x60, 0xA5, 0xFA", Engine, StringComparison.Ordinal);
            Assert.Contains("0x7A, 0x3B, 0x82, 0xF6", Engine, StringComparison.Ordinal);
            Assert.Contains("0x4D, 0x82, 0xF6, 0xFF", Engine, StringComparison.Ordinal);
            Assert.Contains("0x60, 0xA5, 0xFA, 0x66", Engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Light_glass_text_is_decoupled_from_surface_keys()
        {
            // Light glass text uses fixed WARM ink/stone constants — and
            // must never read the surface-named Liquid* keys again (that
            // coupling would white-out text when surfaces are
            // recoloured).
            // 2026-09-01 cream pass: the light palette was cool slate
            // with a BLUE secondary (#1D4ED8) — blue captions across the
            // whole light UI. Now warm ink like the Normal canvas.
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
            // (2026-09-01 stale-pin fix: the canonical cards consume
            // GlassUltraClearBrush/GlassSurfaceBrush/GlassSurfaceElevated
            // — the fact previously pinned GlassSurfaceStandard, which
            // no card file references, so it could never pass. The
            // engine's PublishSectionGlass overrides ALL of these.)
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
        public void Liquid_glass_design_system_is_merged_and_tokens_are_defined_in_both_themes()
        {
            // The global liquid glass system (2026-08-29): one shared
            // dictionary merged in App.xaml; every token it consumes is
            // defined in BOTH theme files so Light/Dark switching and
            // the iOS Liquid Glass family overrides always resolve.
            string app = Read("App.xaml");
            string light = LightTheme;
            string dark = Read("Themes/DarkTheme.xaml");
            string styles = Read("Themes/Styles/LiquidGlassStyles.xaml");

            Assert.Contains("LiquidGlassStyles.xaml", app, StringComparison.Ordinal);
            // (2026-09-01 stale-pin fix: the styles dictionary adopts the
            // reference CARD through BasedOn, not a "Controls/" path.)
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
        public void Liquid_glass_styles_never_pin_theme_colours_with_static_resource()
        {
            // Dynamic switching depends on DynamicResource — the design
            // system must never StaticResource the glass MATERIALS.
            // (Style-key references like {StaticResource
            // LiquidGlassButtonPrimary} in BasedOn are legitimate —
            // styles, not colours.)
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
            // (2026-09-01 stale-pin fix: the design system's surface
            // material is GlassSurfaceElevated/GlassUltraClearBrush —
            // GlassSurfaceStandard was never referenced there.)
            Assert.Contains("{DynamicResource GlassSurfaceElevated}", styles, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource GlassUltraClearBrush}", styles, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource GlassTopReflection}", styles, StringComparison.Ordinal);
        }

        [Fact]
        public void Engine_publishes_the_liquid_glass_tokens_for_the_liquid_family()
        {
            string engine = Engine;
            Assert.Contains("ApplyLiquidGlassTokens", engine, StringComparison.Ordinal);
            Assert.Contains("\"LiquidEdgeBrush\"", engine, StringComparison.Ordinal);
            Assert.Contains("\"LiquidSelectionBrush\"", engine, StringComparison.Ordinal);
            Assert.Contains("\"LiquidPrimaryButtonBrush\"", engine, StringComparison.Ordinal);
            Assert.Contains("CrystalEdge", engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Liquid_glass_card_control_exposes_intensity_and_radius()
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
            // The old Glass* FAMILY identifiers are gone. NOTE: the
            // ADOPTED design surface legitimately keeps keys named
            // GlassSurface* / GlassBorder* / GlassShadow* / GlassTop* /
            // GlassWater* — these are the canonical shared materials
            // every section consumes (the reference design), and this
            // test layer itself requires them to exist in both themes.
            // So the check targets the LEGACY identifiers only, exactly
            // mirroring the banned-identifier list below. The previous
            // version used Contains("Glass"), which false-failed the
            // whole suite — the adopted keys make it impossible to pass.
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
                        // with the old identifiers (GlassFrost vs
                        // GlassFrostedBrush). Prefix matching would
                        // false-flag the adopted design.
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
            // matching keeps the NEW family intact (LiquidGlassCard,
            // LiquidGlassBrush etc. do not contain these identifiers).
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
        public void Theme_swap_adds_the_new_dictionary_before_removing_the_old()
        {
            // Removing the theme dictionary before adding its replacement
            // leaves a gap where EVERY DynamicResource bound to a theme
            // key resolves to nothing — and if the new dictionary load
            // throws, the app stays themeless for the whole session
            // ("all themes broken"). The add must come first.
            string tm = Read("Services/ThemeManager.cs");
            int add = tm.IndexOf("appDicts.Add(newDict)", StringComparison.Ordinal);
            int remove = tm.IndexOf("appDicts.Remove(existing)", StringComparison.Ordinal);
            Assert.True(add >= 0, "ThemeManager must add the new theme dictionary");
            Assert.True(remove >= 0, "ThemeManager must remove the old theme dictionary");
            Assert.True(add < remove,
                "ThemeManager must ADD the new theme dictionary BEFORE removing the old one");
        }

        [Fact]
        public void Accent_application_does_not_clobber_the_liquid_family_tints()
        {
            // ApplyAccent runs after the engine on every family switch;
            // it must not repaint the Blue/Indigo tint chips opaque over
            // the iOS Liquid Glass family's see-through hue frost.
            string tm = Read("Services/ThemeManager.cs");
            int guard = tm.IndexOf(
                "VisualThemeEngine.Current != VisualThemeEngine.LiquidGlass",
                StringComparison.Ordinal);
            int tintBlue = tm.IndexOf(
                "FreezeAndSet(resources, \"TintBlueBrush\"",
                StringComparison.Ordinal);
            Assert.True(guard >= 0, "tint guard missing");
            Assert.True(tintBlue >= 0, "TintBlueBrush write missing");
            Assert.True(guard < tintBlue,
                "the Liquid Glass tint guard must come before the TintBlueBrush write");
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
        public void Liquid_glass_reference_dictionary_is_merged_and_controls_adopt_it()
        {
            // 2026-08-29 reference adoption: the canonical dictionary is
            // merged first in App.xaml, the shared button system renders
            // the reference glass (idle gradient + top glare), the app
            // toggle is the reference switch, the engine publishes the
            // per-mode palette, and the main window rides the reference
            // app background.
            string app = Read("App.xaml");
            Assert.Contains("LiquidGlassDesign.xaml", app, StringComparison.Ordinal);

            string design = Read("Themes/Styles/LiquidGlassDesign.xaml");
            // 2026-09-01 stale-pin fix: LiquidGlassDesign.xaml is a
            // TEMPLATE dictionary (it declares in its own header that
            // every colour/brush token is defined in the theme files).
            // The control styles live here…
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
            // (2026-09-01 stale-pin fix: the pill template's glare layer
            // consumes GlassTopReflectionGradient and the pill surfaces
            // consume GlassSurfaceBrush / GlassSurfaceElevated — the old
            // pins referenced GlassGradientIdle/GlassTopReflection,
            // neither of which appears in ButtonStyles.)
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
            // 2026-09-01 stale-pin fix: the main window rides the
            // WINDOW background brush (the family's opaque aurora stage /
            // Normal's canvas), not AppBackgroundBrush — the reference
            // dictionary still carries AppBackgroundBrush for adoption.
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
                    System.Text.RegularExpressions.Regex.IsMatch(
                        xaml, "(Background|BorderBrush|Foreground|Color|Fill)=\"#[0-9A-Fa-f]{6,8}\""),
                    rel + " contains a hardcoded colour");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  2026-09-01 RICHNESS PASS — the theme system must stay vivid
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
            // The Liquid Glass family's frost carries a visible accent
            // kiss (light 0.16, dark 0.32) instead of a whisper — the
            // whole frosted system reads saturated, not flat white.
            string engine = Engine;
            Assert.Contains("dark ? 0.32 : 0.16", engine, StringComparison.Ordinal);
            Assert.Contains("HueFrost(\"TintBlueBrush\", 0.36, 0.18)", engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Ultra_transparent_keeps_card_text_readable()
        {
            // Ultra Transparent shows the wallpaper behind everything but
            // must never lose the text on cards: a whisper of card frost
            // plus a stronger crystal edge (36% white, was 24%).
            string engine = Engine;
            Assert.Contains("Color.FromArgb(0x5C, 0xFF, 0xFF, 0xFF)", engine, StringComparison.Ordinal);
            Assert.Contains("CARD FROST", engine, StringComparison.Ordinal);
            Assert.Contains("FrostFill(accent, dark ? 0.14 : 0.12, dark ? 0.07 : 0.06)", engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Industrial_family_carries_more_contrast_and_saturation()
        {
            // Professional is the industrial steel look: the cool slate
            // window/surfaces were near-white. The 2026-09-01 pass makes
            // the steel visibly saturated and higher contrast.
            string engine = Engine;
            Assert.Contains("Color.FromRgb(0xEC, 0xF2, 0xF9)", engine, StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0xD9, 0xE5, 0xF2)", engine, StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0xE2, 0xEA, 0xF4)", engine, StringComparison.Ordinal);
        }

        // ══════════════════════════════════════════════════════════════
        //  2026-09-01 SECTION-ADOPTION + CREAM/SaaS + UHD PASS
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Sections_adopt_the_active_family_material_set()
        {
            // "Sections don't accept the theme": Settings section cards,
            // wizard tiles, sidebars and every input consumed the shared
            // GlassSurface*/GlassBorder* keys, but the engine only
            // overrode Module/Elevated/SideMenu — so no matter which
            // family was active, those screens kept the BASE THEME
            // glass. Every family now mirrors its material into the
            // shared set (Normal restores the base values via
            // ClearOverrides; the keys are managed).
            string engine = Engine;
            Assert.Contains("PublishSectionGlass", engine, StringComparison.Ordinal);
            Assert.True(
                Regex.Matches(engine, "PublishSectionGlass\\(resources").Count >= 5,
                "PublishSectionGlass must run for LiquidGlass, Monet, " +
                "Professional, Ocean and Ultra Transparent");
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
            // make EVERY family read blue is gone from both themes:
            // MenuHoverBrush was #185FCDFF / #183B82F6 (blue chips,
            // hover fills, sliders, action buttons), the shadows were
            // blue-tinted (#1A3050 / #3C78A0 / #32B4FF), the card
            // refraction edge and water lines were icy blue, and light
            // secondary text was cool slate #991E2832.
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
        [Fact]
        public void Runtime_text_ink_is_warm_and_managed_in_every_family()
        {
            // The by-default bluish system theme had one more live path
            // than the base XAML: ThemeManager.PublishLiquidReference
            // writes TextPrimaryBrush/TextSecondaryBrush into the APP
            // scope on every Light/Dark swap, and VisualThemeEngine's
            // ManagedKeys did NOT contain those two keys — so even in
            // Normal the cool slate secondary (#991E2832 Light) survived
            // ClearOverrides and permanently masked the warm theme-file
            // value. The reference now publishes warm stone and the
            // engine manages the keys so Normal restores the theme's own
            // cream ink.
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
                Assert.DoesNotMatch(
                    "Foreground.*?Value=\"[{]DynamicResource GlassBorderSoft[}]\"",
                    block, RegexOptions.Singleline);
                Assert.Matches(
                    "Foreground.*?Value=\"[{]DynamicResource (TextPrimaryBrush|AccentBrush)[}]\"",
                    block, RegexOptions.Singleline);
            }
        }

        [Fact]
        public void Settings_navigation_rail_has_real_touch_states()
        {
            // The rail's ACTIVE pill used GlassSurfaceUltra (~5% alpha) —
            // invisible on the frosted rail; IsPressed only dimmed the
            // whole button (making it MORE transparent), and the glyph/
            // label never changed colour. Now: active = the material ramp
            // + crystal edge + accent ink, pressed = accent tint, and the
            // glyph/label light up accent via the parent button's Tag.
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
            // warm-cream / deep-slate frost (the families still override
            // this token at runtime).
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

        public void Normal_keyline_and_tints_stay_warm_neutrals()
        {
            // ApplyAccent used to derive the card keyline + Blue/Indigo
            // tints from the ACCENT hue in every family — with the
            // default blue accent the whole Normal canvas read blue
            // (the "industrial theme forcing blue" feel). In Normal the
            // keyline is the theme's warm hairline and the tints are
            // warm neutrals; the accent still drives CTAs and the
            // colourful families still derive their own keylines.
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
            // (#D7E5F9 window, #334155 ink, #C7D2E1 outline) and once
            // injected they stayed in the app scope. Now cream/warm.
            string engine = Engine;
            Assert.Contains("Color.FromRgb(0xF6, 0xF1, 0xE6)", engine, StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0xFE, 0xFC, 0xF8)", engine, StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0xD8, 0xD0, 0xC2)", engine, StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0x1C, 0x19, 0x17)", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("Color.FromRgb(0x33, 0x41, 0x55)", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("Color.FromRgb(0xC7, 0xD2, 0xE1)", engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Shell_sidebar_frost_is_rich_and_settings_rail_reads_as_glass()
        {
            // Main modules sidebar: the shell frost got RICHER (0.95/0.80
            // light, 0.42/0.20 dark) so module buttons stay readable —
            // previously 0.91/0.63 let the stage wash the nav text out.
            // Settings rail: 0.96/0.76 was a flat white slab that blended
            // into the light stage; 0.90/0.62 + top catch-light keeps it
            // GLASS but clearly visible (and the shadow was lifted).
            string engine = Engine;
            Assert.Contains("ShellLiquidBrush\"] = FrostFill(accent, 0.95, 0.80)", engine, StringComparison.Ordinal);
            Assert.Contains("ShellLiquidBrush\"] = FrostFill(accent, 0.42, 0.20)", engine, StringComparison.Ordinal);
            Assert.Contains("SidebarCardBrush\"] = FrostFill(accent, 0.92, 0.70)", engine, StringComparison.Ordinal);
            Assert.Contains("SideMenuBackgroundBrush\"] = FrostFill(accent, 0.90, 0.62)", engine, StringComparison.Ordinal);

            string settings = Read("Views/Settings/SettingsView.xaml");
            Assert.Contains("{DynamicResource ShellSheenBrush}", settings, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource GlassTopReflection}", settings, StringComparison.Ordinal);
            Assert.Contains("Opacity=\"0.20\"", settings, StringComparison.Ordinal);
        }

        [Fact]
        public void Ultra_transparent_covers_rail_dialogs_and_sections()
        {
            // Ultra Transparent used to leave SideMenu/Elevated at the
            // base-theme fills (opaque cream in Light!), so the settings
            // rail and popups never actually went transparent — the
            // section-adoption half of the family was broken. Now a
            // whisper of frost keeps them glass AND readable.
            string engine = Engine;
            Assert.Contains(
                "FrostFill(accent, dark ? 0.18 : 0.16, dark ? 0.10 : 0.08)",
                engine, StringComparison.Ordinal);
            Assert.Contains(
                "FrostFill(accent, dark ? 0.24 : 0.22, dark ? 0.14 : 0.12)",
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
