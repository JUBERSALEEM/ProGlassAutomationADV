// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/JobOrder/JobOrderModuleAuditTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Job Order — deep senior audit (Aug 2026)
//
// Hermetic source-file tests that lock the fixes from the Job Order deep
// audit. They do NOT boot WPF or touch a DB — they read the source relative
// to the project root and assert:
//   1. JobOrderViewModel disposes its child VMs (ListVM, ActivityVM) so
//      their static-event subscriptions (DocumentLifecycle, DataSeeded,
//      AuditService.ActivityLogged) are detached — the memory-leak fix.
//   2. SavedJobOrdersListViewModel.Dispose also detaches DocumentLifecycle.
//   3. The repository exposes a narrow list projection (GetListRowsAsync)
//      and a JobOrderListRow model that excludes FullSnapshotJson — the
//      list-view performance fix.
//   4. The list & God-View build rows from that projection.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.JobOrder
{
    public class JobOrderModuleAuditTests
    {
        [Fact]
        public void JobOrder_shell_disposes_child_viewmodels()
        {
            var root = LocateProjectRoot();
            var shell = File.ReadAllText(Path.Combine(root,
                "ViewModels/JobOrder/JobOrderViewModel.cs"));

            Assert.Contains("System.IDisposable", shell);
            Assert.Contains("public void Dispose()", shell);
            Assert.Contains("ListVM?.Dispose()", shell);
            Assert.Contains("ActivityVM?.Dispose()", shell);
        }

        [Fact]
        public void List_viewmodel_detaches_the_static_lifecycle_subscription()
        {
            var root = LocateProjectRoot();
            var vm = File.ReadAllText(Path.Combine(root,
                "ViewModels/JobOrder/SavedJobOrdersList/SavedJobOrdersListViewModel.cs"));

            Assert.Contains("DocumentLifecycle.Subscribe(LoadJobOrdersAsync)", vm);
            // Dispose must detach BOTH static subscribers (data-seeded +
            // lifecycle) or the VM leaks.
            Assert.Contains("SampleDataService.DataSeeded -= _dataSeededHandler", vm);
            Assert.Contains("DocumentLifecycle.Unsubscribe(LoadJobOrdersAsync)", vm);
        }

        [Fact]
        public void Repository_exposes_a_narrow_list_projection()
        {
            var root = LocateProjectRoot();
            var iface = File.ReadAllText(Path.Combine(root,
                "Services/JobOrder/Repository/IJobOrderRepository.cs"));
            var repo = File.ReadAllText(Path.Combine(root,
                "Services/JobOrder/Repository/JobOrderRepository.cs"));
            var model = File.ReadAllText(Path.Combine(root,
                "Models/JobOrder/SavedJobOrder/JobOrderListRow.cs"));

            Assert.Contains("GetListRowsAsync", iface);
            Assert.Contains("GetListRowsAsync", repo);
            // The projection must NOT load the snapshot blob.
            Assert.Contains("AsNoTracking", repo);
            var projectionBlock = repo.Substring(repo.IndexOf("GetListRowsAsync"),
                Math.Min(1200, repo.Length - repo.IndexOf("GetListRowsAsync")));
            Assert.DoesNotContain("FullSnapshotJson", projectionBlock);

            Assert.Contains("class JobOrderListRow", model);
            Assert.Contains("Excludes FullSnapshotJson", model);
        }

        [Fact]
        public void List_and_god_view_build_rows_from_the_projection()
        {
            var root = LocateProjectRoot();
            var list = File.ReadAllText(Path.Combine(root,
                "ViewModels/JobOrder/SavedJobOrdersList/SavedJobOrdersListViewModel.cs"));
            var god = File.ReadAllText(Path.Combine(root,
                "ViewModels/JobOrder/GodView/JobOrderGodViewViewModel.cs"));
            var rowVm = File.ReadAllText(Path.Combine(root,
                "ViewModels/JobOrder/SavedJobOrdersList/JobOrderRowViewModel.cs"));

            Assert.Contains("GetListRowsAsync", list);
            Assert.Contains("new JobOrderRowViewModel(", list);
            Assert.Contains("GetListRowsAsync", god);
            Assert.Contains("JobOrderListRow", rowVm);
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
}
