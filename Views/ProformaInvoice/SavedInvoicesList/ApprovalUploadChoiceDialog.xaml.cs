using System.Windows;

namespace ProGlassAutomation.Views.ProformaInvoice.SavedInvoicesList
{
    /// <summary>
    /// The upload-type gate shown when a Proforma Invoice is approved and has
    /// no Step 8 attachments yet. The user picks whether they are uploading a
    /// JPG / image or a PDF (the signed Proforma Invoice or the LPO), then the
    /// shell opens Step 8 with the matching uploader.
    /// </summary>
    public partial class ApprovalUploadChoiceDialog : Window
    {
        /// <summary>True when the user picked a type (not cancel).</summary>
        public bool Chosen { get; private set; }

        /// <summary>True = upload a PDF; false = upload a JPG / image.</summary>
        public bool UploadPdf { get; private set; }

        public ApprovalUploadChoiceDialog()
        {
            InitializeComponent();
        }

        private void OnImageClick(object sender, RoutedEventArgs e)
        {
            Chosen = true;
            UploadPdf = false;
            DialogResult = true;
        }

        private void OnPdfClick(object sender, RoutedEventArgs e)
        {
            Chosen = true;
            UploadPdf = true;
            DialogResult = true;
        }

        }
}
