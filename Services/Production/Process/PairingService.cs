// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Process/PairingService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 16 Pairing Service (DGU especially important)
//   Pair compatible components using deterministic identity JobRef Spec Serial
//   Position Component identity Piece index — not random, not DB return order, not GUID.
//   Example DGU Outer 6mm HD Grey Inner 6mm Clear Qty 4 Pair-001 Outer Piece A Inner Piece A etc.
//   Do NOT pair pieces merely because dimensions match, do NOT pair from different jobs/specifications.
//   Pairing must preserve JobRef Spec Serial Position Component identity Piece identity.
//   Deterministic pairing should use JobRef Spec Serial Position Component identity Piece index.
//   Assembly output preserve component lineage, not flattened into anonymous finished product.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Process;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Production;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production.Process
{
    public static class PairingService
    {
        public static string BuildPairingId(string jobRef, int specGroup, int serial, int pairSeq)
            => ProductionProcessRoutingService.BuildPairingId(jobRef, specGroup, serial, pairSeq);

        /// <summary>
        /// Deterministic pairing for DGU — Outer + Inner components, same JobRef Spec Serial Position.
        /// Stable ordering: JobRef Spec Serial Position Component identity Piece index — per actual model semantics.
        /// </summary>
        public static async Task<(bool Ok, string Message, List<SavedProcessPairing> Pairings)> PairAsync(
            string jobRef,
            int specGroupNumber,
            int serialNumber,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobRef)) return (false, "JobRef required.", new List<SavedProcessPairing>());
            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();
                await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
                using var tx = await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, ct).ConfigureAwait(false);

                // Supporting records only — never writes WorkItem.QtyDone.
                var workItems = await db.WorkItems.AsNoTracking()
                    .Where(w => w.JobRef == jobRef && w.SpecGroupNumber == specGroupNumber && w.SerialNumber == serialNumber && !w.IsCancelled)
                    .OrderBy(w => w.Position) // deterministic: Outer before Inner, but we will order explicitly
                    .ThenBy(w => w.Id)
                    .ToListAsync(ct).ConfigureAwait(false);

                var pairingWi = workItems.FirstOrDefault(w => w.Stage == (int)ProductionStage.Pairing);
                if (pairingWi != null)
                {
                    if (pairingWi.IsCancelled || pairingWi.Status == (int)WorkItemStatus.Cancelled)
                        return (false, "Pairing work item cancelled.", new List<SavedProcessPairing>());
                    if (pairingWi.Status == (int)WorkItemStatus.Rejected)
                        return (false, "Rejected pairing work cannot be paired.", new List<SavedProcessPairing>());
                    if (pairingWi.Status == (int)WorkItemStatus.QcFailed)
                        return (false, "QC failed — cannot pair.", new List<SavedProcessPairing>());
                }

                // Gating: Pairing cannot begin when one component missing
                var outerItems = workItems.Where(w => w.Position.Equals("Outer", StringComparison.OrdinalIgnoreCase) ||
                                                      w.Position.Equals("Lam Outer", StringComparison.OrdinalIgnoreCase) ||
                                                      w.Position.Equals("Glass", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(w => w.Position, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(w => w.Id)
                    .ToList();

                var innerItems = workItems.Where(w => w.Position.Equals("Inner", StringComparison.OrdinalIgnoreCase) ||
                                                      w.Position.Equals("Lam Inner", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(w => w.Position, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(w => w.Id)
                    .ToList();

                // For DGU: need at least one Outer and one Inner
                // For LAM: need Lam Outer and Lam Inner
                // Check if we have required components
                if (outerItems.Count == 0 || innerItems.Count == 0)
                {
                    // Check if this is SGU — then pairing not required, but we still allow?
                    // For SGU, pairing should not be required — return info
                    bool hasUnit = workItems.Any(w => w.Position.Equals("Unit", StringComparison.OrdinalIgnoreCase));
                    if (!hasUnit)
                    {
                        return (false, $"Pairing not required or components missing for {jobRef} Spec {specGroupNumber} Serial {serialNumber} — Outer {outerItems.Count} Inner {innerItems.Count}. For SGU, pairing is not required.", new List<SavedProcessPairing>());
                    }
                }

                // Deterministic pairing: use JobRef Spec Serial Position Component identity Piece index
                // We pair by piece index — Piece-001 Outer with Piece-001 Inner, etc.
                // Get executions for outer/inner to ensure they are completed (gating)
                var executions = await db.ProcessExecutions.AsNoTracking()
                    .Where(e => e.JobRef == jobRef && e.SpecGroupNumber == specGroupNumber && e.SerialNumber == serialNumber)
                    .OrderBy(e => e.Position, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(e => e.PieceId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(e => e.Sequence)
                    .ToListAsync(ct).ConfigureAwait(false);

                // Finished-unit count = pairing WorkItem.Qty (never pane-cut sum).
                var doublingWi = workItems.FirstOrDefault(w => w.Stage == (int)ProductionStage.DoublingLamination);
                var outerCutting = workItems.FirstOrDefault(w =>
                    w.Stage == (int)ProductionStage.Cutting && IsOuterPosition(w.Position));
                var innerCutting = workItems.FirstOrDefault(w =>
                    w.Stage == (int)ProductionStage.Cutting && IsInnerPosition(w.Position));

                int qty = pairingWi?.Qty ?? doublingWi?.Qty ?? 0;
                if (qty <= 0)
                    qty = Math.Min(outerCutting?.Qty ?? 1, innerCutting?.Qty ?? 1);
                if (qty <= 0) qty = 1;

                if (outerCutting != null && outerCutting.QtyDone < outerCutting.Qty)
                    return (false, $"Pairing cannot begin — Outer cutting incomplete {outerCutting.QtyDone}/{outerCutting.Qty}", new List<SavedProcessPairing>());
                if (innerCutting != null && innerCutting.QtyDone < innerCutting.Qty)
                    return (false, $"Pairing cannot begin — Inner cutting incomplete {innerCutting.QtyDone}/{innerCutting.Qty}", new List<SavedProcessPairing>());

                // Deterministic pairing — stable: JobRef Spec Serial Position Component identity Piece index
                var pairings = new List<SavedProcessPairing>();
                var existingPairings = await db.ProcessPairings.AsNoTracking()
                    .Where(p => p.JobRef == jobRef && p.SpecGroupNumber == specGroupNumber && p.SerialNumber == serialNumber)
                    .ToListAsync(ct).ConfigureAwait(false);

                var outerPlaced = await LoadLatestPlacedPanesAsync(db, outerCutting, ct).ConfigureAwait(false);
                var innerPlaced = await LoadLatestPlacedPanesAsync(db, innerCutting, ct).ConfigureAwait(false);

                for (int i = 0; i < qty; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    string pairingId = BuildPairingId(jobRef, specGroupNumber, serialNumber, i);
                    if (existingPairings.Any(p => p.PairingId == pairingId))
                        continue; // idempotent — no duplicate pairing

                    var outerExec = executions.FirstOrDefault(e =>
                        IsOuterPosition(e.Position) && e.Sequence == i);
                    var innerExec = executions.FirstOrDefault(e =>
                        IsInnerPosition(e.Position) && e.Sequence == i);

                    string outerPieceId = ResolvePaneInstanceId(
                        i, outerPlaced, outerExec, outerCutting, jobRef, specGroupNumber, serialNumber, "Outer");
                    string innerPieceId = ResolvePaneInstanceId(
                        i, innerPlaced, innerExec, innerCutting, jobRef, specGroupNumber, serialNumber, "Inner");

                    // Outer/inner work item ids
                    int outerWiId = outerItems.FirstOrDefault()?.Id ?? 0;
                    int innerWiId = innerItems.FirstOrDefault()?.Id ?? 0;

                    var pairing = new SavedProcessPairing
                    {
                        PairingId = pairingId,
                        JobRef = jobRef,
                        SourceProformaId = workItems.FirstOrDefault()?.SourceProformaId ?? "",
                        SpecGroupNumber = specGroupNumber,
                        SerialNumber = serialNumber,
                        Position = "Unit",
                        PairId = $"Pair-{i + 1:000}", // human Pair-001
                        OuterPieceId = outerPieceId,
                        InnerPieceId = innerPieceId,
                        OuterGlassRef = outerItems.FirstOrDefault()?.GlassRef ?? "Outer",
                        InnerGlassRef = innerItems.FirstOrDefault()?.GlassRef ?? "Inner",
                        OuterWorkItemId = outerWiId,
                        InnerWorkItemId = innerWiId,
                        OuterExecutionId = outerExec?.ExecutionId ?? "",
                        InnerExecutionId = innerExec?.ExecutionId ?? "",
                        Status = (int)WorkItemStatus.Completed,
                        CreatedBy = AuditService.CurrentUsername,
                        CreatedAt = DateTime.Now,
                        Notes = $"Paired {outerPieceId} + {innerPieceId} deterministic JobRef Spec Serial Position Component Piece index"
                    };

                    db.ProcessPairings.Add(pairing);
                    pairings.Add(pairing);
                }

                if (pairings.Count > 0)
                {
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                }
                else
                    await tx.CommitAsync(ct).ConfigureAwait(false);

                // Include existing for return
                var allPairings = await db.ProcessPairings.AsNoTracking()
                    .Where(p => p.JobRef == jobRef && p.SpecGroupNumber == specGroupNumber && p.SerialNumber == serialNumber)
                    .OrderBy(p => p.PairId)
                    .ToListAsync(ct).ConfigureAwait(false);

                AppLogService.Info($"[Pairing] Paired {allPairings.Count} pairs for {jobRef} Spec {specGroupNumber} Serial {serialNumber}",
                    new LogContext { Module = "Production", Class = nameof(PairingService), Operation = nameof(PairAsync) });

                return (true, $"Paired {allPairings.Count} pairs deterministically for {jobRef} Spec {specGroupNumber} Serial {serialNumber}", allPairings);
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[Pairing] Pair failed {jobRef} Spec {specGroupNumber} Serial {serialNumber}",
                    ex, new LogContext { Module = "Production", Class = nameof(PairingService), Operation = nameof(PairAsync) });
                return (false, $"Pairing failed: {ex.Message}", new List<SavedProcessPairing>());
            }
        }

        private static bool IsOuterPosition(string? position) =>
            !string.IsNullOrWhiteSpace(position) &&
            (position.Equals("Outer", StringComparison.OrdinalIgnoreCase) ||
             position.Equals("Lam Outer", StringComparison.OrdinalIgnoreCase) ||
             position.Equals("Glass", StringComparison.OrdinalIgnoreCase));

        private static bool IsInnerPosition(string? position) =>
            !string.IsNullOrWhiteSpace(position) &&
            (position.Equals("Inner", StringComparison.OrdinalIgnoreCase) ||
             position.Equals("Lam Inner", StringComparison.OrdinalIgnoreCase));

        private static async Task<List<SavedCuttingPlacedPiece>> LoadLatestPlacedPanesAsync(
            ProductionDbContext db,
            SavedProductionWorkItem? cuttingWi,
            CancellationToken ct)
        {
            if (cuttingWi == null || cuttingWi.Id <= 0)
                return new List<SavedCuttingPlacedPiece>();

            var rows = await db.CuttingPlacedPieces.AsNoTracking()
                .Where(p => p.SourceWorkItemId == cuttingWi.Id)
                .ToListAsync(ct).ConfigureAwait(false);

            return rows
                .GroupBy(p => p.PieceId, StringComparer.Ordinal)
                .Select(g => g.OrderByDescending(p => p.CreatedAt).First())
                .OrderBy(p => p.PieceId, StringComparer.Ordinal)
                .ToList();
        }

        private static string ResolvePaneInstanceId(
            int index0,
            List<SavedCuttingPlacedPiece> placed,
            SavedProcessExecution? exec,
            SavedProductionWorkItem? cuttingWi,
            string jobRef,
            int specGroupNumber,
            int serialNumber,
            string defaultPosition)
        {
            if (index0 >= 0 && index0 < placed.Count
                && ProductionPieceIdentity.IsPaneInstanceId(placed[index0].PieceId))
                return placed[index0].PieceId;

            if (exec != null && ProductionPieceIdentity.IsPaneInstanceId(exec.OutputPieceId))
                return exec.OutputPieceId;
            if (exec != null && ProductionPieceIdentity.IsPaneInstanceId(exec.PieceId))
                return exec.PieceId;

            if (cuttingWi != null)
                return ProductionPieceIdentity.FromWorkItem(cuttingWi, index0 + 1);

            return ProductionPieceIdentity.BuildPaneInstanceId(
                jobRef, specGroupNumber, serialNumber, defaultPosition, 0, 0, index0 + 1);
        }

        public static async Task<List<SavedProcessPairing>> GetByJobAsync(string jobRef, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobRef)) return new List<SavedProcessPairing>();
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.ProcessPairings.AsNoTracking()
                .Where(p => p.JobRef == jobRef)
                .OrderBy(p => p.SpecGroupNumber)
                .ThenBy(p => p.SerialNumber)
                .ThenBy(p => p.PairId)
                .ToListAsync(ct).ConfigureAwait(false);
        }
    }
}
