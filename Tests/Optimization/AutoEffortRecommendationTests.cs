using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

public class AutoEffortRecommendationTests
{
    [Theory]
    [InlineData(16, 1, false, 5)]
    [InlineData(200, 1, false, 5)]
    [InlineData(201, 1, false, 3)]
    [InlineData(800, 2, false, 4)]
    [InlineData(800, 1, true, 4)]
    [InlineData(2000, 1, false, 3)]
    [InlineData(2001, 1, false, 4)]
    [InlineData(8000, 1, true, 4)]
    [InlineData(15001, 1, false, 3)]
    [InlineData(15001, 1, true, 4)]
    [InlineData(100000, 3, true, 3)]
    public void Auto_calls_an_existing_stage_and_does_not_pick_a_plate_pattern(
        int pieces, int stockSizes, bool finite, int expectedStage)
    {
        var choice = OptimizationSearchGovernor.RecommendEffort(pieces, stockSizes, finite);
        Assert.InRange(choice.Stage, 3, 5);
        Assert.Equal(expectedStage, choice.Stage);
        Assert.False(string.IsNullOrWhiteSpace(choice.Reason));
    }
}
