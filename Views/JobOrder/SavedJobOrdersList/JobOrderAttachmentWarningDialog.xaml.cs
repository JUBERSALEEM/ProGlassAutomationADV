// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/JobOrder/SavedJobOrdersList/JobOrderAttachmentWarningDialog.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Job Order — delete flow
//
// Two-option gate shown when a Job Order whose source Proforma Invoice
// still has Step 8 attachments is being deleted:
//   • DELETE ATTACHMENTS → the caller removes the invoice's attachments
//     after the job order delete succeeds.
//   • KEEP ATTACHMENTS   → the attachments stay on disk; the invoice is
//     still cancelled, but its files/manifest are untouched.
//
// The job order itself is deleted in BOTH cases (that decision is made
// by the caller's earlier confirmation dialog). Esc / X / window close
// resolve to KEEP — the safe default that never destroys documents.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using ProGlassAutomation.Views.Shared.Dialogs;

namespace ProGlassAutomation.Views.JobOrder.SavedJobOrdersList
{
    public partial class JobOrderAttachmentWarningDialog : ThemedDialogWindow
    {
        /// <summary>
        /// True when the user chose DELETE ATTACHMENTS; false for KEEP.
        /// Only meaningful after ShowDialog() returns.
        /// </summary>
        public bool DeleteAttachments { get; private set; }

        public JobOrderAttachmentWarningDialog(
            string jobOrderId, string proformaId, int attachmentCount)
        {
            InitializeComponent();

            string countLabel = attachmentCount == 1 ? "1 attachment" : $"{attachmentCount} attachments";

            MessageText.Text =
                $"Deleting job order '{jobOrderId}' will also cancel " +
                $"proforma invoice '{proformaId}'.\n\n" +
                $"Invoice '{proformaId}' has {countLabel} attached in " +
                "Step 8 (Attachments). The job order will be deleted either " +
                "way — delete these attachments together with it, or keep them?";
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            DeleteAttachments = true;
            DialogResult = true;
        }

        private void OnKeepClick(object sender, RoutedEventArgs e)
        {
            DeleteAttachments = false;
            DialogResult = true;
        }

        // Esc / X → KEEP (safe default). IsCancel on the Keep button covers
        // Esc; the window's close button resolves to DialogResult=false, so
        // the caller must treat "no dialog result" as KEEP as well.
    }
}
