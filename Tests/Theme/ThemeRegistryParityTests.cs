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
            Assert.Contains("DynamicResource ThemeCardFaceBrush", styles, StringComparison.Ordinal);
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
            Assert.Contains("case LiquidGlass:", painter, StringComparison.Ordinal);
            Assert.Contains("ApplyCrystalFluid(resources);", painter, StringComparison.Ordinal);
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
            Assert.Contains("DynamicResource SurfacePrimaryBrush", globalStyles, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceSecondaryBrush", globalStyles, StringComparison.Ordinal);
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
        public void Professional_sidebar_uses_runtime_nine_family_navigation_materials()
        {
            string overlay = Read("Services/Theme/VisualThemeEngine.EightThemes.cs");
            foreach (var key in new[]
            {
                "ThemeNavigationSelectedBrush", "ThemeNavigationSelectedForegroundBrush",
                "ThemeNavigationHoverBrush", "ThemeNavigationHoverForegroundBrush",
                "ThemeNavigationFocusBrush", "ThemeNavigationBorderBrush",
                "ThemeNavigationMutedBrush"
            })
                Assert.Contains("resources[\"" + key + "\"]", overlay, StringComparison.Ordinal);

            string shell = Read("Themes/Professional/ProfessionalShell.xaml");
            int start = shell.IndexOf("<Style x:Key=\"PgSidebarItemStyle\"", StringComparison.Ordinal);
            int end = shell.IndexOf("</Style>", shell.IndexOf("<Style x:Key=\"PgSidebarSubListStyle\"", start, StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);
            string navStyles = shell.Substring(start, end - start);
            Assert.Contains("DynamicResource ThemeNavigationSelectedBrush", navStyles, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeNavigationSelectedForegroundBrush", navStyles, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeNavigationHoverBrush", navStyles, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeNavigationBorderBrush", navStyles, StringComparison.Ordinal);
            Assert.DoesNotContain("StaticResource PgSelectedRowBrush", navStyles, StringComparison.Ordinal);
        }


        [Fact]
        public void Professional_drawer_search_and_subitems_follow_runtime_theme_materials()
        {
            string shell = Read("Themes/Professional/ProfessionalShell.xaml");
            Assert.Contains("DynamicResource ThemeCardFaceBrush", shell, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeNavigationBorderBrush", shell, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeControlFaceBrush", shell, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeControlEdgeBrush", shell, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeNavigationSelectedBrush", shell, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeNavigationHoverBrush", shell, StringComparison.Ordinal);
            Assert.DoesNotContain("StaticResource PgSelectedRowBrush", shell, StringComparison.Ordinal);
            Assert.DoesNotContain("StaticResource PgLavenderRibbonBrush", shell, StringComparison.Ordinal);
        }


        [Fact]
        public void Professional_top_bars_and_utility_chrome_use_family_surfaces()
        {
            string shell = Read("Themes/Professional/ProfessionalShell.xaml");
            int top = shell.IndexOf("<Style x:Key=\"PgTopBarStyle\"", StringComparison.Ordinal);
            int topEnd = shell.IndexOf("</Style>", top, StringComparison.Ordinal);
            Assert.True(top >= 0 && topEnd > top);
            Assert.Contains("DynamicResource ThemeCardFaceBrush", shell.Substring(top, topEnd - top), StringComparison.Ordinal);

            int search = shell.IndexOf("<Style x:Key=\"PgSearchKbdStyle\"", StringComparison.Ordinal);
            int searchEnd = shell.IndexOf("</Style>", search, StringComparison.Ordinal);
            Assert.True(search >= 0 && searchEnd > search);
            Assert.Contains("DynamicResource ThemeControlFaceBrush", shell.Substring(search, searchEnd - search), StringComparison.Ordinal);

            int moduleBar = shell.IndexOf("<Style x:Key=\"PgModBarStyle\"", StringComparison.Ordinal);
            int moduleBarEnd = shell.IndexOf("</Style>", moduleBar, StringComparison.Ordinal);
            Assert.True(moduleBar >= 0 && moduleBarEnd > moduleBar);
            Assert.Contains("DynamicResource ThemeCardFaceBrush", shell.Substring(moduleBar, moduleBarEnd - moduleBar), StringComparison.Ordinal);
        }

        [Fact]
        public void Every_approved_family_has_separate_light_and_dark_palette_recipes()
        {
            string overlay = Read("Services/Theme/VisualThemeEngine.EightThemes.cs");
            foreach (var family in new[]
            {
                "BentoGrid", "SpatialImmersive", "SwissInternational",
                "HolographicIridescent", "Neumorphism", "Claymorphism",
                "LiquidGlass", "Glassmorphism", "FluentAcrylic"
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

        [Fact]
        public void Nine_family_engine_is_wired_from_settings_through_pipeline_to_publisher()
        {
            string settings = Read("ViewModels/Settings/SettingsViewModel.ThemeWallpaper.cs");
            string pipeline = Read("Services/Theme/ThemePipeline.cs");
            string themeManager = Read("Services/ThemeManager.Part2.cs");
            string publisher = Read("Services/Theme/Engine/ProGlassThemeEngine.cs");

            Assert.Contains("ProGlassThemeEngine.ParseFamily(raw)", settings, StringComparison.Ordinal);
            Assert.Contains("ProGlassThemeEngine.PersistFamily(", settings, StringComparison.Ordinal);
            Assert.Contains("VisualThemeEngine.ApplyModule(", pipeline, StringComparison.Ordinal);
            Assert.Contains("ThemeManager.PublishProGlassDesignSystem()", pipeline, StringComparison.Ordinal);
            Assert.Contains("ProGlassThemeEngine.Publish();", themeManager, StringComparison.Ordinal);
            Assert.Contains("Family = ParseFamily(appearance?.VisualTheme);", publisher, StringComparison.Ordinal);
            Assert.Contains("Mode = ThemeManager.CurrentTheme == AppTheme.Dark", publisher, StringComparison.Ordinal);
            Assert.Contains("ApplyMaterialTokens(resources);", publisher, StringComparison.Ordinal);
            Assert.Contains("DualWriteSemanticAliases(resources);", publisher, StringComparison.Ordinal);
            Assert.Contains("DualWriteLegacy(resources);", publisher, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(ProGlassAutomation.Services.Theme.Engine.ProGlassThemeFamily.BentoGrid)]
        [InlineData(ProGlassAutomation.Services.Theme.Engine.ProGlassThemeFamily.SpatialImmersive)]
        [InlineData(ProGlassAutomation.Services.Theme.Engine.ProGlassThemeFamily.SwissInternational)]
        [InlineData(ProGlassAutomation.Services.Theme.Engine.ProGlassThemeFamily.HolographicIridescent)]
        [InlineData(ProGlassAutomation.Services.Theme.Engine.ProGlassThemeFamily.Neumorphism)]
        [InlineData(ProGlassAutomation.Services.Theme.Engine.ProGlassThemeFamily.Claymorphism)]
        [InlineData(ProGlassAutomation.Services.Theme.Engine.ProGlassThemeFamily.LiquidGlass)]
        [InlineData(ProGlassAutomation.Services.Theme.Engine.ProGlassThemeFamily.Glassmorphism)]
        [InlineData(ProGlassAutomation.Services.Theme.Engine.ProGlassThemeFamily.FluentAcrylic)]
        public void Fallback_palette_is_family_specific_in_both_modes(
            ProGlassAutomation.Services.Theme.Engine.ProGlassThemeFamily family)
        {
            var industrialLight = ProGlassAutomation.Services.Theme.Engine.ProGlassPalettes.Resolve(
                ProGlassAutomation.Services.Theme.Engine.ProGlassThemeFamily.Industrial,
                ProGlassAutomation.Services.Theme.Engine.ProGlassThemeMode.Light);
            var industrialDark = ProGlassAutomation.Services.Theme.Engine.ProGlassPalettes.Resolve(
                ProGlassAutomation.Services.Theme.Engine.ProGlassThemeFamily.Industrial,
                ProGlassAutomation.Services.Theme.Engine.ProGlassThemeMode.Dark);
            var light = ProGlassAutomation.Services.Theme.Engine.ProGlassPalettes.Resolve(
                family, ProGlassAutomation.Services.Theme.Engine.ProGlassThemeMode.Light);
            var dark = ProGlassAutomation.Services.Theme.Engine.ProGlassPalettes.Resolve(
                family, ProGlassAutomation.Services.Theme.Engine.ProGlassThemeMode.Dark);

            Assert.NotEqual(industrialLight.Window, light.Window);
            Assert.NotEqual(industrialLight.Accent, light.Accent);
            Assert.NotEqual(industrialDark.Window, dark.Window);
            Assert.NotEqual(industrialDark.Accent, dark.Accent);
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
        [InlineData(VisualThemeEngine.FluentAcrylic)]
        public void Nine_theme_family_identity_round_trips(string family)
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

        [Fact]
        public void Module_tabs_and_context_menus_use_nine_family_materials()
        {
            string engine = Read("Services/Theme/VisualThemeEngine.EightThemes.cs");
            string shell = Read("Themes/Professional/ProfessionalShell.xaml");
            string tabs = Read("Themes/Styles/ModuleTabStyles.xaml");
            string menus = Read("Styles/ContextMenuStyles.xaml");

            foreach (string key in new[]
            {
                "ThemeNavigationSelectedBrush",
                "ThemeNavigationSelectedForegroundBrush",
                "ThemeNavigationHoverBrush",
                "ThemeNavigationHoverForegroundBrush",
                "ThemeNavigationFocusBrush",
                "ThemeNavigationBorderBrush",
                "ThemeNavigationMutedBrush"
            })
            {
                Assert.Contains(key, engine, StringComparison.Ordinal);
                Assert.Contains(key, shell, StringComparison.Ordinal);
            }

            Assert.Contains("DynamicResource ThemeNavigationSelectedBrush", tabs, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeNavigationHoverBrush", tabs, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeNavigationBorderBrush", tabs, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeNavigationFocusBrush", menus, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeNavigationHoverBrush", menus, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeCardFaceBrush", menus, StringComparison.Ordinal);
            Assert.DoesNotContain("DynamicResource AccentBrush", tabs, StringComparison.Ordinal);
            Assert.DoesNotContain("DynamicResource MenuHoverBrush", menus, StringComparison.Ordinal);
        }

        [Fact]
        public void Shared_dialog_chrome_uses_nine_family_surface_materials()
        {
            string dialogs = Read("Themes/Styles/DialogStyles.xaml");
            Assert.Contains("DynamicResource ThemeCardFaceBrush", dialogs, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeCardEdgeBrush", dialogs, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeControlFaceBrush", dialogs, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeControlEdgeBrush", dialogs, StringComparison.Ordinal);
            Assert.DoesNotContain("DynamicResource DialogSurfaceBrush", dialogs, StringComparison.Ordinal);
            Assert.DoesNotContain("DynamicResource DialogInsetBrush", dialogs, StringComparison.Ordinal);
        }

        [Fact]
        public void Optimization_summary_uses_semantic_theme_status_colors()
        {
            string summary = Read("Views/Optimization/Tabs/SummaryTabView.xaml");
            Assert.Contains("DynamicResource OnAccentForegroundBrush", summary, StringComparison.Ordinal);
            Assert.Contains("DynamicResource TintRedBrush", summary, StringComparison.Ordinal);
            Assert.Contains("DynamicResource DangerBrush", summary, StringComparison.Ordinal);
            Assert.DoesNotContain("#FFFFFF", summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("#FEE2E2", summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("#991B1B", summary, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Optimization_shared_cards_inputs_and_navigation_use_family_materials()
        {
            string styles = Read("Themes/Styles/OptimizationStyles.xaml");
            Assert.Contains("DynamicResource ThemeCardEdgeBrush", styles, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeControlFaceBrush", styles, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeControlEdgeBrush", styles, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfacePrimaryBrush", styles, StringComparison.Ordinal);
        }

        [Fact]
        public void Optimization_tabs_do_not_use_fixed_status_badge_brushes()
        {
            string[] paths =
            {
                "Views/Optimization/Tabs/StocksTabView.xaml",
                "Views/Optimization/Tabs/SummaryTabView.xaml",
                "Views/Optimization/Tabs/JobsTabView.xaml",
                "Views/Optimization/Tabs/ReportTabView.xaml"
            };
            foreach (string path in paths)
            {
                string xaml = Read(path);
                Assert.DoesNotContain("StaticResource OptSuccessBg", xaml, StringComparison.Ordinal);
                Assert.DoesNotContain("StaticResource OptWarningBg", xaml, StringComparison.Ordinal);
                Assert.DoesNotContain("StaticResource OptInfoBg", xaml, StringComparison.Ordinal);
                Assert.DoesNotContain("StaticResource OptSuccessBorder", xaml, StringComparison.Ordinal);
                Assert.DoesNotContain("StaticResource OptWarningBorder", xaml, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Optimization_gallery_and_unselected_rail_use_family_materials()
        {
            string styles = Read("Themes/Styles/OptimizationStyles.xaml");
            Assert.Contains("DynamicResource ThemeCardEdgeBrush", styles, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeCardHighlightBrush", styles, StringComparison.Ordinal);
        }

        [Fact]
        public void Optimization_cut_visualization_uses_runtime_theme_materials()
        {
            string xaml = Read("Views/Optimization/Tabs/TwoDViewTabView.xaml");
            Assert.Contains("DynamicResource SurfacePrimaryBrush", xaml, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceSecondaryBrush", xaml, StringComparison.Ordinal);
            Assert.Contains("DynamicResource TextPrimaryBrush", xaml, StringComparison.Ordinal);
            Assert.Contains("DynamicResource AccentBrush", xaml, StringComparison.Ordinal);
            Assert.Contains("DynamicResource TintAmberBrush", xaml, StringComparison.Ordinal);
            Assert.Contains("DynamicResource TintPurpleBrush", xaml, StringComparison.Ordinal);
            Assert.Contains("DynamicResource OnAccentForegroundBrush", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("#D6D3D1", xaml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("#0F766E", xaml, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Production_kpi_badges_and_pipeline_chips_follow_family_materials()
        {
            string kpis = Read("Views/Production/Controls/ProductionKpiStrip.xaml");
            string pipeline = Read("Views/Production/Controls/ProductionPipelineBar.xaml");
            Assert.Contains("DynamicResource TintBlueBrush", kpis, StringComparison.Ordinal);
            Assert.Contains("DynamicResource TintAmberBrush", kpis, StringComparison.Ordinal);
            Assert.Contains("DynamicResource TintGreenBrush", kpis, StringComparison.Ordinal);
            Assert.Contains("DynamicResource TextPrimaryBrush", kpis, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeCardEdgeBrush", pipeline, StringComparison.Ordinal);
            Assert.DoesNotContain("StaticResource OptSuccessBg", kpis, StringComparison.Ordinal);
            Assert.DoesNotContain("StaticResource OptWarningBg", kpis, StringComparison.Ordinal);
        }

        [Fact]
        public void Profitability_step_cards_use_nine_family_card_and_inset_materials()
        {
            string styles = Read("Themes/Styles/GlobalDesignSystem.xaml");
            string profitLoss = Read("Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml");
            string sheetProfitability = Read("Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml");
            Assert.Contains("x:Key=\"TgtCardStyle\" TargetType=\"Border\" BasedOn=\"{StaticResource ProGlassRewriteCardStyle}\"", styles, StringComparison.Ordinal);
            Assert.Contains("x:Key=\"TgtInsetStyle\" TargetType=\"Border\" BasedOn=\"{StaticResource ProGlassRewriteInsetStyle}\"", styles, StringComparison.Ordinal);
            Assert.Contains("TgtCardStyle", profitLoss, StringComparison.Ordinal);
            Assert.Contains("TgtCardStyle", sheetProfitability, StringComparison.Ordinal);
        }

        [Fact]
        public void Production_workspaces_use_theme_workspace_and_card_edges()
        {
            string filter = Read("Views/Production/Controls/ProductionFilterBar.xaml");
            string release = Read("Views/Production/Controls/ProductionReleaseBar.xaml");
            string station = Read("Views/Production/Controls/ProductionStationBody.xaml");
            string controlCenter = Read("Views/Production/ProductionControlCenterView.xaml");
            string cutting = Read("Views/Production/CuttingWorkCenterView.xaml");
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", filter, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", release, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", station, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeCardEdgeBrush", controlCenter, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeCardEdgeBrush", cutting, StringComparison.Ordinal);
            Assert.DoesNotContain("DynamicResource Ov.Canvas", filter, StringComparison.Ordinal);
            Assert.DoesNotContain("DynamicResource Ov.Canvas", release, StringComparison.Ordinal);
        }

        [Fact]
        public void Production_stage_pages_do_not_import_fixed_indigo_dashboard_canvas()
        {
            string[] pages =
            {
                "Views/Production/PolishView.xaml",
                "Views/Production/CuttingView.xaml",
                "Views/Production/PairingView.xaml",
                "Views/Production/MitringView.xaml",
                "Views/Production/TemperingView.xaml",
                "Views/Production/HolesCutoutsView.xaml",
                "Views/Production/EdgeDeletionView.xaml",
                "Views/Production/DoublingLaminationView.xaml"
            };
            foreach (string path in pages)
            {
                string xaml = Read(path);
                Assert.Contains("DynamicResource SurfaceWorkspaceBrush", xaml, StringComparison.Ordinal);
                Assert.DoesNotContain("IndigoStudioResources.xaml", xaml, StringComparison.Ordinal);
                Assert.DoesNotContain("Ov.Canvas", xaml, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Job_order_detail_styles_use_nine_family_materials_without_indigo_dictionary()
        {
            string styles = Read("Views/JobOrder/JobOrderDetail/Sections/JoSectionStyles.xaml");
            string overview = Read("Views/JobOrder/JobOrderDetail/Sections/JoOverviewSection.xaml");
            string detail = Read("Views/JobOrder/JobOrderDetail/JobOrderDetailView.xaml");
            string list = Read("Views/JobOrder/JobOrderView.xaml");
            Assert.DoesNotContain("IndigoStudioResources.xaml", styles, StringComparison.Ordinal);
            Assert.DoesNotContain("Ov.", styles, StringComparison.Ordinal);
            Assert.Contains("ProGlassRewriteCardStyle", styles, StringComparison.Ordinal);
            Assert.Contains("ProGlassRewriteInsetStyle", styles, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeCardEdgeBrush", overview, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", detail, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", list, StringComparison.Ordinal);
        }

        [Fact]
        public void Report_workspaces_and_generator_progress_follow_active_theme_materials()
        {
            string[] reports =
            {
                "Views/Reports/ProfitabilityReport/ProfitabilityReportView.xaml",
                "Views/Reports/ExecutiveReport/ExecutiveReportView.xaml",
                "Views/Reports/CompanyReport/CompanyReportView.xaml",
                "Views/Reports/CustomerReport/CustomerReportView.xaml",
                "Views/Reports/JoReport/JoReportView.xaml"
            };
            foreach (string path in reports)
            {
                string xaml = Read(path);
                Assert.Contains("DynamicResource SurfaceWorkspaceBrush", xaml, StringComparison.Ordinal);
                Assert.Contains("DynamicResource TextPrimaryBrush", xaml, StringComparison.Ordinal);
                Assert.DoesNotContain("DynamicResource Ov.Canvas", xaml, StringComparison.Ordinal);
                Assert.DoesNotContain("DynamicResource Ov.Ink", xaml, StringComparison.Ordinal);
            }
            string generator = Read("Views/Reports/Shared/GeneratorStudio.xaml");
            Assert.Contains("DynamicResource AccentBrush", generator, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeCardHighlightBrush", generator, StringComparison.Ordinal);
            Assert.DoesNotContain("Ov.", generator, StringComparison.Ordinal);
        }

        [Fact]
        public void Sheet_store_add_edit_and_history_use_nine_family_theme_surfaces()
        {
            string editor = Read("Views/SheetStore/AddEditSheetView.xaml");
            string history = Read("Views/SheetStore/SheetStoreHistory/HistoryView.xaml");
            Assert.DoesNotContain("IndigoStudioResources.xaml", editor, StringComparison.Ordinal);
            Assert.DoesNotContain("Ov.", editor, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", editor, StringComparison.Ordinal);
            Assert.Contains("ProGlassRewriteInsetStyle", editor, StringComparison.Ordinal);
            Assert.Contains("ProGlassRewriteControlSurfaceStyle", editor, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", history, StringComparison.Ordinal);
            Assert.Contains("DynamicResource TextPrimaryBrush", history, StringComparison.Ordinal);
            Assert.Contains("DynamicResource TintAmberBrush", history, StringComparison.Ordinal);
            Assert.DoesNotContain("Ov.", history, StringComparison.Ordinal);
        }

        [Fact]
        public void Inquiry_editor_uses_family_workspace_without_indigo_palette_merge()
        {
            string xaml = Read("Views/Inquiry/InquiryEditorWindow.xaml");
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("IndigoStudioResources.xaml", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("Ov.Canvas", xaml, StringComparison.Ordinal);
        }

        [Fact]
        public void Sheet_store_stock_and_import_dialogs_use_nine_family_component_styles()
        {
            string[] dialogs =
            {
                "Views/SheetStore/SheetStoreBuy/BuyDialogView.xaml",
                "Views/SheetStore/SheetStoreUse/UseDialogView.xaml",
                "Views/SheetStore/SheetStoreItems/ItemMovementView.xaml",
                "Views/SheetStore/SheetStoreCsv/DuplicateResolutionView.xaml"
            };
            foreach (string path in dialogs)
            {
                string xaml = Read(path);
                Assert.DoesNotContain("Ov.", xaml, StringComparison.Ordinal);
                Assert.Contains("ProGlassRewriteInsetStyle", xaml, StringComparison.Ordinal);
                Assert.Contains("ProGlassRewriteCaptionStyle", xaml, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Sheet_detail_workspace_uses_nine_family_materials_without_indigo_palette()
        {
            string xaml = Read("Views/SheetStore/SheetDetailWindow.xaml");
            Assert.DoesNotContain("IndigoStudioResources.xaml", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("Ov.", xaml, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", xaml, StringComparison.Ordinal);
            Assert.Contains("ProGlassRewriteCardStyle", xaml, StringComparison.Ordinal);
            Assert.Contains("ProGlassRewriteInsetStyle", xaml, StringComparison.Ordinal);
            Assert.Contains("ProGlassRewriteControlSurfaceStyle", xaml, StringComparison.Ordinal);
        }

        [Fact]
        public void Job_order_god_view_and_saved_list_use_live_theme_materials()
        {
            string godView = Read("Views/JobOrder/GodView/JobOrderGodView.xaml");
            string savedList = Read("Views/JobOrder/SavedJobOrdersList/SavedJobOrdersListView.xaml");
            foreach (string xaml in new[] { godView, savedList })
            {
                Assert.Contains("DynamicResource SurfaceWorkspaceBrush", xaml, StringComparison.Ordinal);
                Assert.Contains("DynamicResource TextPrimaryBrush", xaml, StringComparison.Ordinal);
                Assert.Contains("DynamicResource ThemeControlEdgeBrush", xaml, StringComparison.Ordinal);
                Assert.DoesNotContain("DynamicResource Ov.", xaml, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Primary_module_dashboards_use_live_nine_family_palette_resources()
        {
            string[] dashboards =
            {
                "Views/Dashboard/JobOrder/JoDashboard/JoDashboardView.xaml",
                "Views/Dashboard/ProformaInvoice/PiDashboardView.xaml",
                "Views/Dashboard/SheetStore/SheetStoreDashboardView.xaml"
            };
            foreach (string path in dashboards)
            {
                string xaml = Read(path);
                Assert.Contains("DynamicResource SurfaceWorkspaceBrush", xaml, StringComparison.Ordinal);
                Assert.Contains("DynamicResource TextPrimaryBrush", xaml, StringComparison.Ordinal);
                Assert.Contains("DynamicResource ThemeControlFaceBrush", xaml, StringComparison.Ordinal);
                Assert.DoesNotContain("DynamicResource Ov.", xaml, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Tax_invoice_and_delivery_note_use_nine_family_detail_materials()
        {
            string taxStyles = Read("Views/TaxInvoice/TaxInvoiceDetail/Sections/TiSectionStyles.xaml");
            string deliveryStyles = Read("Views/DeliveryNote/DeliveryNoteDetail/Sections/DnSectionStyles.xaml");
            string taxList = Read("Views/TaxInvoice/TaxInvoiceView.xaml");
            string deliveryList = Read("Views/DeliveryNote/DeliveryNoteView.xaml");
            string taxDetail = Read("Views/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailView.xaml");
            string deliveryDetail = Read("Views/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailView.xaml");
            string jobOrderList = Read("Views/JobOrder/JobOrderView.xaml");
            Assert.DoesNotContain("IndigoStudioResources.xaml", taxList, StringComparison.Ordinal);
            Assert.DoesNotContain("IndigoStudioResources.xaml", deliveryList, StringComparison.Ordinal);
            Assert.DoesNotContain("IndigoStudioResources.xaml", jobOrderList, StringComparison.Ordinal);
            foreach (string styles in new[] { taxStyles, deliveryStyles })
            {
                Assert.DoesNotContain("IndigoStudioResources.xaml", styles, StringComparison.Ordinal);
                Assert.DoesNotContain("Ov.", styles, StringComparison.Ordinal);
                Assert.Contains("ProGlassRewriteCardStyle", styles, StringComparison.Ordinal);
                Assert.Contains("ThemeCardEdgeBrush", styles, StringComparison.Ordinal);
            }
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", taxList, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", deliveryList, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", taxDetail, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", deliveryDetail, StringComparison.Ordinal);
        }

        [Fact]
        public void Authentication_ai_chat_and_quick_settings_use_shared_theme_resources()
        {
            string login = Read("Views/Login/LoginWindow.xaml");
            string auth = Read("Views/Login/AuthSurfaceStyles.xaml");
            string quick = Read("Views/Shared/QuickSettingsPanel.xaml");
            string crash = Read("Views/CrashReports/CrashReportDialogStyles.xaml");
            string ai = Read("Views/AI/AiChatView.xaml");
            foreach (string xaml in new[] { login, auth, quick, crash, ai })
                Assert.DoesNotContain("IndigoStudioResources.xaml", xaml, StringComparison.Ordinal);
            Assert.Contains("ThemeCardEdgeBrush", quick, StringComparison.Ordinal);
            Assert.Contains("ThemeCardFaceBrush", quick, StringComparison.Ordinal);
            Assert.Contains("SurfaceWorkspaceBrush", ai, StringComparison.Ordinal);
            Assert.DoesNotContain("Ov.", quick, StringComparison.Ordinal);
            Assert.DoesNotContain("Ov.", ai, StringComparison.Ordinal);
        }

        [Fact]
        public void Main_dashboard_shell_uses_nine_family_workspace_and_semantic_controls()
        {
            string dashboard = Read("Views/Dashboard/DashboardView.xaml");
            Assert.DoesNotContain("IndigoStudioResources.xaml", dashboard, StringComparison.Ordinal);
            Assert.DoesNotContain("Ov.", dashboard, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", dashboard, StringComparison.Ordinal);
            Assert.Contains("DynamicResource AccentBrush", dashboard, StringComparison.Ordinal);
            Assert.Contains("DynamicResource ThemeCardEdgeBrush", dashboard, StringComparison.Ordinal);
            Assert.Contains("StaticResource Typography.Caption", dashboard, StringComparison.Ordinal);
        }

        [Fact]
        public void Personal_glass_blush_token_plane_is_published_by_the_nine_family_engine()
        {
            string engine = Read("Services/Theme/Engine/ProGlassThemeEngine.cs");
            string bridge = Read("Services/Theme/Engine/ProGlassThemeEngine.GlassBlush.cs");
            string tokens = Read("Themes/GlassBlush/GlassBlushTokens.xaml");
            Assert.Contains("PublishGlassBlushAliases(resources)", engine, StringComparison.Ordinal);
            Assert.Contains("public static partial class ProGlassThemeEngine", engine, StringComparison.Ordinal);
            Assert.Contains("GlassBlush.Background", bridge, StringComparison.Ordinal);
            Assert.Contains("GlassBlush.Surface", bridge, StringComparison.Ordinal);
            Assert.Contains("GlassBlush.Accent", bridge, StringComparison.Ordinal);
            Assert.Contains("GlassBlush.Success", bridge, StringComparison.Ordinal);
            Assert.Contains("GlassBlushNight.Background", bridge, StringComparison.Ordinal);
            Assert.Contains("GlassBlush.Surface", tokens, StringComparison.Ordinal);
            Assert.Contains("GlassBlush.Accent", tokens, StringComparison.Ordinal);
        }

        [Fact]
        public void Personal_shell_and_all_personal_workspaces_use_the_live_glass_blush_bridge()
        {
            string[] views =
            {
                "Views/Personal/GlassShell.xaml",
                "Views/Personal/PersonalAiChatView.xaml",
                "Views/Personal/PersonalDashboardOverviewView.xaml",
                "Views/Personal/PersonalDashboardView.xaml",
                "Views/Personal/PersonalDeliveryNoteView.xaml",
                "Views/Personal/PersonalExecutiveReportView.xaml",
                "Views/Personal/PersonalJobOrderView.xaml",
                "Views/Personal/PersonalProductionControlCenterView.xaml",
                "Views/Personal/PersonalProformaInvoiceView.xaml",
                "Views/Personal/PersonalSalesReportView.xaml",
                "Views/Personal/PersonalSettingsView.xaml",
                "Views/Personal/PersonalSheetStoreView.xaml",
                "Views/Personal/PersonalTaxInvoiceDashboardView.xaml"
            };
            foreach (string path in views)
            {
                string xaml = Read(path);
                Assert.DoesNotContain("IndigoStudioResources.xaml", xaml, StringComparison.Ordinal);
                Assert.DoesNotContain("Ov.Canvas", xaml, StringComparison.Ordinal);
                Assert.Contains("GlassBlush.Background", xaml, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Legacy_indigo_component_palette_is_published_by_the_nine_family_engine()
        {
            string engine = Read("Services/Theme/Engine/ProGlassThemeEngine.cs");
            string bridge = Read("Services/Theme/Engine/ProGlassThemeEngine.Indigo.cs");
            string components = Read("Views/Dashboard/Overview/IndigoStudioResources.xaml");
            Assert.Contains("PublishIndigoAliases(resources)", engine, StringComparison.Ordinal);
            Assert.Contains("CopyBrush(resources, \"Ov.Canvas\", \"SurfaceWorkspaceBrush\"", bridge, StringComparison.Ordinal);
            Assert.Contains("CopyBrush(resources, \"Ov.Surface\", \"ThemeCardFaceBrush\"", bridge, StringComparison.Ordinal);
            Assert.Contains("CopyBrush(resources, \"Ov.Accent\", \"AccentBrush\"", bridge, StringComparison.Ordinal);
            Assert.Contains("CopyBrush(resources, \"Ov.SuccessSoft\", \"TintGreenBrush\"", bridge, StringComparison.Ordinal);
            Assert.DoesNotContain("x:Key=\"Ov.Canvas\"", components, StringComparison.Ordinal);
            Assert.DoesNotContain("x:Key=\"Ov.Accent\"", components, StringComparison.Ordinal);
            Assert.DoesNotContain("#F3F0FA", components, StringComparison.Ordinal);
        }

        [Fact]
        public void About_module_legacy_palette_is_published_by_the_nine_family_engine()
        {
            string engine = Read("Services/Theme/Engine/ProGlassThemeEngine.cs");
            string bridge = Read("Services/Theme/Engine/ProGlassThemeEngine.About.cs");
            string styles = Read("Views/About/AboutStyles.xaml");
            string view = Read("Views/About/AboutView.xaml");
            Assert.Contains("PublishAboutAliases(resources)", engine, StringComparison.Ordinal);
            Assert.Contains("Ios26AccentBrush", bridge, StringComparison.Ordinal);
            Assert.Contains("Ios26TextTitleBrush", bridge, StringComparison.Ordinal);
            Assert.Contains("Ios26CanvasBrush", bridge, StringComparison.Ordinal);
            Assert.Contains("Ios26SuccessBrush", bridge, StringComparison.Ordinal);
            Assert.DoesNotContain("IndigoStudioResources.xaml", styles, StringComparison.Ordinal);
            Assert.Contains("DynamicResource SurfaceWorkspaceBrush", view, StringComparison.Ordinal);
        }

        [Fact]
        public void Statement_and_order_report_workspaces_use_shared_theme_canvas()
        {
            string statement = Read("Views/Reports/StatementModule/StatementModuleView.xaml");
            string orderReport = Read("Views/Reports/OrderReport/OrderReportModuleView.xaml");
            foreach (string xaml in new[] { statement, orderReport })
            {
                Assert.DoesNotContain("IndigoStudioResources.xaml", xaml, StringComparison.Ordinal);
                Assert.DoesNotContain("Ov.Canvas", xaml, StringComparison.Ordinal);
                Assert.Contains("DynamicResource SurfaceWorkspaceBrush", xaml, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Tax_invoice_and_order_report_generator_pages_use_theme_workspace_roots()
        {
            string[] paths =
            {
                "Views/Reports/TaxInvoiceReport/TaxInvoiceReportView.xaml",
                "Views/Reports/OrderReport/OrderReportGeneratorView.xaml",
                "Views/Reports/OrderReport/OrderReportListView.xaml"
            };
            foreach (string path in paths)
            {
                string xaml = Read(path);
                Assert.Contains("DynamicResource SurfaceWorkspaceBrush", xaml, StringComparison.Ordinal);
                Assert.DoesNotContain("DynamicResource Ov.Canvas", xaml, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Indigo_dashboard_component_library_does_not_shadow_engine_semantic_brushes()
        {
            string engine = Read("Services/Theme/Engine/ProGlassThemeEngine.Indigo.cs");
            string dashboard = Read("Views/Dashboard/IndigoDashboardResources.xaml");
            Assert.Contains("CopyBrush(resources, \"InfoBrush\", \"AccentBrush\"", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("x:Key=\"AccentBrush\"", dashboard, StringComparison.Ordinal);
            Assert.DoesNotContain("x:Key=\"SuccessBrush\"", dashboard, StringComparison.Ordinal);
            Assert.DoesNotContain("x:Key=\"PrimaryForegroundBrush\"", dashboard, StringComparison.Ordinal);
            Assert.DoesNotContain("x:Key=\"PopupSurfaceBrush\"", dashboard, StringComparison.Ordinal);
            Assert.Contains("DynamicResource Ov.Accent", dashboard, StringComparison.Ordinal);
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
