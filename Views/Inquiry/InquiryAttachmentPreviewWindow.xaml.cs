using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Inquiry;
using System;
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
            InitializeComponent(); _attachment = attachment; _service = service;
            Title = attachment.FileName;
            string ext = attachment.Extension.ToLowerInvariant();
            try
            {
                if (ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp")
                {
                    ImagePreview.Source = new BitmapImage(new Uri(attachment.StoragePath));
                    ImagePreview.Visibility = Visibility.Visible;
                }
                else if (ext == ".pdf")
                {
                    PdfPreview.Visibility = Visibility.Visible;
                    PdfPreview.Navigate(new Uri(Path.GetFullPath(attachment.StoragePath)));
                }
                else UnsupportedPanel.Visibility = Visibility.Visible;
            }
            catch { UnsupportedPanel.Visibility = Visibility.Visible; }
        }
        private void OpenExternal_Click(object sender, RoutedEventArgs e) => _service.Open(_attachment);
    }
}
