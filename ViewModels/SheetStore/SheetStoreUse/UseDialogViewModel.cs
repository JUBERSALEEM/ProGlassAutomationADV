using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.ViewModels;
using System;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.SheetStore.SheetStoreUse
{
    public class UseDialogViewModel : ViewModelBase
    {
        public GlassSheet Sheet { get; }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (value < 1) value = 1;
                if (value > Sheet.Balance) value = Sheet.Balance;
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalSqm));
                OnPropertyChanged(nameof(TotalValue));
                OnPropertyChanged(nameof(RemainingBalance));
                OnPropertyChanged(nameof(CanConfirm));
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }

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

        public bool CanConfirm => Quantity > 0 && Quantity <= Sheet.Balance;
        public bool IsValid => CanConfirm;

        public string ErrorMessage => Sheet.Balance == 0
            ? "No stock available to use."
            : string.Empty;

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        // Result
        public bool? DialogResult { get; private set; }
        public Action? CloseAction { get; set; }

        public void Accept() => DialogResult = true;

        public void ResetAfterCommit()
        {
            DialogResult = null;
            Quantity = 1;
        }

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

            IncreaseCommand = new RelayCommand(_ => { if (Quantity < Sheet.Balance) Quantity++; });
            DecreaseCommand = new RelayCommand(_ => { if (Quantity > 1) Quantity--; });
            ConfirmCommand = new RelayCommand(_ => { DialogResult = true; CloseAction?.Invoke(); }, _ => CanConfirm);
            CancelCommand = new RelayCommand(_ => { DialogResult = false; CloseAction?.Invoke(); });
        }
    }
}