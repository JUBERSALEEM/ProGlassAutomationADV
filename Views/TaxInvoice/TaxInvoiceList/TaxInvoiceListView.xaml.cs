using ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceList;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.TaxInvoice.TaxInvoiceList
{
    /// <summary>
    /// Code-behind for TaxInvoiceListView.xaml.
    ///
    /// Handles:
    ///   • Tab switching (same click-handler pattern as DN list)
    ///   • Row left-click  → select
    ///   • Row right-click → context menu (IssueRowCommand / MarkPaidRowCommand)
    ///   • Family row left-click → open latest invoice
    ///
    /// FIX: ChangeStatusCommand removed — replaced with
    ///      IssueRowCommand and MarkPaidRowCommand (separate commands,
    ///      no ValueTuple parameter — XAML-safe).
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

        // ── Tab switching ─────────────────────────────────────────────────

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
                "TabFamily" => 4,
                _ => 0
            };

            if (Vm != null)
                Vm.SelectedTabIndex = index;
        }

        // ── Invoice row interactions ──────────────────────────────────────

        private void OnRowLeftClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not TaxInvoiceRowViewModel row) return;
            Vm?.SelectRowCommand.Execute(row);
        }

        private void OnRowRightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not TaxInvoiceRowViewModel row) return;
            if (Vm == null) return;

            // Select the row first
            Vm.SelectRowCommand.Execute(row);

            var menu = new ContextMenu();

            // Open
            var openItem = new MenuItem { Header = "📂  Open Invoice" };
            openItem.Click += (_, _) => Vm.OpenCommand.Execute(row);
            menu.Items.Add(openItem);

            menu.Items.Add(new Separator());

            // Issue — only for Draft invoices
            // FIX: IssueRowCommand (not ChangeStatusCommand with tuple)
            if (row.IsDraft)
            {
                var issueItem = new MenuItem { Header = "📤  Issue Invoice" };
                issueItem.Click += (_, _) =>
                    Vm.IssueRowCommand.Execute(row);
                menu.Items.Add(issueItem);
            }

            // Mark Paid — only for Issued invoices
            // FIX: MarkPaidRowCommand (not ChangeStatusCommand with tuple)
            if (row.IsIssued)
            {
                var paidItem = new MenuItem { Header = "✅  Mark as Paid" };
                paidItem.Click += (_, _) =>
                    Vm.MarkPaidRowCommand.Execute(row);
                menu.Items.Add(paidItem);

                var overdueItem = new MenuItem
                {
                    Header = "⚠️  Mark as Overdue"
                };
                overdueItem.Click += (_, _) =>
                {
                    // Overdue is a status change — handled via direct
                    // repository call through ListVM helper
                    // Uses IssueRowCommand pattern but for Overdue:
                    // Wire through a future MarkOverdueRowCommand if needed.
                    // For now: show info message.
                    MessageBox.Show(
                        $"To mark '{row.InvoiceNumber}' as Overdue:\n\n" +
                        "Open the invoice and change the Status dropdown " +
                        "to 'Overdue', then Save.",
                        "Mark as Overdue",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                };
                menu.Items.Add(overdueItem);
            }

            menu.Items.Add(new Separator());

            // Delete
            var deleteItem = new MenuItem
            {
                Header = "🗑️  Delete Invoice",
                Foreground = System.Windows.Media.Brushes.Firebrick
            };
            deleteItem.Click += (_, _) => Vm.DeleteCommand.Execute(row);
            menu.Items.Add(deleteItem);

            menu.IsOpen = true;
            e.Handled = true;
        }

        // ── Family row interactions ───────────────────────────────────────

        private void OnFamilyRowLeftClick(
            object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not InvoiceFamilyRowViewModel family) return;
            Vm?.OpenFamilyCommand.Execute(family);
        }
    }
}