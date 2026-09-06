// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/SheetStore/SheetStoreViewModel.Consumables.cs
// MODULE: Inventory — non-glass consumables surface
// ADDED: 2026-09-06
//
// WHY THIS EXISTS
//
// The previous phase shipped the whole consumables DATA layer —
// InventoryItem, InventoryItemRepository, migration 1003, DI
// registration — with NO UI. Spacer bar, silicone, desiccant, argon and
// corner keys became stockable in the database and stayed invisible in
// the app. That is worse than not shipping it: the schema moved, the
// feature did not.
//
// This is the missing surface.
//
// WHY A TYPE SWITCH RATHER THAN A SECOND MODULE
//
// Glass and consumables are the same job — "what stock do we hold, what
// is it worth, what is running out". A second nav entry would split one
// mental task across two screens and duplicate the filter/search/export
// chrome. Instead the module gains a GLASS SHEETS | CONSUMABLES switch
// that swaps the list beneath, reusing the same header, the same rail
// and the same actions.
//
// This mirrors SalesReportViewModel.ActiveKey, the established in-house
// pattern for swapping content inside one module without touching the
// navigation stack.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.SheetStore;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.SheetStore
{
    public partial class SheetStoreViewModel
    {
        private readonly IInventoryItemRepository _itemRepo = new InventoryItemRepository();

        private List<InventoryItem> _allItems = new();

        /// <summary>Filtered consumables bound by the list.</summary>
        public BulkObservableCollection<InventoryItem> Items { get; } = new();

        // ── Which list is on screen ────────────────────────────────────

        private string _stockKind = "Glass";
        /// <summary>
        /// "Glass" or "Consumables". Drives which list, which stat strip
        /// and which actions are shown.
        /// </summary>
        public string StockKind
        {
            get => _stockKind;
            set
            {
                if (_stockKind == value) return;
                _stockKind = value;

                // Selections do not survive the switch: a GlassSheet and an
                // InventoryItem are different types, and leaving one
                // selected behind the other's list would let a row action
                // fire against an invisible item.
                SelectedSheet = null;
                SelectedItem = null;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGlassView));
                OnPropertyChanged(nameof(IsConsumablesView));
                OnPropertyChanged(nameof(HasItems));
                OnPropertyChanged(nameof(IsItemSelected));
            }
        }

        public bool IsGlassView => StockKind == "Glass";
        public bool IsConsumablesView => StockKind == "Consumables";

        private ICommand? _showGlassCommand;
        public ICommand ShowGlassCommand =>
            _showGlassCommand ??= new RelayCommand(_ => StockKind = "Glass");

        private ICommand? _showConsumablesCommand;
        public ICommand ShowConsumablesCommand =>
            _showConsumablesCommand ??= new RelayCommand(_ => StockKind = "Consumables");

        // ── Selection ──────────────────────────────────────────────────

        private InventoryItem? _selectedItem;
        public InventoryItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsItemSelected));
                OnPropertyChanged(nameof(ItemSelectionLabel));
                OnPropertyChanged(nameof(IsDetailPanelVisible));
            }
        }

        public bool IsItemSelected => _selectedItem != null;

        public string ItemSelectionLabel => _selectedItem == null
            ? string.Empty
            : $"{_selectedItem.CategoryDisplay} • {_selectedItem.UomDisplay}";

        private ICommand? _selectItemCommand;
        /// <summary>Empty-state gate for the consumables list.</summary>
        public bool HasNoItems => Items.Count == 0;

        public ICommand SelectItemCommand =>
            _selectItemCommand ??= new RelayCommand(p =>
            {
                if (p is InventoryItem item) SelectedItem = item;
            });

        public bool HasItems => Items.Count > 0;
        public bool NoItems => Items.Count == 0 && !IsBusy;

        // ── Aggregates (consumables) ───────────────────────────────────

        public int ItemCount => Items.Count;
        public decimal ItemsStockValue => Items.Sum(i => i.BalanceCostValue);
        public decimal ItemsUsedValue => Items.Sum(i => i.UsedCostValue);

        /// <summary>
        /// How many consumables have fallen to or below their reorder
        /// level. This is the number that stops a production line, so it
        /// gets its own badge in the header rather than living in a report
        /// nobody opens.
        /// </summary>
        public int LowStockCount => Items.Count(i => i.IsLowStock);
        public bool HasLowStock => LowStockCount > 0;

        // ── Load ───────────────────────────────────────────────────────

        /// <summary>
        /// Loads consumables and applies the shared search box.
        ///
        /// Filtering happens in memory because this table holds item TYPES
        /// (tens of rows), not movements, and IsLowStock/Balance are
        /// [NotMapped] computed properties EF cannot translate to SQL.
        /// </summary>
        internal async Task LoadItemsAsync()
        {
            try
            {
                _allItems = await _itemRepo.GetAllAsync();
                ApplyItemFilters();
            }
            catch (Exception ex)
            {
                ShowError("Could not load inventory items.\n\n" + ExceptionMessages.Deep(ex));
            }
        }

        internal void ApplyItemFilters()
        {
            IEnumerable<InventoryItem> q = _allItems;

            var term = SearchText?.Trim();
            if (!string.IsNullOrWhiteSpace(term))
            {
                q = q.Where(i =>
                    (i.Name ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (i.Specification ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (i.PartNumber ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (i.Supplier ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    i.CategoryDisplay.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            // Low stock first — the rows that need action should not be
            // buried under alphabetical order.
            var ordered = q.OrderByDescending(i => i.IsLowStock)
                           .ThenBy(i => i.Category)
                           .ThenBy(i => i.Name)
                           .ToList();

            // Same three-call idiom the glass list uses (Part1
            // ApplyFiltersAsync): silent clear + silent bulk add + ONE
            // Reset, so a large refill costs one notification instead of
            // one per row.
            Items.ClearSilent();
            Items.AddRangeSilent(ordered);
            Items.RaiseReset();

            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(NoItems));
            OnPropertyChanged(nameof(ItemCount));
            OnPropertyChanged(nameof(HasNoItems));
            OnPropertyChanged(nameof(ItemsStockValue));
            OnPropertyChanged(nameof(ItemsUsedValue));
            OnPropertyChanged(nameof(LowStockCount));
            OnPropertyChanged(nameof(HasLowStock));
        }

        // ── Actions ────────────────────────────────────────────────────

        private ICommand? _addItemCommand;
        public ICommand AddItemCommand =>
            _addItemCommand ??= new RelayCommand(
                _ => OpenItemEditor(null));

        private ICommand? _editItemCommand;
        public ICommand EditItemCommand =>
            _editItemCommand ??= new RelayCommand(
                p => OpenItemEditor(p as InventoryItem ?? SelectedItem),
                p => (p as InventoryItem ?? SelectedItem) != null);

        private ICommand? _receiveItemCommand;
        public ICommand ReceiveItemCommand =>
            _receiveItemCommand ??= new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(
                        () => MoveItemAsync(p as InventoryItem ?? SelectedItem, receive: true),
                        "Inventory ReceiveItem"),
                p => (p as InventoryItem ?? SelectedItem) != null);

        private ICommand? _consumeItemCommand;
        public ICommand ConsumeItemCommand =>
            _consumeItemCommand ??= new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(
                        () => MoveItemAsync(p as InventoryItem ?? SelectedItem, receive: false),
                        "Inventory ConsumeItem"),
                p => (p as InventoryItem ?? SelectedItem) != null);

        private ICommand? _deleteItemCommand;
        public ICommand DeleteItemCommand =>
            _deleteItemCommand ??= new RelayCommand(
                p => AsyncUtility.SafeFireAndForget(
                        () => DeleteItemAsync(p as InventoryItem ?? SelectedItem),
                        "Inventory DeleteItem"),
                p => (p as InventoryItem ?? SelectedItem) != null);

        private async Task MoveItemAsync(InventoryItem? item, bool receive)
        {
            if (item == null) return;

            if (!PermissionService.Require(
                    AppPermissions.SheetStoreEdit,
                    receive ? "receive inventory stock" : "consume inventory stock"))
                return;

            var vm = new SheetStoreItems.ItemMovementViewModel(item, receive);
            _dialogs.ShowDialog<Views.SheetStore.SheetStoreItems.ItemMovementView>(vm);

            if (vm.DialogResult != true) return;

            // The dialog blocks confirm while invalid, so arriving here with
            // bad input means the UI was bypassed. Report rather than drop
            // it silently — the lesson from the 0-quantity bug.
            if (!vm.IsValid)
            {
                ShowError(receive
                    ? "Receipt not recorded — the quantity was not valid."
                    : "Consumption not recorded — the quantity was not valid.");
                return;
            }

            try
            {
                if (receive)
                    await _itemRepo.ReceiveAsync(item.Id, vm.Quantity, vm.UnitPrice);
                else
                    await _itemRepo.ConsumeAsync(item.Id, vm.Quantity);

                try
                {
                    AuditService.Log(
                        AuditSections.SheetStore,
                        receive ? "Inventory received" : "Inventory consumed",
                        $"{item.FullDescription} — {vm.Quantity:N2} {item.UomDisplay}",
                        "ITEM", item.Id.ToString());
                }
                catch { /* best-effort */ }

                await LoadItemsAsync();
            }
            catch (InvalidOperationException ex)
            {
                // Deliberate business refusal (over-issue, zero) — show the
                // reason verbatim, it is an answer not a crash.
                ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                ShowError("Could not update stock.\n\n" + ExceptionMessages.Deep(ex));
            }
        }

        private async Task DeleteItemAsync(InventoryItem? item)
        {
            if (item == null) return;

            if (!PermissionService.Require(
                    AppPermissions.SheetStoreDelete, "delete an inventory item"))
                return;

            // Refuse to delete anything with recorded consumption: the
            // movement history would point at a row that no longer exists.
            if (item.Used > 0)
            {
                ShowError(
                    $"{item.Name} cannot be deleted — {item.Used:N2} {item.UomDisplay} " +
                    "is already recorded as used. Set its stock to zero instead.");
                return;
            }

            var confirm = _dialogs.Show(
                $"Delete {item.FullDescription}?\n\nThis cannot be undone.",
                "Delete inventory item",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);

            if (confirm != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                await _itemRepo.DeleteAsync(item.Id);
                try
                {
                    AuditService.Log(AuditSections.SheetStore, "Inventory item deleted",
                        item.FullDescription, "ITEM", item.Id.ToString());
                }
                catch { /* best-effort */ }

                SelectedItem = null;
                await LoadItemsAsync();
            }
            catch (Exception ex)
            {
                ShowError("Could not delete item.\n\n" + ExceptionMessages.Deep(ex));
            }
        }

        // ── Editor panel (shares the right-hand column) ────────────────

        private SheetStoreItems.ItemEditorViewModel? _itemEditor;
        public SheetStoreItems.ItemEditorViewModel? ItemEditor
        {
            get => _itemEditor;
            private set
            {
                _itemEditor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsItemEditorOpen));
                OnPropertyChanged(nameof(IsDetailPanelVisible));
            }
        }

        public bool IsItemEditorOpen => _itemEditor != null;

        private void OpenItemEditor(InventoryItem? item)
        {
            bool isNew = item == null;

            if (!PermissionService.Require(
                    isNew ? AppPermissions.SheetStoreCreate : AppPermissions.SheetStoreEdit,
                    isNew ? "add an inventory item" : "edit an inventory item"))
                return;

            _editingItemOriginal = item;
            ItemEditor = new SheetStoreItems.ItemEditorViewModel(item);
        }

        private InventoryItem? _editingItemOriginal;

        private ICommand? _closeItemEditorCommand;
        public ICommand CloseItemEditorCommand =>
            _closeItemEditorCommand ??= new RelayCommand(_ =>
            {
                ItemEditor = null;
                _editingItemOriginal = null;
            });

        private ICommand? _saveItemEditorCommand;
        public ICommand SaveItemEditorCommand =>
            _saveItemEditorCommand ??= new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(SaveItemEditorAsync, "Inventory SaveItem"),
                _ => ItemEditor?.IsValid == true);

        private async Task SaveItemEditorAsync()
        {
            var form = ItemEditor;
            if (form == null || !form.IsValid) return;

            form.Commit();
            if (form.Result == null) return;

            bool isNew = _editingItemOriginal == null;
            var result = form.Result;

            ItemEditor = null;
            _editingItemOriginal = null;

            try
            {
                if (isNew)
                    await _itemRepo.AddAsync(result);
                else
                    await _itemRepo.UpdateAsync(result);

                try
                {
                    AuditService.Log(AuditSections.SheetStore,
                        isNew ? "Inventory item added" : "Inventory item edited",
                        result.FullDescription, "ITEM", result.Id.ToString());
                }
                catch { /* best-effort */ }

                await LoadItemsAsync();
            }
            catch (InvalidOperationException ex)
            {
                ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                ShowError("Could not save item.\n\n" + ExceptionMessages.Deep(ex));
            }
        }
    
        // ═══════════════════════════════════════════════════════════════
        // DETAIL WORKSPACE  (double-click a row, ADD NEW, or EDIT)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Opens the full-screen workspace. A null sheet means ADD NEW: the
        /// window starts on the Edit tab with an empty form, and Overview /
        /// Movements / Analysis are suppressed because a sheet that does
        /// not exist yet has no stock, no history and no margin.
        ///
        /// Permission is checked HERE, before the window is built, so an
        /// unauthorised operator never sees a form they cannot submit.
        /// </summary>
        private void OpenSheetWorkspace(GlassSheet? sheet)
        {
            bool isNew = sheet == null;

            if (!PermissionService.Require(
                    isNew ? AppPermissions.SheetStoreCreate : AppPermissions.SheetStoreEdit,
                    isNew ? "add a glass sheet" : "edit a glass sheet"))
                return;

            try
            {
                // Edit binds a COPY, never the grid's live instance. Editing
                // the bound row directly mutates the visible list as the
                // operator types and leaves those edits behind on cancel.
                var working = isNew ? null : Clone(sheet!);

                var vm = new SheetDetailWindowViewModel(
                    working, _historyRepo, BuyCommand, UseCommand);

                // Saving routes back through the SAME persistence methods the
                // dialogs and the panel use, so there is still exactly one
                // writer to the inventory ledger.
                vm.SaveRequested += async (_, result) =>
                {
                    if (result == null) return;
                    if (isNew) await PersistNewSheetAsync(result);
                    else       await PersistEditedSheetAsync(result, sheet!);
                };

                _dialogs.ShowDialog<Views.SheetStore.SheetDetailWindow>(vm);

                AsyncUtility.SafeFireAndForget(LoadAsync, "SheetStore ReloadAfterDetail");
            }
            catch (Exception ex)
            {
                ShowError("Could not open the sheet workspace.\n\n" + ExceptionMessages.Deep(ex));
            }
        }

        /// <summary>Double click on a row — same workspace, existing sheet.</summary>
        private void OpenSheetDetail(GlassSheet sheet) => OpenSheetWorkspace(sheet);
    }
}
