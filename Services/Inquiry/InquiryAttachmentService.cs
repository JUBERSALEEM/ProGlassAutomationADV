using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.DeliveryNote.Attachments;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Inquiry
{
    /// <summary>
    /// Disk + manifest service for Inquiry attachments — aligned 1:1 with the
    /// proven Proforma Step 8 / Delivery Note attachment services.
    ///
    /// STORAGE LAYOUT (unchanged location, new metadata store):
    ///   {AppDataRoot.CurrentFolder}/Inquiries/{InquiryId}/Attachments/
    ///     manifest.json                ← List&lt;InquiryAttachment&gt; (source of truth)
    ///     {guid}_{fileName}            ← the files
    ///
    /// WHY A MANIFEST INSTEAD OF EF ROWS (the reported defect):
    ///   "Attachment failed: An error occurred while saving the entity
    ///    changes. See the inner exception for details."
    /// The previous implementation inserted an InquiryAttachments row via EF.
    /// On databases created by interim builds that insert can fail (drifted
    /// child-table shape, FK/NOT NULL constraints), taking the whole upload
    /// down with it. The PI module documented the answer long ago — see
    /// ProformaAttachmentRecord.cs: "The manifest makes attachments survive
    /// without any EF schema change." Attachment persistence now writes NO
    /// entity at all, so this failure class is structurally impossible.
    ///
    /// MIGRATION OF LEGACY DATA: the first Get/Add/Delete call for an inquiry
    /// backfills manifest.json from any legacy EF rows (best effort, files
    /// resolved through the current data root) and then never reads them
    /// again. The EF table is kept only as a best-effort mirror so the
    /// register's file-count column keeps working on healthy databases;
    /// mirror failures are logged and ignored.
    /// </summary>
    public sealed class InquiryAttachmentService
    {
        private readonly IInquiryRepository _repository;
        public InquiryAttachmentService(IInquiryRepository repository) => _repository = repository;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>All accepted extensions (lowercase, with dot).</summary>
        public static readonly string[] AllowedExtensions =
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp",
            ".dwg", ".dxf", ".xlsx", ".xls", ".csv"
        };

        public static bool IsAllowedExtension(string extension) =>
            AllowedExtensions.Contains((extension ?? "").ToLowerInvariant());

        public static string StorageFolder(int inquiryId) => Path.Combine(
            AppDataRoot.CurrentFolder, "Inquiries", inquiryId.ToString(), "Attachments");

        private static string ManifestPath(int inquiryId) =>
            Path.Combine(StorageFolder(inquiryId), "manifest.json");

        // ══════════════════════════════════════════════════════════════
        //  READ
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// The attachments for an inquiry, from the manifest. The first call
        /// for a legacy inquiry migrates any EF rows into the manifest.
        /// </summary>
        public async Task<IReadOnlyList<InquiryAttachment>> GetAsync(int inquiryId, CancellationToken ct = default)
        {
            if (!File.Exists(ManifestPath(inquiryId)))
            {
                var legacy = await TryLoadLegacyRowsAsync(inquiryId, ct).ConfigureAwait(false);
                if (legacy.Count > 0)
                {
                    await SaveManifestAsync(inquiryId, legacy).ConfigureAwait(false);
                    return legacy;
                }
            }
            return await LoadManifestAsync(inquiryId).ConfigureAwait(false);
        }

        /// <summary>Reads the legacy EF rows and resolves their files.</summary>
        private async Task<List<InquiryAttachment>> TryLoadLegacyRowsAsync(int inquiryId, CancellationToken ct)
        {
            try
            {
                var rows = await _repository.GetAttachmentsAsync(inquiryId, ct).ConfigureAwait(false);
                var result = new List<InquiryAttachment>();
                foreach (var row in rows)
                {
                    string? resolved = ResolvePath(row);
                    if (resolved == null) continue;   // file gone — row is unusable
                    row.StoragePath = resolved;
                    result.Add(row);
                }
                return result;
            }
            catch (Exception ex)
            {
                // A drifted/unreadable legacy table must never block the module.
                AppLogService.Debug($"[InquiryAttachments] Legacy row read skipped for inquiry {inquiryId}: " +
                    ExceptionMessages.Deep(ex));
                return new List<InquiryAttachment>();
            }
        }

        private static async Task<List<InquiryAttachment>> LoadManifestAsync(int inquiryId)
        {
            string path = ManifestPath(inquiryId);
            try
            {
                if (!File.Exists(path)) return new List<InquiryAttachment>();
                string json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json)) return new List<InquiryAttachment>();
                return JsonSerializer.Deserialize<List<InquiryAttachment>>(json, JsonOptions)
                    ?? new List<InquiryAttachment>();
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                AppLogService.Debug($"[InquiryAttachments] Corrupt manifest for inquiry {inquiryId}: {ex.Message}");
                return new List<InquiryAttachment>();
            }
        }

        private static async Task SaveManifestAsync(int inquiryId, List<InquiryAttachment> records)
        {
            string path = ManifestPath(inquiryId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string json = JsonSerializer.Serialize(records, JsonOptions);
            await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
        }

        // ══════════════════════════════════════════════════════════════
        //  ADD (upload)
        // ══════════════════════════════════════════════════════════════

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
                var record = new InquiryAttachment
                {
                    // Ticks: unique, sortable, no EF identity needed.
                    Id = DateTime.UtcNow.Ticks,
                    InquiryId = inquiryId, FileName = info.Name, Extension = extension,
                    FileSize = info.Length, StoragePath = destination,
                    AttachmentType = extension.TrimStart('.').ToUpperInvariant(),
                    UploadedBy = AuditService.CurrentUsername, UploadedAt = DateTime.Now
                };
                var manifest = await LoadManifestAsync(inquiryId).ConfigureAwait(false);
                manifest.Add(record);
                await SaveManifestAsync(inquiryId, manifest).ConfigureAwait(false);
                MirrorToDatabase(record);
                AuditService.Log(AuditSections.Proforma, "Inquiry attachment added", info.Name, "INQ", inquiryId.ToString());
                return record;
            }
            catch
            {
                // Never leave a copied file behind without metadata.
                try { File.Delete(destination); } catch { /* best effort */ }
                throw;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  DELETE / OPEN
        // ══════════════════════════════════════════════════════════════

        public async Task<bool> DeleteAsync(InquiryAttachment attachment, CancellationToken ct = default)
        {
            OperationGuard.Demand(AppPermissions.ProformaEdit, "delete an inquiry attachment");
            var manifest = await LoadManifestAsync(attachment.InquiryId).ConfigureAwait(false);
            var hit = manifest.FirstOrDefault(x => x.Id == attachment.Id) ?? attachment;
            manifest.Remove(hit);
            await SaveManifestAsync(attachment.InquiryId, manifest).ConfigureAwait(false);
            MirrorDelete(hit);
            // Delete every candidate location; failures are non-fatal.
            try { if (File.Exists(hit.StoragePath)) File.Delete(hit.StoragePath); } catch { }
            try
            {
                string fallback = Path.Combine(StorageFolder(hit.InquiryId), Path.GetFileName(hit.StoragePath));
                if (File.Exists(fallback)) File.Delete(fallback);
            }
            catch { }
            AuditService.Log(AuditSections.Proforma, "Inquiry attachment deleted",
                hit.FileName, "INQ", hit.InquiryId.ToString());
            return true;
        }

        /// <summary>
        /// Path resolution: absolute path first, then the inquiry's folder by
        /// file name (survives Demo/Production data-root switches and backup
        /// restores). Null when the file is gone from disk.
        /// </summary>
        public static string? ResolvePath(InquiryAttachment attachment)
        {
            if (attachment == null) return null;
            if (File.Exists(attachment.StoragePath)) return attachment.StoragePath;
            string name = Path.GetFileName(attachment.StoragePath);
            if (name.Length == 0) name = attachment.FileName;
            if (name.Length == 0) return null;
            string fallback = Path.Combine(StorageFolder(attachment.InquiryId), name);
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

        // ══════════════════════════════════════════════════════════════
        //  OPTIONAL EF MIRROR (register file-count column only)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Best-effort write into the legacy InquiryAttachments table so the
        /// register's "files" count keeps working on healthy databases. The
        /// manifest is the source of truth; a mirror failure (drifted legacy
        /// schema — the reported crash) is logged and ignored.
        /// </summary>
        private void MirrorToDatabase(InquiryAttachment record)
        {
            try
            {
                var mirror = new InquiryAttachment
                {
                    Id = record.Id, InquiryId = record.InquiryId, FileName = record.FileName,
                    Extension = record.Extension, FileSize = record.FileSize,
                    StoragePath = record.StoragePath, AttachmentType = record.AttachmentType,
                    UploadedBy = record.UploadedBy, UploadedAt = record.UploadedAt
                };
                AsyncUtility.SafeFireAndForget(
                    () => _repository.AddAttachmentAsync(mirror),
                    "Inquiry attachment count mirror");
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[InquiryAttachments] Count mirror skipped: " + ExceptionMessages.Deep(ex));
            }
        }

        private void MirrorDelete(InquiryAttachment record)
        {
            try
            {
                AsyncUtility.SafeFireAndForget(
                    () => _repository.DeleteAttachmentAsync(record.Id),
                    "Inquiry attachment count mirror delete");
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[InquiryAttachments] Mirror delete skipped: " + ExceptionMessages.Deep(ex));
            }
        }
    }
}
