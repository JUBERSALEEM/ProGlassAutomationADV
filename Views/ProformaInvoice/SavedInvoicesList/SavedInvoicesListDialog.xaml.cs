using ProGlassAutomation.ViewModels.ProformaInvoice.SavedInvoicesList;
using System.Windows;

namespace ProGlassAutomation.Views.ProformaInvoice.SavedInvoicesList
{
    public partial class SavedInvoicesListDialog : Window
    {
        /// <summary>The Id of the invoice user picked (0 if user cancelled).</summary>
        public int SelectedInvoiceId { get; private set; }

        public SavedInvoicesListDialog()
        {
            InitializeComponent();

            var vm = new SavedInvoicesListViewModel
            {
                OnInvoiceLoadRequested = OnInvoicePicked
            };

            EmbeddedListView.DataContext = vm;
        }

        private void OnInvoicePicked(int invoiceId)
        {
            SelectedInvoiceId = invoiceId;
            DialogResult = true;
            Close();
        }

        }
}