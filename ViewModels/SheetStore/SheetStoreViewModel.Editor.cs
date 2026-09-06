// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/SheetStore/SheetStoreViewModel.Editor.cs
// MODULE: Inventory (Sheet Store) — slide-out Add/Edit editor host
// ADDED: 2026-09-06
//
// WHY A PANEL INSTEAD OF THE DIALOG
//
// Owner: "sometimes full data is not visible what we are editing... add
// sheet make left side bar open etc or doing the redesign of popup".
//
// The Add/Edit form is nine fields today and grows further once
// unit-of-measure lands (item type, stock UoM, pricing UoM, reorder
// level). A modal is the wrong host for a form that size: it is capped by
// the screen, it hides the grid the operator is comparing against, and it
// cannot be left open while they check another row.
//
// Buy and Use STAY modal deliberately. They are short, single-decision
// confirmations where blocking the rest of the UI is the correct
// affordance. Only the long form moves.
//
// WHY THIS REUSES THE EXISTING SLIDE MECHANISM
//
// SheetStoreView already animates a right-hand detail panel by growing a
// ColumnDefinition with GridLengthAnimation (see SheetStoreView.xaml.cs).
// That approach reflows the list live instead of drawing over it, so the
// grid stays usable while the panel is open. The editor panel uses the
// SAME mechanism and the same easing rather than introducing a second,
// subtly different animation vocabulary.
//
// MUTUAL EXCLUSION: the detail (stats) panel and the editor panel share
// the right-hand column, so opening the editor closes the detail view.
// Both cannot occupy the column at once.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Utilities;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.SheetStore
{
    public partial class SheetStoreViewModel
    {
        // ── Right-hand column state ────────────────────────────────────

        /// <summary>
        /// Glass sheets no longer use a slide-out form (see below), so the
        /// stats rail only ever yields to the CONSUMABLE item editor.
        /// </summary>
        public bool IsDetailPanelVisible => IsRowSelected && !IsItemEditorOpen;

        // 2026-09-06: ADD NEW and EDIT now open the FULL-SCREEN detail
        // workspace instead of the 360px slide-out panel.
        //
        // The panel was the right call when the form was the only thing it
        // had to show, but the workspace supersedes it outright: same
        // always-editable fields, plus Overview, Movements and Analysis,
        // and room for all nine fields at once instead of a scroll in a
        // narrow column. Keeping both would have meant two ways to reach
        // the same form, which is exactly the duplication the owner saw as
        // "still the same as before".
        //
        // Persistence still funnels through PersistNewSheetAsync /
        // PersistEditedSheetAsync in Part1 — the only writers to the ledger.
        private ICommand? _openAddPanelCommand;
        public ICommand OpenAddPanelCommand =>
            _openAddPanelCommand ??= new RelayCommand(_ =>
            {
                // ADD NEW follows the visible list. Adding a glass sheet
                // while looking at consumables (or the reverse) would be a
                // silent mode error — the operator would fill in a form for
                // the wrong kind of stock and only find out on save.
                if (IsConsumablesView) AddItemCommand.Execute(null);
                else                   OpenSheetWorkspace(null);
            });

        private ICommand? _openEditPanelCommand;
        public ICommand OpenEditPanelCommand =>
            _openEditPanelCommand ??= new RelayCommand(
                p => OpenSheetWorkspace(p as GlassSheet ?? SelectedSheet),
                p => (p as GlassSheet ?? SelectedSheet) != null);

        // 2026-09-06 — THE GLASS SLIDE-OUT PANEL WAS REMOVED.
        //
        // OpenEditor, CloseEditorCommand, SaveEditorCommand, SaveEditorAsync
        // and _editingOriginal all went with it. Once ADD NEW and EDIT
        // pointed at the full-screen workspace nothing could set Editor to a
        // non-null value, so every one of them was unreachable: Editor was
        // permanently null, IsEditorOpen permanently false, and the panel
        // could never appear. Leaving that in place would have been a
        // phantom feature — code that reads as a live path but cannot run.
        //
        // Clone stays: the workspace uses it to edit a COPY so typing does
        // not mutate the grid's live row.

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
            Used = s.Used,                 // carried so the "stock cannot
                                           // drop below Used" rule works
            CreatedAt = s.CreatedAt,
            LastUpdated = s.LastUpdated,
            LastPurchaseDate = s.LastPurchaseDate
        };

        // The pre-existing PersistNewSheetAsync / PersistEditedSheetAsync
        // in Part1 remain the single persistence path for glass sheets; the
        // workspace calls them via the SaveRequested event it raises.
    }
}
