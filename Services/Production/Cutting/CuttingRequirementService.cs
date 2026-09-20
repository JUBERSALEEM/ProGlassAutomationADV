// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Cutting/CuttingRequirementService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 15 Cutting Requirement Generation
//   Real transformation JO / Production Work Items → Cutting Requirements
//   Preserves complete traceability, deterministic piece identities,
//   no anonymous rectangles, spec identity preserved.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Cutting;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Optimization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production.Cutting
{
    public static class CuttingRequirementService
    {
        /// <summary>
        /// Deterministic piece id: JobRef|Spec|Serial|Position|W*H|Index
        /// </summary>
        public static string BuildPieceId(string jobRef, int specNo, int serial, string position, int w, int h, int index)
        {
            jobRef = (jobRef ?? "").Trim();
            position = (position ?? "").Trim();
            return $"{jobRef}|{specNo}|{serial}|{position}|{w}x{h}|P{index:D4}";
        }

        /// <summary>
        /// Build cutting requirements from Production Work Items for Cutting stage.
        /// Each WorkItem Qty expanded into individual pieces with deterministic identities.
        /// Preserves full traceability JobRef PI JO Spec SpecNo Serial Position W/H Qty Glass type Color Thickness etc.
        /// </summary>
        public static async Task<List<CuttingRequirement>> BuildFromProductionAsync(
            string jobRef,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobRef)) return new List<CuttingRequirement>();

            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();

                var workItems = await db.WorkItems.AsNoTracking()
                    .Where(w => w.JobRef == jobRef &&
                                w.Stage == (int)ProductionStage.Cutting &&
                                !w.IsCancelled)
                    .OrderBy(w => w.SpecGroupNumber)
                    .ThenBy(w => w.SerialNumber)
                    .ThenBy(w => w.Position)
                    .ToListAsync(ct).ConfigureAwait(false);

                var requirements = new List<CuttingRequirement>();
                int globalSeq = 0;

                foreach (var wi in workItems)
                {
                    ct.ThrowIfCancellationRequested();

                    // Parse material identity from WorkItem
                    // GlassRef may contain category, SpecName contains color/coating, Treatment contains FT etc
                    var (family, color, coating) = ParseMaterialFromSpec(wi.SpecName, wi.GlassRef, wi.CustomerName);

                    for (int q = 0; q < wi.Qty; q++)
                    {
                        globalSeq++;
                        int pieceIndex = q + 1;

                        string pieceId = BuildPieceId(jobRef, wi.SpecGroupNumber, wi.SerialNumber, wi.Position, wi.WidthMm, wi.HeightMm, pieceIndex);

                        var req = new CuttingRequirement
                        {
                            JobRef = wi.JobRef,
                            SourceProformaId = wi.SourceProformaId,
                            SpecGroupNumber = wi.SpecGroupNumber,
                            SpecName = wi.SpecName,
                            BuildType = wi.BuildType,
                            SerialNumber = wi.SerialNumber,
                            GlassRef = wi.GlassRef,
                            Position = wi.Position,
                            WidthMm = wi.WidthMm,
                            HeightMm = wi.HeightMm,
                            Quantity = 1, // Expanded per piece
                            GlassFamily = family,
                            Color = color,
                            ThicknessMm = InferThickness(wi),
                            Coating = coating,
                            Treatment = wi.Treatment ?? string.Empty,
                            RequiresTempering = !string.IsNullOrWhiteSpace(wi.Treatment) &&
                                               (wi.Treatment.Contains("FT", StringComparison.OrdinalIgnoreCase) ||
                                                wi.Treatment.Contains("Tempered", StringComparison.OrdinalIgnoreCase) ||
                                                wi.Treatment.Contains("Heat", StringComparison.OrdinalIgnoreCase)),
                            ProcessingFlags = wi.PolishSubTypeLabel ?? wi.Notes ?? string.Empty,
                            RotationPolicy = RotationPolicy.Free90, // Default, extensible per spec
                            MarginProfileThickness = InferThickness(wi),
                            Priority = 5,
                            PieceId = pieceId,
                            PieceLabel = $"{wi.GlassRef} · {wi.SpecGroupNumber}/{wi.SerialNumber} · {wi.Position} · {pieceIndex}/{wi.Qty}",
                            PieceIndex = pieceIndex,
                            PieceSequence = globalSeq,
                            EntryType = (ProductionEntryType)(wi.IsFoc ? 3 : 0),
                            ParentWorkItemId = wi.ParentWorkItemId,
                            ReplacementSequence = wi.ReplacementSequence,
                            IsFoc = wi.IsFoc,
                            IsCancelled = wi.IsCancelled,
                            Notes = wi.Notes ?? string.Empty,
                            SourceWorkItemId = wi.Id,
                            UniqueKey = wi.UniqueKey
                        };

                        requirements.Add(req);
                    }
                }

                AppLogService.Info($"[CuttingReq] Built {requirements.Count} requirements for {jobRef} from {workItems.Count} work items",
                    new LogContext { Module = "Production", Class = nameof(CuttingRequirementService), Operation = nameof(BuildFromProductionAsync) });

                return requirements;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[CuttingReq] Build failed for {jobRef}",
                    ex, new LogContext { Module = "Production", Class = nameof(CuttingRequirementService), Operation = nameof(BuildFromProductionAsync) });
                return new List<CuttingRequirement>();
            }
        }

        /// <summary>
        /// Build from JobOrder snapshot via DistributionEngine (alternative path when Production not yet released).
        /// Uses same pane routing as floor to prevent drift.
        /// </summary>
        public static List<CuttingRequirement> BuildFromJobOrderSnapshot(
            string jobRef,
            Models.ProformaInvoice.SavedInvoice.ProformaInvoiceSnapshot snapshot)
        {
            var requirements = new List<CuttingRequirement>();
            if (snapshot == null || string.IsNullOrWhiteSpace(jobRef)) return requirements;

            try
            {
                var plan = ProductionDistributionEngine.Distribute(jobRef, snapshot);
                int globalSeq = 0;

                var cuttingItems = plan.Items.Where(i => i.Stage == ProductionStage.Cutting).ToList();

                foreach (var item in cuttingItems.OrderBy(i => i.SpecGroupNumber).ThenBy(i => i.SerialNumber))
                {
                    var (family, color, coating) = ParseMaterialFromSpec(item.SpecName, item.GlassRef, plan.CustomerName);

                    for (int q = 0; q < item.Qty; q++)
                    {
                        globalSeq++;
                        int pieceIndex = q + 1;
                        string pieceId = BuildPieceId(jobRef, item.SpecGroupNumber, item.SerialNumber, item.Position, item.WidthMm, item.HeightMm, pieceIndex);

                        requirements.Add(new CuttingRequirement
                        {
                            JobRef = jobRef,
                            SourceProformaId = plan.SourceProformaId,
                            SpecGroupNumber = item.SpecGroupNumber,
                            SpecName = item.SpecName,
                            BuildType = item.BuildType,
                            SerialNumber = item.SerialNumber,
                            GlassRef = item.GlassRef,
                            Position = item.Position,
                            WidthMm = item.WidthMm,
                            HeightMm = item.HeightMm,
                            Quantity = 1,
                            GlassFamily = family,
                            Color = color,
                            ThicknessMm = InferThicknessFromSpec(item.SpecName),
                            Coating = coating,
                            Treatment = item.Treatment ?? string.Empty,
                            RequiresTempering = !string.IsNullOrWhiteSpace(item.Treatment),
                            ProcessingFlags = item.PolishSubTypeLabel ?? item.Notes ?? string.Empty,
                            RotationPolicy = RotationPolicy.Free90,
                            MarginProfileThickness = 6,
                            Priority = 5,
                            PieceId = pieceId,
                            PieceLabel = $"{item.GlassRef} · {item.SpecGroupNumber}/{item.SerialNumber} · {item.Position} · {pieceIndex}/{item.Qty}",
                            PieceIndex = pieceIndex,
                            PieceSequence = globalSeq,
                            EntryType = ProductionEntryType.Normal,
                            UniqueKey = item.UniqueKey,
                            Notes = item.Notes ?? string.Empty
                        });
                    }
                }

                return requirements;
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[CuttingReq] Build from snapshot failed for {jobRef}",
                    ex, new LogContext { Module = "Production", Class = nameof(CuttingRequirementService), Operation = nameof(BuildFromJobOrderSnapshot) });
                return requirements;
            }
        }

        private static (string Family, string Color, string Coating) ParseMaterialFromSpec(string specName, string glassRef, string customer)
        {
            // Simplified parsing — preserves spec identity, extensible
            // Real parsing would use PnLSpecParser, but we keep deterministic fallback
            specName = (specName ?? "").Trim();
            glassRef = (glassRef ?? "").Trim();

            string lower = specName.ToLowerInvariant();
            string family = "Clear";
            string color = "Clear";
            string coating = "";

            if (lower.Contains("low-e") || lower.Contains("low e") || lower.Contains("hp neutral") || lower.Contains("neutral"))
            {
                family = "Low-E";
                color = "Low-E";
                coating = "Low-E";
            }
            else if (lower.Contains("hd grey") || lower.Contains("grey") || lower.Contains("gray"))
            {
                family = "HD Grey";
                color = "Grey";
            }
            else if (lower.Contains("ramly") || lower.Contains("obscure") || lower.Contains("frost"))
            {
                family = "Ramly";
                color = "Obscure";
            }
            else if (lower.Contains("green"))
            {
                family = "Green";
                color = "Green";
            }
            else if (lower.Contains("blue"))
            {
                family = "Blue";
                color = "Blue";
            }
            else if (!string.IsNullOrWhiteSpace(glassRef))
            {
                // Use GlassRef as family fallback
                family = glassRef.Split('·')[0].Trim();
                if (string.IsNullOrWhiteSpace(family)) family = glassRef;
            }

            if (lower.Contains("low-e") || lower.Contains("low e")) coating = "Low-E";

            return (family, color, coating);
        }

        private static int InferThickness(SavedProductionWorkItem wi) =>
            InferThicknessFromSpec(wi?.SpecName);

        /// <summary>
        /// Pane thicknesses from the spec grammar only. Mixed panes
        /// (8mm outer + 6mm inner) are unresolved — 0 — so stock match
        /// cannot silently pick an 8mm rack from Contains("8mm").
        /// Unparsed names are also 0, never a guessed 6mm.
        /// </summary>
        private static int InferThicknessFromSpec(string? specName)
        {
            if (string.IsNullOrWhiteSpace(specName)) return 0;
            try
            {
                var panes = ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss
                    .PnLSpecParser.ResolveAllPanes(specName);
                var mm = panes
                    .Where(p => p != null && p.ThicknessMm > 0)
                    .Select(p => p.ThicknessMm)
                    .Distinct()
                    .ToList();
                if (mm.Count == 1) return mm[0];
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[CuttingReq] thickness parse failed: " + ex.Message);
            }
            return 0;
        }
    }
}
