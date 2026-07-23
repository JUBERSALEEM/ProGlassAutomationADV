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
    /// Phase 10.6a: CopyImage/CopyPdf accept an optional customFileName
    /// so the user can rename the file on upload. Extension is preserved
    /// from the source path if the custom name is missing an extension.
    /// Name collisions handled by auto-suffixing "-1", "-2", etc.
    /// </summary>
    public class AttachmentService
    {
        private readonly string _rootFolder;
        private readonly string _imagesFolder;
        private readonly string _pdfsFolder;

        public AttachmentService()
        {
            _rootFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
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
        //  COPY (upload) — copies from source into managed storage
        //  customFileName is optional; if null/empty, source filename used.
        //  Returns the DB entity ready to persist (Id not yet set).
        // ═════════════════════════════════════════════════════════════════

        public async Task<DeliveryNoteAttachment> CopyImageAsync(
            string sourcePath, string deliveryNoteId, string? customFileName = null)
        {
            return await CopyFileAsync(
                sourcePath, deliveryNoteId, customFileName,
                _imagesFolder, AttachmentType.Image);
        }

        public async Task<DeliveryNoteAttachment> CopyPdfAsync(
            string sourcePath, string deliveryNoteId, string? customFileName = null)
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
                throw new FileNotFoundException($"Source file not found: {sourcePath}");

            string safeDnId = SanitizeFolderName(deliveryNoteId);
            string destFolder = Path.Combine(typeFolder, safeDnId);
            Directory.CreateDirectory(destFolder);

            // Determine final filename:
            //  1. If customFileName given → use it (defensively add source's
            //     extension if custom name is missing one)
            //  2. Otherwise → use original filename from source path
            string finalName;
            if (!string.IsNullOrWhiteSpace(customFileName))
            {
                var customExt = Path.GetExtension(customFileName);
                if (string.IsNullOrWhiteSpace(customExt))
                {
                    // Custom name missing extension — append source's extension
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

            // Sanitize defensively (user input already validated in dialog,
            // but we protect against direct API misuse too)
            finalName = SanitizeFileName(finalName);

            string destPath = ResolveUniquePath(destFolder, finalName);

            using (var src = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            using (var dst = new FileStream(destPath, FileMode.CreateNew, FileAccess.Write))
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

        private static string ResolveUniquePath(string folder, string fileName)
        {
            string candidate = Path.Combine(folder, fileName);
            if (!File.Exists(candidate)) return candidate;

            string nameOnly = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);

            for (int i = 1; i < 10_000; i++)
            {
                string next = Path.Combine(folder, $"{nameOnly}-{i}{ext}");
                if (!File.Exists(next)) return next;
            }

            return Path.Combine(folder, $"{Guid.NewGuid():N}{ext}");
        }

        private static string SanitizeFolderName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars);
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars);
        }

        // ═════════════════════════════════════════════════════════════════
        //  DELETE
        // ═════════════════════════════════════════════════════════════════

        public void DeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch (Exception)
            {
                // Swallow — DB row deletion is authoritative.
                // Orphan files can be cleaned up later if needed.
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