// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/CrystalFluidStatCardAdoptionTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// IOS-17PM P4b — STATCARD RE-SKIN + TOMBSTONE (2026-09-05).
//
// The shared §11 StatCard now hosts the AUTHORED CrystalWaterControl
// (the interim Controls/CrystalFluid/CrystalWater.xaml(+cs) and its
// three transitional token brushes are retired), and wears the authored
// KPI-card chrome (glass surface + crystal border + water-weight
// shadow) through a trigger family-gated on WaterEnabled — every KPI
// host binds CrystalWaterDefaultEnabled, False under every non-crystal
// family, so non-crystal cards stay pixel-identical.
//
// Pinned contracts:
//   · StatCard's public API is unchanged (DP-for-DP),
//   · the interim type is gone and unresolvable,
//   · the authored water layer carries D5/D6/D9/D35-class lifecycle
//     fixes into the KPI plane,
//   · the chrome trigger sits BEFORE the state triggers (state tints
//     keep winning),
//   · tokens file carries no transitional brushes any more.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Controls.StatCard;
using ProGlassAutomation.Tests.TestSupport;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class CrystalFluidStatCardAdoptionTests
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

        // ── 1. STATCARD HOSTS THE AUTHORED WATER CONTROL ────────────────
        [Fact]
        public void Statcard_hosts_the_authored_water_control()
        {
            string xaml = Read("Controls/StatCard/StatCardControl.xaml");

            Assert.Contains("<crystal:CrystalWaterControl", xaml,
                StringComparison.Ordinal);
            Assert.DoesNotContain("<crystal:CrystalWater\n", xaml,
                StringComparison.Ordinal);
            Assert.DoesNotContain("<crystal:CrystalWater ", xaml,
                StringComparison.Ordinal);

            // DP-for-DP wiring carried over: family gate + semantic level
            // + host corner radius; the water plane cannot catch input.
            Assert.Contains(
                "WaterEnabled=\"{Binding WaterEnabled, ElementName=Root}\"",
                xaml, StringComparison.Ordinal);
            Assert.Contains(
                "WaterLevel=\"{Binding WaterLevel, ElementName=Root}\"",
                xaml, StringComparison.Ordinal);
            Assert.Contains(
                "CornerRadius=\"{DynamicResource CardCornerRadiusLarge}\"",
                xaml, StringComparison.Ordinal);
            Assert.Contains("IsHitTestVisible=\"False\"", xaml,
                StringComparison.Ordinal);
        }

        // ── 2. PUBLIC API IS UNCHANGED (STABLE SURFACE RULE) ────────────
        [Fact]
        public void Statcard_public_api_is_unchanged()
        {
            foreach (var dp in new[]
            {
                ("WaterEnabled", typeof(bool)),
                ("WaterLevel",   typeof(double)),
                ("Icon",         typeof(string)),
                ("Title",        typeof(string)),
                ("Value",        typeof(string)),
                ("Unit",         typeof(string)),
                ("TrendText",    typeof(string)),
            })
            {
                var prop = typeof(StatCardControl).GetProperty(dp.Item1);
                Assert.NotNull(prop);
                Assert.Equal(dp.Item2, prop!.PropertyType);
            }

            // the steady-state defaults that keep non-water cards
            // pixel-identical are still the authored ambience values
            string cs = Read("Controls/StatCard/StatCardControl.xaml.cs");
            Assert.Contains("new PropertyMetadata(false)", cs,
                StringComparison.Ordinal);
            Assert.Contains("new PropertyMetadata(0.30, null,", cs,
                StringComparison.Ordinal);
        }

        // ── 3. FAMILY-GATED AUTHORED KPI CHROME ─────────────────────────
        [Fact]
        public void Authored_chrome_is_gated_before_the_state_triggers()
        {
            string xaml = Read("Controls/StatCard/StatCardControl.xaml");

            int chrome = xaml.IndexOf("IOS-17PM P4b (2026-09-05): AUTHORED KPI CHROME",
                StringComparison.Ordinal);
            Assert.True(chrome > 0, "chrome trigger banner present");
            int stateTrigger = xaml.IndexOf("<!-- State tints -->",
                StringComparison.Ordinal);
            Assert.True(stateTrigger > chrome,
                "chrome trigger precedes state tints → states still win");

            // chrome adopts authored nouns by DynamicResource (engine live)
            Assert.Contains("Value=\"{DynamicResource CrystalGlassSurfaceBrush}\"",
                xaml, StringComparison.Ordinal);
            Assert.Contains("Value=\"{DynamicResource CrystalGlassBorderBrush}\"",
                xaml, StringComparison.Ordinal);
            Assert.Contains("Value=\"{DynamicResource CrystalBorderThickness}\"",
                xaml, StringComparison.Ordinal);
            Assert.Contains("Color=\"{DynamicResource CrystalShadowColor}\"",
                xaml, StringComparison.Ordinal);

            // and it is gated on the WaterEnabled plane, not a style tweak
            int gate = xaml.IndexOf("Binding=\"{Binding WaterEnabled,",
                chrome, StringComparison.Ordinal);
            Assert.True(gate > 0, "chrome trigger binds WaterEnabled");
            string around = xaml.Substring(gate,
                System.Math.Min(220, xaml.Length - gate));
            Assert.Contains("ElementName=Root}\" Value=\"True\"", around,
                StringComparison.Ordinal);
            // and it precedes the state triggers (checked above too):
            Assert.True(gate < stateTrigger);
        }

        // ── 4. INTERIM CONTROL IS GONE AND UNRESOLVABLE ─────────────────
        [Fact]
        public void Interim_crystalwater_is_deleted_everywhere()
        {
            Assert.False(File.Exists(Path.Combine(LocateProjectRoot(),
                "Controls/CrystalFluid/CrystalWater.xaml")));
            Assert.False(File.Exists(Path.Combine(LocateProjectRoot(),
                "Controls/CrystalFluid/CrystalWater.xaml.cs")));

            // the CLR type is gone — ProGlassAutomation.Controls.CrystalFluid.CrystalWater?
            // must not resolve in the app assembly
            Assert.Null(Type.GetType(
                "ProGlassAutomation.Controls.CrystalFluid.CrystalWater, ProGlassAutomation"));

            string tokens = Read("Themes/CrystalFluid/CrystalFluidTokens.xaml");
            foreach (var k in new[]
            {
                "CrystalWaterBrush", "CrystalWaterSurfaceBrush",
                "CrystalBubbleBrush",
            })
                Assert.DoesNotContain($"x:Key=\"{k}\"", tokens,
                    StringComparison.Ordinal);

            // no XAML consumer anywhere in the shipped tree
            string mw = Read("MainWindow.xaml");
            Assert.DoesNotContain("CrystalWaterBrush", mw,
                StringComparison.Ordinal);
            Assert.DoesNotContain("CrystalWaterSurfaceBrush", mw,
                StringComparison.Ordinal);
            Assert.DoesNotContain("CrystalBubbleBrush\"", mw,
                StringComparison.Ordinal);
        }
    }
}
