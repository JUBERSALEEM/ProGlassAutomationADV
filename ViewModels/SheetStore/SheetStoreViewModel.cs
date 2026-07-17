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

        // ============ STATS (computed) ============
        public int TotalItems => Sheets.Count;
        public int TotalStock => Sheets.Sum(s => s.Stock);
        public int TotalUsed => Sheets.Sum(s => s.Used);
        public int TotalBalance => Sheets.Sum(s => s.Balance);
        public decimal TotalValue => Sheets.Sum(s => s.BalanceValue);
        public decimal TotalCost => Sheets.Sum(s => s.TotalPurchaseValue);
        public decimal TotalSellPotential => Sheets.Sum(s => s.TotalSellValue);
        public decimal TotalProfit => TotalSellPotential - TotalCost;
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

            // Initial load
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                IsBusy = true;
                var all = await _repo.GetAllAsync();

                // In-memory search filter
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
            OnPropertyChanged(nameof(TotalValue));
            OnPropertyChanged(nameof(TotalCost));
            OnPropertyChanged(nameof(TotalSellPotential));
            OnPropertyChanged(nameof(TotalProfit));
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
                $"Supplier: {sheet.Supplier}\n\n" +
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

            // Simple quantity prompt for now — will build styled dialog later
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                $"How many sheets to buy?\n\nCurrent stock: {sheet.Stock}",
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
                $"How many sheets to use?\n\nAvailable: {sheet.Balance}",
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