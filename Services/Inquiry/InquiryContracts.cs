using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Reporting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Inquiry
{
    /// <summary>
    /// Filter definition for the Inquiry register. Values map 1:1 to the
    /// segmented tabs / command-bar controls of InquiryListView.
    /// </summary>
    public sealed class InquiryQuery
    {
        public string Search { get; init; } = "";
        /// <summary>"All statuses" disables the status predicate.</summary>
        public string Status { get; init; } = "All statuses";
        public int? SalesmanId { get; init; }
        public string SalesmanName { get; init; } = "";
        public int? PreparedByUserId { get; init; }
        public string PreparedByName { get; init; } = "";
        public DateTime? From { get; init; }
        public DateTime? ToExclusive { get; init; }
        public bool HasAttachmentsOnly { get; init; }
        public string Priority { get; init; } = "";
        public string Sort { get; init; } = "Updated newest";
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 50;
    }

    /// <summary>
    /// Database-side projection for register rows. No file bytes, no tracking.
    /// </summary>
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
        public decimal InquiryValue { get; init; }
        public string Notes { get; init; } = "";
        public DateTime ReceivedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
        public string UpdatedBy { get; init; } = "";
        public int AttachmentCount { get; init; }
        public string AttachmentBadgeText { get; set; } = "";
        public int? ProformaInvoiceId { get; init; }
        public string ProformaNumber { get; init; } = "";
        public string JobOrderNumber { get; init; } = "";

        // ── Full-lifecycle projection (hydrated by IInquiryLifecycleService) ──
        // Defaults represent "not started" so a row that fails the lifecycle
        // hydration still renders correctly. The service overwrites these
        // before the page is returned to the UI.
        public InquiryLifecycleSnapshot Lifecycle { get; set; } =
            new InquiryLifecycleSnapshot();
    }

    /// <summary>
    /// Live counts for the segmented status tabs. Statistics ignore the status
    /// filter (so tab counts stay stable while a tab is active) but honour every
    /// other filter.
    /// </summary>
    public sealed class InquiryStatistics
    {
        public int All { get; init; }
        public int Received { get; init; }
        public int Prepared { get; init; }
        public int Sent { get; init; }
        public int Confirmed { get; init; }
        public int Hold { get; init; }
        public int Cancelled { get; init; }
        /// <summary>Sum of inquiry values, cancelled rows excluded.</summary>
        public decimal OpenPipelineValue { get; init; }
    }

    public interface IInquiryRepository
    {
        Task InitializeAsync(CancellationToken ct = default);
        Task<PagedResult<InquiryListProjection>> QueryAsync(InquiryQuery query, CancellationToken ct = default);
        Task<InquiryStatistics> GetStatisticsAsync(InquiryQuery query, CancellationToken ct = default);
        Task<InquiryRecord?> GetDetailAsync(int id, CancellationToken ct = default);
        Task<InquiryRecord> CreateAsync(InquiryRecord inquiry, CancellationToken ct = default);
        Task<InquiryRecord> UpdateAsync(InquiryRecord inquiry, CancellationToken ct = default);
        Task<bool> ChangeStatusAsync(int id, string status, string actor, CancellationToken ct = default);
        Task<IReadOnlyList<InquiryAttachment>> GetAttachmentsAsync(int inquiryId, CancellationToken ct = default);
        Task<InquiryAttachment> AddAttachmentAsync(InquiryAttachment attachment, CancellationToken ct = default);
        Task<bool> DeleteAttachmentAsync(long attachmentId, CancellationToken ct = default);
        Task<IReadOnlyList<InquiryCommunication>> GetCommunicationsAsync(int inquiryId, CancellationToken ct = default);
        /// <summary>Drop the lifecycle cache (call after a downstream edit).</summary>
        void InvalidateLifecycle(int inquiryId);
    }
}
