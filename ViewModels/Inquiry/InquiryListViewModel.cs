using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.MasterData.Repository;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.Inquiry
{
    /// <summary>
    /// Inquiry register — 2026 SaaS rebuild.
    ///
    /// Read path: segmented status tabs → database-side paged projection.
    /// Write path: editor dialog / drawer quick actions → workflow service
    /// (guarded transitions, audit, version snapshots).
    ///
    /// Design notes (fixes from the senior analysis):
    /// - date filter defaults to "All time" (S1: saved inquiries vanished);
    /// - cancellations return quietly (S7: log spam);
    /// - selection survives a reload (A7: drawer slammed shut after save);
    /// - no placeholder commands — every visible control does real work.
    /// </summary>
    public sealed partial class InquiryListViewModel : ViewModelBase, System.IDisposable
    {
    }

    // ────────────────────────────────────────────────────────────────────
    // Row models
    // ────────────────────────────────────────────────────────────────────

    /// <summary>Segmented status tab with a live count.</summary>
    public sealed class InquiryStatusTab : ViewModelBase
    {
        public string Key { get; init; } = "";
        public string Label { get; init; } = "";
        private int _count;
        public int Count { get => _count; set { _count = value; OnPropertyChanged(); } }
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    }

    /// <summary>One register row. Display text is precomputed for cheap binding.</summary>
    public sealed class InquiryListRow
    {
        public int Id { get; private set; }
        public int RowVersion { get; private set; }
        public string InquiryNumber { get; private set; } = "";
        public string InquiryReference { get; private set; } = "";
        public string CustomerName { get; private set; } = "";
        public string CustomerLocation { get; private set; } = "";
        public string Project { get; private set; } = "";
        public string Subject { get; private set; } = "";
        public string Salesman { get; private set; } = "";
        public string PreparedBy { get; private set; } = "";
        public string PreparedByRole { get; private set; } = "";
        public string Status { get; internal set; } = InquiryStatuses.Received;
        public string Priority { get; private set; } = "Normal";
        public decimal InquiryValue { get; private set; }
        public string ValueText => InquiryValue == 0 ? "—" : InquiryValue.ToString("N0", CultureInfo.CurrentCulture);
        public DateTime ReceivedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public string ReceivedDateText => ReceivedAt.ToString("dd MMM yyyy", CultureInfo.CurrentCulture);
        public string ReceivedTimeText => ReceivedAt.ToString("hh:mm tt", CultureInfo.CurrentCulture);
        public string UpdatedText => UpdatedAt.ToString("dd MMM yyyy · hh:mm tt", CultureInfo.CurrentCulture);
        public string UpdatedBy { get; private set; } = "";
        public string UpdatedByDisplay => string.IsNullOrWhiteSpace(UpdatedBy) ? "" : $"by {UpdatedBy}";
        public int AttachmentCount { get; internal set; }
        public string AttachmentBadgeText { get; private set; } = "";
        public bool HasAttachments => AttachmentCount > 0;
        public string AttachmentChipText => AttachmentCount == 0 ? "None" : $"{AttachmentCount} file{(AttachmentCount == 1 ? "" : "s")}";
        public string ProformaNumber { get; private set; } = "";
        public string JobOrderNumber { get; private set; } = "";
        public bool IsConverted => !string.IsNullOrWhiteSpace(ProformaNumber);
        /// <summary>
        /// A PI can be created from a Prepared or Confirmed inquiry.
        /// The inquiry status is then automatically advanced based on
        /// the PI lifecycle: PI created → Sent, PI confirmed → Confirmed,
        /// PI rejected/cancelled → Cancelled. This removes the need for
        /// the operator to manually mark the inquiry as Confirmed before
        /// creating a PI — the PI itself IS the confirmation evidence.
        /// </summary>
        public bool CanConvert =>
            (Status == InquiryStatuses.Prepared || Status == InquiryStatuses.Confirmed)
            && !IsConverted;
        // Workflow affordances for the drawer (guarded by the same state
        // machine the workflow service enforces server-side).
        public bool CanPrepareWorkflow => Services.Inquiry.InquiryWorkflowService.CanTransition(Status, InquiryStatuses.Prepared);
        public bool CanSendWorkflow => Services.Inquiry.InquiryWorkflowService.CanTransition(Status, InquiryStatuses.Sent);
        public bool CanConfirmWorkflow => Services.Inquiry.InquiryWorkflowService.CanTransition(Status, InquiryStatuses.Confirmed);
        public bool CanHoldWorkflow => Services.Inquiry.InquiryWorkflowService.CanTransition(Status, InquiryStatuses.Hold);
        public bool CanCancelWorkflow => Services.Inquiry.InquiryWorkflowService.CanTransition(Status, InquiryStatuses.Cancelled);
        public string ChainText => IsConverted
            ? "PI " + ProformaNumber + (string.IsNullOrWhiteSpace(JobOrderNumber) ? " · JO not released" : " · JO " + JobOrderNumber)
            : "Not converted to PI yet";
        public string Notes { get; internal set; } = "";
        public string NotesPreview { get; internal set; } = "";
        public bool IsDetailLoaded { get; internal set; }
        public string ContactSummary => string.IsNullOrWhiteSpace(CustomerLocation) ? "" : CustomerLocation;
        /// <summary>Avatar initials for the row card (first letters of the first two words).</summary>
        public string CustomerInitials
        {
            get
            {
                var parts = CustomerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return "·";
                return parts.Length == 1
                    ? parts[0][..1].ToUpperInvariant()
                    : (parts[0][..1] + parts[1][..1]).ToUpperInvariant();
            }
        }
        public ObservableCollection<InquiryAttachmentItem> Attachments { get; } = new();
        public ObservableCollection<InquiryActivityItem> RecentActivity { get; } = new();

        // ── Full-lifecycle display fields ────────────────────────────
        // Bound from the InquiryLifecycleSnapshot hydrated by the repository
        // via IInquiryLifecycleService. Every property here is a precomputed
        // string the XAML can bind to directly without converters.

        public InquiryLifecycleSnapshot Lifecycle { get; private set; } = new();

        // Per-stage cell text for the row card. Empty when the stage has not started.
        public string ProformaCellText => Lifecycle.HasProforma
            ? $"{Lifecycle.ProformaNumber} · {Lifecycle.ProformaStatus}"
            : "—";
        public string JobOrderCellText => Lifecycle.HasJobOrder
            ? $"{Lifecycle.JobOrderNumber} · {Lifecycle.JobOrderStatus}"
            : "—";
        public string DeliveryNoteCellText => Lifecycle.HasDeliveryNote
            ? (Lifecycle.DeliveryNoteCount > 1
                ? $"{Lifecycle.LatestDeliveryNoteNumber} · {Lifecycle.LatestDeliveryNoteStatus} (+{Lifecycle.DeliveryNoteCount - 1})"
                : $"{Lifecycle.LatestDeliveryNoteNumber} · {Lifecycle.LatestDeliveryNoteStatus}")
            : "—";
        public string TaxInvoiceCellText => Lifecycle.HasTaxInvoice
            ? (Lifecycle.TaxInvoiceCount > 1
                ? $"{Lifecycle.LatestTaxInvoiceNumber} · {Lifecycle.LatestTaxInvoiceStatus} (+{Lifecycle.TaxInvoiceCount - 1})"
                : $"{Lifecycle.LatestTaxInvoiceNumber} · {Lifecycle.LatestTaxInvoiceStatus}")
            : "—";
        public string StatementCellText => Lifecycle.HasStatement
            ? Lifecycle.LatestStatementNumber ?? "—"
            : "—";

        // Per-stage coarse state — bound to background tints in the row card.
        public InquiryLifecycleState ProformaState =>
            Lifecycle.StateFor(InquiryLifecycleKind.ProformaInvoice);
        public InquiryLifecycleState JobOrderState =>
            Lifecycle.StateFor(InquiryLifecycleKind.JobOrder);
        public InquiryLifecycleState DeliveryNoteState =>
            Lifecycle.StateFor(InquiryLifecycleKind.DeliveryNote);
        public InquiryLifecycleState TaxInvoiceState =>
            Lifecycle.StateFor(InquiryLifecycleKind.TaxInvoice);
        public InquiryLifecycleState StatementState =>
            Lifecycle.StateFor(InquiryLifecycleKind.Statement);

        // Aggregated money fields.
        public string TotalBilledText => Lifecycle.TotalBilled == 0
            ? "—"
            : $"AED {Lifecycle.TotalBilled.ToString("N0", CultureInfo.CurrentCulture)}";
        public string CurrentBillOutstandingText => Lifecycle.CurrentBillOutstanding == 0
            ? "—"
            : $"AED {Lifecycle.CurrentBillOutstanding.ToString("N0", CultureInfo.CurrentCulture)}";
        public string TotalOutstandingText => Lifecycle.TotalOutstanding == 0
            ? "—"
            : $"AED {Lifecycle.TotalOutstanding.ToString("N0", CultureInfo.CurrentCulture)}";

        // Pipeline progress — "2/6" of stages reached, with a label.
        public string PipelineProgressText =>
            $"{Lifecycle.FurthestStageReached + 1}/{(int)InquiryLifecycleKind.Statement + 1}";
        public int PipelineProgressPercent =>
            (int)Math.Round(100.0 * (Lifecycle.FurthestStageReached + 1) /
                ((int)InquiryLifecycleKind.Statement + 1));

        public static InquiryListRow FromProjection(InquiryListProjection x) => new()
        {
            Id = x.Id, RowVersion = x.RowVersion,
            InquiryNumber = x.InquiryNumber, InquiryReference = x.InquiryReference,
            CustomerName = x.CustomerName, CustomerLocation = x.CustomerLocation,
            Project = x.ProjectName, Subject = x.ProjectDescription,
            Salesman = x.SalesmanName, PreparedBy = x.PreparedByName, PreparedByRole = x.PreparedByRole,
            Status = InquiryStatuses.Normalize(x.Status), Priority = x.Priority,
            InquiryValue = x.InquiryValue, ReceivedAt = x.ReceivedAt, UpdatedAt = x.UpdatedAt,
            UpdatedBy = x.UpdatedBy, AttachmentCount = x.AttachmentCount,
            AttachmentBadgeText = x.AttachmentBadgeText, ProformaNumber = x.ProformaNumber,
            JobOrderNumber = x.JobOrderNumber, Lifecycle = x.Lifecycle
        };
    }

    /// <summary>Attachment chip in the details drawer.</summary>
    public sealed class InquiryAttachmentItem
    {
        public long Id { get; private set; }
        public string FileName { get; private set; } = "";
        public string Extension { get; private set; } = "";
        public string TypeLabel { get; private set; } = "";
        public long FileSize { get; private set; }
        public string FileSizeDisplay => FileSize < 1024 * 1024
            ? $"{FileSize / 1024d:N0} KB" : $"{FileSize / (1024d * 1024d):N1} MB";
        public string UploadedBy { get; private set; } = "";
        public DateTime UploadedAt { get; private set; }
        public string UploadedText => UploadedAt.ToString("dd MMM yyyy", CultureInfo.CurrentCulture);
        public InquiryAttachment Attachment { get; private set; } = new();

        public static InquiryAttachmentItem FromModel(InquiryAttachment x) => new()
        {
            Id = x.Id, FileName = x.FileName, Extension = x.Extension,
            TypeLabel = (x.Extension ?? "").TrimStart('.').ToUpperInvariant(),
            FileSize = x.FileSize, UploadedBy = x.UploadedBy, UploadedAt = x.UploadedAt,
            Attachment = x
        };
    }

    /// <summary>Timeline entry for the drawer activity feed.</summary>
    public sealed class InquiryActivityItem
    {
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
        public string Actor { get; init; } = "";
        public DateTime OccurredAt { get; init; }
        public string OccurredText => OccurredAt.ToString("dd MMM yyyy · hh:mm tt", CultureInfo.CurrentCulture);
    }
}
