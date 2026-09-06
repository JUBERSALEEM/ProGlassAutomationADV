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
        // ── Editor panel state ─────────────────────────────────────────

        private AddEditSheetViewModel? _editor;
        /// <summary>
        /// The live Add/Edit form, or null when the panel is closed.
        /// The panel binds its whole content to this.
        /// </summary>
        public AddEditSheetViewModel? Editor
        {
            get => _editor;
            private set
            {
                _editor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEditorOpen));
                OnPropertyChanged(nameof(IsDetailPanelVisible));
            }
        }

        public bool IsEditorOpen => _editor != null;

        /// <summary>
        /// The stats/detail panel and the editor share the right-hand
        /// column. This is what the detail panel binds to instead of
        /// IsRowSelected, so the editor wins when both would show.
        /// </summary>
        public bool IsDetailPanelVisible => IsRowSelected && !IsEditorOpen;

        private ICommand? _openAddPanelCommand;
        public ICommand OpenAddPanelCommand =>
            _openAddPanelCommand ??= new RelayCommand(_ => OpenEditor(null));

        private ICommand? _openEditPanelCommand;
        public ICommand OpenEditPanelCommand =>
            _openEditPanelCommand ??= new RelayCommand(
                p => OpenEditor(p as GlassSheet ?? SelectedSheet),
                p => (p as GlassSheet ?? SelectedSheet) != null);

        private ICommand? _closeEditorCommand;
        public ICommand CloseEditorCommand =>
            _closeEditorCommand ??= new RelayCommand(_ => CloseEditor());

        private ICommand? _saveEditorCommand;
        /// <summary>
        /// Commits the panel. Gated on the form's own IsValid, so the
        /// SAVE button is visibly dead until every field is acceptable —
        /// the same blocking contract the dialogs now use.
        /// </summary>
        public ICommand SaveEditorCommand =>
            _saveEditorCommand ??= new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(SaveEditorAsync, "Inventory SaveEditor"),
                _ => Editor?.IsValid == true);

        /// <summary>
        /// Opens the panel for a new sheet (null) or an existing one.
        /// Permission is checked HERE, before the form is even built, so
        /// an unauthorised operator never sees a form they cannot submit.
        /// </summary>
        private void OpenEditor(GlassSheet? sheet)
        {
            bool isNew = sheet == null;

            if (!PermissionService.Require(
                    isNew ? AppPermissions.SheetStoreCreate : AppPermissions.SheetStoreEdit,
                    isNew ? "add a glass sheet" : "edit a glass sheet"))
                return;

            // Edit binds a COPY, never the grid's live instance. Editing
            // the bound row directly would mutate the visible list as the
            // operator types and leave those edits behind on cancel.
            _editingOriginal = sheet;
            Editor = new AddEditSheetViewModel(sheet == null ? null : Clone(sheet));
        }

        private GlassSheet? _editingOriginal;

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

        private void CloseEditor()
        {
            Editor = null;
            _editingOriginal = null;
        }

        /// <summary>
        /// Commits the panel through the SAME persistence path the modal
        /// dialogs use.
        ///
        /// DELIBERATELY NOT a reimplementation: AddAsync/EditAsync already
        /// own the repository call, the change-diff, the history entry and
        /// the audit record with its AuditChange list. Duplicating that
        /// here would mean two code paths writing the inventory ledger,
        /// and they would drift. Instead the panel hands its validated
        /// Result to those methods via a pre-supplied form.
        /// </summary>
        private async System.Threading.Tasks.Task SaveEditorAsync()
        {
            var form = Editor;
            if (form == null || !form.IsValid) return;

            // Materialise the result from the form (re-validates inside).
            form.SaveCommand.Execute(null);
            if (form.DialogResult != true || form.Result == null) return;

            var original = _editingOriginal;

            // Close first: persistence triggers LoadAsync, which rebuilds
            // the collection the panel is bound against.
            CloseEditor();

            if (original == null)
                await PersistNewSheetAsync(form.Result);
            else
                await PersistEditedSheetAsync(form.Result, original);
        }
    }
}
