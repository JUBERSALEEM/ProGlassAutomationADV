using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote
{
    /// <summary>
    /// Tracks one uploaded image or PDF file attached to a delivery note.
    /// File is stored on disk, path reference stored in DB.
    ///
    /// Storage structure:
    ///   %AppData%\ProGlassAutomation\DeliveryNotes\Attachments\
    ///     Images\{DeliveryNoteId}\{filename}
    ///     PDFs\{DeliveryNoteId}\{filename}
    /// </summary>
    [Table("DeliveryNoteAttachments")]
    public class DeliveryNoteAttachment
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Parent DN reference, e.g. "DN-2026-001-01".</summary>
        [Required, MaxLength(50)]
        public string DeliveryNoteId { get; set; } = "";

        /// <summary>"Image" or "Pdf".</summary>
        [Required, MaxLength(20)]
        public string AttachmentType { get; set; } = "Image";

        /// <summary>Original filename, e.g. "photo1.jpg".</summary>
        [Required, MaxLength(260)]
        public string FileName { get; set; } = "";

        /// <summary>Full path on disk where the file is stored.</summary>
        [Required, MaxLength(500)]
        public string FilePath { get; set; } = "";

        /// <summary>File size in bytes.</summary>
        public long FileSize { get; set; }

        /// <summary>Optional user-entered description.</summary>
        [MaxLength(500)]
        public string Description { get; set; } = "";

        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}