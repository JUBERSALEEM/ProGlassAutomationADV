// ═══════════════════════════════════════════════════════════════════════
// FILE: SheetStoreViewModel.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Win32;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.SheetStore;
using ProGlassAutomation.Services.SheetStore.SheetStoreCsv;
using ProGlassAutomation.Services.SheetStore.SheetStoreExcel;
using ProGlassAutomation.Services.SheetStore.SheetStorePdf;
using ProGlassAutomation.Services.SheetStore.SheetStoreHistory;
using ProGlassAutomation.Services.ProformaInvoice;
using ProGlassAutomation.Views.SheetStore;
using ProGlassAutomation.Views.SheetStore.SheetStoreBuy;
using ProGlassAutomation.Views.SheetStore.SheetStoreCsv;
using ProGlassAutomation.Views.SheetStore.SheetStoreHistory;
using ProGlassAutomation.Views.SheetStore.SheetStoreUse;
using ProGlassAutomation.ViewModels.SheetStore.SheetStoreBuy;
using ProGlassAutomation.ViewModels.SheetStore.SheetStoreCsv;
using ProGlassAutomation.ViewModels.SheetStore.SheetStoreHistory;
using ProGlassAutomation.ViewModels.SheetStore.SheetStoreUse;
using ProGlassAutomation.ViewModels.Shared;
using System;
using ProGlassAutomation.Utilities;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace ProGlassAutomation.ViewModels.SheetStore
{
        public partial class SheetStoreViewModel : ViewModelBase,
        ProGlassAutomation.Agent.Context.IAgentContextAware
    {
            private async Task DeleteAsync(GlassSheet? sheet)
        {
            if (sheet == null) return;

            if (!PermissionService.Require(
                AppPermissions.SheetStoreDelete, "delete a glass sheet"))
                return;

            var result = _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.DeleteThisGlassSheetX"), sheet.Category, sheet.ThicknessDisplay, sheet.Color, sheet.SizeDisplay, sheet.Stock, sheet.TotalPurchaseValue),
                LocalizationService.Instance.T("Msg.CapConfirmDelete"), MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var desc = DescribeSheet(sheet);
                    var stockLost = sheet.Stock;

                    // Stock snapshot, deleted row, and movement history are
                    // committed by one repository transaction.
                    await _repo.DeleteAsync(sheet.Id, sheet.RowVersion);

                    try
                    {
                        AuditService.Log(AuditSections.SheetStore, "Sheet deleted",
                            $"{desc} — {stockLost} sheets removed",
                            "SHEET", sheet.Id.ToString());
                    }
                    catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("if suppressed");
            }

                    await LoadAsync();
                }
                catch (Exception ex) { ShowError("Could not delete.\n\n" + ExceptionMessages.Deep(ex)); }
            }
        }

        // ============ BUY ============

        private Task BuyAsync(GlassSheet? sheet)
        {
            if (sheet == null) return Task.CompletedTask;
            OpenSheetWorkspace(sheet, SheetDetailWindowViewModel.TabBuy);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Records a purchase from an ALREADY-COMPLETED Buy form.
        /// Shared by the modal dialog and the workspace Buy tab.
        /// </summary>
        internal async Task CommitBuyAsync(GlassSheet sheet, BuyDialogViewModel vm)
        {
            if (vm.DialogResult == true && !vm.IsValid)
            {
                ShowError("Purchase not recorded — the quantity was not valid.");
                return;
            }

            if (vm.DialogResult == true && vm.Quantity > 0)
            {
                try
                {
                    await _repo.BuyStockAsync(
                        sheet.Id, vm.Quantity, source: "Sheet Store purchase");

                    try
                    {
                        AuditService.Log(AuditSections.SheetStore, "Stock purchased",
                            $"{DescribeSheet(sheet)} — +{vm.Quantity} sheets",
                            "SHEET", sheet.Id.ToString());
                    }
                    catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("if suppressed");
            }

                    await LoadAsync();
                    RaiseStockLedgerChanged();
                }
                catch (Exception ex) { ShowError("Could not add stock.\n\n" + ExceptionMessages.Deep(ex)); }
            }
        }

        // ============ USE ============

        private Task UseAsync(GlassSheet? sheet)
        {
            if (sheet == null) return Task.CompletedTask;

            if (sheet.Balance <= 0)
            {
                ShowError("No stock available to use.");
                return Task.CompletedTask;
            }

            OpenSheetWorkspace(sheet, SheetDetailWindowViewModel.TabUse);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Records consumption from an ALREADY-COMPLETED Use form.
        /// Shared by the modal dialog and the workspace Use tab.
        /// </summary>
        internal async Task CommitUseAsync(GlassSheet sheet, UseDialogViewModel vm)
        {
            if (vm.DialogResult == true && !vm.IsValid)
            {
                ShowError("Consumption not recorded — the quantity was not valid.");
                return;
            }

            if (vm.DialogResult == true && vm.Quantity > 0)
            {
                // Shared consumption core (repo.UseStockAsync + history +
                // audit) — the SAME path Step 6 auto-use takes, so the
                // manual dialog and the wizard can never drift apart.
                var link = await SheetStockLinkService.CommitConsumptionAsync(
                    sheet, vm.Quantity,
                    jobReference: vm.JobOrderReference ?? "",
                    customerName: vm.CustomerName ?? "",
                    projectName: vm.ProjectName ?? "",
                    salesman: vm.Salesman ?? "",
                    origin: "Sheet Store (manual)").ConfigureAwait(true);

                if (!link.Success)
                {
                    ShowError(link.Message);
                    return;
                }

                try { await LoadAsync(); }
                catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("if suppressed");
            }

                RaiseStockLedgerChanged();
            }
        }

        // ============ HISTORY ============

        private void ShowHistory()
        {
            var vm = new HistoryViewModel();
            _dialogs.ShowDialog<HistoryView>(vm);
        }
    }
}
