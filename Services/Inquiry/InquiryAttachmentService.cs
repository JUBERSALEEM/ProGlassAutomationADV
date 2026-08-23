using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.DeliveryNote.Attachments;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Common;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Inquiry
{
    public sealed class InquiryAttachmentService
    {
        private readonly IInquiryRepository _repository;
        public InquiryAttachmentService(IInquiryRepository repository) => _repository = repository;

        public async Task<InquiryAttachment> AddAsync(int inquiryId, string sourcePath, CancellationToken ct = default)
        {
            OperationGuard.Demand(AppPermissions.ProformaEdit, "attach an inquiry document");
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Attachment not found.", sourcePath);
            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (!AttachmentLimits.ImageExtensions.Contains(extension) && !AttachmentLimits.PdfExtensions.Contains(extension)
                && extension is not ".dwg" and not ".dxf" and not ".xlsx" and not ".xls" and not ".csv")
                throw new InvalidOperationException("Unsupported attachment type.");
            var info = new FileInfo(sourcePath);
            if (info.Length > AttachmentLimits.MaxFileSizeBytes) throw new InvalidOperationException($"Attachment exceeds {AttachmentLimits.MaxFileSizeDisplay}.");
            string folder = Path.Combine(AppDataRoot.CurrentFolder, "Inquiries", inquiryId.ToString(), "Attachments");
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
            catch { try { File.Delete(destination); } catch { } throw; }
        }

        public Result Open(InquiryAttachment attachment) =>
            ProcessStartGuard.TryOpenFile(attachment.StoragePath, CorrelationContext.NewId());

        public async Task<bool> DeleteAsync(InquiryAttachment attachment, CancellationToken ct = default)
        {
            OperationGuard.Demand(AppPermissions.ProformaEdit, "delete an inquiry attachment");
            bool removed = await _repository.DeleteAttachmentAsync(attachment.Id, ct).ConfigureAwait(false);
            if (removed) { try { File.Delete(attachment.StoragePath); } catch { } }
            return removed;
        }
    }
}
