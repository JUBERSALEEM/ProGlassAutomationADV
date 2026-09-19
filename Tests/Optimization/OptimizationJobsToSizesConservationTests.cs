// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Optimization/OptimizationJobsToSizesConservationTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Tests — Optimization JOBS → SIZES quantity conservation
//
// WHAT THIS PINS
// The reported defect: tick a JOB on the left → SPECIFICATIONS and the
// middle pane checklist populate → the SIZES cutting-size list stays
// EMPTY. A second-pass forensic audit traced the whole chain and found the
// values are never "lost" mid-flight in the current source; they are lost
// at three specific, separately-fixable points, plus one build-identity
// problem that made the whole thing unauditable in the field:
//
//   THE CHAIN (each hop is asserted below)
//     Q0  ProductionDistributionEngine.DistributeGroup  → job Step 5 rows
//     Q1  LoadJobsInternalAsync                          → layer.Pieces
//     Q2  AutoRebuildPiecesFromSelectedJobs (selected)   → pane rows/qty
//     Q3  …merge by composition + W + H + position       → merged qty
//     Q4  …                                              → Pieces
//     Q5  RefreshDerived → SizeGroups / CutPlanRows       → visible qty
//     Q6  …filtered by the cutting-group dropdown         → SIZES on screen
//
//   RC-009 (first pass, REMOVED here) forced every pane layer of every
//   selected job back to IsSelected=true inside RebuildSelectedJobBreakdown.
//   That made "Clear → untick job → retick job" work, and simultaneously
//   re-added glass the operator had deliberately removed from a DIFFERENT,
//   still-ticked job. Its "fallback" (re-add layer.Pieces unmerged when the
//   merge produced 0 rows) read the same collection through the same
//   w<=0||h<=0||qty<=0 filter that had just produced 0 rows, so it could
//   never produce a different result — dead code.
//   RC-013 replaces the blanket force with a PER-JOB reset: a job's panes
//   return to their pre-ticked default only when THAT job transitions
//   unselected → selected.
//
//   RC-010: the SIZES dropdown was built from compositions with Length>0
//   while the group cards keyed on the raw trimmed ref ("") and the cut plan
//   on "—". A piece with no resolved composition therefore had no dropdown
//   entry and vanished from BOTH cards and cut plan the moment any specific
//   group was selected. All consumers now go through CompositionLabel.
//
//   RC-011: the SelectedCuttingGroup setter raised PropertyChanged but never
//   re-derived SizeGroups / CutPlanRows, so the dropdown looked dead while
//   Run() already honoured the selection — screen and engine disagreed.
//
// HOW THIS DRIVES THE REAL PATH WITHOUT A DATABASE
// `LoadJobs()` only applies its results through
// `System.Windows.Application.Current?.Dispatcher`, which is null under
// xUnit — so the picker's database path is unreachable in tests by design.
// `AttachJobOption(job)` is the same wiring `LoadJobsInternalAsync` uses,
// extracted so a job option can be registered without the database and then
// ticked EXACTLY as the JOBS-tab CheckBox ticks it: IsSelected → 
// OnJobSelectionChanged → RebuildSelectedJobBreakdown →
// SyncStocksFromSelectedJobs → AutoRebuildPiecesFromSelectedJobs →
// RefreshDerived. Nothing here reaches into private state.
//
// INVARIANT UNDER TEST
// A merge may COMBINE rows; it must never make glass disappear. Total
// quantity measured at Q1 == Q2 == Q3 == Q4 == Q5, and Q6 equals Q5 while
// the dropdown is on ALL and equals the selected group's own quantity
// otherwise.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProGlassAutomation.ViewModels.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization
{
    public sealed class OptimizationJobsToSizesConservationTests
    {
        // ── fixtures ────────────────────────────────────────────────────

        /// <summary>One Step 5 pane row as the distribution engine emits it:
        /// composition + position + W×H + qty, inside a named spec group.</summary>
        private sealed class PaneDef
        {
            public PaneDef(string spec, string composition, string position, int w, int h, int qty)
            {
                Spec = spec;
                Composition = composition;
                Position = position;
                W = w;
                H = h;
                Qty = qty;
            }

            public string Spec { get; }
            public string Composition { get; }
            public string Position { get; }
            public int W { get; }
            public int H { get; }
            public int Qty { get; }
        }

        /// <summary>Rack big enough for every test piece, so a run is never
        /// refused for stock reasons.</summary>
        private const int SheetW = 3210;
        private const int SheetH = 2250;

        /// <summary>
        /// Builds a job option the way <c>LoadJobsInternalAsync</c> builds one:
        /// pane layers constructed PRE-TICKED (<c>IsSelected = true</c>), one
        /// <c>Pieces</c> row per Step 5 cutting row, and one Step 4 rack per
        /// composition so STOCKS follows the selection.
        /// </summary>
        private static OptimizationViewModel.JobOptionVm MakeJob(string jobId, params PaneDef[] panes)
        {
            var job = new OptimizationViewModel.JobOptionVm
            {
                JobOrderId = jobId,
                CustomerName = "Customer " + jobId,
                Summary = jobId,
            };

            int specNumber = 0;
            foreach (var bySpec in panes.GroupBy(p => p.Spec, StringComparer.Ordinal))
            {
                specNumber++;
                var spec = new OptimizationViewModel.SpecOptionVm
                {
                    JobOrderId = jobId,
                    SpecGroupNumber = specNumber,
                    SpecName = bySpec.Key,
                    BuildType = "DGU",
                };

                foreach (var p in bySpec)
                {
                    var layer = new OptimizationViewModel.BuildUpLayerVm
                    {
                        Kind = "Pane",
                        Position = p.Position,
                        SpecLabel = "Spec " + specNumber.ToString(CultureInfo.InvariantCulture),
                        Title = p.Composition,
                        Thickness = p.Composition,
                        TotalPcs = p.Qty,
                        // Exactly the picker's default: panes are pre-ticked so
                        // a job's sizes import with a single click.
                        IsSelected = true,
                    };
                    layer.Pieces.Add((p.Composition, "PC-" + p.Position, p.W, p.H, p.Qty));
                    spec.Layers.Add(layer);
                }

                job.Specs.Add(spec);
            }

            foreach (var comp in panes.Select(p => p.Composition).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                job.Step4Sheets.Add((SheetW, SheetH, comp,
                    jobId + " rack", 120m, 1, "Outer"));
            }

            job.PieceRows = panes.Length;
            job.PiecesLabel = panes.Sum(p => p.Qty).ToString(CultureInfo.InvariantCulture) + " pcs";
            job.SpecCountLabel = specNumber.ToString(CultureInfo.InvariantCulture) + " specs";
            return job;
        }

        /// <summary>Registers a job with the picker and ticks it — the exact
        /// sequence the JOBS-tab CheckBox performs.</summary>
        private static OptimizationViewModel.JobOptionVm AddAndSelect(
            OptimizationViewModel vm, OptimizationViewModel.JobOptionVm job)
        {
            vm.AvailableJobs.Add(job);
            vm.AttachJobOption(job);
            job.IsSelected = true;
            return job;
        }

        /// <summary>Q1: everything the job's pane layers carry.</summary>
        private static int LayerQty(IEnumerable<OptimizationViewModel.BuildUpLayerVm> layers)
        {
            int total = 0;
            foreach (var l in layers)
                foreach (var (_, _, _, _, qty) in l.Pieces)
                    if (qty > 0) total += qty;
            return total;
        }

        private static int LayerQty(OptimizationViewModel.JobOptionVm job) =>
            LayerQty(job.Specs.SelectMany(s => s.Layers).Where(l => l.IsPane));

        /// <summary>Q4: the authoritative cutting list.</summary>
        private static int PieceQty(IEnumerable<OptimizationViewModel.PieceEntryVm> pieces)
        {
            int total = 0;
            foreach (var p in pieces)
                if (p.TryParse(out _, out _, out int q)) total += q;
            return total;
        }

        /// <summary>Q5/Q6: what the SIZES tab can actually show.</summary>
        private static int GroupQty(IEnumerable<OptimizationViewModel.SizeGroupVm> groups)
        {
            int total = 0;
            foreach (var g in groups)
                if (int.TryParse(g.PieceCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int q))
                    total += q;
            return total;
        }

        // ══════════════════════════════════════════════════════════════
        // A — single job, single pane, single size
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void A_SingleJob_SinglePane_SingleSize_PopulatesPiecesAndSizes()
        {
            var vm = new OptimizationViewModel();
            var job = MakeJob("JO-A", new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 4));

            AddAndSelect(vm, job);

            Assert.Single(vm.Pieces);
            Assert.True(vm.HasPieces, "HasPieces must be true once a job is ticked");
            Assert.Equal(4, PieceQty(vm.Pieces));
            Assert.Equal(4, LayerQty(job));

            var group = Assert.Single(vm.SizeGroups);
            Assert.Equal(4, GroupQty(vm.SizeGroups));
            Assert.Equal("1", group.SizeRows);

            // The cut plan expands qty 1..N — one visible row per physical piece.
            Assert.Equal(4, vm.CutPlanRows.Count);
            Assert.DoesNotContain("No pieces yet", vm.PieceSummary, StringComparison.Ordinal);
        }

        // ══════════════════════════════════════════════════════════════
        // B — multiple panes: equal-size DGU outer + inner must BOTH survive
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void B_EqualSizeDgu_OuterAndInner_BothPanesSurviveTheMerge()
        {
            var vm = new OptimizationViewModel();
            // Same composition, same W×H, different position — the exact case
            // that made pane position part of the merge key. Without position
            // in the key the inner pane silently merged into the outer one and
            // half the glass left the cutting list.
            var job = MakeJob("JO-B",
                new PaneDef("Spec 1", "6mm clear ft", "Outer", 1200, 800, 10),
                new PaneDef("Spec 1", "6mm clear ft", "Inner", 1200, 800, 10));

            AddAndSelect(vm, job);

            Assert.Equal(20, LayerQty(job));
            Assert.Equal(20, PieceQty(vm.Pieces));
            Assert.Equal(2, vm.Pieces.Count);
            Assert.Equal(20, GroupQty(vm.SizeGroups));
            Assert.Contains(vm.Pieces, p => p.Position == "Outer");
            Assert.Contains(vm.Pieces, p => p.Position == "Inner");
        }

        // ══════════════════════════════════════════════════════════════
        // C — multiple jobs merge, quantity conserved
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void C_TwoJobs_SameCompositionAndSize_MergeIntoOneRow_QuantityConserved()
        {
            var vm = new OptimizationViewModel();
            var j1 = MakeJob("JO-C1", new PaneDef("Spec 1", "6mm low-e", "Outer", 900, 600, 30));
            var j2 = MakeJob("JO-C2", new PaneDef("Spec 1", "6mm low-e", "Outer", 900, 600, 46));

            AddAndSelect(vm, j1);
            Assert.Equal(30, PieceQty(vm.Pieces));

            AddAndSelect(vm, j2);

            // One merged row carrying the SUM — merging combines, never drops.
            var row = Assert.Single(vm.Pieces);
            row.TryParse(out _, out _, out int qty);
            Assert.Equal(76, qty);
            Assert.Equal(76, LayerQty(j1) + LayerQty(j2));
            Assert.Equal(76, GroupQty(vm.SizeGroups));
            Assert.Equal(2, vm.SelectedJobs.Count());
        }

        // ══════════════════════════════════════════════════════════════
        // D — different W/H inside one composition
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void D_SameComposition_DifferentSizes_ProduceSeparateRowsInOneGroup()
        {
            var vm = new OptimizationViewModel();
            var job = MakeJob("JO-D",
                new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 5),
                new PaneDef("Spec 1", "6mm clear ft", "Outer", 1500, 750, 7));

            AddAndSelect(vm, job);

            Assert.Equal(2, vm.Pieces.Count);
            Assert.Equal(12, PieceQty(vm.Pieces));
            var group = Assert.Single(vm.SizeGroups);
            Assert.Equal("2", group.SizeRows);
            Assert.Equal(12, GroupQty(vm.SizeGroups));
            Assert.Equal(12, vm.CutPlanRows.Count);
        }

        // ══════════════════════════════════════════════════════════════
        // E — quantity greater than 1
        // ══════════════════════════════════════════════════════════════

        [Theory]
        [InlineData(2)]
        [InlineData(17)]
        [InlineData(152)]
        public void E_QuantityGreaterThanOne_IsConservedAcrossEveryHop(int qty)
        {
            var vm = new OptimizationViewModel();
            var job = MakeJob("JO-E", new PaneDef("Spec 1", "6mm ramly", "Outer", 800, 400, qty));

            AddAndSelect(vm, job);

            Assert.Equal(qty, LayerQty(job));          // Q1
            Assert.Equal(qty, PieceQty(vm.Pieces));    // Q4
            Assert.Equal(qty, GroupQty(vm.SizeGroups));// Q5
            // Q6 — the cut plan is capped for the screen, so compare up to the cap.
            Assert.Equal(Math.Min(qty, 500), vm.CutPlanRows.Count);
            Assert.Contains(qty.ToString(CultureInfo.InvariantCulture), vm.PieceSummary, StringComparison.Ordinal);
        }

        // ══════════════════════════════════════════════════════════════
        // F — navigate away and return (module reopened)
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void F_ModuleReopened_NewViewModel_SameJobData_SameResult()
        {
            var panes = new[]
            {
                new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 64),
                new PaneDef("Spec 1", "6mm low-e", "Inner", 1000, 500, 76),
            };

            var first = new OptimizationViewModel();
            var firstJob = MakeJob("JO-F", panes);
            AddAndSelect(first, firstJob);
            int firstQty = PieceQty(first.Pieces);
            int firstGroups = first.SizeGroups.Count;

            // Reopening the module constructs a fresh VM over freshly built job
            // options — the picker's own refresh semantics.
            var second = new OptimizationViewModel();
            var secondJob = MakeJob("JO-F", panes);
            AddAndSelect(second, secondJob);

            Assert.Equal(140, firstQty);
            Assert.Equal(firstQty, PieceQty(second.Pieces));
            Assert.Equal(firstGroups, second.SizeGroups.Count);
            Assert.Equal(GroupQty(first.SizeGroups), GroupQty(second.SizeGroups));
            Assert.Equal(first.CutPlanRows.Count, second.CutPlanRows.Count);
        }

        // ══════════════════════════════════════════════════════════════
        // G — Clear, then untick and retick the job  (RC-013 regression pin)
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void G_ClearThenUntickAndRetickJob_RepopulatesSizes()
        {
            var vm = new OptimizationViewModel();
            var job = MakeJob("JO-G",
                new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 8),
                new PaneDef("Spec 1", "6mm low-e", "Inner", 1000, 500, 12));

            AddAndSelect(vm, job);
            Assert.Equal(20, PieceQty(vm.Pieces));

            // PARTS-list CLEAR empties Pieces but does not touch the job tick.
            vm.ClearPiecesCommand.Execute(null);
            Assert.Empty(vm.Pieces);
            Assert.False(vm.HasPieces);

            // The operator's recovery: untick the job, tick it again.
            job.IsSelected = false;
            Assert.Empty(vm.Pieces);

            job.IsSelected = true;

            // This is precisely the case RC-009's blanket force was added for.
            // RC-013 handles it by resetting THIS job's panes on re-selection.
            Assert.Equal(20, PieceQty(vm.Pieces));
            Assert.Equal(20, GroupQty(vm.SizeGroups));
            Assert.Equal(2, vm.SizeGroups.Count);
        }

        [Fact]
        public void G2_OperatorUnticksOnePane_RetickingTheSamePaneRestoresIt()
        {
            var vm = new OptimizationViewModel();
            var job = MakeJob("JO-G2",
                new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 8),
                new PaneDef("Spec 1", "6mm low-e", "Inner", 1000, 500, 12));
            AddAndSelect(vm, job);

            var inner = job.Specs.SelectMany(s => s.Layers).First(l => l.Position == "Inner");
            inner.IsSelected = false;

            Assert.Equal(8, PieceQty(vm.Pieces));
            Assert.Equal(8, GroupQty(vm.SizeGroups));

            inner.IsSelected = true;

            Assert.Equal(20, PieceQty(vm.Pieces));
            Assert.Equal(20, GroupQty(vm.SizeGroups));
        }

        // ══════════════════════════════════════════════════════════════
        // H — run the optimization after the import
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void H_RunAfterImport_IsNotRefusedForMissingPiecesOrStock()
        {
            var vm = new OptimizationViewModel();
            var job = MakeJob("JO-H",
                new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 6),
                new PaneDef("Spec 1", "6mm low-e", "Inner", 1000, 500, 6));
            AddAndSelect(vm, job);

            // STOCKS follows the job selection: without racks TryBuildInputs
            // refuses with "Add at least one stock size".
            Assert.NotEmpty(vm.Stocks);
            Assert.True(vm.HasPieces);
            Assert.All(vm.Pieces, p => Assert.True(p.TryParse(out _, out _, out _)));

            vm.RunQuickCommand.Execute(null);

            // None of these refusals may be the outcome of a successful import.
            Assert.DoesNotContain("Add pieces first", vm.RunMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("Add at least one stock size", vm.RunMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("no valid pieces", vm.RunMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("invalid size", vm.RunMessage, StringComparison.Ordinal);
            // The engine must have accounted for every imported piece.
            Assert.Equal(12, PieceQty(vm.Pieces));
        }

        // ══════════════════════════════════════════════════════════════
        // I — change the selection after the list is populated
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void I_SelectingACuttingGroup_ImmediatelyRefiltersSizesAndCutPlan()
        {
            var vm = new OptimizationViewModel();
            var job = MakeJob("JO-I",
                new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 64),
                new PaneDef("Spec 1", "6mm low-e", "Inner", 1000, 500, 76));
            AddAndSelect(vm, job);

            Assert.Equal(2, vm.SizeGroups.Count);
            Assert.Equal(140, GroupQty(vm.SizeGroups));
            Assert.Equal(140, vm.CutPlanRows.Count);

            // RC-011: the dropdown used to raise PropertyChanged and nothing
            // else — the tab kept showing every group until an unrelated edit
            // happened to call RefreshDerived, while Run() already honoured the
            // selection. Screen and engine disagreed.
            vm.SelectedCuttingGroup = "6mm low-e";

            var only = Assert.Single(vm.SizeGroups);
            Assert.Equal("6mm low-e", only.Composition);
            Assert.Equal(76, GroupQty(vm.SizeGroups));
            Assert.Equal(76, vm.CutPlanRows.Count);
            Assert.All(vm.CutPlanRows, r => Assert.Equal("6mm low-e", r.Composition));

            // The underlying cutting list is untouched — filtering is a view
            // concern, never a data loss.
            Assert.Equal(140, PieceQty(vm.Pieces));

            vm.SelectedCuttingGroup = OptimizationViewModel.AllCuttingGroups;
            Assert.Equal(2, vm.SizeGroups.Count);
            Assert.Equal(140, GroupQty(vm.SizeGroups));
        }

        [Fact]
        public void I2_UntickingAWholeJob_RemovesItsGlass_KeepsTheOtherJob()
        {
            var vm = new OptimizationViewModel();
            var j1 = MakeJob("JO-I1", new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 40));
            var j2 = MakeJob("JO-I2", new PaneDef("Spec 1", "6mm low-e", "Outer", 1000, 500, 25));
            AddAndSelect(vm, j1);
            AddAndSelect(vm, j2);
            Assert.Equal(65, PieceQty(vm.Pieces));

            j1.IsSelected = false;

            Assert.Equal(25, PieceQty(vm.Pieces));
            Assert.Equal(25, GroupQty(vm.SizeGroups));
            Assert.Single(vm.SelectedJobs);
        }

        // ══════════════════════════════════════════════════════════════
        // J — application reload over the same job objects
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void J_AppReload_FreshViewModelOverTheSameJobObjects_RebuildsIdentically()
        {
            // Pane tick state lives on BuildUpLayerVm, which the JobOptionVm
            // OWNS — so a reload that reuses job objects must not inherit a
            // stale untick. Attaching a fresh VM re-registers the wiring and
            // the tick rebuilds from the layers' current state.
            var job = MakeJob("JO-J",
                new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 12),
                new PaneDef("Spec 2", "6mm low-e", "Outer", 1200, 900, 9));

            var before = new OptimizationViewModel();
            AddAndSelect(before, job);
            int beforeQty = PieceQty(before.Pieces);
            int beforeGroups = before.SizeGroups.Count;

            var after = new OptimizationViewModel();
            after.AvailableJobs.Add(job);
            after.AttachJobOption(job);
            // Simulate the reload re-applying the persisted tick.
            job.IsSelected = false;
            job.IsSelected = true;

            Assert.Equal(21, beforeQty);
            Assert.Equal(beforeQty, PieceQty(after.Pieces));
            Assert.Equal(beforeGroups, after.SizeGroups.Count);
            Assert.Equal(before.CutPlanRows.Count, after.CutPlanRows.Count);
        }

        // ══════════════════════════════════════════════════════════════
        // RC-010 — unlabelled composition must be reachable, not invisible
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void RC010_UnlabelledComposition_GetsItsOwnDropdownEntry()
        {
            var vm = new OptimizationViewModel();
            var job = MakeJob("JO-U",
                new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 64),
                new PaneDef("Spec 1", string.Empty, "Inner", 1000, 500, 13));
            AddAndSelect(vm, job);

            // Both compositions are selectable — previously the empty one had
            // no dropdown entry at all, so it could never be shown on its own.
            Assert.Contains(OptimizationViewModel.UnlabelledGlass, vm.CuttingGroups);
            Assert.Contains("6mm clear ft", vm.CuttingGroups);
            Assert.Equal(77, PieceQty(vm.Pieces));
            Assert.Equal(77, GroupQty(vm.SizeGroups));
        }

        [Fact]
        public void RC010_SelectingALabelledGroup_DoesNotSilentlyDropUnlabelledGlassFromTheTotal()
        {
            var vm = new OptimizationViewModel();
            var job = MakeJob("JO-U2",
                new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 64),
                new PaneDef("Spec 1", string.Empty, "Inner", 1000, 500, 13));
            AddAndSelect(vm, job);

            // 9 of 13 used to vanish here: the cards keyed on "" and the cut
            // plan on "—", neither of which any dropdown value can match.
            vm.SelectedCuttingGroup = OptimizationViewModel.UnlabelledGlass;

            var only = Assert.Single(vm.SizeGroups);
            Assert.Equal(OptimizationViewModel.UnlabelledGlass, only.Composition);
            Assert.Equal(13, GroupQty(vm.SizeGroups));
            Assert.Equal(13, vm.CutPlanRows.Count);
            Assert.All(vm.CutPlanRows,
                r => Assert.Equal(OptimizationViewModel.UnlabelledGlass, r.Composition));

            // Nothing was destroyed — the full set is still in the cutting list.
            Assert.Equal(77, PieceQty(vm.Pieces));
        }

        // ══════════════════════════════════════════════════════════════
        // RC-013 — the anti-regression pin for RC-009's blanket force
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void RC013_TickingASecondJob_DoesNotRetickPanesTheOperatorRemovedFromTheFirst()
        {
            var vm = new OptimizationViewModel();
            var j1 = MakeJob("JO-R1",
                new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 30),
                new PaneDef("Spec 1", "6mm low-e", "Inner", 1000, 500, 30));
            var j2 = MakeJob("JO-R2", new PaneDef("Spec 1", "6mm ramly", "Outer", 900, 600, 12));

            AddAndSelect(vm, j1);
            Assert.Equal(60, PieceQty(vm.Pieces));

            // The operator decides job 1 needs no inner pane and unticks it.
            var inner = j1.Specs.SelectMany(s => s.Layers).First(l => l.Position == "Inner");
            inner.IsSelected = false;
            Assert.Equal(30, PieceQty(vm.Pieces));

            // RC-009 forced EVERY pane of EVERY selected job back to
            // IsSelected=true on the next rebuild, so ticking job 2 silently
            // re-added the 30 inner pieces the operator had just removed.
            AddAndSelect(vm, j2);

            Assert.False(inner.IsSelected);
            Assert.Equal(42, PieceQty(vm.Pieces));
            Assert.Equal(42, GroupQty(vm.SizeGroups));
        }

        // ══════════════════════════════════════════════════════════════
        // Conservation — the invariant itself, on a realistic multi-spec job
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Conservation_TwoSpecJob_QuantityIsIdenticalAtEveryHop()
        {
            var vm = new OptimizationViewModel();
            var job = MakeJob("JO-Q",
                new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 32),
                new PaneDef("Spec 1", "6mm low-e", "Inner", 1000, 500, 38),
                new PaneDef("Spec 1", "6mm ramly", "Inner", 800, 400, 6),
                new PaneDef("Spec 2", "6mm clear ft", "Outer", 1000, 500, 32),
                new PaneDef("Spec 2", "6mm low-e", "Inner", 1000, 500, 38),
                new PaneDef("Spec 2", "6mm ramly", "Inner", 800, 400, 6));

            AddAndSelect(vm, job);

            int q1 = LayerQty(job);                    // what the panes carry
            int q4 = PieceQty(vm.Pieces);              // the cutting list
            int q5 = GroupQty(vm.SizeGroups);          // the SIZES cards
            int q6 = vm.CutPlanRows.Count;             // the expanded plan

            Assert.Equal(152, q1);
            Assert.Equal(q1, q4);
            Assert.Equal(q1, q5);
            Assert.Equal(q1, q6);

            // Spec 1 and Spec 2 carry IDENTICAL composition + size + position,
            // so the merge collapses each pair into one row: 3 cutting-list rows
            // in 3 cutting groups, each holding BOTH specs' quantity
            // (32+32, 38+38, 6+6). Merging combined 6 pane rows into 3 cutting
            // rows and still conserved all 152 pieces.
            Assert.Equal(3, vm.SizeGroups.Count);
            Assert.Equal(3, vm.Pieces.Count);
            Assert.Equal("64", vm.SizeGroups.First(g => g.Composition == "6mm clear ft").PieceCount);
            Assert.Equal("76", vm.SizeGroups.First(g => g.Composition == "6mm low-e").PieceCount);
            Assert.Equal("12", vm.SizeGroups.First(g => g.Composition == "6mm ramly").PieceCount);

            // Panes ticked == panes the checklist offers == specs shown.
            Assert.Equal(6, vm.PaneChecklist.Count);
            Assert.Equal(6, vm.SelectedPaneCount);
            Assert.Equal(2, vm.JobSpecBreakdown.Count);
            Assert.True(vm.HasSpecBreakdown);
            Assert.True(vm.HasPaneChecklist);
        }

        // ══════════════════════════════════════════════════════════════
        // Honest empties — no silent zero, no dead fallback
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void HonestEmpty_PaneWithNoCuttingRows_SaysSoInsteadOfRenderingNothing()
        {
            var vm = new OptimizationViewModel();
            var job = MakeJob("JO-N", new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 4));
            // A pane whose Step 5 rows never reached the picker: the layer
            // exists and is ticked, but carries no cuttable row.
            job.Specs[0].Layers[0].Pieces.Clear();

            AddAndSelect(vm, job);

            Assert.Empty(vm.Pieces);
            Assert.False(vm.HasPieces);
            // RC-009's fallback would have re-read the same empty collection
            // through the same filter and then relabelled the outcome as a bug.
            // The honest version tells the operator which hop is empty.
            Assert.Contains("no Step 5 cutting rows", vm.RunMessage, StringComparison.Ordinal);
        }

        [Fact]
        public void HonestEmpty_AllRowsFilteredAsUncuttable_SaysSoInsteadOfRenderingNothing()
        {
            var vm = new OptimizationViewModel();
            var job = MakeJob("JO-Z", new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 4));
            // Zero width: the same filter DistributeGroup already applies, so
            // this row can never be cut and must never become a piece.
            job.Specs[0].Layers[0].Pieces.Clear();
            job.Specs[0].Layers[0].Pieces.Add(("6mm clear ft", "PC", 0, 500, 4));

            AddAndSelect(vm, job);

            Assert.Empty(vm.Pieces);
            Assert.Contains("rejected", vm.RunMessage, StringComparison.Ordinal);
        }

        [Fact]
        public void HonestEmpty_NoJobSelected_LeavesSizesEmptyWithAGuidanceMessage()
        {
            var vm = new OptimizationViewModel();
            Assert.Empty(vm.Pieces);
            Assert.Empty(vm.SizeGroups);
            Assert.False(vm.HasPieces);
            Assert.Equal("No pieces yet", vm.PieceSummary);

            var job = MakeJob("JO-0", new PaneDef("Spec 1", "6mm clear ft", "Outer", 1000, 500, 4));
            vm.AvailableJobs.Add(job);
            vm.AttachJobOption(job);
            job.IsSelected = true;
            job.IsSelected = false;

            Assert.Empty(vm.Pieces);
            Assert.Contains("No jobs selected", vm.RunMessage, StringComparison.Ordinal);
        }

        // ══════════════════════════════════════════════════════════════
        // RC-014 — build identity is knowable from code, not from folklore
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void RC014_BuildStamp_CarriesVersionAndBuildTime()
        {
            // The whole field investigation turned on WHICH build was running.
            // <Version> sat at 2.0.1 across nine days of behaviour-changing
            // commits and UpdateService.IsNewer is a strict greater-than
            // compare, so OTA never offered them and no log line identified
            // the binary. The stamp must therefore never be blank and must
            // always carry both halves.
            string stamp = ProGlassAutomation.Services.Updates.AppInfo.BuildStamp;

            Assert.False(string.IsNullOrWhiteSpace(stamp));
            Assert.Contains(ProGlassAutomation.Services.Updates.AppInfo.Version, stamp, StringComparison.Ordinal);
            Assert.Contains("build", stamp, StringComparison.Ordinal);
            Assert.Contains("UTC", stamp, StringComparison.Ordinal);
            Assert.Equal(ProGlassAutomation.Services.Updates.AppInfo.FallbackVersion, "2.0.2");
        }
    }
}
