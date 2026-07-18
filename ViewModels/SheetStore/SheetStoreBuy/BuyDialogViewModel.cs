using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.ViewModels;
using System;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.SheetStore.SheetStoreBuy
{
    public class BuyDialogViewModel : ViewModelBase
    {
        public GlassSheet Sheet { get; }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (value < 1) value = 1;
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalSqm));
                OnPropertyChanged(nameof(TotalCost));
                OnPropertyChanged(nameof(NewStockAfterBuy));
                OnPropertyChanged(nameof(NewBalanceAfterBuy));
                OnPropertyChanged(nameof(CanConfirm));
            }
        }

        // Computed previews
        public decimal TotalSqm => Sheet.SqmPerSheet * Quantity;
        public decimal TotalCost => TotalSqm * Sheet.PurchasePricePerSqm;
        public int NewStockAfterBuy => Sheet.Stock + Quantity;
        public int NewBalanceAfterBuy => Sheet.Balance + Quantity;

        public bool CanConfirm => Quantity > 0;

        // Result
        public bool? DialogResult { get; private set; }
        public Action? CloseAction { get; set; }

        // Commands
        public ICommand IncreaseCommand { get; }
        public ICommand DecreaseCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public BuyDialogViewModel(GlassSheet sheet)
        {
            Sheet = sheet;

            IncreaseCommand = new RelayCommand(_ => Quantity++);
            DecreaseCommand = new RelayCommand(_ => { if (Quantity > 1) Quantity--; });
            ConfirmCommand = new RelayCommand(_ => { DialogResult = true; CloseAction?.Invoke(); });
            CancelCommand = new RelayCommand(_ => { DialogResult = false; CloseAction?.Invoke(); });
        }
    }
}