// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 24:
//   Services/DeliveryNote/Attachments/AttachmentType.cs (AttachmentLimits)
//   Services/DeliveryNote/Attachments/AttachmentService.cs (SanitizeName
//   S-8, ResolveUniquePath BUG-28, FormatFileSize)
// The WPF file is the source of truth; this port changes no behaviour.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Saas.Domain.Attachments;

/// <summary>Attachment limits and name rules (WPF AttachmentLimits).</summary>
public static class AttachmentRules
{
    /// <summary>10 MB per file.</summary>
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public const string MaxFileSizeDisplay = "10 MB";

    /// <summary>Accepted image extensions (lowercase, with dot).</summary>
    public static readonly string[] ImageExtensions =
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
    };

    /// <summary>Accepted PDF extensions.</summary>
    public static readonly string[] PdfExtensions = { ".pdf" };

    /// <summary>All accepted extensions (the WPF combined dialog filter).</summary>
    public static readonly string[] AllExtensions =
        ImageExtensions.Concat(PdfExtensions).ToArray();

    public static bool IsValidExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        return ImageExtensions.Contains(ext) || PdfExtensions.Contains(ext);
    }

    public static bool IsImage(string fileName)
    {
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        return ImageExtensions.Contains(ext);
    }

    /// <summary>
    /// S-8 hardened name sanitizer (WPF SanitizeName, verbatim):
    /// blank → "_"; invalid chars and directory separators → '_'; a
    /// segment that is entirely dots (path traversal) → "_"; trailing
    /// dots/spaces trimmed (Windows would silently drop them and collide
    /// two distinct ids onto one folder); capped at 120 chars.
    /// </summary>
    public static string SanitizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "_";

        var invalid = Path.GetInvalidFileNameChars();
        var chars = name
            .Select(c => invalid.Contains(c)
                         || c == Path.DirectorySeparatorChar
                         || c == Path.AltDirectorySeparatorChar
                    ? '_'
                    : c)
            .ToArray();

        string safe = new string(chars).Trim();

        // "." and ".." are valid filenames to the char filter but are
        // directory navigation to a path join. Anything that is only
        // dots is replaced outright rather than trimmed, so "..." cannot
        // degrade into "." either.
        if (safe.Length == 0 || safe.All(c => c == '.'))
            return "_";

        safe = safe.TrimEnd('.', ' ');
        if (safe.Length == 0) return "_";

        const int MaxSegment = 120;
        return safe.Length <= MaxSegment ? safe : safe[..MaxSegment];
    }

    /// <summary>
    /// Unique-name resolution (WPF verbatim: the plain name first, then
    /// name-1 … name-999, then an immediate GUID fallback — BUG-28: no
    /// silent overwrite, no unbounded disk I/O). Returns a file name,
    /// NOT a path.
    /// </summary>
    public static string ResolveUniqueName(string fileName, Func<string, bool> exists)
    {
        if (!exists(fileName))
            return fileName;

        var nameOnly = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (int i = 1; i < 1_000; i++)
        {
            var next = $"{nameOnly}-{i}{ext}";
            if (!exists(next))
                return next;
        }

        return Guid.NewGuid().ToString("N") + ext;
    }

    /// <summary>WPF FormatFileSize.</summary>
    public static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.##} MB"
    };
}
