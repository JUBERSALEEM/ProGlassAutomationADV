// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/SheetStore/SheetStoreViewModel.Editor.cs
// MODULE: Inventory (Sheet Store) — full-screen Add/Edit workspace host
// ADDED: 2026-09-06 (ported 2026-09-08)
//
// ADD NEW, EDIT, BUY and USE all open the full-screen detail workspace.
// Persistence still funnels through PersistNewSheetAsync /
// PersistEditedSheetAsync in Part1 and CommitBuyAsync / CommitUseAsync
// in Part2 — the only writers to the ledger.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Views.SheetStore;
using System;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.SheetStore
{
    public partial class SheetStoreViewModel
    {
        private ICommand? _openAddPanelCommand;
        public ICommand OpenAddPanelCommand =>
            _openAddPanelCommand ??= new RelayCommand(_ =>
            {
                if (IsConsumablesView) AddItemCommand.Execute(null);
                else OpenSheetWorkspace(null);
            });

        private ICommand? _openEditPanelCommand;
        public ICommand OpenEditPanelCommand =>
            _openEditPanelCommand ??= new RelayCommand(
                p => OpenSheetWorkspace(p as GlassSheet ?? SelectedSheet, SheetDetailWindowViewModel.TabEdit),
                p => (p as GlassSheet ?? SelectedSheet) != null);

        /// <summary>
        /// Edit binds a COPY, never the grid's live instance. Editing the
        /// bound row directly mutates the visible list as the operator
        /// types and leaves those edits behind on cancel.
        /// </summary>
        private static GlassSheet Clone(GlassSheet s) => new()
        {
            Id = s.Id,
            Category = s.Category,
            ThicknessMm = s.ThicknessMm,
            Color = s.Color,
            WidthMm = s.WidthMm,
            HeightMm = s.HeightMm,
            Supplier = s.Supplier,
            PurchasePricePerSqm = s.PurchasePricePerSqm,
            SellPricePerSqm = s.SellPricePerSqm,
            Stock = s.Stock,
            Used = s.Used,
            CreatedAt = s.CreatedAt,
            LastUpdated = s.LastUpdated,
            LastPurchaseDate = s.LastPurchaseDate
        };

        /// <summary>
        /// Opens the full-screen workspace. A null sheet means ADD NEW.
        /// <paramref name="initialTab"/> selects Buy / Use / Edit so the
        /// list's BUY, USE and EDIT actions land on that tab instead of
        /// opening a second modal.
        /// </summary>
        private void OpenSheetWorkspace(GlassSheet? sheet, string? initialTab = null)
        {
            bool isNew = sheet == null;

            if (!PermissionService.Require(
                    isNew ? AppPermissions.SheetStoreCreate : AppPermissions.SheetStoreEdit,
                    isNew ? "add a glass sheet" : "edit a glass sheet"))
                return;

            try
            {
                var working = isNew ? null : Clone(sheet!);

                var vm = new SheetDetailWindowViewModel(
                    working, _historyRepo, buyCommand: null, useCommand: null);

                if (!isNew && !string.IsNullOrEmpty(initialTab))
                    vm.ActiveKey = initialTab;

                vm.SaveRequested += async (_, result) =>
                {
                    if (result == null) return;
                    if (isNew) await PersistNewSheetAsync(result);
                    else await PersistEditedSheetAsync(result, sheet!);
                };

                vm.BuyRequested += async (_, form) =>
                {
                    await CommitBuyAsync(sheet!, form);
                    vm.RefreshAfterMovement();
                };

                vm.UseRequested += async (_, form) =>
                {
                    await CommitUseAsync(sheet!, form);
                    vm.RefreshAfterMovement();
                };

                _dialogs.ShowDialog<SheetDetailWindow>(vm);

                AsyncUtility.SafeFireAndForget(LoadAsync, "SheetStore ReloadAfterDetail");
            }
            catch (Exception ex)
            {
                ShowError("Could not open the sheet workspace.\n\n" + ExceptionMessages.Deep(ex));
            }
        }
    }
}
