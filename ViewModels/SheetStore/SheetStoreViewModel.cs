using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.SheetStore;
using ProGlassAutomation.Services.SheetStore.SheetStoreHistory;
using ProGlassAutomation.Views.SheetStore;
using ProGlassAutomation.Views.SheetStore.SheetStoreHistory;
using ProGlassAutomation.ViewModels.SheetStore.SheetStoreHistory;
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
        private readonly ISheetHistoryRepository _historyRepo;

        public ObservableCollection<GlassSheet> Sheets { get; } = new();

        // ============ AGGREGATE STATS ============
        public int TotalItems => Sheets.Count;
        public int TotalStock => Sheets.Sum(s => s.Stock);
        public int TotalUsed => Sheets.Sum(s => s.Used);
        public int TotalBalance => Sheets.Sum(s => s.Balance);
        public decimal TotalSqmInStock => Sheets.Sum(s => s.TotalSqmInStock);
        public decimal TotalSqmBalance => Sheets.Sum(s => s.TotalSqmBalance);
        public decimal TotalBuyingValue => Sheets.Sum(s => s.TotalPurchaseValue);
        public decimal TotalSellingValue => Sheets.Sum(s => s.TotalSellValue);
        public decimal TotalProfit => TotalSellingValue - TotalBuyingValue;

        // ============ DISPLAY STATS (switches on selection) ============
        public int DisplayItems => TotalItems;
        public int DisplayStock => SelectedSheet?.Stock ?? TotalStock;
        public int DisplayUsed => SelectedSheet?.Used ?? TotalUsed;
        public int DisplayBalance => SelectedSheet?.Balance ?? TotalBalance;
        public decimal DisplaySqmInStock => SelectedSheet?.TotalSqmInStock ?? TotalSqmInStock;
        public decimal DisplaySqmBalance => SelectedSheet?.TotalSqmBalance ?? TotalSqmBalance;
        public decimal DisplayBuyingValue => SelectedSheet?.TotalPurchaseValue ?? TotalBuyingValue;
        public decimal DisplaySellingValue => SelectedSheet?.TotalSellValue ?? TotalSellingValue;
        public decimal DisplayProfit => SelectedSheet?.ProfitValue ?? TotalProfit;
        public bool IsRowSelected => SelectedSheet != null;
        public string SelectionLabel => SelectedSheet == null
            ? string.Empty
            : $"{SelectedSheet.Category} • {SelectedSheet.ThicknessDisplay} • {SelectedSheet.Color}";

        // ============ LAST UPDATED ============
        private DateTime? _lastUpdated;
        public DateTime? LastUpdated
        {
            get => _lastUpdated;
            set
            {
                _lastUpdated = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastUpdatedDisplay));
                OnPropertyChanged(nameof(HasEverBeenUpdated));
            }
        }

        public string LastUpdatedDisplay => LastUpdated.HasValue
            ? $"Last updated: {LastUpdated.Value:dd-MMM-yyyy (dddd) HH:mm:ss}"
            : "No changes yet";

        public bool HasEverBeenUpdated => LastUpdated.HasValue;

        public bool HasSheets => Sheets.Count > 0;
        public bool IsEmpty => Sheets.Count == 0;

        // ============ STATE ============
        private GlassSheet? _selectedSheet;
        public GlassSheet? SelectedSheet
        {
            get => _selectedSheet;
            set { _selectedSheet = value; OnPropertyChanged(); RaiseDisplayStatsChanged(); }
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
        public ICommand ClearSelectionCommand { get; }
        public ICommand ShowHistoryCommand { get; }

        public SheetStoreViewModel()
        {
            _repo = new SheetStoreRepository();
            _historyRepo = new SheetHistoryRepository();

            AddCommand = new RelayCommand(async _ => await AddAsync());
            EditCommand = new RelayCommand(async p => await EditAsync(p as GlassSheet ?? SelectedSheet));
            DeleteCommand = new RelayCommand(async p => await DeleteAsync(p as GlassSheet ?? SelectedSheet));
            BuyCommand = new RelayCommand(async p => await BuyAsync(p as GlassSheet ?? SelectedSheet));
            UseCommand = new RelayCommand(async p => await UseAsync(p as GlassSheet ?? SelectedSheet));
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            ClearSelectionCommand = new RelayCommand(_ => SelectedSheet = null);
            ShowHistoryCommand = new RelayCommand(_ => ShowHistory());

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

                SelectedSheet = null;
                LastUpdated = await _historyRepo.GetLastUpdatedAsync();
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
            OnPropertyChanged(nameof(HasSheets));
            OnPropertyChanged(nameof(IsEmpty));
            RaiseDisplayStatsChanged();
        }

        private void RaiseDisplayStatsChanged()
        {
            OnPropertyChanged(nameof(DisplayItems));
            OnPropertyChanged(nameof(DisplayStock));
            OnPropertyChanged(nameof(DisplayUsed));
            OnPropertyChanged(nameof(DisplayBalance));
            OnPropertyChanged(nameof(DisplaySqmInStock));
            OnPropertyChanged(nameof(DisplaySqmBalance));
            OnPropertyChanged(nameof(DisplayBuyingValue));
            OnPropertyChanged(nameof(DisplaySellingValue));
            OnPropertyChanged(nameof(DisplayProfit));
            OnPropertyChanged(nameof(IsRowSelected));
            OnPropertyChanged(nameof(SelectionLabel));
        }

        private static string DescribeSheet(GlassSheet s) =>
            $"{s.Category} • {s.ThicknessDisplay} • {s.Color} • {s.SizeDisplay}";

        private async Task AddAsync()
        {
            var vm = new AddEditSheetViewModel();
            var dialog = new AddEditSheetView(vm) { Owner = Application.Current.MainWindow };
            dialog.ShowDialog();

            if (vm.DialogResult == true && vm.Result != null)
            {
                try
                {
                    var added = await _repo.AddAsync(vm.Result);
                    await _historyRepo.LogAsync(
                        added.Id, DescribeSheet(added), "Added",
                        $"Initial stock: {added.Stock} sheets @ {added.PurchasePricePerSqm:N2} AED/SQM. " +
                        $"Total value: {added.TotalPurchaseValue:N2} AED.",
                        added.Stock, added.TotalPurchaseValue);
                    await LoadAsync();
                }
                catch (Exception ex) { ShowError("Could not add glass sheet.\n\n" + ex.Message); }
            }
        }

        private async Task EditAsync(GlassSheet? sheet)
        {
            if (sheet == null) return;

            var oldStock = sheet.Stock;
            var oldBuyPrice = sheet.PurchasePricePerSqm;
            var oldSellPrice = sheet.SellPricePerSqm;

            var vm = new AddEditSheetViewModel(sheet);
            var dialog = new AddEditSheetView(vm) { Owner = Application.Current.MainWindow };
            dialog.ShowDialog();

            if (vm.DialogResult == true && vm.Result != null)
            {
                try
                {
                    await _repo.UpdateAsync(vm.Result);

                    var changes = new System.Collections.Generic.List<string>();
                    if (oldStock != vm.Result.Stock) changes.Add($"Stock: {oldStock} → {vm.Result.Stock}");
                    if (oldBuyPrice != vm.Result.PurchasePricePerSqm) changes.Add($"Buy: {oldBuyPrice:N2} → {vm.Result.PurchasePricePerSqm:N2}");
                    if (oldSellPrice != vm.Result.SellPricePerSqm) changes.Add($"Sell: {oldSellPrice:N2} → {vm.Result.SellPricePerSqm:N2}");
                    var details = changes.Count > 0 ? string.Join(" | ", changes) : "Details edited";

                    await _historyRepo.LogAsync(vm.Result.Id, DescribeSheet(vm.Result), "Edited", details);
                    await LoadAsync();
                }
                catch (Exception ex) { ShowError("Could not update.\n\n" + ex.Message); }
            }
        }

        private async Task DeleteAsync(GlassSheet? sheet)
        {
            if (sheet == null) return;

            var result = MessageBox.Show(
                $"Delete this glass sheet?\n\n" +
                $"{sheet.Category} • {sheet.ThicknessDisplay} • {sheet.Color}\n" +
                $"Size: {sheet.SizeDisplay}\n" +
                $"Stock: {sheet.Stock} sheets ({sheet.TotalPurchaseValue:N0} AED)\n\n" +
                $"This cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var desc = DescribeSheet(sheet);
                    var stockLost = sheet.Stock;
                    var valueLost = sheet.TotalPurchaseValue;

                    await _repo.DeleteAsync(sheet.Id);
                    await _historyRepo.LogAsync(
                        sheet.Id, desc, "Deleted",
                        $"Removed from inventory. Lost {stockLost} sheets worth {valueLost:N2} AED.",
                        stockLost, valueLost);
                    await LoadAsync();
                }
                catch (Exception ex) { ShowError("Could not delete.\n\n" + ex.Message); }
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
                "Buy Stock", "10");

            if (int.TryParse(input, out int qty) && qty > 0)
            {
                try
                {
                    var oldStock = sheet.Stock;
                    await _repo.BuyStockAsync(sheet.Id, qty);
                    var value = qty * sheet.SqmPerSheet * sheet.PurchasePricePerSqm;

                    await _historyRepo.LogAsync(
                        sheet.Id, DescribeSheet(sheet), "Bought",
                        $"Purchased {qty} sheets. Stock: {oldStock} → {oldStock + qty}. Value: {value:N2} AED.",
                        qty, value);
                    await LoadAsync();
                }
                catch (Exception ex) { ShowError("Could not add stock.\n\n" + ex.Message); }
            }
        }

        private async Task UseAsync(GlassSheet? sheet)
        {
            if (sheet == null) return;

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                $"How many sheets to use?\n\n" +
                $"Available: {sheet.Balance} sheets ({sheet.TotalSqmBalance:N2} SQM)",
                "Use Stock", "1");

            if (int.TryParse(input, out int qty) && qty > 0)
            {
                try
                {
                    var oldUsed = sheet.Used;
                    await _repo.UseStockAsync(sheet.Id, qty);
                    var value = qty * sheet.SqmPerSheet * sheet.SellPricePerSqm;

                    await _historyRepo.LogAsync(
                        sheet.Id, DescribeSheet(sheet), "Used",
                        $"Consumed {qty} sheets. Used: {oldUsed} → {oldUsed + qty}. Value: {value:N2} AED.",
                        qty, value);
                    await LoadAsync();
                }
                catch (Exception ex) { ShowError(ex.Message); }
            }
        }

        private void ShowHistory()
        {
            var vm = new HistoryViewModel();
            var dialog = new HistoryView(vm) { Owner = Application.Current.MainWindow };
            dialog.ShowDialog();
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}