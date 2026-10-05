// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/MasterData/CompanyUpdateSemanticsTests.cs
// PROJECT: ProGlassAutomation.Tests
// PHASE 4 — regression net for SaveOrUpdateCompanyAsync semantics.
//
// These are REAL repository tests, not mocks:
//   MasterDataDbContext gained an options-accepting constructor
//   (self-configuration skipped when options are injected) and
//   MasterDataRepository a context-factory seam, so the suite drives the
//   actual EF Core + SQLite engine against an isolated in-memory database.
//   The real masterdata.db is never touched.
//
// Gate overrides (OperationGuard.Enabled / ReadOnlyGate.Enabled) are the
// documented test seams — restored in Dispose so parallel suites are
// unaffected.
//
// Semantics pinned (all fixed in commit 25618e9 — this suite is what
// stops them regressing):
//   1. Explicit editor path  → latest data wins for ALL fields, blank
//      clears, IsDefault persists, UsageCount increments.
//   2. Invoice auto-save path (preserveUnsetProfileFields) → a blank
//      header field must never erase a stored profile value; non-blank
//      enriches; IsDefault untouched.
//   3. Renaming onto a name+TRN pair another row owns → refused with
//      InvalidOperationException (hidden duplicates prevented).
//   4. Rename by primary key updates IN PLACE — never inserts a second row.
//   5. Stale caller Id → natural-key (name+TRN) lookup still finds the row.
//   6. Insert path → caller-supplied Id is discarded (SQLite assigns the
//      rowid); UsageCount starts at 1.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.MasterData.Database;
using ProGlassAutomation.Services.MasterData.Repository;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProGlassAutomation.Tests.MasterData
{
    public sealed class CompanyUpdateSemanticsTests : IDisposable
    {
        private readonly SqliteConnection _keepAlive;
        private readonly DbContextOptions<MasterDataDbContext> _options;
        private readonly bool _guardOriginal;
        private readonly bool _readOnlyOriginal;

        public CompanyUpdateSemanticsTests()
        {
            _guardOriginal = OperationGuard.Enabled;
            _readOnlyOriginal = ReadOnlyGate.Enabled;
            OperationGuard.Enabled = false;
            ReadOnlyGate.Enabled = false;

            // SQLite in-memory; the physical connection must stay OPEN for
            // the whole fixture or the database evaporates.
            _keepAlive = new SqliteConnection("Data Source=:memory:");
            _keepAlive.Open();
            _options = new DbContextOptionsBuilder<MasterDataDbContext>()
                .UseSqlite(_keepAlive)
                .Options;

            using var seed = CreateContext();
            seed.Database.EnsureCreated();
        }

        public void Dispose()
        {
            OperationGuard.Enabled = _guardOriginal;
            ReadOnlyGate.Enabled = _readOnlyOriginal;
            _keepAlive.Dispose();
        }

        private MasterDataDbContext CreateContext() => new(_options);
        private MasterDataRepository CreateRepository() => new(() => CreateContext());

        // ── 1. explicit editor: latest data wins, blank clears; the
        //    default-mark is ignored (mechanism removed 2026-09-04) ──────
        [Fact]
        public async Task ExplicitEdit_ThenLatestWins_AndBlankClearsProfileFields()
        {
            var repo = CreateRepository();
            var created = await repo.SaveOrUpdateCompanyAsync(new SavedCompany
            {
                CompanyName = "Alpha Glass LLC",
                CompanyTrn = "100000000000001",
                CompanyLocation = "Dubai",
                Representative = "Old Rep",
                LogoPath = "C:\\old-logo.png"
            });

            var updated = await repo.SaveOrUpdateCompanyAsync(new SavedCompany
            {
                Id = created.Id,
                CompanyName = "Alpha Glass LLC",
                CompanyTrn = "100000000000001",
                CompanyLocation = "",            // blank = clear
                Representative = " New Rep ",    // trimmed + stored
                IsDefault = true                 // MUST be ignored — the mark
                                               // mechanism is gone; Step 1
                                               // selection is the only authority
            });

            Assert.Equal(created.Id, updated.Id);
            Assert.Equal("", updated.CompanyLocation);
            Assert.Equal("New Rep", updated.Representative);
            Assert.False(updated.IsDefault);     // never persisted since removal
            Assert.Equal(2, updated.UsageCount);

            using var verify = CreateContext();
            Assert.Single(verify.Companies.Where(c => c.CompanyName.StartsWith("Alpha")));
        }

        // ── 2. auto-save: blank preserves, non-blank enriches ────────────
        [Fact]
        public async Task AutoSave_WithBlankHeader_NeverErasesStoredProfile()
        {
            var repo = CreateRepository();
            var created = await repo.SaveOrUpdateCompanyAsync(new SavedCompany
            {
                CompanyName = "Beta Interiors",
                CompanyTrn = "200000000000002",
                CompanyLocation = "Abu Dhabi",
                BankName = "Emirates NBD"
            });

            var after = await repo.SaveOrUpdateCompanyAsync(new SavedCompany
            {
                CompanyName = "Beta Interiors",
                CompanyTrn = "200000000000002",
                CompanyLocation = "",   // header field empty on the invoice
                CompanyEmail = "info@beta.example", // new info → enriches
                IsDefault = true        // ignored either way: the default-mark
                                        // mechanism was removed 2026-09-04
            }, preserveUnsetProfileFields: true);

            Assert.Equal(created.Id, after.Id);
            Assert.Equal("Abu Dhabi", after.CompanyLocation);
            Assert.Equal("Emirates NBD", after.BankName);
            Assert.Equal("info@beta.example", after.CompanyEmail);
            Assert.False(after.IsDefault); // never overwritten by auto-save
            Assert.Equal(2, after.UsageCount);
        }

        // ── 3. renaming onto another row's name+TRN is refused ───────────
        [Fact]
        public async Task Rename_OntoExistingNameAndTrn_ThrowsInsteadOfDuplicating()
        {
            var repo = CreateRepository();
            var a = await repo.SaveOrUpdateCompanyAsync(new SavedCompany
                { CompanyName = "Gamma Paints", CompanyTrn = "300" });
            await repo.SaveOrUpdateCompanyAsync(new SavedCompany
                { CompanyName = "Delta Frames", CompanyTrn = "400" });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repo.SaveOrUpdateCompanyAsync(new SavedCompany
                {
                    Id = a.Id,
                    CompanyName = "Delta Frames",    // another row's identity
                    CompanyTrn = "400"
                }));

            using var verify = CreateContext();
            Assert.Equal(2, verify.Companies.Count());
        }

        // ── 4. rename by Id updates in place, no duplicate row ───────────
        [Fact]
        public async Task Rename_ById_UpdatesInPlace_NoNewRow()
        {
            var repo = CreateRepository();
            var created = await repo.SaveOrUpdateCompanyAsync(new SavedCompany
                { CompanyName = "Epsilon Contracts", CompanyTrn = "500" });

            var renamed = await repo.SaveOrUpdateCompanyAsync(new SavedCompany
            {
                Id = created.Id,
                CompanyName = "Epsilon Contracts & Co",
                CompanyTrn = "500"
            });

            Assert.Equal(created.Id, renamed.Id);
            Assert.Equal("Epsilon Contracts & Co", renamed.CompanyName);

            using var verify = CreateContext();
            Assert.Single(verify.Companies);
            Assert.DoesNotContain(verify.Companies,
                c => c.CompanyName == "Epsilon Contracts");
        }

        // ── 5. stale Id falls back to the natural key ────────────────────
        [Fact]
        public async Task StaleId_NaturalKeyLookup_StillFindsTheRow()
        {
            var repo = CreateRepository();
            var created = await repo.SaveOrUpdateCompanyAsync(new SavedCompany
                { CompanyName = "Zeta Aluminium", CompanyTrn = "600" });

            var saved = await repo.SaveOrUpdateCompanyAsync(new SavedCompany
            {
                Id = 9999,                          // deleted row's stale Id
                CompanyName = "Zeta Aluminium",
                CompanyTrn = "600",
                Representative = "Rep"
            });

            Assert.Equal(created.Id, saved.Id);
            using var verify = CreateContext();
            Assert.Single(verify.Companies);
            Assert.Equal(2, saved.UsageCount);
        }

        // ── 6. insert discards caller Id ─────────────────────────────────
        [Fact]
        public async Task Insert_CallerSuppliedId_IsNeverReused()
        {
            var repo = CreateRepository();
            var saved = await repo.SaveOrUpdateCompanyAsync(new SavedCompany
            {
                Id = 777,
                CompanyName = "Omega Fit-Out",
                CompanyTrn = "700"
            });

            Assert.True(saved.Id > 0);
            Assert.NotEqual(777, saved.Id);
            Assert.Equal(1, saved.UsageCount);
            Assert.NotEqual(default(DateTime), saved.CreatedAt);

            using var verify = CreateContext();
            Assert.Single(verify.Companies);
            Assert.DoesNotContain(verify.Companies, c => c.Id == 777);
        }
    }
}
