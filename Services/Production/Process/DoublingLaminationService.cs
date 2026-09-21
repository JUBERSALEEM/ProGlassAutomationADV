// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Process/DoublingLaminationService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 16 Doubling/Lamination Service
//   Use existing stage DoublingLamination=8, support DGU, Lamination, DGU+Lamination.
//   Do not create separate competing production engines.
//   For DGU: Component A + Spacer + Component B → DGU assembly
//   For Lamination: Glass A + Interlayer + Glass B → Laminated assembly
//   Preserve component lineage, not flattened into anonymous finished product.
//   Assembly output preserve component lineage, final assembly must still allow tracing back to both glass pieces.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Process;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production.Process
{
    public static class DoublingLaminationService
    {
        public static string BuildAssemblyId(string jobRef, int specGroup, int serial, int assemblySeq, string type)
            => ProductionProcessRoutingService.BuildAssemblyId(jobRef, specGroup, serial, assemblySeq, type);

        /// <summary>
        /// Assemble DGU/Lamination from pairings — preserves component lineage.
        /// Doubling cannot begin when pairing has not passed (gating).
        /// </summary>
        public static async Task<(bool Ok, string Message, List<SavedProcessAssembly> Assemblies)> AssembleAsync(
            string jobRef,
            int specGroupNumber,
            int serialNumber,
            string assemblyType, // DGU, Lamination, DGU+LAM
            string spacerInfo = "",
            string interlayerInfo = "",
            string sealantInfo = "",
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobRef)) return (false, "JobRef required.", new List<SavedProcessAssembly>());
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
                    .ToListAsync(ct).ConfigureAwait(false);

                var doublingWi = workItems.FirstOrDefault(w => w.Stage == (int)ProductionStage.DoublingLamination);
                if (doublingWi != null)
                {
                    if (doublingWi.IsCancelled || doublingWi.Status == (int)WorkItemStatus.Cancelled)
                        return (false, "Doubling work item cancelled.", new List<SavedProcessAssembly>());
                    if (doublingWi.Status == (int)WorkItemStatus.Rejected)
                        return (false, "Rejected doubling work cannot assemble.", new List<SavedProcessAssembly>());
                    if (doublingWi.Status == (int)WorkItemStatus.QcFailed)
                        return (false, "QC failed — cannot assemble.", new List<SavedProcessAssembly>());
                }

                // Gating: Pairing WorkItem PCS must have passed (not ProcessPairing.Status)
                var pairingWorkItem = workItems.FirstOrDefault(w => w.Stage == (int)ProductionStage.Pairing);
                if (pairingWorkItem != null && pairingWorkItem.QtyDone < pairingWorkItem.Qty)
                    return (false, $"Doubling cannot begin when pairing has not passed — Pairing {pairingWorkItem.QtyDone}/{pairingWorkItem.Qty}", new List<SavedProcessAssembly>());

                var pairings = await db.ProcessPairings.AsNoTracking()
                    .Where(p => p.JobRef == jobRef && p.SpecGroupNumber == specGroupNumber && p.SerialNumber == serialNumber)
                    .OrderBy(p => p.PairId)
                    .ToListAsync(ct).ConfigureAwait(false);

                // Finished-unit count = pairing/doubling WorkItem.Qty, never pane-cut sum.
                int qty = pairingWorkItem?.Qty ?? doublingWi?.Qty ?? workItems.FirstOrDefault()?.Qty ?? 1;
                if (qty <= 0) qty = 1;

                var existingAssemblies = await db.ProcessAssemblies.AsNoTracking()
                    .Where(a => a.JobRef == jobRef && a.SpecGroupNumber == specGroupNumber && a.SerialNumber == serialNumber)
                    .ToListAsync(ct).ConfigureAwait(false);

                var newAssemblies = new List<SavedProcessAssembly>();

                for (int i = 0; i < qty; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    string assemblyId = BuildAssemblyId(jobRef, specGroupNumber, serialNumber, i, assemblyType);
                    if (existingAssemblies.Any(a => a.AssemblyId == assemblyId))
                        continue; // idempotent — no duplicate assembly

                    string assemblyLabel = assemblyType.Equals("Lamination", StringComparison.OrdinalIgnoreCase) ||
                                           assemblyType.Equals("LAM", StringComparison.OrdinalIgnoreCase)
                        ? $"LAM-{i + 1:000}" : $"DGU-{i + 1:000}";

                    // Preserve component lineage — not flattened into anonymous finished product
                    var components = new List<AssemblyComponent>();

                    var pairing = pairings.FirstOrDefault(p => p.PairId == $"Pair-{i + 1:000}") ?? pairings.ElementAtOrDefault(i);

                    if (pairing != null)
                    {
                        components.Add(new AssemblyComponent
                        {
                            PieceId = pairing.OuterPieceId,
                            GlassRef = pairing.OuterGlassRef,
                            Position = "Outer",
                            WorkItemId = pairing.OuterWorkItemId,
                            ExecutionId = pairing.OuterExecutionId,
                            Role = "Outer"
                        });
                        components.Add(new AssemblyComponent
                        {
                            PieceId = pairing.InnerPieceId,
                            GlassRef = pairing.InnerGlassRef,
                            Position = "Inner",
                            WorkItemId = pairing.InnerWorkItemId,
                            ExecutionId = pairing.InnerExecutionId,
                            Role = "Inner"
                        });
                        if (!string.IsNullOrWhiteSpace(spacerInfo))
                        {
                            components.Add(new AssemblyComponent
                            {
                                PieceId = $"Spacer-{spacerInfo}",
                                GlassRef = spacerInfo,
                                Position = "Spacer",
                                Role = "Spacer"
                            });
                        }
                        if (!string.IsNullOrWhiteSpace(interlayerInfo))
                        {
                            components.Add(new AssemblyComponent
                            {
                                PieceId = $"Interlayer-{interlayerInfo}",
                                GlassRef = interlayerInfo,
                                Position = "Interlayer",
                                Role = "Interlayer"
                            });
                        }
                    }
                    else
                    {
                        // No pairing record — build from work items directly, still preserve lineage
                        var outerWi = workItems.FirstOrDefault(w => w.Position.Equals("Outer", StringComparison.OrdinalIgnoreCase) || w.Position.Equals("Glass", StringComparison.OrdinalIgnoreCase));
                        var innerWi = workItems.FirstOrDefault(w => w.Position.Equals("Inner", StringComparison.OrdinalIgnoreCase));

                        if (outerWi != null)
                        {
                            components.Add(new AssemblyComponent
                            {
                                PieceId = $"{jobRef}|{specGroupNumber}|{serialNumber}|Outer|{outerWi.WidthMm}*{outerWi.HeightMm}|{i}",
                                GlassRef = outerWi.GlassRef,
                                Position = "Outer",
                                WorkItemId = outerWi.Id,
                                Role = "Outer"
                            });
                        }
                        if (innerWi != null)
                        {
                            components.Add(new AssemblyComponent
                            {
                                PieceId = $"{jobRef}|{specGroupNumber}|{serialNumber}|Inner|{innerWi.WidthMm}*{innerWi.HeightMm}|{i}",
                                GlassRef = innerWi.GlassRef,
                                Position = "Inner",
                                WorkItemId = innerWi.Id,
                                Role = "Inner"
                            });
                        }
                    }

                    var assembly = new SavedProcessAssembly
                    {
                        AssemblyId = assemblyId,
                        JobRef = jobRef,
                        SourceProformaId = workItems.FirstOrDefault()?.SourceProformaId ?? "",
                        SpecGroupNumber = specGroupNumber,
                        SerialNumber = serialNumber,
                        Position = "Unit",
                        AssemblyLabel = assemblyLabel,
                        AssemblyType = assemblyType,
                        ComponentsJson = JsonSerializer.Serialize(components),
                        SpacerInfo = spacerInfo,
                        InterlayerInfo = interlayerInfo,
                        SealantInfo = sealantInfo,
                        PairingId = pairing?.PairingId ?? "",
                        Status = (int)WorkItemStatus.Completed,
                        QtyRequired = 1,
                        QtyDone = 1,
                        CreatedBy = AuditService.CurrentUsername,
                        CreatedAt = DateTime.Now,
                        CompletedAt = DateTime.Now,
                        Notes = $"Assembly {assemblyLabel} {assemblyType} preserves lineage Outer+Inner+Spacer/Interlayer — traceable to both glass pieces"
                    };

                    db.ProcessAssemblies.Add(assembly);
                    newAssemblies.Add(assembly);
                }

                if (newAssemblies.Count > 0)
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);

                var allAssemblies = await db.ProcessAssemblies.AsNoTracking()
                    .Where(a => a.JobRef == jobRef && a.SpecGroupNumber == specGroupNumber && a.SerialNumber == serialNumber)
                    .OrderBy(a => a.AssemblyLabel)
                    .ToListAsync(ct).ConfigureAwait(false);

                AppLogService.Info($"[DoublingLamination] Assembled {allAssemblies.Count} assemblies {assemblyType} for {jobRef} Spec {specGroupNumber} Serial {serialNumber}",
                    new LogContext { Module = "Production", Class = nameof(DoublingLaminationService), Operation = nameof(AssembleAsync) });

                return (true, $"Assembled {allAssemblies.Count} {assemblyType} assemblies — component lineage preserved", allAssemblies);
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[DoublingLamination] Assemble failed {jobRef} Spec {specGroupNumber} Serial {serialNumber}",
                    ex, new LogContext { Module = "Production", Class = nameof(DoublingLaminationService), Operation = nameof(AssembleAsync) });
                return (false, $"Assembly failed: {ex.Message}", new List<SavedProcessAssembly>());
            }
        }

        public static async Task<List<SavedProcessAssembly>> GetByJobAsync(string jobRef, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobRef)) return new List<SavedProcessAssembly>();
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.ProcessAssemblies.AsNoTracking()
                .Where(a => a.JobRef == jobRef)
                .OrderBy(a => a.SpecGroupNumber)
                .ThenBy(a => a.SerialNumber)
                .ThenBy(a => a.AssemblyLabel)
                .ToListAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Get assembly traceability — final assembly must still allow tracing back to both glass pieces.
        /// </summary>
        public static string GetTraceability(SavedProcessAssembly assembly)
        {
            try
            {
                var components = JsonSerializer.Deserialize<List<AssemblyComponent>>(assembly.ComponentsJson ?? "[]") ?? new List<AssemblyComponent>();
                string comps = string.Join(" + ", components.Select(c => $"{c.Role} {c.PieceId} {c.GlassRef}"));
                return $"Assembly {assembly.AssemblyLabel} {assembly.AssemblyType} — Components: {comps} — Spacer {assembly.SpacerInfo} Interlayer {assembly.InterlayerInfo} — Job {assembly.JobRef} Spec {assembly.SpecGroupNumber} Serial {assembly.SerialNumber}";
            }
            catch
            {
                return $"Assembly {assembly.AssemblyLabel} {assembly.AssemblyType} Job {assembly.JobRef} Spec {assembly.SpecGroupNumber} Serial {assembly.SerialNumber}";
            }
        }
    }
}
