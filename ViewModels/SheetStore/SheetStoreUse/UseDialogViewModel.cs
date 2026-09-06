// ═══════════════════════════════════════════════════════════════════════
// 2026-09-06 — BLOCKING VALIDATION.
//
// This dialog already gated ConfirmCommand on CanConfirm (the Buy dialog
// did not — that was the reported 0-quantity bug). But it shared the
// other, subtler fault: an INT bound with UpdateSourceTrigger=
// PropertyChanged cannot observe an EMPTY box. Clearing the field sends
// "", conversion fails, the setter never runs, and the stale value
// survives behind an empty-looking control.
//
// It also silently CLAMPED. Typing 999 against a balance of 12 became 12
// with no explanation; typing 0 became 1. Quietly rewriting an operator's
// entry is how stock records drift away from physical reality — the
// operator believes they recorded 999 and never learns otherwise.
//
// Now: raw text in, explicit parse, refuse-and-explain against the real
// balance ("Only 12 sheet(s) available").
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.ViewModels.Validation;
using System;
using System.Globalization;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.SheetStore.SheetStoreUse
{
    public class UseDialogViewModel : ValidatableViewModelBase
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
        /// Parsed, validated quantity. Only trustworthy when
        /// <see cref="IsValid"/> is true.
        /// </summary>
        public int Quantity { get; private set; } = 1;

        /// <summary>Inline error under the quantity box.</summary>
        public string QuantityError => ErrorFor(nameof(QuantityText));
        public bool HasQuantityError => !string.IsNullOrEmpty(QuantityError);

        private string _jobOrderReference = string.Empty;
        public string JobOrderReference
        {
            get => _jobOrderReference;
            set { _jobOrderReference = value; OnPropertyChanged(); }
        }

        // ── CONSUMPTION TRACEABILITY ─────────────────────────────────
        // So a later Sheet Store report can answer "which salesman's
        // customer's project consumed how many sheets". Pre-filled from
        // the active invoice context when one exists, but always
        // editable: stock is also consumed for samples and remakes that
        // have no live document.

        private string _customerName = string.Empty;
        public string CustomerName
        {
            get => _customerName;
            set { _customerName = value; OnPropertyChanged(); }
        }

        private string _projectName = string.Empty;
        public string ProjectName
        {
            get => _projectName;
            set { _projectName = value; OnPropertyChanged(); }
        }

        private string _salesman = string.Empty;
        public string Salesman
        {
            get => _salesman;
            set { _salesman = value; OnPropertyChanged(); }
        }

        /// <summary>"Outer" / "Inner" when the sheet feeds one pane of a
        /// multi-leaf spec. Blank for a plain stock issue.</summary>
        private string _panePosition = string.Empty;
        public string PanePosition
        {
            get => _panePosition;
            set { _panePosition = value; OnPropertyChanged(); }
        }

        // Computed previews
        public decimal TotalSqm => Sheet.SqmPerSheet * Quantity;
        public decimal TotalValue => TotalSqm * Sheet.SellPricePerSqm;
        public int RemainingBalance => Sheet.Balance - Quantity;

        /// <summary>Previews stay hidden while the entry is invalid.</summary>
        public bool ShowPreview => IsValid && !IsOutOfStock;

        /// <summary>
        /// Nothing on the rack — a whole-dialog blocker rather than a
        /// field error, since no quantity could ever be valid.
        /// </summary>
        public bool IsOutOfStock => Sheet.Balance <= 0;

        public bool CanConfirm => IsValid && !IsOutOfStock;

        public string ErrorMessage => IsOutOfStock
            ? "No stock available to use."
            : string.Empty;

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        // Result
        public bool? DialogResult { get; private set; }
        public Action? CloseAction { get; set; }

        // Commands
        public ICommand IncreaseCommand { get; }
        public ICommand DecreaseCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public UseDialogViewModel(GlassSheet sheet)
        {
            // Pre-fill from the invoice the operator is working on, if any.
            // ActiveInvoiceContext is ambient and may be empty (Sheet Store
            // opened directly), which is fine — the fields stay editable.
            try
            {
                // ActiveInvoiceContext is a STATIC class — accessed directly,
                // not through an instance.
                _jobOrderReference =
                    ProGlassAutomation.Services.ProformaInvoice
                        .ActiveInvoiceContext.ProformaId ?? string.Empty;
                _customerName =
                    ProGlassAutomation.Services.ProformaInvoice
                        .ActiveInvoiceContext.CustomerName ?? string.Empty;
                _projectName =
                    ProGlassAutomation.Services.ProformaInvoice
                        .ActiveInvoiceContext.ProjectName ?? string.Empty;
            }
            catch
            {
                // Never let a context probe stop the dialog opening.
            }

            Sheet = sheet;

            IncreaseCommand = new RelayCommand(_ => Nudge(+1));
            DecreaseCommand = new RelayCommand(_ => Nudge(-1));
            ConfirmCommand = new RelayCommand(
                _ => { DialogResult = true; CloseAction?.Invoke(); },
                _ => CanConfirm);
            CancelCommand = new RelayCommand(
                _ => { DialogResult = false; CloseAction?.Invoke(); });

            Revalidate();
        }

        /// <summary>
        /// ± stepper, bounded by the real balance. Unlike the old setter
        /// this only constrains the BUTTON's own arithmetic — typing an
        /// over-balance number by hand still produces a visible error
        /// rather than a silent correction.
        /// </summary>
        private void Nudge(int delta)
        {
            int current = IsValid ? Quantity : 0;
            int next = Math.Max(1, Math.Min(Sheet.Balance, current + delta));
            QuantityText = next.ToString(CultureInfo.CurrentCulture);
        }

        private void Revalidate()
        {
            // Consumption is bounded by what is physically on the rack.
            // The message names the real number so the operator can act
            // on it without hunting for the balance elsewhere.
            var parsed = ParseQuantity(
                QuantityText,
                nameof(QuantityText),
                fieldLabel: "quantity",
                max: Sheet.Balance > 0 ? Sheet.Balance : (int?)null,
                maxMessage: $"Only {Sheet.Balance} sheet(s) available.");

            if (parsed.HasValue)
                Quantity = parsed.Value;

            Raise(nameof(Quantity));
            Raise(nameof(QuantityError));
            Raise(nameof(HasQuantityError));
            Raise(nameof(TotalSqm));
            Raise(nameof(TotalValue));
            Raise(nameof(RemainingBalance));
            Raise(nameof(ShowPreview));
            Raise(nameof(CanConfirm));
        }
    }
}