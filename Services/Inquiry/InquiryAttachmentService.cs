using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.DeliveryNote.Attachments;
using ProGlassAutomation.Services.Security;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Inquiry
{
    /// <summary>
    /// Disk + metadata service for Inquiry attachments.
    ///
    /// Storage layout (unchanged from the first release):
    ///   {AppDataRoot.CurrentFolder}/Inquiries/{InquiryId}/Attachments/{guid}_{fileName}
    ///
    /// Path resolution (A4 fix): rows created before the Demo/Production data-root
    /// switch store absolute paths captured at upload time. ResolvePath falls back
    /// to the well-known location under the CURRENT data root, so attachments keep
    /// opening after the root changed or a backup was restored elsewhere.
    /// </summary>
    public sealed class InquiryAttachmentService
    {
        private readonly IInquiryRepository _repository;
        public InquiryAttachmentService(IInquiryRepository repository) => _repository = repository;

        /// <summary>All accepted extensions (lowercase, with dot).</summary>
        public static readonly string[] AllowedExtensions =
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp",
            ".dwg", ".dxf", ".xlsx", ".xls", ".csv"
        };

        public static bool IsAllowedExtension(string extension) =>
            AllowedExtensions.Contains((extension ?? "").ToLowerInvariant());

        public async Task<InquiryAttachment> AddAsync(int inquiryId, string sourcePath, CancellationToken ct = default)
        {
            OperationGuard.Demand(AppPermissions.ProformaEdit, "attach an inquiry document");
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Attachment not found.", sourcePath);
            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (!IsAllowedExtension(extension))
                throw new InvalidOperationException(
                    $"Unsupported attachment type '{extension}'. Allowed: {string.Join(", ", AllowedExtensions)}.");
            var info = new FileInfo(sourcePath);
            if (info.Length > AttachmentLimits.MaxFileSizeBytes)
                throw new InvalidOperationException($"Attachment exceeds {AttachmentLimits.MaxFileSizeDisplay}.");
            string folder = StorageFolder(inquiryId);
            Directory.CreateDirectory(folder);
            string safeName = string.Concat(info.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string destination = Path.Combine(folder, Guid.NewGuid().ToString("N") + "_" + safeName);
            await using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            await using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                await source.CopyToAsync(target, ct).ConfigureAwait(false);
            try
            {
                var row = await _repository.AddAttachmentAsync(new InquiryAttachment
                {
                    InquiryId = inquiryId, FileName = info.Name, Extension = extension,
                    FileSize = info.Length, StoragePath = destination,
                    AttachmentType = extension.TrimStart('.').ToUpperInvariant(),
                    UploadedBy = AuditService.CurrentUsername, UploadedAt = DateTime.Now
                }, ct).ConfigureAwait(false);
                AuditService.Log(AuditSections.Proforma, "Inquiry attachment added", info.Name, "INQ", inquiryId.ToString());
                return row;
            }
            catch { try { File.Delete(destination); } catch { /* best effort */ } throw; }
        }

        public static string StorageFolder(int inquiryId) => Path.Combine(
            AppDataRoot.CurrentFolder, "Inquiries", inquiryId.ToString(), "Attachments");

        /// <summary>
        /// A4 fix: absolute path first, well-known current-root location second.
        /// Returns null when the file is gone from disk.
        /// </summary>
        public static string? ResolvePath(InquiryAttachment attachment)
        {
            if (attachment == null) return null;
            if (File.Exists(attachment.StoragePath)) return attachment.StoragePath;
            string fallback = Path.Combine(StorageFolder(attachment.InquiryId),
                Path.GetFileName(attachment.StoragePath));
            return File.Exists(fallback) ? fallback : null;
        }

        /// <summary>Opens with the registered system application. Never throws.</summary>
        public Result Open(InquiryAttachment attachment)
        {
            OperationGuard.Demand(AppPermissions.ProformaEdit, "open an inquiry attachment");
            string? path = ResolvePath(attachment);
            if (path == null)
                return Result.Fail(
                    $"'{attachment.FileName}' is not found on disk. It may have been moved or deleted outside ProGlass.",
                    CorrelationContext.NewId());
            return ProcessStartGuard.TryOpenFile(path, CorrelationContext.NewId());
        }

        public async Task<bool> DeleteAsync(InquiryAttachment attachment, CancellationToken ct = default)
        {
            OperationGuard.Demand(AppPermissions.ProformaEdit, "delete an inquiry attachment");
            bool removed = await _repository.DeleteAttachmentAsync(attachment.Id, ct).ConfigureAwait(false);
            if (removed)
            {
                // Delete every candidate location; failures are non-fatal (metadata
                // row is already gone — an orphan file is cleaned by nothing, but
                // must never break the user flow).
                try { if (File.Exists(attachment.StoragePath)) File.Delete(attachment.StoragePath); } catch { }
                try
                {
                    string fallback = Path.Combine(StorageFolder(attachment.InquiryId),
                        Path.GetFileName(attachment.StoragePath));
                    if (File.Exists(fallback)) File.Delete(fallback);
                }
                catch { }
                AuditService.Log(AuditSections.Proforma, "Inquiry attachment deleted",
                    attachment.FileName, "INQ", attachment.InquiryId.ToString());
            }
            return removed;
        }
    }
}
