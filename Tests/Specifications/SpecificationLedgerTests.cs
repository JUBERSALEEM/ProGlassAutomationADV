// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Specifications/SpecificationLedgerTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Specifications — ledger ground-truth reconciliation
//
// WHAT IS ACTUALLY BEING ASSERTED
// ───────────────────────────────
// The ledger contract, end to end against real SQLite files:
//
//   • Counts come from SAVED invoices' Step 5 groups — 2 PIs × 2 specs =
//     4 verified uses — never from Step 4 APPLY presses.
//   • A third PI reusing the same specs increments their counts.
//   • Usage rows for deleted (or never-saved draft) PIs are purged.
//   • Editing a PI to drop/reprice a spec updates the ledger.
//   • A spec quoted on a saved PI but missing from the catalogue is
//     recreated from verified truth (freehand Step 5 entry).
//   • A corrupt snapshot fails one invoice, never the whole pass.
//   • Last-activity reporting names the latest verified use.
//
// Hermetic: AppDataRoot points at a per-test temp folder, App.Invoices /
// App.Specs are pointed at real repositories over that folder (saved and
// restored via reflection), and the folder is deleted in Dispose. Joins
// the shared non-parallel "HermeticDb" collection so no other folder-
// redirecting test can flip the static mid-run.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
using ProGlassAutomation.Services.Specifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace ProGlassAutomation.Tests.Specifications
{
    [CollectionDefinition("HermeticDb", DisableParallelization = true)]
    public sealed class HermeticDbCollection
    {
        // xUnit anchor only: classes in [Collection("HermeticDb")] never
        // run in parallel with each other, because they share the
        // AppDataRoot.TestFolderOverride + App.* repository statics.
    }

    [Collection("HermeticDb")]
    public sealed class SpecificationLedgerTests : IDisposable
    {
        private const string SpecA = "6mm Clear FT Glass";
        private const string SpecB = "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass";

        private readonly string _tempFolder;
        private readonly string? _savedFolderOverride;
        private readonly object? _savedInvoices;
        private readonly object? _savedSpecs;

        public SpecificationLedgerTests()
        {
            _tempFolder = Path.Combine(
                Path.GetTempPath(),
                "pga_spec_ledger_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);

            _savedFolderOverride = AppDataRoot.TestFolderOverride;
            AppDataRoot.TestFolderOverride = _tempFolder;

            // Real repositories over the temp folder, so the ledger reads
            // exactly what production reads — just hermetically.
            _savedInvoices = GetAppRepo("Invoices");
            _savedSpecs = GetAppRepo("Specs");
            SetAppRepo("Invoices", new ProformaInvoiceRepository());
            SetAppRepo("Specs", new SpecificationsRepository());

            using (var db = new ProformaInvoicesDbContext())
                db.Database.EnsureCreated();
            using (var db = new SpecificationsDbContext())
                db.Database.EnsureCreated();

            // Lets the ledger's audit summary write somewhere real instead
            // of into the void (failures would be swallowed either way).
            try { AuditService.InitializeAsync().GetAwaiter().GetResult(); }
            catch { /* hermetic best-effort */ }
        }

        public void Dispose()
        {
            SetAppRepo("Invoices", _savedInvoices);
            SetAppRepo("Specs", _savedSpecs);
            AppDataRoot.TestFolderOverride = _savedFolderOverride;
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_tempFolder, recursive: true); }
            catch { /* best effort — a lingering temp folder fails nothing */ }
        }

        // ── 1. Counts come from saved invoices, not Step 4 ────────────

        [Fact]
        public async Task Counts_come_from_saved_invoices_not_step4()
        {
            // Arrange: catalogue pre-inflated the way Step 4 APPLYs used to
            // (UsageCount=99 on a spec quoted twice), plus phantom usage
            // rows for a deleted PI and an unsaved draft.
            SeedCatalogueRow("SGU", SpecA, price: 100m, usageCount: 99);
            SeedCatalogueRow("DGU", SpecB, price: 200m, usageCount: 5);
            SeedUsageRow(specId: 1, "SGU", SpecA, "PI-DELETED-001", price: 100m);
            SeedUsageRow(specId: 1, "SGU", SpecA, "", price: 100m); // draft

            SeedInvoice("PI-2026-001", "Acme Glass LLC", "Alpha",
                new DateTime(2026, 1, 10),
                ("SGU", SpecA, 100m),
                ("DGU", SpecB, 200m));
            SeedInvoice("PI-2026-002", "Beta Traders", "Beta",
                new DateTime(2026, 2, 20),
                ("SGU", SpecA, 110m));

            // Act.
            await SpecificationLedgerService.ReconcileAsync();

            // Assert: 2 PIs quote A (twice total), 1 quotes B — the 99/5
            // APPLY-inflated counts are healed to verified truth.
            var specs = ReadCatalogue();
            Assert.Equal(2, specs.Single(s => s.SpecName == SpecA).UsageCount);
            Assert.Equal(1, specs.Single(s => s.SpecName == SpecB).UsageCount);

            var usages = ReadUsages();
            Assert.DoesNotContain(usages, u => u.ProformaId == "PI-DELETED-001");
            Assert.DoesNotContain(usages, u => string.IsNullOrWhiteSpace(u.ProformaId));
            Assert.Equal(3, usages.Count);

            // Per-invoice quoted rates survive: A's two uses at two rates.
            var aRates = usages
                .Where(u => u.SpecName == SpecA)
                .Select(u => u.PricePerSqm)
                .OrderBy(p => p)
                .ToList();
            Assert.Equal(new[] { 100m, 110m }, aRates);

            // Company travels from the saved invoice row, not draft context.
            Assert.Contains(usages, u =>
                u.ProformaId == "PI-2026-002" && u.CompanyName == "Beta Traders");
        }

        // ── 2. A third PI reusing the specs increments them ────────────

        [Fact]
        public async Task Third_invoice_reusing_specs_increments_counts()
        {
            SeedInvoice("PI-2026-001", "Acme", "Alpha",
                new DateTime(2026, 1, 10), ("SGU", SpecA, 100m));
            await SpecificationLedgerService.ReconcileAsync();
            Assert.Equal(1, ReadCatalogue().Single(s => s.SpecName == SpecA).UsageCount);

            SeedInvoice("PI-2026-002", "Beta", "Beta",
                new DateTime(2026, 2, 20), ("SGU", SpecA, 100m));
            await SpecificationLedgerService.ReconcileAsync();

            Assert.Equal(2, ReadCatalogue().Single(s => s.SpecName == SpecA).UsageCount);
            Assert.Equal(2, ReadUsages().Count);
        }

        // ── 3. Editing a PI drops what it no longer quotes ─────────────

        [Fact]
        public async Task Edited_invoice_drops_removed_spec_and_keeps_rest()
        {
            SeedInvoice("PI-2026-001", "Acme", "Alpha",
                new DateTime(2026, 1, 10),
                ("SGU", SpecA, 100m),
                ("DGU", SpecB, 200m));
            await SpecificationLedgerService.ReconcileAsync();
            Assert.Equal(1, ReadCatalogue().Single(s => s.SpecName == SpecB).UsageCount);

            // Edit: PI-001 no longer quotes B.
            OverwriteInvoiceGroups("PI-2026-001", ("SGU", SpecA, 100m));
            await SpecificationLedgerService.ReconcileAsync();

            var specs = ReadCatalogue();
            Assert.Equal(1, specs.Single(s => s.SpecName == SpecA).UsageCount);
            var b = specs.Single(s => s.SpecName == SpecB);
            Assert.Equal(0, b.UsageCount);   // row stays (library), count zeroed
            Assert.Null(b.ProformaId);       // unverified provenance cleared
            Assert.DoesNotContain(ReadUsages(), u => u.SpecName == SpecB);
        }

        // ── 4. Deleting a PI purges its rows ────────────────────────────

        [Fact]
        public async Task Deleted_invoice_purges_its_usage_rows()
        {
            SeedInvoice("PI-2026-001", "Acme", "Alpha",
                new DateTime(2026, 1, 10), ("SGU", SpecA, 100m));
            SeedInvoice("PI-2026-002", "Beta", "Beta",
                new DateTime(2026, 2, 20), ("SGU", SpecA, 100m));
            await SpecificationLedgerService.ReconcileAsync();
            Assert.Equal(2, ReadUsages().Count);

            using (var db = new ProformaInvoicesDbContext())
            {
                var gone = db.ProformaInvoices
                    .Single(i => i.ProformaId == "PI-2026-002");
                db.ProformaInvoices.Remove(gone);
                db.SaveChanges();
            }

            await SpecificationLedgerService.ReconcileAsync();

            Assert.Equal(1, ReadCatalogue().Single(s => s.SpecName == SpecA).UsageCount);
            var usages = ReadUsages();
            Assert.Single(usages);
            Assert.Equal("PI-2026-001", usages[0].ProformaId);
        }

        // ── 5. Freehand specs are recreated from truth ─────────────────

        [Fact]
        public async Task Spec_missing_from_catalogue_is_recreated_from_invoice()
        {
            // No catalogue row at all — the operator typed the name freehand
            // in Step 5 and saved. The ledger recreates it so the Center and
            // the Step 5 dropdown agree on what exists.
            const string freehand = "8mm Clear FT Glass + 1.52mm Clear PVB + 8mm Clear FT Glass";
            SeedInvoice("PI-2026-007", "Acme", "Gamma",
                new DateTime(2026, 3, 1), ("LAM", freehand, 250m));

            await SpecificationLedgerService.ReconcileAsync();

            var row = ReadCatalogue().Single(s => s.SpecName == freehand);
            Assert.Equal("LAM", row.GlassType);
            Assert.Equal(250m, row.PricePerSqm);
            Assert.Equal(1, row.UsageCount);
            Assert.Equal("PI-2026-007", row.ProformaId);
        }

        // ── 6. Corrupt snapshots fail one invoice, not the pass ─────────

        [Fact]
        public async Task Corrupt_snapshot_does_not_abort_the_pass()
        {
            SeedInvoice("PI-2026-001", "Acme", "Alpha",
                new DateTime(2026, 1, 10), ("SGU", SpecA, 100m));

            using (var db = new ProformaInvoicesDbContext())
            {
                db.ProformaInvoices.Add(new SavedProformaInvoice
                {
                    ProformaId = "PI-CORRUPT",
                    CompanyName = "Corrupt Co",
                    Status = "Draft",
                    FullSnapshotJson = "{ this is not json",
                    CreatedAt = new DateTime(2026, 1, 5),
                    UpdatedAt = new DateTime(2026, 1, 5)
                });
                db.SaveChanges();
            }

            // Must complete (never throws) and still verify the good invoice.
            await SpecificationLedgerService.ReconcileAsync();

            Assert.Equal(1, ReadCatalogue().Single(s => s.SpecName == SpecA).UsageCount);
            Assert.DoesNotContain(ReadUsages(), u => u.ProformaId == "PI-CORRUPT");
        }

        // ── 7. Last activity names the latest verified use ─────────────

        [Fact]
        public async Task Last_activity_reports_latest_verified_use()
        {
            SeedInvoice("PI-2026-001", "Acme Glass LLC", "Alpha",
                new DateTime(2026, 1, 10), ("SGU", SpecA, 100m));
            SeedInvoice("PI-2026-002", "Beta Traders", "Beta",
                new DateTime(2026, 2, 20), ("DGU", SpecB, 200m));

            await SpecificationLedgerService.ReconcileAsync();

            var last = await SpecificationLedgerService.GetLastActivityAsync();
            Assert.NotNull(last);
            Assert.Equal(SpecB, last!.SpecName);
            Assert.Equal("PI-2026-002", last.ProformaId);
            Assert.Equal("Beta Traders", last.CompanyName);
            Assert.Equal("PI-2026-002 · Beta Traders", last.InvoiceContextDisplay);
        }

        [Fact]
        public async Task Last_activity_is_null_when_nothing_is_quoted()
        {
            // An empty ledger (no invoices at all) reports null, not fiction.
            var last = await SpecificationLedgerService.GetLastActivityAsync();
            Assert.Null(last);
        }

        // ── Helpers ────────────────────────────────────────────────────

        private static object? GetAppRepo(string propertyName) =>
            typeof(ProGlassAutomation.App)
                .GetProperty(propertyName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(null);

        private static void SetAppRepo(string propertyName, object? value) =>
            typeof(ProGlassAutomation.App)
                .GetProperty(propertyName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetSetMethod(nonPublic: true)
                ?.Invoke(null, new[] { value });

        private static void SeedCatalogueRow(
            string glassType, string specName, decimal price, int usageCount)
        {
            using var db = new SpecificationsDbContext();
            db.Specifications.Add(new SavedSpecification
            {
                GlassType = glassType,
                SpecName = specName,
                PricePerSqm = price,
                CreatedAt = new DateTime(2026, 1, 1),
                LastUsedAt = new DateTime(2026, 1, 2),
                UsageCount = usageCount
            });
            db.SaveChanges();
        }

        private static void SeedUsageRow(
            int specId, string glassType, string specName,
            string proformaId, decimal price)
        {
            using var db = new SpecificationsDbContext();
            db.SpecificationUsages.Add(new SpecificationUsage
            {
                SpecificationId = specId,
                SpecName = specName,
                GlassType = glassType,
                ProformaId = proformaId,
                PricePerSqm = price,
                UsedAt = new DateTime(2026, 1, 3)
            });
            db.SaveChanges();
        }

        private static void SeedInvoice(
            string proformaId, string company, string customer, DateTime stamp,
            params (string GlassType, string SpecName, decimal Rate)[] groups)
        {
            var snapshot = new ProformaInvoiceSnapshot
            {
                SpecGroups = groups.Select((g, i) => new SpecGroupSnapshot
                {
                    GroupNumber = i + 1,
                    SelectedGlassType = g.GlassType,
                    SelectedSpecName = g.SpecName,
                    BaseRate = g.Rate,
                    OuterSheetWidthMm = 3210,
                    OuterSheetHeightMm = 2250
                }).ToList()
            };

            using var db = new ProformaInvoicesDbContext();
            db.ProformaInvoices.Add(new SavedProformaInvoice
            {
                ProformaId = proformaId,
                CompanyName = company,
                CustomerName = customer,
                ProjectName = customer + " Project",
                Status = "Draft",
                IssueDate = stamp,
                ValidUntil = stamp.AddDays(7),
                FullSnapshotJson = ProformaInvoiceMapper.SerializeSnapshot(snapshot),
                CreatedAt = stamp,
                UpdatedAt = stamp
            });
            db.SaveChanges();
        }

        private static void OverwriteInvoiceGroups(
            string proformaId,
            params (string GlassType, string SpecName, decimal Rate)[] groups)
        {
            using var db = new ProformaInvoicesDbContext();
            var entity = db.ProformaInvoices
                .Single(i => i.ProformaId == proformaId);
            var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);
            snapshot.SpecGroups = groups.Select((g, i) => new SpecGroupSnapshot
            {
                GroupNumber = i + 1,
                SelectedGlassType = g.GlassType,
                SelectedSpecName = g.SpecName,
                BaseRate = g.Rate
            }).ToList();
            entity.FullSnapshotJson =
                ProformaInvoiceMapper.SerializeSnapshot(snapshot);
            entity.UpdatedAt = new DateTime(2026, 5, 5);
            db.SaveChanges();
        }

        private static List<SavedSpecification> ReadCatalogue()
        {
            using var db = new SpecificationsDbContext();
            return db.Specifications.AsNoTracking().ToList();
        }

        private static List<SpecificationUsage> ReadUsages()
        {
            using var db = new SpecificationsDbContext();
            return db.SpecificationUsages.AsNoTracking().ToList();
        }
    }
}
