// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/SheetStore/SheetStoreItems/ItemEditorViewModel.cs
// MODULE: Inventory — add / edit a consumable
// ADDED: 2026-09-06
//
// The consumables counterpart to AddEditSheetViewModel, and it follows
// the same contract: raw-text binding, blocking per-field validation,
// nothing silently clamped.
//
// It edits a CLONE. The panel binds the grid's live row otherwise, which
// mutates the visible list as the operator types and leaves those edits
// behind on cancel.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.ViewModels.Validation;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ProGlassAutomation.ViewModels.SheetStore.SheetStoreItems
{
    public class ItemEditorViewModel : ValidatableViewModelBase
    {
        private readonly InventoryItem? _existing;

        public ItemEditorViewModel(InventoryItem? existing)
        {
            _existing = existing;

            if (existing != null)
            {
                var ci = CultureInfo.CurrentCulture;
                _name = existing.Name;
                _partNumber = existing.PartNumber;
                _supplier = existing.Supplier;
                _specification = existing.Specification;
                _category = existing.Category;
                _uom = existing.Uom;
                _stockText = existing.Stock.ToString("0.####", ci);
                _reorderText = existing.ReorderLevel.ToString("0.####", ci);
                _purchaseText = existing.PurchasePricePerUnit.ToString("0.####", ci);
                _sellText = existing.SellPricePerUnit.ToString("0.####", ci);
            }

            Revalidate();
        }

        public bool IsEditMode => _existing != null;
        public string Title => IsEditMode ? "Edit item" : "New item";
        public string SaveButtonText => IsEditMode ? "Update" : "Add";

        /// <summary>
        /// Existing stock cannot be edited freely once anything is used —
        /// the field is shown read-only in that case and moved through
        /// Receive/Issue instead, so every change has a reason.
        /// </summary>
        public bool CanEditStock => _existing == null || _existing.Used <= 0;

        // ── Pickers ────────────────────────────────────────────────────

        public IReadOnlyList<InventoryCategory> Categories { get; } =
            System.Enum.GetValues<InventoryCategory>().ToList();

        public IReadOnlyList<StockUom> Uoms { get; } =
            System.Enum.GetValues<StockUom>().ToList();

        // ── Fields ─────────────────────────────────────────────────────

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { if (_name == value) return; _name = value; Raise(); Revalidate(); }
        }

        private string _partNumber = string.Empty;
        public string PartNumber
        {
            get => _partNumber;
            set { if (_partNumber == value) return; _partNumber = value; Raise(); Revalidate(); }
        }

        private string _supplier = string.Empty;
        public string Supplier
        {
            get => _supplier;
            set { if (_supplier == value) return; _supplier = value; Raise(); Revalidate(); }
        }

        private string _specification = string.Empty;
        public string Specification
        {
            get => _specification;
            set { if (_specification == value) return; _specification = value; Raise(); Revalidate(); }
        }

        private InventoryCategory _category = InventoryCategory.SpacerBar;
        public InventoryCategory Category
        {
            get => _category;
            set { if (_category == value) return; _category = value; Raise(); Revalidate(); }
        }

        private StockUom _uom = StockUom.LinearMetre;
        public StockUom Uom
        {
            get => _uom;
            set
            {
                if (_uom == value) return;
                _uom = value;
                Raise();
                Raise(nameof(UomDisplay));
                Revalidate();
            }
        }

        public string UomDisplay => _uom switch
        {
            StockUom.Piece => "pcs",
            StockUom.LinearMetre => "LM",
            StockUom.SquareMetre => "SQM",
            StockUom.Kilogram => "kg",
            StockUom.Litre => "L",
            StockUom.CubicMetre => "m³",
            _ => string.Empty
        };

        private string _stockText = "0";
        public string StockText
        {
            get => _stockText;
            set { if (_stockText == value) return; _stockText = value; Raise(); Revalidate(); }
        }

        private string _reorderText = "0";
        public string ReorderText
        {
            get => _reorderText;
            set { if (_reorderText == value) return; _reorderText = value; Raise(); Revalidate(); }
        }

        private string _purchaseText = "0";
        public string PurchaseText
        {
            get => _purchaseText;
            set { if (_purchaseText == value) return; _purchaseText = value; Raise(); Revalidate(); }
        }

        private string _sellText = "0";
        public string SellText
        {
            get => _sellText;
            set { if (_sellText == value) return; _sellText = value; Raise(); Revalidate(); }
        }

        // ── Parsed outputs ─────────────────────────────────────────────

        public decimal Stock { get; private set; }
        public decimal ReorderLevel { get; private set; }
        public decimal PurchasePricePerUnit { get; private set; }
        public decimal SellPricePerUnit { get; private set; }

        // ── Per-field errors ───────────────────────────────────────────

        public string NameError => ErrorFor(nameof(Name));
        public bool HasNameError => !string.IsNullOrEmpty(NameError);

        public string StockError => ErrorFor(nameof(StockText));
        public bool HasStockError => !string.IsNullOrEmpty(StockError);

        public string ReorderError => ErrorFor(nameof(ReorderText));
        public bool HasReorderError => !string.IsNullOrEmpty(ReorderError);

        public string PurchaseError => ErrorFor(nameof(PurchaseText));
        public bool HasPurchaseError => !string.IsNullOrEmpty(PurchaseError);

        public string SellError => ErrorFor(nameof(SellText));
        public bool HasSellError => !string.IsNullOrEmpty(SellError);

        /// <summary>
        /// Selling below cost is legal (clearance, samples) so this warns
        /// rather than blocks — same rule as the glass editor.
        /// </summary>
        public bool HasMarginWarning =>
            IsValid && SellPricePerUnit > 0 && SellPricePerUnit < PurchasePricePerUnit;

        public string MarginWarning => HasMarginWarning
            ? "Sell price is below purchase price — this item will trade at a loss."
            : string.Empty;

        public decimal StockValue => Stock * PurchasePricePerUnit;

        // ── Result ─────────────────────────────────────────────────────

        public InventoryItem? Result { get; private set; }

        /// <summary>
        /// Materialises the edited item. Re-validates first: the caller
        /// gates on IsValid, but CanExecute is an affordance rather than a
        /// boundary — accelerators and automation peers can still get here.
        /// </summary>
        public void Commit()
        {
            Revalidate();
            if (!IsValid) { Result = null; return; }

            var target = _existing == null
                ? new InventoryItem()
                : Clone(_existing);

            target.Name = Name.Trim();
            target.PartNumber = (PartNumber ?? string.Empty).Trim();
            target.Supplier = (Supplier ?? string.Empty).Trim();
            target.Specification = (Specification ?? string.Empty).Trim();
            target.Category = Category;
            target.Uom = Uom;
            target.ReorderLevel = ReorderLevel;
            target.PurchasePricePerUnit = PurchasePricePerUnit;
            target.SellPricePerUnit = SellPricePerUnit;

            // Stock only moves from here when nothing has been consumed;
            // otherwise it is Receive/Issue territory and the repository
            // enforces the same rule server-side.
            if (CanEditStock) target.Stock = Stock;

            Result = target;
        }

        private static InventoryItem Clone(InventoryItem s) => new()
        {
            Id = s.Id,
            Name = s.Name,
            Category = s.Category,
            PartNumber = s.PartNumber,
            Supplier = s.Supplier,
            Specification = s.Specification,
            Uom = s.Uom,
            Stock = s.Stock,
            Used = s.Used,
            ReorderLevel = s.ReorderLevel,
            PurchasePricePerUnit = s.PurchasePricePerUnit,
            SellPricePerUnit = s.SellPricePerUnit,
            RowVersion = s.RowVersion,
            CreatedAt = s.CreatedAt,
            LastUpdated = s.LastUpdated,
            LastPurchaseDate = s.LastPurchaseDate
        };

        private void Revalidate()
        {
            ParseRequiredText(Name, nameof(Name), "Name", maxLength: 200);

            // Opening stock of 0 is legitimate — items are routinely created
            // before the delivery lands — so this is a plain non-negative
            // parse, not the quantity rule that rejects 0 by design.
            ParseNonNegative(StockText, nameof(StockText), "Opening stock",
                             v => Stock = v);

            // 0 reorder level means "not tracked" and must stay allowed,
            // or every item would alarm on day one.
            ParseNonNegative(ReorderText, nameof(ReorderText), "Reorder level",
                             v => ReorderLevel = v);

            var purchase = ParseDecimal(PurchaseText, nameof(PurchaseText),
                                        "purchase price", allowZero: true);
            if (purchase.HasValue) PurchasePricePerUnit = purchase.Value;

            var sell = ParseDecimal(SellText, nameof(SellText),
                                    "sell price", allowZero: true);
            if (sell.HasValue) SellPricePerUnit = sell.Value;

            // Editing must not strand recorded consumption.
            if (_existing != null && CanEditStock == false && Stock < _existing.Used)
            {
                SetError(nameof(StockText),
                    $"Stock cannot be below {_existing.Used:N2} {UomDisplay} — " +
                    "that much is already used.");
            }

            foreach (var n in new[]
            {
                nameof(NameError), nameof(HasNameError),
                nameof(StockError), nameof(HasStockError),
                nameof(ReorderError), nameof(HasReorderError),
                nameof(PurchaseError), nameof(HasPurchaseError),
                nameof(SellError), nameof(HasSellError),
                nameof(Stock), nameof(ReorderLevel),
                nameof(PurchasePricePerUnit), nameof(SellPricePerUnit),
                nameof(StockValue),
                nameof(HasMarginWarning), nameof(MarginWarning),
                nameof(ValidationSummary), nameof(HasValidationSummary),
                nameof(IsValid)
            })
                OnPropertyChanged(n);
        }

        private void ParseNonNegative(
            string? raw, string property, string label, System.Action<decimal> assign)
        {
            raw = raw?.Trim();

            if (string.IsNullOrEmpty(raw))
            {
                SetError(property, $"Enter a {label.ToLowerInvariant()} (0 is allowed).");
                return;
            }

            if (!decimal.TryParse(raw, out var value))
            {
                SetError(property, $"{label} must be a number.");
                return;
            }

            if (value < 0)
            {
                SetError(property, $"{label} cannot be negative.");
                return;
            }

            SetError(property, null);
            assign(value);
        }
    }
}
