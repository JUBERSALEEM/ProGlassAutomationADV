// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/SheetStore/SheetStoreBuy/BuyDialogViewModel.cs
//
// 2026-09-06 — BLOCKING VALIDATION (owner: "if mistakenly enter 0 total
// buy it still saving when an industry erp will not let it happen").
//
// TWO FAULTS, ONE SYMPTOM.
//
// 1. The guard existed and was never wired.
//        public bool CanConfirm => Quantity > 0;   // declared
//        ConfirmCommand = new RelayCommand(_ => {...});   // no CanExecute
//    The Use dialog passes `_ => CanConfirm`; this one did not. So the
//    CONFIRM button stayed enabled at quantity 0. BuyAsync then re-tested
//    `vm.Quantity > 0` and silently did nothing: dialog closed, no stock
//    added, no message shown. The operator walks away believing the
//    purchase was recorded. Silent success-looking failure is the worst
//    outcome of the three possible ones.
//
// 2. The int binding could not observe an empty box. With
//    UpdateSourceTrigger=PropertyChanged on an INT property, clearing the
//    field sends "", conversion fails, and WPF never invokes the setter —
//    so `if (value < 1) value = 1` never ran, and the backing field kept
//    a stale value while the box showed empty. The clamp was also wrong
//    in principle: it rewrote a deliberate 0 into 1 behind the operator's
//    back instead of refusing and explaining.
//
// THE FIX: bind RAW TEXT, parse explicitly, gate the command.
// QuantityText is the binding surface; Quantity is the parsed OUTPUT.
// The VM now sees exactly what the operator sees, including empty, and
// every preview figure recomputes off the validated value only.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.ViewModels.Validation;
using System;
using System.Globalization;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.SheetStore.SheetStoreBuy
{
    public class BuyDialogViewModel : ValidatableViewModelBase
    {
        public GlassSheet Sheet { get; }

        // ── Raw entry surface ──────────────────────────────────────────
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

        /// <summary>
        /// Parsed, validated quantity. 0 means "not currently valid" —
        /// callers must check <see cref="IsValid"/>, never trust this
        /// number on its own.
        /// </summary>
        public int Quantity { get; private set; } = 1;

        /// <summary>Inline error under the quantity box.</summary>
        public string QuantityError => ErrorFor(nameof(QuantityText));
        public bool HasQuantityError => !string.IsNullOrEmpty(QuantityError);

        // ── Live previews (only meaningful while valid) ────────────────
        public decimal TotalSqm => Sheet.SqmPerSheet * Quantity;
        public decimal TotalCost => TotalSqm * Sheet.PurchasePricePerSqm;
        public int NewStockAfterBuy => Sheet.Stock + Quantity;
        public int NewBalanceAfterBuy => Sheet.Balance + Quantity;

        /// <summary>
        /// Previews are hidden while the input is bad — showing
        /// "New stock: 46" next to an empty quantity box invites the
        /// operator to believe something will be saved.
        /// </summary>
        public bool ShowPreview => IsValid;

        public bool CanConfirm => IsValid;

        // ── Result ─────────────────────────────────────────────────────
        public bool? DialogResult { get; private set; }
        public Action? CloseAction { get; set; }

        /// <summary>Marks the form confirmed so the workspace tab can commit without closing.</summary>
        public void Accept() => DialogResult = true;

        // ── Commands ───────────────────────────────────────────────────
        public ICommand IncreaseCommand { get; }
        public ICommand DecreaseCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public BuyDialogViewModel(GlassSheet sheet)
        {
            Sheet = sheet;

            IncreaseCommand = new RelayCommand(_ => Nudge(+1));
            DecreaseCommand = new RelayCommand(_ => Nudge(-1));

            // THE FIX for fault 1 — the confirm button is dead while the
            // form is invalid, so a bad quantity can never reach the
            // repository in the first place.
            ConfirmCommand = new RelayCommand(
                _ => { DialogResult = true; CloseAction?.Invoke(); },
                _ => CanConfirm);

            CancelCommand = new RelayCommand(
                _ => { DialogResult = false; CloseAction?.Invoke(); });

            Revalidate();
        }

        /// <summary>
        /// ± stepper. Operates on the PARSED value and refuses to step
        /// below 1, but when the box is currently unparseable it restarts
        /// from 1 rather than propagating garbage.
        /// </summary>
        private void Nudge(int delta)
        {
            int current = IsValid ? Quantity : 0;
            int next = Math.Max(1, current + delta);
            QuantityText = next.ToString(CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Resets the form after a committed movement so the SAME instance
        /// can take another entry.
        ///
        /// Only needed because the full-screen workspace hosts this form as
        /// a TAB that stays open: the modal path discards the view-model on
        /// close. Quantity returns to 1 and every Balance-derived figure is
        /// re-announced, since the underlying sheet has just changed.
        /// </summary>
        public void ResetAfterCommit()
        {
            _quantityText = "1";
            Raise(nameof(QuantityText));
            Revalidate();
        }

        private void Revalidate()
        {
            // No upper bound on a PURCHASE — you can buy as many as you
            // like. (Consumption is the bounded side; see UseDialog.)
            var parsed = ParseQuantity(
                QuantityText,
                nameof(QuantityText),
                fieldLabel: "quantity");

            if (parsed.HasValue)
                Quantity = parsed.Value;

            Raise(nameof(Quantity));
            Raise(nameof(QuantityError));
            Raise(nameof(HasQuantityError));
            Raise(nameof(TotalSqm));
            Raise(nameof(TotalCost));
            Raise(nameof(NewStockAfterBuy));
            Raise(nameof(NewBalanceAfterBuy));
            Raise(nameof(ShowPreview));
            Raise(nameof(CanConfirm));
        }
    }
}
