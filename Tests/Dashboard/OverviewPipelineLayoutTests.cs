// Pins the Overview pipeline stage layout: stage name sits in column 1
// so it cannot paint over the 38px glyph. Source-level — no SDK.

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Dashboard
{
    public sealed class OverviewPipelineLayoutTests
    {
        [Fact]
        public void Pipeline_stage_name_is_in_column_one_not_on_the_icon()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Themes", "Professional", "ProfessionalCore.xaml"));

            int start = src.IndexOf(
                "x:Key=\"PgPipelineStageTemplate\"", StringComparison.Ordinal);
            Assert.True(start >= 0, "PgPipelineStageTemplate missing");
            int end = src.IndexOf("</DataTemplate>", start, StringComparison.Ordinal);
            string tmpl = src.Substring(start, end - start);

            Assert.Contains("Text=\"{Binding Name}\"", tmpl);
            int nameAt = tmpl.IndexOf("Text=\"{Binding Name}\"", StringComparison.Ordinal);
            int open = tmpl.LastIndexOf("<TextBlock", nameAt, StringComparison.Ordinal);
            Assert.True(open >= 0, "stage Name TextBlock not found");
            string nameTag = tmpl.Substring(open, nameAt - open);
            Assert.Contains("Grid.Column=\"1\"", nameTag);
        }

        [Fact]
        public void Indigo_stage_keeps_name_beside_glyph_and_binds_all_stage_details()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Views", "Dashboard", "Overview", "IndigoStudioResources.xaml"));
            int start = src.IndexOf("x:Key=\"Ov.StageTemplate\"", StringComparison.Ordinal);
            Assert.True(start >= 0);
            int end = src.IndexOf("</DataTemplate>", start, StringComparison.Ordinal);
            string template = src.Substring(start, end - start);
            Assert.Contains("<DockPanel", template);
            foreach (string property in new[] { "Name", "Glyph", "ValueDisplay", "Foot", "IsFirst", "IsActive", "AccentKey" })
                Assert.Contains("{Binding " + property, template);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Themes", "Professional", "ProfessionalCore.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
