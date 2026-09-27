using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Infrastructure.Persistence;

/// <summary>
/// IProformaStore implementation over short-lived tenant-bound contexts
/// (ISaasDbContextFactory — same pattern as the other stores). MECHANICAL
/// ONLY: numbering retries, lifecycle gating and phantom-row filtering
/// live in ProformaService. Reads are tenant-scoped twice (EF query filter
/// + RLS GUC); the Step 5 grid replacement runs in one transaction, like
/// the WPF's single SaveChanges.
/// </summary>
public sealed class ProformaStore : IProformaStore
{
    private readonly ISaasDbContextFactory _factory;

    public ProformaStore(ISaasDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    // ── numbering ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<string>> GetNumbersWithPrefixAsync(
        Guid tenantId, string prefix, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        var lowered = prefix.ToLowerInvariant();
        var ids = await db.ProformaInvoices.AsNoTracking()
            .Where(p => p.ProformaNo.ToLower().StartsWith(lowered))
            .Select(p => p.ProformaNo)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return ids;
    }

    public async Task<bool> ExistsByNumberAsync(
        Guid tenantId, string proformaNo, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.ProformaInvoices.AsNoTracking()
            .AnyAsync(p => p.ProformaNo == proformaNo, ct)
            .ConfigureAwait(false);
    }

    // ── document rows ─────────────────────────────────────────────

    public async Task<ProformaInvoice?> FindByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.ProformaInvoices.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> TryInsertAsync(
        ProformaInvoice invoice, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(invoice.TenantId);
        db.ProformaInvoices.Add(invoice);
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // The WPF caught the unique-violation DbUpdateException and
            // retried with the next free id; here the FALSE tells the
            // service to run that same retry loop.
            return false;
        }
    }

    public async Task UpdateAsync(
        ProformaInvoice invoice, Guid actorId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(invoice.TenantId);
        await using var transaction = await db.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);

        var tracked = await db.ProformaInvoices
            .FirstAsync(p => p.Id == invoice.Id, ct)
            .ConfigureAwait(false);

        CopyDocumentFields(invoice, tracked);
        tracked.RowVersion = tracked.RowVersion + 1;
        tracked.UpdatedAt = DateTime.UtcNow;
        tracked.UpdatedBy = actorId;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProformaInvoice>> GetRevisionChainAsync(
        Guid tenantId, Guid rootId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        // WPF: root plus every "-R{n}" revision, RevisionNumber then Id.
        var chain = await db.ProformaInvoices.AsNoTracking()
            .Where(p => p.RootProformaId == rootId || p.Id == rootId)
            .OrderBy(p => p.RevisionNumber).ThenBy(p => p.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return chain;
    }

    public async Task<(IReadOnlyList<ProformaInvoice> Items, int TotalCount)> ListAsync(
        Guid tenantId, int page, int pageSize, string? status, string? search,
        CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);

        var query = db.ProformaInvoices.AsNoTracking().AsQueryable();
        if (status is not null)
        {
            query = query.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.Trim().ToLowerInvariant();
            query = query.Where(p => p.ProformaNo.ToLower().Contains(lowered));
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);

        // WPF list order: newest first (CreatedAt DESC).
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (items, total);
    }

    // ── Step 5 grid ───────────────────────────────────────────────

    public async Task ReplaceSpecGroupsAsync(
        ProformaInvoice invoice, IReadOnlyList<SpecGroupWithItems> groups,
        Guid actorId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(groups);

        using var db = _factory.CreateForTenant(invoice.TenantId);
        await using var transaction = await db.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);

        var tracked = await db.ProformaInvoices
            .FirstAsync(p => p.Id == invoice.Id, ct)
            .ConfigureAwait(false);

        CopyDocumentFields(invoice, tracked);
        tracked.RowVersion = tracked.RowVersion + 1;
        tracked.UpdatedAt = DateTime.UtcNow;
        tracked.UpdatedBy = actorId;

        // Whole-grid replace: the wizard saves the grid as one unit. The
        // items die with their groups (FK ON DELETE CASCADE in the DDL).
        await db.SpecGroups
            .Where(g => g.ProformaId == invoice.Id)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        foreach (var pair in groups)
        {
            db.SpecGroups.Add(pair.Group);
            foreach (var item in pair.Items)
            {
                db.SpecGroupItems.Add(item);
            }
        }

        // ONE SaveChanges for document + grid — the WPF's single
        // SaveChanges was atomic; a crash between the two writes must not
        // be able to leave a stale grid under a new subtotal.
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SpecGroupWithItems>> GetSpecGroupsAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);

        var groups = await db.SpecGroups.AsNoTracking()
            .Where(g => g.ProformaId == proformaId)
            .OrderBy(g => g.GroupNumber)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (groups.Count == 0)
        {
            return Array.Empty<SpecGroupWithItems>();
        }

        var groupIds = groups.Select(g => g.Id).ToList();
        var items = await db.SpecGroupItems.AsNoTracking()
            .Where(i => groupIds.Contains(i.SpecGroupId))
            .OrderBy(i => i.RowSerial)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return groups
            .Select(g => new SpecGroupWithItems(
                g, items.Where(i => i.SpecGroupId == g.Id).ToList()))
            .ToList();
    }


    // ── Step 6: other charges / wastages / custom charges ─────────

    /// <summary>
    /// Whole-section replace (the wizard saves Step 6 as one unit, like
    /// the grid). ONE transaction: document + all three row tables.
    /// </summary>
    public async Task ReplaceStep6Async(
        ProformaInvoice invoice,
        IReadOnlyList<Domain.Entities.OtherCharge> charges,
        IReadOnlyList<Domain.Entities.WastageRow> wastages,
        IReadOnlyList<Domain.Entities.CustomCharge> customs,
        Guid actorId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        using var db = _factory.CreateForTenant(invoice.TenantId);
        await using var transaction = await db.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);

        var tracked = await db.ProformaInvoices
            .FirstAsync(p => p.Id == invoice.Id, ct)
            .ConfigureAwait(false);

        CopyDocumentFields(invoice, tracked);
        tracked.RowVersion = tracked.RowVersion + 1;
        tracked.UpdatedAt = DateTime.UtcNow;
        tracked.UpdatedBy = actorId;

        await db.OtherCharges
            .Where(c => c.ProformaId == invoice.Id)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await db.WastageRows
            .Where(w => w.ProformaId == invoice.Id)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await db.CustomCharges
            .Where(c => c.ProformaId == invoice.Id)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);

        foreach (var charge in charges)
        {
            db.OtherCharges.Add(charge);
        }

        foreach (var wastage in wastages)
        {
            db.WastageRows.Add(wastage);
        }

        foreach (var custom in customs)
        {
            db.CustomCharges.Add(custom);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<Step6Rows> GetStep6Async(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);

        var charges = await db.OtherCharges.AsNoTracking()
            .Where(c => c.ProformaId == proformaId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct).ConfigureAwait(false);
        var wastages = await db.WastageRows.AsNoTracking()
            .Where(w => w.ProformaId == proformaId)
            .OrderBy(w => w.SortOrder)
            .ToListAsync(ct).ConfigureAwait(false);
        var customs = await db.CustomCharges.AsNoTracking()
            .Where(c => c.ProformaId == proformaId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct).ConfigureAwait(false);

        return new Step6Rows(charges, wastages, customs);
    }

    public async Task ReplaceStep9Async(
        ProformaInvoice invoice,
        IReadOnlyList<Domain.Entities.PnlRow> rows,
        Guid actorId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        using var db = _factory.CreateForTenant(invoice.TenantId);
        await using var transaction = await db.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);

        var tracked = await db.ProformaInvoices
            .FirstAsync(p => p.Id == invoice.Id, ct)
            .ConfigureAwait(false);

        CopyDocumentFields(invoice, tracked);
        tracked.RowVersion = tracked.RowVersion + 1;
        tracked.UpdatedAt = DateTime.UtcNow;
        tracked.UpdatedBy = actorId;

        await db.PnlRows
            .Where(r => r.ProformaId == invoice.Id)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);

        foreach (var row in rows)
        {
            db.PnlRows.Add(row);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Domain.Entities.PnlRow>> GetStep9Async(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);

        return await db.PnlRows.AsNoTracking()
            .Where(r => r.ProformaId == proformaId)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> TryDuplicateAsync(
        ProformaInvoice invoice,
        IReadOnlyList<SpecGroupWithItems> groups,
        Step6Rows step6,
        IReadOnlyList<Domain.Entities.PnlRow> pnlRows,
        Guid actorId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        // Fresh context per attempt: a unique-violation aborts the
        // transaction, and the retry must start from a clean tracker.
        using var db = _factory.CreateForTenant(invoice.TenantId);
        await using var transaction = await db.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);

        db.ProformaInvoices.Add(invoice);

        foreach (var group in groups)
        {
            db.SpecGroups.Add(group.Group);
            foreach (var item in group.Items)
            {
                db.SpecGroupItems.Add(item);
            }
        }

        foreach (var charge in step6.Charges) db.OtherCharges.Add(charge);
        foreach (var wastage in step6.Wastages) db.WastageRows.Add(wastage);
        foreach (var custom in step6.Customs) db.CustomCharges.Add(custom);
        foreach (var row in pnlRows) db.PnlRows.Add(row);

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return false;
        }
    }

    public async Task<Inquiry?> FindInquiryAsync(
        Guid tenantId, Guid inquiryId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);

        return await db.Inquiries.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == inquiryId, ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasProformaForInquiryAsync(
        Guid tenantId, Guid inquiryId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);

        return await db.ProformaInvoices.AsNoTracking()
            .AnyAsync(p => p.InquiryId == inquiryId, ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> TryLinkInquiryToProformaAsync(
        Guid tenantId, Guid inquiryId, Guid proformaId, Guid actorId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);

        var rows = await db.Inquiries
            .Where(i => i.Id == inquiryId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.ConvertedProformaId, proformaId)
                .SetProperty(i => i.UpdatedAt, DateTime.UtcNow)
                .SetProperty(i => i.UpdatedBy, actorId), ct)
            .ConfigureAwait(false);

        return rows == 1;
    }

    public async Task<bool> TryAdvanceInquiryStatusAsync(
        Guid tenantId, Guid inquiryId, string status, Guid actorId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);

        var rows = await db.Inquiries
            .Where(i => i.Id == inquiryId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, status)
                .SetProperty(i => i.UpdatedAt, DateTime.UtcNow)
                .SetProperty(i => i.UpdatedBy, actorId), ct)
            .ConfigureAwait(false);

        return rows == 1;
    }

    public async Task<IReadOnlyList<Domain.Proforma.SheetStockEntry>> GetSheetStockAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);

        // The WPF pricing service reads the WHOLE table and filters in
        // memory (Norm colour matching is not expressible in SQL); the
        // Sheet Store is a small per-tenant table.
        var sheets = await db.GlassSheets.AsNoTracking()
            .Where(s => s.WidthMm > 0 && s.HeightMm > 0)
            .ToListAsync(ct).ConfigureAwait(false);

        return sheets.Select(s => new Domain.Proforma.SheetStockEntry(
            s.Color, s.ThicknessMm, s.WidthMm, s.HeightMm,
            s.PurchasePricePerSqm, s.SellPricePerSqm)).ToList();
    }


    // ── helpers ────────────────────────────────────────────────────


    /// <summary>Full-row field copy from a detached snapshot onto the
    /// tracked row (identity/audit-created columns stay).</summary>
    private static void CopyDocumentFields(ProformaInvoice source, ProformaInvoice target)
    {
        target.ProformaNo = source.ProformaNo;
        target.RootProformaId = source.RootProformaId;
        target.RevisionNumber = source.RevisionNumber;
        target.InquiryId = source.InquiryId;
        target.CompanyId = source.CompanyId;
        target.ClientId = source.ClientId;
        target.ProjectId = source.ProjectId;
        target.SalesmanId = source.SalesmanId;
        target.Status = source.Status;
        target.StatusText = source.StatusText;
        target.LifecycleState = source.LifecycleState;
        target.IssueDate = source.IssueDate;
        target.ValidUntil = source.ValidUntil;
        target.GlassMaterialsSubtotal = source.GlassMaterialsSubtotal;
        target.OtherChargesSubtotal = source.OtherChargesSubtotal;
        target.DiscountAmount = source.DiscountAmount;
        target.VatAmount = source.VatAmount;
        target.GrandTotalVatInclusive = source.GrandTotalVatInclusive;
        target.IsVoid = source.IsVoid;
        target.VoidedAt = source.VoidedAt;
        target.VoidedBy = source.VoidedBy;
        target.VoidReason = source.VoidReason;
        target.FurthestStep = source.FurthestStep;
        target.SnapshotJson = source.SnapshotJson;
        target.SentAt = source.SentAt;
        target.ApprovedAt = source.ApprovedAt;
        target.ConfirmedAt = source.ConfirmedAt;
        target.JobOrderId = source.JobOrderId;
        target.PnlWastagePercent = source.PnlWastagePercent;
        target.PnlCostAtSellingPrice = source.PnlCostAtSellingPrice;
        target.PnlGrossVatInclusive = source.PnlGrossVatInclusive;
        target.PnlNetExVat = source.PnlNetExVat;
        target.PnlTotalGlassCost = source.PnlTotalGlassCost;
        target.PnlTotalProductionCost = source.PnlTotalProductionCost;
        target.PnlProfit = source.PnlProfit;
        target.PnlMarginPercent = source.PnlMarginPercent;
        target.PnlOverallMarginPercent = source.PnlOverallMarginPercent;
        target.PnlNetMarginPercent = source.PnlNetMarginPercent;
        target.PnlUInsertCost = source.PnlUInsertCost;
        target.PnlBasisCost = source.PnlBasisCost;
        target.PnlCalculatedAt = source.PnlCalculatedAt;
        target.DiscountType = source.DiscountType;
        target.DiscountValue = source.DiscountValue;
        target.IsVatApplicable = source.IsVatApplicable;
        target.VatPercent = source.VatPercent;
    }

    /// <summary>PostgreSQL unique violation (23505) inside a
    /// DbUpdateException — the WPF used the SQLite error classifier for
    /// exactly this test (extended code 2067); this is the Npgsql
    /// equivalent, immune to error-message drift.</summary>
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == "23505";
}
