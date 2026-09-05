// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/CrystalFluidPreviewAdoptionTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// IOS-17PM P4e — PREVIEW PAGE (SETTINGS) REBUILT ON FILE-20 (2026-09-05).
//
// The settings appearance preview hosts the authored file-20
// composition — environment highlight trio + ambient, KPI strip with
// water, authored nav row, status pill + determinate progress, authored
// DataGrid chrome — family-gated by CrystalPreviewHostVisibility, which
// the crystal paint writes and BOTH family-switch paths remove (same
// proven contract as the P4a ambient host). Invariants pinned:
//   · the universal preview still hosts the authored water control
//     (P4b interim retirement carried through),
//   · indeterminate progress is untouched — the section MUST NOT gain
//     any IsIndeterminate override (stock invariant),
//   · no interim crystal-water artifacts anywhere in the tree,
//   · composition uses only authored nouns that resolve.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Tests.TestSupport;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class CrystalFluidPreviewAdoptionTests
    {
        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(LocateProjectRoot(), relative));

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

        private const string Preview =
            "Views/Settings/Sections/SettingsAppearanceSection.xaml";

        // ── 1. PREVIEW GATE: engine writes, both switch paths remove ────
        [Fact]
        public void Preview_gate_follows_the_ambient_contract()
        {
            string part3 = Read("Services/Theme/VisualThemeEngine.Part3.cs");
            string part1 = Read("Services/Theme/VisualThemeEngine.Part1.cs");

            Assert.Contains(
                "resources[\"CrystalPreviewHostVisibility\"] = Visibility.Visible;",
                part3, StringComparison.Ordinal);
            Assert.Equal(2, Regex.Matches(part1,
                Regex.Escape(
                    "resources.Remove(\"CrystalPreviewHostVisibility\");"))
                .Count);

            // not a managed key, not a studio pipeline token
            var managedKeys = (string[])typeof(VisualThemeEngine)
                .GetField("ManagedKeys",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static)!
                .GetValue(null)!;
            var tokenKeys = (string[])typeof(VisualThemeEngine)
                .GetField("CrystalFluidTokenKeys",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static)!
                .GetValue(null)!;
            Assert.DoesNotContain("CrystalPreviewHostVisibility", managedKeys);
            Assert.DoesNotContain("CrystalPreviewHostVisibility", tokenKeys);
            Assert.Equal(7, tokenKeys.Length);
        }

        // ── 2. PAGE REBUILD: composition + gate binding present ─────────
        [Fact]
        public void Preview_page_carries_the_file20_composition()
        {
            string xaml = Read(Preview);

            Assert.Contains(
                "Visibility=\"{DynamicResource CrystalPreviewHostVisibility}\"",
                xaml, StringComparison.Ordinal);
            Assert.Contains(
                "IOS-17PM P4e (2026-09-05) — AUTHORED PREVIEW", xaml,
                StringComparison.Ordinal);

            // environment trio + ambient
            Assert.Contains("CrystalEnvironmentHighlightTopLeftBrush", xaml,
                StringComparison.Ordinal);
            Assert.Contains("CrystalEnvironmentCyanTopRightBrush", xaml,
                StringComparison.Ordinal);
            Assert.Contains("CrystalEnvironmentCyanBottomRightBrush", xaml,
                StringComparison.Ordinal);
            Assert.Contains("<crystal:CrystalAmbientBackground", xaml,
                StringComparison.Ordinal);

            // KPI strip with water: exactly authored controls
            Assert.True(Regex.Matches(xaml,
                Regex.Escape("<crystal:CrystalKpiCard ")).Count >= 3);
            Assert.Contains("<crystal:CrystalStatusPill", xaml,
                StringComparison.Ordinal);

            // authored nav reference styles adopted in-place
            Assert.Contains("{StaticResource CrystalNavButtonStyle}", xaml,
                StringComparison.Ordinal);
            Assert.Contains("{StaticResource CrystalNavLabelStyle}", xaml,
                StringComparison.Ordinal);

            // authored grid suite adopted IN THE PREVIEW ONLY (matching
            // the P4d leak firewall's deliberate carrier)
            foreach (var key in new[]
            {
                "CrystalDataGridStyle", "CrystalDataGridColumnHeaderStyle",
                "CrystalDataGridRowStyle", "CrystalDataGridCellStyle",
            })
                Assert.Contains($"{{StaticResource {key}}}", xaml,
                    StringComparison.Ordinal);
        }

        // ── 3. UNIVERSAL PREVIEW NOW HOSTS THE AUTHORED WATER CONTROL ───
        [Fact]
        public void Universal_preview_water_is_authored()
        {
            string xaml = Read(Preview);
            Assert.Contains("<crystal:CrystalWaterControl", xaml,
                StringComparison.Ordinal);
            Assert.Contains(
                "WaterEnabled=\"{DynamicResource CrystalWaterDefaultEnabled}\"",
                xaml, StringComparison.Ordinal);
            Assert.Contains("WaterLevel=\"{Binding StudioWaterLevel01}\"",
                xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("<crystal:CrystalWater ", xaml,
                StringComparison.Ordinal);
        }

        // ── 4. INDETERMINATE PROGRESS STAYS STOCK (HARD INVARIANT) ──────
        [Fact]
        public void Indeterminate_progress_is_never_overridden()
        {
            // The rule: NOTHING P4e adds any indeterminate behavior; the
            // stock indicators keep rendering EXACTLY as before.
            // The section already hosts three stock
            // <ProgressBar IsIndeterminate="True"> indicators and the
            // authored crystal progress template ships its authored
            // breathing trigger for indeterminate mode (P2) — both stay
            // untouched. The pinned fact: P4e's authored tile contains
            // no IsIndeterminate anywhere, and the stock count is
            // unchanged.
            string xaml = Read(Preview);
            Assert.Equal(3, Regex.Matches(xaml,
                Regex.Escape("IsIndeterminate=\"True\"")).Count);

            int tile = xaml.IndexOf("IOS-17PM P4e (2026-09-05)", // banner head
                StringComparison.Ordinal);
            int tileEnd = xaml.IndexOf(
                "<UniformGrid Columns=\"3\" Margin=\"0,0,0,6\">",
                tile, StringComparison.Ordinal);
            Assert.True(tile > 0 && tileEnd > tile);
            string authoredTile = xaml.Substring(tile, tileEnd - tile);
            Assert.DoesNotContain("IsIndeterminate=\"", authoredTile,
                StringComparison.Ordinal);

            // and the demo progress bar is explicitly determinate
            Assert.Contains("Value=\"64\"", authoredTile,
                StringComparison.Ordinal);
            Assert.Contains("Maximum=\"100\"", authoredTile,
                StringComparison.Ordinal);
        }

        // ── 5. EVERY PRIMITIVE USED RESOLVES IN THE PLANE ───────────────
        [Fact]
        public void Preview_static_resources_resolve_in_the_plane()
        {
            string plane =
                Read("Themes/CrystalFluid/CrystalFluidColors.xaml") +
                Read("Themes/CrystalFluid/CrystalFluidBrushes.xaml") +
                Read("Themes/CrystalFluid/CrystalFluidMetrics.xaml") +
                Read("Themes/CrystalFluid/CrystalFluidTokens.xaml") +
                Read("Themes/CrystalFluid/CrystalFluidControls.xaml") +
                Read("Themes/CrystalFluid/CrystalFluidDataGrid.xaml") +
                Read("Themes/CrystalFluid/CrystalFluidNavigation.xaml") +
                Read("Themes/CrystalFluid/CrystalFluidStyles.xaml") +
                Read("Themes/CrystalFluid/CrystalFluidScrollBar.xaml");

            string xaml = Read(Preview);
            foreach (var key in new[]
            {
                "CrystalEnvironmentHighlightTopLeftBrush",
                "CrystalEnvironmentCyanTopRightBrush",
                "CrystalEnvironmentCyanBottomRightBrush",
                "CrystalPositiveBrush", "CrystalNavButtonStyle",
                "CrystalNavLabelStyle", "CrystalDataGridStyle",
                "CrystalDataGridColumnHeaderStyle",
                "CrystalDataGridRowStyle", "CrystalDataGridCellStyle",
            })
                Assert.Contains($"x:Key=\"{key}\"", plane,
                    StringComparison.Ordinal);
        }
    }
}
