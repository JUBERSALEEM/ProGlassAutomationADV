// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/DeliveryNote/DeliveryNoteModuleAuditTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Delivery Note — deep senior audit (Aug 2026)
//
// Hermetic source-file tests that lock the fixes from the Delivery Note
// deep audit. They do NOT boot WPF or touch a DB — they read the source
// relative to the project root and assert:
//   1. The repository projects every list query to DeliveryNoteListItem at
//      the SQL level (a shared EF projection) so FullSnapshotJson is never
//      read for a list.
//   2. GetPendingQtyJobsAsync no longer runs a per-job-order query inside
//      the loop (the N+1 fix).
//   3. The shell VM disposes its children and no longer double-unsubscribes.
//   4. The list VM is IDisposable and detaches DocumentLifecycle (leak fix).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.DeliveryNote
{
    public class DeliveryNoteModuleAuditTests
    {
        [Fact]
        public void Repository_projects_every_list_query_at_sql_level()
        {
            var root = LocateProjectRoot();
            var repo = File.ReadAllText(Path.Combine(root,
                "Services/DeliveryNote/Repository/DeliveryNoteRepository.cs"));

            // A shared EF-translatable projection that excludes the blob.
            Assert.Contains("ToListItemProjection", repo);
            Assert.Contains("System.Linq.Expressions.Expression", repo);
            Assert.Contains("AsNoTracking", repo);
            // Every list method must use it (no in-memory full-entity select
            // that reads the snapshot blob).
            int projectionUses = Count(repo, ".Select(ToListItemProjection)");
            Assert.True(projectionUses >= 6,
                $"Expected >= 6 list methods using the SQL projection, found {projectionUses}.");
            // The old full-entity in-memory mapper must be gone.
            Assert.DoesNotContain("private static DeliveryNoteListItem ToListItem(", repo);
        }

        [Fact]
        public void Pending_qty_query_does_not_fire_a_query_per_job_order()
        {
            var root = LocateProjectRoot();
            var repo = File.ReadAllText(Path.Combine(root,
                "Services/DeliveryNote/Repository/DeliveryNoteRepository.cs"));

            // The method must batch-load the latest DN per job order in one
            // query (a "latestByJo" dictionary), not run FirstOrDefaultAsync
            // inside the per-jo loop.
            var block = Extraction.Between(
                repo, "public async Task<List<PendingQtySummary>> GetPendingQtyJobsAsync",
                "public async Task<List<DeliveryNoteListItem>> GetReworkAsync");
            Assert.NotNull(block);
            Assert.Contains("latestByJo", block!);
            Assert.Contains("latestByJo.TryGetValue(joId", block!);
        }

        [Fact]
        public void Shell_vm_disposes_children_and_does_not_double_unsubscribe()
        {
            var root = LocateProjectRoot();
            var shell = File.ReadAllText(Path.Combine(root,
                "ViewModels/DeliveryNote/DeliveryNoteViewModel.cs"));

            Assert.Contains("ListVM?.Dispose()", shell);
            Assert.Contains("ActivityVM?.Dispose()", shell);
            // The double-unsubscribe copy-paste bug must not return: there
            // must be exactly ONE DataSeeded unsubscribe in Dispose.
            int unsub = Count(shell, "DataSeeded -= _dataSeededHandler");
            Assert.Equal(1, unsub);
        }

        [Fact]
        public void List_vm_is_disposable_and_detaches_lifecycle()
        {
            var root = LocateProjectRoot();
            var vm = File.ReadAllText(Path.Combine(root,
                "ViewModels/DeliveryNote/DeliveryNoteList/DeliveryNoteListViewModel.cs"));

            Assert.Contains("System.IDisposable", vm);
            Assert.Contains("DocumentLifecycle.Subscribe(LoadAsync)", vm);
            Assert.Contains("DocumentLifecycle.Unsubscribe(LoadAsync)", vm);
            Assert.Contains("public void Dispose()", vm);
        }

        [Fact]
        public void Main_vm_disposes_all_module_shells()
        {
            var root = LocateProjectRoot();
            var main = File.ReadAllText(Path.Combine(root,
                "ViewModels/MainViewModel.cs"));

            Assert.Contains("DeliveryNoteVM?.Dispose()", main);
            Assert.Contains("ProformaInvoiceVM?.Dispose()", main);
            Assert.Contains("TaxInvoiceVM?.Dispose()", main);
        }

        private static int Count(string s, string needle)
        {
            int c = 0, i = 0;
            while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { c++; i += needle.Length; }
            return c;
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName, "Views", "Settings",
                    "SettingsSectionStyles.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }

    internal static class Extraction
    {
        public static string? Between(string source, string start, string end)
        {
            int s = source.IndexOf(start, StringComparison.Ordinal);
            if (s < 0) return null;
            int e = source.IndexOf(end, s, StringComparison.Ordinal);
            if (e < 0) return null;
            return source.Substring(s, e - s);
        }
    }
}
