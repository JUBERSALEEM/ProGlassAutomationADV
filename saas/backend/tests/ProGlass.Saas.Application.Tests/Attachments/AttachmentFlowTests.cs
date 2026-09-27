// ═══════════════════════════════════════════════════════════════════════
//  Phase 24 — the attachments port golden tests:
//   • AttachmentRules — the S-8 sanitizer, the BUG-28 unique-name rule
//     and FormatFileSize, verbatim from the WPF
//     Services/DeliveryNote/Attachments/AttachmentService.cs +
//     AttachmentType.cs (read 2026-09-27).
//   • AttachmentService — the upload/list/download/delete flow against
//     an in-memory store and a REAL temp-dir filesystem storage (the
//     bytes path is the port's core, so it is exercised on disk).
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Application.Attachments;
using ProGlass.Saas.Domain.Attachments;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Infrastructure.Attachments;

namespace ProGlass.Saas.Application.Tests.Attachments;

/// <summary>In-memory IAttachmentStore (the EF store is thin — these
/// tests target the service's rules, not EF).</summary>
sealed class FakeAttachmentStore : IAttachmentStore
{
    public List<Attachment> Rows { get; } = [];

    public Task<Attachment> AddAsync(Attachment attachment, CancellationToken ct)
    {
        Rows.Add(attachment);
        return Task.FromResult(attachment);
    }

    public Task<List<Attachment>> ListAsync(Guid tenantId, string entityType, Guid entityId, CancellationToken ct) =>
        Task.FromResult(Rows
            .Where(a => a.TenantId == tenantId && a.EntityType == entityType
                        && a.EntityId == entityId)
            .OrderBy(a => a.CreatedAt)
            .ToList());

    public Task<Attachment?> FindAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        Task.FromResult(Rows.FirstOrDefault(a => a.TenantId == tenantId && a.Id == id));

    public Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        Task.FromResult(Rows.RemoveAll(a => a.TenantId == tenantId && a.Id == id) > 0);

    public Task<long> SumBytesAsync(Guid tenantId, CancellationToken ct) =>
        Task.FromResult(Rows.Where(a => a.TenantId == tenantId)
            .Sum(a => a.SizeBytes));
}

public sealed class AttachmentFlowTests : IDisposable
{
    private readonly FakeAttachmentStore _store = new();
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "pg-att-tests-" + Guid.NewGuid().ToString("N"));
    private readonly AttachmentService _service;

    public AttachmentFlowTests()
    {
        _service = new AttachmentService(
            _store, new FileAttachmentStorage(_root));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private static MemoryStream Bytes(string text) =>
        new(System.Text.Encoding.UTF8.GetBytes(text));

    // ── upload → list → download → delete ───────────────────────────

    [Fact]
    public async Task Upload_saves_bytes_under_tenant_key_and_lists()
    {
        var tenant = Guid.NewGuid();
        var dn = Guid.NewGuid();
        var actor = Guid.NewGuid();

        var up = await _service.UploadAsync(
            tenant, actor, "delivery_note", dn, "site photo.jpg",
            "image/jpeg", Bytes("jpeg-bytes"));
        Assert.True(up.Success, up.Message);

        var dto = up.Value!;
        Assert.Equal("site photo.jpg", dto.OriginalName);
        Assert.Equal("image/jpeg", dto.MimeType);
        Assert.Equal(10, dto.SizeBytes);          // "jpeg-bytes".Length
        Assert.Equal("10 B", dto.SizeDisplay);
        Assert.True(dto.IsImage);
        Assert.Equal(actor, dto.UploadedBy);

        // The storage key is tenant-scoped and the sanitized name has
        // the space turned into '_' (S-8: ' ' is an invalid filename
        // char on Windows).
        var row = _store.Rows.Single();
        Assert.Equal(tenant, row.TenantId);
        Assert.Equal("delivery_note", row.EntityType);
        Assert.Equal(dn, row.EntityId);
        Assert.StartsWith($"{tenant:N}/delivery_note/{dn:N}/", row.StorageKey);
        Assert.EndsWith("site_photo.jpg", row.StorageKey);
        Assert.True(File.Exists(Path.Combine(_root,
            row.StorageKey.Replace('/', Path.DirectorySeparatorChar))));

        var list = await _service.ListAsync(tenant, "delivery_note", dn);
        Assert.Single(list.Value!);
        Assert.Equal(dto.Id, list.Value![0].Id);
    }

    [Fact]
    public async Task Download_returns_the_stored_bytes()
    {
        var tenant = Guid.NewGuid();
        var dn = Guid.NewGuid();
        var up = await _service.UploadAsync(
            tenant, Guid.NewGuid(), "delivery_note", dn, "signed.pdf",
            "application/pdf", Bytes("pdf-bytes"));
        Assert.True(up.Success, up.Message);

        var dl = await _service.DownloadAsync(tenant, up.Value!.Id);
        Assert.True(dl.Success, dl.Message);
        using var reader = new StreamReader(dl.Value!.Content);
        Assert.Equal("pdf-bytes", reader.ReadToEnd());
        Assert.False(dl.Value.Metadata.IsImage);
        Assert.Equal("application/pdf", dl.Value.Metadata.MimeType);
    }

    [Fact]
    public async Task Delete_removes_row_and_file_best_effort()
    {
        var tenant = Guid.NewGuid();
        var dn = Guid.NewGuid();
        var up = await _service.UploadAsync(
            tenant, Guid.NewGuid(), "delivery_note", dn, "gone.png",
            "image/png", Bytes("png"));
        var key = _store.Rows.Single().StorageKey;
        var path = Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar));

        var del = await _service.DeleteAsync(tenant, up.Value!.Id);
        Assert.True(del.Success, del.Message);
        Assert.Empty(_store.Rows);
        Assert.False(File.Exists(path));

        // BUG-27 semantics: deleting an unknown id fails cleanly.
        var missing = await _service.DeleteAsync(tenant, up.Value.Id);
        Assert.False(missing.Success);
    }

    [Fact]
    public async Task Upload_same_name_twice_never_overwrites()
    {
        var tenant = Guid.NewGuid();
        var dn = Guid.NewGuid();
        var actor = Guid.NewGuid();

        var first = await _service.UploadAsync(
            tenant, actor, "delivery_note", dn, "a.pdf", "application/pdf",
            Bytes("first"));
        var second = await _service.UploadAsync(
            tenant, actor, "delivery_note", dn, "a.pdf", "application/pdf",
            Bytes("second"));

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.NotEqual(first.Value!.Id, second.Value!.Id);

        // BUG-28: the plain name, then name-1, name-2 …
        var keys = _store.Rows.Select(r => Path.GetFileName(r.StorageKey))
            .OrderBy(k => k, StringComparer.Ordinal).ToList();
        // "a-1.pdf" sorts before "a.pdf" (hyphen < dot).
        Assert.Equal(new[] { "a-1.pdf", "a.pdf" }, keys);

        // Both files' bytes survive.
        var dl1 = await _service.DownloadAsync(tenant, first.Value.Id);
        var dl2 = await _service.DownloadAsync(tenant, second.Value!.Id);
        using (var r1 = new StreamReader(dl1.Value!.Content)) Assert.Equal("first", r1.ReadToEnd());
        using (var r2 = new StreamReader(dl2.Value!.Content)) Assert.Equal("second", r2.ReadToEnd());
    }

    // ── the WPF validation order: extension, then size ──────────────

    [Theory]
    [InlineData("notes.exe")]
    [InlineData("archive.zip")]
    [InlineData("photo.gifx")]
    public async Task Upload_rejects_unaccepted_extensions(string name)
    {
        var result = await _service.UploadAsync(
            Guid.NewGuid(), Guid.NewGuid(), "delivery_note", Guid.NewGuid(),
            name, "application/octet-stream", Bytes("x"));
        Assert.False(result.Success);
        Assert.Contains("not an accepted attachment extension", result.Message);
        Assert.Empty(_store.Rows);
    }

    [Fact]
    public async Task Upload_rejects_over_10mb()
    {
        var big = new MemoryStream(new byte[AttachmentRules.MaxFileSizeBytes + 1]);
        var result = await _service.UploadAsync(
            Guid.NewGuid(), Guid.NewGuid(), "delivery_note", Guid.NewGuid(),
            "huge.pdf", "application/pdf", big);
        Assert.False(result.Success);
        Assert.Contains("10 MB", result.Message);
        Assert.Empty(_store.Rows);
    }

    [Fact]
    public async Task Upload_rejects_unknown_entity_type()
    {
        var result = await _service.UploadAsync(
            Guid.NewGuid(), Guid.NewGuid(), "purchase_order", Guid.NewGuid(),
            "a.pdf", "application/pdf", Bytes("x"));
        Assert.False(result.Success);
        Assert.Contains("Unknown attachment entity type", result.Message);
    }

    [Fact]
    public async Task Upload_allows_all_six_entity_types()
    {
        var tenant = Guid.NewGuid();
        // The five document families + inquiry (the migration imports
        // InquiryAttachments, so the API surface must accept them).
        foreach (var type in new[]
                 { "delivery_note", "proforma_invoice", "tax_invoice",
                   "credit_note", "job_order", "inquiry" })
        {
            var up = await _service.UploadAsync(
                tenant, Guid.NewGuid(), type, Guid.NewGuid(), "a.pdf",
                "application/pdf", Bytes("x"));
            Assert.True(up.Success, type + ": " + up.Message);
        }
        Assert.Equal(6, _store.Rows.Count);
        Assert.Equal(6, await _service.GetStorageBytesAsync(tenant));
    }

    [Fact]
    public async Task Tenants_are_isolated()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var dn = Guid.NewGuid();
        await _service.UploadAsync(a, Guid.NewGuid(), "delivery_note", dn,
            "a.pdf", "application/pdf", Bytes("tenant-a"));
        await _service.UploadAsync(b, Guid.NewGuid(), "delivery_note", dn,
            "b.pdf", "application/pdf", Bytes("tenant-b!"));

        var listA = await _service.ListAsync(a, "delivery_note", dn);
        var listB = await _service.ListAsync(b, "delivery_note", dn);
        Assert.Single(listA.Value!);
        Assert.Single(listB.Value!);
        Assert.Equal("a.pdf", listA.Value![0].OriginalName);

        Assert.Equal(9, await _service.GetStorageBytesAsync(a));
        Assert.Equal(9, await _service.GetStorageBytesAsync(b));

        // Cross-tenant download is a not-found, never a leak.
        var probe = await _service.DownloadAsync(b, listA.Value![0].Id);
        Assert.False(probe.Success);
    }

    // ── AttachmentRules: the WPF goldens ────────────────────────────

    [Theory]
    [InlineData("", "_")]
    [InlineData("   ", "_")]
    [InlineData("..", "_")]
    [InlineData("...", "_")]
    [InlineData("a/b", "a_b")]
    [InlineData("a\\b", "a_b")]
    [InlineData("a:b", "a_b")]
    [InlineData("trailing... ", "trailing")]
    [InlineData("report<>.pdf", "report__.pdf")]
    public void SanitizeName_matches_the_wpf_rule(string input, string expected)
    {
        Assert.Equal(expected, AttachmentRules.SanitizeName(input));
    }

    [Fact]
    public void SanitizeName_caps_at_120_chars()
    {
        var safe = new string('a', 300);
        Assert.Equal(120, AttachmentRules.SanitizeName(safe).Length);
    }

    [Fact]
    public void ResolveUniqueName_uses_the_wpf_hyphen_rule()
    {
        var taken = new HashSet<string> { "a.pdf", "a-1.pdf", "a-2.pdf" };
        var name = AttachmentRules.ResolveUniqueName(
            "a.pdf", taken.Contains);
        Assert.Equal("a-3.pdf", name);

        // Free name → unchanged.
        Assert.Equal("free.pdf",
            AttachmentRules.ResolveUniqueName("free.pdf", _ => false));

        // The GUID fallback fires when name-1 … name-999 are all taken.
        var exhausted = AttachmentRules.ResolveUniqueName(
            "a.pdf", _ => true);
        Assert.NotEqual("a.pdf", exhausted);
        Assert.EndsWith(".pdf", exhausted);
        Assert.Equal(32 + 4, exhausted.Length);   // 32 hex + ".pdf"
    }

    [Fact]
    public void FileSize_display_matches_the_wpf()
    {
        Assert.Equal("512 B", AttachmentRules.FormatFileSize(512));
        Assert.Equal("1.5 KB", AttachmentRules.FormatFileSize(1536));
        Assert.Equal("10.5 MB", AttachmentRules.FormatFileSize(10 * 1024 * 1024 + 512 * 1024));
        Assert.Equal("1 MB", AttachmentRules.FormatFileSize(1024 * 1024));
    }

    [Fact]
    public void Extension_sets_match_the_wpf()
    {
        Assert.Equal(
            new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" },
            AttachmentRules.ImageExtensions);
        Assert.Equal(new[] { ".pdf" }, AttachmentRules.PdfExtensions);
        Assert.Equal(10 * 1024 * 1024, AttachmentRules.MaxFileSizeBytes);
        Assert.True(AttachmentRules.IsValidExtension("photo.JPG"));   // case-insensitive
        Assert.False(AttachmentRules.IsValidExtension("photo.txt"));
        Assert.True(AttachmentRules.IsImage("p.webp"));
        Assert.False(AttachmentRules.IsImage("p.pdf"));
    }

    [Fact]
    public void Storage_keys_cannot_escape_the_root()
    {
        var storage = new FileAttachmentStorage(_root);
        Assert.False(storage.Exists("../../etc/passwd"));
        Assert.Null(storage.OpenReadAsync(
            $"../../{Guid.NewGuid()}", CancellationToken.None).Result);
        Assert.False(storage.Delete("../outside.txt"));
    }
}
