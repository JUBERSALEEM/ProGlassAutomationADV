using System.Collections.ObjectModel;

namespace ProGlassAutomation.ViewModels.ProformaInvoice
{
    /// <summary>
    /// Temporary list item — will be replaced with real ProformaInvoice entity later.
    /// Kept here so Dashboard bindings don't break.
    /// </summary>
    public class InvoiceItem : ViewModelBase
    {
        public string InvoiceNo { get; set; }
        public string CustomerName { get; set; }
        public string Date { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Pending";
    }

    /// <summary>
    /// Main ViewModel for the Proforma Invoice module.
    /// Currently hosts the 7-step wizard for building new invoices.
    /// A "Saved Invoices" list view will be added in a later phase.
    /// </summary>
    public class ProformaInvoiceViewModel : ViewModelBase
    {
        public string Title { get; } = "Proforma Invoice Builder";
        public string Subtitle { get; } = "Create professional glass invoices in 7 simple steps";

        // === Dashboard Contract (preserved so DashboardViewModel doesn't break) ===
        public ObservableCollection<InvoiceItem> Invoices { get; }
            = new ObservableCollection<InvoiceItem>();

        public decimal TotalSales => 0m;
        public int InvoiceCount => 0;
        public int PendingCount => 0;
        public bool HasInvoices => false;

        public ProformaInvoiceViewModel()
        {
            // Wizard steps will be initialized here in Step 1
        }
    }
}