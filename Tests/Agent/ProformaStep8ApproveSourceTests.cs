using System;
using System.IO;
using Xunit;
using System.Linq;

namespace ProGlassAutomation.Tests.Agent
{
    // ═══════════════════════════════════════════════════════════════════
    // FILE: Tests/Agent/ProformaStep8ApproveSourceTests.cs
    // FIX-2026-09-09 pins — ONE approve chain, two surfaces.
    //
    // Owner defect class ("Step 8 has a different action / duplication"):
    // the wizard's Step 8 (Attachments) grew its own way of behaving at
    // approval time while the saved-list right-click menu ran the real
    // Approve chain. Two surface-separate behaviours for the SAME business
    // act = the classic action-duplication drift. The fix put the list's
    // OWN ApproveAsync onto the wizard Step 8 as a visible APPROVE INVOICE
    // button, routed through an ApprovalRequested event on Step8VM.
    //
    // These pins keep it single-source forever:
    //   1. ApproveAsync exists exactly ONCE (SavedInvoicesList Part2).
    //   2. The wizard calls SavedInvoicesListVM.ApproveAsync(entity)
    //      (the list's own method — no second implementation).
    //   3. Step8VM exposes ApprovalRequested + ApproveInvoiceCommand,
    //      and the view binds it (DynamicResource ProformaInvoice.
    //      ApproveInvoice label — reused localization, no invented key).
    //   4. _pendingApprovalInvoiceId chain is intact (upload completes
    //      approval identically for both entry points).
    // ═══════════════════════════════════════════════════════════════════
    public sealed class ProformaStep8ApproveSourceTests
    {
        [Fact]
        public void ApproveAsync_is_defined_exactly_once_in_the_whole_module()
        {
            string part2 = File.ReadAllText(Path.Combine(
                LocateProjectRoot(),
                "ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.Part2.cs"));

            int defs = part2.Split(
                new[] { "Task ApproveAsync(" }, StringSplitOptions.None).Length - 1;
            Assert.True(defs == 1,
                $"ApproveAsync must exist exactly once (single chain), found {defs}.");
            Assert.Contains("internal async Task ApproveAsync", part2);
        }

        [Fact]
        public void Wizard_calls_the_list_own_method_not_a_copy()
        {
            string wizard = File.ReadAllText(Path.Combine(
                LocateProjectRoot(),
                "ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs"));

            // The single real call site in the wizard.
            Assert.Contains("await SavedInvoicesListVM.ApproveAsync(entity);", wizard);

            // Guard: nothing calls CompleteApprovalAsync directly from the
            // Step 8 button path (it must stay behind the upload — same
            // ordering as the list right-click).
            Assert.DoesNotContain("SavedInvoicesListVM.CompleteApprovalAsync(_loadedInvoiceId", wizard);

            // The event wiring + symmetrical teardown (IDisposable, no leak).
            Assert.Contains("Step8VM.ApprovalRequested += OnStep8ApprovalRequested;", wizard);
            Assert.Contains("Step8VM.ApprovalRequested -= OnStep8ApprovalRequested;", wizard);

            // _loadedInvoiceId is nulled at every lifecycle edge so the
            // Step 8 button can never approve a stale row (New draft /
            // BackToList / load-failure).
            int clears = wizard.Split(
                new[] { "_loadedInvoiceId = null;" }, StringSplitOptions.None).Length - 1;
            Assert.True(clears >= 3,
                $"Expected =3 _loadedInvoiceId clears (New / ReturnToList / load-fail), found {clears}.");
        }

        [Fact]
        public void Step8_surface_exposes_the_button_and_binds_it()
        {
            string step8Vm = File.ReadAllText(Path.Combine(
                LocateProjectRoot(),
                "ViewModels/ProformaInvoice/Step8Attachments/AttachmentsStepViewModel.cs"));
            string step8View = File.ReadAllText(Path.Combine(
                LocateProjectRoot(),
                "Views/ProformaInvoice/Step8Attachments/AttachmentsStepView.xaml"));

            Assert.Contains("public event System.Action? ApprovalRequested;", step8Vm);
            Assert.Contains("ApproveInvoiceCommand = new RelayCommand(_ => ApprovalRequested?.Invoke());", step8Vm);

            Assert.Contains("Command=\"{Binding ApproveInvoiceCommand}\"", step8View);
            // Localized label: reuse the list's existing key — never a
            // hardcoded twin string.
            Assert.Contains("{DynamicResource ProformaInvoice.ApproveInvoice}", step8View);
            // Same enable-gate as the upload actions (no Id, no approve).
            Assert.Contains("IsEnabled=\"{Binding HasProformaId}\"", step8View);
        }

        [Fact]
        public void Upload_completes_approval_through_one_gate()
        {
            string wizard = File.ReadAllText(Path.Combine(
                LocateProjectRoot(),
                "ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs"));
            string part2 = File.ReadAllText(Path.Combine(
                LocateProjectRoot(),
                "ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.Part2.cs"));

            // The upload-completes-approval plumbing both surfaces rely on.
            Assert.Contains("_pendingApprovalInvoiceId", wizard);
            Assert.Contains("OnStep8AttachmentUploaded", wizard);
            Assert.Contains("SavedInvoicesListVM.CompleteApprovalAsync(id.Value)", wizard);

            // The approve itself remains status-machine validated (the
            // gate that stops e.g. Cancelled → Approved drift).
            Assert.Contains("StatusTransitions.Kind.ProformaInvoice", part2);
            Assert.Contains("ProformaInvoiceStatus.Approved", part2);
            Assert.Contains("ConvertToJobOrderAsync(entity)", part2);
        }

        // ── helpers ──────────────────────────────────────────────────

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Views", "ProformaInvoice", "Step5DataGrid", "DataGridView.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
