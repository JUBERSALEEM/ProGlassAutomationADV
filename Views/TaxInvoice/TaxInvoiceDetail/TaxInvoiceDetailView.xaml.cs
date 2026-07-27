using ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceDetail;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.TaxInvoice.TaxInvoiceDetail
{
    /// <summary>
    /// Code-behind for TaxInvoiceDetailView.xaml.
    ///
    /// Minimal code-behind — all logic in TaxInvoiceDetailViewModel.
    /// DataContext set automatically by ContentControl DataTemplate
    /// in TaxInvoiceView.xaml (same pattern as DeliveryNoteDetailView).
    ///
    /// RemoveRowCommand is bound inside the ItemsControl DataTemplate
    /// using RelativeSource AncestorType=ItemsControl — this correctly
    /// walks up to the InvSpecGroupViewModel which owns RemoveRowCommand,
    /// since the ItemsControl DataContext = InvSpecGroupViewModel.
    /// </summary>
    public partial class TaxInvoiceDetailView : UserControl
    {
        public TaxInvoiceDetailView()
        {
            InitializeComponent();
        }
    }
}