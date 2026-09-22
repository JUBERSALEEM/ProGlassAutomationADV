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

        public TaxInvoiceListView()
        {
            InitializeComponent();
        }

        // ── Tab switching (4 tabs: All / Draft / Issued / Paid) ──────────

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

        private void OnFamilyRowRightClick(object sender, MouseButtonEventArgs e)
        {
            ShowRowMenu(sender as FrameworkElement, atPointer: true);
            e.Handled = true;
        }

        // The visible More button and right-click share every original menu action.
        private void OnRowMoreClick(object sender, RoutedEventArgs e)
        {
            ShowRowMenu(sender as FrameworkElement, atPointer: false);
            e.Handled = true;
        }

        private void ShowRowMenu(FrameworkElement? fe, bool atPointer)
        {
            if (fe == null) return;
            if (fe.DataContext is not InvoiceFamilyRowViewModel family) return;
            if (Vm == null) return;

            // Select the family first
            Vm.SelectFamilyCommand.Execute(family);

            var menu = new ContextMenu
            {
                PlacementTarget = fe,
                Placement = atPointer ? System.Windows.Controls.Primitives.PlacementMode.MousePoint
                    : System.Windows.Controls.Primitives.PlacementMode.Bottom,
                Style = (Style)FindResource("Sl.ContextMenu")
            };

            // ── Open Latest Invoice ──────────────────────────────────────
            var openItem = new MenuItem
            {
                Header = "📂  Open Latest Invoice",
                FontWeight = FontWeights.SemiBold,
                Style = (Style)FindResource("Sl.MenuItem"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            openItem.Click += (_, _) =>
                Vm.OpenFamilyCommand.Execute(family);
            menu.Items.Add(openItem);

            menu.Items.Add(new Separator
            {
                Style = (Style)FindResource("Sl.MenuSeparator")
            });

            // ── Family info section (visual only, no click) ──────────────
            var infoHeader = new MenuItem
            {
                Header = $"📋 {family.ProformaId}",
                IsEnabled = false,
                Foreground = (System.Windows.Media.Brush)FindResource("SecondaryForegroundBrush"),
                Style = (Style)FindResource("Sl.MenuItem"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            menu.Items.Add(infoHeader);

            var countItem = new MenuItem
            {
                Header = $"🧾 {family.InvoiceCount} invoice(s) in family",
                IsEnabled = false,
                Style = (Style)FindResource("Sl.MenuItem"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            menu.Items.Add(countItem);

            var statusItem = new MenuItem
            {
                Header = $"📊 Status: {family.FamilyStatus}",
                IsEnabled = false,
                Style = (Style)FindResource("Sl.MenuItem"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            menu.Items.Add(statusItem);

            var progressItem = new MenuItem
            {
                Header = $"💰 {family.ProgressPercent:0.#}% invoiced " +
                         $"({family.InvoicedTotalDisplay})",
                IsEnabled = false,
                Style = (Style)FindResource("Sl.MenuItem"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            menu.Items.Add(progressItem);

            menu.Items.Add(new Separator
            {
                Style = (Style)FindResource("Sl.MenuSeparator")
            });

            // ── Refresh list ─────────────────────────────────────────────
            var refreshItem = new MenuItem
            {
                Header = "🔄  Refresh List",
                Style = (Style)FindResource("Sl.MenuItem"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            refreshItem.Click += (_, _) => Vm.RefreshCommand.Execute(null);
            menu.Items.Add(refreshItem);

            menu.IsOpen = true;

        }
    }
}