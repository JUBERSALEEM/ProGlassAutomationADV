using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.ViewModels;
using System;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.SheetStore.SheetStoreCsv
{
    /// <summary>
    /// Represents the user's choice for a duplicate row during CSV import.
    /// </summary>
    public enum DuplicateAction
    {
        Skip,
        AddToStock,
        UpdateAll,
        Cancel
    }

    /// <summary>
    /// ViewModel for the duplicate resolution dialog.
    /// Shows existing vs incoming side-by-side and lets user choose an action.
    /// </summary>
    public class DuplicateResolutionViewModel : ViewModelBase
    {
        public GlassSheet Existing { get; }
        public GlassSheet Incoming { get; }

        /// <summary>Row number in CSV (1-indexed).</summary>
        public int RowNumber { get; }

        /// <summary>Total duplicate rows expected in this import.</summary>
        public int TotalDuplicates { get; }

        public string HeaderText => $"Duplicate {RowNumber} of {TotalDuplicates}";

        public string SheetLabel =>
            $"{Existing.Category} • {Existing.ThicknessDisplay} • {Existing.Color} • {Existing.SizeDisplay}";

        // ============ COMPARISON HIGHLIGHTS ============
        public bool StockDifferent => Existing.Stock != Incoming.Stock;
        public bool BuyPriceDifferent => Existing.PurchasePricePerSqm != Incoming.PurchasePricePerSqm;
        public bool SellPriceDifferent => Existing.SellPricePerSqm != Incoming.SellPricePerSqm;

        // ============ USER CHOICE ============
        private bool _applyToRemaining;
        public bool ApplyToRemaining
        {
            get => _applyToRemaining;
            set { _applyToRemaining = value; OnPropertyChanged(); }
        }

        public DuplicateAction? Result { get; private set; }
        public bool? DialogResult { get; private set; }

        public Action? CloseAction { get; set; }

        // ============ COMMANDS ============
        public ICommand SkipCommand { get; }
        public ICommand AddToStockCommand { get; }
        public ICommand UpdateAllCommand { get; }
        public ICommand CancelImportCommand { get; }

        public DuplicateResolutionViewModel(GlassSheet existing, GlassSheet incoming, int rowNumber, int totalDuplicates)
        {
            Existing = existing;
            Incoming = incoming;
            RowNumber = rowNumber;
            TotalDuplicates = totalDuplicates;

            SkipCommand = new RelayCommand(_ => Choose(DuplicateAction.Skip));
            AddToStockCommand = new RelayCommand(_ => Choose(DuplicateAction.AddToStock));
            UpdateAllCommand = new RelayCommand(_ => Choose(DuplicateAction.UpdateAll));
            CancelImportCommand = new RelayCommand(_ => Choose(DuplicateAction.Cancel));
        }

        private void Choose(DuplicateAction action)
        {
            Result = action;
            DialogResult = true;
            CloseAction?.Invoke();
        }
    }
}