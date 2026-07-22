using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.ViewModels.ProformaInvoice.SavedInvoicesList;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.ProformaInvoice.SavedInvoicesList
{
    public partial class SavedInvoicesListView : UserControl
    {
        public SavedInvoicesListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Builds a styled right-click context menu dynamically.
        /// Uses code-behind because ContextMenu has its own visual tree
        /// and WPF XAML bindings for commands don't resolve correctly.
        /// </summary>
        private void OnRowRightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element) return;
            if (element.DataContext is not SavedInvoiceListItem item) return;
            if (DataContext is not SavedInvoicesListViewModel vm) return;

            var menu = new ContextMenu
            {
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontSize = 12
            };

            menu.Items.Add(CreateMenuItem("📂  Open & Edit", vm.LoadInvoiceCommand, item, bold: true));
            menu.Items.Add(CreateMenuItem("📋  Duplicate", vm.DuplicateInvoiceCommand, item));
            menu.Items.Add(new Separator());

            menu.Items.Add(CreateMenuItem("📄  Export PDF", vm.ExportPdfFromListCommand, item));
            menu.Items.Add(CreateMenuItem("📊  Export Excel", vm.ExportExcelFromListCommand, item));
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
            //  Phase 9.3 — Convert to Job Order (now functional)
            //  Creates a new Job Order from this PI's snapshot + saves
            //  to joborders.db. Shows success message with the new JOB ID.
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
            deleteItem.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
            menu.Items.Add(deleteItem);

            element.ContextMenu = menu;
            menu.IsOpen = true;
            e.Handled = true;
        }

        /// <summary>
        /// Phase 9.3 — Converts a Proforma Invoice to a Job Order.
        /// 1. Fetches the full PI entity from proformainvoices.db
        /// 2. Uses JobOrderMapper to deep-clone the PI snapshot into a
        ///    new JobOrderSnapshot with default production fields
        /// 3. Checks for duplicate JOB IDs (auto-increments if needed)
        /// 4. Saves to joborders.db via App.Jobs
        /// 5. Shows success message with the new JOB-YYYY-NNN ID
        /// </summary>
        private async System.Threading.Tasks.Task ConvertToJobOrderAsync(
            SavedInvoiceListItem item,
            SavedInvoicesListViewModel vm)
        {
            if (item == null || App.Invoices == null || App.Jobs == null) return;

            try
            {
                // 1. Check if already converted
                var existing = await App.Jobs.GetBySourceProformaIdAsync(item.ProformaId ?? "");
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

                // 3. Build the Job Order snapshot (deep-clones the PI snapshot)
                var jobSnapshot = JobOrderMapper.BuildFromProformaInvoice(piEntity);

                // 4. Check for duplicate JOB ID and auto-increment if needed
                if (await App.Jobs.ExistsByJobOrderIdAsync(jobSnapshot.JobOrderId))
                {
                    jobSnapshot.JobOrderId = await App.Jobs.SuggestNextIdAsync(jobSnapshot.JobOrderId);
                }

                // 5. Build the EF entity from the snapshot
                //    Pass the PI's GrandTotalVatInclusive as override since
                //    the snapshot POCO doesn't store computed grand total
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

                // Optional: navigate to Job Order module
                // (We can't directly navigate from here — would need a callback
                //  wired through MainViewModel. For now just refresh the list.)
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

        private static MenuItem CreateMenuItem(string header, ICommand command, object parameter, bool bold = false)
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

        private MenuItem CreateStatusItem(string status, SavedInvoiceListItem item, SavedInvoicesListViewModel vm)
        {
            var menuItem = new MenuItem
            {
                Header = status,
                Tag = status,
                Padding = new Thickness(4, 4, 20, 4)
            };

            menuItem.Click += async (s, e) =>
            {
                if (App.Invoices == null) return;
                try
                {
                    var entity = await App.Invoices.GetByIdAsync(item.Id);
                    if (entity == null) return;

                    entity.Status = status;
                    await App.Invoices.UpdateAsync(entity);
                    await vm.LoadInvoicesAsync();
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Failed to change status: {ex.Message}",
                        "Status Change Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            return menuItem;
        }
    }
}