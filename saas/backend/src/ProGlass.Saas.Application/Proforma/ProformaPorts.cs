using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Application.Proforma;

/// <summary>
/// Persistence port for the proforma document core (schema 0003
/// proforma_invoices / spec_groups / spec_group_items). MECHANICAL ONLY —
/// the WPF policies (numbering retries, lifecycle gating, phantom-row
/// filtering) live in ProformaService so they are unit-tested in one
/// place. Like the other stores, every method takes the tenant id
/// EXPLICITLY; implementations own tenant-scoped access (query filter +
/// RLS both fail closed).
/// </summary>
public interface IProformaStore
{
    // ── numbering (BUG-08: one batch query per allocation attempt) ──

    /// <summary>All stored numbers starting with the prefix (the id set
    /// ProformaNumberSequence allocates against).</summary>
    Task<IReadOnlyList<string>> GetNumbersWithPrefixAsync(
        Guid tenantId, string prefix, CancellationToken ct);

    Task<bool> ExistsByNumberAsync(
        Guid tenantId, string proformaNo, CancellationToken ct);

    // ── document rows ─────────────────────────────────────────────

    Task<ProformaInvoice?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>
    /// Insert; FALSE (no throw) when the number collides — the WPF caught
    /// the unique-violation DbUpdateException and retried with the next
    /// free id, capped at 5 attempts; the service owns that loop.
    /// </summary>
    Task<bool> TryInsertAsync(ProformaInvoice invoice, CancellationToken ct);

    /// <summary>Detached full-row update (tracked reload + copy + stamp),
    /// one SaveChanges.</summary>
    Task UpdateAsync(ProformaInvoice invoice, Guid actorId, CancellationToken ct);

    /// <summary>
    /// The revision chain: the root row plus every "-R{n}" revision, in
    /// WPF order (RevisionNumber, then Id).
    /// </summary>
    Task<IReadOnlyList<ProformaInvoice>> GetRevisionChainAsync(
        Guid tenantId, Guid rootId, CancellationToken ct);

    Task<(IReadOnlyList<ProformaInvoice> Items, int TotalCount)> ListAsync(
        Guid tenantId, int page, int pageSize, string? status, string? search,
        CancellationToken ct);

    // ── Step 5 grid ───────────────────────────────────────────────

    /// <summary>
    /// Replace the invoice's whole Step 5 grid (delete + insert) AND apply
    /// the invoice-field changes from the save (glass subtotal, row
    /// version, audit stamps) in ONE transaction — the wizard's save was
    /// one atomic unit.
    /// </summary>
    Task ReplaceSpecGroupsAsync(
        ProformaInvoice invoice, IReadOnlyList<SpecGroupWithItems> groups,
        Guid actorId, CancellationToken ct);

    Task<IReadOnlyList<SpecGroupWithItems>> GetSpecGroupsAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct);
}
