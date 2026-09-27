// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 24:
//   Services/DeliveryNote/Attachments/AttachmentService.cs — the upload
//   surface (the WPF is a file-dialog + local folder per delivery note;
//   the SaaS is a multipart upload + keyed byte storage behind the
//   entity-generic attachments table).
// Permissions: each entity type's own pair — view to list/download,
// edit to upload/delete (the WPF DN attachments sit behind the DN
// module's permissions; the SaaS generalizes to the entity).
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Attachments;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

[Authorize]
public sealed class AttachmentsController(
    AttachmentService attachments,
    IAuthorizationService authorization) : TenantControllerBase
{
    /// <summary>Upload one image or PDF (≤ 10 MB) against a document.
    /// The WPF validation order: extension first, then size — no byte
    /// is written for an invalid file.</summary>
    [HttpPost("api/v1/attachments/{entityType}/{entityId:guid}")]
    public async Task<IActionResult> Upload(
        string entityType, Guid entityId, IFormFile file, CancellationToken ct)
    {
        var tenant = RequiredTenantId;
        if (!await AuthorizeEntityAsync(entityType, edit: true, ct).ConfigureAwait(false))
            return Forbid();

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file was uploaded." });

        await using var stream = file.OpenReadStream();
        var result = await attachments.UploadAsync(
            tenant, RequiredUserId, entityType, entityId,
            file.FileName, file.ContentType, stream, ct).ConfigureAwait(false);
        return result.Success ? Ok(result.Value) : BadRequest(new { message = result.Message });
    }

    /// <summary>List a document's attachments (metadata only).</summary>
    [HttpGet("api/v1/attachments/{entityType}/{entityId:guid}")]
    public async Task<IActionResult> List(
        string entityType, Guid entityId, CancellationToken ct)
    {
        var tenant = RequiredTenantId;
        if (!await AuthorizeEntityAsync(entityType, edit: false, ct).ConfigureAwait(false))
            return Forbid();

        var result = await attachments.ListAsync(tenant, entityType, entityId, ct)
            .ConfigureAwait(false);
        return result.Success ? Ok(result.Value) : BadRequest(new { message = result.Message });
    }

    /// <summary>Download one attachment's bytes.</summary>
    [HttpGet("api/v1/attachments/{id:guid}/content")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var tenant = RequiredTenantId;
        var result = await attachments.DownloadAsync(tenant, id, ct).ConfigureAwait(false);
        if (!result.Success)
            return NotFound(new { message = result.Message });

        var download = result.Value!;
        var contentType = string.IsNullOrWhiteSpace(download.Metadata.MimeType)
            ? "application/octet-stream"
            : download.Metadata.MimeType;
        return File(download.Content, contentType, download.Metadata.OriginalName);
    }

    /// <summary>Delete one attachment — the DB row is authoritative, the
    /// file cleanup is best-effort (WPF BUG-27).</summary>
    [HttpDelete("api/v1/attachments/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var tenant = RequiredTenantId;
        // The entity type is only known after the row is read; deleting
        // requires the EDIT permission of the entity the row belongs to.
        var probe = await attachments.GetAttachmentAsync(tenant, id, ct).ConfigureAwait(false);
        if (!probe.Success)
            return NotFound(new { message = probe.Message });

        if (!await AuthorizeEntityAsync(probe.Value!.EntityType, edit: true, ct)
                .ConfigureAwait(false))
            return Forbid();

        var result = await attachments.DeleteAsync(tenant, id, ct).ConfigureAwait(false);
        return result.Success ? Ok(new { deleted = true }) : BadRequest(new { message = result.Message });
    }

    /// <summary>Total attachment bytes stored for the caller's tenant
    /// (the storage-GB counter).</summary>
    [HttpGet("api/v1/attachments/usage")]
    [HasPermission(AppPermissions.ReportsView)]
    public async Task<IActionResult> Usage(CancellationToken ct)
    {
        var tenant = RequiredTenantId;
        var bytes = await attachments.GetStorageBytesAsync(tenant, ct).ConfigureAwait(false);
        return Ok(new
        {
            bytes,
            sizeDisplay = Domain.Attachments.AttachmentRules.FormatFileSize(bytes)
        });
    }

    private async Task<bool> AuthorizeEntityAsync(string entityType, bool edit, CancellationToken ct)
    {
        if (!AttachmentEntities.All.TryGetValue(entityType, out var pair))
            return false;

        var permission = edit ? pair.Edit : pair.View;
        var decision = await authorization.AuthorizeAsync(User, null, "permission:" + permission)
            .ConfigureAwait(false);
        return decision.Succeeded;
    }
}
