// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Specifications/SpecificationLedgerContractTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Specifications — ledger ground-truth architecture contracts
//
// Source-level pins for the facts the behavioural tests cannot see: WHICH
// code is (and is not) allowed to write counts, history and provenance.
// If a future edit re-introduces a Step 4 usage write, a count bump in
// the repository, or an unvalidated stored-JSON read, these fail with the
// file and the forbidden call named — deliberately, so the fix is a
// conscious architecture decision, not a silent regression.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Tests.TestSupport;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Specifications
{
    public sealed class SpecificationLedgerContractTests
    {
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

        // ── Step 4 writes the library, never usage ────────────────────

        [Fact]
        public void Step4_apply_writes_library_row_but_no_usage()
        {
            string root = LocateProjectRoot();
            string step4 = SourceFamily.Read(root,
                "ViewModels/ProformaInvoice/Step4PricingEstimator/PricingEstimatorViewModel.cs");

            // The library write stays (Step 5 must offer the spec pre-save).
            Assert.Contains("SaveOrUpdateAsync", step4);

            // The phantom write is gone: an APPLY press is not a use until
            // its invoice is saved. The ledger derives history instead.
            Assert.DoesNotContain("RecordUsageAsync", step4);
            Assert.DoesNotContain("MarkAsUsedAsync", step4);
        }

        [Fact]
        public void Repository_save_never_bumps_counts()
        {
            string root = LocateProjectRoot();
            string repo = File.ReadAllText(Path.Combine(root,
                "Services/ProformaInvoice/SpecificationsRepository/SpecificationsRepository.cs"));

            // SaveOrUpdateAsync must not touch the existing row's
            // UsageCount/LastUsedAt — the ledger owns both — and new rows
            // start at 0 (built is not used). Pinned as exact code shapes
            // so the explanatory comments cannot trip the assertion.
            // (MarkAsUsedAsync's legacy bump is the only remaining
            // increment, and it is interface-marked Obsolete.)
            Assert.DoesNotContain("existing.UsageCount", repo);
            Assert.DoesNotContain("existing.LastUsedAt", repo);
            Assert.DoesNotContain("UsageCount = 1", repo);
            Assert.Contains("UsageCount = 0", repo);
        }

        [Fact]
        public void MarkAsUsed_is_obsolete_at_the_contract()
        {
            string root = LocateProjectRoot();
            string contract = File.ReadAllText(Path.Combine(root,
                "Services/ProformaInvoice/SpecificationsRepository/ISpecificationsRepository.cs"));

            Assert.Contains("Obsolete", contract);
            Assert.Contains("MarkAsUsedAsync", contract);
        }

        // ── Ledger lifecycle wiring ───────────────────────────────────

        [Fact]
        public void Ledger_runs_on_startup_and_every_invoice_write()
        {
            string root = LocateProjectRoot();
            string ledger = File.ReadAllText(Path.Combine(root,
                "Services/Specifications/SpecificationLedgerService.cs"));

            // Ongoing sync: every invoice insert/update/delete reconciles.
            Assert.Contains("SavedInvoicesChanged += OnInvoicesChanged", ledger);
            Assert.Contains("SavedInvoicesChanged -= OnInvoicesChanged", ledger);

            // Startup heal + shutdown detach are wired in the App family.
            string app = SourceFamily.Read(root, "App.xaml.cs");
            Assert.Contains("SpecificationLedgerService.Start()", app);
            Assert.Contains("SpecificationLedgerService.Stop()", app);
        }

        [Fact]
        public void Ledger_derives_from_saved_snapshots_and_purges_orphans()
        {
            string root = LocateProjectRoot();
            string ledger = File.ReadAllText(Path.Combine(root,
                "Services/Specifications/SpecificationLedgerService.cs"));

            // Ground truth: saved invoices → snapshot Step-5 groups.
            Assert.Contains("GetAllAsync", ledger);
            Assert.Contains("DeserializeSnapshot", ledger);
            Assert.Contains("SpecGroups", ledger);

            // Orphans (deleted PIs + never-saved drafts) are purged.
            Assert.Contains("RemoveRange(orphans)", ledger);

            // The estimator's rate is never clobbered by old saved rates.
            Assert.Contains("PricePerSqm is", ledger);
        }

        // ── Center reads verified truth only ──────────────────────────

        [Fact]
        public void Center_listens_to_both_signals_and_shows_last_activity()
        {
            string root = LocateProjectRoot();
            string vm = File.ReadAllText(Path.Combine(root,
                "ViewModels/Specifications/SpecificationsCenterViewModel.cs"));

            Assert.Contains("SpecCatalogNotifier.Changed += OnCatalogChanged", vm);
            Assert.Contains("SavedInvoicesChanged += OnInvoicesChanged", vm);
            Assert.Contains("SavedInvoicesChanged -= OnInvoicesChanged", vm);

            // Last-activity stat: spec + invoice + company, ledger-sourced.
            Assert.Contains("GetLastActivityAsync", vm);
            Assert.Contains("LastActivitySpec", vm);
            Assert.Contains("LastActivityInvoice", vm);

            // Manual resync path exists (Refresh → reconcile → reload).
            Assert.Contains("RefreshWithReconcileAsync", vm);
        }

        // ── No blind reads ────────────────────────────────────────────

        [Fact]
        public void Pane_resolver_validates_stored_sizes_against_live_store()
        {
            string root = LocateProjectRoot();
            string resolver = File.ReadAllText(Path.Combine(root,
                "Services/Specifications/SpecPaneResolver.cs"));

            // Stored JSON is a hint validated per pane — never returned blind.
            Assert.Contains("MatchSheetsAsync(parsed, stored)", resolver);
            Assert.Contains("storedPreference", resolver);
            Assert.Contains("still stocks", resolver);
        }

        // ── Password policy is the single home ────────────────────────

        [Fact]
        public void Every_password_choice_validates_through_the_policy()
        {
            string root = LocateProjectRoot();

            // Service boundary (create / change / admin reset).
            string audit = SourceFamily.Read(root,
                "Services/Audit/AuditService.cs");
            Assert.Contains("PasswordPolicy", audit);

            // UI pre-checks mirror the same home (User Center, first-run,
            // reset, forced rotation) so messages cannot drift.
            string userCenter = File.ReadAllText(Path.Combine(root,
                "ViewModels/UserCenter/UserCenterViewModel.cs"));
            Assert.Contains("PasswordPolicy", userCenter);

            string firstRun = File.ReadAllText(Path.Combine(root,
                "Views/Login/FirstRunSetupPanel.xaml.cs"));
            Assert.Contains("PasswordPolicy", firstRun);

            string reset = File.ReadAllText(Path.Combine(root,
                "Views/Login/ResetPasswordWindow.xaml.cs"));
            Assert.Contains("PasswordPolicy", reset);

            string force = File.ReadAllText(Path.Combine(root,
                "Views/Login/ForcePasswordChangeWindow.xaml.cs"));
            Assert.Contains("PasswordPolicy", force);
        }

        [Fact]
        public void Password_minimum_is_six_and_single_sourced()
        {
            string root = LocateProjectRoot();
            string policy = File.ReadAllText(Path.Combine(root,
                "Services/Security/PasswordPolicy.cs"));
            string hasher = File.ReadAllText(Path.Combine(root,
                "Services/Security/PasswordHasher.cs"));

            Assert.Contains("MinLength = 6", policy);
            Assert.Contains("MinLength = PasswordPolicy.MinLength", hasher);
        }
    }
}
