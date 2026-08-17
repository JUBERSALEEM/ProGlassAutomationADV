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

        public InvoiceApprovalDialog()
        {
            InitializeComponent();
            OnModeChanged(this, null);
        }

        private void OnModeChanged(object sender, RoutedEventArgs? e)
        {
            // The Checked event can fire during InitializeComponent before all
            // named elements exist — guard so it never throws.
            if (LpoRadio == null || LpoPanel == null || ValidationBorder == null) return;

            bool lpoSelected = LpoRadio.IsChecked == true;
            LpoPanel.Visibility = lpoSelected ? Visibility.Visible : Visibility.Collapsed;
            ValidationBorder.Visibility = Visibility.Collapsed;

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

            if (lpoSelected)
            {
                string lpo = LpoBox.Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(lpo))
                {
                    ValidationText.Text =
                        "Enter the LPO / purchase order number to approve " +
                        "this invoice — it is required for invoices and the " +
                        "statement generator.";
                    ValidationBorder.Visibility = Visibility.Visible;
                    LpoBox.Focus();
                    return;
                }
                LpoNumber = lpo;
            }

            IsSignedPi = !lpoSelected;
            Approved = true;
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Approved = false;
            DialogResult = false;
        }
    }
}
