using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Production;

/// <summary>
/// Storage port for the Production module (Phase 9b). Plan generation is
/// pure (the engine); the store owns release persistence (unique-key
/// preservation) and the immutable event log.
/// </summary>
public interface IProductionStore
{
    Task<JobOrder?> FindJobAsync(Guid tenantId, Guid jobOrderId, CancellationToken ct);

    /// <summary>The source PI's Step 5 grid, as engine inputs.</summary>
    Task<IReadOnlyList<ProductionSpecInput>> GetSpecInputsAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct);

    /// <summary>The PI's ACTIVE Step 6 predefined charges, as engine
    /// inputs (template key + metric qty + polish labels + spec
    /// targets).</summary>
    Task<IReadOnlyList<ProductionChargeInput>> GetChargeInputsAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct);

    Task<ProductionPiece?> FindAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<IReadOnlyList<ProductionPiece>> ListByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct);

    Task<MasterData.PagedResult<ProductionPiece>> SearchAsync(
        Guid tenantId, Guid? jobOrderId, string? stage, string? status,
        bool includeCancelled, int page, int pageSize, CancellationToken ct);

    /// <summary>Existing unique keys for the job (re-release
    /// preservation contract).</summary>
    Task<HashSet<string>> GetExistingKeysAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct);

    /// <summary>Insert the missing items in one transaction (rows whose
    /// unique_key already exists are SKIPPED — statuses survive).</summary>
    Task<int> InsertNewAsync(
        Guid tenantId, Guid jobOrderId, Guid actorId,
        IReadOnlyList<ProductionWorkItemPlan> items, CancellationToken ct);

    Task<bool> TryUpdateAsync(
        ProductionPiece piece, CancellationToken ct);

    /// <summary>Append one immutable event row; returns its id.</summary>
    Task AppendEventAsync(
        Guid tenantId, ProductionEvent entry, CancellationToken ct);

    Task<IReadOnlyList<ProductionEvent>> ListEventsAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct);

    /// <summary>QC history for one piece (QcSequence is derived from the
    /// event log — the WPF keeps a dedicated table; the SaaS stores the
    /// QC rows as typed events, sequence included in the payload).</summary>
    Task<IReadOnlyList<ProductionEvent>> ListQcEventsAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct);
}
