// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Architecture/PaginationRatchetTests.cs
// PROJECT: ProGlassAutomation.Tests
// PHASE 2 — pagination ratchet (source scan, following the established
// pattern of StatusVocabularySourceTests and ReadOnlyGateCoverageSourceTests).
//
// WHY THIS TEST EXISTS
// ────────────────────
// 25 unfiltered GetAllAsync() call sites across 18 ViewModels load a whole
// materialise an entire table to display or count part of it. That is
// invisible at 500 records and a multi-second UI freeze at 50,000 — on a
// machine that is also driving a cutting saw.
//
// Documenting that in a README does not stop the 29th call site from
// appearing. This test does.
//
// HOW THE RATCHET WORKS
// ─────────────────────
// The allowlist below is the exact set of offenders as of 2026-09-08. The
// test fails in BOTH directions:
//
//   • a file calls GetAllAsync() and is NOT allow-listed  → paginate it
//   • a file IS allow-listed but no longer offends        → remove it here
//
// So the list can only shrink. Phase 2 of docs/REMEDIATION_PLAN.md is
// complete when it is empty.
//
// WHAT COUNTS AS A CALL
// ─────────────────────
// Comment lines are excluded deliberately. OverviewDashboardViewModel and
// PiDashboardViewModel both *mention* GetAllAsync in comments describing a
// migration that already happened; counting those produced a false figure
// of 23 offending files when the real number was 20. A ratchet built on a
// wrong baseline is worse than no ratchet.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Architecture
{
    public sealed class PaginationRatchetTests
    {
        /// <summary>
        /// Known offenders, relative to ViewModels/, using forward slashes.
        /// Remove an entry as soon as its ViewModel is paginated — the test
        /// will fail until you do, which is the point.
        /// </summary>
        private static readonly HashSet<string> AllowList = new(StringComparer.Ordinal)
        {
            // ── Phase 3 targets: dashboards loading whole tables to compute
            //    four numbers. IDashboardMetricsService already exists and
            //    Overview was migrated to it; these four were not.
            "Dashboard/DeliveryNote/DnDashboard/DnDashboardViewModel.cs",
            "Dashboard/JobOrder/JoDashboard/JoDashboardViewModel.cs",
            "Dashboard/SheetStore/SheetStoreDashboardViewModel.cs",

            // StDashboardViewModel.cs was here until 2026-09-08, when Phase 3
            // moved its aggregates to IDashboardMetricsService
            // .GetStatementMetricsAsync. Removing it is the ratchet working:
            // the stale-entries test fails until a cleaned-up file is dropped
            // from this list.

            // ── Phase 2 targets: list screens.
            "DeliveryNote/DeliveryNoteList/DeliveryNoteListViewModel.Part1.cs",
            "JobOrder/SavedJobOrdersList/JobOrderProformaPickerViewModel.cs",
            "JobOrder/SavedJobOrdersList/SavedJobOrdersListViewModel.Part1.cs",
            // SpecGroup.Part2.cs was here until 2026-09-08. It calls
            // GetAllAsync(selectedType) — already scoped to one spec type, so
            // it was never a whole-table load. See CallsGetAll.
            "Reports/OrderReport/OrderReportListViewModel.cs",
            "Reports/StatementGenerator/StatementGeneratorViewModel.Helpers.cs",
            "Reports/StatementList/StatementListViewModel.cs",
            "Shared/GlobalSearchViewModel.cs",
            "SheetStore/AddEditSheetViewModel.cs",
            "SheetStore/SheetStoreHistory/HistoryViewModel.cs",
            "SheetStore/SheetStoreViewModel.Consumables.cs",
            "SheetStore/SheetStoreViewModel.Part1.cs",
            "Specifications/SpecificationsCenterViewModel.cs",
            "TaxInvoice/GodView/TaxInvoiceGodViewViewModel.cs",
            "TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.InitNew.cs",
            "TaxInvoice/TaxInvoiceViewModel.cs",
        };

        [Fact]
        public void No_new_ViewModel_loads_an_entire_table()
        {
            var offenders = FindOffenders();

            var unlisted = offenders.Where(f => !AllowList.Contains(f)).OrderBy(f => f, StringComparer.Ordinal).ToList();

            Assert.True(
                unlisted.Count == 0,
                "These ViewModels call GetAllAsync() and are not allow-listed:" + Environment.NewLine
                + string.Join(Environment.NewLine, unlisted.Select(f => "    ViewModels/" + f)) + Environment.NewLine
                + Environment.NewLine
                + "A list screen must page through GlobalQueryEngine — see" + Environment.NewLine
                + "Services/Inquiry/InquiryRepository.cs and InquiryListViewModel for the pattern." + Environment.NewLine
                + "If this read is a genuine aggregate, route it through IDashboardMetricsService" + Environment.NewLine
                + "or a projection instead of materialising the table.");
        }

        [Fact]
        public void AllowList_has_no_stale_entries()
        {
            var offenders = FindOffenders();

            var stale = AllowList.Where(f => !offenders.Contains(f)).OrderBy(f => f, StringComparer.Ordinal).ToList();

            Assert.True(
                stale.Count == 0,
                "These allow-list entries no longer call GetAllAsync() — the ratchet can shrink:" + Environment.NewLine
                + string.Join(Environment.NewLine, stale.Select(f => "    " + f)) + Environment.NewLine
                + Environment.NewLine
                + "Delete them from AllowList in this file. Phase 2 of" + Environment.NewLine
                + "docs/REMEDIATION_PLAN.md is complete when the list is empty.");
        }

        [Fact]
        public void Offender_count_matches_the_documented_baseline()
        {
            // 20 files / 28 call sites as measured on 2026-09-08. This is a
            // tripwire, not a target: it is expected to be lowered as Phase 2
            // lands, and lowering it is a one-line change in this file.
            var offenders = FindOffenders();
            Assert.Equal(AllowList.Count, offenders.Count);
        }

        // ────────────────────────────────────────────────────────────────

        private static HashSet<string> FindOffenders()
        {
            var root = Root();
            var viewModels = Path.Combine(root, "ViewModels");
            var result = new HashSet<string>(StringComparer.Ordinal);

            if (!Directory.Exists(viewModels))
                return result;

            foreach (var file in Directory.EnumerateFiles(viewModels, "*.cs", SearchOption.AllDirectories))
            {
                if (CallsGetAll(file))
                {
                    var rel = Path.GetRelativePath(viewModels, file).Replace('\\', '/');
                    result.Add(rel);
                }
            }
            return result;
        }

        /// <summary>
        /// An UNFILTERED whole-table load: <c>GetAllAsync()</c> with no
        /// arguments.
        ///
        /// <para>
        /// Narrowed from "any line containing GetAllAsync" on 2026-09-08.
        /// <c>SpecGroup.Part2.cs</c> calls <c>GetAllAsync(selectedType)</c>,
        /// which is already scoped to one spec type — it is not a whole-table
        /// scan and should never have been on this list. Matching the bare
        /// name also produced a false sense of scope: 28 hits looked like 28
        /// tables to paginate, when two of them were one filtered query.
        /// </para>
        /// </summary>
        private static bool CallsGetAll(string file)
        {
            foreach (var raw in File.ReadLines(file))
            {
                var line = raw.TrimStart();

                // A comment is not a call. Including these inflated the
                // baseline from 20 files to 23.
                if (line.StartsWith("//", StringComparison.Ordinal)) continue;
                if (line.StartsWith("*", StringComparison.Ordinal)) continue;
                if (line.StartsWith("/*", StringComparison.Ordinal)) continue;

                if (UnfilteredGetAll.IsMatch(line)) return true;
            }
            return false;
        }

        /// <summary>GetAllAsync with an empty argument list.</summary>
        private static readonly Regex UnfilteredGetAll =
            new(@"GetAllAsync\s*\(\s*\)", RegexOptions.Compiled);

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }
}
