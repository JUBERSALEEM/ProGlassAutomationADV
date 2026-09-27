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

    Task ReplaceStep6Async(
        Domain.Entities.ProformaInvoice invoice,
        IReadOnlyList<Domain.Entities.OtherCharge> charges,
        IReadOnlyList<Domain.Entities.WastageRow> wastages,
        IReadOnlyList<Domain.Entities.CustomCharge> customs,
        Guid actorId, CancellationToken ct);

    Task<Step6Rows> GetStep6Async(Guid tenantId, Guid proformaId, CancellationToken ct);

    Task<IReadOnlyList<SpecGroupWithItems>> GetSpecGroupsAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct);

    // ── Duplicate (WPF SavedInvoicesListViewModel.DuplicateAsync) ────

    /// <summary>
    /// Insert the duplicate invoice + its copied sections in ONE
    /// transaction. FALSE (no throw) when the number collided — the
    /// service retries with the next free id, capped at 5 attempts.
    /// </summary>
    Task<bool> TryDuplicateAsync(
        ProformaInvoice invoice,
        IReadOnlyList<SpecGroupWithItems> groups,
        Step6Rows step6,
        IReadOnlyList<Domain.Entities.PnlRow> pnlRows,
        Guid actorId, CancellationToken ct);

    // ── Inquiry link (WPF InquiryLinkSync) ───────────────────────────

    Task<Inquiry?> FindInquiryAsync(Guid tenantId, Guid inquiryId, CancellationToken ct);

    /// <summary>Any PI already linked to this inquiry (the Convert-to-PI
    /// duplicate-prevention guard reads the source of truth, not the
    /// denormalized link columns the WPF had to keep in sync).</summary>
    Task<bool> HasProformaForInquiryAsync(
        Guid tenantId, Guid inquiryId, CancellationToken ct);

    /// <summary>Write the converted-proforma link (converted_proforma_id
    /// + audit stamps). FALSE when the inquiry row is gone.</summary>
    Task<bool> TryLinkInquiryToProformaAsync(
        Guid tenantId, Guid inquiryId, Guid proformaId, Guid actorId, CancellationToken ct);

    /// <summary>Status-only write for the link sync (WPF
    /// ChangeStatusAsync). FALSE when the inquiry row is gone.</summary>
    Task<bool> TryAdvanceInquiryStatusAsync(
        Guid tenantId, Guid inquiryId, string status, Guid actorId, CancellationToken ct);

    // ── Step 9 P&L ────────────────────────────────────────────────

    /// <summary>
    /// Replace the invoice's whole P&amp;L row set (delete + insert) and
    /// apply the invoice-field changes (operator inputs, KPI snapshot,
    /// pnl_calculated_at, row version) in ONE transaction.
    /// </summary>
    Task ReplaceStep9Async(
        ProformaInvoice invoice,
        IReadOnlyList<Domain.Entities.PnlRow> rows,
        Guid actorId, CancellationToken ct);

    Task<IReadOnlyList<Domain.Entities.PnlRow>> GetStep9Async(
        Guid tenantId, Guid proformaId, CancellationToken ct);

    /// <summary>
    /// The tenant's Sheet Store glass rows (WPF
    /// SheetStorePricingService.ReadAll — colour/thickness/size/prices).
    /// Read-only; Step 9 prices panes from it.
    /// </summary>
    Task<IReadOnlyList<Domain.Proforma.SheetStockEntry>> GetSheetStockAsync(
        Guid tenantId, CancellationToken ct);
}

/// <summary>The store's unit for reading the Step 6 section back.</summary>
public sealed record Step6Rows(
    IReadOnlyList<Domain.Entities.OtherCharge> Charges,
    IReadOnlyList<Domain.Entities.WastageRow> Wastages,
    IReadOnlyList<Domain.Entities.CustomCharge> Customs);
