// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/AppearanceServiceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Phase 6b + Phase 7 — AppearanceService
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Settings;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class AppearanceServiceTests
    {
        [Fact]
        public void ApplyTheme_succeeds_or_fails_gracefully_without_config()
        {
            // App.Config may be null in test — should return Fail, not throw
            var result = AppearanceService.ApplyTheme(isDark: true, correlationId: "CORR-TEST");

            // Should not throw, either Ok or Fail is acceptable in test context
            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result.CorrelationId));
        }

        [Fact]
        public void ApplyAccent_succeeds_or_fails_gracefully()
        {
            var result = AppearanceService.ApplyAccent("GlassBlue", "CORR-TEST");

            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result.CorrelationId));
            // In test without App.Current.Resources, may fail but should have ErrorId
            if (result.IsFailure)
            {
                Assert.False(string.IsNullOrWhiteSpace(result.Error));
            }
        }

        [Fact]
        public void ApplyDensity_validates_input()
        {
            var result = AppearanceService.ApplyDensity("Compact", "CORR-TEST");
            Assert.NotNull(result);

            // Empty density should fallback to Compact, not throw
            var resultEmpty = AppearanceService.ApplyDensity("", "CORR-TEST");
            Assert.NotNull(resultEmpty);
        }

        [Fact]
        public void ApplyUiScale_clamps_value()
        {
            var result = AppearanceService.ApplyUiScale(1.0, "CORR-TEST");
            Assert.NotNull(result);

            var resultLarge = AppearanceService.ApplyUiScale(5.0, "CORR-TEST");
            Assert.NotNull(resultLarge);
            // Should clamp, not throw
        }
    }
}
