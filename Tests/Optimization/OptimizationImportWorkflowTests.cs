using System.Globalization;
using System.Reflection;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.ViewModels.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

public sealed class OptimizationImportWorkflowTests
{
    [Theory]
    [InlineData("1000", "800", "4")]
    [InlineData("1000.0", "800.0", "4.0")]
    [InlineData(" 1000 ", " 800 ", " 4 ")]
    public void Successful_piece_parse_returns_dimensions_and_quantity_not_zero(string width, string height, string quantity)
    {
        var piece = new OptimizationViewModel.PieceEntryVm
        { WidthText = width, HeightText = height, QtyText = quantity };
        Assert.True(piece.TryParse(out int w, out int h, out int q));
        Assert.Equal(1000, w);
        Assert.Equal(800, h);
        Assert.Equal(4, q);
    }

    [Theory]
    [InlineData("0", "800", "4")]
    [InlineData("1000", "-1", "4")]
    [InlineData("1000", "800", "4.5")]
    [InlineData("1000.5", "800", "4")]
    [InlineData("bad", "800", "4")]
    public void Invalid_import_values_are_not_successful_sizes(string width, string height, string quantity)
    {
        var piece = new OptimizationViewModel.PieceEntryVm
        { WidthText = width, HeightText = height, QtyText = quantity };
        Assert.False(piece.TryParse(out _, out _, out _));
    }

    private static OptimizationViewModel NewStudio() => new(true) { IsHeadlessHost = false };

    private static OptimizationViewModel.JobOptionVm SavedJob()
    {
        var invoice = new ProformaInvoiceSnapshot();
        invoice.SpecGroups.Add(Group(1, 2, 1000, 800, 900, 700,
            "6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass"));
        invoice.SpecGroups.Add(Group(2, 3, 1100, 900, 1050, 850,
            "6mm Clear FT Glass + 12mm ASP + 8mm Clear FT Glass"));
        var saved = new SavedJobOrder
        {
            JobOrderId = "JOB-IMPORT", SourceProformaId = "PI-IMPORT", CustomerName = "Import test",
            FullSnapshotJson = JobOrderMapper.SerializeSnapshot(new JobOrderSnapshot
            { JobOrderId = "JOB-IMPORT", SourceProformaId = "PI-IMPORT", EmbeddedInvoice = invoice })
        };
        // Exercise the same saved JSON, mapper, distribution and pane projection
        // as LoadJobsAsync, not pre-populated/mock pane rows.
        var snapshot = JobOrderMapper.DeserializeSnapshot(saved);
        Assert.NotNull(snapshot);
        return OptimizationViewModel.BuildJobOption(saved.JobOrderId, saved.SourceProformaId,
            saved.CustomerName, snapshot!.EmbeddedInvoice);
    }

    private static SpecGroupSnapshot Group(int number, int qty, int w1, int h1, int w2, int h2, string spec) => new()
    {
        GroupNumber = number, SelectedGlassType = "DGU", SelectedSpecName = spec,
        OuterGlassColor = "Clear", InnerGlassColor = "Clear",
        OuterSheetWidthMm = 3210, OuterSheetHeightMm = 2250, OuterSheetPricePerSqm = 40m,
        InnerSheetWidthMm = 3210, InnerSheetHeightMm = 2250, InnerSheetPricePerSqm = 50m,
        ItemRows = new List<GlassItemRowSnapshot>
        {
            new() { SerialNumber = 1, GlassRef = "G1", W1 = w1, H1 = h1, W2 = w2, H2 = h2, Qty = qty }
        }
    };

    private static OptimizationViewModel.JobOptionVm SelectCard(OptimizationViewModel vm)
    {
        var job = SavedJob();
        vm.AvailableJobs.Add(job);
        vm.SelectedJob = job; // actual SelectedItem binding path, not direct checkbox mutation
        return job;
    }

    [Fact]
    public void Saved_job_card_to_add_four_panes_populates_real_sizes_and_cut_plan()
    {
        using var vm = NewStudio();
        var job = SelectCard(vm);
        Assert.True(job.IsSelected);
        Assert.Equal(4, vm.SelectedPaneCount);
        Assert.Contains("Add 4 selected", vm.AddPanesButtonLabel);
        Assert.True(vm.AddSelectedPanesCommand.CanExecute(null));
        Assert.Empty(vm.Pieces);
        vm.AddSelectedPanesCommand.Execute(null);
        Assert.Equal(4, vm.Pieces.Count);
        Assert.True(vm.HasPieces);
        Assert.True(vm.HasCutPlan);
        Assert.Equal(10, vm.CutPlanRows.Count);
        Assert.Equal(10, vm.SizeGroups.Sum(group => int.Parse(group.PieceCount, CultureInfo.InvariantCulture)));
        Assert.Contains(vm.Pieces, p => p.WidthText == "1000" && p.HeightText == "800" && p.QtyText == "2");
        Assert.Contains(vm.Pieces, p => p.WidthText == "900" && p.HeightText == "700" && p.QtyText == "2");
        Assert.Contains(vm.Pieces, p => p.WidthText == "1100" && p.HeightText == "900" && p.QtyText == "3");
        Assert.Contains(vm.Pieces, p => p.WidthText == "1050" && p.HeightText == "850" && p.QtyText == "3");
        Assert.All(vm.Pieces, p =>
        {
            Assert.True(p.TryParse(out int w, out int h, out int q));
            Assert.True(w > 0 && h > 0 && q > 0);
            Assert.Equal("JOB-IMPORT", p.JobOrderId);
        });
        Assert.NotEmpty(vm.Stocks);
        // Follow the import all the way to the unchanged engine's input contract.
        object?[] inputs = { null, null, null };
        var build = typeof(OptimizationViewModel).GetMethod("TryBuildInputs", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.True((bool)build.Invoke(vm, inputs)!);
        var enginePieces = Assert.IsType<List<CutPiece>>(inputs[0]);
        Assert.Equal(10, enginePieces.Count);
        Assert.All(enginePieces, piece => Assert.True(piece.WidthMm > 0 && piece.HeightMm > 0));
        vm.AddSelectedPanesCommand.Execute(null);
        Assert.Equal(4, vm.Pieces.Count);
        Assert.Equal(10, vm.CutPlanRows.Count);
    }

    [Fact]
    public void Unchecking_a_job_then_clicking_its_card_can_select_it_again()
    {
        using var vm = NewStudio();
        var job = SelectCard(vm);
        job.IsSelected = false;
        Assert.Null(vm.SelectedJob);
        Assert.Empty(vm.PaneChecklist);
        vm.SelectedJob = job;
        Assert.True(job.IsSelected);
        Assert.Equal(4, vm.SelectedPaneCount);
    }

    [Fact]
    public void Add_imports_only_ticked_panes_and_updates_the_count_label()
    {
        using var vm = NewStudio();
        SelectCard(vm);
        vm.ClearPaneSelectionCommand.Execute(null);
        Assert.False(vm.AddSelectedPanesCommand.CanExecute(null));
        var pane = vm.PaneChecklist.Single(p => p.SpecGroupNumber == 1 && p.Position == "Inner");
        pane.IsSelected = true;
        Assert.Equal(1, vm.SelectedPaneCount);
        vm.AddSelectedPanesCommand.Execute(null);
        var piece = Assert.Single(vm.Pieces);
        Assert.Equal("900", piece.WidthText);
        Assert.Equal("700", piece.HeightText);
        Assert.Equal(2, vm.CutPlanRows.Count);
    }

    [Fact]
    public void Pane_double_click_command_imports_one_pane_and_opens_its_sizes_idempotently()
    {
        using var vm = NewStudio();
        SelectCard(vm);
        var pane = vm.PaneChecklist.Single(p => p.SpecGroupNumber == 2 && p.Position == "Inner");
        Assert.True(vm.ImportPaneSizesCommand.CanExecute(pane));
        vm.ImportPaneSizesCommand.Execute(pane);
        vm.ImportPaneSizesCommand.Execute(pane);
        Assert.Single(vm.Pieces);
        Assert.Equal(OptimizationViewModel.TabIndexSizes, vm.SelectedTabIndex);
        Assert.Equal(pane.Title, Assert.Single(vm.SizeGroups).Composition);
        Assert.Equal(3, vm.CutPlanRows.Count);
    }

    [Fact]
    public void Cutting_row_double_click_and_group_selector_refresh_sizes_without_copying_pieces()
    {
        using var vm = NewStudio();
        SelectCard(vm);
        vm.AddSelectedPanesCommand.Execute(null);
        var piece = vm.Pieces.Single(p => p.SpecGroupNumber == 2 && p.Position == "Inner");
        Assert.True(vm.OpenPieceSizesCommand.CanExecute(piece));
        vm.OpenPieceSizesCommand.Execute(piece);
        Assert.Equal(OptimizationViewModel.TabIndexSizes, vm.SelectedTabIndex);
        Assert.Equal(piece.GlassRef, Assert.Single(vm.SizeGroups).Composition);
        Assert.Equal(3, vm.CutPlanRows.Count);
        vm.OpenPieceSizesCommand.Execute(piece);
        Assert.Equal(4, vm.Pieces.Count);
        vm.SelectedCuttingGroup = OptimizationViewModel.AllCuttingGroups;
        Assert.Equal(10, vm.CutPlanRows.Count);
        Assert.True(vm.SizeGroups.Count > 1);
    }

    [Fact]
    public void Removing_filtered_material_resets_filter_before_rebuilding_rows()
    {
        using var vm = NewStudio();
        SelectCard(vm);
        vm.AddSelectedPanesCommand.Execute(null);
        var piece = vm.Pieces.Single(p => p.SpecGroupNumber == 2 && p.Position == "Inner");
        vm.OpenPieceSizesCommand.Execute(piece);
        vm.RemovePieceCommand.Execute(piece);
        Assert.Equal(OptimizationViewModel.AllCuttingGroups, vm.SelectedCuttingGroup);
        Assert.Equal(7, vm.CutPlanRows.Count);
        Assert.Equal(7, vm.SizeGroups.Sum(group => int.Parse(group.PieceCount, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void New_pane_import_resets_old_filter_and_inline_quantity_edits_reach_sizes()
    {
        using var vm = NewStudio();
        SelectCard(vm);
        var pane = vm.PaneChecklist.First();
        vm.ImportPaneSizesCommand.Execute(pane);
        vm.AddSelectedPanesCommand.Execute(null);
        Assert.Equal(OptimizationViewModel.AllCuttingGroups, vm.SelectedCuttingGroup);
        Assert.Equal(10, vm.CutPlanRows.Count);
        var piece = vm.Pieces.First(p => p.SpecGroupNumber == 1);
        piece.QtyText = "7";
        Assert.Equal(15, vm.CutPlanRows.Count);
        Assert.Equal(15, vm.SizeGroups.Sum(group => int.Parse(group.PieceCount, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Missing_snapshot_is_explained_not_replaced_with_fake_sizes()
    {
        var job = OptimizationViewModel.BuildJobOption("JOB-BAD", "PI-BAD", "Test", null);
        Assert.False(job.HasOptimizablePanes);
        Assert.Empty(job.Specs);
        Assert.True(job.HasWarnings);
        using var vm = NewStudio();
        vm.AvailableJobs.Add(job);
        vm.SelectedJob = job;
        Assert.Empty(vm.PaneChecklist);
        Assert.False(vm.AddSelectedPanesCommand.CanExecute(null));
        Assert.Empty(vm.Pieces);
    }
}
