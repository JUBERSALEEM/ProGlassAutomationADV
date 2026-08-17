// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/ProformaInvoice/Attachments/ProformaAttachmentService.cs
// PROJECT: ProGlassAutomation
// MODULE: Proforma Invoice — Step 8 Attachments
//
// Disk I/O service for proforma invoice attachments. Modelled 1:1 on
// the proven Delivery Note AttachmentService
// (Services/DeliveryNote/Attachments/AttachmentService.cs) so the two
// sections behave identically: same limits, same file-name handling,
// same copy/delete semantics.
//
// STORAGE LAYOUT
//   %AppData%\ProGlassAutomation\ProformaInvoices\Attachments\
//     {ProformaId}\
//       manifest.json      ← List<ProformaAttachmentRecord>
//       Images\{filename}
//       PDFs\{filename}
//
// The manifest makes attachments survive without any EF schema change
// (see ProformaAttachmentRecord.cs for the reasoning).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.Attachments;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.DeliveryNote.Attachments;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.ProformaInvoice.Attachments
{
    /// <summary>
    /// Disk service for proforma attachments (copy / delete / open /
    /// rename + manifest persistence). Thread-safe per-call; one
    /// instance per ViewModel is the intended lifetime.
    /// </summary>
    public class ProformaAttachmentService
    {
        private readonly string _rootFolder;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public ProformaAttachmentService()
        {
            _rootFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "ProGlassAutomation",
                "ProformaInvoices",
                "Attachments");

            Directory.CreateDirectory(_rootFolder);
        }

        // ══════════════════════════════════════════════════════════════
        //  VALIDATION (delegates to shared DN limits — single source)
        // ══════════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════════
        //  COPY (upload)
        // ══════════════════════════════════════════════════════════════

        /// <summary>Copies an image into the invoice folder + manifest.</summary>
        public async Task<ProformaAttachmentRecord> CopyImageAsync(
            string sourcePath, string proformaId)
        {
            return await CopyFileAsync(
                sourcePath, proformaId, "Images",
                AttachmentType.Image);
        }

        /// <summary>Copies a PDF into the invoice folder + manifest.</summary>
        public async Task<ProformaAttachmentRecord> CopyPdfAsync(
            string sourcePath, string proformaId)
        {
            return await CopyFileAsync(
                sourcePath, proformaId, "PDFs",
                AttachmentType.Pdf);
        }

        private async Task<ProformaAttachmentRecord> CopyFileAsync(
            string sourcePath,
            string proformaId,
            string typeFolderName,
            string attachmentType)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException(
                    $"Source file not found: {sourcePath}");

            string invoiceFolder = InvoiceFolder(proformaId);
            string destFolder = Path.Combine(invoiceFolder, typeFolderName);
            Directory.CreateDirectory(destFolder);

            string finalName = SanitizeName(Path.GetFileName(sourcePath));
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

            var record = new ProformaAttachmentRecord
            {
                ProformaId = proformaId,
                AttachmentType = attachmentType,
                FileName = Path.GetFileName(destPath),
                FilePath = destPath,
                FileSize = info.Length,
                UploadedAt = DateTime.Now
            };

            // Persist immediately so no upload is ever lost.
            var manifest = await LoadManifestAsync(proformaId);
            manifest.Add(record);
            await SaveManifestAsync(proformaId, manifest);

            return record;
        }

        // ══════════════════════════════════════════════════════════════
        //  DELETE / RENAME
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Removes the record from the manifest and deletes the file.
        /// File deletion is best-effort (same BUG-27 semantics as DN).
        /// </summary>
        public async Task<bool> DeleteAsync(
            string proformaId, string recordId)
        {
            // A-1: deleting an attachment destroys a document that may be
            // the only copy of a signed drawing or approval. Neither the
            // UI nor this service checked anything before now.
            OperationGuard.Demand(
                AppPermissions.ProformaEdit, "delete a proforma attachment");

            var manifest = await LoadManifestAsync(proformaId);
            var record = manifest.FirstOrDefault(r => r.Id == recordId);
            if (record == null) return false;

            manifest.Remove(record);
            await SaveManifestAsync(proformaId, manifest);
            return DeleteFile(record.FilePath);
        }

        /// <summary>Renames the file on disk + in the manifest.</summary>
        public async Task<bool> RenameAsync(
            string proformaId, string recordId, string newName)
        {
            // A-1: renaming moves the file on disk, so a failure or a
            // hostile name change is a mutation, not a display tweak.
            OperationGuard.Demand(
                AppPermissions.ProformaEdit, "rename a proforma attachment");

            var manifest = await LoadManifestAsync(proformaId);
            var record = manifest.FirstOrDefault(r => r.Id == recordId);
            if (record == null) return false;

            string ext = Path.GetExtension(record.FileName);
            string candidate = SanitizeName(newName);

            if (!Path.HasExtension(candidate))
                candidate += ext;

            string folder = Path.GetDirectoryName(record.FilePath)!;
            string newPath = ResolveUniquePath(folder, candidate);

            try
            {
                File.Move(record.FilePath, newPath);
            }
            catch
            {
                return false;   // target locked / access denied
            }

            record.FilePath = newPath;
            record.FileName = Path.GetFileName(newPath);
            await SaveManifestAsync(proformaId, manifest);
            return true;
        }

        // ══════════════════════════════════════════════════════════════
        //  MANIFEST
        // ══════════════════════════════════════════════════════════════

        /// <summary>Loads the invoice's attachment list (empty if none).</summary>
        public async Task<List<ProformaAttachmentRecord>> LoadManifestAsync(
            string proformaId)
        {
            string path = ManifestPath(proformaId);

            try
            {
                if (!File.Exists(path))
                    return new List<ProformaAttachmentRecord>();

                string json = await File.ReadAllTextAsync(path);

                if (string.IsNullOrWhiteSpace(json))
                    return new List<ProformaAttachmentRecord>();

                return JsonSerializer
                    .Deserialize<List<ProformaAttachmentRecord>>(
                        json, _jsonOptions)
                    ?? new List<ProformaAttachmentRecord>();
            }
            catch (JsonException ex)
            {
                Debug.WriteLine(
                    $"[ProformaAttachments] Corrupt manifest for " +
                    $"{proformaId}: {ex.Message}");
                return new List<ProformaAttachmentRecord>();
            }
        }

        private async Task SaveManifestAsync(
            string proformaId, List<ProformaAttachmentRecord> records)
        {
            string path = ManifestPath(proformaId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            string json = JsonSerializer.Serialize(records, _jsonOptions);
            await File.WriteAllTextAsync(path, json);
        }

        // ══════════════════════════════════════════════════════════════
        //  OPEN IN DEFAULT APP
        // ══════════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════════
        //  PATH HELPERS
        // ══════════════════════════════════════════════════════════════

        private string InvoiceFolder(string proformaId) =>
            Path.Combine(_rootFolder, SanitizeName(proformaId));

        private string ManifestPath(string proformaId) =>
            Path.Combine(InvoiceFolder(proformaId), "manifest.json");

        /// <summary>Shared file-name sanitizer (same rule as DN service).</summary>
        private static string SanitizeName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars);
        }

        /// <summary>Collision-safe path with a fast GUID fallback.</summary>
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

            return Path.Combine(folder, $"{Guid.NewGuid():N}{ext}");
        }

        /// <summary>Best-effort file delete (locked files are left behind).</summary>
        public bool DeleteFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                File.Delete(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Human-friendly file size (shared with DN display).</summary>
        public static string FormatFileSize(long bytes) =>
            AttachmentService.FormatFileSize(bytes);
    }
}
