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
                    var valueLost = sheet.TotalPurchaseValue;

                    // Primary DB write — this is the source of truth.
                    await _repo.DeleteAsync(sheet.Id);

                    // Secondary logging must never block the reload: if it
                    // throws, the row is still gone from the DB and the list
                    // must still refresh so the deleted row disappears.
                    try
                    {
                        await _historyRepo.LogAsync(sheet.Id, desc, "Deleted",
                            $"Removed from inventory. Lost {stockLost} sheets worth {valueLost:N2} AED.",
                            stockLost, valueLost);
                    }
                    catch { /* history is best-effort */ }
                    try
                    {
                        AuditService.Log(AuditSections.SheetStore, "Sheet deleted",
                            $"{desc} — {stockLost} sheets removed",
                            "SHEET", sheet.Id.ToString());
                    }
                    catch { /* audit is best-effort */ }

                    await LoadAsync();
                }
                catch (Exception ex) { ShowError("Could not delete.\n\n" + ExceptionMessages.Deep(ex)); }
            }
        }

        // ============ BUY ============

        private async Task BuyAsync(GlassSheet? sheet)
        {
            if (sheet == null) return;

            if (!PermissionService.Require(
                AppPermissions.SheetStoreEdit, "buy stock"))
                return;

            var vm = new BuyDialogViewModel(sheet);
            _dialogs.ShowDialog<BuyDialogView>(vm);

            if (vm.DialogResult == true && vm.Quantity > 0)
            {
                try
                {
                    var oldStock = sheet.Stock;
                    await _repo.BuyStockAsync(sheet.Id, vm.Quantity);
                    var value = vm.Quantity * sheet.SqmPerSheet * sheet.PurchasePricePerSqm;

                    try
                    {
                        await _historyRepo.LogAsync(
                            sheet.Id, DescribeSheet(sheet), "Bought",
                            $"Purchased {vm.Quantity} sheets. Stock: {oldStock} → {oldStock + vm.Quantity}. Value: {value:N2} AED.",
                            vm.Quantity, value);
                    }
                    catch { /* best-effort */ }
                    try
                    {
                        AuditService.Log(AuditSections.SheetStore, "Stock purchased",
                            $"{DescribeSheet(sheet)} — +{vm.Quantity} sheets",
                            "SHEET", sheet.Id.ToString());
                    }
                    catch { /* best-effort */ }

                    await LoadAsync();
                }
                catch (Exception ex) { ShowError("Could not add stock.\n\n" + ExceptionMessages.Deep(ex)); }
            }
        }

        // ============ USE ============

        private async Task UseAsync(GlassSheet? sheet)
        {
            if (sheet == null) return;

            if (!PermissionService.Require(
                AppPermissions.SheetStoreEdit, "use stock"))
                return;

            if (sheet.Balance <= 0)
            {
                ShowError("No stock available to use.");
                return;
            }

            var vm = new UseDialogViewModel(sheet);
            _dialogs.ShowDialog<UseDialogView>(vm);

            if (vm.DialogResult == true && vm.Quantity > 0)
            {
                try
                {
                    var oldUsed = sheet.Used;
                    await _repo.UseStockAsync(sheet.Id, vm.Quantity);
                    var value = vm.Quantity * sheet.SqmPerSheet * sheet.SellPricePerSqm;

                    var jobRef = string.IsNullOrWhiteSpace(vm.JobOrderReference)
                        ? ""
                        : $" [Job: {vm.JobOrderReference.Trim()}]";

                    try
                    {
                        await _historyRepo.LogAsync(
                            sheet.Id, DescribeSheet(sheet), "Used",
                            $"Consumed {vm.Quantity} sheets.{jobRef} Used: {oldUsed} → {oldUsed + vm.Quantity}. Value: {value:N2} AED.",
                            vm.Quantity, value,
                            documentReference: vm.JobOrderReference,
                            customerName: vm.CustomerName,
                            projectName: vm.ProjectName,
                            salesman: vm.Salesman,
                            panePosition: vm.PanePosition);
                    }
                    catch { /* best-effort */ }
                    try
                    {
                        AuditService.Log(AuditSections.SheetStore, "Stock used",
                            $"{DescribeSheet(sheet)} — {vm.Quantity} sheets{jobRef}",
                            "SHEET", sheet.Id.ToString());
                    }
                    catch { /* best-effort */ }

                    await LoadAsync();
                }
                catch (Exception ex) { ShowError(ExceptionMessages.Deep(ex)); }
            }
        }

        // ============ HISTORY ============

        private void ShowHistory()
        {
            var vm = new HistoryViewModel();

            // 2026-09-06 — undoing a movement from inside History restores
            // stock, so the grid behind this dialog is instantly stale.
            // Reload on the event rather than unconditionally after close:
            // the common case (operator just browsing history) then costs
            // nothing. Unsubscribed straight after ShowDialog returns —
            // the VM is per-dialog, but the handler captures `this`.
            void OnReversed() =>
                AsyncUtility.SafeFireAndForget(LoadAsync, "SheetStore ReloadAfterUndo");

            vm.MovementReversed += OnReversed;
            try
            {
                _dialogs.ShowDialog<HistoryView>(vm);
            }
            finally
            {
                vm.MovementReversed -= OnReversed;
            }
        }
    }
}
