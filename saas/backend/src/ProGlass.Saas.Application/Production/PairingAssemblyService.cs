using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Production;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 26a service — the WPF pairing + assembly core:
//    • PairingService.PairAsync (deterministic pairing for DGU: Outer +
//      Inner of the SAME JobRef|Spec|Serial paired by piece index
//      Pair-001 Outer + Pair-001 Inner — never by dimension match, never
//      across jobs/specs; gating: pairing work item not cancelled/
//      rejected/QC-failed, outer AND inner cutting complete; qty = the
//      pairing work item's Qty, never the pane-cut sum; idempotent on
//      PairingId)
//    • DoublingLaminationService.AssembleAsync (DGU / Lamination /
//      DGU+LAM assemblies preserving component lineage — Outer + Inner +
//      synthetic Spacer/Interlayer — never flattened into an anonymous
//      finished product; gating: doubling work item live + pairing work
//      item QtyDone ≥ Qty; idempotent on AssemblyId)
//    • GetByJob reads + assembly traceability
//  SaaS differences (documented, mapping §1.7c): the WPF resolves pane
//  identities from its CuttingPlacedPieces table first — the SaaS has no
//  placed-piece rows, so the resolution chain is execution PieceId →
//  FromWorkItem(cutting Wi, index) (the same deterministic
//  JOB|SPEC|SERIAL|POSITION|WxH|P0001 scheme); no NotifyFloor event.
// ═══════════════════════════════════════════════════════════════════════

public sealed class PairingAssemblyService(
    IProcessControlStore store, IProductionStore production, IAuditWriter audit)
{
    /// <summary>
    /// POST /pairings — WPF PairAsync: pair compatible components using
    /// deterministic identity JobRef|Spec|Serial|Position|Component|
    /// Piece index — not random, not DB order, not GUIDs.
    /// </summary>
    public async Task<ProductionResult<IReadOnlyList<ProcessPairingDto>>> PairAsync(
        Guid tenantId, Guid actorId, PairUnitRequest request,
        string? ip, CancellationToken ct)
    {
        var (ok, message, spec, serial, job, unitPieces) =
            await ResolveUnitAsync(tenantId, request.JobOrderId,
                request.SpecGroupNumber, request.SerialNumber, ct);
        if (!ok)
        {
            return ProductionResult<IReadOnlyList<ProcessPairingDto>>.Fail(
                ProductionErrorCode.NotFound, message!);
        }

        var jobRef = job!.JobNo;
        var workItems = unitPieces!; // already !IsCancelled

        // Supporting-record guards (never writes the work item's QtyDone).
        var pairingWi = workItems.FirstOrDefault(
            w => w.Stage == ProductionStages.PairingName);
        if (pairingWi is not null)
        {
            if (pairingWi.IsCancelled || pairingWi.Status == WorkItemStatuses.Cancelled)
            {
                return FailPair("Pairing work item cancelled.");
            }
            if (pairingWi.Status == WorkItemStatuses.Rejected)
            {
                return FailPair("Rejected pairing work cannot be paired.");
            }
            if (pairingWi.Status == WorkItemStatuses.QcFailed)
            {
                return FailPair("QC failed — cannot pair.");
            }
        }

        var outerItems = workItems
            .Where(w => IsOuterPosition(w.Position))
            .OrderBy(w => w.Position, StringComparer.OrdinalIgnoreCase)
            .ThenBy(w => w.Id)
            .ToList();
        var innerItems = workItems
            .Where(w => IsInnerPosition(w.Position))
            .OrderBy(w => w.Position, StringComparer.OrdinalIgnoreCase)
            .ThenBy(w => w.Id)
            .ToList();

        // For DGU: at least one Outer and one Inner. SGU has no pairing.
        if (outerItems.Count == 0 || innerItems.Count == 0)
        {
            bool hasUnit = workItems.Any(w =>
                string.Equals(w.Position, "Unit", StringComparison.OrdinalIgnoreCase));
            if (!hasUnit)
            {
                return FailPair(
                    $"Pairing not required or components missing for {jobRef} " +
                    $"Spec {spec} Serial {serial} — Outer {outerItems.Count} " +
                    $"Inner {innerItems.Count}. For SGU, pairing is not required.");
            }
        }

        // Finished-unit count = the pairing work item's Qty (never the
        // pane-cut sum).
        var doublingWi = workItems.FirstOrDefault(
            w => w.Stage == ProductionStages.DoublingLaminationName);
        var outerCutting = workItems.FirstOrDefault(w =>
            w.Stage == ProductionStages.CuttingName && IsOuterPosition(w.Position));
        var innerCutting = workItems.FirstOrDefault(w =>
            w.Stage == ProductionStages.CuttingName && IsInnerPosition(w.Position));

        int qty = pairingWi?.Qty ?? doublingWi?.Qty ?? 0;
        if (qty <= 0)
            qty = Math.Min(outerCutting?.Qty ?? 1, innerCutting?.Qty ?? 1);
        if (qty <= 0) qty = 1;

        // Gating: both cuttings complete.
        if (outerCutting is not null && outerCutting.QtyDone < outerCutting.Qty)
        {
            return FailPair(
                $"Pairing cannot begin — Outer cutting incomplete " +
                $"{outerCutting.QtyDone}/{outerCutting.Qty}");
        }
        if (innerCutting is not null && innerCutting.QtyDone < innerCutting.Qty)
        {
            return FailPair(
                $"Pairing cannot begin — Inner cutting incomplete " +
                $"{innerCutting.QtyDone}/{innerCutting.Qty}");
        }

        // Executions for identity resolution (outer/inner by sequence).
        var executions = (await store.ListExecutionsByJobAsync(tenantId, job.Id, ct))
            .Where(e => e.SpecGroupNumber == spec && e.SerialNumber == serial)
            .OrderBy(e => e.Position, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.PieceId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Sequence)
            .ToList();

        var rows = new List<ProcessPairing>();
        for (int i = 0; i < qty; i++)
        {
            ct.ThrowIfCancellationRequested();

            string pairingId = ProcessRouting.BuildPairingId(jobRef, spec, serial, i);

            var outerExec = executions.FirstOrDefault(e =>
                IsOuterPosition(e.Position) && e.Sequence == i);
            var innerExec = executions.FirstOrDefault(e =>
                IsInnerPosition(e.Position) && e.Sequence == i);

            string outerPieceId = ResolvePaneInstanceId(
                i, outerExec, outerCutting, jobRef, spec, serial, "Outer");
            string innerPieceId = ResolvePaneInstanceId(
                i, innerExec, innerCutting, jobRef, spec, serial, "Inner");

            var outerWi = outerItems.FirstOrDefault();
            var innerWi = innerItems.FirstOrDefault();

            rows.Add(new ProcessPairing
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PairingId = pairingId,
                JobOrderId = job.Id,
                JobRef = jobRef,
                SourceProformaId = job.ProformaId.ToString(),
                SpecGroupNumber = spec,
                SerialNumber = serial,
                Position = "Unit",
                PairId = $"Pair-{i + 1:000}", // human Pair-001
                OuterPieceId = outerPieceId,
                InnerPieceId = innerPieceId,
                OuterGlassRef = outerWi?.GlassRef ?? "Outer",
                InnerGlassRef = innerWi?.GlassRef ?? "Inner",
                OuterProductionPieceId = outerWi?.Id,
                InnerProductionPieceId = innerWi?.Id,
                OuterExecutionId = outerExec?.ExecutionId ?? "",
                InnerExecutionId = innerExec?.ExecutionId ?? "",
                Status = WorkItemStatuses.Completed,
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Notes = $"Paired {outerPieceId} + {innerPieceId} deterministic " +
                        "JobRef Spec Serial Position Component Piece index"
            });
        }

        // Idempotent insert inside one serializable transaction.
        var stored = await store.InsertPairingsAsync(
            tenantId, job.Id, spec, serial, rows, ct);

        await AuditAsync(tenantId, actorId, "production.pairing_recorded",
            "job_orders", job.Id, null, new
            {
                jobOrderNo = jobRef,
                spec,
                serial,
                pairs = stored.Count
            }, ip, ct);

        return ProductionResult<IReadOnlyList<ProcessPairingDto>>.Ok(
            stored.Select(p => ToDto(p)).ToList(),
            $"Paired {stored.Count} pairs deterministically for {jobRef} " +
            $"Spec {spec} Serial {serial}.");
    }

    /// <summary>
    /// POST /assemblies — WPF AssembleAsync: DGU (Outer + Spacer + Inner),
    /// Lamination (Glass + Interlayer + Glass), DGU+LAM; the component
    /// lineage is preserved so a finished unit traces back to BOTH glass
    /// pieces.
    /// </summary>
    public async Task<ProductionResult<IReadOnlyList<ProcessAssemblyDto>>> AssembleAsync(
        Guid tenantId, Guid actorId, AssembleUnitRequest request,
        string? ip, CancellationToken ct)
    {
        var (ok, message, spec, serial, job, unitPieces) =
            await ResolveUnitAsync(tenantId, request.JobOrderId,
                request.SpecGroupNumber, request.SerialNumber, ct);
        if (!ok)
        {
            return ProductionResult<IReadOnlyList<ProcessAssemblyDto>>.Fail(
                ProductionErrorCode.NotFound, message!);
        }

        var jobRef = job!.JobNo;
        var workItems = unitPieces!;
        var assemblyType = (request.AssemblyType ?? "DGU").Trim();
        if (assemblyType.Length == 0) assemblyType = "DGU";

        var doublingWi = workItems.FirstOrDefault(
            w => w.Stage == ProductionStages.DoublingLaminationName);
        if (doublingWi is not null)
        {
            if (doublingWi.IsCancelled || doublingWi.Status == WorkItemStatuses.Cancelled)
            {
                return FailAsm("Doubling work item cancelled.");
            }
            if (doublingWi.Status == WorkItemStatuses.Rejected)
            {
                return FailAsm("Rejected doubling work cannot assemble.");
            }
            if (doublingWi.Status == WorkItemStatuses.QcFailed)
            {
                return FailAsm("QC failed — cannot assemble.");
            }
        }

        // Gating: the Pairing WORK ITEM PCS must have passed (not the
        // supporting pairing rows).
        var pairingWorkItem = workItems.FirstOrDefault(
            w => w.Stage == ProductionStages.PairingName);
        if (pairingWorkItem is not null
            && pairingWorkItem.QtyDone < pairingWorkItem.Qty)
        {
            return FailAsm(
                "Doubling cannot begin when pairing has not passed — Pairing " +
                $"{pairingWorkItem.QtyDone}/{pairingWorkItem.Qty}");
        }

        var pairings = (await store.ListPairingsByJobAsync(tenantId, job.Id, ct))
            .Where(p => p.SpecGroupNumber == spec && p.SerialNumber == serial)
            .OrderBy(p => p.PairId, StringComparer.Ordinal)
            .ToList();

        // Finished-unit count = pairing/doubling work item Qty.
        int qty = pairingWorkItem?.Qty ?? doublingWi?.Qty
            ?? workItems.FirstOrDefault()?.Qty ?? 1;
        if (qty <= 0) qty = 1;

        var rows = new List<ProcessAssembly>();
        for (int i = 0; i < qty; i++)
        {
            ct.ThrowIfCancellationRequested();

            string assemblyId = ProcessRouting.BuildAssemblyId(
                jobRef, spec, serial, i, assemblyType);

            string assemblyLabel =
                assemblyType.Equals("Lamination", StringComparison.OrdinalIgnoreCase)
                || assemblyType.Equals("LAM", StringComparison.OrdinalIgnoreCase)
                    ? $"LAM-{i + 1:000}"
                    : $"DGU-{i + 1:000}";

            // Preserve component lineage — not an anonymous finished product.
            var components = new List<AssemblyComponent>();
            var pairing = pairings.FirstOrDefault(p => p.PairId == $"Pair-{i + 1:000}")
                ?? pairings.ElementAtOrDefault(i);

            if (pairing is not null)
            {
                components.Add(new AssemblyComponent
                {
                    PieceId = pairing.OuterPieceId,
                    GlassRef = pairing.OuterGlassRef,
                    Position = "Outer",
                    Role = "Outer",
                    ExecutionId = pairing.OuterExecutionId
                });
                components.Add(new AssemblyComponent
                {
                    PieceId = pairing.InnerPieceId,
                    GlassRef = pairing.InnerGlassRef,
                    Position = "Inner",
                    Role = "Inner",
                    ExecutionId = pairing.InnerExecutionId
                });
                if (!string.IsNullOrWhiteSpace(request.SpacerInfo))
                {
                    components.Add(new AssemblyComponent
                    {
                        PieceId = $"Spacer-{request.SpacerInfo}",
                        GlassRef = request.SpacerInfo,
                        Position = "Spacer",
                        Role = "Spacer"
                    });
                }
                if (!string.IsNullOrWhiteSpace(request.InterlayerInfo))
                {
                    components.Add(new AssemblyComponent
                    {
                        PieceId = $"Interlayer-{request.InterlayerInfo}",
                        GlassRef = request.InterlayerInfo,
                        Position = "Interlayer",
                        Role = "Interlayer"
                    });
                }
            }
            else
            {
                // No pairing record — build from the work items directly,
                // still preserving lineage.
                var outerWi = workItems.FirstOrDefault(w =>
                    string.Equals(w.Position, "Outer", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(w.Position, "Glass", StringComparison.OrdinalIgnoreCase));
                var innerWi = workItems.FirstOrDefault(w =>
                    string.Equals(w.Position, "Inner", StringComparison.OrdinalIgnoreCase));

                if (outerWi is not null)
                {
                    components.Add(new AssemblyComponent
                    {
                        PieceId = ProductionPieceIdentity.FromWorkItem(
                            ProcessExecutionService.ToRouteItem(outerWi, jobRef), i + 1),
                        GlassRef = outerWi.GlassRef,
                        Position = "Outer",
                        Role = "Outer"
                    });
                }
                if (innerWi is not null)
                {
                    components.Add(new AssemblyComponent
                    {
                        PieceId = ProductionPieceIdentity.FromWorkItem(
                            ProcessExecutionService.ToRouteItem(innerWi, jobRef), i + 1),
                        GlassRef = innerWi.GlassRef,
                        Position = "Inner",
                        Role = "Inner"
                    });
                }
            }

            rows.Add(new ProcessAssembly
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AssemblyId = assemblyId,
                JobOrderId = job.Id,
                JobRef = jobRef,
                SourceProformaId = job.ProformaId.ToString(),
                SpecGroupNumber = spec,
                SerialNumber = serial,
                Position = "Unit",
                AssemblyLabel = assemblyLabel,
                AssemblyType = assemblyType,
                ComponentsJson = JsonSerializer.Serialize(components),
                SpacerInfo = request.SpacerInfo ?? "",
                InterlayerInfo = request.InterlayerInfo ?? "",
                SealantInfo = request.SealantInfo ?? "",
                PairingId = pairing?.PairingId ?? "",
                Status = WorkItemStatuses.Completed,
                QtyRequired = 1,
                QtyDone = 1,
                CompletedAt = DateTime.UtcNow,
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Notes = $"Assembly {assemblyLabel} {assemblyType} preserves lineage " +
                        "Outer+Inner+Spacer/Interlayer — traceable to both glass pieces"
            });
        }

        var stored = await store.InsertAssembliesAsync(
            tenantId, job.Id, spec, serial, rows, ct);

        await AuditAsync(tenantId, actorId, "production.assembly_recorded",
            "job_orders", job.Id, null, new
            {
                jobOrderNo = jobRef,
                spec,
                serial,
                assemblyType,
                assemblies = stored.Count
            }, ip, ct);

        return ProductionResult<IReadOnlyList<ProcessAssemblyDto>>.Ok(
            stored.Select(ToDto).ToList(),
            $"Assembled {stored.Count} {assemblyType} assemblies — component " +
            "lineage preserved.");
    }

    // ── reads ────────────────────────────────────────────────────────

    public async Task<ProductionResult<IReadOnlyList<ProcessPairingDto>>> ListPairingsAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var job = await store.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<IReadOnlyList<ProcessPairingDto>>.Fail(
                ProductionErrorCode.NotFound, $"Job order {jobOrderId} was not found.");
        }
        var rows = await store.ListPairingsByJobAsync(tenantId, jobOrderId, ct);
        return ProductionResult<IReadOnlyList<ProcessPairingDto>>.Ok(
            rows.Select(ToDto).ToList());
    }

    public async Task<ProductionResult<IReadOnlyList<ProcessAssemblyDto>>> ListAssembliesAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var job = await store.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<IReadOnlyList<ProcessAssemblyDto>>.Fail(
                ProductionErrorCode.NotFound, $"Job order {jobOrderId} was not found.");
        }
        var rows = await store.ListAssembliesByJobAsync(tenantId, jobOrderId, ct);
        return ProductionResult<IReadOnlyList<ProcessAssemblyDto>>.Ok(
            rows.Select(ToDto).ToList());
    }

    // ── helpers ──────────────────────────────────────────────────────

    private async Task<(bool Ok, string? Message, int Spec, int Serial, JobOrder? Job,
        IReadOnlyList<ProductionPiece>? UnitPieces)> ResolveUnitAsync(
        Guid tenantId, Guid jobOrderId, int spec, int serial, CancellationToken ct)
    {
        var job = await store.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return (false, $"Job order {jobOrderId} was not found.", 0, 0, null, null);
        }
        if (spec <= 0 || serial <= 0)
        {
            return (false, "SpecGroupNumber and SerialNumber are required.",
                0, 0, job, null);
        }

        var unitPieces = (await store.ListPiecesByJobAsync(tenantId, jobOrderId, ct))
            .Where(w => w.SpecGroupNumber == spec && w.SerialNumber == serial
                && !w.IsCancelled)
            .ToList();
        return (true, null, spec, serial, job, unitPieces);
    }

    public static bool IsOuterPosition(string? position) =>
        !string.IsNullOrWhiteSpace(position)
        && (position.Equals("Outer", StringComparison.OrdinalIgnoreCase)
            || position.Equals("Lam Outer", StringComparison.OrdinalIgnoreCase)
            || position.Equals("Glass", StringComparison.OrdinalIgnoreCase));

    public static bool IsInnerPosition(string? position) =>
        !string.IsNullOrWhiteSpace(position)
        && (position.Equals("Inner", StringComparison.OrdinalIgnoreCase)
            || position.Equals("Lam Inner", StringComparison.OrdinalIgnoreCase));

    /// <summary>Pane identity resolution (WPF chain minus the placed-piece
    /// table the SaaS does not have): execution PieceId → the cutting work
    /// item's deterministic identity.</summary>
    private static string ResolvePaneInstanceId(
        int index0,
        ProcessExecution? exec,
        ProductionPiece? cuttingWi,
        string jobRef,
        int spec,
        int serial,
        string defaultPosition)
    {
        if (exec is not null && ProductionPieceIdentity.IsPaneInstanceId(exec.OutputPieceId))
            return exec.OutputPieceId;
        if (exec is not null && ProductionPieceIdentity.IsPaneInstanceId(exec.PieceId))
            return exec.PieceId;

        if (cuttingWi is not null)
        {
            return ProductionPieceIdentity.FromWorkItem(
                ProcessExecutionService.ToRouteItem(cuttingWi, jobRef), index0 + 1);
        }

        return ProductionPieceIdentity.BuildPaneInstanceId(
            jobRef, spec, serial, defaultPosition, 0, 0, index0 + 1);
    }

    private static ProcessPairingDto ToDto(ProcessPairing p) => new(
        p.Id,
        p.PairingId,
        p.JobRef,
        p.JobOrderId,
        p.SpecGroupNumber,
        p.SerialNumber,
        p.Position,
        p.PairId,
        p.OuterPieceId,
        p.InnerPieceId,
        p.OuterGlassRef,
        p.InnerGlassRef,
        p.OuterProductionPieceId,
        p.InnerProductionPieceId,
        p.OuterExecutionId,
        p.InnerExecutionId,
        p.Status,
        p.Notes,
        p.CreatedAt);

    internal static ProcessAssemblyDto ToDto(ProcessAssembly a)
    {
        IReadOnlyList<AssemblyComponentDto> components = Array.Empty<AssemblyComponentDto>();
        try
        {
            components = (JsonSerializer.Deserialize<List<AssemblyComponent>>(
                    a.ComponentsJson ?? "[]") ?? new List<AssemblyComponent>())
                .Select(c => new AssemblyComponentDto(
                    c.PieceId, c.GlassRef, c.Position, c.Role))
                .ToList();
        }
        catch (JsonException)
        {
            // Unparseable lineage JSON falls back to an empty component
            // list — the row itself stays readable (WPF GetTraceability
            // catch).
        }

        return new ProcessAssemblyDto(
            a.Id,
            a.AssemblyId,
            a.JobRef,
            a.JobOrderId,
            a.SpecGroupNumber,
            a.SerialNumber,
            a.Position,
            a.AssemblyLabel,
            a.AssemblyType,
            components,
            a.SpacerInfo,
            a.InterlayerInfo,
            a.SealantInfo,
            a.PairingId,
            a.Status,
            a.QtyRequired,
            a.QtyDone,
            GetTraceability(a, components),
            a.Notes,
            a.CompletedAt,
            a.CreatedAt);
    }

    /// <summary>WPF GetTraceability — the one-line lineage proof.</summary>
    internal static string GetTraceability(
        ProcessAssembly a, IReadOnlyList<AssemblyComponentDto> components)
    {
        string comps = string.Join(" + ",
            components.Select(c => $"{c.Role} {c.PieceId} {c.GlassRef}"));
        return $"Assembly {a.AssemblyLabel} {a.AssemblyType} — Components: {comps} " +
               $"— Spacer {a.SpacerInfo} Interlayer {a.InterlayerInfo} — " +
               $"Job {a.JobRef} Spec {a.SpecGroupNumber} Serial {a.SerialNumber}";
    }

    private static ProductionResult<IReadOnlyList<ProcessPairingDto>> FailPair(string message) =>
        ProductionResult<IReadOnlyList<ProcessPairingDto>>.Fail(
            ProductionErrorCode.Conflict, message);

    private static ProductionResult<IReadOnlyList<ProcessAssemblyDto>> FailAsm(string message) =>
        ProductionResult<IReadOnlyList<ProcessAssemblyDto>>.Fail(
            ProductionErrorCode.Conflict, message);

    private async Task AuditAsync(
        Guid tenantId, Guid? actorUserId, string action, string entityType,
        Guid entityId, object? before, object? after, string? ip, CancellationToken ct)
        => await audit.WriteAsync(tenantId, actorUserId, action, entityType,
            entityId, ip, null,
            before is null ? null : JsonSerializer.Serialize(before),
            after is null ? null : JsonSerializer.Serialize(after),
            ct).ConfigureAwait(false);
}
