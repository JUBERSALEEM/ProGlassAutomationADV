using System.Reflection;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.ViewModels.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

public sealed class OptimizationPortAdapterTests
{
    private static bool Build(OptimizationViewModel vm, out List<CutPiece> pieces,
        out List<(StockSize Stock, string Composition)> stocks)
    {
        var method = typeof(OptimizationViewModel).GetMethod("TryBuildInputs", BindingFlags.NonPublic | BindingFlags.Instance)!;
        object?[] args = { null, null, null };
        bool valid = (bool)method.Invoke(vm, args)!;
        pieces = (List<CutPiece>)args[0]!;
        stocks = (List<(StockSize Stock, string Composition)>)args[1]!;
        return valid;
    }

    private static OptimizationViewModel Input(int quantity = 1, string availability = "∞")
    {
        var vm = new OptimizationViewModel(true);
        vm.Pieces.Add(new OptimizationViewModel.PieceEntryVm
        { GlassRef = "6mm clear ft glass", WidthText = "500", HeightText = "700", QtyText = quantity.ToString() });
        vm.Stocks.Add(new OptimizationViewModel.StockEntryVm("3210", "2250") { AvailableText = availability });
        return vm;
    }

    [Theory]
    [InlineData(20001)]
    [InlineData(50001)]
    public void Expanded_inputs_are_not_capped_at_twenty_thousand(int quantity)
    {
        using var vm = Input(quantity);
        Assert.True(Build(vm, out var pieces, out _));
        Assert.Equal(quantity, pieces.Count);
    }

    [Fact]
    public void Oversized_quantity_is_rejected_before_expansion()
    {
        using var vm = Input(2_000_001);
        Assert.False(Build(vm, out var pieces, out _));
        Assert.Empty(pieces);
        Assert.Contains("2,000,000", vm.RunMessage);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("garbage")]
    public void Invalid_or_empty_inventory_never_becomes_unlimited(string available)
    {
        using var vm = Input(1, available);
        Assert.False(Build(vm, out _, out var stocks));
        Assert.Empty(stocks);
    }

    [Fact]
    public void Rotation_lock_reaches_the_preserved_engine_contract()
    {
        using var vm = Input();
        vm.Pieces[0].RotationLocked = true;
        Assert.True(Build(vm, out var pieces, out _));
        Assert.False(Assert.Single(pieces).AllowRotate);
    }

    [Fact]
    public void Missing_matching_rack_is_a_validation_failure_not_other_glass()
    {
        using var vm = Input();
        vm.Stocks[0].Composition = "8mm bronze ft glass";
        vm.RunQuickCommand.Execute(null);
        Assert.False(vm.IsRunning);
        Assert.Contains("No matching stock", vm.RunMessage);
    }

    [Fact]
    public void Editing_a_completed_plan_invalidates_its_feed()
    {
        using var vm = Input();
        typeof(OptimizationViewModel).GetProperty("HasResult")!.SetValue(vm, true);
        OptimizationResultFeed.PublishLive(new[] { new OptimizationPaneResult { SpecNumber = 1, Side = "Outer" } },
            "port test", "PI-test", "run-test", "fingerprint");
        Assert.True(OptimizationResultFeed.IsForDocument("PI-test"));
        vm.Stocks[0].AvailableText = "5";
        Assert.False(vm.HasResult);
        Assert.False(OptimizationResultFeed.IsForDocument("PI-test"));
    }
    [Fact]
    public void Exhausted_run_balance_drops_stock_instead_of_setting_unlimited_sentinel()
    {
        var rack = new StockSize { WidthMm = 3210, HeightMm = 2250, MaxSheets = 2, ThicknessMm = 6, Color = "CLEAR" };
        var pool = new List<(StockSize Stock, string Composition)> { (rack, "6mm clear ft glass") };
        var ledger = new Dictionary<(int W, int H, int T, string Color), long>
        { [(3210, 2250, 6, "CLEAR")] = 0 };
        object?[] args = { pool, ledger, null };
        var method = typeof(OptimizationViewModel).GetMethod("ApplyRunBalance", BindingFlags.NonPublic | BindingFlags.Static)!;
        var remaining = (List<(StockSize Stock, string Composition)>)method.Invoke(null, args)!;
        Assert.Empty(remaining);
        Assert.True((bool)args[2]!);
        Assert.Equal(2, rack.MaxSheets);
    }

}
