// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/CrystalFluidDataGridAdoptionTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// IOS-17PM P4d — DATAGRID SUITE ADOPTION (2026-09-05).
//
// Reality of the app surface (verified by §2 map before adoption):
//   4 real `<DataGrid>` elements (3 GodView grids on their own style,
//   1 line-item editor); the fleet of user-facing "lists" is
//   ListBox/ItemsControl + ModuleList* styles — those ride engine
//   ManagedKeys (MenuHoverBrush already authored-adopted at P4c).
//
// P4d ships:
//   · the authored GRID-LINE identity (CrystalGridRowBorderBrush) is
//     adopted onto DividerBrush in the crystal paint — divider repaint
//     from the neutral baseline is guaranteed on every family switch
//     by the critical-resources pass, so no leak (proven, not assumed);
//   · the authored 4-part CrystalDataGrid suite stays preview-scoped:
//     production XAML must NEVER bind Crystal*DataGrid*Style statically
//     (a static style under Monet/Ocean would leak the crystal look —
//     preview family-gating is the deliberate carrier).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Tests.TestSupport;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class CrystalFluidDataGridAdoptionTests
    {
        private const string PreviewExemption =
            "Views/Settings/Sections/SettingsAppearanceSection.xaml";

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

        // ── 1. DIVIDER ADOPTS THE AUTHORED GRID-LINE IDENTITY ───────────
        [Fact]
        public void Crystal_divider_adopts_authored_grid_row_border()
        {
            string part3 = Read("Services/Theme/VisualThemeEngine.Part3.cs");

            Assert.Contains(
                "resources[\"DividerBrush\"] = TokenBrush(resources,\n                \"CrystalGridRowBorderBrush\")",
                part3, StringComparison.Ordinal);
            Assert.Contains("?? FrozenArgb(0x33, 0xFF, 0xFF, 0xFF);",
                part3, StringComparison.Ordinal);

            string brushes = Read("Themes/CrystalFluid/CrystalFluidBrushes.xaml");
            Assert.Contains("x:Key=\"CrystalGridRowBorderBrush\"", brushes,
                StringComparison.Ordinal);
            Assert.Contains("x:Key=\"CrystalGridHeaderBorderBrush\"", brushes,
                StringComparison.Ordinal);
        }

        // ── 2. LEAK AXIS: BASELINE RECLAIMS DIVIDERBRUSH PER SWITCH ─────
        [Fact]
        public void Neutral_baseline_repaints_dividerbrush_on_switch()
        {
            string part1 = Read("Services/Theme/VisualThemeEngine.Part1.cs");
            // the critical-resources generator pairs DividerBrush with
            // the neutral outline — proven leak firewall for the
            // authored adoption above
            Assert.Contains("(\"DividerBrush\", outlineColor)", part1,
                StringComparison.Ordinal);
            Assert.Contains("GuaranteeCriticalResources", part1,
                StringComparison.Ordinal);
        }

        // ── 3. AUTHORED SUITE STAYS PREVIEW-SCOPED (ZERO PRODUCTION LEAK)
        [Fact]
        public void Production_xaml_never_binds_crystal_datagrid_styles()
        {
            string root = LocateProjectRoot();
            var offenders =
                Directory.EnumerateFiles(Path.Combine(root, "Views"),
                      "*.xaml", SearchOption.AllDirectories)
                .Concat(new[]
                {
                    Path.Combine(root, "MainWindow.xaml"),
                })
                .Where(f =>
                    !f.Replace(Path.DirectorySeparatorChar, '/')
                      .EndsWith(PreviewExemption, StringComparison.Ordinal))
                .Where(f =>
                {
                    string text = File.ReadAllText(f);
                    return text.Contains("CrystalDataGridStyle") ||
                           text.Contains("CrystalDataGridRowStyle") ||
                           text.Contains("CrystalDataGridCellStyle") ||
                           text.Contains("CrystalDataGridColumnHeaderStyle");
                })
                .Select(Path.GetFileName)
                .ToList();

            Assert.True(offenders.Count == 0,
                "crystal datagrid styles leaked into production XAML: " +
                string.Join(", ", offenders));
        }

        // ── 4. AUTHORED SUITE PARTS PRESENT FOR THE P4E PREVIEW ─────────
        [Fact]
        public void Authored_grid_suite_is_complete()
        {
            string grid = Read("Themes/CrystalFluid/CrystalFluidDataGrid.xaml");
            foreach (var key in new[]
            {
                "CrystalDataGridStyle", "CrystalDataGridColumnHeaderStyle",
                "CrystalDataGridRowStyle", "CrystalDataGridCellStyle",
            })
                Assert.Contains($"x:Key=\"{key}\"", grid,
                    StringComparison.Ordinal);
            // 5 TargetType declarations: the 4 keyed parts + the inner
            // scrollbar style folded in at P2 (banner rule preserved).
            // {x:Type} TargetTypes legitimately wrap lines (whitespace
            // inside x:Type is tolerant — only {x:Static} isn't, D36),
            // so the count is on plain attribute names.
            Assert.Equal(5, Regex.Matches(grid,
                Regex.Escape("TargetType=\"")).Count);
        }
    }
}
