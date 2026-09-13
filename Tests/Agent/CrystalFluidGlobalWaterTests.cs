// Crystal / liquid live water retired. Pins that auto-attach is off
// and the painter never publishes CrystalWaterDefaultEnabled = true.

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
        public void Painter_never_enables_live_water()
        {
            string engine = Read("Services/Theme/VisualThemeEngine.Part3.cs");
            Assert.Contains("CrystalWaterDefaultEnabled", engine, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "CrystalWaterDefaultEnabled\"] = true",
                engine, StringComparison.Ordinal);
        }
    }
}
