using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Security;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Inquiry
{
    public interface IInquiryWorkflowService
    {
        Task<InquiryRecord> CreateAsync(InquiryRecord inquiry, CancellationToken ct = default);
        Task<InquiryRecord> UpdateAsync(InquiryRecord inquiry, CancellationToken ct = default);
        Task<bool> ChangeStatusAsync(int id, string current, string next, CancellationToken ct = default);

        /// <summary>
        /// Clone an existing inquiry as a brand new draft. The clone
        /// gets a fresh number and Status=Received; the source is
        /// unchanged. Requires <see cref="AppPermissions.InquiryCreate"/>.
        /// </summary>
        Task<InquiryRecord> DuplicateAsync(int sourceId, CancellationToken ct = default);

        /// <summary>
        /// Permanently delete an inquiry that has no downstream
        /// documents. Refuses to delete when a PI / JO / DN / TI /
        /// Statement exists; the caller should cancel the inquiry
        /// instead. Requires <see cref="AppPermissions.InquiryDelete"/>.
        /// </summary>
        Task<DeleteInquiryResult> DeleteAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Update the priority field only. Useful for the right-click
        /// "Change priority" sub-menu and the bulk-priority toolbar.
        /// Requires <see cref="AppPermissions.InquiryEdit"/>.
        /// </summary>
        Task<bool> SetPriorityAsync(int id, string priority, CancellationToken ct = default);
    }

    public sealed class InquiryWorkflowService : IInquiryWorkflowService
    {
        private readonly IInquiryRepository _repository;
        public InquiryWorkflowService(IInquiryRepository repository) => _repository = repository;

        public async Task<InquiryRecord> CreateAsync(InquiryRecord inquiry, CancellationToken ct = default)
        {
            // Migrated from the legacy ProformaCreate permission when the
            // dedicated Inquiry.* permission set landed. The legacy
            // permission is still recognised so old role configurations
            // keep working.
            OperationGuard.DemandAny(
                new[] { AppPermissions.InquiryCreate, AppPermissions.ProformaCreate },
                "create an inquiry");
            Validate(inquiry);
            inquiry.PreparedByName = string.IsNullOrWhiteSpace(inquiry.PreparedByName)
                ? AuditService.CurrentUser?.DisplayName ?? AuditService.CurrentUsername : inquiry.PreparedByName;
            inquiry.PreparedByUserId ??= AuditService.CurrentUser?.Id;
            inquiry.PreparedByRole = string.IsNullOrWhiteSpace(inquiry.PreparedByRole)
                ? AuditService.CurrentUser?.Role ?? "" : inquiry.PreparedByRole;
            inquiry.UpdatedBy = AuditService.CurrentUsername;
            var saved = await _repository.CreateAsync(inquiry, ct).ConfigureAwait(false);
            AuditService.Log(AuditSections.Proforma, "Inquiry created", saved.InquiryNumber, "INQ", saved.InquiryNumber);
            await VersionHistoryService.AddVersionAsync("INQ", saved.InquiryNumber, "Inquiry created", Snapshot(saved));
            return saved;
        }

        public async Task<InquiryRecord> UpdateAsync(InquiryRecord inquiry, CancellationToken ct = default)
        {
            OperationGuard.DemandAny(
                new[] { AppPermissions.InquiryEdit, AppPermissions.ProformaEdit },
                "edit an inquiry");
            Validate(inquiry); inquiry.UpdatedBy = AuditService.CurrentUsername;
            var saved = await _repository.UpdateAsync(inquiry, ct).ConfigureAwait(false);
            AuditService.Log(AuditSections.Proforma, "Inquiry updated", saved.InquiryNumber, "INQ", saved.InquiryNumber);
            await VersionHistoryService.AddVersionAsync("INQ", saved.InquiryNumber, "Inquiry updated", Snapshot(saved));
            return saved;
        }

        public async Task<bool> ChangeStatusAsync(int id, string current, string next, CancellationToken ct = default)
        {
            OperationGuard.DemandAny(
                new[] { AppPermissions.InquiryEdit, AppPermissions.ProformaEdit },
                "change inquiry status");
            next = InquiryStatuses.Normalize(next);
            if (!CanTransition(current, next)) throw new InvalidOperationException($"Inquiry cannot move from {current} to {next}.");
            bool changed = await _repository.ChangeStatusAsync(id, next, AuditService.CurrentUsername, ct).ConfigureAwait(false);
            if (changed) AuditService.Log(AuditSections.Proforma, "Inquiry status changed", $"{current} → {next}", "INQ", id.ToString());
            return changed;
        }

        public async Task<InquiryRecord> DuplicateAsync(int sourceId, CancellationToken ct = default)
        {
            OperationGuard.DemandAny(
                new[] { AppPermissions.InquiryCreate, AppPermissions.ProformaCreate },
                "duplicate an inquiry");
            var clone = await _repository.DuplicateAsync(sourceId, ct).ConfigureAwait(false);
            AuditService.Log(AuditSections.Proforma, "Inquiry duplicated",
                $"Cloned from #{sourceId} → {clone.InquiryNumber}",
                "INQ", clone.InquiryNumber);
            await VersionHistoryService.AddVersionAsync("INQ", clone.InquiryNumber, "Inquiry duplicated", Snapshot(clone));
            return clone;
        }

        public async Task<DeleteInquiryResult> DeleteAsync(int id, CancellationToken ct = default)
        {
            OperationGuard.DemandAny(
                new[] { AppPermissions.InquiryDelete, AppPermissions.ProformaDelete },
                "delete an inquiry");
            var detail = await _repository.GetDetailAsync(id, ct).ConfigureAwait(false);
            string label = detail?.InquiryNumber ?? id.ToString();
            var result = await _repository.DeleteAsync(id, ct).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                AuditService.Log(AuditSections.Proforma, "Inquiry deleted", label, "INQ", label);
            }
            return result;
        }

        public async Task<bool> SetPriorityAsync(int id, string priority, CancellationToken ct = default)
        {
            OperationGuard.DemandAny(
                new[] { AppPermissions.InquiryEdit, AppPermissions.ProformaEdit },
                "change inquiry priority");
            var detail = await _repository.GetDetailAsync(id, ct).ConfigureAwait(false);
            if (detail == null) return false;
            string normalized = NormalizePriority(priority);
            if (string.Equals(detail.Priority, normalized, StringComparison.OrdinalIgnoreCase))
                return true; // idempotent no-op
            detail.Priority = normalized;
            detail.UpdatedBy = AuditService.CurrentUsername;
            await _repository.UpdateAsync(detail, ct).ConfigureAwait(false);
            AuditService.Log(AuditSections.Proforma, "Inquiry priority changed",
                $"{detail.InquiryNumber} → {normalized}", "INQ", detail.InquiryNumber);
            return true;
        }

        public static bool CanTransition(string current, string next) => InquiryStatuses.Normalize(current) switch
        {
            InquiryStatuses.Received => next is InquiryStatuses.Prepared or InquiryStatuses.Hold or InquiryStatuses.Cancelled,
            InquiryStatuses.Prepared => next is InquiryStatuses.Sent or InquiryStatuses.Hold or InquiryStatuses.Cancelled,
            InquiryStatuses.Sent => next is InquiryStatuses.Confirmed or InquiryStatuses.Hold or InquiryStatuses.Cancelled,
            InquiryStatuses.Hold => next is InquiryStatuses.Prepared or InquiryStatuses.Cancelled,
            InquiryStatuses.Confirmed => next == InquiryStatuses.Cancelled,
            _ => false
        };

        private static string NormalizePriority(string? value)
        {
            string t = (value ?? "").Trim();
            if (t.Length == 0) return "Normal";
            // Allow common short forms: "U"/"H"/"N"/"L".
            return t.ToLowerInvariant() switch
            {
                "urgent" or "u" or "critical" => "Urgent",
                "high" or "h" => "High",
                "low" or "l" => "Low",
                _ => "Normal"
            };
        }

        private static void Validate(InquiryRecord x)
        {
            if (string.IsNullOrWhiteSpace(x.CustomerName)) throw new InvalidOperationException("Customer is required.");
            if (string.IsNullOrWhiteSpace(x.ProjectName)) throw new InvalidOperationException("Project is required.");
            if (x.ReceivedAt == default) x.ReceivedAt = DateTime.Now;
        }
        private static string Snapshot(InquiryRecord x) => JsonSerializer.Serialize(new
        {
            x.Id, x.InquiryNumber, x.CustomerId, x.CustomerName, x.ProjectId, x.ProjectName,
            x.SalesmanId, x.SalesmanName, x.PreparedByUserId, x.PreparedByName,
            x.Status, x.Priority, x.InquiryValue, x.ReceivedAt, x.UpdatedAt, x.UpdatedBy
        });
    }
}
