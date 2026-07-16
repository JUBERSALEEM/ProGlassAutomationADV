using ProGlassAutomation.Commands;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice
{
    public class InvoiceItem : ViewModelBase
    {
        public string InvoiceNo { get; set; }
        public string CustomerName { get; set; }
        public string Date { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Pending"; // Pending / Paid
    }

    public class ProformaInvoiceViewModel : ViewModelBase
    {
        public string Title { get; } = "Proforma Invoice Module";

        // Shared list of invoices — Dashboard binds to this
        public ObservableCollection<InvoiceItem> Invoices { get; }
            = new ObservableCollection<InvoiceItem>();

        // Aggregate values
        public decimal TotalSales => Invoices.Sum(i => i.Amount);
        public int InvoiceCount => Invoices.Count;
        public int PendingCount => Invoices.Count(i => i.Status == "Pending");
        public bool HasInvoices => Invoices.Count > 0;

        // Selected item (for keyboard delete)
        private InvoiceItem _selectedInvoice;
        public InvoiceItem SelectedInvoice
        {
            get => _selectedInvoice;
            set { _selectedInvoice = value; OnPropertyChanged(); }
        }

        public ICommand AddInvoiceCommand { get; }
        public ICommand DeleteInvoiceCommand { get; }
        public ICommand EditInvoiceCommand { get; }
        public ICommand ViewInvoiceCommand { get; }

        public ProformaInvoiceViewModel()
        {
            Invoices.CollectionChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(TotalSales));
                OnPropertyChanged(nameof(InvoiceCount));
                OnPropertyChanged(nameof(PendingCount));
                OnPropertyChanged(nameof(HasInvoices));
            };

            AddInvoiceCommand = new RelayCommand(_ =>
            {
                var next = Invoices.Count + 1;
                Invoices.Add(new InvoiceItem
                {
                    InvoiceNo = $"PI-2025-{next:000}",
                    CustomerName = $"New Customer {next}",
                    Date = System.DateTime.Now.ToString("dd-MMM-yyyy"),
                    Amount = 10000 * next,
                    Status = "Pending"
                });
            });

            DeleteInvoiceCommand = new RelayCommand(param =>
            {
                var invoice = param as InvoiceItem ?? SelectedInvoice;
                if (invoice == null) return;

                var result = MessageBox.Show(
                    $"Delete invoice {invoice.InvoiceNo}?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    Invoices.Remove(invoice);
                }
            });

            EditInvoiceCommand = new RelayCommand(param =>
            {
                var invoice = param as InvoiceItem ?? SelectedInvoice;
                if (invoice == null) return;

                // TODO: Open edit dialog later
                MessageBox.Show(
                    $"Edit invoice {invoice.InvoiceNo}\n(Edit form will be added later)",
                    "Edit Invoice",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });

            ViewInvoiceCommand = new RelayCommand(param =>
            {
                var invoice = param as InvoiceItem ?? SelectedInvoice;
                if (invoice == null) return;

                // TODO: Open view dialog later
                MessageBox.Show(
                    $"Invoice: {invoice.InvoiceNo}\n" +
                    $"Customer: {invoice.CustomerName}\n" +
                    $"Date: {invoice.Date}\n" +
                    $"Amount: ₹ {invoice.Amount:N0}\n" +
                    $"Status: {invoice.Status}",
                    "Invoice Details",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });
        }
    }
}