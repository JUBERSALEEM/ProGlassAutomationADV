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
    }

    public sealed class InquiryWorkflowService : IInquiryWorkflowService
    {
        private readonly IInquiryRepository _repository;
        public InquiryWorkflowService(IInquiryRepository repository) => _repository = repository;

        public async Task<InquiryRecord> CreateAsync(InquiryRecord inquiry, CancellationToken ct = default)
        {
            OperationGuard.Demand(AppPermissions.ProformaCreate, "create an inquiry");
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
            OperationGuard.Demand(AppPermissions.ProformaEdit, "edit an inquiry");
            Validate(inquiry); inquiry.UpdatedBy = AuditService.CurrentUsername;
            var saved = await _repository.UpdateAsync(inquiry, ct).ConfigureAwait(false);
            AuditService.Log(AuditSections.Proforma, "Inquiry updated", saved.InquiryNumber, "INQ", saved.InquiryNumber);
            await VersionHistoryService.AddVersionAsync("INQ", saved.InquiryNumber, "Inquiry updated", Snapshot(saved));
            return saved;
        }

        public async Task<bool> ChangeStatusAsync(int id, string current, string next, CancellationToken ct = default)
        {
            OperationGuard.Demand(AppPermissions.ProformaEdit, "change inquiry status");
            next = InquiryStatuses.Normalize(next);
            if (!Allowed(current, next)) throw new InvalidOperationException($"Inquiry cannot move from {current} to {next}.");
            bool changed = await _repository.ChangeStatusAsync(id, next, AuditService.CurrentUsername, ct).ConfigureAwait(false);
            if (changed) AuditService.Log(AuditSections.Proforma, "Inquiry status changed", $"{current} → {next}", "INQ", id.ToString());
            return changed;
        }

        private static bool Allowed(string current, string next) => InquiryStatuses.Normalize(current) switch
        {
            InquiryStatuses.Received => next is InquiryStatuses.Prepared or InquiryStatuses.Hold or InquiryStatuses.Cancelled,
            InquiryStatuses.Prepared => next is InquiryStatuses.Sent or InquiryStatuses.Hold or InquiryStatuses.Cancelled,
            InquiryStatuses.Sent => next is InquiryStatuses.Confirmed or InquiryStatuses.Hold or InquiryStatuses.Cancelled,
            InquiryStatuses.Hold => next is InquiryStatuses.Prepared or InquiryStatuses.Cancelled,
            InquiryStatuses.Confirmed => next == InquiryStatuses.Cancelled,
            _ => false
        };

        private static void Validate(InquiryRecord x)
        {
            if (string.IsNullOrWhiteSpace(x.CustomerName)) throw new InvalidOperationException("Customer is required.");
            if (string.IsNullOrWhiteSpace(x.ProjectName)) throw new InvalidOperationException("Project is required.");
            if (x.ReceivedAt == default) x.ReceivedAt = DateTimeOffset.Now;
        }
        private static string Snapshot(InquiryRecord x) => JsonSerializer.Serialize(new
        {
            x.Id, x.InquiryNumber, x.CustomerId, x.CustomerName, x.ProjectId, x.ProjectName,
            x.SalesmanId, x.SalesmanName, x.PreparedByUserId, x.PreparedByName,
            x.Status, x.Priority, x.InquiryValue, x.ReceivedAt, x.UpdatedAt, x.UpdatedBy
        });
    }
}
