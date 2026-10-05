// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/StatementLogHelperTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins StatementLogHelper (InferLogSeverity + IsImageFileName) extracted
// from the StatementGeneratorViewModel.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Statement.Workflow;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class StatementLogHelperTests
    {
        [Theory]
        [InlineData("Loaded PI ✅", LogSeverity.Success)]
        [InlineData("✓ done", LogSeverity.Success)]
        [InlineData("Failed ❌", LogSeverity.Error)]
        [InlineData("Missing ✗", LogSeverity.Error)]
        [InlineData("Warning ⚠", LogSeverity.Warning)]
        [InlineData("plain line", LogSeverity.Info)]
        [InlineData("", LogSeverity.Info)]
        [InlineData(null, LogSeverity.Info)]
        public void InferLogSeverity_maps_glyphs(string? line, LogSeverity expected)
        {
            Assert.Equal(expected, StatementLogHelper.InferLogSeverity(line!));
        }

        [Theory]
        [InlineData("photo.png", true)]
        [InlineData("IMG_01.JPG", true)]
        [InlineData("scan.jpeg", true)]
        [InlineData("pic.bmp", true)]
        [InlineData("anim.gif", true)]
        [InlineData("doc.pdf", false)]
        [InlineData("data.xlsx", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsImageFileName_matches_raster_extensions(
            string? name, bool expected)
        {
            Assert.Equal(expected, StatementLogHelper.IsImageFileName(name!));
        }
    }
}
