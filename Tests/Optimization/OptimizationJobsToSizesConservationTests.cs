using ProGlassAutomation.Tests.Agent;
using ProGlassAutomation.ViewModels.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

public sealed class OptimizationJobsToSizesConservationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(150)] // no hundred-job UI admission limit
    public void Explicit_pane_import_conserves_quantities_across_identically_sized_jobs(int jobs)
    {
        using var vm = new OptimizationViewModel(true) { IsHeadlessHost = false };
        for (int i = 1; i <= jobs; i++)
            OptimizationPickerRegressionTests.Select(vm, OptimizationPickerRegressionTests.Job(i.ToString(), i));
        vm.AddSelectedPanesCommand.Execute(null);
        Assert.Equal(jobs, vm.Pieces.Count);
        Assert.Equal(jobs * (jobs + 1) / 2, vm.Pieces.Sum(p => int.Parse(p.QtyText)));
        vm.AddSelectedPanesCommand.Execute(null);
        Assert.Equal(jobs, vm.Pieces.Count);
    }

    [Fact]
    public void Equal_dimensions_in_different_panes_or_specs_remain_distinct_inputs()
    {
        using var vm = new OptimizationViewModel(true) { IsHeadlessHost = false };
        var job = OptimizationPickerRegressionTests.Job("same-job");
        var second = new OptimizationViewModel.BuildUpLayerVm
        {
            Kind = "Pane", Position = "Inner", Title = "6mm clear ft glass", IsSelected = true,
            JobOrderId = job.JobOrderId, ProformaId = job.SourceProformaId, SpecGroupNumber = 1
        };
        second.Pieces.Add((second.Title, "G1", 500, 700, 4));
        job.Specs[0].Layers.Add(second);
        OptimizationPickerRegressionTests.Select(vm, job);
        vm.AddSelectedPanesCommand.Execute(null);
        Assert.Equal(2, vm.Pieces.Count);
        Assert.Equal(7, vm.Pieces.Sum(p => int.Parse(p.QtyText)));
    }
}
