using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Reporting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Inquiry
{
    public sealed class InquiryQuery
    {
        public string Search { get; init; } = "";
        public string Status { get; init; } = "All statuses";
        public int? SalesmanId { get; init; }
        public int? PreparedByUserId { get; init; }
        public DateTimeOffset? From { get; init; }
        public DateTimeOffset? ToExclusive { get; init; }
        public bool HasAttachmentsOnly { get; init; }
        public string Sort { get; init; } = "Updated newest";
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 50;
    }

    public sealed class InquiryListProjection
    {
        public int Id { get; init; }
        public string InquiryNumber { get; init; } = "";
        public string InquiryReference { get; init; } = "";
        public string CustomerName { get; init; } = "";
        public string CustomerLocation { get; init; } = "";
        public string ProjectName { get; init; } = "";
        public string ProjectDescription { get; init; } = "";
        public string SalesmanName { get; init; } = "";
        public string PreparedByName { get; init; } = "";
        public string PreparedByRole { get; init; } = "";
        public string Status { get; init; } = "";
        public string Priority { get; init; } = "";
        public DateTimeOffset ReceivedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
        public string UpdatedBy { get; init; } = "";
        public int AttachmentCount { get; init; }
        public int? ProformaInvoiceId { get; init; }
        public string ProformaNumber { get; init; } = "";
        public string JobOrderNumber { get; init; } = "";
    }

    public sealed class InquiryStatistics
    {
        public int All { get; init; }
        public int Received { get; init; }
        public int Sent { get; init; }
        public int Confirmed { get; init; }
        public int Hold { get; init; }
        public int Cancelled { get; init; }
    }

    public interface IInquiryRepository
    {
        Task InitializeAsync(CancellationToken ct = default);
        Task<PagedResult<InquiryListProjection>> QueryAsync(InquiryQuery query, CancellationToken ct = default);
        Task<InquiryStatistics> GetStatisticsAsync(InquiryQuery query, CancellationToken ct = default);
        Task<InquiryRecord?> GetDetailAsync(int id, CancellationToken ct = default);
        Task<IReadOnlyList<InquiryAttachment>> GetAttachmentsAsync(int inquiryId, CancellationToken ct = default);
        Task<IReadOnlyList<InquiryCommunication>> GetCommunicationsAsync(int inquiryId, CancellationToken ct = default);
    }
}
