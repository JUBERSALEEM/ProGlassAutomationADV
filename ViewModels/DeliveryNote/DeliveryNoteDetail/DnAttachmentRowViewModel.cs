using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Attachments;
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail
{
    /// <summary>
    /// One row in the attachments list on the DN detail view.
    /// Wraps a DeliveryNoteAttachment entity for display.
    ///
    /// BUG-FIX: Preview was calling LoadPreview() on every property
    /// access — file read + BitmapImage allocation on every render.
    /// Now cached on first access via _preview backing field.
    /// </summary>
    public class DnAttachmentRowViewModel : ViewModelBase
    {
        public int Id { get; }
        public string DeliveryNoteId { get; }
        public string AttachmentType { get; }
        public string FileName { get; }
        public string FilePath { get; }
        public long FileSize { get; }
        public DateTime UploadedAt { get; }

        // ── Display helpers ───────────────────────────────────────────────

        public string FileSizeDisplay { get; }
        public string UploadedAtDisplay { get; }
        public string TypeIcon { get; }
        public bool IsImage { get; }
        public bool IsPdf { get; }

        /// <summary>
        /// Preview thumbnail — only populated for Image type.
        /// Loaded lazily on first access and cached — never re-reads
        /// from disk on subsequent renders (BUG-FIX).
        /// Small (80 px wide) for memory efficiency.
        /// </summary>
        private ImageSource? _preview;
        private bool _previewLoaded;

        public ImageSource? Preview
        {
            get
            {
                if (!IsImage) return null;

                // Cache on first access — never re-read from disk
                if (!_previewLoaded)
                {
                    _preview = LoadPreview();
                    _previewLoaded = true;
                }

                return _preview;
            }
        }

        private string _description = "";
        public string Description
        {
            get => _description;
            set
            {
                if (_description == value) return;
                _description = value;
                OnPropertyChanged();
            }
        }

        public DnAttachmentRowViewModel(DeliveryNoteAttachment e)
        {
            Id = e.Id;
            DeliveryNoteId = e.DeliveryNoteId;
            AttachmentType = e.AttachmentType;
            FileName = e.FileName;
            FilePath = e.FilePath;
            FileSize = e.FileSize;
            UploadedAt = e.UploadedAt;
            _description = e.Description ?? "";

            // Fully qualify to avoid clash with local AttachmentType property
            IsImage = string.Equals(
                AttachmentType,
                ProGlassAutomation.Services.DeliveryNote.Attachments
                    .AttachmentType.Image,
                StringComparison.OrdinalIgnoreCase);

            IsPdf = string.Equals(
                AttachmentType,
                ProGlassAutomation.Services.DeliveryNote.Attachments
                    .AttachmentType.Pdf,
                StringComparison.OrdinalIgnoreCase);

            TypeIcon = IsImage ? "🖼️" : "📄";
            FileSizeDisplay = AttachmentService.FormatFileSize(FileSize);
            UploadedAtDisplay = UploadedAt.ToString("dd MMM yyyy HH:mm");
        }

        private ImageSource? LoadPreview()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;

                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.DecodePixelWidth = 80;
                img.UriSource = new Uri(FilePath, UriKind.Absolute);
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch
            {
                return null;
            }
        }
    }
}