// ═══════════════════════════════════════════════════════════════════════
//  FILE: ProGlass.Saas.Application/Production/CuttingService.cs
//  PORTED from the WPF application (read 2026-09-27):
//    • Services/Production/Cutting/CuttingRequirementService.cs —
//      BuildFromProductionAsync (Stage=Cutting, !Cancelled, ordered
//      Spec/Serial/Position; Qty expanded per piece; pane-instance ids;
//      material identity from the spec grammar; thickness
//      single-distinct-or-0; RequiresTempering FT/Tempered/Heat).
//    • Services/Production/Cutting/MaterialGroupingService.cs — grouping
//      + validation warnings.
//    • Services/Production/Cutting/StockSelectionService.cs — compatible
//      racks (thickness exact, color/family fallback, Clear↔Low-E
//      hard-incompatible, largest piece fits rotated), effective =
//      Balance − active holds (allocations are a different unit, never
//      subtracted), waste score, the 80% fill estimate.
//    • Services/Production/Cutting/CuttingWorkflowService.cs — the
//      per-group workflow: reserve BEFORE optimization, pack
//      (effort ≤ 1 = QUICK), GuillotinePacker.Verify MUST pass before
//      allocation, compensation releases the group's reservations on
//      optimization/verification/save failure, session Verified →
//      Allocated, per-piece allocations + placed pieces with full
//      lineage.
//    • Services/Production/Cutting/CuttingCompletionService.cs —
//      CompletePiece (work item QtyDone+1 clamped, allocation → Consumed
//      with the reservation cascade), breakage during cutting (Phase 14
//      replacement), progress tuple, CompleteSession.
//  SaaS differences (documented, mapping §1.7d): GUID ids + tenant RLS;
//  the WPF's optimization.db SavedOptimizationSession becomes the SaaS
//  optimization_sessions row via the existing store (save failure still
//      warns only — recoverable); the per-group optimization session
//  counts against the Phase 18 optimization-jobs plan limit (the WPF has
//  no subscriptions); the WPF's synthetic unlimited fallback sheet
//  (Sheet.Id == 0) has no SaaS equivalent — racks come from the Sheet
//  Store; reservation keys embed no optimization session (the SaaS opt
//  session id is a GUID assigned after the plan exists; linkage rides
//  the optimization_session_id columns).
// ═══════════════════════════════════════════════════════════════════════

using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Application.Optimization;
using ProGlass.Saas.Application.Subscriptions;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Optimization;
using ProGlass.Saas.Domain.Proforma;
using ProGlass.Saas.Domain.Subscriptions;

namespace ProGlass.Saas.Application.Production;

public sealed class CuttingService(
    ICuttingCostStore store,
    IProductionStore production,
    IProcessControlStore control,
    IOptimizationStore optimization,
    ReservationService reservations,
    ReplacementService replacements,
    PlanLimitGuard limits,
    IAuditWriter audit)
{
    // ── Requirements (WPF BuildFromProductionAsync) ──────────────────

    /// <summary>
    /// GET /cutting/requirements?jobOrderId= — the read-only requirement
    /// build: every live Cutting work item expanded into per-piece
    /// requirements with pane-instance identity.
    /// </summary>
    public async Task<ProductionResult<IReadOnlyList<CuttingRequirementDto>>> BuildRequirementsAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var job = await production.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<IReadOnlyList<CuttingRequirementDto>>.Fail(
                ProductionErrorCode.NotFound,
                $"Job order {jobOrderId} was not found.");
        }

        var requirements = BuildRequirements(job, await production.ListByJobAsync(
            tenantId, jobOrderId, ct));
        return ProductionResult<IReadOnlyList<CuttingRequirementDto>>.Ok(
            requirements.Select(ToDto).ToList());
    }

    internal static IReadOnlyList<CuttingRequirement> BuildRequirements(
        JobOrder job, IReadOnlyList<ProductionPiece> pieces)
    {
        var requirements = new List<CuttingRequirement>();
        int globalSeq = 0;

        // Stage = Cutting, !Cancelled, ordered Spec/Serial/Position (WPF).
        var workItems = pieces
            .Where(p => p.Stage == ProductionStages.CuttingName && !p.IsCancelled)
            .OrderBy(p => p.SpecGroupNumber)
            .ThenBy(p => p.SerialNumber)
            .ThenBy(p => p.Position, StringComparer.Ordinal)
            .ToList();

        foreach (var wi in workItems)
        {
            var (family, color, coating) = CuttingMaterial.ParseFromSpec(
                wi.SpecName, wi.GlassRef);
            int thickness = CuttingMaterial.InferThickness(wi.SpecName);

            for (int q = 0; q < wi.Qty; q++)
            {
                globalSeq++;
                int pieceIndex = q + 1;

                string pieceId = ProductionPieceIdentity.BuildPaneInstanceId(
                    job.JobNo, wi.SpecGroupNumber, wi.SerialNumber, wi.Position,
                    wi.WidthMm, wi.HeightMm, pieceIndex, wi.ReplacementSequence);

                requirements.Add(new CuttingRequirement(
                    job.JobNo,
                    job.ProformaId.ToString(),
                    wi.SpecGroupNumber,
                    wi.SpecName,
                    wi.BuildType,
                    wi.SerialNumber,
                    wi.GlassRef,
                    wi.Position,
                    wi.WidthMm,
                    wi.HeightMm,
                    family,
                    color,
                    thickness,
                    coating,
                    wi.Treatment ?? string.Empty,
                    CuttingMaterial.RequiresTempering(wi.Treatment),
                    pieceId,
                    CuttingMaterial.BuildPieceLabel(
                        wi.GlassRef, wi.SpecGroupNumber, wi.SerialNumber,
                        wi.Position, pieceIndex, wi.Qty),
                    pieceIndex,
                    globalSeq,
                    wi.IsFoc,
                    wi.ReplacementSequence,
                    wi.UniqueKey,
                    wi.Id,
                    wi.Notes ?? string.Empty));
            }
        }

        return requirements;
    }

    // ── The workflow (WPF RunAsync) ──────────────────────────────────

    /// <summary>
    /// POST /cutting/run — the full per-group workflow: requirements →
    /// grouping → stock selection → reservation → optimization →
    /// verification → persistence → allocation. A group that fails is
    /// skipped with its reservations released (compensation); the run
    /// succeeds when no group errored and at least one session exists.
    /// </summary>
    public async Task<ProductionResult<CuttingRunResultDto>> RunAsync(
        Guid tenantId, Guid actorId, RunCuttingRequest request,
        string? ip, CancellationToken ct)
    {
        if (request.JobOrderId == Guid.Empty)
        {
            return ProductionResult<CuttingRunResultDto>.Fail(
                ProductionErrorCode.ValidationFailed, "JobOrderId required.");
        }

        var job = await production.FindJobAsync(tenantId, request.JobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<CuttingRunResultDto>.Fail(
                ProductionErrorCode.NotFound,
                $"Job order {request.JobOrderId} was not found.");
        }

        // Rotation / split / effort — the OptimizationService parse rules.
        var rotation = string.Equals(request.Rotation, "Locked", StringComparison.OrdinalIgnoreCase)
            ? RotationPolicy.Locked
            : RotationPolicy.Free90;
        var split = string.Equals(request.Split, "VerticalFirst", StringComparison.OrdinalIgnoreCase)
            ? SplitMode.VerticalFirst
            : string.Equals(request.Split, "LongerLeftover", StringComparison.OrdinalIgnoreCase)
                ? SplitMode.LongerLeftover
                : SplitMode.HorizontalFirst;
        int effort = Math.Clamp(request.EffortLevel ?? 3, 1, 5);

        // Stage 1 — requirements.
        var requirements = BuildRequirements(job, await production.ListByJobAsync(
            tenantId, job.Id, ct));
        if (requirements.Count == 0)
        {
            return ProductionResult<CuttingRunResultDto>.Ok(
                FailedRun(job.JobNo,
                    $"No cutting requirements found for job {job.JobNo}. " +
                    "Ensure Production Release exists for Cutting stage."),
                $"No cutting requirements found for job {job.JobNo}. " +
                "Ensure Production Release exists for Cutting stage.");
        }

        // Stage 2 — grouping (+ validation warnings, WPF).
        var groups = CuttingGrouping.GroupByMaterial(requirements);
        var warnings = new List<string>();
        foreach (var g in groups)
        {
            var first = g.Requirements.FirstOrDefault();
            if (first is null) continue;
            foreach (var req in g.Requirements.Skip(1))
            {
                if (!CuttingGrouping.AreCompatible(first, req))
                {
                    warnings.Add(
                        $"Group {g.GroupId} contains incompatible pieces: " +
                        $"{first.PieceId} ({first.MaterialKey}) vs " +
                        $"{req.PieceId} ({req.MaterialKey})");
                }
            }
        }

        // Scanner-grade reads once: racks + active holds (WPF reads the
        // Sheet Store + all live reservations).
        var sheets = (await store.ListAllGlassSheetsAsync(tenantId, ct))
            .Select(s => (s.Id, s.Category, s.Color, s.ThicknessMm,
                s.WidthMm, s.HeightMm, s.Stock, s.Used))
            .ToList();
        var reservedPerSheet = (await store.ListAllReservationsAsync(tenantId, ct))
            .Where(r => ReservationStatuses.IsActiveHold(r.Status))
            .GroupBy(r => r.GlassSheetId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.ReservedQty));

        // Phase 18 limit — the per-group optimization sessions count
        // against the tenant's optimization-jobs plan (SaaS-specific).
        var limitCheck = await limits.ResolveAsync(
            tenantId, ModuleFeatureMap.Optimization, ct);
        int sessionsThisRun = 0;

        var errors = new List<string>();
        var groupResults = new List<CuttingGroupResultDto>();
        var runReservations = new List<ReservationDto>();
        int sessionCount = 0, sheetCount = 0, pieceCount = 0, allocationCount = 0;

        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();

            var groupWarnings = new List<string>();
            var groupReservations = new List<MaterialReservation>();

            // Stage 3 — stock selection.
            var candidates = CuttingStockSelection.Evaluate(
                group, sheets, reservedPerSheet);

            if (candidates.Count == 0)
            {
                string msg =
                    $"No compatible stock for group {group.GroupId} " +
                    $"({group.DisplayLabel}) — {group.TotalPieces} pieces " +
                    "unplaceable. Check Sheet Store.";
                errors.Add(msg);
                warnings.Add(msg);
                groupResults.Add(new CuttingGroupResultDto(
                    group.GroupId, false, msg, null, 0, 0, Array.Empty<string>()));
                continue;
            }

            // Stage 4 — reservation BEFORE optimization (WPF): the 80%
            // fill estimate drives how many racks to hold.
            string cuttingSessionId = CuttingIds.BuildCuttingSessionId(
                job.JobNo, group.GroupId, DateTime.UtcNow);

            var selectedRackList = SelectRacks(group, candidates);
            var selectedRackIds = selectedRackList
                .Where(c => c.GlassSheetId.HasValue)
                .Select(c => c.GlassSheetId!.Value)
                .ToHashSet();
            var selectedRacks = selectedRackList;

            foreach (var candidate in selectedRackList)
            {
                ct.ThrowIfCancellationRequested();

                int sheetQty = Math.Max(1, (int)Math.Ceiling(
                    group.TotalAreaSqm / (candidate.SheetSqm > 0
                        ? candidate.SheetSqm
                        : 7.2225m)));

                var reserveResult = await reservations.ReserveAsync(
                    tenantId, actorId, new ReserveRequest(
                        job.Id,
                        candidate.GlassSheetId ?? Guid.Empty,
                        group.Requirements[0].SpecGroupNumber,
                        0,
                        group.GlassFamily,
                        group.DisplayLabel,
                        null,
                        sheetQty,
                        $"Cutting group {group.GroupId} {group.TotalPieces} pcs " +
                        $"{group.TotalAreaSqm:0.##} sqm"),
                    ip, ct);

                if (reserveResult.Success && reserveResult.Value is not null)
                {
                    // Track the row itself for compensation + hold math.
                    var row = await store.FindReservationByKeyAsync(
                        tenantId, reserveResult.Value.ReservationKey, ct);
                    if (row is not null) groupReservations.Add(row);
                    runReservations.Add(reserveResult.Value);
                    if (!reservedPerSheet.ContainsKey(reserveResult.Value.GlassSheetId))
                    {
                        reservedPerSheet[reserveResult.Value.GlassSheetId] = 0;
                    }
                    reservedPerSheet[reserveResult.Value.GlassSheetId] += sheetQty;
                }
                else
                {
                    groupWarnings.Add(
                        $"Reservation failed for group {group.GroupId} sheet " +
                        $"{candidate.GlassSheetId}: {reserveResult.Message}");
                }
            }

            // Plan limit (SaaS): one optimization session per group.
            if (limitCheck?.Limit is int cap)
            {
                var used = await optimization.CountSessionsSinceAsync(
                    tenantId, limitCheck.PeriodStart, ct);
                if (used + sessionsThisRun >= cap)
                {
                    string msg =
                        "The current plan's optimization-job limit " +
                        $"({cap} per billing period) has been reached — " +
                        $"{used} runs since {limitCheck.PeriodStart:yyyy-MM-dd}. " +
                        "Choose a plan with a higher limit in Settings → Subscription.";
                    errors.Add(msg);
                    warnings.Add(msg);
                    await ReleaseGroupAsync(
                        tenantId, actorId, groupReservations,
                        $"Compensation for plan limit group {group.GroupId}", ip, ct);
                    groupResults.Add(new CuttingGroupResultDto(
                        group.GroupId, false, msg, null, 0, 0, groupWarnings));
                    continue;
                }
            }

            // Stage 5 — optimization input.
            var cutPieces = group.Requirements.Select(r => new CutPiece(
                r.JobRef,
                r.GlassRef,
                r.SpecName,
                r.Position,
                r.Color,
                r.ThicknessMm,
                r.WidthMm,
                r.HeightMm,
                rotation != RotationPolicy.Locked,
                r.PieceLabel,
                r.MaterialKey,
                r.SpecGroupNumber,
                r.SerialNumber)).ToList();

            var stockSizes = candidates
                .Where(c => selectedRackIds.Contains(c.GlassSheetId ?? Guid.Empty))
                .Select(c => new StockSize(
                    c.WidthMm, c.HeightMm, group.ThicknessMm, group.Color,
                    c.StockSizeLabel, "", c.EffectiveAvailable))
                .ToList();

            // WPF fallback benchmark stock (kept verbatim).
            if (stockSizes.Count == 0)
            {
                stockSizes.Add(new StockSize(
                    3210, 2250, group.ThicknessMm, group.Color,
                    "3210 × 2250 (fallback benchmark)", "", 0));
            }

            var margins = MarginProfile.DefaultFor(group.ThicknessMm);

            // Stage 6 — optimization (effort ≤ 1 = QUICK; finite inventory
            // takes PackOptimize so the rack cap holds globally, everything
            // else the partitioned engine — the 25c routing).
            OptimizationResult plan;
            try
            {
                bool finiteStock = stockSizes.Any(s =>
                    s.MaxSheets > 0 && s.MaxSheets < StockSize.EffectivelyUnlimitedSheets);
                plan = effort <= 1
                    ? GuillotinePacker.PackQuick(cutPieces, stockSizes, margins, rotation, split)
                    : finiteStock
                        ? GuillotinePacker.PackOptimize(cutPieces, stockSizes, margins,
                            rotation, effortLevel: effort)
                        : GuillotinePacker.PackOptimizePartitioned(cutPieces, stockSizes,
                            margins, rotation, effortLevel: effort);
            }
            catch (Exception ex)
            {
                string msg = $"Optimization failed for group {group.GroupId}: {ex.Message}";
                errors.Add(msg);
                warnings.Add(msg);
                await ReleaseGroupAsync(tenantId, actorId, groupReservations,
                    $"Compensation for failed optimization group {group.GroupId}", ip, ct);
                groupResults.Add(new CuttingGroupResultDto(
                    group.GroupId, false, msg, null, 0, 0, groupWarnings));
                continue;
            }

            decimal utilization = plan.UtilizationPct;
            decimal wastage = 100 - utilization;

            // Stage 7 — verification MUST pass before allocation (WPF).
            var verificationProblems = GuillotinePacker.Verify(plan, margins);
            bool isVerified = verificationProblems.Count == 0;

            if (!isVerified)
            {
                string msg =
                    $"Verification FAILED for group {group.GroupId}: " +
                    $"{string.Join("; ", verificationProblems.Take(5))} — DO NOT allocate.";
                errors.Add(msg);
                warnings.Add(msg);
                await ReleaseGroupAsync(tenantId, actorId, groupReservations,
                    $"Compensation for verification failure group {group.GroupId}", ip, ct);
                groupResults.Add(new CuttingGroupResultDto(
                    group.GroupId, false, msg, null, 0, 0, groupWarnings));
                continue;
            }

            // Benchmark regression check (allowed deviation, WPF).
            if (job.JobNo.Contains("BENCHMARK", StringComparison.OrdinalIgnoreCase)
                || job.JobNo.Contains("LOW-E", StringComparison.OrdinalIgnoreCase)
                || group.GroupId.Contains("LOW-E", StringComparison.Ordinal))
            {
                if (plan.SheetsUsed < 15 || plan.SheetsUsed > 19)
                {
                    warnings.Add(
                        $"Benchmark deviation: expected ~17 sheets, got " +
                        $"{plan.SheetsUsed} for {job.JobNo} group {group.GroupId} — " +
                        "regression check (allowed)");
                }
            }

            // Stage 8 — optimization session persistence (the WPF's
            // cross-DB optimization.db save; failure warns only).
            OptimizationSession? optSession = null;
            try
            {
                optSession = await optimization.PersistAsync(
                    tenantId, actorId, "job_order", job.Id, group.GroupId, plan,
                    JsonSerializer.Serialize(new
                    {
                        rotation = rotation.ToString(),
                        split = split.ToString(),
                        effortLevel = effort,
                        platePattern = PlatePattern.Yield.ToString(),
                        thicknessMm = group.ThicknessMm,
                        minBreakoutMm = margins.MinBreakoutMm,
                        source = "cutting_workflow"
                    }), ct);
                sessionsThisRun++;
            }
            catch (Exception ex)
            {
                warnings.Add(
                    $"Session save failed for group {group.GroupId} " +
                    $"(recoverable): {ex.Message} — result preserved, retry save.");
            }

            // Cutting session row (status Verified, WPF).
            var cuttingSession = new CuttingSession
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CuttingSessionId = cuttingSessionId,
                JobOrderId = job.Id,
                JobRef = job.JobNo,
                SourceProformaId = job.ProformaId.ToString(),
                OptimizationSessionId = optSession?.Id,
                MaterialGroupId = group.GroupId,
                TotalPieces = group.TotalPieces,
                SheetsUsed = plan.SheetsUsed,
                UtilizationPct = utilization,
                WastagePct = wastage,
                Status = CuttingSessionStatuses.Verified,
                RotationPolicy = rotation.ToString(),
                SplitMode = split.ToString(),
                EffortLevel = effort,
                MarginProfileJson = JsonSerializer.Serialize(margins),
                IsVerified = isVerified,
                VerificationResult = isVerified
                    ? "Verified Layout — no overlap, within bounds, kerf/margin respected, rotation valid"
                    : string.Join("; ", verificationProblems),
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow,
                Notes = $"Group {group.DisplayLabel} {group.TotalPieces} pcs " +
                        $"{group.TotalAreaSqm:0.##} sqm"
            };

            try
            {
                if (await store.InsertCuttingSessionAsync(tenantId, cuttingSession, ct) == 0)
                {
                    // Idempotent re-run: the deterministic session id
                    // exists — reuse it (the WPF Add would throw; the
                    // SaaS treats it as idempotent, documented).
                    var existingSession = await store.FindCuttingSessionAsync(
                        tenantId, cuttingSessionId, ct);
                    if (existingSession is not null)
                    {
                        cuttingSession = existingSession;
                    }
                }
                sessionCount++;
            }
            catch (Exception ex)
            {
                // WPF: session save failure warns, the workflow continues.
                warnings.Add($"Cutting session save failed for group {group.GroupId}: {ex.Message}");
            }

            // Stage 9 — material allocation: sheets + placed pieces +
            // per-piece allocations (WPF loop).
            var cuttingSheets = new List<CuttingSheet>();
            var placedPieces = new List<CuttingPlacedPiece>();
            var groupAllocations = new List<MaterialAllocation>();

            foreach (var sheet in plan.Sheets)
            {
                ct.ThrowIfCancellationRequested();

                string cuttingSheetId = CuttingIds.BuildCuttingSheetId(
                    cuttingSessionId, sheet.SheetNo);

                // WPF: match the rack by width/height against the
                // selected candidates.
                var matchingRack = selectedRacks
                    .FirstOrDefault(s => s.WidthMm == sheet.Stock.WidthMm
                                      && s.HeightMm == sheet.Stock.HeightMm);
                Guid? glassSheetId = matchingRack?.GlassSheetId;

                string reservationKey = groupReservations.FirstOrDefault()?.ReservationKey ?? "";

                // WPF: no reservation yet → reserve qty 1 for this sheet.
                if (string.IsNullOrWhiteSpace(reservationKey))
                {
                    var reserveResult = await reservations.ReserveAsync(
                        tenantId, actorId, new ReserveRequest(
                            job.Id,
                            glassSheetId ?? Guid.Empty,
                            0, 0,
                            group.GlassFamily,
                            group.DisplayLabel,
                            null,
                            1,
                            $"Cutting sheet {sheet.SheetNo}"),
                        ip, ct);
                    if (reserveResult.Success && reserveResult.Value is not null)
                    {
                        reservationKey = reserveResult.Value.ReservationKey;
                        var row = await store.FindReservationByKeyAsync(
                            tenantId, reservationKey, ct);
                        if (row is not null) groupReservations.Add(row);
                        runReservations.Add(reserveResult.Value);
                    }
                }

                var cutSheet = new CuttingSheet
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CuttingSheetId = cuttingSheetId,
                    CuttingSessionId = cuttingSession.Id,
                    JobOrderId = job.Id,
                    JobRef = job.JobNo,
                    SheetNo = sheet.SheetNo,
                    StockSheetId = glassSheetId,
                    StockSizeLabel = sheet.Stock.SizeLabel,
                    StockWidthMm = sheet.Stock.WidthMm,
                    StockHeightMm = sheet.Stock.HeightMm,
                    ThicknessMm = group.ThicknessMm,
                    Color = group.Color,
                    UsableX = sheet.UsableX,
                    UsableY = sheet.UsableY,
                    UsableWidthMm = sheet.UsableWidth,
                    UsableHeightMm = sheet.UsableHeight,
                    UsedSqm = sheet.UsedSqm,
                    StockSqm = sheet.StockSqm,
                    UtilizationPct = sheet.UtilizationPct,
                    WastagePct = 100 - sheet.UtilizationPct,
                    IsVerified = isVerified,
                    VerificationResult = isVerified
                        ? "Verified"
                        : string.Join("; ", verificationProblems),
                    PlacedPieceCount = sheet.Pieces.Count,
                    CreatedBy = actorId,
                    CreatedAt = DateTime.UtcNow
                };
                cuttingSheets.Add(cutSheet);

                int cutSeq = 0;
                foreach (var placed in sheet.Pieces)
                {
                    ct.ThrowIfCancellationRequested();
                    cutSeq++;

                    string placedPieceLabel =
                        string.IsNullOrWhiteSpace(placed.Piece.PieceLabel)
                            ? placed.Piece.GlassRef
                            : placed.Piece.PieceLabel;

                    // WPF matching: PieceLabel, then PieceId, then GlassRef.
                    var matchingRequirement = group.Requirements.FirstOrDefault(r =>
                        r.PieceLabel == placed.Piece.PieceLabel
                        || r.PieceId == placed.Piece.PieceLabel
                        || r.GlassRef == placed.Piece.GlassRef);

                    string pieceId = matchingRequirement?.PieceId ?? placedPieceLabel;
                    string placedId = CuttingIds.BuildPlacedPieceId(cuttingSheetId, pieceId);
                    string allocationId = string.IsNullOrWhiteSpace(reservationKey)
                        ? ""
                        : CuttingIds.BuildAllocationId(reservationKey, sheet.SheetNo, placedPieceLabel);

                    if (!string.IsNullOrWhiteSpace(allocationId))
                    {
                        groupAllocations.Add(new MaterialAllocation
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            AllocationId = allocationId,
                            ReservationId = groupReservations
                                .FirstOrDefault(r => r.ReservationKey == reservationKey)?.Id,
                            ReservationKey = reservationKey,
                            JobOrderId = job.Id,
                            JobRef = job.JobNo,
                            SpecGroupNumber = matchingRequirement?.SpecGroupNumber ?? 0,
                            SerialNumber = matchingRequirement?.SerialNumber ?? 0,
                            OptimizationSessionId = optSession?.Id,
                            SheetNo = sheet.SheetNo,
                            PlacedPieceLabel = placedPieceLabel,
                            GlassRef = placed.Piece.GlassRef,
                            WidthMm = placed.WidthMm,
                            HeightMm = placed.HeightMm,
                            Quantity = 1,
                            UtilizationPct = sheet.UtilizationPct,
                            WastagePct = 100 - sheet.UtilizationPct,
                            Status = AllocationStatuses.Allocated,
                            CreatedBy = actorId,
                            CreatedAt = DateTime.UtcNow,
                            Notes = $"Placed at {placed.X},{placed.Y} " +
                                    $"rotated={placed.Rotated} seq={cutSeq}"
                        });
                    }

                    placedPieces.Add(new CuttingPlacedPiece
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        PlacedPieceId = placedId,
                        CuttingSheetId = cutSheet.Id,
                        CuttingSessionId = cuttingSession.Id,
                        JobOrderId = job.Id,
                        JobRef = job.JobNo,
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
                        Status = CuttingPieceStatuses.Allocated,
                        SourcePieceId = matchingRequirement?.SourcePieceId,
                        ReservationKey = reservationKey,
                        AllocationId = allocationId,
                        IsFoc = matchingRequirement?.IsFoc ?? false,
                        CreatedBy = actorId,
                        CreatedAt = DateTime.UtcNow,
                        Notes = $"Sheet {sheet.SheetNo} at {placed.X},{placed.Y} " +
                                $"{(placed.Rotated ? "90°" : "0°")} seq {cutSeq}"
                    });
                }
            }

            // Stage 10 — persist the layout + allocations, session →
            // Allocated (WPF batch save).
            try
            {
                await store.PersistCuttingLayoutAsync(
                    tenantId, cuttingSheets, placedPieces, groupAllocations, ct);
                sheetCount += cuttingSheets.Count;
                pieceCount += placedPieces.Count;
                allocationCount += groupAllocations.Count;

                cuttingSession.Status = CuttingSessionStatuses.Allocated;
                await store.TryUpdateCuttingSessionAsync(cuttingSession, ct);
            }
            catch (Exception ex)
            {
                string msg =
                    $"Cutting sheets save failed for group {group.GroupId}: " +
                    $"{ex.Message} — compensation: release reservations";
                warnings.Add(msg);
                errors.Add(msg);
                await ReleaseGroupAsync(tenantId, actorId, groupReservations,
                    $"Compensation for save failure group {group.GroupId}", ip, ct);
                groupResults.Add(new CuttingGroupResultDto(
                    group.GroupId, false, msg, cuttingSessionId,
                    0, 0, groupWarnings));
                continue;
            }

            groupResults.Add(new CuttingGroupResultDto(
                group.GroupId, true,
                $"Group {group.DisplayLabel}: {plan.SheetsUsed} sheets, " +
                $"{placedPieces.Count} pieces, {utilization:0.##}% utilization " +
                "(Best Found, Verified Layout)",
                cuttingSessionId, plan.SheetsUsed, placedPieces.Count, groupWarnings));
        }

        bool success = errors.Count == 0 && sessionCount > 0;
        string message = success
            ? $"Cutting workflow complete: {sessionCount} sessions, " +
              $"{sheetCount} sheets, {pieceCount} pieces, " +
              $"{runReservations.Count} reservations, {allocationCount} " +
              "allocations — terminology: Best Found, Verified Layout"
            : $"Cutting workflow completed with {errors.Count} errors, " +
              $"{warnings.Count} warnings — {sessionCount} sessions succeeded";

        await AuditAsync(tenantId, actorId, "production.cutting_run",
            "cutting_sessions", job.Id, null, new
            {
                jobOrderId = job.Id,
                jobRef = job.JobNo,
                groups = groupResults.Count,
                sessions = sessionCount,
                sheets = sheetCount,
                pieces = pieceCount,
                reservations = runReservations.Count
            }, ip, ct);

        var runResult = new CuttingRunResultDto(
            success, message, job.JobNo, groupResults, sessionCount,
            sheetCount, pieceCount, runReservations.Count, allocationCount,
            warnings, errors);
        return ProductionResult<CuttingRunResultDto>.Ok(runResult, message);
    }

    // ── selected racks (WPF SelectBestSheets) ────────────────────────

    /// <summary>WPF SelectBestSheets(requestedSheetCount: 1): the 80%
    /// fill estimate sets how many sheets the group needs; racks are
    /// taken in waste order until covered. The WPF's unlimited fallback
    /// rack (Sheet.Id == 0) has no SaaS equivalent — racks come from the
    /// Sheet Store.</summary>
    private IReadOnlyList<StockCandidate> SelectRacks(
        MaterialGroup group, IReadOnlyList<StockCandidate> candidates)
    {
        var best = candidates.FirstOrDefault();
        if (best is null) return Array.Empty<StockCandidate>();

        int estimated = Math.Max(
            CuttingStockSelection.EstimateSheetCount(group, best.SheetSqm), 1);

        var selected = new List<StockCandidate>();
        int remaining = estimated;
        foreach (var candidate in candidates)
        {
            if (remaining <= 0) break;
            int take = Math.Min(remaining,
                candidate.EffectiveAvailable > 0 ? candidate.EffectiveAvailable : remaining);
            if (take > 0)
            {
                selected.Add(candidate);
                remaining -= take;
            }
        }

        return selected;
    }

    private async Task ReleaseGroupAsync(
        Guid tenantId, Guid actorId, IReadOnlyList<MaterialReservation> rows,
        string reason, string? ip, CancellationToken ct)
    {
        foreach (var row in rows)
        {
            var released = await reservations.ReleaseAsync(
                tenantId, row.Id, actorId,
                new ReservationActionRequest(reason), ip, ct);
            if (!released.Success)
            {
                // Consumed material is never released — the WPF ignores
                // the failure and keeps compensating the rest.
            }
        }
    }

    // ── Completion (WPF CuttingCompletionService) ────────────────────

    /// <summary>
    /// POST /cutting/pieces/{placedPieceId}/complete — the piece flips to
    /// Completed, the source work item's QtyDone gains 1 (clamped to Qty,
    /// partial production preserved), and the allocation is consumed with
    /// the reservation cascade. Idempotent.
    /// </summary>
    public async Task<ProductionResult<CuttingPlacedPieceDto>> CompletePieceAsync(
        Guid tenantId, Guid actorId, string placedPieceId,
        CompletePieceRequest request, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(placedPieceId))
        {
            return ProductionResult<CuttingPlacedPieceDto>.Fail(
                ProductionErrorCode.ValidationFailed, "PlacedPieceId required.");
        }

        var placed = await store.FindPlacedPieceAsync(tenantId, placedPieceId, ct);
        if (placed is null)
        {
            return ProductionResult<CuttingPlacedPieceDto>.Fail(
                ProductionErrorCode.NotFound, "Placed piece not found.");
        }

        if (string.Equals(placed.Status, CuttingPieceStatuses.Completed,
                StringComparison.OrdinalIgnoreCase))
        {
            return ProductionResult<CuttingPlacedPieceDto>.Ok(ToDto(placed),
                "Already completed (idempotent).");
        }

        if (string.Equals(placed.Status, CuttingPieceStatuses.Cancelled,
                StringComparison.OrdinalIgnoreCase))
        {
            return ProductionResult<CuttingPlacedPieceDto>.Fail(
                ProductionErrorCode.Conflict, "Cannot complete cancelled piece.");
        }

        placed.Status = CuttingPieceStatuses.Completed;

        // Work item QtyDone — piece flow: +1, clamped (WPF
        // ApplyDoneQtyOnTrackedItem).
        if (placed.SourcePieceId is { } sourceId && sourceId != Guid.Empty)
        {
            var piece = await production.FindAsync(tenantId, sourceId, ct);
            if (piece is not null && !piece.IsCancelled)
            {
                int newDone = Math.Min(piece.Qty, piece.QtyDone + 1);
                piece.QtyDone = newDone;
                piece.Notes = $"Cutting completed piece {placed.PieceLabel} at " +
                              $"{placed.X},{placed.Y} {(placed.IsRotated ? "90°" : "0°")}";
                if (!string.Equals(piece.Status, WorkItemStatuses.QcFailed,
                        StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(piece.Status, WorkItemStatuses.Rework,
                        StringComparison.OrdinalIgnoreCase))
                {
                    piece.Status = newDone >= piece.Qty
                        ? WorkItemStatuses.Completed
                        : newDone > 0 ? WorkItemStatuses.InProgress
                        : WorkItemStatuses.Pending;
                }
                piece.CompletedAt = string.Equals(piece.Status, WorkItemStatuses.Completed,
                    StringComparison.OrdinalIgnoreCase)
                    ? DateTime.UtcNow : null;
                piece.CompletedBy = piece.CompletedAt is null ? null : actorId;
                piece.UpdatedAt = DateTime.UtcNow;
                piece.UpdatedBy = actorId;
                if (!await production.TryUpdateAsync(piece, ct))
                {
                    return ProductionResult<CuttingPlacedPieceDto>.Fail(
                        ProductionErrorCode.ConcurrencyConflict,
                        "The work item was modified by someone else — reload and retry.");
                }

                // The work item is authoritative — mirror onto the
                // sequence-0 execution (Phase 26a parity).
                await control.SyncLinkedExecutionsAsync(tenantId, piece, ct);

                await production.AppendEventAsync(tenantId, new ProductionEvent
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProductionPieceId = piece.Id,
                    EventType = ProductionEntryTypes.Stage,
                    JobOrderId = piece.JobOrderId,
                    Stage = piece.Stage,
                    SpecGroupNumber = piece.SpecGroupNumber,
                    SerialNumber = piece.SerialNumber,
                    Position = piece.Position,
                    SpecName = piece.SpecName,
                    GlassRef = piece.GlassRef,
                    Qty = newDone,
                    DoneBy = actorId,
                    Reason = "",
                    CreatedAt = DateTime.UtcNow,
                    Payload = JsonSerializer.Serialize(new
                    {
                        placedPieceId,
                        pieceLabel = placed.PieceLabel,
                        x = placed.X,
                        y = placed.Y,
                        rotated = placed.IsRotated,
                        source = "cutting_completion"
                    })
                }, ct);
            }
        }

        // Planning consume only — Consumed means the production hold is
        // finished, not that a sheet physically left the Sheet Store
        // (WPF contract).
        if (!string.IsNullOrWhiteSpace(placed.AllocationId))
        {
            var allocation = await store.FindAllocationAsync(
                tenantId, placed.AllocationId, ct);
            if (allocation is not null)
            {
                if (!AllocationStatuses.CanConsume(allocation.Status))
                {
                    return ProductionResult<CuttingPlacedPieceDto>.Fail(
                        ProductionErrorCode.Conflict,
                        $"Cannot consume {allocation.Status} allocation.");
                }

                if (!string.Equals(allocation.Status, AllocationStatuses.Consumed,
                        StringComparison.OrdinalIgnoreCase))
                {
                    allocation.Status = AllocationStatuses.Consumed;
                    allocation.ConsumedAt = DateTime.UtcNow;
                    allocation.Notes = $"Consumed via cutting completion {placedPieceId}";
                    await store.TryUpdateAllocationAsync(allocation, ct);
                }

                await CascadeReservationConsumedAsync(
                    tenantId, allocation.ReservationKey, ct);
            }
        }

        if (!await store.TryUpdatePlacedPieceAsync(placed, ct))
        {
            return ProductionResult<CuttingPlacedPieceDto>.Fail(
                ProductionErrorCode.ConcurrencyConflict,
                "The placed piece was modified by someone else — reload and retry.");
        }

        await AuditAsync(tenantId, actorId, "production.cutting_piece_complete",
            "cutting_placed_pieces", placed.Id, null,
            new { placedPieceId, pieceLabel = placed.PieceLabel }, ip, ct);

        return ProductionResult<CuttingPlacedPieceDto>.Ok(ToDto(placed),
            $"Completed {placed.PieceLabel}");
    }

    /// <summary>Cascade shared with AllocationCostService (WPF: the
    /// reservation flips to Consumed only when ALL its allocations are
    /// consumed, never overwriting Released/Cancelled).</summary>
    private async Task CascadeReservationConsumedAsync(
        Guid tenantId, string reservationKey, CancellationToken ct)
    {
        var reservationRow = await store.FindReservationByKeyAsync(
            tenantId, reservationKey, ct);
        if (reservationRow is null) return;
        if (string.Equals(reservationRow.Status, ReservationStatuses.Consumed,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(reservationRow.Status, ReservationStatuses.Released,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(reservationRow.Status, ReservationStatuses.Cancelled,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var allAllocations = await store.ListAllocationsByReservationAsync(
            tenantId, reservationKey, ct);
        bool allConsumed = allAllocations.Count > 0 && allAllocations.All(
            a => string.Equals(a.Status, AllocationStatuses.Consumed,
                StringComparison.OrdinalIgnoreCase));
        if (allConsumed)
        {
            reservationRow.Status = ReservationStatuses.Consumed;
            reservationRow.UpdatedAt = DateTime.UtcNow;
            await store.TryUpdateReservationAsync(reservationRow, ct);
        }
    }

    /// <summary>
    /// POST /cutting/pieces/{placedPieceId}/breakage — breakage during
    /// cutting: the placed piece flips to Breakage/FOC and the Phase 14
    /// replacement flow creates the replacement work item (the original
    /// is never silently modified).
    /// </summary>
    public async Task<ProductionResult<CuttingPlacedPieceDto>> RecordBreakageDuringCuttingAsync(
        Guid tenantId, Guid actorId, string placedPieceId,
        CuttingBreakageRequest request, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(placedPieceId))
        {
            return ProductionResult<CuttingPlacedPieceDto>.Fail(
                ProductionErrorCode.ValidationFailed, "PlacedPieceId required.");
        }
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ProductionResult<CuttingPlacedPieceDto>.Fail(
                ProductionErrorCode.ValidationFailed, "A breakage reason is required.");
        }

        var placed = await store.FindPlacedPieceAsync(tenantId, placedPieceId, ct);
        if (placed is null)
        {
            return ProductionResult<CuttingPlacedPieceDto>.Fail(
                ProductionErrorCode.NotFound, "Placed piece not found.");
        }

        placed.Status = request.IsFoc
            ? CuttingPieceStatuses.Foc
            : CuttingPieceStatuses.Breakage;
        placed.Notes = $"Breakage during cutting: {request.Reason} IsFoc={request.IsFoc}";
        if (!await store.TryUpdatePlacedPieceAsync(placed, ct))
        {
            return ProductionResult<CuttingPlacedPieceDto>.Fail(
                ProductionErrorCode.ConcurrencyConflict,
                "The placed piece was modified by someone else — reload and retry.");
        }

        // Phase 14 replacement (WPF RecordBreakageAsync qty 1).
        if (placed.SourcePieceId is { } sourceId && sourceId != Guid.Empty)
        {
            var breakage = await replacements.RecordBreakageAsync(
                tenantId, actorId, new ReplacementBreakageRequest(
                    sourceId, 1, request.Reason, request.IsFoc,
                    $"Breakage during cutting {placedPieceId}"),
                ip, ct);
            if (!breakage.Success)
            {
                return ProductionResult<CuttingPlacedPieceDto>.Fail(
                    breakage.ErrorCode, $"Breakage record failed: {breakage.Message}");
            }
            return ProductionResult<CuttingPlacedPieceDto>.Ok(ToDto(placed),
                $"Breakage recorded: {breakage.Message} — replacement traceable " +
                $"Original {placed.PieceId} → Replacement {breakage.Value?.ReplacementId}");
        }

        return ProductionResult<CuttingPlacedPieceDto>.Ok(ToDto(placed),
            $"Breakage recorded for {placed.PieceLabel} — no source work item, " +
            "manual replacement needed");
    }

    /// <summary>GET /cutting/progress?jobOrderId= — WPF
    /// GetProgressByJobAsync: remaining = required − completed −
    /// breakage − foc, clamped at 0.</summary>
    public async Task<ProductionResult<CuttingProgressDto>> GetProgressByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var job = await production.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<CuttingProgressDto>.Fail(
                ProductionErrorCode.NotFound,
                $"Job order {jobOrderId} was not found.");
        }

        var placed = await store.ListPlacedPiecesByJobAsync(tenantId, job.Id, ct);
        int required = placed.Count;
        int completed = Count(placed, CuttingPieceStatuses.Completed);
        int breakage = Count(placed, CuttingPieceStatuses.Breakage);
        int foc = Count(placed, CuttingPieceStatuses.Foc);
        int rework = Count(placed, CuttingPieceStatuses.Rework);
        int qcPending = Count(placed, CuttingPieceStatuses.QcPending);
        int remaining = Math.Max(0, required - completed - breakage - foc);

        return ProductionResult<CuttingProgressDto>.Ok(new CuttingProgressDto(
            required, completed, remaining, breakage, foc, rework, qcPending));
    }

    /// <summary>POST /cutting/sessions/{id}/complete — complete every
    /// Allocated piece, then flip the session to Completed (WPF
    /// CompleteSessionAsync).</summary>
    public async Task<ProductionResult<string>> CompleteSessionAsync(
        Guid tenantId, Guid actorId, string cuttingSessionId, string? ip,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cuttingSessionId))
        {
            return ProductionResult<string>.Fail(
                ProductionErrorCode.ValidationFailed, "SessionId required.");
        }

        var session = await store.FindCuttingSessionAsync(tenantId, cuttingSessionId, ct);
        if (session is null)
        {
            return ProductionResult<string>.Fail(
                ProductionErrorCode.NotFound, "Cutting session not found.");
        }

        var placed = await store.ListPlacedPiecesBySessionAsync(
            tenantId, session.Id, ct);
        var toComplete = placed
            .Where(p => string.Equals(p.Status, CuttingPieceStatuses.Allocated,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        int completed = 0;
        foreach (var p in toComplete)
        {
            ct.ThrowIfCancellationRequested();
            var result = await CompletePieceAsync(
                tenantId, actorId, p.PlacedPieceId,
                new CompletePieceRequest(null), ip, ct);
            if (result.Success) completed++;
        }

        session.Status = CuttingSessionStatuses.Completed;
        session.CompletedAt = DateTime.UtcNow;
        await store.TryUpdateCuttingSessionAsync(session, ct);

        await AuditAsync(tenantId, actorId, "production.cutting_session_complete",
            "cutting_sessions", session.Id, null,
            new { cuttingSessionId, completed, total = toComplete.Count }, ip, ct);

        return ProductionResult<string>.Ok(
            $"Completed {completed}/{toComplete.Count} pieces for session {cuttingSessionId}");
    }

    // ── Reads ────────────────────────────────────────────────────────

    public async Task<ProductionResult<CuttingSessionDto>> GetSessionAsync(
        Guid tenantId, string cuttingSessionId, CancellationToken ct)
    {
        var session = await store.FindCuttingSessionAsync(tenantId, cuttingSessionId, ct);
        if (session is null)
        {
            return ProductionResult<CuttingSessionDto>.Fail(
                ProductionErrorCode.NotFound, "Cutting session not found.");
        }
        return ProductionResult<CuttingSessionDto>.Ok(ToDto(session));
    }

    /// <summary>GET /cutting/sessions?jobOrderId= — WPF
    /// GetSessionsByJobAsync (most recent first).</summary>
    public async Task<ProductionResult<IReadOnlyList<CuttingSessionDto>>> ListSessionsByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var rows = await store.ListCuttingSessionsByJobAsync(tenantId, jobOrderId, ct);
        return ProductionResult<IReadOnlyList<CuttingSessionDto>>.Ok(
            rows.Select(ToDto).ToList());
    }

    /// <summary>GET /cutting/sessions/{id}/sheets — WPF
    /// GetSheetsBySessionAsync (SheetNo order).</summary>
    public async Task<ProductionResult<IReadOnlyList<CuttingSheetDto>>> ListSheetsBySessionAsync(
        Guid tenantId, Guid cuttingSessionId, CancellationToken ct)
    {
        var session = await store.FindCuttingSessionAsync(tenantId, cuttingSessionId, ct);
        if (session is null)
        {
            return ProductionResult<IReadOnlyList<CuttingSheetDto>>.Fail(
                ProductionErrorCode.NotFound, "Cutting session not found.");
        }
        var rows = await store.ListCuttingSheetsBySessionAsync(tenantId, session.Id, ct);
        return ProductionResult<IReadOnlyList<CuttingSheetDto>>.Ok(
            rows.Select(ToDto).ToList());
    }

    /// <summary>GET /cutting/sessions/{id}/pieces — WPF
    /// GetPlacedPiecesBySessionAsync (CutSequence order).</summary>
    public async Task<ProductionResult<IReadOnlyList<CuttingPlacedPieceDto>>> ListPiecesBySessionAsync(
        Guid tenantId, Guid cuttingSessionId, CancellationToken ct)
    {
        var session = await store.FindCuttingSessionAsync(tenantId, cuttingSessionId, ct);
        if (session is null)
        {
            return ProductionResult<IReadOnlyList<CuttingPlacedPieceDto>>.Fail(
                ProductionErrorCode.NotFound, "Cutting session not found.");
        }
        var rows = await store.ListPlacedPiecesBySessionAsync(tenantId, session.Id, ct);
        return ProductionResult<IReadOnlyList<CuttingPlacedPieceDto>>.Ok(
            rows.Select(ToDto).ToList());
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static int Count(IEnumerable<CuttingPlacedPiece> rows, string status) =>
        rows.Count(p => string.Equals(p.Status, status, StringComparison.OrdinalIgnoreCase));

    private static CuttingRunResultDto FailedRun(string jobRef, string message) => new(
        false, message, jobRef, Array.Empty<CuttingGroupResultDto>(),
        0, 0, 0, 0, 0, Array.Empty<string>(), new[] { message });

    internal static CuttingRequirementDto ToDto(CuttingRequirement r) => new(
        r.PieceId, r.PieceLabel, r.PieceIndex, r.PieceSequence, r.JobRef,
        r.SpecGroupNumber, r.SerialNumber, r.SpecName, r.GlassRef, r.Position,
        r.WidthMm, r.HeightMm, r.GlassFamily, r.Color, r.ThicknessMm,
        r.Coating, r.Treatment, r.RequiresTempering, r.IsFoc,
        r.ReplacementSequence, r.MaterialKey);

    internal static CuttingSessionDto ToDto(CuttingSession s) => new(
        s.Id, s.CuttingSessionId, s.JobOrderId, s.JobRef, s.SourceProformaId,
        s.OptimizationSessionId, s.MaterialGroupId, s.TotalPieces, s.SheetsUsed,
        s.UtilizationPct, s.WastagePct, s.Status, s.RotationPolicy, s.SplitMode,
        s.EffortLevel, s.IsVerified, s.VerificationResult, s.CompletedAt,
        s.CreatedAt, s.Notes);

    internal static CuttingSheetDto ToDto(CuttingSheet s) => new(
        s.Id, s.CuttingSheetId, s.CuttingSessionId, s.JobRef,
        s.SheetNo, s.StockSheetId, s.StockSizeLabel, s.StockWidthMm,
        s.StockHeightMm, s.ThicknessMm, s.Color, s.UsableX, s.UsableY,
        s.UsableWidthMm, s.UsableHeightMm, s.UsedSqm, s.StockSqm,
        s.UtilizationPct, s.WastagePct, s.IsVerified, s.PlacedPieceCount,
        s.CreatedAt);

    internal static CuttingPlacedPieceDto ToDto(CuttingPlacedPiece p) => new(
        p.Id, p.PlacedPieceId, p.CuttingSheetId, p.CuttingSessionId,
        p.JobRef, p.SpecGroupNumber, p.SerialNumber, p.GlassRef, p.SpecName,
        p.Position, p.PieceId, p.PieceLabel, p.WidthMm, p.HeightMm, p.X, p.Y,
        p.IsRotated, p.CutSequence, p.Status, p.SourcePieceId, p.ReservationKey,
        p.AllocationId, p.IsFoc, p.Notes);

    private async Task AuditAsync(
        Guid tenantId, Guid? actorUserId, string action, string entityType,
        Guid entityId, object? before, object? after, string? ip, CancellationToken ct)
        => await audit.WriteAsync(tenantId, actorUserId, action, entityType,
            entityId, ip, null,
            before is null ? null : JsonSerializer.Serialize(before),
            after is null ? null : JsonSerializer.Serialize(after),
            ct).ConfigureAwait(false);
}
