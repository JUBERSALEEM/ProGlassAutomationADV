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
            SavedOptimizationSession session, CancellationToken ct = default)
        {
            if (session == null) return;

            session.Id = SavedOptimizationSession.LatestRunId;

            await using var db = new OptimizationDbContext();

            var existing = await db.Sessions
                .FirstOrDefaultAsync(s => s.Id == session.Id, ct)
                .ConfigureAwait(false);

            if (existing == null)
                db.Sessions.Add(session);
            else
                db.Entry(existing).CurrentValues.SetValues(session);

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
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
