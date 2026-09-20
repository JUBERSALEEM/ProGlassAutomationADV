// Live water auto-attach stays retired (no class-handler spray).
// The Crystal Fluid painter MUST publish CrystalWaterDefaultEnabled
// = true — that gate is what makes iPhone 17 Pro Max distinct from
// iOS 26. Non-crystal families still seed FALSE in Part1.

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class CrystalFluidGlobalWaterTests
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

        [Fact]
        public void Water_auto_attach_is_retired()
        {
            string global = Read("Controls/CrystalFluid/CrystalWaterGlobal.cs");
            Assert.Contains("Live water auto-attach retired",
                global, StringComparison.Ordinal);
            Assert.DoesNotContain("RegisterClassHandler",
                global, StringComparison.Ordinal);

            string startup = Read("App.StartupCore.cs");
            Assert.Contains("CrystalWaterGlobal.Install();",
                startup, StringComparison.Ordinal);
        }

        [Fact]
        public void Painter_enables_live_water_for_crystal_only()
        {
            string part3 = Read("Services/Theme/VisualThemeEngine.Part3.cs");
            Assert.Contains(
                "CrystalWaterDefaultEnabled\"] = true",
                part3, StringComparison.Ordinal);

            string part1 = Read("Services/Theme/VisualThemeEngine.Part1.cs");
            Assert.Contains(
                "CrystalWaterDefaultEnabled\"] = false",
                part1, StringComparison.Ordinal);

            string hub = Read("Services/Theme/Engine/ProGlassThemeEngine.cs");
            Assert.Contains("Family == ProGlassThemeFamily.Crystal",
                hub, StringComparison.Ordinal);
        }
    }
}
