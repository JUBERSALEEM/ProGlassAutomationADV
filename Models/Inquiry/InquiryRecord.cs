using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.Inquiry
{
    public static class InquiryStatuses
    {
        public const string Received = "Received";
        public const string Prepared = "Prepared";
        public const string Sent = "Sent";
        public const string Confirmed = "Confirmed";
        public const string Hold = "Hold";
        public const string Cancelled = "Cancelled";
        public static readonly IReadOnlyList<string> All =
            [Received, Prepared, Sent, Confirmed, Hold, Cancelled];
        public static string Normalize(string? value)
        {
            string text = value?.Trim() ?? "";
            if (text.Equals("On Hold", StringComparison.OrdinalIgnoreCase)) return Hold;
            if (text.Equals("Canceled", StringComparison.OrdinalIgnoreCase)) return Cancelled;
            return All.FirstOrDefault(x => string.Equals(x, text, StringComparison.OrdinalIgnoreCase))
                ?? Received;
        }
    }

    [Table("Inquiries")]
    public sealed class InquiryRecord
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(50)] public string InquiryNumber { get; set; } = "";
        [MaxLength(100)] public string InquiryReference { get; set; } = "Customer Inquiry";
        public int? CustomerId { get; set; }
        [MaxLength(200)] public string CustomerName { get; set; } = "";
        [MaxLength(250)] public string CustomerLocation { get; set; } = "";
        public int? ProjectId { get; set; }
        [MaxLength(200)] public string ProjectName { get; set; } = "";
        [MaxLength(500)] public string ProjectDescription { get; set; } = "";
        public int? SalesmanId { get; set; }
        [MaxLength(200)] public string SalesmanName { get; set; } = "";
        public int? PreparedByUserId { get; set; }
        [MaxLength(200)] public string PreparedByName { get; set; } = "";
        [MaxLength(100)] public string PreparedByRole { get; set; } = "";
        [Required, MaxLength(30)] public string Status { get; set; } = InquiryStatuses.Received;
        [MaxLength(20)] public string Priority { get; set; } = "Normal";
        [Column(TypeName = "TEXT")] public decimal InquiryValue { get; set; }
        public string Notes { get; set; } = "";
        public DateTimeOffset ReceivedAt { get; set; }
        public DateTimeOffset? PreparedAt { get; set; }
        public DateTimeOffset? SentAt { get; set; }
        public DateTimeOffset? ConfirmedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        [MaxLength(200)] public string UpdatedBy { get; set; } = "";
        public int? ProformaInvoiceId { get; set; }
        [MaxLength(50)] public string ProformaNumber { get; set; } = "";
        [MaxLength(50)] public string JobOrderNumber { get; set; } = "";
        public List<InquiryAttachment> Attachments { get; set; } = new();
        public List<InquiryCommunication> Communications { get; set; } = new();
    }

    [Table("InquiryAttachments")]
    public sealed class InquiryAttachment
    {
        [Key] public long Id { get; set; }
        public int InquiryId { get; set; }
        [Required, MaxLength(260)] public string FileName { get; set; } = "";
        [MaxLength(20)] public string Extension { get; set; } = "";
        public long FileSize { get; set; }
        [Required] public string StoragePath { get; set; } = "";
        [MaxLength(50)] public string AttachmentType { get; set; } = "Document";
        [MaxLength(200)] public string UploadedBy { get; set; } = "";
        public DateTimeOffset UploadedAt { get; set; }
        public InquiryRecord Inquiry { get; set; } = null!;
    }

    [Table("InquiryCommunications")]
    public sealed class InquiryCommunication
    {
        [Key] public long Id { get; set; }
        public int InquiryId { get; set; }
        [Required, MaxLength(100)] public string EventType { get; set; } = "";
        [Required, MaxLength(200)] public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTimeOffset OccurredAt { get; set; }
        public int? ActorUserId { get; set; }
        [MaxLength(200)] public string ActorName { get; set; } = "";
        public InquiryRecord Inquiry { get; set; } = null!;
    }
}
