using ProGlassAutomation.ViewModels.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Agent;

// The imported workflow intentionally uses ADD SELECTED PANES, not the retired
// auto-import/synthetic-row picker. Pin observable behavior, not private method names.
public sealed class OptimizationPickerRegressionTests
{
    internal static OptimizationViewModel.JobOptionVm Job(string id, int qty = 3)
    {
        var job = new OptimizationViewModel.JobOptionVm
        { JobOrderId = id, SourceProformaId = "PI-" + id };
        var spec = new OptimizationViewModel.SpecOptionVm { JobOrderId = id, SpecGroupNumber = 1 };
        var pane = new OptimizationViewModel.BuildUpLayerVm
        {
            Kind = "Pane", Position = "Outer", Title = "6mm clear ft glass", IsSelected = true,
            JobOrderId = id, ProformaId = job.SourceProformaId, SpecGroupNumber = 1
        };
        pane.Pieces.Add((pane.Title, "G1", 500, 700, qty));
        spec.Layers.Add(pane);
        job.Specs.Add(spec);
        job.Step4Sheets.Add((3210, 2250, pane.Title, "Spec 1", 40m, 1, "Outer"));
        return job;
    }

    internal static void Select(OptimizationViewModel vm, OptimizationViewModel.JobOptionVm job)
    {
        vm.AttachJobOption(job);
        vm.AvailableJobs.Add(job);
        job.IsSelected = true;
    }

    [Fact]
    public void Picker_selects_real_panes_and_import_requires_explicit_action()
    {
        using var vm = new OptimizationViewModel(true) { IsHeadlessHost = false };
        Select(vm, Job("1"));
        Assert.True(vm.HasSpecBreakdown);
        Assert.Single(vm.Stocks);
        Assert.Empty(vm.Pieces);
        vm.AddSelectedPanesCommand.Execute(null);
        Assert.Single(vm.Pieces);
        Assert.Equal("3", vm.Pieces[0].QtyText);
        Assert.Equal("1", vm.Pieces[0].JobOrderId);
        Assert.Equal("PI-1", vm.DocumentKey);
    }

    [Fact]
    public void Repeated_import_is_idempotent_but_different_jobs_are_not_deduplicated()
    {
        using var vm = new OptimizationViewModel(true) { IsHeadlessHost = false };
        Select(vm, Job("1", 3));
        Select(vm, Job("2", 5));
        vm.AddSelectedPanesCommand.Execute(null);
        vm.AddSelectedPanesCommand.Execute(null);
        Assert.Equal(2, vm.Pieces.Count);
        Assert.Equal(8, vm.Pieces.Sum(p => int.Parse(p.QtyText)));
        Assert.Empty(vm.DocumentKey); // no single-quote feed for a multi-job plan
    }

    [Fact]
    public void Deselecting_a_job_removes_only_its_imported_sizes()
    {
        using var vm = new OptimizationViewModel(true) { IsHeadlessHost = false };
        var first = Job("1");
        Select(vm, first);
        Select(vm, Job("2"));
        vm.AddSelectedPanesCommand.Execute(null);
        first.IsSelected = false;
        Assert.Single(vm.Pieces);
        Assert.Equal("2", vm.Pieces[0].JobOrderId);
    }

    [Fact]
    public void Reference_action_never_invents_a_calibration_job()
    {
        using var vm = new OptimizationViewModel(true);
        vm.LoadReferenceJobCommand.Execute(null);
        Assert.Empty(vm.Pieces);
        Assert.Empty(vm.Stocks);
    }

    [Fact]
    public void Headless_host_does_not_allow_job_selection_to_replace_step5_inputs()
    {
        using var vm = new OptimizationViewModel(true);
        var row = new OptimizationViewModel.PieceEntryVm { WidthText = "400", HeightText = "500", QtyText = "2" };
        vm.Pieces.Add(row);
        Select(vm, Job("1"));
        Assert.Same(row, Assert.Single(vm.Pieces));
        Assert.Empty(vm.Stocks);
    }
}
