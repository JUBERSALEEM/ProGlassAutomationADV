using ProGlassAutomation.ViewModels.TaxInvoice;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.TaxInvoice
{
    /// <summary>
    /// Code-behind for TaxInvoiceView.xaml — shell container.
    ///
    /// Minimal code-behind: DataContext set externally by MainWindow
    /// or navigation system. All logic in TaxInvoiceViewModel.
    ///
    /// Same pattern as DeliveryNoteView.xaml.cs.
    /// </summary>
    public partial class TaxInvoiceView : UserControl
    {
        public TaxInvoiceView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Called by navigation system to set the ViewModel.
        /// Allows MainWindow to inject the VM rather than constructing
        /// it here — enables proper App.TaxInvoices initialization order.
        /// </summary>
        public void SetViewModel(TaxInvoiceViewModel vm)
        {
            DataContext = vm;
        }
    }
}