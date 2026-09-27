// ═══════════════════════════════════════════════════════════════════════
//  FILE: ProGlass.Saas.Application/Reporting/ProductionReportsService.cs
//  PORTED from the WPF application (read 2026-09-27):
//    • Services/Reporting/ProductionProcessReportService.cs — the
//      process-status rows (one per execution, ordered Spec/ProcessType,
//      with the full PI → JO → … → QC traceability line), the
//      pairing/assembly traceability rows (pairs + their assembly, plus
//      assemblies without a pair), the route report (one row per unit
//      with the route label + current stage from the latest execution).
//    • Services/Reporting/ProductionControlReportService.cs — the
//      rework queue and QC queue filters.
//  The WPF's delegating control reports (production queue, blocked work,
//  status KPIs, progress, workstation workload, queue pressure) are the
//  Phase 26a endpoints verbatim — no second endpoint duplicates them
//  (documented, mapping §1.7d).
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Application.Production;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Reporting;

public sealed record ProcessStatusRowDto(
    string JobRef,
    string PiRef,
    string SpecName,
    string PieceId,
    string ProcessType,
    string PreviousStage,
    string NextStage,
    string Status,
    int QtyRequired,
    int QtyDone,
    int Remaining,
    string QcStatus,
    string OperatorName,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string Traceability);

public sealed record PairingAssemblyTraceabilityRowDto(
    string JobRef,
    string SpecName,
    string PairId,
    string OuterPieceId,
    string InnerPieceId,
    string AssemblyLabel,
    string AssemblyType,
    string Components,
    string Traceability);

public sealed record RouteReportRowDto(
    string JobRef,
    string SpecName,
    string BuildType,
    string PieceId,
    string Route,
    string CurrentStage,
    string Status);

public sealed class ProductionReportsService(
    Production.IProcessControlStore store,
    Production.IProductionStore production,
    Production.PairingAssemblyService pairingAssembly,
    Production.ProductionQueueService queue)
{
    // ── Process status (WPF GenerateProcessStatusAsync) ──────────────

    /// <summary>
    /// GET /production/reports/process-status?jobOrderId= — one row per
    /// execution, ordered SpecName → ProcessType, with the traceability
    /// line PI → JO → Spec → Position → Piece → Input → Process →
    /// Output → WorkItem → QC.
    /// </summary>
    public async Task<ProductionResult<IReadOnlyList<ProcessStatusRowDto>>>
        GenerateProcessStatusAsync(Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var job = await production.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<IReadOnlyList<ProcessStatusRowDto>>.Fail(
                ProductionErrorCode.NotFound,
                $"Job order {jobOrderId} was not found.");
        }

        var execs = await store.ListExecutionsByJobAsync(tenantId, job.Id, ct);
        var rows = execs
            .Select(e => new ProcessStatusRowDto(
                e.JobRef,
                e.SourceProformaId,
                e.SpecName,
                e.PieceId,
                ProductionStages.Name(e.ProcessType),
                e.PreviousStage,
                e.NextStage,
                e.Status,
                e.QtyRequired,
                e.QtyDone,
                Math.Max(0, e.QtyRequired - e.QtyDone),
                e.QcStatus,
                $"{e.StartedBy}→{e.CompletedBy}",
                e.StartedAt,
                e.CompletedAt,
                $"PI {e.SourceProformaId} → JO {e.JobRef} → " +
                $"Spec {e.SpecGroupNumber}/{e.SerialNumber} {e.SpecName} → " +
                $"Position {e.Position} → Piece {e.PieceId} → " +
                $"Input {e.InputPieceId} → " +
                $"Process {ProductionStages.Name(e.ProcessType)} {e.ExecutionId} → " +
                $"Output {e.OutputPieceId} → WorkItem {e.ProductionPieceId} → " +
                $"QC {e.QcId}"))
            .OrderBy(r => r.SpecName, StringComparer.Ordinal)
            .ThenBy(r => r.ProcessType, StringComparer.Ordinal)
            .ToList();

        return ProductionResult<IReadOnlyList<ProcessStatusRowDto>>.Ok(rows);
    }

    // ── Pairing/assembly traceability (WPF
    //    GeneratePairingAssemblyTraceabilityAsync) ────────────────────

    /// <summary>
    /// GET /production/reports/pairing-assembly?jobOrderId= — one row per
    /// pairing (joined to its assembly when it exists) plus the
    /// pair-less assemblies (SGU lamination etc.).
    /// </summary>
    public async Task<ProductionResult<IReadOnlyList<PairingAssemblyTraceabilityRowDto>>>
        GeneratePairingAssemblyTraceabilityAsync(
            Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var job = await production.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<IReadOnlyList<PairingAssemblyTraceabilityRowDto>>.Fail(
                ProductionErrorCode.NotFound,
                $"Job order {jobOrderId} was not found.");
        }

        var pairingsResult = await pairingAssembly.ListPairingsAsync(
            tenantId, job.Id, ct);
        var assembliesResult = await pairingAssembly.ListAssembliesAsync(
            tenantId, job.Id, ct);
        var pairings = pairingsResult.Value ?? Array.Empty<ProcessPairingDto>();
        var assemblies = assembliesResult.Value ?? Array.Empty<ProcessAssemblyDto>();

        var rows = new List<PairingAssemblyTraceabilityRowDto>();

        foreach (var p in pairings)
        {
            var assembly = assemblies.FirstOrDefault(
                a => a.PairingId == p.PairingId);
            string comps = $"{p.OuterPieceId} + {p.InnerPieceId}";
            string trace =
                $"PI {job.ProformaId} → JO {p.JobRef} → " +
                $"Spec {p.SpecGroupNumber}/{p.SerialNumber} → " +
                $"Pair {p.PairId} Outer {p.OuterPieceId} {p.OuterGlassRef} " +
                $"Inner {p.InnerPieceId} {p.InnerGlassRef}";

            if (assembly is not null)
            {
                trace += $" → Assembly {assembly.AssemblyLabel} " +
                         $"{assembly.AssemblyType} {assembly.SpacerInfo} " +
                         $"{assembly.InterlayerInfo}";
                comps = string.Join(" + ",
                    assembly.Components.Select(c => $"{c.Role} {c.PieceId} {c.GlassRef}"));
            }

            rows.Add(new PairingAssemblyTraceabilityRowDto(
                p.JobRef,
                $"{p.SpecGroupNumber}/{p.SerialNumber}",
                p.PairId,
                p.OuterPieceId,
                p.InnerPieceId,
                assembly?.AssemblyLabel ?? "",
                assembly?.AssemblyType ?? "",
                comps,
                trace));
        }

        // Assemblies without pairings (SGU lamination etc.).
        foreach (var a in assemblies.Where(a => string.IsNullOrWhiteSpace(a.PairingId)))
        {
            rows.Add(new PairingAssemblyTraceabilityRowDto(
                a.JobRef,
                $"{a.SpecGroupNumber}/{a.SerialNumber}",
                "",
                "",
                "",
                a.AssemblyLabel,
                a.AssemblyType,
                string.Join(" + ",
                    a.Components.Select(c => $"{c.Role} {c.PieceId} {c.GlassRef}")),
                a.Traceability));
        }

        return ProductionResult<IReadOnlyList<PairingAssemblyTraceabilityRowDto>>.Ok(rows);
    }

    // ── Route report (WPF GenerateRouteReportAsync) ──────────────────

    /// <summary>
    /// GET /production/reports/route?jobOrderId= — one row per unit
    /// (spec/serial/position): the route label from the routing engine
    /// plus the current stage from the unit's latest execution (Cutting /
    /// Pending when none).
    /// </summary>
    public async Task<ProductionResult<IReadOnlyList<RouteReportRowDto>>>
        GenerateRouteReportAsync(Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var job = await production.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<IReadOnlyList<RouteReportRowDto>>.Fail(
                ProductionErrorCode.NotFound,
                $"Job order {jobOrderId} was not found.");
        }

        var pieces = await store.ListPiecesByJobAsync(tenantId, job.Id, ct);
        var execs = await store.ListExecutionsByJobAsync(tenantId, job.Id, ct);

        var rows = new List<RouteReportRowDto>();
        foreach (var g in pieces
                     .Where(p => !p.IsCancelled)
                     .GroupBy(p => new { p.SpecGroupNumber, p.SerialNumber, p.Position }))
        {
            var unitItems = g.ToList();
            var first = unitItems[0];
            var route = ProcessRouting.BuildRouteForWorkItem(
                ProcessExecutionService.ToRouteItem(first, job.JobNo),
                unitItems.Select(p => ProcessExecutionService.ToRouteItem(p, job.JobNo))
                    .ToList());

            var latest = execs
                .Where(e => e.SpecGroupNumber == first.SpecGroupNumber
                            && e.SerialNumber == first.SerialNumber)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefault();
            string current = latest is null
                ? ProductionStages.CuttingName
                : ProductionStages.Name(latest.ProcessType);
            string status = latest?.Status ?? WorkItemStatuses.Pending;

            rows.Add(new RouteReportRowDto(
                job.JobNo,
                first.SpecName,
                first.BuildType,
                route.PieceId,
                route.RouteLabel,
                current,
                status));
        }

        return ProductionResult<IReadOnlyList<RouteReportRowDto>>.Ok(rows);
    }

    // ── Control report filters (WPF
    //    ProductionControlReportService.Generate{Rework,Qc}Queue) ─────

    /// <summary>
    /// GET /production/reports/rework-queue — live queue rows whose
    /// readiness is Rework or flagged IsRework (WPF
    /// GenerateReworkQueueReportAsync).
    /// </summary>
    public async Task<ProductionResult<IReadOnlyList<QueueItemDto>>> GenerateReworkQueueReportAsync(
        Guid tenantId, CancellationToken ct)
    {
        var result = await queue.GetQueueAsync(
            tenantId, includeCompleted: false, includeCancelled: false,
            take: 1000, ct: ct);
        if (!result.Success)
        {
            return ProductionResult<IReadOnlyList<QueueItemDto>>.Fail(
                result.ErrorCode, result.Message);
        }
        var rows = (result.Value?.Items ?? Array.Empty<QueueItemDto>())
            .Where(q => q.Readiness == nameof(ProductionReadiness.Rework) || q.IsRework)
            .Take(500)
            .ToList();
        return ProductionResult<IReadOnlyList<QueueItemDto>>.Ok(rows);
    }

    /// <summary>
    /// GET /production/reports/qc-queue — queue rows awaiting or failing
    /// QC (WPF GenerateQcQueueReportAsync; includeCompleted because a
    /// finished piece can still sit in QC).
    /// </summary>
    public async Task<ProductionResult<IReadOnlyList<QueueItemDto>>> GenerateQcQueueReportAsync(
        Guid tenantId, CancellationToken ct)
    {
        var result = await queue.GetQueueAsync(
            tenantId, includeCompleted: true, includeCancelled: false,
            take: 1000, ct: ct);
        if (!result.Success)
        {
            return ProductionResult<IReadOnlyList<QueueItemDto>>.Fail(
                result.ErrorCode, result.Message);
        }
        var rows = (result.Value?.Items ?? Array.Empty<QueueItemDto>())
            .Where(q => q.Readiness == nameof(ProductionReadiness.QcPending)
                     || q.Readiness == nameof(ProductionReadiness.QcFailed))
            .Take(500)
            .ToList();
        return ProductionResult<IReadOnlyList<QueueItemDto>>.Ok(rows);
    }
}
