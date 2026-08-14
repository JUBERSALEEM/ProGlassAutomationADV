using ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceList;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.TaxInvoice.TaxInvoiceList
{
    /// <summary>
    /// Code-behind for TaxInvoiceListView.xaml.
    ///
    /// RESTRUCTURED for family-grouped list (mirrors DN List pattern):
    ///   • All 4 tabs show family rows (no more flat invoice list)
    ///   • Single click on family → select (highlights with indigo border)
    ///   • Double-click / Open button → navigate to detail view
    ///   • Right-click → context menu (Open, Refresh)
    ///
    /// The individual invoices within a family are only visible inside
    /// the Detail view's right sidebar (Invoice Family history).
    /// </summary>
    public partial class TaxInvoiceListView : UserControl
    {
        private TaxInvoiceListViewModel? Vm =>
            DataContext as TaxInvoiceListViewModel;

        private Button? _activeTab;

        public TaxInvoiceListView()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _activeTab = TabAll;
        }

        // ── Tab switching (4 tabs: All / Draft / Issued / Paid) ──────────

        private void OnTabClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button clicked) return;

            if (_activeTab != null)
                _activeTab.Style = (Style)Resources["InvTabInactiveStyle"];

            clicked.Style = (Style)Resources["InvTabActiveStyle"];
            _activeTab = clicked;

            int index = clicked.Name switch
            {
                "TabAll" => 0,
                "TabDraft" => 1,
                "TabIssued" => 2,
                "TabPaid" => 3,
                _ => 0
            };

            if (Vm != null)
                Vm.SelectedTabIndex = index;
        }

        // ── Family row: left-click selects, second click opens ───────────

        private void OnFamilyRowLeftClick(
            object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not InvoiceFamilyRowViewModel family) return;
            if (Vm == null) return;

            // Toggle selection on click
            Vm.SelectFamilyCommand.Execute(family);

            // Double-click opens the family (opens latest invoice in detail)
            if (e.ClickCount >= 2)
                Vm.OpenFamilyCommand.Execute(family);
        }

        // ── Family row: right-click context menu ─────────────────────────

        private void OnFamilyRowRightClick(
            object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not InvoiceFamilyRowViewModel family) return;
            if (Vm == null) return;

            // Select the family first
            Vm.SelectFamilyCommand.Execute(family);

            var menu = new ContextMenu();

            // ── Open Latest Invoice ──────────────────────────────────────
            var openItem = new MenuItem
            {
                Header = "📂  Open Latest Invoice"
            };
            openItem.Click += (_, _) =>
                Vm.OpenFamilyCommand.Execute(family);
            menu.Items.Add(openItem);

            menu.Items.Add(new Separator());

            // ── Family info section (visual only, no click) ──────────────
            var infoHeader = new MenuItem
            {
                Header = $"📋 {family.ProformaId}",
                IsEnabled = false,
                Foreground = global::System.Windows.Media.Brushes.DarkSlateGray
            };
            menu.Items.Add(infoHeader);

            var countItem = new MenuItem
            {
                Header = $"🧾 {family.InvoiceCount} invoice(s) in family",
                IsEnabled = false
            };
            menu.Items.Add(countItem);

            var statusItem = new MenuItem
            {
                Header = $"📊 Status: {family.FamilyStatus}",
                IsEnabled = false
            };
            menu.Items.Add(statusItem);

            var progressItem = new MenuItem
            {
                Header = $"💰 {family.ProgressPercent:0.#}% invoiced " +
                         $"({family.InvoicedTotalDisplay})",
                IsEnabled = false
            };
            menu.Items.Add(progressItem);

            menu.Items.Add(new Separator());

            // ── Refresh list ─────────────────────────────────────────────
            var refreshItem = new MenuItem { Header = "🔄  Refresh List" };
            refreshItem.Click += (_, _) => Vm.RefreshCommand.Execute(null);
            menu.Items.Add(refreshItem);

            menu.IsOpen = true;
            e.Handled = true;
        }
    }
}