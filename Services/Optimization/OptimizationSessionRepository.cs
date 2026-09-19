// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Optimization/OptimizationSessionRepository.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — session persistence (optimization.db)
//
// Thin CRUD over OptimizationDbContext so saved optimization sessions
// (pieces + Step 4 stock pool + margins + run metadata) can be saved,
// listed, reopened and deleted. Mirrors the InquiryRepository house
// pattern: one short-lived DbContext per operation, no tracking on reads.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Optimization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Optimization
{
    public sealed class OptimizationSessionRepository
    {
        /// <summary>Creates the table on first use (idempotent).</summary>
        public async Task EnsureCreatedAsync(CancellationToken ct = default)
        {
            await using var db = new OptimizationDbContext();
            await db.Database.EnsureCreatedAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// The reserved latest-run row for one document, or null when that
        /// document has never had a run persisted. An empty key addresses the
        /// legacy global row.
        /// </summary>
        public async Task<SavedOptimizationSession?> GetLatestRunAsync(
            string? documentKey, CancellationToken ct = default)
        {
            // PHASE 3 / finding 2: canonicalised, so a run stamped with a job
            // order id and a lookup by proforma id address the SAME reserved row
            // instead of two rows that can never find each other.
            string id = SavedOptimizationSession.LatestRunIdFor(
                OptimizationDocumentKey.Canonicalize(documentKey));

            await using var db = new OptimizationDbContext();
            return await db.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, ct)
                .ConfigureAwait(false);
        }

        public async Task<List<SavedOptimizationSession>> ListAsync(CancellationToken ct = default)
        {
            await using var db = new OptimizationDbContext();
            return await db.Sessions
                .AsNoTracking()
                .OrderByDescending(s => s.CreatedUtc)
                .Take(100)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        public async Task<SavedOptimizationSession?> GetAsync(string id, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            await using var db = new OptimizationDbContext();
            return await db.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, ct)
                .ConfigureAwait(false);
        }

        public async Task InsertAsync(SavedOptimizationSession session, CancellationToken ct = default)
        {
            await using var db = new OptimizationDbContext();
            db.Sessions.Add(session);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes the latest run into the one reserved row, replacing whatever was
        /// there. Insert-or-update rather than insert: the row has a fixed id, so
        /// a second run would otherwise collide on the primary key.
        /// </summary>
        public async Task UpsertLatestRunAsync(
            SavedOptimizationSession session, string? documentKey = null,
            CancellationToken ct = default)
        {
            if (session == null) return;

            // Stamped with the document the run was produced for, so a quote
            // reopened later reads its OWN plan and not whichever run happened
            // to be newest globally.
            session.Id = SavedOptimizationSession.LatestRunIdFor(
                OptimizationDocumentKey.Canonicalize(documentKey));

            // PHASE 7 / finding 15 (2026-09-20): this was a read-then-write with
            // no transaction. Two runs finishing near-simultaneously — the Step 5
            // headless host and the module, or a double-clicked RUN — both read
            // existing == null, both called Add with the SAME primary key, and
            // the second threw DbUpdateException. WAL and busy_timeout (applied
            // by SqliteResilience) handle lock CONTENTION, not this logical race.
            // The caller logged the failure at Debug, so the lost save was
            // invisible: the operator's run simply was not there next time.
            //
            // Now wrapped in a transaction with a bounded retry. A retry is the
            // correct response to a PK collision because the next attempt reads
            // the row the other writer just committed and updates it instead of
            // inserting.
            const int maxAttempts = 3;

            for (int attempt = 1; ; attempt++)
            {
                await using var db = new OptimizationDbContext();
                try
                {
                    await using var tx = await db.Database
                        .BeginTransactionAsync(ct).ConfigureAwait(false);

                    var existing = await db.Sessions
                        .FirstOrDefaultAsync(s => s.Id == session.Id, ct)
                        .ConfigureAwait(false);

                    if (existing == null)
                        db.Sessions.Add(session);
                    else
                        db.Entry(existing).CurrentValues.SetValues(session);

                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                    return;
                }
                catch (DbUpdateException) when (attempt < maxAttempts && !ct.IsCancellationRequested)
                {
                    await Task.Delay(50 * attempt, ct).ConfigureAwait(false);
                }
            }
        }

        public async Task DeleteAsync(string id, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            await using var db = new OptimizationDbContext();
            var row = await db.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
            if (row == null) return;
            db.Sessions.Remove(row);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
