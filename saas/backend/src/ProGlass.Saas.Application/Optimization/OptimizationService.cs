using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Optimization;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Optimization;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 11 service — the WPF optimization workflow core:
//    • pieces come from the job's CUTTING plan (the distribution
//      engine's pane routing — "the Optimization module reuses the SAME
//      pane routing as the floor", WPF ProductionOptimizationBridge) or
//      from an explicit request;
//    • pieces are grouped by glass COMPOSITION (PaneDisplayName — the
//      merge key: two specs sharing the same outer pane collapse into
//      ONE cutting group);
//    • each group packs against the racks of its own thickness (margins
//      from the house table: thin 15/15/15/15 kerf 3, medium 30 all
//      kerf 3, thick 50/50/0/50 kerf 4);
//    • the result is validated (no overlap / bounds / conservation)
//      BEFORE it is persisted — a plan nobody validated is a plan nobody
//      can cut.
//  The QUICK portfolio's extra candidates and the OPTIMIZE multi-start
//  are later slices (documented).
// ═══════════════════════════════════════════════════════════════════════

public sealed class OptimizationService(
    IOptimizationStore store, IAuditWriter audit,
    Production.IProductionStore productionStore,
    Subscriptions.PlanLimitGuard limits)
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    /// <summary>
    /// POST /api/v1/optimization/run — one session per composition
    /// group, all persisted with their cut sequences and leftovers.
    /// </summary>
    public async Task<OptimizationResult<OptimizationRunDto>> RunAsync(
        Guid tenantId, Guid actorId, RunOptimizationRequest request,
        string? ip, CancellationToken ct)
    {
        // Phase 18 — per-period optimization-jobs limit (plan §6; catalog
        // value, NULL = unlimited, hard-coded nowhere).
        var limitCheck = await limits.ResolveAsync(
            tenantId, Domain.Subscriptions.ModuleFeatureMap.Optimization, ct);
        if (limitCheck?.Limit is int cap)
        {
            var used = await store.CountSessionsSinceAsync(
                tenantId, limitCheck.PeriodStart, ct);
            if (used >= cap)
            {
                return OptimizationResult<OptimizationRunDto>.Fail(
                    OptimizationErrorCode.SubscriptionLimitExceeded,
                    $"The current plan's optimization-job limit ({cap} per " +
                    $"billing period) has been reached — {used} runs since " +
                    $"{limitCheck.PeriodStart:yyyy-MM-dd}. Choose a plan " +
                    "with a higher limit in Settings → Subscription.");
            }
        }

        // ── pieces: explicit or from the job's cutting plan ──────────
        List<CutPiece> pieces;
        JobOrder? job = null;
        if (request.JobOrderId.HasValue)
        {
            job = await store.FindJobAsync(tenantId, request.JobOrderId.Value, ct);
            if (job is null)
            {
                return OptimizationResult<OptimizationRunDto>.Fail(
                    OptimizationErrorCode.NotFound,
                    $"Job order {request.JobOrderId} was not found.");
            }

            var specs = await productionStore.GetSpecInputsAsync(
                tenantId, job.ProformaId, ct);
            var charges = await productionStore.GetChargeInputsAsync(
                tenantId, job.ProformaId, ct);
            var plan = ProductionDistributionEngine.Distribute(
                job.JobNo, job.CustomerName, job.ProjectName, "", specs, charges);
            if (plan.Items.Count == 0)
            {
                return OptimizationResult<OptimizationRunDto>.Fail(
                    OptimizationErrorCode.ValidationFailed,
                    $"Job order {job.JobNo} has no cutting items.");
            }

            // The CUTTING stage carries the pane routing: W1×H1 per
            // occurrence, spec = the pane's composition.
            pieces = plan.Items
                .Where(i => i.Stage == "Cutting")
                .Select(i => new CutPiece(
                    JobRef: job.JobNo,
                    GlassRef: i.GlassRef,
                    SpecLabel: ResolveComposition(i.SpecName, i.Position),
                    Position: i.Position,
                    Color: "",
                    ThicknessMm: ParseThickness(i.SpecName),
                    WidthMm: i.WidthMm,
                    HeightMm: i.HeightMm,
                    AllowRotate: true,
                    PieceLabel: $"{i.GlassRef} · {i.SpecGroupNumber}/{i.SerialNumber}",
                    MaterialKey: i.BuildType,
                    SpecGroupNumber: i.SpecGroupNumber,
                    SourceRowSerial: i.SerialNumber))
                .ToList();
        }
        else if (request.Pieces is { Count: > 0 })
        {
            pieces = request.Pieces
                .SelectMany(p => Enumerable.Range(1, Math.Max(1, p.Qty)),
                    (p, _) => new CutPiece(
                        JobRef: "", GlassRef: p.GlassRef, SpecLabel: p.SpecLabel,
                        Position: p.Position, Color: p.Color,
                        ThicknessMm: p.ThicknessMm, WidthMm: p.WidthMm,
                        HeightMm: p.HeightMm, AllowRotate: p.AllowRotate,
                        PieceLabel: p.GlassRef, MaterialKey: "",
                        SpecGroupNumber: p.SpecGroupNumber,
                        SourceRowSerial: p.SourceRowSerial))
                .ToList();
        }
        else
        {
            return OptimizationResult<OptimizationRunDto>.Fail(
                OptimizationErrorCode.ValidationFailed,
                "Supply pieces or a jobOrderId.");
        }

        if (pieces.Count == 0)
        {
            return OptimizationResult<OptimizationRunDto>.Fail(
                OptimizationErrorCode.ValidationFailed,
                "No pieces to optimize.");
        }

        // ── rotation / split ─────────────────────────────────────────
        var rotation = string.Equals(request.Rotation, "Locked", StringComparison.OrdinalIgnoreCase)
            ? RotationPolicy.Locked
            : RotationPolicy.Free90;
        var split = string.Equals(request.Split, "VerticalFirst", StringComparison.OrdinalIgnoreCase)
            ? SplitMode.VerticalFirst
            : string.Equals(request.Split, "LongerLeftover", StringComparison.OrdinalIgnoreCase)
                ? SplitMode.LongerLeftover
                : SplitMode.HorizontalFirst;

        // ── effort + plate pattern (Phase 25a, WPF PackOptimize) ────
        var effort = Math.Clamp(request.EffortLevel ?? 3, 1, 5);
        var platePattern = ParsePlatePattern(request.PlatePattern);

        // ── group by composition (the merge key) ─────────────────────
        var warnings = new List<string>();
        var sessions = new List<OptimizationSession>();
        var allSheets = new List<OptimizationSheet>();

        foreach (var group in pieces.GroupBy(p => p.SpecLabel,
                     StringComparer.Ordinal))
        {
            var thickness = group.Max(p => p.ThicknessMm);
            var margins = MarginProfile.DefaultFor(thickness, request.MinBreakoutMm);

            // Stock pool: explicit, or every rack of this thickness.
            IReadOnlyList<StockSize> stocks;
            if (request.Stocks is { Count: > 0 })
            {
                stocks = request.Stocks
                    .Select(s => new StockSize(
                        s.WidthMm, s.HeightMm, s.ThicknessMm, s.Color,
                        s.Label, "", s.MaxSheets))
                    .ToList();
            }
            else
            {
                var racks = (await store.ListSheetsAsync(tenantId, ct))
                    .Where(g => g.ThicknessMm == thickness)
                    .Select(g => new StockSize(
                        g.WidthMm, g.HeightMm, g.ThicknessMm, g.Color,
                        $"{g.WidthMm}×{g.HeightMm} {g.ThicknessMm}mm {g.Color}",
                        "", Math.Max(0, g.Stock - g.Used)))
                    .ToList();
                if (racks.Count == 0)
                {
                    warnings.Add(
                        $"Group '{group.Key}': no {thickness}mm racks in the " +
                        "Sheet Store — supply stocks explicitly.");
                    continue;
                }
                stocks = racks;
            }

            // Effort 1 is exactly the QUICK pass; 2..5 widen the
            // multi-start grid (orderings × splits × deterministic
            // shuffles) with the governor's budget and tier machinery.
            var plan = effort <= 1
                ? GuillotinePacker.PackQuick(group.ToList(), stocks, margins, rotation, split)
                : GuillotinePacker.PackOptimize(group.ToList(), stocks, margins, rotation,
                    effortLevel: effort, platePattern: platePattern);
            if (!plan.ConservationVerified || !plan.GeometryVerified)
            {
                warnings.AddRange(plan.ValidationProblems);
            }

            var session = await store.PersistAsync(
                tenantId, actorId,
                job is null ? "manual" : "job_order",
                job?.Id,
                group.Key,
                plan,
                JsonSerializer.Serialize(new
                {
                    rotation = rotation.ToString(),
                    split = split.ToString(),
                    effortLevel = effort,
                    platePattern = platePattern.ToString(),
                    thicknessMm = thickness,
                    minBreakoutMm = margins.MinBreakoutMm
                }),
                ct);
            sessions.Add(session);
            warnings.AddRange(plan.ScaleNotes);

            var sheets = await store.ListSheetsForSessionAsync(
                tenantId, session.Id, ct);
            allSheets.AddRange(sheets);
        }

        if (sessions.Count == 0)
        {
            return OptimizationResult<OptimizationRunDto>.Fail(
                OptimizationErrorCode.ValidationFailed,
                "No run could be produced: " + string.Join(" ", warnings));
        }

        await AuditAsync(tenantId, actorId, "optimization.run_completed",
            "optimization_sessions", sessions[0].Id, null, new
            {
                sessions = sessions.Count,
                sheets = allSheets.Count,
                jobOrderNo = job?.JobNo
            }, ip, ct);

        return OptimizationResult<OptimizationRunDto>.Ok(
            new OptimizationRunDto(
                sessions.Select(ToDto).ToList(),
                (await BuildSheetDtosAsync(tenantId, allSheets, ct)).ToList(),
                warnings));
    }

    /// <summary>The pane composition for a cutting item: the WPF cuts by
    /// PANE, and the optimizer merges by PaneDisplayName. Position "Glass"
    /// (SGU) uses the first pane; Outer/Inner/Lam × use the matching pane
    /// from the spec grammar.</summary>
    private static string ResolveComposition(string specName, string position)
    {
        var panes = SpecGrammar.ResolveAllPanes(specName);
        if (panes.Count == 0)
        {
            return specName;
        }

        return position switch
        {
            "Inner" or "Lam Inner" => ProductionDistributionEngine.PaneDisplayName(
                panes[^1]),
            "Lam Outer" => panes.Count > 1
                ? ProductionDistributionEngine.PaneDisplayName(panes[1])
                : ProductionDistributionEngine.PaneDisplayName(panes[0]),
            _ => ProductionDistributionEngine.PaneDisplayName(panes[0])
        };
    }

    private static int ParseThickness(string specName)
    {
        var panes = SpecGrammar.ResolveAllPanes(specName);
        return panes.Count > 0 ? panes[0].ThicknessMm : 0;
    }

    // ── reads ───────────────────────────────────────────────────────

    public async Task<OptimizationResult<OptimizationSessionDto>> GetSessionAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var session = await store.FindSessionAsync(tenantId, id, ct);
        return session is null
            ? OptimizationResult<OptimizationSessionDto>.Fail(
                OptimizationErrorCode.NotFound,
                $"Optimization session {id} was not found.")
            : OptimizationResult<OptimizationSessionDto>.Ok(ToDto(session));
    }

    public async Task<OptimizationResult<IReadOnlyList<OptimizationSheetDto>>> GetSheetsAsync(
        Guid tenantId, Guid sessionId, CancellationToken ct)
    {
        var session = await store.FindSessionAsync(tenantId, sessionId, ct);
        if (session is null)
        {
            return OptimizationResult<IReadOnlyList<OptimizationSheetDto>>.Fail(
                OptimizationErrorCode.NotFound,
                $"Optimization session {sessionId} was not found.");
        }

        var sheets = await store.ListSheetsForSessionAsync(tenantId, sessionId, ct);
        return OptimizationResult<IReadOnlyList<OptimizationSheetDto>>.Ok(
            await BuildSheetDtosAsync(tenantId, sheets, ct));
    }

    private async Task<List<OptimizationSheetDto>> BuildSheetDtosAsync(
        Guid tenantId, IReadOnlyList<OptimizationSheet> sheets, CancellationToken ct)
    {
        if (sheets.Count == 0)
        {
            return new List<OptimizationSheetDto>();
        }

        var ids = sheets.Select(s => s.Id).ToList();
        var placements = (await store.ListPlacementsAsync(tenantId, ids, ct))
            .GroupBy(p => p.SheetId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var cuts = (await store.ListCutsAsync(tenantId, ids, ct))
            .GroupBy(c => c.SheetId)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Seq).ToList());
        var leftovers = (await store.ListLeftoversAsync(tenantId, ids, ct))
            .GroupBy(l => l.SheetId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return sheets.Select(s => new OptimizationSheetDto(
            s.Id, s.SheetNo, s.StockWMm, s.StockHMm, s.ThicknessMm, s.Color,
            s.UsableX, s.UsableY, s.UsableW, s.UsableH,
            s.UsedSqm, s.StockSqm, s.UtilizationPct, s.TrimCutCount,
            placements.GetValueOrDefault(s.Id, new List<OptimizationPlacement>())
                .Select(p => new PlacementDto(
                    p.Id, s.SheetNo, p.PieceRef, p.GlassRef, p.SpecLabel,
                    p.Position, p.XMm, p.YMm, p.WMm, p.HMm, p.Rotated)).ToList(),
            cuts.GetValueOrDefault(s.Id, new List<OptimizationCut>())
                .Select(c => new CutDto(
                    c.Seq, c.Axis, c.AtMm, c.FromX, c.FromY, c.FromW,
                    c.FromH, c.Stage, c.IsTrim)).ToList(),
            leftovers.GetValueOrDefault(s.Id, new List<OptimizationLeftover>())
                .Select(l => new LeftoverDto(
                    s.SheetNo, l.XMm, l.YMm, l.WMm, l.HMm, l.IsReusable)).ToList()))
            .ToList();
    }

    private static OptimizationSessionDto ToDto(OptimizationSession s) => new(
        s.Id, s.DocumentType, s.DocumentId, s.CompositionKey, s.Status,
        s.Strategy, s.SheetsUsed, s.UnplacedCount, s.TotalUsedSqm,
        s.TotalStockSqm, s.UtilizationPct, s.SheetsLowerBound,
        s.ConservationVerified, s.GeometryVerified,
        ParseStrings(s.ValidationProblems), ParseStrings(s.ScaleNotes),
        s.CreatedAt);

    private static IReadOnlyList<string> ParseStrings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)
                ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private async Task AuditAsync(
        Guid tenantId, Guid? actorUserId, string action, string entityType,
        Guid entityId, object? before, object? after, string? ip, CancellationToken ct)
        => await audit.WriteAsync(tenantId, actorUserId, action, entityType,
            entityId, ip, null,
            before is null ? null : JsonSerializer.Serialize(before),
            after is null ? null : JsonSerializer.Serialize(after),
            ct).ConfigureAwait(false);
    /// <summary>Parses the plate pattern name (case-insensitive);
    /// unknown → Yield, the WPF default.</summary>
    private static PlatePattern ParsePlatePattern(string? value) =>
        Enum.TryParse<PlatePattern>(value, ignoreCase: true, out var parsed)
            ? parsed
            : PlatePattern.Yield;

}
