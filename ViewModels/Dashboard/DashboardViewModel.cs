using ProGlassAutomation.ViewModels.ProformaInvoice;
using System.ComponentModel;

namespace ProGlassAutomation.ViewModels.Dashboard
{
    public class DashboardViewModel : ViewModelBase
    {
        public string WelcomeMessage { get; } = "Welcome to ProGlass Automation Dashboard";

        // Direct reference to Proforma VM (quick binding — will refactor later)
        public ProformaInvoiceViewModel ProformaVM { get; }

        public decimal TotalSales => ProformaVM.TotalSales;
        public int InvoiceCount => ProformaVM.InvoiceCount;
        public int PendingCount => ProformaVM.PendingCount;
        public int CustomerCount => 142; // Placeholder until Customers module exists

        public DashboardViewModel(ProformaInvoiceViewModel proformaVM)
        {
            ProformaVM = proformaVM;

            // Whenever Proforma raises a property change on aggregates,
            // re-raise on Dashboard so bindings refresh
            ProformaVM.PropertyChanged += ProformaVM_PropertyChanged;
        }

        private void ProformaVM_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ProformaInvoiceViewModel.TotalSales):
                    OnPropertyChanged(nameof(TotalSales));
                    break;
                case nameof(ProformaInvoiceViewModel.InvoiceCount):
                    OnPropertyChanged(nameof(InvoiceCount));
                    break;
                case nameof(ProformaInvoiceViewModel.PendingCount):
                    OnPropertyChanged(nameof(PendingCount));
                    break;
            }
        }
    }
}