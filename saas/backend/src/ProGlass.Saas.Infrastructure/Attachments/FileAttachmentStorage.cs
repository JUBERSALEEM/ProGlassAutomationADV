// ═══════════════════════════════════════════════════════════════════════
//  Phase 24 — filesystem byte storage behind the attachments table.
//  Root: SAAS_ATTACHMENT_ROOT (absolute) or "./attachments" under the
//  process working directory. Storage keys are relative paths of
//  pre-sanitized segments (tenant/entity/entity-id), so no key can walk
//  out of the root.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Application.Attachments;
using ProGlass.Saas.Domain.Attachments;

namespace ProGlass.Saas.Infrastructure.Attachments;

public sealed class FileAttachmentStorage : IAttachmentStorage
{
    private readonly string _root;

    public FileAttachmentStorage(string? root = null)
    {
        _root = root
                ?? Environment.GetEnvironmentVariable("SAAS_ATTACHMENT_ROOT")
                ?? Path.Combine(Directory.GetCurrentDirectory(), "attachments");
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(
        string baseKey, string fileName, Stream content, CancellationToken ct)
    {
        var folder = SafeFullPath(baseKey);
        Directory.CreateDirectory(folder);

        // BUG-28: 1,000 tries then a GUID name — never an overwrite,
        // never an infinite loop.
        var finalName = AttachmentRules.ResolveUniqueName(
            fileName, candidate => File.Exists(Path.Combine(folder, candidate)));

        var destPath = Path.Combine(folder, finalName);
        await using var dst = new FileStream(
            destPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, useAsync: true);
        await content.CopyToAsync(dst, ct).ConfigureAwait(false);

        return $"{baseKey}/{finalName}";
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        if (!TrySafeFullPath(storageKey, out var path) || !File.Exists(path))
            return Task.FromResult<Stream?>(null);

        try
        {
            return Task.FromResult<Stream?>(new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, useAsync: true));
        }
        catch (IOException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    /// <summary>BUG-27: true if deleted, false if missing or locked —
    /// best-effort, never throws.</summary>
    public bool Delete(string storageKey)
    {
        if (!TrySafeFullPath(storageKey, out var path) || !File.Exists(path))
            return false;
        try
        {
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public bool Exists(string storageKey) =>
        TrySafeFullPath(storageKey, out var path) && File.Exists(path);

    // ── path safety ───────────────────────────────────────────────────

    private string SafeFullPath(string baseKey)
    {
        var path = Path.GetFullPath(Path.Combine(_root, baseKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(Path.GetFullPath(_root) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Attachment storage key escaped the root.");
        return path;
    }

    private bool TrySafeFullPath(string storageKey, out string path)
    {
        path = "";
        if (string.IsNullOrWhiteSpace(storageKey)) return false;
        try
        {
            path = SafeFullPath(storageKey);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
