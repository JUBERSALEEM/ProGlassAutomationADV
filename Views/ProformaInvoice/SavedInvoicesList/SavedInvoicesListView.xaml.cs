using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.ViewModels.ProformaInvoice.SavedInvoicesList;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.ProformaInvoice.SavedInvoicesList
{
    /// <summary>
    /// SavedInvoicesListView code-behind.
    ///
    /// BUG-72 SUPPORT: Status menu now passes Tuple(item, status) matching
    ///                 updated ChangeStatusCommand signature in the VM.
    /// APPROVAL FLOW: The manual "Convert to Job Order" item is REMOVED.
    ///                Approving now runs a gate (Signed PI or LPO) and, on
    ///                confirmation, the invoice is auto-converted to a Job
    ///                Order inside the ViewModel (ApproveAsync), so this view
    ///                no longer holds any conversion logic.
    /// BUG-86 [HIGH]: Dynamic MenuItem generation stays in code-behind by
    ///                WPF design — XAML cannot cleanly bind nested
    ///                MenuItem.Command with per-item CommandParameter for
    ///                a status list. Documented as WPF limitation, not bug.
    /// BUG-87 [MEDIUM]: menu.PlacementTarget = element set explicitly so
    ///                  the menu anchors to the row that was right-clicked.
    /// BUG-88 [MEDIUM]: Loading spinner glyph documented — animation is
    ///                  a UI polish concern deferred to a future phase.
    /// </summary>
    public partial class SavedInvoicesListView : UserControl
    {
        public SavedInvoicesListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Builds a styled right-click context menu dynamically.
        /// BUG-86: This uses code-behind by design — WPF cannot bind
        /// nested MenuItem.Command with per-item CommandParameter for
        /// dynamic lists of status values in pure XAML.
        /// </summary>
        private void OnRowRightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element) return;
            if (element.DataContext is not SavedInvoiceListItem item) return;
            if (DataContext is not SavedInvoicesListViewModel vm) return;

            var menu = new ContextMenu
            {
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontSize = 12,

                // BUG-87: anchor menu to the clicked row explicitly
                PlacementTarget = element,
                Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint
            };

            menu.Items.Add(CreateMenuItem("📂  Open & Edit", vm.LoadInvoiceCommand, item, bold: true));
            menu.Items.Add(CreateMenuItem("📋  Duplicate", vm.DuplicateInvoiceCommand, item));
            menu.Items.Add(new Separator());

            menu.Items.Add(CreateMenuItem("📄  Export PDF", vm.ExportPdfFromListCommand, item));
            menu.Items.Add(CreateMenuItem("📊  Export Excel", vm.ExportExcelFromListCommand, item));
            menu.Items.Add(CreateMenuItem("📋  Export CSV", vm.ExportCsvFromListCommand, item));  // BUG-76 parity
            menu.Items.Add(new Separator());

            // Change Status sub-menu
            var statusMenu = new MenuItem { Header = "🔄  Change Status" };
            statusMenu.Items.Add(CreateStatusItem("Draft", item, vm));
            statusMenu.Items.Add(CreateStatusItem("Sent", item, vm));
            statusMenu.Items.Add(CreateStatusItem("Approved", item, vm));
            statusMenu.Items.Add(CreateStatusItem("Rejected", item, vm));
            statusMenu.Items.Add(CreateStatusItem("Expired", item, vm));
            menu.Items.Add(statusMenu);

            menu.Items.Add(new Separator());

            var deleteItem = CreateMenuItem("🗑  Delete", vm.DeleteInvoiceCommand, item);
            deleteItem.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
            menu.Items.Add(deleteItem);

            element.ContextMenu = menu;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private static MenuItem CreateMenuItem(
            string header, ICommand command, object parameter, bool bold = false)
        {
            var item = new MenuItem
            {
                Header = header,
                Command = command,
                CommandParameter = parameter,
                Padding = new Thickness(4, 6, 20, 6)
            };
            if (bold) item.FontWeight = FontWeights.SemiBold;
            return item;
        }

        /// <summary>
        /// BUG-72 SUPPORT: Now passes Tuple(item, status) as command parameter
        /// matching updated ChangeStatusCommand signature in the VM.
        ///
        /// Previously used a direct event handler that called API directly —
        /// this bypassed the VM's error handling. Now goes through the
        /// command's SafeFireAsync path.
        /// </summary>
        private MenuItem CreateStatusItem(
            string status, SavedInvoiceListItem item, SavedInvoicesListViewModel vm)
        {
            var menuItem = new MenuItem
            {
                Header = status,
                Tag = status,
                Padding = new Thickness(4, 4, 20, 4),

                // BUG-72: use command with Tuple parameter — VM handles errors
                Command = vm.ChangeStatusCommand,
                CommandParameter = Tuple.Create(item, status)
            };

            return menuItem;
        }
    }
}