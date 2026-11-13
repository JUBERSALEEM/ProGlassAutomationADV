// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/ThemeRegistryParityTests.cs
// PROJECT: ProGlassAutomation.Tests
// ─────────────────────────────────────────────────────────────────────────
// Phase-1 audit deliverable (2026-09-05 — ../../docs/SETTINGS_MODULE_AUDIT.md):
// the F-01..F-05 bug class was REGISTRY DRIFT — lists that must agree,
// maintained by hand, silently disagreeing:
//
//   F-01  family painter vs the managed-key contract      (ERR-50472F)
//   F-06  VM mode-coupling switch vs engine family data
//
// (F-03, the studio preset registry, was retired with the Theme Studio
// removal on 2026-09-06 — the preset/DefaultProfile machinery no longer
// exists, so its parity pins went with it.)
//
// These tests pin the registries so a future drift is a red build,
// not a customer crash report. No WPF Application is constructed —
// everything asserted here is static catalogue data (safe on any runner).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ProGlassAutomation.Services.Theme;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public class ThemeRegistryParityTests
    {
        // Canonical selectable contract: nine material families, each
        // with Light and Dark palette recipes. Older Industrial, Monet,
        // Crystal Fluid, and iOS identities are migration inputs only.
        private static readonly string[] AllFamilies =
        {
            VisualThemeEngine.BentoGrid,
            VisualThemeEngine.SpatialImmersive,
            VisualThemeEngine.SwissInternational,
            VisualThemeEngine.HolographicIridescent,
            VisualThemeEngine.Neumorphism,
            VisualThemeEngine.Claymorphism,
            VisualThemeEngine.LiquidGlass,
            VisualThemeEngine.Glassmorphism,
            VisualThemeEngine.FluentAcrylic,
        };

        // ── F-01: the managed-key contract ──────────────────────────────

        private static string[] ManagedKeys()
        {
            var field = typeof(VisualThemeEngine).GetField(
                "ManagedKeys",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);
            return (string[])field!.GetValue(null)!;
        }

        [Fact]
        public void Main_shell_surfaces_use_family_managed_geometry_and_surface_tokens()
        {
            string shell = Read("MainWindow.xaml");

            // Shell geometry follows the selected family instead of fixed pixel radii.
            Assert.Contains("CornerRadius=\"{DynamicResource ControlCornerRadius}\"", shell, StringComparison.Ordinal);
            Assert.Contains("CornerRadius=\"{DynamicResource CardCornerRadius}\"", shell, StringComparison.Ordinal);
            Assert.Contains("CornerRadius=\"{DynamicResource CardCornerRadiusLarge}\"", shell, StringComparison.Ordinal);
            Assert.Contains("CornerRadius=\"{DynamicResource RadiusCompact}\"", shell, StringComparison.Ordinal);

            // License chrome follows the active Light/Dark semantic surface.
            Assert.Contains("Background=\"{DynamicResource SurfacePrimaryBrush}\"", shell, StringComparison.Ordinal);
        }

        [Fact]
        public void Rewrite_component_primitives_are_merged_and_use_semantic_tokens()
        {
            string app = Read("App.xaml");
            string styles = Read("Themes/Styles/RewriteComponentStyles.xaml");

            Assert.Contains("Themes/Styles/RewriteComponentStyles.xaml", app, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"ProGlassRewriteWorkspaceStyle\"", styles, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"ProGlassRewriteCardStyle\"", styles, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"ProGlassRewriteRaisedCardStyle\"", styles, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"ProGlassRewriteInsetStyle\"", styles, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"ProGlassRewriteToolbarStyle\"", styles, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"ProGlassRewritePageHeaderStyle\"", styles, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", styles, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceCardBrush", styles, StringComparison.Ordinal);
            Assert.Contains("DynamicResource RadiusCard", styles, StringComparison.Ordinal);
            Assert.Contains("DynamicResource RadiusControl", styles, StringComparison.Ordinal);
        }

        [Fact]
        public void Studio_list_cards_and_workspaces_use_rewrite_semantic_surfaces()
        {
            string listStyles = Read("Views/Shared/Lists/StudioListResources.xaml");
            int cardStart = listStyles.IndexOf("<Style x:Key=\"Sl.Card\"", StringComparison.Ordinal);
            Assert.True(cardStart >= 0);
            int cardEnd = listStyles.IndexOf("</Style>", cardStart, StringComparison.Ordinal);
            Assert.True(cardEnd > cardStart);
            string cardStyle = listStyles.Substring(cardStart, cardEnd - cardStart);
            Assert.Contains("BasedOn=\"{StaticResource ProGlassRewriteCardStyle}\"", cardStyle, StringComparison.Ordinal);
            Assert.DoesNotContain("Appearance.StudioFace", cardStyle, StringComparison.Ordinal);
            Assert.DoesNotContain("Appearance.StudioEdge", cardStyle, StringComparison.Ordinal);

            foreach (var view in new[]
            {
                "Views/Inquiry/InquiryListView.xaml",
                "Views/Specifications/SpecificationsCenterView.xaml",
                "Views/SheetStore/SheetStoreView.xaml"
            })
                Assert.Contains("Background=\"{DynamicResource SurfaceWorkspaceBrush}\"",
                    Read(view), StringComparison.Ordinal);
        }

        [Fact]
        public void Theme_picker_uses_valid_WPF_height_properties()
        {
            string xaml = Read("Views/Settings/Sections/SettingsThemesSection.xaml");
            Assert.DoesNotContain("nine=", xaml, StringComparison.Ordinal);
            Assert.Contains("<RowDefinition Height=\"Auto\"/>", xaml, StringComparison.Ordinal);
        }

        [Fact]
        public void Additional_core_module_workspaces_use_the_semantic_workspace_surface()
        {
            foreach (var view in new[]
            {
                "Views/ProformaInvoice/ProformaInvoiceView.xaml",
                "Views/Reports/SalesReportView.xaml",
                "Views/Diagnostics/DiagnosticsView.xaml",
                "Views/UserCenter/UserCenterView.xaml"
            })
                Assert.Contains("Background=\"{DynamicResource SurfaceWorkspaceBrush}\"",
                    Read(view), StringComparison.Ordinal);
        }

        [Fact]
        public void ManagedKeys_ContainsEveryCrashCriticalBrush()
        {
            // ERR-50472F proved these must ALWAYS be family-managed:
            // ClearOverrides strips them and every family republishes
            // them. If one disappears from this list, a template can
            // inflate against an absent key again — that crash class
            // must never return.
            var critical = new[]
            {
                "WindowBackgroundBrush", "ModuleBackgroundBrush",
                "ElevatedSurfaceBrush", "SideMenuBackgroundBrush",
                "MenuHoverBrush",
                "DashLiquidSurfaceBrush", "DashAmbientBrush",
                "DashAmbientBlob1Brush", "DashAmbientBlob2Brush",
                "ShellLiquidBrush", "SidebarCardBrush",
                "LiquidGlassBrush", "LiquidGlassSubtleBrush",
                "LiquidGlassStrongBrush",
                "LiquidEdgeBrush", "LiquidInnerHighlightBrush",
                "LiquidReflectionBrush",
                "LiquidControlHoverBrush", "LiquidControlPressedBrush",
                "LiquidControlDisabledBrush",
                "LiquidFocusBrush", "LiquidSelectionBrush",
                "LiquidPrimaryButtonBrush", "LiquidOverlayBrush",
                "LiquidShadowColor",
                "TintIndigoBrush", "TintBlueBrush", "TintGreenBrush",
                "TintRedBrush", "TintAmberBrush", "TintPurpleBrush",
                "TintGrayBrush",
                "CardCornerRadius", "CardCornerRadiusLarge",
                "ControlCornerRadius",
                "PrimaryForegroundBrush", "SecondaryForegroundBrush",
                "MutedForegroundBrush", "TextPrimaryBrush",
                "TextSecondaryBrush",
            };

            var managed = ManagedKeys();
            foreach (var key in critical)
                Assert.Contains(key, managed);
        }

        [Fact]
        public void ManagedKeys_DoesNotContainAccentOwnedOutlines()
        {
            // Documented engine decision (VisualThemeEngine.Part1):
            // OutlineBrush / OutlineDefaultBrush / BorderBrush /
            // DividerBrush are written by ThemeManager.ApplyAccent LAST
            // on every paint. Listing them in ManagedKeys would make
            // ClearOverrides delete the accent-derived keyline every
            // family switch — a known historical bug. Keep them OUT.
            var managed = ManagedKeys();
            Assert.DoesNotContain("OutlineBrush", managed);
            Assert.DoesNotContain("OutlineDefaultBrush", managed);
            Assert.DoesNotContain("BorderBrush", managed);
            Assert.DoesNotContain("DividerBrush", managed);
        }

        // ── F-01 net: the integrity guarantee must cover the crash set ──

        private static string[] CriticalFallbackKeys(bool dark)
        {
            var method = typeof(VisualThemeEngine).GetMethod(
                "CriticalFallbacks",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var sequence = (System.Collections.IEnumerable)method!
                .Invoke(null, new object[] { dark })!;
            var keys = new List<string>();
            foreach (var item in sequence)
            {
                var field = item.GetType().GetField("Item1");
                Assert.NotNull(field);
                keys.Add((string)field!.GetValue(item)!);
            }
            return keys.ToArray();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CriticalFallbacks_CoverTheErpCrashSet(bool dark)
        {
            // The pre-seed (ERR-50472F fix) can only close the never-
            // absent window for keys this list knows. Every key the
            // settings/dashboard chrome cannot live without is pinned.
            var netCritical = new[]
            {
                "WindowBackgroundBrush", "ModuleBackgroundBrush",
                "ElevatedSurfaceBrush", "SideMenuBackgroundBrush",
                "MenuHoverBrush", "SidebarCardBrush", "ShellLiquidBrush",
                "DashLiquidSurfaceBrush", "DashAmbientBrush",
                "OutlineBrush", "BorderBrush", "DividerBrush",
                "PrimaryForegroundBrush", "SecondaryForegroundBrush",
                "MutedForegroundBrush", "AccentBrush",
                "LiquidGlassBrush", "LiquidGlassSubtleBrush",
                "LiquidGlassStrongBrush",
                "TintIndigoBrush", "TintBlueBrush", "TintGreenBrush",
                "TintRedBrush", "TintAmberBrush", "TintPurpleBrush",
                "TintGrayBrush",
            };

            var coverage = CriticalFallbackKeys(dark);
            foreach (var key in netCritical)
                Assert.Contains(key, coverage);
        }

        [Fact]
        public void Nine_theme_material_overlay_defines_all_families_and_both_modes()
        {
            string overlay = Read("Services/Theme/VisualThemeEngine.EightThemes.cs");
            foreach (var family in new[]
            {
                "BentoGrid", "SpatialImmersive", "SwissInternational",
                "HolographicIridescent", "Neumorphism", "Claymorphism",
                "LiquidGlass", "Glassmorphism", "FluentAcrylic"
            })
                Assert.Contains(family, overlay, StringComparison.Ordinal);

            Assert.Contains("ThemeManager.CurrentTheme == AppTheme.Dark", overlay, StringComparison.Ordinal);
            Assert.Contains("ApplyEightThemeMaterial(resources, Current)", Read("Services/Theme/VisualThemeEngine.Part1.cs"), StringComparison.Ordinal);
            string painter = Read("Services/Theme/VisualThemeEngine.Part1.cs");
            Assert.Contains("case LiquidGlass:\\n                        ApplyCrystalFluid(resources);", painter, StringComparison.Ordinal);
            Assert.Contains("case Claymorphism:", painter, StringComparison.Ordinal);
            Assert.Contains("case Glassmorphism:", painter, StringComparison.Ordinal);
            Assert.Contains("case SpatialImmersive:", painter, StringComparison.Ordinal);
            Assert.Contains("case HolographicIridescent:", painter, StringComparison.Ordinal);
            Assert.Contains("ApplyIndustrial(resources);", painter, StringComparison.Ordinal);
            Assert.Contains("bool crystal = Family == ProGlassThemeFamily.LiquidGlass;", Read("Services/Theme/Engine/ProGlassThemeEngine.cs"), StringComparison.Ordinal);
        }

        [Fact]
        public void Eight_theme_material_overlay_populates_live_card_shell_and_status_surface_sources()
        {
            string overlay = Read("Services/Theme/VisualThemeEngine.EightThemes.cs");

            // These are not preview-only tokens. The semantic surface publisher,
            // dashboard shell, and legacy ERP card styles read these exact keys.
            // If the overlay stops writing them, most of the app silently falls
            // back to ApplyIndustrial's shared cream palette.
            foreach (var key in new[]
            {
                "GlassUltraClearBrush", "GlassSurfaceBrush", "GlassCrystalBrush",
                "GlassFrostedBrush", "GlassSurfaceStandard", "GlassSurfaceUltra",
                "GlassSurfaceElevated", "GlassWaterBrush", "GlassBorderGradientCrystal",
                "GlassBorderSoft", "GlassBorderPrimary", "GlassBorderBright",
                "ShellLiquidBrush", "SidebarCardBrush", "DashLiquidSurfaceBrush",
                "DashAmbientBrush", "DashAmbientBlob1Brush", "DashAmbientBlob2Brush",
                "TintIndigoBrush", "TintBlueBrush", "TintGreenBrush", "TintRedBrush",
                "TintAmberBrush", "TintPurpleBrush", "TintGrayBrush",
                // Dedicated keys consumed directly by SettingsCardStyles.xaml.
                "SettingsCardFaceBrush", "SettingsCardEdgeBrush",
                "SettingsInnerEdgeBrush", "SettingsCardShadow", "Ios26CardShadow",
                "Ios26RadiusCard", "Ios26RadiusInner", "Ios26RadiusTile",
                "Ios26RadiusChip", "Ios26RadiusSmall", "Ios26CardBrush",
                "Ios26TileBrush", "Ios26RailBrush", "Ios26CardBorderBrush",
                "Ios26TileBorderBrush", "Ios26HairlineBrush", "Ios26HoverBrush",
                "Ios26AccentBrush", "Ios26AccentDeepBrush", "Ios26AccentSoftBrush",
                "Ios26AccentSoftTextBrush", "Ios26TextTitleBrush",
                "Ios26TextBodyBrush", "Ios26TextSecondaryBrush", "Ios26TextMutedBrush"
            })
                Assert.Contains($"resources[\"{key}\"]", overlay, StringComparison.Ordinal);

            string bridge = Read("Services/Theme/Engine/ThemeCompatibilityHub.cs");
            foreach (var roleKey in new[]
            {
                "GlassUltraClearBrush", "GlassSurfaceBrush", "GlassCrystalBrush",
                "GlassFrostedBrush", "GlassSurfaceElevated", "GlassSurfaceUltra",
                "GlassSurfaceStandard", "GlassBorderPrimary", "GlassBorderGradientCrystal",
                "ModuleBackgroundBrush"
            })
                Assert.DoesNotContain($"[\"{roleKey}\"] =", bridge, StringComparison.Ordinal);

            Assert.Contains(
                "(\"SurfaceInputBrush\", \"GlassSurfaceStandard\", \"GlassSurfaceUltra\")",
                bridge,
                StringComparison.Ordinal);

            string semanticPublisher = Read("Services/ThemeManager.Part2.cs");
            Assert.Contains("SetBrushResource(resources, \"Surface.Primary.BackgroundBrush\", primaryKey", semanticPublisher, StringComparison.Ordinal);
            Assert.Contains("SetBrushResource(resources, \"Surface.Secondary.BackgroundBrush\", secondaryKey", semanticPublisher, StringComparison.Ordinal);

            string globalStyles = Read("Themes/Styles/GlobalDesignSystem.xaml");
            Assert.Contains("DynamicResource Surface.Primary.BackgroundBrush", globalStyles, StringComparison.Ordinal);
            Assert.Contains("DynamicResource Surface.Secondary.BackgroundBrush", globalStyles, StringComparison.Ordinal);
        }


        [Fact]
        public void Shared_inputs_and_secondary_buttons_consume_family_control_materials()
        {
            string inputs = Read("Themes/Styles/InputStyles.xaml");
            Assert.Contains("DynamicResource ThemeControlFaceBrush", inputs, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeControlEdgeBrush", inputs, StringComparison.Ordinal);

            string global = Read("Themes/Styles/GlobalDesignSystem.xaml");
            int inputStart = global.IndexOf("<Style x:Key=\"Input.TextBox\"", StringComparison.Ordinal);
            Assert.True(inputStart >= 0);
            int inputEnd = global.IndexOf("<!-- ═══════════════════════════════════════════════════════════════", inputStart + 10, StringComparison.Ordinal);
            Assert.True(inputEnd > inputStart);
            string inputBlock = global.Substring(inputStart, inputEnd - inputStart);
            Assert.Contains("DynamicResource ThemeControlFaceBrush", inputBlock, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeControlEdgeBrush", inputBlock, StringComparison.Ordinal);

            string buttons = Read("Styles/ButtonStyles.xaml");
            Assert.Contains("DynamicResource ThemeControlFaceBrush", buttons, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeControlEdgeBrush", buttons, StringComparison.Ordinal);
        }


        [Fact]
        public void Dense_data_grids_use_opaque_family_table_materials()
        {
            string overlay = Read("Services/Theme/VisualThemeEngine.EightThemes.cs");
            foreach (var key in new[]
            {
                "ThemeTableSurfaceBrush", "ThemeTableHeaderBrush",
                "ThemeTableRowHoverBrush", "ThemeTableSelectionBrush",
                "ThemeTableBorderBrush"
            })
                Assert.Contains("resources[\"" + key + "\"]", overlay, StringComparison.Ordinal);

            string styles = Read("Themes/Styles/RewriteComponentStyles.xaml");
            Assert.Contains("x:Key=\"ThemeTableSurfaceBrush\"", styles, StringComparison.Ordinal);
            string global = Read("Themes/Styles/GlobalDesignSystem.xaml");
            Assert.Contains("x:Key=\"DataGrid.ColumnHeader\"", global, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"DataGrid.Row\"", global, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeTableHeaderBrush", global, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeTableRowHoverBrush", global, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeTableSelectionBrush", global, StringComparison.Ordinal);
            int start = global.IndexOf("<Style x:Key=\"DataGrid.Canonical\"", StringComparison.Ordinal);
            int end = global.IndexOf("</Style>", start, StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);
            Assert.Contains("DynamicResource ThemeTableSurfaceBrush", global.Substring(start, end - start), StringComparison.Ordinal);
        }

        [Fact]
        public void Every_approved_family_has_separate_light_and_dark_palette_recipes()
        {
            string overlay = Read("Services/Theme/VisualThemeEngine.EightThemes.cs");
            foreach (var family in new[]
            {
                "BentoGrid", "SpatialImmersive", "SwissInternational",
                "HolographicIridescent", "Neumorphism", "Claymorphism",
                "LiquidGlass", "Glassmorphism"
            })
            {
                // Every switch arm must explicitly provide a dark and light
                // palette rather than falling through to one shared palette.
                Assert.Matches(
                    family + @"\s*=>\s*dark\s*\?\s*new\([^\r\n]*\)\s*:\s*new\(",
                    overlay);
            }

            foreach (var token in new[]
            {
                "BackgroundBrush", "WindowBackgroundBrush", "ModuleBackgroundBrush",
                "ElevatedSurfaceBrush", "PrimaryForegroundBrush", "BorderBrush",
                "CardCornerRadius", "ControlCornerRadius", "ShadowElevated"
            })
                Assert.Contains($"resources[\"{token}\"]", overlay, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(VisualThemeEngine.BentoGrid)]
        [InlineData(VisualThemeEngine.SpatialImmersive)]
        [InlineData(VisualThemeEngine.SwissInternational)]
        [InlineData(VisualThemeEngine.HolographicIridescent)]
        [InlineData(VisualThemeEngine.Neumorphism)]
        [InlineData(VisualThemeEngine.Claymorphism)]
        [InlineData(VisualThemeEngine.LiquidGlass)]
        [InlineData(VisualThemeEngine.Glassmorphism)]
        public void Eight_theme_family_identity_round_trips(string family)
        {
            var parsed = ProGlassAutomation.Services.Theme.Engine.ProGlassThemeEngine.ParseFamily(family);
            Assert.Equal(family,
                ProGlassAutomation.Services.Theme.Engine.ProGlassThemeEngine.PersistFamily(parsed));
        }

        [Theory]
        [InlineData("Normal", VisualThemeEngine.BentoGrid)]
        [InlineData("Normal (Industrial)", VisualThemeEngine.BentoGrid)]
        [InlineData("Industrial", VisualThemeEngine.BentoGrid)]
        [InlineData("Monet", VisualThemeEngine.Claymorphism)]
        [InlineData("Crystal", VisualThemeEngine.LiquidGlass)]
        [InlineData("Crystal Fluid", VisualThemeEngine.LiquidGlass)]
        [InlineData("iOS 26", VisualThemeEngine.LiquidGlass)]
        [InlineData("iPhone 17 Pro Max", VisualThemeEngine.LiquidGlass)]
        public void Legacy_theme_identities_migrate_to_selectable_families(
            string legacy, string expected)
        {
            var parsed = ProGlassAutomation.Services.Theme.Engine.ProGlassThemeEngine.ParseFamily(legacy);
            Assert.Equal(expected,
                ProGlassAutomation.Services.Theme.Engine.ProGlassThemeEngine.PersistFamily(parsed));
        }

        // ── F-06: mode coupling lives in the engine ─────────────────────

        [Theory]
        [InlineData(VisualThemeEngine.NormalIndustrial, null)]
        [InlineData(VisualThemeEngine.Monet, null)]
        [InlineData(VisualThemeEngine.Ios26, null)]
        [InlineData(VisualThemeEngine.BentoGrid, null)]
        [InlineData(VisualThemeEngine.SpatialImmersive, null)]
        [InlineData(VisualThemeEngine.SwissInternational, null)]
        [InlineData(VisualThemeEngine.HolographicIridescent, null)]
        [InlineData(VisualThemeEngine.Neumorphism, null)]
        [InlineData(VisualThemeEngine.Claymorphism, null)]
        [InlineData(VisualThemeEngine.LiquidGlass, null)]
        [InlineData(VisualThemeEngine.Glassmorphism, null)]
        // Crystal Fluid (iPhone 17 Pro Max) is light-only (2026-09-05
        // product decision); Industrial, Monet and iOS 26 respect the
        // user's own mode.
        [InlineData(VisualThemeEngine.CrystalFluid, false)]
        public void ModePairFor_OnlyCrystalFluidCouplesTheMode(
            string family, bool? expected)
        {
            Assert.Equal(expected, VisualThemeEngine.ModePairFor(family));
        }

        // ── Catalogue coverage: scenes for every family ─────────────────

        [Fact]
        public void SceneCatalogue_CoversEveryFamily_Exactly()
        {
            var sceneKeys = BuiltinWallpaperScenes.All
                .Select(row => row.Key)
                .ToList();
            Assert.Equal(
                AllFamilies.OrderBy(k => k, StringComparer.Ordinal),
                sceneKeys.OrderBy(k => k, StringComparer.Ordinal));

            // ...and the painter actually renders each one (never throws,
            // never returns null).
            foreach (var family in AllFamilies)
                Assert.NotNull(BuiltinWallpaperScenes.For(family));
        }

        // ── the dropdown must offer exactly the catalogued families ──────

        // 2026-09-16. The defect this pins: VisualThemeOptionRow seeds
        // `string scene = "Studio"` and then looks the family up in
        // BuiltinWallpaperScenes.All. A family the picker offers but the
        // catalogue omits does not fail — it silently advertises another
        // family's wallpaper. Two of four rows were wrong that way, and
        // every existing test was green, because the catalogue and the
        // picker were each internally consistent and never compared.
        [Fact]
        public void ThemeDropdown_OffersExactlyTheCataloguedFamilies()
        {
            string vm = Read(
                "ViewModels/Settings/SettingsViewModel.ThemeWallpaper.cs");
            int start = vm.IndexOf("VisualThemeOptions", StringComparison.Ordinal);
            Assert.True(start >= 0, "VisualThemeOptions is gone from the VM");
            int end = vm.IndexOf("};", start, StringComparison.Ordinal);
            Assert.True(end > start, "cannot bound the VisualThemeOptions list");

            var offered = Regex.Matches(vm.Substring(start, end - start),
                    @"new\(\s*(?:[\w\.]*\.)?VisualThemeEngine\.(\w+)\s*\)")
                .Cast<Match>()
                .Select(m => ConstValue(m.Groups[1].Value))
                .ToList();

            Assert.Equal(
                AllFamilies.OrderBy(k => k, StringComparer.Ordinal),
                offered.OrderBy(k => k, StringComparer.Ordinal));

            // ...and every offered row resolves to a catalogued scene, so the
            // "Studio" fallback can never fire again.
            var catalogued = BuiltinWallpaperScenes.All
                .Select(row => row.Key)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var family in offered)
                Assert.Contains(family, catalogued);
        }

        [Fact]
        public void SceneLabel_IsNeverTheFamilyDisplayName()
        {
            // The catalogue's own contract: "scene labels differ from the
            // family display names so the theme picker never reads X — X".
            // It was documented and never enforced.
            foreach (var (key, label) in BuiltinWallpaperScenes.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(label),
                    "empty scene label for " + key);
                Assert.NotEqual(VisualThemeEngine.FamilyDisplayName(key), label);
                Assert.NotEqual(key, label);
            }
        }

        [Fact]
        public void SceneLabels_AreUniquePerFamily()
        {
            // Sharing "Luminous Water" between Crystal Fluid and iOS 26
            // made two picker rows — and two ThemeLinked stages — identical.
            var labels = BuiltinWallpaperScenes.All.Select(r => r.Label).ToList();
            Assert.Equal(labels.Count,
                labels.Distinct(StringComparer.Ordinal).Count());
        }

        private static string ConstValue(string name)
        {
            var field = typeof(VisualThemeEngine).GetField(
                name, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(field);
            return (string)field!.GetRawConstantValue()!;
        }

        private static string Read(string relativePath)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string probe = Path.Combine(dir.FullName, relativePath
                    .Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(probe)) return File.ReadAllText(probe);
                if (File.Exists(Path.Combine(
                        dir.FullName, "ProGlassAutomation.csproj")))
                    break;
                dir = dir.Parent;
            }

            throw new FileNotFoundException(
                "could not locate " + relativePath + " from the test runner");
        }
    }
}
