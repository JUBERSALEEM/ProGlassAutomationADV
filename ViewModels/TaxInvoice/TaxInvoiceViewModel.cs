using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.ViewModels.Shared;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.TaxInvoice
{
    /// <summary>
    /// Shell ViewModel for the Tax Invoice module.
    /// Manages switching between List View and Create/Detail View.
    /// </summary>
    public class TaxInvoiceViewModel : ViewModelBase
    {
        public string Title { get; } = "Tax Invoices";
        public string Subtitle { get; } = "Generate and manage UAE 5% VAT-compliant Tax Invoices";

        public TaxInvoiceListViewModel ListVM { get; }
        public TaxInvoiceCreateViewModel CreateVM { get; }

        private bool _isListView = true;
        public bool IsListView
        {
            get => _isListView;
            set
            {
                if (_isListView == value) return;
                _isListView = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCreateView));
            }
        }

        public bool IsCreateView => !_isListView;

        public ICommand BackToListCommand { get; }

        public TaxInvoiceViewModel()
        {
            ListVM = new TaxInvoiceListViewModel(this);
            CreateVM = new TaxInvoiceCreateViewModel(this);

            BackToListCommand = new RelayCommand(_ => ReturnToList());
        }

        public void ReturnToList()
        {
            IsListView = true;
            _ = ListVM.LoadAsync();
        }

        public void NavigateToCreateFull(SavedDeliveryNote dn)
        {
            IsListView = false;
            CreateVM.InitializeFromDeliveryNote(dn, isFullConversion: true);
        }

        public void NavigateToCreatePartial(SavedDeliveryNote dn)
        {
            IsListView = false;
            CreateVM.InitializeFromDeliveryNote(dn, isFullConversion: false);
        }
    }
}