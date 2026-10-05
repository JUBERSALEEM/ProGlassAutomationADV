using System;

namespace ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceDetail
{
    /// <summary>
    /// Display model for one attachment on a Tax Invoice.
    ///
    /// Split from TaxInvoiceDetailViewModel.cs (400-line limit).
    ///
    /// STORAGE:
    ///   FileBytes stored as byte[] in DB (TaxInvoiceAttachment entity).
    ///   In snapshot JSON: stored as base64 string (InvAttachmentSnapshot).
    ///   Mapper converts between byte[] ↔ base64 at save/load time.
    ///
    /// ATTACHMENT TYPES:
    ///   SignedPI  — signed/stamped Proforma Invoice PDF
    ///   SignedDN  — signed/stamped Delivery Note PDF
    ///   Other     — any other supporting document
    ///
    /// DOWNSTREAM (Statement PDF — future module):
    ///   Statement generator reads FileBytes directly from DB,
    ///   appends as pages to combined QuestPDF output.
    ///   Order enforced by TaxInvoiceRepository.GetAttachmentsAsync():
    ///   SignedPI → SignedDN → Other.
    ///
    /// UI BINDING:
    ///   AttachmentType is bound to ComboBox in TaxInvoiceDetailView.xaml.
    ///   TypeIcon and TypeLabel update via OnPropertyChanged when type changes.
    /// </summary>
    public class InvAttachmentViewModel : ViewModelBase
    {
        // ── DB identity ───────────────────────────────────────────────────

        /// <summary>
        /// DB primary key — 0 for unsaved (new invoice) attachments.
        /// Set by AddAttachmentAsync after persisting to DB.
        /// </summary>
        public int Id { get; set; }

        // ── Type ──────────────────────────────────────────────────────────

        private string _attachmentType = "Other";
        public string AttachmentType
        {
            get => _attachmentType;
            set
            {
                if (_attachmentType == value) return;
                _attachmentType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TypeIcon));
                OnPropertyChanged(nameof(TypeLabel));
            }
        }

        // ── File metadata ─────────────────────────────────────────────────

        public string FileName { get; set; } = "";
        public string MimeType { get; set; } = "application/pdf";
        public long FileSize { get; set; }
        public string Description { get; set; } = "";
        public DateTime UploadedAt { get; set; }

        // ── File content ──────────────────────────────────────────────────

        /// <summary>
        /// Raw bytes of the attached file.
        /// Loaded from DB on detail open.
        /// NULL for reference-only attachments (not yet supported).
        /// </summary>
        public byte[]? FileBytes { get; set; }

        // ── Computed display ──────────────────────────────────────────────

        /// <summary>
        /// Human-readable file size: "2.4 MB", "856 KB", "12 B".
        /// </summary>
        public string FileSizeDisplay
        {
            get
            {
                if (FileSize >= 1_048_576)
                    return $"{FileSize / 1_048_576.0:0.#} MB";
                if (FileSize >= 1_024)
                    return $"{FileSize / 1_024.0:0.#} KB";
                return $"{FileSize} B";
            }
        }

        /// <summary>
        /// Emoji icon for attachment type — used in list row UI.
        /// Updates when AttachmentType changes.
        /// </summary>
        public string TypeIcon => AttachmentType switch
        {
            "SignedPI" => "📄",
            "SignedDN" => "🚚",
            _ => "📎"
        };

        /// <summary>
        /// Human-readable label for attachment type.
        /// Updates when AttachmentType changes.
        /// </summary>
        public string TypeLabel => AttachmentType switch
        {
            "SignedPI" => "Signed PI",
            "SignedDN" => "Signed DN",
            _ => "Document"
        };

        /// <summary>
        /// True when FileBytes is populated and non-empty.
        /// Used to determine if Statement PDF can append this attachment.
        /// </summary>
        public bool HasContent =>
            FileBytes != null && FileBytes.Length > 0;

        /// <summary>
        /// Formatted upload timestamp for display.
        /// e.g. "15 Jan 2026 · 14:32"
        /// </summary>
        public string UploadedAtDisplay =>
            UploadedAt == default
                ? "—"
                : UploadedAt.ToString("dd MMM yyyy · HH:mm");
    }
}