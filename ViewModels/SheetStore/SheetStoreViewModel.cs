using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.SheetStore;
using ProGlassAutomation.Views.SheetStore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.SheetStore
{
    public class SheetStoreViewModel : ViewModelBase
    {
        private readonly ISheetStoreRepository _repo;

        public ObservableCollection<GlassSheet> Sheets { get; } = new();

        // ============ STATS ============
        public int TotalItems => Sheets.Count;
        public int TotalStock => Sheets.Sum(s => s.Stock);
        public int TotalUsed => Sheets.Sum(s => s.Used);
        public int TotalBalance => Sheets.Sum(s => s.Balance);

        // Total SQM
        public decimal TotalSqmInStock => Sheets.Sum(s => s.TotalSqmInStock);
        public decimal TotalSqmBalance => Sheets.Sum(s => s.TotalSqmBalance);

        // MONEY VALUES (the important ones you asked for)
        public decimal TotalBuyingValue => Sheets.Sum(s => s.TotalPurchaseValue);
        public decimal TotalSellingValue => Sheets.Sum(s => s.TotalSellValue);
        public decimal TotalProfit => TotalSellingValue - TotalBuyingValue;
        public decimal TotalBalanceValue => Sheets.Sum(s => s.BalanceValue);

        public bool HasSheets => Sheets.Count > 0;
        public bool IsEmpty => Sheets.Count == 0;

        // ============ STATE ============
        private GlassSheet? _selectedSheet;
        public GlassSheet? SelectedSheet
        {
            get => _selectedSheet;
            set { _selectedSheet = value; OnPropertyChanged(); }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); _ = LoadAsync(); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        // ============ COMMANDS ============
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand BuyCommand { get; }
        public ICommand UseCommand { get; }
        public ICommand RefreshCommand { get; }

        public SheetStoreViewModel()
        {
            _repo = new SheetStoreRepository();

            AddCommand = new RelayCommand(async _ => await AddAsync());
            EditCommand = new RelayCommand(async p => await EditAsync(p as GlassSheet ?? SelectedSheet));
            DeleteCommand = new RelayCommand(async p => await DeleteAsync(p as GlassSheet ?? SelectedSheet));
            BuyCommand = new RelayCommand(async p => await BuyAsync(p as GlassSheet ?? SelectedSheet));
            UseCommand = new RelayCommand(async p => await UseAsync(p as GlassSheet ?? SelectedSheet));
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                IsBusy = true;
                var all = await _repo.GetAllAsync();

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var q = SearchText.Trim().ToLower();
                    all = all.Where(g =>
                        g.Category.ToLower().Contains(q) ||
                        g.Color.ToLower().Contains(q) ||
                        g.Supplier.ToLower().Contains(q) ||
                        g.ThicknessDisplay.ToLower().Contains(q)
                    ).ToList();
                }

                Sheets.Clear();
                foreach (var s in all)
                    Sheets.Add(s);

                RaiseStatsChanged();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RaiseStatsChanged()
        {
            OnPropertyChanged(nameof(TotalItems));
            OnPropertyChanged(nameof(TotalStock));
            OnPropertyChanged(nameof(TotalUsed));
            OnPropertyChanged(nameof(TotalBalance));
            OnPropertyChanged(nameof(TotalSqmInStock));
            OnPropertyChanged(nameof(TotalSqmBalance));
            OnPropertyChanged(nameof(TotalBuyingValue));
            OnPropertyChanged(nameof(TotalSellingValue));
            OnPropertyChanged(nameof(TotalProfit));
            OnPropertyChanged(nameof(TotalBalanceValue));
            OnPropertyChanged(nameof(HasSheets));
            OnPropertyChanged(nameof(IsEmpty));
        }

        private async Task AddAsync()
        {
            var vm = new AddEditSheetViewModel();
            var dialog = new AddEditSheetView(vm) { Owner = Application.Current.MainWindow };
            dialog.ShowDialog();

            if (vm.DialogResult == true && vm.Result != null)
            {
                try
                {
                    await _repo.AddAsync(vm.Result);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    ShowError("Could not add glass sheet.\n\n" + ex.Message);
                }
            }
        }

        private async Task EditAsync(GlassSheet? sheet)
        {
            if (sheet == null) return;

            var vm = new AddEditSheetViewModel(sheet);
            var dialog = new AddEditSheetView(vm) { Owner = Application.Current.MainWindow };
            dialog.ShowDialog();

            if (vm.DialogResult == true && vm.Result != null)
            {
                try
                {
                    await _repo.UpdateAsync(vm.Result);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    ShowError("Could not update glass sheet.\n\n" + ex.Message);
                }
            }
        }

        private async Task DeleteAsync(GlassSheet? sheet)
        {
            if (sheet == null) return;

            var result = MessageBox.Show(
                $"Delete this glass sheet?\n\n" +
                $"{sheet.Category} • {sheet.ThicknessDisplay} • {sheet.Color}\n" +
                $"Size: {sheet.SizeDisplay}\n" +
                $"Supplier: {sheet.Supplier}\n" +
                $"Current stock: {sheet.Stock} sheets " +
                $"(worth {sheet.TotalPurchaseValue:N0} AED)\n\n" +
                $"This cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _repo.DeleteAsync(sheet.Id);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    ShowError("Could not delete.\n\n" + ex.Message);
                }
            }
        }

        private async Task BuyAsync(GlassSheet? sheet)
        {
            if (sheet == null) return;

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                $"How many sheets to buy?\n\n" +
                $"Current stock: {sheet.Stock} sheets\n" +
                $"Price per SQM: {sheet.PurchasePricePerSqm:N2} AED\n" +
                $"SQM per sheet: {sheet.SqmPerSheet:N4}",
                "Buy Stock",
                "10");

            if (int.TryParse(input, out int qty) && qty > 0)
            {
                try
                {
                    await _repo.BuyStockAsync(sheet.Id, qty);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    ShowError("Could not add stock.\n\n" + ex.Message);
                }
            }
        }

        private async Task UseAsync(GlassSheet? sheet)
        {
            if (sheet == null) return;

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                $"How many sheets to use?\n\n" +
                $"Available: {sheet.Balance} sheets " +
                $"({sheet.TotalSqmBalance:N2} SQM)",
                "Use Stock",
                "1");

            if (int.TryParse(input, out int qty) && qty > 0)
            {
                try
                {
                    await _repo.UseStockAsync(sheet.Id, qty);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    ShowError(ex.Message);
                }
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}