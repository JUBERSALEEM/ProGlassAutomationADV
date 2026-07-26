using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.TaxInvoice;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.TaxInvoice
{
    public class TaxInvoiceListViewModel : ViewModelBase
    {
        private readonly TaxInvoiceViewModel _parent;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); }
        }

        private ObservableCollection<Models.TaxInvoice.TaxInvoice> _invoices = new();
        public ObservableCollection<Models.TaxInvoice.TaxInvoice> Invoices
        {
            get => _invoices;
            set { if (_invoices == value) return; _invoices = value; OnPropertyChanged(); }
        }

        private List<Models.TaxInvoice.TaxInvoice> _rawInvoices = new();

        // ── Filters ──────────────────────────────────────────────────────

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        private string _selectedPaymentFilter = "All Statuses";
        public string SelectedPaymentFilter
        {
            get => _selectedPaymentFilter;
            set
            {
                if (_selectedPaymentFilter == value) return;
                _selectedPaymentFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public List<string> PaymentFilterOptions { get; } = new() { "All Statuses", "Unpaid", "Paid", "Partially Paid" };

        // ── Commands ──────────────────────────────────────────────────────

        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand DeleteCommand { get; }

        public TaxInvoiceListViewModel(TaxInvoiceViewModel parent)
        {
            _parent = parent;

            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

            OpenCommand = new RelayCommand(param =>
            {
                if (param is Models.TaxInvoice.TaxInvoice inv)
                {
                    _parent.IsListView = false;
                    _parent.CreateVM.LoadExistingInvoice(inv);
                }
            });

            DeleteCommand = new RelayCommand(async param =>
            {
                if (param is Models.TaxInvoice.TaxInvoice inv)
                {
                    await DeleteInvoiceAsync(inv);
                }
            });
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                if (App.TaxInvoices != null)
                {
                    _rawInvoices = await App.TaxInvoices.GetAllAsync();
                    ApplyFilters();
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilters()
        {
            IEnumerable<Models.TaxInvoice.TaxInvoice> query = _rawInvoices;

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string q = _searchText.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    x.InvoiceNumber.ToLowerInvariant().Contains(q) ||
                    x.DeliveryNoteId.ToLowerInvariant().Contains(q) ||
                    x.CustomerName.ToLowerInvariant().Contains(q) ||
                    x.ProjectName.ToLowerInvariant().Contains(q));
            }

            if (_selectedPaymentFilter != "All Statuses")
            {
                query = query.Where(x => string.Equals(x.PaymentStatus, _selectedPaymentFilter, StringComparison.OrdinalIgnoreCase));
            }

            Invoices = new ObservableCollection<Models.TaxInvoice.TaxInvoice>(query);
        }

        private void ClearFilters()
        {
            _searchText = "";
            _selectedPaymentFilter = "All Statuses";

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedPaymentFilter));

            ApplyFilters();
        }

        private async Task DeleteInvoiceAsync(Models.TaxInvoice.TaxInvoice invoice)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to permanently delete Tax Invoice '{invoice.InvoiceNumber}'?\n\n" +
                "This will:\n" +
                "• Delete the Tax Invoice entity\n" +
                "• Release the invoiced quantities back to the source Delivery Note\n\n" +
                "This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                IsLoading = true;

                // ── Step 1: Release InvoicedQty on source Delivery Note ────────
                if (App.DeliveryNotes != null && !string.IsNullOrWhiteSpace(invoice.DeliveryNoteId))
                {
                    var dn = await App.DeliveryNotes.GetByDeliveryNoteIdAsync(invoice.DeliveryNoteId);
                    if (dn != null)
                    {
                        var freshDn = await App.DeliveryNotes.GetByIdAsync(dn.Id);
                        if (freshDn != null)
                        {
                            foreach (var item in freshDn.Items)
                            {
                                var billedItem = invoice.Items.FirstOrDefault(i => i.SpecGroupNumber == item.SpecGroupNumber && i.SerialNumber == item.SerialNumber);
                                if (billedItem != null)
                                {
                                    item.InvoicedQty = Math.Max(0, item.InvoicedQty - billedItem.InvoicedQty);
                                }
                            }

                            freshDn.HasTaxInvoice = false;
                            freshDn.TaxInvoiceId = "";

                            await App.DeliveryNotes.UpdateAsync(freshDn);
                        }
                    }
                }

                // ── Step 2: Delete Tax Invoice ─────────────────────────────────
                if (App.TaxInvoices != null)
                {
                    await App.TaxInvoices.DeleteAsync(invoice.Id);
                }

                MessageBox.Show($"Tax Invoice '{invoice.InvoiceNumber}' deleted successfully and quantities released.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete Tax Invoice:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}