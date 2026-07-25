using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.DeliveryNote.Attachments
{
    /// <summary>
    /// Disk I/O service for DN attachments.
    ///
    /// Files stored under:
    ///   %AppData%\ProGlassAutomation\DeliveryNotes\Attachments\
    ///     Images\{DeliveryNoteId}\{filename}
    ///     PDFs\{DeliveryNoteId}\{filename}
    ///
    /// BUG-26: SanitizeFolderName + SanitizeFileName were identical —
    ///         merged into one shared SanitizeName helper.
    /// BUG-27: DeleteFile now returns bool — caller knows if file was
    ///         actually deleted or was missing/locked.
    /// BUG-28: ResolveUniquePath is synchronous disk I/O — kept as-is
    ///         since it is called from async context, but loop limit
    ///         reduced to 1000 and GUID fallback is immediate safety net.
    ///
    /// Phase 10.6a: CopyImage/CopyPdf accept optional customFileName.
    /// </summary>
    public class AttachmentService
    {
        private readonly string _rootFolder;
        private readonly string _imagesFolder;
        private readonly string _pdfsFolder;

        public AttachmentService()
        {
            _rootFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "ProGlassAutomation",
                "DeliveryNotes",
                "Attachments");

            _imagesFolder = Path.Combine(_rootFolder, "Images");
            _pdfsFolder = Path.Combine(_rootFolder, "PDFs");

            Directory.CreateDirectory(_imagesFolder);
            Directory.CreateDirectory(_pdfsFolder);
        }

        // ═════════════════════════════════════════════════════════════════
        //  VALIDATION
        // ═════════════════════════════════════════════════════════════════

        public bool IsValidImageExtension(string sourcePath)
        {
            var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            return AttachmentLimits.ImageExtensions.Contains(ext);
        }

        public bool IsValidPdfExtension(string sourcePath)
        {
            var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            return AttachmentLimits.PdfExtensions.Contains(ext);
        }

        public bool IsUnderSizeLimit(string sourcePath)
        {
            var info = new FileInfo(sourcePath);
            return info.Length <= AttachmentLimits.MaxFileSizeBytes;
        }

        // ═════════════════════════════════════════════════════════════════
        //  COPY (upload)
        // ═════════════════════════════════════════════════════════════════

        public async Task<DeliveryNoteAttachment> CopyImageAsync(
            string sourcePath,
            string deliveryNoteId,
            string? customFileName = null)
        {
            return await CopyFileAsync(
                sourcePath, deliveryNoteId, customFileName,
                _imagesFolder, AttachmentType.Image);
        }

        public async Task<DeliveryNoteAttachment> CopyPdfAsync(
            string sourcePath,
            string deliveryNoteId,
            string? customFileName = null)
        {
            return await CopyFileAsync(
                sourcePath, deliveryNoteId, customFileName,
                _pdfsFolder, AttachmentType.Pdf);
        }

        private async Task<DeliveryNoteAttachment> CopyFileAsync(
            string sourcePath,
            string deliveryNoteId,
            string? customFileName,
            string typeFolder,
            string attachmentType)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException(
                    $"Source file not found: {sourcePath}");

            // BUG-26: use shared SanitizeName instead of duplicate methods
            string safeDnId = SanitizeName(deliveryNoteId);
            string destFolder = Path.Combine(typeFolder, safeDnId);
            Directory.CreateDirectory(destFolder);

            string finalName;
            if (!string.IsNullOrWhiteSpace(customFileName))
            {
                var customExt = Path.GetExtension(customFileName);
                if (string.IsNullOrWhiteSpace(customExt))
                {
                    var srcExt = Path.GetExtension(sourcePath);
                    finalName = customFileName + srcExt;
                }
                else
                {
                    finalName = customFileName;
                }
            }
            else
            {
                finalName = Path.GetFileName(sourcePath);
            }

            finalName = SanitizeName(finalName);  // BUG-26: shared helper

            string destPath = ResolveUniquePath(destFolder, finalName);

            using (var src = new FileStream(
                       sourcePath, FileMode.Open, FileAccess.Read,
                       FileShare.Read, 81920, useAsync: true))
            using (var dst = new FileStream(
                       destPath, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 81920, useAsync: true))
            {
                await src.CopyToAsync(dst);
            }

            var info = new FileInfo(destPath);

            return new DeliveryNoteAttachment
            {
                DeliveryNoteId = deliveryNoteId,
                AttachmentType = attachmentType,
                FileName = Path.GetFileName(destPath),
                FilePath = destPath,
                FileSize = info.Length,
                UploadedAt = DateTime.Now
            };
        }

        // BUG-28: reduced loop to 1_000, GUID fallback is immediate safety net
        private static string ResolveUniquePath(string folder, string fileName)
        {
            string candidate = Path.Combine(folder, fileName);
            if (!File.Exists(candidate)) return candidate;

            string nameOnly = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);

            for (int i = 1; i < 1_000; i++)
            {
                string next = Path.Combine(folder, $"{nameOnly}-{i}{ext}");
                if (!File.Exists(next)) return next;
            }

            // Immediate GUID fallback — avoids 10,000 disk I/O calls
            return Path.Combine(folder, $"{Guid.NewGuid():N}{ext}");
        }

        // BUG-26: single shared sanitize helper (was two identical methods)
        private static string SanitizeName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars);
        }

        // ═════════════════════════════════════════════════════════════════
        //  DELETE
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Deletes the file at the given path from disk.
        /// BUG-27: Now returns bool — true if deleted, false if missing or locked.
        /// DB row deletion is authoritative; this is best-effort file cleanup.
        /// </summary>
        public bool DeleteFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                File.Delete(filePath);
                return true;
            }
            catch (Exception)
            {
                // File locked or access denied — DB row still deleted.
                // Orphan file can be cleaned up manually if needed.
                return false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  OPEN IN DEFAULT APP
        // ═════════════════════════════════════════════════════════════════

        public void OpenInDefaultApp(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════

        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
            return $"{bytes / (1024.0 * 1024.0):0.##} MB";
        }
    }
}