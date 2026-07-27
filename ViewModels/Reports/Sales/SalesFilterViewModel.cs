using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Reports.Sales;
using ProGlassAutomation.Services.Reports.Repository;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.Sales
{
    public class SalesFilterViewModel : ViewModelBase
    {
        private readonly ISalesReportRepository _repository;

        public event Action? FilterChanged;

        // ── Filter values ───────────────────────────────────────────────

        private DateTime? _startDate;
        public DateTime? StartDate
        {
            get => _startDate;
            set { if (_startDate == value) return; _startDate = value; OnPropertyChanged(); TriggerFilterChanged(); }
        }

        private DateTime? _endDate;
        public DateTime? EndDate
        {
            get => _endDate;
            set { if (_endDate == value) return; _endDate = value; OnPropertyChanged(); TriggerFilterChanged(); }
        }

        private string _selectedCustomer = "All Customers";
        public string SelectedCustomer
        {
            get => _selectedCustomer;
            set { if (_selectedCustomer == value) return; _selectedCustomer = value; OnPropertyChanged(); TriggerFilterChanged(); }
        }

        private string _selectedProject = "All Projects";
        public string SelectedProject
        {
            get => _selectedProject;
            set { if (_selectedProject == value) return; _selectedProject = value; OnPropertyChanged(); TriggerFilterChanged(); }
        }

        private string _selectedSalesman = "All Salesmen";
        public string SelectedSalesman
        {
            get => _selectedSalesman;
            set { if (_selectedSalesman == value) return; _selectedSalesman = value; OnPropertyChanged(); TriggerFilterChanged(); }
        }

        private string _selectedCompany = "All Companies";
        public string SelectedCompany
        {
            get => _selectedCompany;
            set { if (_selectedCompany == value) return; _selectedCompany = value; OnPropertyChanged(); TriggerFilterChanged(); }
        }

        private string _invoiceSearchText = "";
        public string InvoiceSearchText
        {
            get => _invoiceSearchText;
            set { if (_invoiceSearchText == value) return; _invoiceSearchText = value; OnPropertyChanged(); TriggerFilterChanged(); }
        }

        private string _selectedPaymentStatus = "All Statuses";
        public string SelectedPaymentStatus
        {
            get => _selectedPaymentStatus;
            set { if (_selectedPaymentStatus == value) return; _selectedPaymentStatus = value; OnPropertyChanged(); TriggerFilterChanged(); }
        }

        // ── Dropdown options ─────────────────────────────────────────────

        public ObservableCollection<string> Customers { get; } = new();
        public ObservableCollection<string> Projects { get; } = new();
        public ObservableCollection<string> Salesmen { get; } = new();
        public ObservableCollection<string> Companies { get; } = new();
        public ObservableCollection<string> PaymentStatuses { get; } = new() { "All Statuses", "Paid", "Partially Paid", "Unpaid" };

        public ICommand ClearFiltersCommand { get; }

        public SalesFilterViewModel(ISalesReportRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));

            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
        }

        public async Task LoadFilterOptionsAsync()
        {
            try
            {
                var custs = await _repository.GetUniqueCustomersAsync();
                var projs = await _repository.GetUniqueProjectsAsync();
                var salesmenList = await _repository.GetUniqueSalesmenAsync();
                var comps = await _repository.GetUniqueCompaniesAsync();

                Customers.Clear();
                Customers.Add("All Customers");
                foreach (var c in custs) Customers.Add(c);

                Projects.Clear();
                Projects.Add("All Projects");
                foreach (var p in projs) Projects.Add(p);

                Salesmen.Clear();
                Salesmen.Add("All Salesmen");
                foreach (var s in salesmenList) Salesmen.Add(s);

                Companies.Clear();
                Companies.Add("All Companies");
                foreach (var co in comps) Companies.Add(co);
            }
            catch { /* Silent protection in production */ }
        }

        public SalesFilterModel ToFilterModel()
        {
            return new SalesFilterModel
            {
                StartDate = StartDate,
                EndDate = EndDate,
                CustomerName = SelectedCustomer,
                ProjectName = SelectedProject,
                Salesman = SelectedSalesman,
                CompanyName = SelectedCompany,
                InvoiceNumber = InvoiceSearchText,
                PaymentStatus = SelectedPaymentStatus
            };
        }

        public void ClearFilters()
        {
            // Block change notifications from executing multiple times
            _startDate = null;
            _endDate = null;
            _selectedCustomer = "All Customers";
            _selectedProject = "All Projects";
            _selectedSalesman = "All Salesmen";
            _selectedCompany = "All Companies";
            _invoiceSearchText = "";
            _selectedPaymentStatus = "All Statuses";

            OnPropertyChanged(nameof(StartDate));
            OnPropertyChanged(nameof(EndDate));
            OnPropertyChanged(nameof(SelectedCustomer));
            OnPropertyChanged(nameof(SelectedProject));
            OnPropertyChanged(nameof(SelectedSalesman));
            OnPropertyChanged(nameof(SelectedCompany));
            OnPropertyChanged(nameof(InvoiceSearchText));
            OnPropertyChanged(nameof(SelectedPaymentStatus));

            TriggerFilterChanged();
        }

        private void TriggerFilterChanged()
        {
            FilterChanged?.Invoke();
        }
    }
}