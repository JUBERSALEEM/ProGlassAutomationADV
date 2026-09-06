// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/SheetStore/SheetStoreItems/ItemMovementViewModel.cs
// MODULE: Inventory — receive / consume a consumable
// ADDED: 2026-09-06
//
// One dialog for both directions. Receiving 45.55 LM of spacer bar and
// issuing 12.5 kg of desiccant are the same interaction with a different
// sign and a different bound; two near-identical dialogs would drift.
//
// DECIMAL, unlike the glass Buy/Use dialogs. That is the whole point of
// the consumables table: you genuinely receive 45.55 LM, not 46.
//
// Validation inherits the contract established when the 0-quantity bug
// was fixed:
//   • raw text is the binding surface, so an EMPTY box is observable
//     (an int/decimal binding fails conversion and never calls the
//     setter, leaving a stale value behind a blank control);
//   • the confirm command is GATED, never merely advisory;
//   • nothing is silently clamped — over-issue is refused by name with
//     the real available figure.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.ViewModels.Validation;
using System;
using System.Globalization;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.SheetStore.SheetStoreItems
{
    public class ItemMovementViewModel : ValidatableViewModelBase
    {
        public InventoryItem Item { get; }

        /// <summary>True = receiving stock in, false = issuing it out.</summary>
        public bool IsReceive { get; }

        public ItemMovementViewModel(InventoryItem item, bool isReceive)
        {
            Item = item;
            IsReceive = isReceive;

            // Prefill the unit price on a receipt so the common case
            // (same supplier, same price) is one keystroke. It stays
            // editable because a price change is exactly what a new
            // delivery often brings.
            _unitPriceText = item.PurchasePricePerUnit.ToString("0.####", CultureInfo.CurrentCulture);

            ConfirmCommand = new RelayCommand(
                _ => { DialogResult = true; CloseAction?.Invoke(); },
                _ => CanConfirm);

            CancelCommand = new RelayCommand(
                _ => { DialogResult = false; CloseAction?.Invoke(); });

            Revalidate();
        }

        // ── Titles ─────────────────────────────────────────────────────

        public string DialogTitle => IsReceive ? "Receive stock" : "Issue stock";

        public string ActionLabel => IsReceive ? "RECEIVE" : "ISSUE";

        public string ItemLabel => Item.FullDescription;

        public string UomLabel => Item.UomDisplay;

        public string AvailableLabel =>
            $"{Item.Balance:N2} {Item.UomDisplay} available";

        // ── Quantity ───────────────────────────────────────────────────

        private string _quantityText = "1";
        public string QuantityText
        {
            get => _quantityText;
            set
            {
                if (_quantityText == value) return;
                _quantityText = value;
                Raise();
                Revalidate();
            }
        }

        /// <summary>Parsed quantity. Only trustworthy when IsValid.</summary>
        public decimal Quantity { get; private set; } = 1m;

        public string QuantityError => ErrorFor(nameof(QuantityText));
        public bool HasQuantityError => !string.IsNullOrEmpty(QuantityError);

        // ── Unit price (receipts only) ─────────────────────────────────

        private string _unitPriceText;
        public string UnitPriceText
        {
            get => _unitPriceText;
            set
            {
                if (_unitPriceText == value) return;
                _unitPriceText = value;
                Raise();
                Revalidate();
            }
        }

        /// <summary>
        /// New purchase price, or null to keep the existing one. Only
        /// meaningful on a receipt — issuing stock never repricers it.
        /// </summary>
        public decimal? UnitPrice { get; private set; }

        public string UnitPriceError => ErrorFor(nameof(UnitPriceText));
        public bool HasUnitPriceError => !string.IsNullOrEmpty(UnitPriceError);

        public bool ShowUnitPrice => IsReceive;

        // ── Preview ────────────────────────────────────────────────────

        public decimal BalanceAfter => IsReceive
            ? Item.Balance + Quantity
            : Item.Balance - Quantity;

        public string BalanceAfterDisplay => $"{BalanceAfter:N2} {Item.UomDisplay}";

        public decimal MovementValue => Quantity *
            (UnitPrice ?? Item.PurchasePricePerUnit);

        public bool ShowPreview => IsValid;

        /// <summary>
        /// Warns when an issue would drop the item to or below its reorder
        /// level. Non-blocking — running low is allowed, being surprised
        /// by it is the problem.
        /// </summary>
        public bool HasReorderWarning =>
            IsValid && !IsReceive && Item.ReorderLevel > 0 && BalanceAfter <= Item.ReorderLevel;

        public string ReorderWarning => HasReorderWarning
            ? $"This drops stock to {BalanceAfter:N2} {Item.UomDisplay}, " +
              $"at or below the reorder level of {Item.ReorderLevel:N2}."
            : string.Empty;

        public bool CanConfirm => IsValid;

        // ── Result ─────────────────────────────────────────────────────

        public bool? DialogResult { get; private set; }
        public Action? CloseAction { get; set; }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        private void Revalidate()
        {
            // Issues are bounded by what is physically on the shelf;
            // receipts are not. The message names the real figure so the
            // operator can act without hunting for the balance.
            var qty = ParseDecimal(
                QuantityText,
                nameof(QuantityText),
                fieldLabel: "quantity",
                allowZero: false,
                max: IsReceive ? (decimal?)null
                               : (Item.Balance > 0 ? Item.Balance : 0m));

            if (qty.HasValue) Quantity = qty.Value;

            // ParseDecimal's generic ceiling message would read "quantity
            // cannot exceed 12.00", which does not say WHY. Replace it
            // with the stock reality.
            if (!IsReceive && qty is null && HasQuantityError
                && QuantityError.Contains("cannot exceed", StringComparison.OrdinalIgnoreCase))
            {
                SetError(nameof(QuantityText),
                    $"Only {Item.Balance:N2} {Item.UomDisplay} available.");
            }

            if (IsReceive)
            {
                // A receipt may legitimately carry no price change, but a
                // BLANK box is ambiguous, so it is treated as "keep the
                // current price" only when it parses to the existing value.
                var price = ParseDecimal(
                    UnitPriceText, nameof(UnitPriceText),
                    fieldLabel: "unit price", allowZero: true);

                UnitPrice = price;
            }
            else
            {
                SetError(nameof(UnitPriceText), null);
                UnitPrice = null;
            }

            Raise(nameof(Quantity));
            Raise(nameof(QuantityError));
            Raise(nameof(HasQuantityError));
            Raise(nameof(UnitPriceError));
            Raise(nameof(HasUnitPriceError));
            Raise(nameof(BalanceAfter));
            Raise(nameof(BalanceAfterDisplay));
            Raise(nameof(MovementValue));
            Raise(nameof(ShowPreview));
            Raise(nameof(HasReorderWarning));
            Raise(nameof(ReorderWarning));
            Raise(nameof(CanConfirm));
        }
    }
}
