// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/ProductionBreakageService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 14 Breakage / Rework / FOC foundation
//   Implements proper EntryType model: Normal, Breakage, Rework, FOC.
//   Each entry preserves EntryType, Source JO/PI/Spec/WorkItem, original
//   piece identity, parent/replacement relationship, quantity, CreatedBy,
//   CreatedAt, Reason, Notes, audit.
//   Breakage: do NOT silently modify original historical piece.
//   Record original as broken/failed, create traceable replacement/rework.
//   FOC: commercially traceable, source PI/JO, reason, originating piece,
//   replacement relationship, quantity, cost, selling value zero, profitability.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production
{
    public static class ProductionBreakageService
    {
        public static string BuildReplacementId(
            string jobRef, int specGroup, int serial, string position, int stage, int sequence)
        {
            jobRef = (jobRef ?? "").Trim();
            position = (position ?? "").Trim();
            return $"{jobRef}|{specGroup}|{serial}|{position}|{stage}|RPL:{sequence}";
        }

        /// <summary>
        /// Record breakage for a work item — does NOT modify original Qty/QtyDone
        /// history, creates Breakage entry + Replacement requirement.
        /// </summary>
        public static async Task<(bool Ok, SavedProductionEntry? BreakageEntry, SavedProductionReplacement? Replacement, string Message)>
            RecordBreakageAsync(
                int workItemId,
                int breakageQty,
                string reason,
                string? notes = null,
                bool isFoc = false,
                CancellationToken ct = default)
        {
            if (workItemId <= 0) return (false, null, null, "WorkItemId required.");
            if (breakageQty <= 0) return (false, null, null, "Breakage qty must be >0.");
            if (string.IsNullOrWhiteSpace(reason)) reason = "Breakage reported";

            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();
                var workItem = await db.WorkItems.FindAsync(new object[] { workItemId }, ct).ConfigureAwait(false);
                if (workItem == null) return (false, null, null, "Work item not found.");

                // Find last entry for this work item to link parent
                var lastEntry = await db.Entries
                    .Where(e => e.WorkItemId == workItemId)
                    .OrderByDescending(e => e.DoneAt)
                    .FirstOrDefaultAsync(ct).ConfigureAwait(false);

                // Create breakage entry
                var breakageEntry = new SavedProductionEntry
                {
                    WorkItemId = workItemId,
                    JobRef = workItem.JobRef,
                    Stage = workItem.Stage,
                    SpecGroupNumber = workItem.SpecGroupNumber,
                    SerialNumber = workItem.SerialNumber,
                    Position = workItem.Position,
                    SpecName = workItem.SpecName,
                    GlassRef = workItem.GlassRef,
                    Qty = breakageQty,
                    DoneBy = AuditService.CurrentUsername,
                    DoneAt = DateTime.Now,
                    EntryType = (int)ProductionEntryType.Breakage,
                    Reason = reason,
                    ParentEntryId = lastEntry?.Id,
                    ReplacementSequence = 0,
                    IsFoc = isFoc,
                    Notes = notes ?? string.Empty
                };

                db.Entries.Add(breakageEntry);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                // Determine replacement sequence for this work item
                var existingReplacements = await db.Replacements
                    .Where(r => r.OriginalWorkItemId == workItemId)
                    .OrderByDescending(r => r.ReplacementSequence)
                    .ToListAsync(ct).ConfigureAwait(false);

                int nextSeq = existingReplacements.Count == 0 ? 1 : existingReplacements.Max(r => r.ReplacementSequence) + 1;

                string replacementId = BuildReplacementId(
                    workItem.JobRef, workItem.SpecGroupNumber, workItem.SerialNumber,
                    workItem.Position, workItem.Stage, nextSeq);

                // Idempotency: check if replacement with same deterministic id exists
                var existingReplacement = await db.Replacements
                    .FirstOrDefaultAsync(r => r.ReplacementId == replacementId, ct)
                    .ConfigureAwait(false);

                if (existingReplacement != null)
                {
                    return (true, breakageEntry, existingReplacement, "Breakage recorded, replacement already exists (idempotent).");
                }

                // Create replacement work item — same geometry, replacement sequence, IsFoc flag
                string originalUniqueKey = workItem.UniqueKey;
                string replacementUniqueKey = $"{originalUniqueKey}|RPL:{nextSeq}";

                // Check if replacement work item already exists (from previous breakage)
                var replacementWorkItem = await db.WorkItems
                    .FirstOrDefaultAsync(w => w.UniqueKey == replacementUniqueKey, ct)
                    .ConfigureAwait(false);

                if (replacementWorkItem == null)
                {
                    replacementWorkItem = new SavedProductionWorkItem
                    {
                        UniqueKey = replacementUniqueKey,
                        JobRef = workItem.JobRef,
                        SourceProformaId = workItem.SourceProformaId,
                        CustomerName = workItem.CustomerName,
                        ProjectName = workItem.ProjectName,
                        Salesman = workItem.Salesman,
                        SpecGroupNumber = workItem.SpecGroupNumber,
                        SpecName = workItem.SpecName,
                        BuildType = workItem.BuildType,
                        SerialNumber = workItem.SerialNumber,
                        GlassRef = workItem.GlassRef,
                        Position = workItem.Position,
                        WidthMm = workItem.WidthMm,
                        HeightMm = workItem.HeightMm,
                        Qty = breakageQty, // replacement qty = broken qty
                        QtyDone = 0,
                        UnitSqm = workItem.UnitSqm,
                        TotalSqm = workItem.UnitSqm * breakageQty,
                        TotalLm = workItem.Qty > 0 ? workItem.TotalLm * breakageQty / workItem.Qty : workItem.TotalLm,
                        Treatment = workItem.Treatment,
                        Stage = workItem.Stage,
                        Status = (int)WorkItemStatus.Pending,
                        PredecessorStage = workItem.PredecessorStage,
                        ChargeTemplateKey = workItem.ChargeTemplateKey,
                        ChargeMetricQty = workItem.ChargeMetricQty,
                        ChargeMetricUnit = workItem.ChargeMetricUnit,
                        PolishSubTypeLabel = workItem.PolishSubTypeLabel,
                        Notes = $"Replacement for breakage: {reason}",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        IsFoc = isFoc,
                        ReplacementSequence = nextSeq,
                        ParentWorkItemId = workItemId,
                        IsCancelled = false
                    };
                    db.WorkItems.Add(replacementWorkItem);
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                }

                var replacement = new SavedProductionReplacement
                {
                    ReplacementId = replacementId,
                    JobRef = workItem.JobRef,
                    SourceProformaId = workItem.SourceProformaId,
                    SpecGroupNumber = workItem.SpecGroupNumber,
                    SerialNumber = workItem.SerialNumber,
                    GlassRef = workItem.GlassRef,
                    SpecName = workItem.SpecName,
                    Position = workItem.Position,
                    Stage = workItem.Stage,
                    OriginalWorkItemId = workItemId,
                    OriginalEntryId = breakageEntry.Id,
                    ReplacementWorkItemId = replacementWorkItem.Id,
                    EntryType = isFoc ? (int)ProductionEntryType.Foc : (int)ProductionEntryType.Rework,
                    Reason = reason,
                    Quantity = breakageQty,
                    IsFoc = isFoc,
                    ReplacementSequence = nextSeq,
                    CreatedBy = AuditService.CurrentUsername,
                    CreatedAt = DateTime.Now,
                    Status = 0 // Pending
                };

                db.Replacements.Add(replacement);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                AppLogService.Info($"[Breakage] Job {workItem.JobRef} Spec {workItem.SpecGroupNumber} Serial {workItem.SerialNumber} breakage {breakageQty} reason {reason} replacement {replacementId} IsFoc={isFoc}",
                    new LogContext { Module = "Production", Class = nameof(ProductionBreakageService), Operation = nameof(RecordBreakageAsync) });

                return (true, breakageEntry, replacement, "Breakage recorded + replacement created.");
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[Breakage] Record failed for WorkItem {workItemId}",
                    ex, new LogContext { Module = "Production", Class = nameof(ProductionBreakageService), Operation = nameof(RecordBreakageAsync) });
                return (false, null, null, $"Breakage failed: {ex.Message}");
            }
        }

        public static async Task<(bool Ok, SavedProductionEntry? ReworkEntry, string Message)>
            RecordReworkAsync(
                int workItemId,
                int reworkQty,
                string reason,
                int? parentEntryId = null,
                bool isFoc = false,
                string? notes = null,
                CancellationToken ct = default)
        {
            if (workItemId <= 0) return (false, null, "WorkItemId required.");
            if (reworkQty <= 0) return (false, null, "Rework qty must be >0.");

            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();
                var workItem = await db.WorkItems.FindAsync(new object[] { workItemId }, ct).ConfigureAwait(false);
                if (workItem == null) return (false, null, "Work item not found.");

                var entry = new SavedProductionEntry
                {
                    WorkItemId = workItemId,
                    JobRef = workItem.JobRef,
                    Stage = workItem.Stage,
                    SpecGroupNumber = workItem.SpecGroupNumber,
                    SerialNumber = workItem.SerialNumber,
                    Position = workItem.Position,
                    SpecName = workItem.SpecName,
                    GlassRef = workItem.GlassRef,
                    Qty = reworkQty,
                    DoneBy = AuditService.CurrentUsername,
                    DoneAt = DateTime.Now,
                    EntryType = (int)ProductionEntryType.Rework,
                    Reason = reason ?? "Rework",
                    ParentEntryId = parentEntryId,
                    ReplacementSequence = workItem.ReplacementSequence,
                    IsFoc = isFoc,
                    Notes = notes ?? string.Empty
                };

                db.Entries.Add(entry);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                // Mark work item as Rework status if not already completed
                if (workItem.Status != (int)WorkItemStatus.Completed)
                {
                    workItem.Status = (int)WorkItemStatus.Rework;
                    workItem.UpdatedAt = DateTime.Now;
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                }

                return (true, entry, "Rework recorded.");
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[Breakage] Rework failed for WorkItem {workItemId}",
                    ex, new LogContext { Module = "Production", Class = nameof(ProductionBreakageService), Operation = nameof(RecordReworkAsync) });
                return (false, null, $"Rework failed: {ex.Message}");
            }
        }

        public static async Task<(bool Ok, SavedProductionEntry? FocEntry, string Message)>
            RecordFocAsync(
                int workItemId,
                int focQty,
                string reason,
                int? parentEntryId = null,
                string? notes = null,
                CancellationToken ct = default)
        {
            // FOC is Free Of Charge — commercially traceable, selling value zero
            return await RecordReworkAsync(workItemId, focQty, reason, parentEntryId, isFoc: true, notes: notes, ct: ct)
                .ContinueWith(t =>
                {
                    if (!t.Result.Ok) return (false, (SavedProductionEntry?)null, t.Result.Message);
                    var entry = t.Result.ReworkEntry;
                    if (entry != null)
                    {
                        entry.EntryType = (int)ProductionEntryType.Foc;
                        entry.IsFoc = true;
                    }
                    return (t.Result.Ok, entry, t.Result.Message.Replace("Rework", "FOC"));
                }, ct).ConfigureAwait(false);
        }
    }
}
