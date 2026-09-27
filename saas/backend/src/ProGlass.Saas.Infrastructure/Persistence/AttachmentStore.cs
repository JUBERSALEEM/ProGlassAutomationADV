using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.Attachments;

namespace ProGlass.Saas.Infrastructure.Persistence;

/// <summary>
/// Phase 24 — EF storage for the attachments table (0003; entity-keyed,
/// RLS-scoped, tenant-cascading).
/// </summary>
public sealed class AttachmentStore(SaasDbContextFactory factory) : IAttachmentStore
{
    public async Task<Domain.Entities.Attachment> AddAsync(
        Domain.Entities.Attachment attachment, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(attachment.TenantId);
        db.Attachments.Add(attachment);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return attachment;
    }

    public async Task<List<Domain.Entities.Attachment>> ListAsync(
        Guid tenantId, string entityType, Guid entityId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.Attachments.AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Domain.Entities.Attachment?> FindAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.Attachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var row = await db.Attachments
            .FirstOrDefaultAsync(a => a.Id == id, ct).ConfigureAwait(false);
        if (row is null) return false;
        db.Attachments.Remove(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<long> SumBytesAsync(Guid tenantId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.Attachments.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .SumAsync(a => (long?)a.SizeBytes, ct).GetValueOrDefault() ;
    }
}
