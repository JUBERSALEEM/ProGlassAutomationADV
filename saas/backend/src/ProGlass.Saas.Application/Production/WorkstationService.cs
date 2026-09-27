using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Production;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 26a service — the WPF WorkstationService (ERP-level only: no
//  machine integration, no PLC/CNC APIs):
//    • EnsureSeededAsync — the ten default stations, only when the
//      tenant has none (CUT-01/02, CNC-01, POLISH-01, EDGEDEL-01,
//      MITRING-01, TEMPER-01, PAIR-01, LAM-01, DGU-01)
//    • GetAll / GetByProcess (active only) / GetById
//    • CreateAsync — idempotent on the upper-invariant code
//  Per-tenant master data (the WPF shares one production.db; the SaaS
//  seeds every tenant's own station list).
// ═══════════════════════════════════════════════════════════════════════

public sealed class WorkstationService(IProcessControlStore store)
{
    /// <summary>GET /workstations — ordered by process type then code.</summary>
    public async Task<ProductionResult<IReadOnlyList<WorkstationDto>>> ListAsync(
        Guid tenantId, bool activeOnly, CancellationToken ct)
    {
        var rows = await store.ListWorkstationsAsync(tenantId, activeOnly, ct);
        return ProductionResult<IReadOnlyList<WorkstationDto>>.Ok(
            rows.Select(ToDto).ToList());
    }

    /// <summary>GET /workstations/{id} — by station id (the code).</summary>
    public async Task<ProductionResult<WorkstationDto>> GetAsync(
        Guid tenantId, string workstationId, CancellationToken ct)
    {
        var row = await store.FindWorkstationAsync(tenantId, workstationId, ct);
        if (row is null)
        {
            return ProductionResult<WorkstationDto>.Fail(
                ProductionErrorCode.NotFound,
                $"Workstation '{workstationId}' was not found.");
        }
        return ProductionResult<WorkstationDto>.Ok(ToDto(row));
    }

    /// <summary>POST /workstations — idempotent on the code; ProcessType
    /// 1–8 names the stage.</summary>
    public async Task<ProductionResult<WorkstationDto>> CreateAsync(
        Guid tenantId, Guid? actorId, CreateWorkstationRequest request,
        CancellationToken ct)
    {
        var code = (request.Code ?? "").Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            return ProductionResult<WorkstationDto>.Fail(
                ProductionErrorCode.ValidationFailed, "Workstation code required.");
        }
        if (request.ProcessType is < 1 or > 8)
        {
            return ProductionResult<WorkstationDto>.Fail(
                ProductionErrorCode.ValidationFailed,
                "ProcessType must be 1–8 (Cutting…DoublingLamination).");
        }

        var row = await store.CreateWorkstationAsync(
            tenantId, code,
            string.IsNullOrWhiteSpace(request.Name) ? code : request.Name!.Trim(),
            request.ProcessType, request.Description ?? "", actorId, ct);
        return ProductionResult<WorkstationDto>.Ok(ToDto(row));
    }

    /// <summary>POST /workstations/seed — the ten default stations, only
    /// when the tenant has none (idempotent).</summary>
    public async Task<ProductionResult<WorkstationSeedResultDto>> EnsureSeededAsync(
        Guid tenantId, CancellationToken ct)
    {
        int seeded = await store.EnsureWorkstationsSeededAsync(tenantId, ct);
        int total = (await store.ListWorkstationsAsync(tenantId, false, ct)).Count;
        return ProductionResult<WorkstationSeedResultDto>.Ok(
            new WorkstationSeedResultDto(seeded, total),
            seeded > 0 ? $"Seeded {seeded} default workstations." : "Already seeded.");
    }

    private static WorkstationDto ToDto(Workstation w) => new(
        w.Id, w.WorkstationId, w.Code, w.Name, w.ProcessType,
        ProductionStages.Name(w.ProcessType), w.IsActive, w.Description);
}
