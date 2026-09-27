using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Application.Tests.Proforma;

/// <summary>
/// In-memory IProformaStore mirroring the schema contracts the EF store
/// relies on: UNIQUE(tenant_id, proforma_no) exact-match, revision chains
/// in WPF order, and whole-grid replacement per invoice.
/// </summary>
public sealed class FakeProformaStore : IProformaStore
{
    private readonly List<ProformaInvoice> _invoices = new();
    private readonly Dictionary<Guid, List<SpecGroupWithItems>> _grids = new();

    public IReadOnlyList<ProformaInvoice> Invoices => _invoices;

    /// <summary>When set, TryInsertAsync refuses every insert (simulates a
    /// permanent concurrent-writer race).</summary>
    public bool AlwaysRefuseInserts { get; set; }

    /// <summary>When set, the NEXT TryInsertAsync fails once (simulates a
    /// single writer stealing the number between check and insert), then
    /// behaves normally — the WPF unique-violation retry path.</summary>
    public bool RefuseNextInsert { get; set; }

    public int UpdateCalls { get; private set; }

    public Task<IReadOnlyList<string>> GetNumbersWithPrefixAsync(
        Guid tenantId, string prefix, CancellationToken ct)
    {
        IReadOnlyList<string> ids = _invoices
            .Where(i => i.TenantId == tenantId
                        && i.ProformaNo.StartsWith(prefix, StringComparison.Ordinal))
            .Select(i => i.ProformaNo)
            .ToList();
        return Task.FromResult(ids);
    }

    public Task<bool> ExistsByNumberAsync(
        Guid tenantId, string proformaNo, CancellationToken ct) =>
        Task.FromResult(_invoices.Any(i =>
            i.TenantId == tenantId && i.ProformaNo == proformaNo));

    public Task<ProformaInvoice?> FindByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct) =>
        Task.FromResult(_invoices.FirstOrDefault(i =>
            i.TenantId == tenantId && i.Id == id));

    public Task<bool> TryInsertAsync(ProformaInvoice invoice, CancellationToken ct)
    {
        if (AlwaysRefuseInserts)
        {
            return Task.FromResult(false);
        }

        if (RefuseNextInsert)
        {
            RefuseNextInsert = false;
            return Task.FromResult(false);
        }

        if (_invoices.Any(i => i.TenantId == invoice.TenantId
                                && i.ProformaNo == invoice.ProformaNo))
        {
            return Task.FromResult(false); // UNIQUE(tenant_id, proforma_no)
        }

        _invoices.Add(invoice);
        return Task.FromResult(true);
    }

    public Task UpdateAsync(ProformaInvoice invoice, Guid actorId, CancellationToken ct)
    {
        var stored = _invoices.First(i =>
            i.TenantId == invoice.TenantId && i.Id == invoice.Id);
        stored.ProformaNo = invoice.ProformaNo;
        stored.RootProformaId = invoice.RootProformaId;
        stored.RevisionNumber = invoice.RevisionNumber;
        stored.Status = invoice.Status;
        stored.LifecycleState = invoice.LifecycleState;
        stored.IssueDate = invoice.IssueDate;
        stored.ValidUntil = invoice.ValidUntil;
        stored.GlassMaterialsSubtotal = invoice.GlassMaterialsSubtotal;
        stored.OtherChargesSubtotal = invoice.OtherChargesSubtotal;
        stored.DiscountAmount = invoice.DiscountAmount;
        stored.VatAmount = invoice.VatAmount;
        stored.GrandTotalVatInclusive = invoice.GrandTotalVatInclusive;
        stored.IsVoid = invoice.IsVoid;
        stored.VoidedAt = invoice.VoidedAt;
        stored.VoidedBy = invoice.VoidedBy;
        stored.VoidReason = invoice.VoidReason;
        stored.SentAt = invoice.SentAt;
        stored.ApprovedAt = invoice.ApprovedAt;
        stored.ConfirmedAt = invoice.ConfirmedAt;
        stored.SnapshotJson = invoice.SnapshotJson;
        stored.RowVersion++;
        stored.UpdatedAt = DateTime.UtcNow;
        stored.UpdatedBy = actorId;

        UpdateCalls++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProformaInvoice>> GetRevisionChainAsync(
        Guid tenantId, Guid rootId, CancellationToken ct)
    {
        IReadOnlyList<ProformaInvoice> chain = _invoices
            .Where(i => i.TenantId == tenantId
                        && (i.RootProformaId == rootId || i.Id == rootId))
            .OrderBy(i => i.RevisionNumber).ThenBy(i => i.Id)
            .ToList();
        return Task.FromResult(chain);
    }

    public Task<(IReadOnlyList<ProformaInvoice> Items, int TotalCount)> ListAsync(
        Guid tenantId, int page, int pageSize, string? status, string? search,
        CancellationToken ct)
    {
        var query = _invoices.Where(i => i.TenantId == tenantId).AsEnumerable();
        if (status is not null)
        {
            query = query.Where(i => i.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(i => i.ProformaNo.Contains(
                search.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var ordered = query.OrderByDescending(i => i.CreatedAt).ToList();
        var items = (IReadOnlyList<ProformaInvoice>)ordered
            .Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult((items, ordered.Count));
    }

    public Task ReplaceSpecGroupsAsync(
        ProformaInvoice invoice, IReadOnlyList<SpecGroupWithItems> groups,
        Guid actorId, CancellationToken ct)
    {
        _grids[invoice.Id] = groups.ToList();

        // The EF store updates the document row in the same transaction.
        return UpdateAsync(invoice, actorId, ct);
    }

    public Task<IReadOnlyList<SpecGroupWithItems>> GetSpecGroupsAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        IReadOnlyList<SpecGroupWithItems> groups =
            _grids.TryGetValue(proformaId, out var grid)
                ? grid.OrderBy(g => g.Group.GroupNumber).ToList()
                : Array.Empty<SpecGroupWithItems>();
        return Task.FromResult(groups);
    }

    private readonly Dictionary<Guid, Step6Rows> _step6 = new();

    public Task ReplaceStep6Async(
        ProformaInvoice invoice,
        IReadOnlyList<Domain.Entities.OtherCharge> charges,
        IReadOnlyList<Domain.Entities.WastageRow> wastages,
        IReadOnlyList<Domain.Entities.CustomCharge> customs,
        Guid actorId, CancellationToken ct)
    {
        _step6[invoice.Id] = new Step6Rows(
            charges.OrderBy(c => c.SortOrder).ToList(),
            wastages.OrderBy(w => w.SortOrder).ToList(),
            customs.OrderBy(c => c.SortOrder).ToList());
        return UpdateAsync(invoice, actorId, ct);
    }

    public Task<Step6Rows> GetStep6Async(Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        return Task.FromResult(_step6.TryGetValue(proformaId, out var rows)
            ? rows
            : new Step6Rows(
                Array.Empty<Domain.Entities.OtherCharge>(),
                Array.Empty<Domain.Entities.WastageRow>(),
                Array.Empty<Domain.Entities.CustomCharge>()));
    }
}
