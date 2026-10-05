// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/CrystalFluidShellAdoptionTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// IOS-17PM P4a — SHELL WALLPAPER ADOPTION (2026-09-05).
//
// Two pinned deliverables:
//   A) TOKENIZED SCENE GRADIENT — the authored §22 environment colours
//      live exactly once, in CrystalFluidBrushes.xaml; the engine stage
//      (Part3) and the wallpaper scene (BuiltinWallpaperScenes)
//      CONSUME those tokens with byte-exact literal fallbacks, so the
//      render can never drift from the authored palette by accident
//      and can never break in hosts without merged resources.
//   B) FAMILY-GATED AMBIENT HOST — MainWindow hosts the authored
//      CrystalAmbientBackground behind every shell row; the engine
//      writes CrystalAmbientHostVisibility = Visible for Crystal
//      Fluid and BOTH family-switch paths remove the key, so ambient
//      clocks (D35-hardened to respect collapse) can never leak into
//      another family.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Tests.TestSupport;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class CrystalFluidShellAdoptionTests
    {
        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(LocateProjectRoot(),
                relative.Replace('/', Path.DirectorySeparatorChar)));

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new FileNotFoundException("Project root not found");
        }

        // ── 1. AUTHORED NUMBER HOME: the tokens exist with exact values ─
        [Fact]
        public void Stage_and_scene_tokens_carry_the_authored_palette()
        {
            string brushes = Read("Themes/CrystalFluid/CrystalFluidBrushes.xaml");

            foreach (var (key, hex) in new[]
            {
                ("CrystalStageTopBrush",         "#EEF6FF"),
                ("CrystalStageLightBrush",       "#DBEDFF"),
                ("CrystalStageBaseBrush",        "#C6E4FF"),
                ("CrystalSceneTopBrush",         "#FDFEFF"),
                ("CrystalSceneLitBrush",         "#F2FAFE"),
                ("CrystalSceneAtmosphereBrush",  "#E6F6FD"),
                ("CrystalSceneBaseBrush",        "#D5F0FB"),
                ("CrystalSceneFloorBrush",       "#DFF5FF"),
            })
            {
                Assert.Contains($"x:Key=\"{key}\"", brushes,
                    StringComparison.Ordinal);
                Assert.Contains($"Color=\"{hex}\"", brushes,
                    StringComparison.Ordinal);
            }
        }

        // ── 2. SINGLE-HOME: the tokens did NOT migrate to the pipeline ──
        [Fact]
        public void Scene_tokens_stay_out_of_the_pipeline_tokens_file()
        {
            string tokens = Read("Themes/CrystalFluid/CrystalFluidTokens.xaml");
            foreach (var k in new[]
            {
                "CrystalStageTopBrush", "CrystalSceneTopBrush",
                "CrystalSceneFloorBrush",
            })
                Assert.DoesNotContain($"x:Key=\"{k}\"", tokens,
                    StringComparison.Ordinal);
        }

        // ── 3. ENGINE CONSUMES TOKENS (with byte-exact fallbacks) ───────
        [Fact]
        public void Engine_stage_reads_tokens_with_literal_fallbacks()
        {
            string part3 = Read("Services/Theme/VisualThemeEngine.Part3.cs");

            // helper present
            Assert.Contains("private static Color TokenColor(", part3,
                StringComparison.Ordinal);

            // every stage stop reads through the token helper
            foreach (var token in new[]
            {
                "CrystalStageTopBrush", "CrystalStageLightBrush",
                "CrystalStageBaseBrush",
            })
                Assert.Contains($"TokenColor(resources, \"{token}\",", part3,
                    StringComparison.Ordinal);

            // the authored fallback literals remain byte-exact
            Assert.Contains("Color.FromRgb(0xEE, 0xF6, 0xFF)", part3,
                StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0xDB, 0xED, 0xFF)", part3,
                StringComparison.Ordinal);
            Assert.Contains("Color.FromRgb(0xC6, 0xE4, 0xFF)", part3,
                StringComparison.Ordinal);

            // the stage still lands on the same pipeline key
            Assert.Contains("resources[\"WindowBackgroundBrush\"] = stage;",
                part3, StringComparison.Ordinal);
        }

        // ── 4. WALLPAPER SCENE CONSUMES TOKENS (5 stops) ────────────────
        [Fact]
        public void Wallpaper_scene_reads_five_token_stops()
        {
            string scenes = Read("Services/Theme/BuiltinWallpaperScenes.cs");

            Assert.Equal(5, Regex.Matches(scenes,
                "SceneToken(\"Crystal").Count);
            foreach (var token in new[]
            {
                "CrystalSceneTopBrush", "CrystalSceneLitBrush",
                "CrystalSceneAtmosphereBrush", "CrystalSceneBaseBrush",
                "CrystalSceneFloorBrush",
            })
                Assert.Contains($"SceneToken(\"{token}\"", scenes,
                    StringComparison.Ordinal);

            // authored literals remain the fallbacks (render equivalence)
            foreach (var hex in new[]
                { "#FDFEFF", "#F2FAFE", "#E6F6FD", "#D5F0FB" })
                Assert.Contains($"\"{hex}\"", scenes, StringComparison.Ordinal);

            Assert.Contains(
                "private static System.Windows.Media.Color SceneToken(",
                scenes, StringComparison.Ordinal);
        }

        // ── 5. FAMILY GATE: engine writes the ambient visibility ────────
        [Fact]
        public void Crystal_paint_publishes_the_ambient_visibility_gate()
        {
            string part3 = Read("Services/Theme/VisualThemeEngine.Part3.cs");
            Assert.Contains(
                "resources[\"CrystalAmbientHostVisibility\"] = Visibility.Visible;",
                part3, StringComparison.Ordinal);
        }

        // ── 6. FAMILY GATE: BOTH switch paths remove the key ────────────
        [Fact]
        public void Both_family_switch_paths_remove_the_gate()
        {
            string part1 = Read("Services/Theme/VisualThemeEngine.Part1.cs");
            Assert.Equal(2, Regex.Matches(part1,
                Regex.Escape("resources.Remove(\"CrystalAmbientHostVisibility\");"))
                .Count);
            // it is NOT a pipeline token — keep CrystalFluidTokenKeys at 7
            var field = typeof(VisualThemeEngine).GetField(
                "CrystalFluidTokenKeys",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);
            var keys = (string[])field!.GetValue(null)!;
            Assert.Equal(7, keys.Length);
            Assert.DoesNotContain("CrystalAmbientHostVisibility", keys);
        }

        // ── 7. MAINWINDOW HOSTS THE LAYER BEHIND EVERY SHELL ROW ────────
        [Fact]
        public void MainWindow_hosts_the_ambient_background()
        {
            string main = Read("MainWindow.xaml");

            Assert.Contains(
                "xmlns:crystal=\"clr-namespace:ProGlassAutomation.Controls.CrystalFluid\"",
                main, StringComparison.Ordinal);
            Assert.Contains("<crystal:CrystalAmbientBackground", main,
                StringComparison.Ordinal);
            Assert.Contains(
                "Visibility=\"{DynamicResource CrystalAmbientHostVisibility}\"",
                main, StringComparison.Ordinal);
            Assert.Contains("Grid.RowSpan=\"2\"", main,
                StringComparison.Ordinal);

            // back-most child: the host must precede the first shell row
            // content (top panel border) in document order
            int host = main.IndexOf("<crystal:CrystalAmbientBackground",
                StringComparison.Ordinal);
            int topPanel = main.IndexOf("<Border Grid.Row=\"0\"",
                StringComparison.Ordinal);
            Assert.True(host > 0 && topPanel > 0 && host < topPanel,
                "ambient host must be the BACK-most child of the root grid");
        }

        // ── 8. D35: collapsed hosts hold no clocks ──────────────────────
        [Fact]
        public void Ambient_control_stops_clocks_when_collapsed()
        {
            string ambient = Read(
                "Controls/CrystalFluid/Controls/CrystalAmbientBackground.cs");

            Assert.Contains("IsVisibleChanged += OnIsVisibleChanged;",
                ambient, StringComparison.Ordinal);
            Assert.Contains("StopAnimation();", ambient,
                StringComparison.Ordinal);
            // resume path exists for becoming visible again
            Assert.Contains("(bool)e.NewValue)", ambient,
                StringComparison.Ordinal);
            // hard guard inside StartAnimation itself
            Assert.Contains("if (!IsVisible || ReducedMotion || _blob1 == null",
                ambient, StringComparison.Ordinal);
            // register marker
            Assert.Contains("D35", ambient, StringComparison.Ordinal);
        }
    }
}
