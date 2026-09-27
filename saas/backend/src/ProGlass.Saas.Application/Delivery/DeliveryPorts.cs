using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Application.Delivery;

/// <summary>
/// Storage port for the Delivery module (Phase 10). The master-DN
/// recompute (remaining qty from CONFIRMED deliveries only) is a store
/// operation so it can run set-based inside one transaction.
/// </summary>
public interface IDeliveryStore
{
    Task<JobOrder?> FindJobAsync(Guid tenantId, Guid jobOrderId, CancellationToken ct);

    /// <summary>The source PI's Step 5 grid (the master DN's line
    /// source).</summary>
    Task<IReadOnlyList<Proforma.SpecGroupWithItems>> GetProformaGridAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct);

    /// <summary>Note + lines (JobOrder nav included for the DTO).</summary>
    Task<DeliveryNoteAggregate?> FindAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>The job's master DN (balance tracker) with lines.</summary>
    Task<DeliveryNoteAggregate?> FindMasterAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct);

    /// <summary>The tenant's notes for one job order (newest seq first;
    /// master included, voided included — callers filter).</summary>
    Task<IReadOnlyList<DeliveryNote>> ListForJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct);

    Task<MasterData.PagedResult<DeliveryNote>> SearchAsync(
        Guid tenantId, string? search, string? status, bool includeVoid,
        bool mastersOnly, int page, int pageSize, CancellationToken ct);

    /// <summary>Max non-master sequence for the job + 1 (starts at 1).</summary>
    Task<int> GetNextSequenceAsync(Guid tenantId, Guid jobOrderId, CancellationToken ct);

    /// <summary>Insert note + items in one transaction; false on the
    /// (tenant, delivery_no) UNIQUE violation.</summary>
    Task<bool> TryInsertAsync(
        DeliveryNote note, IReadOnlyList<DeliveryNoteItem> items, CancellationToken ct);

    /// <summary>Tracked save of the aggregate (header fields + line
    /// edits), RowVersion guard, false on conflict.</summary>
    Task<bool> TryUpdateAsync(
        DeliveryNoteAggregate aggregate, int? expectedRowVersion, CancellationToken ct);

    /// <summary>
    /// WPF UpdateMasterDnAsync: refresh the master's per-line
    /// previously-delivered + balance and its totals from the CONFIRMED
    /// non-master, non-void notes of the job (in-progress drafts never
    /// consume remaining qty). No-op when the master does not exist.
    /// </summary>
    Task RecomputeMasterAsync(Guid tenantId, Guid jobOrderId, CancellationToken ct);
}
