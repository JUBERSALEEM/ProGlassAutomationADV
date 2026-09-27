using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.JobOrders;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Infrastructure.Persistence;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 9 storage — the WPF JobOrderRepository port (bump-and-retry
//  number allocation, optimistic RowVersion, RLS-scoped contexts).
// ═══════════════════════════════════════════════════════════════════════

public sealed class JobOrderStore(SaasDbContextFactory factory) : IJobOrderStore
{
    public async Task<ProformaContext?> FindProformaContextAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var invoice = await db.ProformaInvoices.AsNoTracking()
            .Include(p => p.Client)
            .Include(p => p.Project)
            .FirstOrDefaultAsync(p => p.Id == proformaId, ct)
            .ConfigureAwait(false);
        if (invoice is null)
        {
            return null;
        }

        // WPF JobOrderMapper.ResolveProjectName: identifier, then the
        // descriptive reference, then the contract number.
        var projectName = invoice.Project is null
            ? ""
            : !string.IsNullOrWhiteSpace(invoice.Project.IdentifierName)
                ? invoice.Project.IdentifierName.Trim()
                : !string.IsNullOrWhiteSpace(invoice.Project.Reference)
                    ? invoice.Project.Reference.Trim()
                    : (invoice.Project.ContractNo ?? "").Trim();

        return new ProformaContext(
            invoice, invoice.Client?.CustomerName ?? "", projectName);
    }

    public async Task<MasterData.PagedResult<JobOrder>> SearchAsync(
        Guid tenantId, string? search, string? status, bool includeVoid,
        int page, int pageSize, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var query = db.JobOrders.AsNoTracking().AsQueryable();

        if (!includeVoid)
        {
            query = query.Where(j => !j.IsVoid);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(j => j.Status == status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(j =>
                j.JobNo.ToLower().Contains(s)
                || j.CustomerName.ToLower().Contains(s)
                || j.ProjectName.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);

        return new MasterData.PagedResult<JobOrder>(items, page, pageSize, total);
    }

    public async Task<JobOrder?> FindAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.JobOrders.AsNoTracking()
            .Include(j => j.SourceProforma)
            .FirstOrDefaultAsync(j => j.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<JobOrder>> FindByProformaAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.JobOrders.AsNoTracking()
            .Where(j => j.ProformaId == proformaId)
            .OrderBy(j => j.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> ExistsNoAsync(
        Guid tenantId, string jobOrderNo, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.JobOrders.AsNoTracking()
            .AnyAsync(j => j.JobNo == jobOrderNo, ct).ConfigureAwait(false);
    }

    public async Task<string> SuggestNextNoAsync(
        Guid tenantId, string baseNo, CancellationToken ct)
    {
        // The WPF loop: increment until unused (candidate++ each round).
        using var db = factory.CreateForTenant(tenantId);
        var suggestion = Domain.Proforma.JobOrderNumbers.SuggestNext(baseNo);
        for (var guard = 0; guard < 1000; guard++)
        {
            if (!await db.JobOrders.AsNoTracking()
                    .AnyAsync(j => j.JobNo == suggestion, ct)
                    .ConfigureAwait(false))
            {
                return suggestion;
            }

            suggestion = Domain.Proforma.JobOrderNumbers.SuggestNext(suggestion);
        }

        return suggestion;
    }

    public async Task<bool> TryInsertAsync(JobOrder jobOrder, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(jobOrder.TenantId);
        db.JobOrders.Add(jobOrder);
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return false;
        }
    }

    public async Task<bool> TryUpdateAsync(
        JobOrder jobOrder, int? expectedRowVersion, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(jobOrder.TenantId);
        var stored = await db.JobOrders
            .FirstOrDefaultAsync(j => j.Id == jobOrder.Id, ct).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        // Optimistic-concurrency check: a moved-on counter means someone
        // else saved first — never silently overwrite (WPF
        // ConcurrencyConflictException).
        if (expectedRowVersion.HasValue
            && stored.RowVersion != expectedRowVersion.Value)
        {
            return false;
        }

        stored.JobNo = jobOrder.JobNo;
        stored.CustomerId = jobOrder.CustomerId;
        stored.ProjectId = jobOrder.ProjectId;
        stored.SalesmanId = jobOrder.SalesmanId;
        stored.CustomerName = jobOrder.CustomerName;
        stored.ProjectName = jobOrder.ProjectName;
        stored.GrandTotalVatInclusive = jobOrder.GrandTotalVatInclusive;
        stored.Status = jobOrder.Status;
        stored.Priority = jobOrder.Priority;
        stored.DueDate = jobOrder.DueDate;
        stored.SiteDeliveryContact = jobOrder.SiteDeliveryContact;
        stored.SiteDeliveryPhone = jobOrder.SiteDeliveryPhone;
        stored.SiteDeliveryAddress = jobOrder.SiteDeliveryAddress;
        stored.AssignedTo = jobOrder.AssignedTo;
        stored.CuttingInstructions = jobOrder.CuttingInstructions;
        stored.DrawingReference = jobOrder.DrawingReference;
        stored.InternalNotes = jobOrder.InternalNotes;
        stored.Notes = jobOrder.Notes;
        stored.IsVoid = jobOrder.IsVoid;
        stored.VoidedAt = jobOrder.VoidedAt;
        stored.VoidedBy = jobOrder.VoidedBy;
        stored.VoidReason = jobOrder.VoidReason;
        stored.SnapshotJson = jobOrder.SnapshotJson;
        // The grand total NEVER changes after conversion (WPF
        // UpdateEntityFromSnapshot note) — neither does the source link.
        stored.RowVersion++;
        stored.UpdatedAt = jobOrder.UpdatedAt;
        stored.UpdatedBy = jobOrder.UpdatedBy;

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            // Reflect the authoritative counter back onto the caller's
            // instance so the DTO carries the fresh version.
            jobOrder.RowVersion = stored.RowVersion;
            jobOrder.UpdatedAt = stored.UpdatedAt;
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return false;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
        || ex.InnerException?.Message.Contains(
            "duplicate key value violates unique constraint",
            StringComparison.OrdinalIgnoreCase) == true;
}
