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

        // Computed previews
        public decimal TotalSqm => Sheet.SqmPerSheet * Quantity;
        public decimal TotalValue => TotalSqm * Sheet.SellPricePerSqm;
        public int RemainingBalance => Sheet.Balance - Quantity;

        public bool CanConfirm => Quantity > 0 && Quantity <= Sheet.Balance;

        public string ErrorMessage => Sheet.Balance == 0
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
            Sheet = sheet;

            IncreaseCommand = new RelayCommand(_ => { if (Quantity < Sheet.Balance) Quantity++; });
            DecreaseCommand = new RelayCommand(_ => { if (Quantity > 1) Quantity--; });
            ConfirmCommand = new RelayCommand(_ => { DialogResult = true; CloseAction?.Invoke(); }, _ => CanConfirm);
            CancelCommand = new RelayCommand(_ => { DialogResult = false; CloseAction?.Invoke(); });
        }
    }
}