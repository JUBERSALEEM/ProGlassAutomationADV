using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ProGlassAutomation.Views.Inquiry
{
    public partial class InquiryAttachmentPreviewWindow : Window
    {
        private readonly InquiryAttachment _attachment;
        private readonly InquiryAttachmentService _service;

        public InquiryAttachmentPreviewWindow(InquiryAttachment attachment, InquiryAttachmentService service)
        {
            InitializeComponent();
            _attachment = attachment ?? throw new ArgumentNullException(nameof(attachment));
            _service = service ?? throw new ArgumentNullException(nameof(service));
            Title = "Preview — " + _attachment.FileName;
            NameText.Text = _attachment.FileName;
            MetaText.Text = string.Format(CultureInfo.CurrentCulture, "{0} · {1:N0} bytes · attached {2:dd MMM yyyy}",
                (_attachment.Extension ?? "").TrimStart('.').ToUpperInvariant(),
                _attachment.FileSize, _attachment.UploadedAt);

            // A3+A4 fix: resolve the real on-disk path (absolute, then the
            // current data-root fallback) before choosing a surface.
            string? path = InquiryAttachmentService.ResolvePath(_attachment);
            string ext = (_attachment.Extension ?? "").ToLowerInvariant();
            try
            {
                if (path == null)
                {
                    MissingPanel.Visibility = Visibility.Visible;
                }
                else if (ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp")
                {
                    // A1 fix: decode into memory and freeze — the file handle is
                    // released immediately, so attachment delete never fails.
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.UriSource = new Uri(path);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    ImagePreview.Source = bitmap;
                    ImagePreview.Visibility = Visibility.Visible;
                }
                else if (ext == ".pdf")
                {
                    PdfPreview.Visibility = Visibility.Visible;
                    PdfPreview.Navigate(new Uri(Path.GetFullPath(path)));
                }
                else
                {
                    UnsupportedPanel.Visibility = Visibility.Visible;
                }
            }
            catch (IOException) { MissingPanel.Visibility = Visibility.Visible; }
            catch (Exception) { UnsupportedPanel.Visibility = Visibility.Visible; }
        }

        private void OpenExternal_Click(object sender, RoutedEventArgs e)
        {
            var result = _service.Open(_attachment);
            if (!result.IsSuccess)
                MessageBox.Show(this, result.Error, "Open attachment",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>A2 fix: release the PDF document handle before disposal.</summary>
        private void OnClosed(object? sender, EventArgs e)
        {
            if (PdfPreview.Visibility == Visibility.Visible)
            {
                try { PdfPreview.Navigate("about:blank"); }
                catch { /* browser already torn down */ }
            }
        }
    }
}
