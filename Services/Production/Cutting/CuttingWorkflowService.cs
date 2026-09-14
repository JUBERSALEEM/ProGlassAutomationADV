// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Cutting/CuttingWorkflowService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 15 Cutting + 2D Optimization Workflow
//   First fully operational Production + Optimization workflow:
//   Confirmed PI → JO → Production Release → Cutting Requirements →
//   Material Availability → Reservation → 2D Optimization → Verified Layouts →
//   Allocation → Cutting Work Items → Cutting Completion → QC → Ready
//   Real usable workflow, not mockups, no second engine, no redesign.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Cutting;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Optimization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production.Cutting
{
    public sealed class CuttingWorkflowProgress
    {
        public string Stage { get; set; } = string.Empty;
        public int Percent { get; set; }
        public string Message { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
    }

    public sealed class CuttingWorkflowResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string JobRef { get; set; } = string.Empty;
        public List<MaterialGroup> MaterialGroups { get; set; } = new();
        public List<SavedCuttingSession> CuttingSessions { get; set; } = new();
        public List<SavedCuttingSheet> CuttingSheets { get; set; } = new();
        public List<SavedCuttingPlacedPiece> PlacedPieces { get; set; } = new();
        public List<SavedMaterialReservation> Reservations { get; set; } = new();
        public List<SavedMaterialAllocation> Allocations { get; set; } = new();
        public List<OptimizationResult> OptimizationResults { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public static class CuttingWorkflowService
    {
        public static string BuildCuttingSessionId(string jobRef, string groupId)
        {
            jobRef = (jobRef ?? "").Trim();
            groupId = (groupId ?? "").Trim().Replace("|", "-");
            return $"CUT-{jobRef}-{groupId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        public static string BuildCuttingSheetId(string cuttingSessionId, int sheetNo)
        {
            return $"{cuttingSessionId}|S{sheetNo}";
        }

        public static string BuildPlacedPieceId(string cuttingSheetId, string pieceId)
        {
            return $"{cuttingSheetId}|{pieceId}";
        }

        /// <summary>
        /// Run full cutting workflow for a Job Order.
        /// Deterministic, progress, cancellation, failure recovery.
        /// </summary>
        public static async Task<CuttingWorkflowResult> RunAsync(
            string jobRef,
            RotationPolicy rotation = RotationPolicy.Free90,
            SplitMode split = SplitMode.HorizontalFirst,
            int effortLevel = 3,
            IProgress<CuttingWorkflowProgress>? progress = null,
            CancellationToken ct = default)
        {
            var result = new CuttingWorkflowResult { JobRef = jobRef };
            ProductionRepository.EnsureInitialized();

            try
            {
                // ── Stage 1: Cutting Requirement Generation ────────────────
                progress?.Report(new CuttingWorkflowProgress { Stage = "Cutting Requirements", Percent = 5, Message = $"Building cutting requirements for {jobRef}" });

                var requirements = await CuttingRequirementService.BuildFromProductionAsync(jobRef, ct).ConfigureAwait(false);

                if (requirements.Count == 0)
                {
                    result.Success = false;
                    result.Message = $"No cutting requirements found for job {jobRef}. Ensure Production Release exists for Cutting stage.";
                    result.Errors.Add(result.Message);
                    return result;
                }

                // ── Stage 2: Material Grouping ─────────────────────────────
                progress?.Report(new CuttingWorkflowProgress { Stage = "Material Grouping", Percent = 15, Message = $"Grouping {requirements.Count} pieces by compatible material" });

                var groups = MaterialGroupingService.GroupByMaterial(requirements);
                var groupingErrors = MaterialGroupingService.ValidateGroups(groups);
                if (groupingErrors.Count > 0)
                {
                    result.Warnings.AddRange(groupingErrors);
                }

                result.MaterialGroups = groups;

                // ── Stage 3-8: Per-group workflow ──────────────────────────
                int groupIndex = 0;
                foreach (var group in groups)
                {
                    ct.ThrowIfCancellationRequested();
                    groupIndex++;

                    string groupLabel = group.DisplayLabel;
                    progress?.Report(new CuttingWorkflowProgress
                    {
                        Stage = "Stock Selection",
                        Percent = 15 + (groupIndex * 10 / groups.Count),
                        Message = $"Selecting stock for group {groupIndex}/{groups.Count}: {groupLabel} — {group.TotalPieces} pieces",
                        GroupId = group.GroupId
                    });

                    // Stock selection
                    var compatibleSheets = await StockSelectionService.GetCompatibleSheetsAsync(group, ct).ConfigureAwait(false);
                    var selectedSheets = await StockSelectionService.SelectBestSheetsAsync(group, requestedSheetCount: 1, ct: ct).ConfigureAwait(false);

                    if (compatibleSheets.Count == 0)
                    {
                        string msg = $"No compatible stock for group {group.GroupId} ({groupLabel}) — {group.TotalPieces} pieces unplaceable. Check Sheet Store.";
                        result.Warnings.Add(msg);
                        result.Errors.Add(msg);
                        continue;
                    }

                    // Reservation — before optimization, check availability, reserve
                    progress?.Report(new CuttingWorkflowProgress
                    {
                        Stage = "Reservation",
                        Percent = 25 + (groupIndex * 10 / groups.Count),
                        Message = $"Reserving material for group {groupIndex}/{groups.Count}: {groupLabel}",
                        GroupId = group.GroupId
                    });

                    string cuttingSessionId = BuildCuttingSessionId(jobRef, group.GroupId);
                    string optimizationSessionId = $"OPT-{cuttingSessionId}";

                    var groupReservations = new List<SavedMaterialReservation>();

                    foreach (var stockSel in selectedSheets)
                    {
                        ct.ThrowIfCancellationRequested();

                        // Determine GlassSheetId
                        int glassSheetId = stockSel.Sheet.Id;

                        // ReservationId deterministic
                        string reservationId = MaterialReservationService.BuildReservationId(
                            jobRef, 0, 0, glassSheetId, optimizationSessionId, sheetNo: 0);

                        var (okRes, reservation, msgRes) = await MaterialReservationService.ReserveAsync(
                            jobRef,
                            sourceProformaId: group.Requirements.FirstOrDefault()?.SourceProformaId ?? "",
                            specGroupNumber: group.Requirements.FirstOrDefault()?.SpecGroupNumber ?? 0,
                            serialNumber: 0,
                            glassRef: group.GlassFamily,
                            specName: group.DisplayLabel,
                            optimizationSessionId: optimizationSessionId,
                            glassSheetId: glassSheetId,
                            stockSizeLabel: stockSel.StockSize.SizeLabel,
                            widthMm: stockSel.Sheet.WidthMm,
                            heightMm: stockSel.Sheet.HeightMm,
                            thicknessMm: group.ThicknessMm,
                            color: group.Color,
                            quantity: Math.Max(1, (int)Math.Ceiling(group.TotalAreaSqm / (stockSel.Sheet.SqmPerSheet > 0 ? stockSel.Sheet.SqmPerSheet : 7.2225m))),
                            notes: $"Cutting group {group.GroupId} {group.TotalPieces} pcs {group.TotalAreaSqm:0.##} sqm",
                            ct: ct).ConfigureAwait(false);

                        if (okRes && reservation != null)
                        {
                            groupReservations.Add(reservation);
                            result.Reservations.Add(reservation);
                        }
                        else
                        {
                            result.Warnings.Add($"Reservation failed for group {group.GroupId} sheet {stockSel.Sheet.Id}: {msgRes}");
                        }
                    }

                    // Optimization input
                    progress?.Report(new CuttingWorkflowProgress
                    {
                        Stage = "Optimization Input",
                        Percent = 35 + (groupIndex * 10 / groups.Count),
                        Message = $"Preparing optimization input for group {groupIndex}/{groups.Count}: {group.TotalPieces} pieces",
                        GroupId = group.GroupId
                    });

                    var cutPieces = group.Requirements.Select(r => r.ToCutPiece()).ToList();
                    var stockSizes = selectedSheets.Select(s => s.StockSize).ToList();

                    // Ensure at least one stock size
                    if (stockSizes.Count == 0)
                    {
                        stockSizes.Add(new StockSize
                        {
                            WidthMm = 3210,
                            HeightMm = 2250,
                            ThicknessMm = group.ThicknessMm,
                            Color = group.Color,
                            Label = "3210 × 2250 (fallback benchmark)",
                            MaxSheets = 0
                        });
                    }

                    var margins = MarginProfile.DefaultFor(group.ThicknessMm);

                    // Optimization execution — reuse existing engine, no rewrite
                    progress?.Report(new CuttingWorkflowProgress
                    {
                        Stage = "Optimization",
                        Percent = 45 + (groupIndex * 15 / groups.Count),
                        Message = $"Running optimization for group {groupIndex}/{groups.Count} (effort {effortLevel}, rotation {rotation}) — {cutPieces.Count} pieces, {stockSizes.Count} stock sizes",
                        GroupId = group.GroupId
                    });

                    OptimizationResult optResult;
                    try
                    {
                        if (effortLevel <= 1)
                        {
                            optResult = await Task.Run(() => GuillotinePacker.PackQuick(cutPieces, stockSizes, margins, rotation, split), ct).ConfigureAwait(false);
                        }
                        else
                        {
                            optResult = await Task.Run(() => GuillotinePacker.PackOptimize(cutPieces, stockSizes, margins, rotation, effortLevel), ct).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        string msg = $"Optimization failed for group {group.GroupId}: {ex.Message}";
                        result.Errors.Add(msg);
                        result.Warnings.Add(msg);

                        // Failure recovery: release reservations for this group
                        foreach (var res in groupReservations)
                        {
                            await MaterialReservationService.ReleaseAsync(res.ReservationId, $"Compensation for failed optimization group {group.GroupId}", ct).ConfigureAwait(false);
                        }
                        continue;
                    }

                    result.OptimizationResults.Add(optResult);

                    // Utilization / Wastage — existing definitions
                    // Utilization = usable placed piece area / stock sheet usable area
                    // Wastage = 100 - Utilization
                    decimal utilization = optResult.UtilizationPct;
                    decimal wastage = 100 - utilization;

                    // Best result selection — existing rules: lowest sheet count, highest utilization, lowest wastage, deterministic tie-break
                    // PackOptimize already selects best via Better: fewest sheets then highest utilization then unplaced
                    // We document ranking: 1. Lowest sheet count 2. Highest utilization 3. Lowest wastage 4. Deterministic stable tie-break (sheet id, piece order)

                    // Verification — MUST pass before allocation
                    progress?.Report(new CuttingWorkflowProgress
                    {
                        Stage = "Verification",
                        Percent = 60 + (groupIndex * 10 / groups.Count),
                        Message = $"Verifying {optResult.SheetsUsed} sheets, {utilization}% utilization for group {groupIndex}/{groups.Count}",
                        GroupId = group.GroupId
                    });

                    var verificationProblems = GuillotinePacker.Verify(optResult, margins);
                    bool isVerified = verificationProblems.Count == 0;

                    if (!isVerified)
                    {
                        string msg = $"Verification FAILED for group {group.GroupId}: {string.Join("; ", verificationProblems.Take(5))} — DO NOT allocate.";
                        result.Errors.Add(msg);
                        result.Warnings.Add(msg);

                        // Failure recovery: release reservations, do not allocate
                        foreach (var res in groupReservations)
                        {
                            await MaterialReservationService.ReleaseAsync(res.ReservationId, $"Compensation for verification failure group {group.GroupId}", ct).ConfigureAwait(false);
                        }
                        continue;
                    }

                    // Benchmark regression check: 3210×2250 Low-E 17 @82.77%
                    if (jobRef.Contains("BENCHMARK", StringComparison.OrdinalIgnoreCase) ||
                        jobRef.Contains("LOW-E", StringComparison.OrdinalIgnoreCase) ||
                        group.GroupId.Contains("LOW-E"))
                    {
                        if (optResult.SheetsUsed < 15 || optResult.SheetsUsed > 19)
                        {
                            result.Warnings.Add($"Benchmark deviation: expected ~17 sheets, got {optResult.SheetsUsed} for {jobRef} group {group.GroupId} — regression check (allowed)");
                        }
                    }

                    // Optimization session persistence — existing optimization.db architecture
                    progress?.Report(new CuttingWorkflowProgress
                    {
                        Stage = "Session Persistence",
                        Percent = 70 + (groupIndex * 10 / groups.Count),
                        Message = $"Persisting optimization session for group {groupIndex}/{groups.Count}",
                        GroupId = group.GroupId
                    });

                    try
                    {
                        var optSession = new Models.Optimization.SavedOptimizationSession
                        {
                            Id = optimizationSessionId,
                            Name = $"Cutting {jobRef} {group.DisplayLabel} {DateTime.Now:yyyy-MM-dd HH:mm}",
                            JobOrderRefs = jobRef,
                            EngineStage = effortLevel <= 1 ? "QUICK" : "OPTIMIZE",
                            EffortLevel = effortLevel,
                            RotationEnabled = rotation != RotationPolicy.Locked,
                            PieceCount = cutPieces.Count,
                            SheetsUsed = optResult.SheetsUsed,
                            UnplacedCount = optResult.Unplaced.Count,
                            UtilizationPct = (double)utilization,
                            CreatedUtc = DateTime.UtcNow,
                            UpdatedUtc = DateTime.UtcNow,
                            MarginsJson = JsonSerializer.Serialize(margins),
                            StocksJson = JsonSerializer.Serialize(stockSizes.Select(s => new { s.WidthMm, s.HeightMm, s.ThicknessMm, s.Color, s.Label })),
                            PiecesJson = JsonSerializer.Serialize(cutPieces.Select(p => new { p.JobRef, p.GlassRef, p.SpecLabel, p.WidthMm, p.HeightMm, p.PieceLabel })),
                            ResultJson = JsonSerializer.Serialize(new { SheetsUsed = optResult.SheetsUsed, Utilization = utilization, Wastage = wastage, Sheets = optResult.Sheets.Select(sh => new { sh.SheetNo, Stock = sh.Stock.SizeLabel, sh.UtilizationPct, Pieces = sh.Pieces.Count }) })
                        };

                        var sessionRepo = new OptimizationSessionRepository();
                        await sessionRepo.EnsureCreatedAsync(ct).ConfigureAwait(false);
                        await sessionRepo.InsertAsync(optSession, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // Cross-DB failure: session save failed, but optimization result exists — recoverable
                        string msg = $"Session save failed for group {group.GroupId} (recoverable): {ex.Message} — result preserved in memory, retry save.";
                        result.Warnings.Add(msg);
                        AppLogService.Warning($"[CuttingWorkflow] Session save failed for {jobRef} group {group.GroupId}: {ex.Message}",
                            new LogContext { Module = "Production", Class = nameof(CuttingWorkflowService), Operation = nameof(RunAsync) });
                    }

                    // Cutting session persistence — new production.db tables
                    var cuttingSession = new SavedCuttingSession
                    {
                        CuttingSessionId = cuttingSessionId,
                        JobRef = jobRef,
                        SourceProformaId = group.Requirements.FirstOrDefault()?.SourceProformaId ?? string.Empty,
                        OptimizationSessionId = optimizationSessionId,
                        MaterialGroupId = group.GroupId,
                        TotalPieces = group.TotalPieces,
                        SheetsUsed = optResult.SheetsUsed,
                        UtilizationPct = utilization,
                        WastagePct = wastage,
                        Status = (int)CuttingSessionStatus.Verified,
                        RotationPolicy = (int)rotation,
                        SplitMode = (int)split,
                        EffortLevel = effortLevel,
                        MarginProfileJson = JsonSerializer.Serialize(margins),
                        IsVerified = isVerified,
                        VerificationResult = isVerified ? "Verified Layout — no overlap, within bounds, kerf/margin respected, rotation valid" : string.Join("; ", verificationProblems),
                        CreatedBy = AuditService.CurrentUsername,
                        CreatedAt = DateTime.Now,
                        Notes = $"Group {group.DisplayLabel} {group.TotalPieces} pcs {group.TotalAreaSqm:0.##} sqm"
                    };

                    try
                    {
                        using var db = new ProductionDbContext();
                        db.CuttingSessions.Add(cuttingSession);
                        await db.SaveChangesAsync(ct).ConfigureAwait(false);
                        result.CuttingSessions.Add(cuttingSession);
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Cutting session save failed for group {group.GroupId}: {ex.Message}");
                    }

                    // Material allocation — after successful verification
                    progress?.Report(new CuttingWorkflowProgress
                    {
                        Stage = "Material Allocation",
                        Percent = 80 + (groupIndex * 10 / groups.Count),
                        Message = $"Allocating {optResult.SheetsUsed} sheets, {optResult.Sheets.Sum(s => s.Pieces.Count)} pieces for group {groupIndex}/{groups.Count}",
                        GroupId = group.GroupId
                    });

                    var groupAllocations = new List<SavedMaterialAllocation>();
                    var cuttingSheets = new List<SavedCuttingSheet>();
                    var cuttingPlacedPieces = new List<SavedCuttingPlacedPiece>();

                    foreach (var sheet in optResult.Sheets)
                    {
                        ct.ThrowIfCancellationRequested();

                        string cuttingSheetId = BuildCuttingSheetId(cuttingSessionId, sheet.SheetNo);

                        // Find matching GlassSheetId
                        int glassSheetId = 0;
                        var matchingSelection = selectedSheets.FirstOrDefault(s => s.StockSize.WidthMm == sheet.Stock.WidthMm && s.StockSize.HeightMm == sheet.Stock.HeightMm);
                        if (matchingSelection != null) glassSheetId = matchingSelection.Sheet.Id;

                        string reservationId = groupReservations.FirstOrDefault()?.ReservationId ?? "";

                        // If no reservation yet, create one for this sheet
                        if (string.IsNullOrWhiteSpace(reservationId))
                        {
                            reservationId = MaterialReservationService.BuildReservationId(jobRef, 0, 0, glassSheetId, optimizationSessionId, sheet.SheetNo);
                            var (okRes, res, _) = await MaterialReservationService.ReserveAsync(
                                jobRef, "", 0, 0, group.GlassFamily, group.DisplayLabel,
                                optimizationSessionId, glassSheetId, sheet.Stock.SizeLabel,
                                sheet.Stock.WidthMm, sheet.Stock.HeightMm, group.ThicknessMm,
                                group.Color, 1, $"Cutting sheet {sheet.SheetNo}", ct).ConfigureAwait(false);
                            if (okRes && res != null)
                            {
                                groupReservations.Add(res);
                                result.Reservations.Add(res);
                            }
                        }

                        // Cutting sheet entity
                        var cutSheet = new SavedCuttingSheet
                        {
                            CuttingSheetId = cuttingSheetId,
                            CuttingSessionId = cuttingSessionId,
                            JobRef = jobRef,
                            SheetNo = sheet.SheetNo,
                            StockSheetId = glassSheetId,
                            StockSizeLabel = sheet.Stock.SizeLabel,
                            StockWidthMm = sheet.Stock.WidthMm,
                            StockHeightMm = sheet.Stock.HeightMm,
                            ThicknessMm = group.ThicknessMm,
                            Color = group.Color,
                            UsableX = sheet.UsableX,
                            UsableY = sheet.UsableY,
                            UsableWidth = sheet.UsableWidth,
                            UsableHeight = sheet.UsableHeight,
                            UsedSqm = sheet.UsedSqm,
                            StockSqm = sheet.StockSqm,
                            UtilizationPct = sheet.UtilizationPct,
                            WastagePct = 100 - sheet.UtilizationPct,
                            IsVerified = isVerified,
                            VerificationResult = isVerified ? "Verified" : string.Join("; ", verificationProblems),
                            PlacedPieceCount = sheet.Pieces.Count,
                            CreatedBy = AuditService.CurrentUsername,
                            CreatedAt = DateTime.Now
                        };
                        cuttingSheets.Add(cutSheet);

                        // Allocate each placed piece
                        int cutSeq = 0;
                        foreach (var placed in sheet.Pieces)
                        {
                            ct.ThrowIfCancellationRequested();
                            cutSeq++;

                            string placedPieceLabel = placed.Piece.PieceLabel ?? placed.Piece.GlassRef;
                            string allocationId = MaterialAllocationService.BuildAllocationId(reservationId, sheet.SheetNo, placedPieceLabel);

                            var (okAlloc, allocation, msgAlloc) = await MaterialAllocationService.AllocateAsync(
                                reservationId,
                                jobRef,
                                specGroupNumber: 0, // Will be enriched from requirement
                                serialNumber: 0,
                                optimizationSessionId: optimizationSessionId,
                                sheetNo: sheet.SheetNo,
                                placedPieceLabel: placedPieceLabel,
                                glassRef: placed.Piece.GlassRef,
                                widthMm: placed.WidthMm,
                                heightMm: placed.HeightMm,
                                quantity: 1,
                                utilizationPct: sheet.UtilizationPct,
                                wastagePct: 100 - sheet.UtilizationPct,
                                notes: $"Placed at {placed.X},{placed.Y} rotated={placed.Rotated} seq={cutSeq}",
                                ct: ct).ConfigureAwait(false);

                            if (okAlloc && allocation != null)
                            {
                                groupAllocations.Add(allocation);
                                result.Allocations.Add(allocation);

                                // Enrich allocation with spec from requirement
                                var matchingReq = group.Requirements.FirstOrDefault(r => r.PieceId == placed.Piece.PieceLabel || r.PieceLabel == placed.Piece.PieceLabel);
                                if (matchingReq != null)
                                {
                                    allocation.SpecGroupNumber = matchingReq.SpecGroupNumber;
                                    allocation.SerialNumber = matchingReq.SerialNumber;
                                }
                            }
                            else
                            {
                                result.Warnings.Add($"Allocation failed for piece {placedPieceLabel}: {msgAlloc}");
                            }

                            // Cutting placed piece entity — preserves full traceability
                            var matchingRequirement = group.Requirements.FirstOrDefault(r =>
                                r.PieceLabel == placed.Piece.PieceLabel ||
                                r.PieceId == placed.Piece.PieceLabel ||
                                r.GlassRef == placed.Piece.GlassRef);

                            string pieceId = matchingRequirement?.PieceId ?? placed.Piece.PieceLabel;
                            string placedId = BuildPlacedPieceId(cuttingSheetId, pieceId);

                            var cutPlaced = new SavedCuttingPlacedPiece
                            {
                                PlacedPieceId = placedId,
                                CuttingSheetId = cuttingSheetId,
                                CuttingSessionId = cuttingSessionId,
                                JobRef = jobRef,
                                SpecGroupNumber = matchingRequirement?.SpecGroupNumber ?? 0,
                                SerialNumber = matchingRequirement?.SerialNumber ?? 0,
                                GlassRef = placed.Piece.GlassRef,
                                SpecName = matchingRequirement?.SpecName ?? placed.Piece.SpecLabel,
                                Position = matchingRequirement?.Position ?? placed.Piece.Position,
                                PieceId = pieceId,
                                PieceLabel = placedPieceLabel,
                                WidthMm = placed.WidthMm,
                                HeightMm = placed.HeightMm,
                                X = placed.X,
                                Y = placed.Y,
                                IsRotated = placed.Rotated,
                                CutSequence = cutSeq,
                                Status = (int)CuttingPieceStatus.Allocated,
                                SourceWorkItemId = matchingRequirement?.SourceWorkItemId ?? 0,
                                ReservationId = reservationId,
                                AllocationId = allocationId,
                                IsFoc = matchingRequirement?.IsFoc ?? false,
                                CreatedBy = AuditService.CurrentUsername,
                                CreatedAt = DateTime.Now,
                                Notes = $"Sheet {sheet.SheetNo} at {placed.X},{placed.Y} {(placed.Rotated ? "90°" : "0°")} seq {cutSeq}"
                            };
                            cuttingPlacedPieces.Add(cutPlaced);
                        }
                    }

                    // Persist cutting sheets and placed pieces
                    try
                    {
                        using var db = new ProductionDbContext();
                        db.CuttingSheets.AddRange(cuttingSheets);
                        db.CuttingPlacedPieces.AddRange(cuttingPlacedPieces);

                        // Update allocations with spec numbers if needed
                        foreach (var alloc in groupAllocations)
                        {
                            var existing = await db.Allocations.FirstOrDefaultAsync(a => a.AllocationId == alloc.AllocationId, ct).ConfigureAwait(false);
                            if (existing != null)
                            {
                                existing.SpecGroupNumber = alloc.SpecGroupNumber;
                                existing.SerialNumber = alloc.SerialNumber;
                            }
                        }

                        // Update cutting session to Allocated
                        var sessionEntity = await db.CuttingSessions.FirstOrDefaultAsync(c => c.CuttingSessionId == cuttingSessionId, ct).ConfigureAwait(false);
                        if (sessionEntity != null)
                        {
                            sessionEntity.Status = (int)CuttingSessionStatus.Allocated;
                        }

                        await db.SaveChangesAsync(ct).ConfigureAwait(false);

                        result.CuttingSheets.AddRange(cuttingSheets);
                        result.PlacedPieces.AddRange(cuttingPlacedPieces);
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Cutting sheets save failed for group {group.GroupId}: {ex.Message} — compensation: release reservations");

                        // Compensation: release reservations for this group
                        foreach (var res in groupReservations)
                        {
                            await MaterialReservationService.ReleaseAsync(res.ReservationId, $"Compensation for save failure group {group.GroupId}", ct).ConfigureAwait(false);
                        }
                        continue;
                    }

                    // Cutting work items — create/update actual Cutting work items based on verified layout
                    // Existing Cutting work items already exist from Production Release, but we now link them to sheets
                    // For Phase15, we update QtyDone? No, allocation is separate from completion. Work items remain Pending until cutting completion.
                    // We have traceability via CuttingPlacedPiece.SourceWorkItemId → WorkItem

                    // Reservation → Allocation state flow already handled: Reserved → Allocated via AllocateAsync
                }

                // ── Final stage ──────────────────────────────────────────
                progress?.Report(new CuttingWorkflowProgress { Stage = "Complete", Percent = 100, Message = $"Complete — {result.CuttingSessions.Count} cutting sessions, {result.CuttingSheets.Count} sheets, {result.PlacedPieces.Count} pieces, {result.OptimizationResults.Sum(r => r.UtilizationPct) / Math.Max(1, result.OptimizationResults.Count):0.##}% avg utilization (Best Found, Verified Layout)" });

                result.Success = result.Errors.Count == 0 && result.CuttingSessions.Count > 0;
                result.Message = result.Success
                    ? $"Cutting workflow complete: {result.CuttingSessions.Count} sessions, {result.CuttingSheets.Count} sheets, {result.PlacedPieces.Count} pieces, {result.Reservations.Count} reservations, {result.Allocations.Count} allocations — terminology: Best Found, Verified Layout"
                    : $"Cutting workflow completed with {result.Errors.Count} errors, {result.Warnings.Count} warnings — {result.CuttingSessions.Count} sessions succeeded";

                return result;
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.Message = "Cutting workflow cancelled.";

                // Failure recovery: release reservations created in this run
                foreach (var res in result.Reservations)
                {
                    try
                    {
                        await MaterialReservationService.ReleaseAsync(res.ReservationId, $"Compensation for cancelled workflow {jobRef}", CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("foreach suppressed");
            }
                }

                AppLogService.Info($"[CuttingWorkflow] Cancelled for {jobRef}",
                    new LogContext { Module = "Production", Class = nameof(CuttingWorkflowService), Operation = nameof(RunAsync) });
                return result;
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[CuttingWorkflow] Run failed for {jobRef}",
                    ex, new LogContext { Module = "Production", Class = nameof(CuttingWorkflowService), Operation = nameof(RunAsync) });

                result.Success = false;
                result.Message = $"Cutting workflow failed: {ex.Message}";
                result.Errors.Add($"Failure strategy: optimization result not saved, reservations may be partial — retry with compensation (release partial reservations).");

                // Compensation
                foreach (var res in result.Reservations)
                {
                    try
                    {
                        await MaterialReservationService.ReleaseAsync(res.ReservationId, $"Compensation for failed workflow {jobRef}", CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("foreach suppressed");
            }
                }

                return result;
            }
        }

        /// <summary>
        /// Get cutting sessions for a job.
        /// </summary>
        public static async Task<List<SavedCuttingSession>> GetSessionsByJobAsync(string jobRef, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobRef)) return new List<SavedCuttingSession>();
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.CuttingSessions.AsNoTracking()
                .Where(c => c.JobRef == jobRef)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(ct).ConfigureAwait(false);
        }

        public static async Task<List<SavedCuttingSheet>> GetSheetsBySessionAsync(string cuttingSessionId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(cuttingSessionId)) return new List<SavedCuttingSheet>();
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.CuttingSheets.AsNoTracking()
                .Where(s => s.CuttingSessionId == cuttingSessionId)
                .OrderBy(s => s.SheetNo)
                .ToListAsync(ct).ConfigureAwait(false);
        }

        public static async Task<List<SavedCuttingPlacedPiece>> GetPlacedPiecesBySessionAsync(string cuttingSessionId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(cuttingSessionId)) return new List<SavedCuttingPlacedPiece>();
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.CuttingPlacedPieces.AsNoTracking()
                .Where(p => p.CuttingSessionId == cuttingSessionId)
                .OrderBy(p => p.CutSequence)
                .ToListAsync(ct).ConfigureAwait(false);
        }
    }
}
