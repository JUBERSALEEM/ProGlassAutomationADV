// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 24:
//   Services/DeliveryNote/Attachments/AttachmentService.cs (the upload
//   path: extension + size validation, sanitized unique file names,
//   copy-not-move semantics, delete = row authoritative + best-effort
//   file cleanup BUG-27)
//
// Documented differences (mapping §1.12):
//  • The WPF stores files under AppData/DeliveryNotes/Attachments/
//    {Images|PDFs}/{DnId}/ on the workstation. The SaaS stores them
//    under a configurable root (SAAS_ATTACHMENT_ROOT) keyed
//    tenant/entity-type/entity-id, with the storage_key indirection in
//    the attachments table so an object-storage backend can drop in
//    later without a schema change.
//  • The WPF attaches to delivery notes only; the SaaS attachments
//    table is entity-generic (since 0003) — the service accepts the
//    five document entity types that exist today, each gated by that
//    entity's own permission pair.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Attachments;

namespace ProGlass.Saas.Application.Attachments;

/// <summary>One stored attachment (the WPF DeliveryNoteAttachment,
/// generalized to the entity-keyed SaaS row).</summary>
public sealed record AttachmentDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string OriginalName,
    string MimeType,
    long SizeBytes,
    string SizeDisplay,
    bool IsImage,
    Guid? UploadedBy,
    DateTime UploadedAt);

/// <summary>The stored bytes ready to stream.</summary>
public sealed record AttachmentDownload(
    AttachmentDto Metadata,
    Stream Content);

public sealed class AttachmentResult<T>
{
    public bool Success { get; private init; }
    public string Message { get; private init; } = "";
    public T? Value { get; private init; }

    public static AttachmentResult<T> Ok(T value) =>
        new() { Success = true, Value = value };

    public static AttachmentResult<T> Fail(string message) =>
        new() { Success = false, Message = message };
}

/// <summary>Byte storage behind the attachments table (filesystem today;
/// object storage can implement the same contract later).</summary>
public interface IAttachmentStorage
{
    /// <summary>Writes the stream under the key and returns the final
    /// (unique) storage key.</summary>
    Task<string> SaveAsync(string baseKey, string fileName, Stream content,
        CancellationToken ct);

    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct);

    /// <summary>BUG-27 semantics: true if deleted, false if missing or
    /// locked — best-effort, never throws.</summary>
    bool Delete(string storageKey);

    bool Exists(string storageKey);
}

public interface IAttachmentStore
{
    Task<Attachment> AddAsync(Attachment attachment, CancellationToken ct);
    Task<List<Attachment>> ListAsync(Guid tenantId, string entityType, Guid entityId, CancellationToken ct);
    Task<Attachment?> FindAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<long> SumBytesAsync(Guid tenantId, CancellationToken ct);
}

/// <summary>
/// The WPF entity types the SaaS accepts attachments for, with each
/// entity's own permission pair (view to list/download, edit to
/// upload/delete).
/// </summary>
public static class AttachmentEntities
{
    public static readonly IReadOnlyDictionary<string, (string View, string Edit)> All =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["delivery_note"] = ("DeliveryNote.View", "DeliveryNote.Edit"),
            ["proforma_invoice"] = ("ProformaInvoice.View", "ProformaInvoice.Edit"),
            ["tax_invoice"] = ("TaxInvoice.View", "TaxInvoice.Edit"),
            ["credit_note"] = ("CreditNote.View", "CreditNote.Edit"),
            ["job_order"] = ("JobOrder.View", "JobOrder.Edit"),
            ["inquiry"] = ("Inquiry.View", "Inquiry.Edit")
        };

    public static bool IsKnown(string entityType) => All.ContainsKey(entityType ?? "");
}

public sealed class AttachmentService(
    IAttachmentStore store,
    IAttachmentStorage storage)
{
    public async Task<AttachmentResult<AttachmentDto>> UploadAsync(
        Guid tenantId, Guid actorId, string entityType, Guid entityId,
        string fileName, string? contentType, Stream content, CancellationToken ct = default)
    {
        if (!AttachmentEntities.IsKnown(entityType))
        {
            return AttachmentResult<AttachmentDto>.Fail(
                $"Unknown attachment entity type '{entityType}'. Known: " +
                string.Join(", ", AttachmentEntities.All.Keys.OrderBy(k => k)) + ".");
        }

        // The WPF upload path validates BEFORE any byte is written:
        // extension first, then the 10 MB size limit.
        if (!AttachmentRules.IsValidExtension(fileName))
        {
            return AttachmentResult<AttachmentDto>.Fail(
                $"'{Path.GetExtension(fileName)}' is not an accepted attachment extension. " +
                $"Accepted: {string.Join(", ", AttachmentRules.AllExtensions)} " +
                $"(max {AttachmentRules.MaxFileSizeDisplay}).");
        }

        if (content.CanSeek && content.Length > AttachmentRules.MaxFileSizeBytes)
        {
            return AttachmentResult<AttachmentDto>.Fail(
                $"The file exceeds the {AttachmentRules.MaxFileSizeDisplay} attachment limit.");
        }

        // storage_key: tenant/entity/entity-id + the sanitized UNIQUE name
        // (S-8 sanitizer; BUG-28 unique resolution — never a silent
        // overwrite).
        var safeName = AttachmentRules.SanitizeName(
            string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(fileName))
                ? "file" + Path.GetExtension(fileName)
                : fileName);
        var baseKey = $"{tenantId:N}/{AttachmentRules.SanitizeName(entityType)}/{entityId:N}";
        var storageKey = await storage.SaveAsync(baseKey, safeName, content, ct)
            .ConfigureAwait(false);

        // A non-seekable stream (chunked HTTP) is length-checked by the
        // bytes actually written.
        long size = 0;
        if (storage.Exists(storageKey))
        {
            var probe = await storage.OpenReadAsync(storageKey, ct).ConfigureAwait(false);
            if (probe is not null)
            {
                size = probe.Length;
                probe.Dispose();
            }
        }
        if (size > AttachmentRules.MaxFileSizeBytes)
        {
            storage.Delete(storageKey);   // never keep an over-limit file
            return AttachmentResult<AttachmentDto>.Fail(
                $"The file exceeds the {AttachmentRules.MaxFileSizeDisplay} attachment limit.");
        }

        var attachment = new Domain.Entities.Attachment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = entityType,
            EntityId = entityId,
            StorageKey = storageKey,
            OriginalName = fileName,
            MimeType = contentType ?? "",
            SizeBytes = size,
            UploadedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        try
        {
            await store.AddAsync(attachment, ct).ConfigureAwait(false);
        }
        catch
        {
            // No orphaned bytes: the row is the record — if it cannot
            // be written, the file goes with it (best effort).
            storage.Delete(storageKey);
            throw;
        }

        return AttachmentResult<AttachmentDto>.Ok(ToDto(attachment));
    }

    public async Task<AttachmentResult<List<AttachmentDto>>> ListAsync(
        Guid tenantId, string entityType, Guid entityId, CancellationToken ct = default)
    {
        if (!AttachmentEntities.IsKnown(entityType))
            return AttachmentResult<List<AttachmentDto>>.Fail(
                $"Unknown attachment entity type '{entityType}'.");
        var rows = await store.ListAsync(tenantId, entityType, entityId, ct)
            .ConfigureAwait(false);
        return AttachmentResult<List<AttachmentDto>>.Ok(rows.Select(ToDto).ToList());
    }

    /// <summary>Metadata-only lookup (no byte stream is opened).</summary>
    public async Task<AttachmentResult<AttachmentDto>> GetAttachmentAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var attachment = await store.FindAsync(tenantId, id, ct).ConfigureAwait(false);
        if (attachment is null)
            return AttachmentResult<AttachmentDto>.Fail($"Attachment {id} was not found.");
        return AttachmentResult<AttachmentDto>.Ok(ToDto(attachment));
    }

    public async Task<AttachmentResult<AttachmentDownload>> DownloadAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var attachment = await store.FindAsync(tenantId, id, ct).ConfigureAwait(false);
        if (attachment is null)
            return AttachmentResult<AttachmentDownload>.Fail(
                $"Attachment {id} was not found.");

        var stream = await storage.OpenReadAsync(attachment.StorageKey, ct).ConfigureAwait(false);
        if (stream is null)
            return AttachmentResult<AttachmentDownload>.Fail(
                $"The file for attachment {id} is missing from storage.");

        return AttachmentResult<AttachmentDownload>.Ok(
            new AttachmentDownload(ToDto(attachment), stream));
    }

    /// <summary>
    /// Delete — the DB row is authoritative; the file cleanup is
    /// best-effort (WPF BUG-27 semantics).
    /// </summary>
    public async Task<AttachmentResult<bool>> DeleteAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var attachment = await store.FindAsync(tenantId, id, ct).ConfigureAwait(false);
        if (attachment is null)
            return AttachmentResult<bool>.Fail($"Attachment {id} was not found.");

        var deleted = await store.DeleteAsync(tenantId, id, ct).ConfigureAwait(false);
        storage.Delete(attachment.StorageKey);
        return AttachmentResult<bool>.Ok(deleted);
    }

    /// <summary>Total bytes stored for a tenant (the storage-GB counter
    /// the subscription catalog will bill against).</summary>
    public Task<long> GetStorageBytesAsync(Guid tenantId, CancellationToken ct = default) =>
        store.SumBytesAsync(tenantId, ct);

    private static AttachmentDto ToDto(Domain.Entities.Attachment a) => new(
        a.Id, a.EntityType, a.EntityId, a.OriginalName, a.MimeType,
        a.SizeBytes, AttachmentRules.FormatFileSize(a.SizeBytes),
        AttachmentRules.IsImage(a.OriginalName),
        a.UploadedBy, a.CreatedAt);
}
