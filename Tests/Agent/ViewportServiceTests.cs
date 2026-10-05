using ProGlassAutomation.Services.Viewport;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class ViewportServiceTests
    {
        [Theory]
        [InlineData("Comfortable", 700, "Compact")]
        [InlineData("Comfortable", 820, "Normal")]
        [InlineData("Compact", 1200, "Compact")]
        [InlineData("Normal", 1200, "Normal")]
        [InlineData("Comfortable", 1100, "Comfortable")]
        public void Density_only_downgrades_on_short_screens(
            string preferred, double workHeight, string expected)
        {
            Assert.Equal(expected, ViewportService.RecommendDensity(preferred, workHeight));
        }

        [Fact]
        public void Ui_scale_clamps_and_snaps()
        {
            Assert.Equal(0.80, ViewportService.ClampUiScale(0.5));
            Assert.Equal(1.25, ViewportService.ClampUiScale(2.0));
            Assert.Equal(1.00, ViewportService.ClampUiScale(1.02));
            Assert.Equal(0.90, ViewportService.ClampUiScale(0.88));
        }
    }
}
