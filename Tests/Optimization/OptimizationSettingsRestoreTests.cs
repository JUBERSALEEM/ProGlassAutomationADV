using System.Reflection;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.ViewModels.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

public sealed class OptimizationSettingsRestoreTests
{
    private static void Add(OptimizationViewModel vm, int thickness)
    {
        vm.NewPiece.GlassRef = $"{thickness}mm Clear FT Glass";
        vm.NewPiece.WidthText = "1000";
        vm.NewPiece.HeightText = "800";
        vm.NewPiece.QtyText = "2";
        vm.AddPieceCommand.Execute(null);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(12)]
    public void Imported_thickness_sets_correct_top_bottom_and_breakout(int thickness)
    {
        using var vm = new OptimizationViewModel(true);
        Add(vm, thickness);
        Assert.Equal(thickness.ToString(), vm.ThicknessText);
        Assert.Equal("50", vm.LeftMarginText);
        Assert.Equal("50", vm.RightMarginText);
        Assert.Equal("0", vm.TopMarginText);
        Assert.Equal("50", vm.BottomMarginText);
        Assert.Equal("50", vm.MinBreakoutText);
        Assert.True(vm.HasBreakoutChoice);
        vm.SelectedBreakoutOption = "40";
        Assert.Equal("40", vm.MinBreakoutText);
        Assert.Contains("TM 0", vm.MarginHint);
    }

    [Fact]
    public void Group_selection_tracks_specs_but_does_not_erase_custom_trims()
    {
        using var vm = new OptimizationViewModel(true);
        Add(vm, 6);
        Add(vm, 12);
        vm.SelectedCuttingGroup = "12mm Clear FT Glass";
        Assert.Equal("12", vm.ThicknessText);
        Assert.Equal("50", vm.LeftMarginText);
        vm.SelectedCuttingGroup = "6mm Clear FT Glass";
        Assert.Equal("15", vm.LeftMarginText);
        vm.LeftMarginText = "23";
        vm.SelectedCuttingGroup = "12mm Clear FT Glass";
        Assert.Equal("12", vm.ThicknessText);
        Assert.Equal("23", vm.LeftMarginText);
        vm.LoadHouseDefaultsCommand.Execute(null);
        Assert.Equal("50", vm.LeftMarginText);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Five_stage_selector_and_saved_effort_are_bidirectional(int stage)
    {
        using var vm = new OptimizationViewModel(true);
        Assert.Equal(5, vm.StageStrategies.Count);
        vm.SelectedStageStrategy = vm.StageStrategies[stage - 1];
        Assert.Equal(stage.ToString(), vm.SelectedEffortLevel);
        vm.SelectedEffortLevel = (6 - stage).ToString();
        Assert.Equal(vm.StageStrategies[5 - stage], vm.SelectedStageStrategy);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(12)]
    public void Group_engine_profile_uses_pane_metadata_and_preserves_approved_breakout(int thickness)
    {
        var resolve = typeof(OptimizationViewModel).GetMethod("ResolveGroupMargins", BindingFlags.NonPublic | BindingFlags.Static)!;
        var input = MarginProfile.DefaultFor(10, 40);
        var output = ((MarginProfile Margins, string Note))resolve.Invoke(null, new object[] { "legacy pane label", input, thickness })!;
        Assert.Equal(thickness, output.Margins.ThicknessMm);
        Assert.Equal(0, output.Margins.TopMm);
        Assert.Equal(50, output.Margins.BottomMm);
        Assert.Equal(40, output.Margins.MinBreakoutMm);
        var thin = ((MarginProfile Margins, string Note))resolve.Invoke(null, new object[] { "thin", input, 6 })!;
        Assert.Equal(15, thin.Margins.MinBreakoutMm);
        Assert.Equal(15, thin.Margins.LeftMm);
    }
}
