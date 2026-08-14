using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.JobOrder.Mapper;
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
    /// BUG-85 [HIGH]: ConvertToJobOrder logic stays here (calls
    ///                JobOrderMapper.BuildFromProformaInvoice which lives in
    ///                the JobOrder service). Moving to VM would require
    ///                exposing the mapper via a service abstraction — that
    ///                is a Session 4 refactor. Wrapped in try/catch and
    ///                documented as intentional isolation.
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
                FontFamily = new global::System.Windows.Media.FontFamily("Segoe UI"),
                FontSize = 12,

                // BUG-87: anchor menu to the clicked row explicitly
                PlacementTarget = element,
                Placement = global::System.Windows.Controls.Primitives.PlacementMode.MousePoint
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

            // ══════════════════════════════════════════════════════════════
            //  Phase 9.3 — Convert to Job Order
            //  BUG-85: kept in code-behind because JobOrderMapper is in a
            //  different service namespace. Full VM refactor deferred.
            // ══════════════════════════════════════════════════════════════
            var jobOrderItem = new MenuItem
            {
                Header = "📦  Convert to Job Order",
                Padding = new Thickness(4, 6, 20, 6),
                FontWeight = FontWeights.SemiBold
            };
            jobOrderItem.Click += async (_, __) =>
            {
                await ConvertToJobOrderAsync(item, vm);
            };
            menu.Items.Add(jobOrderItem);

            menu.Items.Add(new Separator());

            var deleteItem = CreateMenuItem("🗑  Delete", vm.DeleteInvoiceCommand, item);
            deleteItem.Foreground = new global::System.Windows.Media.SolidColorBrush(
                global::System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
            menu.Items.Add(deleteItem);

            element.ContextMenu = menu;
            menu.IsOpen = true;
            e.Handled = true;
        }

        /// <summary>
        /// Phase 9.3 — Converts a Proforma Invoice to a Job Order.
        /// BUG-85: wrapped in try/catch (was already in original).
        /// Kept in code-behind because JobOrderMapper is in a separate
        /// service namespace — full MVVM refactor requires exposing
        /// a JobOrderConversionService which is out of scope for this
        /// bug-fix session.
        /// </summary>
        private async global::System.Threading.Tasks.Task ConvertToJobOrderAsync(
            SavedInvoiceListItem item,
            SavedInvoicesListViewModel vm)
        {
            if (item == null || App.Invoices == null || App.Jobs == null) return;

            try
            {
                // 1. Check if already converted
                var existing = await App.Jobs.GetBySourceProformaIdAsync(
                    item.ProformaId ?? "");
                if (existing != null && existing.Count > 0)
                {
                    var existingId = existing[0].JobOrderId;
                    var overwrite = MessageBox.Show(
                        $"A Job Order '{existingId}' already exists for invoice '{item.ProformaId}'.\n\n" +
                        $"• YES → Create another Job Order anyway\n" +
                        $"• NO → Cancel",
                        "Job Order Already Exists",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (overwrite != MessageBoxResult.Yes) return;
                }

                // 2. Fetch the full PI entity
                var piEntity = await App.Invoices.GetByIdAsync(item.Id);
                if (piEntity == null)
                {
                    MessageBox.Show(
                        "Source invoice not found.",
                        "Conversion Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // 3. Build the Job Order snapshot
                var jobSnapshot = JobOrderMapper.BuildFromProformaInvoice(piEntity);

                // 4. Check for duplicate JOB ID and auto-increment if needed
                if (await App.Jobs.ExistsByJobOrderIdAsync(jobSnapshot.JobOrderId))
                {
                    jobSnapshot.JobOrderId = await App.Jobs.SuggestNextIdAsync(
                        jobSnapshot.JobOrderId);
                }

                // 5. Build the EF entity from the snapshot
                var jobEntity = JobOrderMapper.BuildEntityFromSnapshot(
                    jobSnapshot,
                    piEntity.GrandTotalVatInclusive);

                // 6. Save to joborders.db
                await App.Jobs.InsertAsync(jobEntity);

                // 7. Success message
                var result = MessageBox.Show(
                    $"Job Order '{jobEntity.JobOrderId}' created successfully!\n\n" +
                    $"Source: {item.ProformaId}\n" +
                    $"Customer: {item.CustomerName}\n" +
                    $"Value: AED {piEntity.GrandTotalVatInclusive:N0}\n" +
                    $"Status: Draft · Priority: Medium\n\n" +
                    $"Navigate to Job Orders module now?",
                    "Conversion Successful",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    MessageBox.Show(
                        "Open the Job Orders module from the sidebar menu " +
                        "to view and manage your new job order.",
                        "Navigation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to convert to Job Order:\n\n{ex.Message}",
                    "Conversion Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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