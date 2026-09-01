using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.Views.ProformaInvoice.SavedInvoicesList
{
    /// <summary>
    /// Modal gate shown when a Proforma Invoice is approved. The user must
    /// confirm HOW the customer confirmed the quote:
    ///   • "Customer signed the Proforma Invoice" → approval proceeds without
    ///     needing an LPO number.
    ///   • "Customer provided an LPO / Purchase Order" → an LPO number is
    ///     required; it is saved back to the invoice (Step 3) so invoices and
    ///     the statement generator always have it.
    ///
    /// Returns true from ShowDialog() only when the user actually confirmed
    /// approval (Approved == true). Cancel / close returns false and leaves
    /// the invoice status unchanged.
    /// </summary>
    public partial class InvoiceApprovalDialog : Window
    {
        /// <summary>True when the user confirmed the approval.</summary>
        public bool Approved { get; private set; }

        /// <summary>True when "Customer signed the Proforma Invoice" was chosen.</summary>
        public bool IsSignedPi { get; private set; }

        /// <summary>The LPO / PO number the user entered (empty if Signed PI).</summary>
        public string LpoNumber { get; private set; } = "";

        public InvoiceApprovalDialog(int attachmentCount = 0)
        {
            InitializeComponent();

            // Show that the confirmation is grounded on the real Step 8
            // attachments already attached to this invoice.
            AttachmentNote.Text =
                $"{attachmentCount} file(s) currently attached in Step 8 (Attachments).";

            OnModeChanged(this, null);
        }

        private void OnModeChanged(object sender, RoutedEventArgs? e)
        {
            // The Checked event can fire during InitializeComponent before all
            // named elements exist — guard so it never throws.
            if (LpoRadio == null || LpoPanel == null || ValidationBorder == null) return;

            bool lpoSelected = LpoRadio.IsChecked == true;

            // Hidden (not Collapsed) keeps the reserved strip laid out, so
            // showing an error later never re-measures the window.
            ValidationBorder.Visibility = Visibility.Hidden;

            // The label stays a constant single line in BOTH states, so
            // switching modes cannot change the dialog's layout height.
            if (LpoLabel != null)
            {
                LpoLabel.Text = lpoSelected
                    ? "LPO / PO NUMBER (REQUIRED)"
                    : "LPO / PO NUMBER (OPTIONAL)";
            }

            if (lpoSelected)
            {
                // Focus after the panel is laid out.
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    LpoBox.Focus();
                    LpoBox.SelectAll();
                }));
            }
        }

        private void OnLpoKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) OnApproveClick(sender, e);
        }

        private void OnApproveClick(object sender, RoutedEventArgs e)
        {
            bool lpoSelected = LpoRadio.IsChecked == true;
            string lpo = LpoBox.Text?.Trim() ?? "";

            // LPO → the number is REQUIRED (an LPO always carries one).
            if (lpoSelected && string.IsNullOrWhiteSpace(lpo))
            {
                ValidationText.Text =
                    "An LPO always carries an LPO / purchase order number. " +
                    "Enter it to approve this invoice.";
                ValidationBorder.Visibility = Visibility.Visible;
                LpoBox.Focus();
                return;
            }

            // Signed PI → the LPO number is OPTIONAL: save it only if the user
            // entered one (sometimes it is written on the signed PI).
            LpoNumber = lpo;
            IsSignedPi = !lpoSelected;
            Approved = true;
            DialogResult = true;
        }

        }
}
