using Microsoft.Win32;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.SheetStore;
using ProGlassAutomation.Services.SheetStore.SheetStoreCsv;
using ProGlassAutomation.Services.SheetStore.SheetStoreExcel;
using ProGlassAutomation.Services.SheetStore.SheetStorePdf;
using ProGlassAutomation.Services.SheetStore.SheetStoreHistory;
using ProGlassAutomation.Views.SheetStore;
using ProGlassAutomation.Views.SheetStore.SheetStoreBuy;
using ProGlassAutomation.Views.SheetStore.SheetStoreCsv;
using ProGlassAutomation.Views.SheetStore.SheetStoreHistory;
using ProGlassAutomation.Views.SheetStore.SheetStoreUse;
using ProGlassAutomation.ViewModels.SheetStore.SheetStoreBuy;
using ProGlassAutomation.ViewModels.SheetStore.SheetStoreCsv;
using ProGlassAutomation.ViewModels.SheetStore.SheetStoreHistory;
using ProGlassAutomation.ViewModels.SheetStore.SheetStoreUse;
using System;
using System.Collections.Generic;
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
        private readonly SheetStoreCsvService _csvService;
        private readonly SheetStoreExcelService _excelService;
        private readonly SheetStorePdfService _pdfService;

        private List<GlassSheet> _allSheets = new();

        public ObservableCollection<GlassSheet> Sheets { get; } = new();

        // ============ FILTER DROPDOWN OPTIONS ============
        public ObservableCollection<string> CategoryOptions { get; } = new();
        public ObservableCollection<string> ThicknessOptions { get; } = new();
        public ObservableCollection<string> ColorOptions { get; } = new();
        public ObservableCollection<string> SupplierOptions { get; } = new();

        // ============ SORT OPTIONS ============
        private const string DEFAULT_SORT = "Category → Color → Thickness";

        public ObservableCollection<string> SortOptions { get; } = new()
        {
            DEFAULT_SORT,
            "Newest first",
            "Oldest first",
            "Stock: Low to High",
            "Stock: High to Low",
            "Balance: Low to High",
            "Value: High to Low",
            "Value: Low to High",
            "Category A-Z",
            "Thickness: Thin to Thick"
        };

        private string _selectedSort = DEFAULT_SORT;
        public string SelectedSort
        {
            get => _selectedSort;
            set { if (_selectedSort == value) return; _selectedSort = value; OnPropertyChanged(); ApplyFilters(); }
        }

        // ============ FILTER SELECTIONS ============
        private const string ALL = "All";

        private string _selectedCategory = ALL;
        public string SelectedCategory
        {
            get => _selectedCategory;
            set { if (_selectedCategory == value) return; _selectedCategory = value; OnPropertyChanged(); ApplyFilters(); }
        }

        private string _selectedThickness = ALL;
        public string SelectedThickness
        {
            get => _selectedThickness;
            set { if (_selectedThickness == value) return; _selectedThickness = value; OnPropertyChanged(); ApplyFilters(); }
        }

        private string _selectedColor = ALL;
        public string SelectedColor
        {
            get => _selectedColor;
            set { if (_selectedColor == value) return; _selectedColor = value; OnPropertyChanged(); ApplyFilters(); }
        }

        private string _selectedSupplier = ALL;
        public string SelectedSupplier
        {
            get => _selectedSupplier;
            set { if (_selectedSupplier == value) return; _selectedSupplier = value; OnPropertyChanged(); ApplyFilters(); }
        }

        public bool HasActiveFilters =>
            SelectedCategory != ALL ||
            SelectedThickness != ALL ||
            SelectedColor != ALL ||
            SelectedSupplier != ALL ||
            SelectedSort != DEFAULT_SORT ||
            !string.IsNullOrWhiteSpace(SearchText);

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

        // ============ DISPLAY STATS ============
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
            set { if (_searchText == value) return; _searchText = value; OnPropertyChanged(); ApplyFilters(); }
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
        public ICommand ClearFiltersCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ImportCsvCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportPdfCommand { get; }

        public SheetStoreViewModel()
        {
            _repo = new SheetStoreRepository();
            _historyRepo = new SheetHistoryRepository();
            _csvService = new SheetStoreCsvService();
            _excelService = new SheetStoreExcelService();
            _pdfService = new SheetStorePdfService();

            AddCommand = new RelayCommand(async _ => await AddAsync());
            EditCommand = new RelayCommand(async p => await EditAsync(p as GlassSheet ?? SelectedSheet));
            DeleteCommand = new RelayCommand(async p => await DeleteAsync(p as GlassSheet ?? SelectedSheet));
            BuyCommand = new RelayCommand(async p => await BuyAsync(p as GlassSheet ?? SelectedSheet));
            UseCommand = new RelayCommand(async p => await UseAsync(p as GlassSheet ?? SelectedSheet));
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            ClearSelectionCommand = new RelayCommand(_ => SelectedSheet = null);
            ShowHistoryCommand = new RelayCommand(_ => ShowHistory());
            ClearFiltersCommand = new RelayCommand(_ => ClearAllFilters());
            ExportCsvCommand = new RelayCommand(async _ => await ExportCsvAsync());
            ImportCsvCommand = new RelayCommand(async _ => await ImportCsvAsync());
            ExportExcelCommand = new RelayCommand(async _ => await ExportExcelAsync());
            ExportPdfCommand = new RelayCommand(async _ => await ExportPdfAsync());

            _ = LoadAsync();
        }

        // ============ LOAD ============

        private async Task LoadAsync()
        {
            try
            {
                IsBusy = true;
                _allSheets = await _repo.GetAllAsync();
                RebuildFilterOptions();
                ApplyFilters();
                SelectedSheet = null;
                LastUpdated = await _historyRepo.GetLastUpdatedAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ============ FILTERS ============

        private void RebuildFilterOptions()
        {
            var oldCategory = SelectedCategory;
            var oldThickness = SelectedThickness;
            var oldColor = SelectedColor;
            var oldSupplier = SelectedSupplier;

            CategoryOptions.Clear();
            CategoryOptions.Add(ALL);
            foreach (var c in _allSheets.Select(s => s.Category).Distinct().OrderBy(c => c))
                CategoryOptions.Add(c);

            ThicknessOptions.Clear();
            ThicknessOptions.Add(ALL);
            foreach (var t in _allSheets.Select(s => s.ThicknessDisplay).Distinct().OrderBy(t => t))
                ThicknessOptions.Add(t);

            ColorOptions.Clear();
            ColorOptions.Add(ALL);
            foreach (var c in _allSheets.Select(s => s.Color).Distinct().OrderBy(c => c))
                ColorOptions.Add(c);

            SupplierOptions.Clear();
            SupplierOptions.Add(ALL);
            foreach (var s in _allSheets.Select(x => x.Supplier).Distinct().OrderBy(s => s))
                SupplierOptions.Add(s);

            _selectedCategory = CategoryOptions.Contains(oldCategory) ? oldCategory : ALL;
            _selectedThickness = ThicknessOptions.Contains(oldThickness) ? oldThickness : ALL;
            _selectedColor = ColorOptions.Contains(oldColor) ? oldColor : ALL;
            _selectedSupplier = SupplierOptions.Contains(oldSupplier) ? oldSupplier : ALL;

            OnPropertyChanged(nameof(SelectedCategory));
            OnPropertyChanged(nameof(SelectedThickness));
            OnPropertyChanged(nameof(SelectedColor));
            OnPropertyChanged(nameof(SelectedSupplier));
        }

        private void ApplyFilters()
        {
            IEnumerable<GlassSheet> filtered = _allSheets;

            if (SelectedCategory != ALL)
                filtered = filtered.Where(s => s.Category == SelectedCategory);
            if (SelectedThickness != ALL)
                filtered = filtered.Where(s => s.ThicknessDisplay == SelectedThickness);
            if (SelectedColor != ALL)
                filtered = filtered.Where(s => s.Color == SelectedColor);
            if (SelectedSupplier != ALL)
                filtered = filtered.Where(s => s.Supplier == SelectedSupplier);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var q = SearchText.Trim().ToLower();
                filtered = filtered.Where(g =>
                    g.Category.ToLower().Contains(q) ||
                    g.Color.ToLower().Contains(q) ||
                    g.Supplier.ToLower().Contains(q) ||
                    g.ThicknessDisplay.ToLower().Contains(q));
            }

            filtered = ApplySort(filtered);

            Sheets.Clear();
            foreach (var s in filtered) Sheets.Add(s);

            RaiseStatsChanged();
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        private IEnumerable<GlassSheet> ApplySort(IEnumerable<GlassSheet> data)
        {
            return SelectedSort switch
            {
                "Newest first" => data.OrderByDescending(s => s.CreatedAt),
                "Oldest first" => data.OrderBy(s => s.CreatedAt),
                "Stock: Low to High" => data.OrderBy(s => s.Stock),
                "Stock: High to Low" => data.OrderByDescending(s => s.Stock),
                "Balance: Low to High" => data.OrderBy(s => s.Balance),
                "Value: High to Low" => data.OrderByDescending(s => s.TotalPurchaseValue),
                "Value: Low to High" => data.OrderBy(s => s.TotalPurchaseValue),
                "Category A-Z" => data.OrderBy(s => s.Category),
                "Thickness: Thin to Thick" => data.OrderBy(s => s.ThicknessMm),
                _ => data.OrderBy(s => s.Category).ThenBy(s => s.Color).ThenBy(s => s.ThicknessMm)
            };
        }

        private void ClearAllFilters()
        {
            _selectedCategory = ALL;
            _selectedThickness = ALL;
            _selectedColor = ALL;
            _selectedSupplier = ALL;
            _selectedSort = DEFAULT_SORT;
            _searchText = string.Empty;

            OnPropertyChanged(nameof(SelectedCategory));
            OnPropertyChanged(nameof(SelectedThickness));
            OnPropertyChanged(nameof(SelectedColor));
            OnPropertyChanged(nameof(SelectedSupplier));
            OnPropertyChanged(nameof(SelectedSort));
            OnPropertyChanged(nameof(SearchText));

            ApplyFilters();
        }

        // ============ STATS ============

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

        // ============ HELPERS ============

        private static string DescribeSheet(GlassSheet s) =>
            $"{s.Category} • {s.ThicknessDisplay} • {s.Color} • {s.SizeDisplay}";

        private GlassSheet? FindDuplicate(GlassSheet incoming) =>
            _allSheets.FirstOrDefault(s =>
                s.Category == incoming.Category &&
                s.ThicknessMm == incoming.ThicknessMm &&
                s.Color == incoming.Color &&
                s.WidthMm == incoming.WidthMm &&
                s.HeightMm == incoming.HeightMm &&
                s.Supplier == incoming.Supplier);

        private string BuildFilterInfoString()
        {
            var filters = new List<string>();
            if (SelectedCategory != ALL) filters.Add($"Category: {SelectedCategory}");
            if (SelectedThickness != ALL) filters.Add($"Thickness: {SelectedThickness}");
            if (SelectedColor != ALL) filters.Add($"Color: {SelectedColor}");
            if (SelectedSupplier != ALL) filters.Add($"Supplier: {SelectedSupplier}");
            if (SelectedSort != DEFAULT_SORT) filters.Add($"Sort: {SelectedSort}");
            if (!string.IsNullOrWhiteSpace(SearchText)) filters.Add($"Search: \"{SearchText}\"");
            return filters.Count > 0
                ? string.Join(" • ", filters)
                : "All sheets (no filters)";
        }

        private void ShowError(string message) =>
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

        // ============ CSV EXPORT ============

        private async Task ExportCsvAsync()
        {
            if (!Sheets.Any())
            {
                MessageBox.Show("Nothing to export — the current view is empty.",
                    "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Sheet Store to CSV",
                Filter = "CSV file (*.csv)|*.csv",
                FileName = $"SheetStore_{DateTime.Now:yyyy-MM-dd_HH-mm}.csv"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                await _csvService.ExportAsync(dialog.FileName, Sheets.ToList());
                MessageBox.Show(
                    $"Exported {Sheets.Count} sheets to:\n\n{dialog.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("Export failed.\n\n" + ex.Message);
            }
        }

        // ============ EXCEL EXPORT ============

        private async Task ExportExcelAsync()
        {
            if (!Sheets.Any())
            {
                MessageBox.Show("Nothing to export — the current view is empty.",
                    "Export Excel", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Sheet Store to Excel",
                Filter = "Excel file (*.xlsx)|*.xlsx",
                FileName = $"SheetStore_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                await _excelService.ExportAsync(dialog.FileName, Sheets.ToList(), BuildFilterInfoString());
                MessageBox.Show(
                    $"Exported {Sheets.Count} sheets to Excel:\n\n{dialog.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("Excel export failed.\n\n" + ex.Message);
            }
        }

        // ============ PDF EXPORT ============

        private async Task ExportPdfAsync()
        {
            if (!Sheets.Any())
            {
                MessageBox.Show("Nothing to export — the current view is empty.",
                    "Export PDF", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Sheet Store Report as PDF",
                Filter = "PDF file (*.pdf)|*.pdf",
                FileName = $"SheetStore_Report_{DateTime.Now:yyyy-MM-dd_HH-mm}.pdf"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                await _pdfService.GenerateReportAsync(dialog.FileName, Sheets.ToList(), BuildFilterInfoString());
                MessageBox.Show(
                    $"PDF report generated ({Sheets.Count} sheets):\n\n{dialog.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("PDF export failed.\n\n" + ex.Message);
            }
        }

        // ============ CSV IMPORT ============

        private async Task ImportCsvAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import Sheets from CSV",
                Filter = "CSV file (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true) return;

            List<GlassSheet> imported;
            try
            {
                imported = await _csvService.ImportAsync(dialog.FileName);
            }
            catch (Exception ex)
            {
                ShowError("Could not read CSV file.\n\n" + ex.Message);
                return;
            }

            if (imported.Count == 0)
            {
                MessageBox.Show("The CSV file has no rows to import.",
                    "Import CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var duplicates = imported.Where(row => FindDuplicate(row) != null).ToList();

            int addedCount = 0, stockAddedCount = 0, updatedCount = 0, skippedCount = 0;
            DuplicateAction? rememberedAction = null;
            int duplicateNumber = 0;

            try
            {
                foreach (var row in imported)
                {
                    var existing = FindDuplicate(row);

                    if (existing == null)
                    {
                        var added = await _repo.AddAsync(row);
                        await _historyRepo.LogAsync(
                            added.Id, DescribeSheet(added), "Added",
                            $"[CSV Import] Initial stock: {added.Stock} sheets. Value: {added.TotalPurchaseValue:N2} AED.",
                            added.Stock, added.TotalPurchaseValue);
                        addedCount++;
                        _allSheets = await _repo.GetAllAsync();
                        continue;
                    }

                    duplicateNumber++;
                    DuplicateAction action;

                    if (rememberedAction.HasValue)
                    {
                        action = rememberedAction.Value;
                    }
                    else
                    {
                        var dupVm = new DuplicateResolutionViewModel(existing, row, duplicateNumber, duplicates.Count);
                        var dupDialog = new DuplicateResolutionView(dupVm) { Owner = Application.Current.MainWindow };
                        dupDialog.ShowDialog();

                        if (dupVm.Result == null) break;
                        action = dupVm.Result.Value;
                        if (dupVm.ApplyToRemaining) rememberedAction = action;
                    }

                    if (action == DuplicateAction.Cancel) break;

                    switch (action)
                    {
                        case DuplicateAction.Skip:
                            skippedCount++;
                            break;

                        case DuplicateAction.AddToStock:
                            if (row.Stock > 0)
                            {
                                var oldStock = existing.Stock;
                                await _repo.BuyStockAsync(existing.Id, row.Stock);
                                var value = row.Stock * existing.SqmPerSheet * existing.PurchasePricePerSqm;
                                await _historyRepo.LogAsync(
                                    existing.Id, DescribeSheet(existing), "Bought",
                                    $"[CSV Import] Added {row.Stock} sheets. Stock: {oldStock} → {oldStock + row.Stock}. Value: {value:N2} AED.",
                                    row.Stock, value);
                                stockAddedCount++;
                            }
                            else skippedCount++;
                            break;

                        case DuplicateAction.UpdateAll:
                            existing.PurchasePricePerSqm = row.PurchasePricePerSqm;
                            existing.SellPricePerSqm = row.SellPricePerSqm;
                            existing.Stock = row.Stock;
                            existing.Used = row.Used;
                            await _repo.UpdateAsync(existing);
                            await _historyRepo.LogAsync(
                                existing.Id, DescribeSheet(existing), "Edited",
                                $"[CSV Import] Overwritten. Buy: {existing.PurchasePricePerSqm:N2}, " +
                                $"Sell: {existing.SellPricePerSqm:N2}, Stock: {existing.Stock}, Used: {existing.Used}.");
                            updatedCount++;
                            break;
                    }
                }

                await LoadAsync();

                MessageBox.Show(
                    $"Import complete!\n\n" +
                    $"✓ {addedCount} new sheets added\n" +
                    $"✓ {stockAddedCount} duplicates got stock added\n" +
                    $"✓ {updatedCount} sheets updated\n" +
                    $"✕ {skippedCount} duplicates skipped",
                    "Import Summary", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("Import failed partway.\n\n" + ex.Message);
                await LoadAsync();
            }
        }

        // ============ CRUD ============

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

                    var changes = new List<string>();
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
                    await _historyRepo.LogAsync(sheet.Id, desc, "Deleted",
                        $"Removed from inventory. Lost {stockLost} sheets worth {valueLost:N2} AED.",
                        stockLost, valueLost);
                    await LoadAsync();
                }
                catch (Exception ex) { ShowError("Could not delete.\n\n" + ex.Message); }
            }
        }

        // ============ BUY ============

        private async Task BuyAsync(GlassSheet? sheet)
        {
            if (sheet == null) return;

            var vm = new BuyDialogViewModel(sheet);
            var dialog = new BuyDialogView(vm) { Owner = Application.Current.MainWindow };
            dialog.ShowDialog();

            if (vm.DialogResult == true && vm.Quantity > 0)
            {
                try
                {
                    var oldStock = sheet.Stock;
                    await _repo.BuyStockAsync(sheet.Id, vm.Quantity);
                    var value = vm.Quantity * sheet.SqmPerSheet * sheet.PurchasePricePerSqm;

                    await _historyRepo.LogAsync(
                        sheet.Id, DescribeSheet(sheet), "Bought",
                        $"Purchased {vm.Quantity} sheets. Stock: {oldStock} → {oldStock + vm.Quantity}. Value: {value:N2} AED.",
                        vm.Quantity, value);
                    await LoadAsync();
                }
                catch (Exception ex) { ShowError("Could not add stock.\n\n" + ex.Message); }
            }
        }

        // ============ USE ============

        private async Task UseAsync(GlassSheet? sheet)
        {
            if (sheet == null) return;

            if (sheet.Balance <= 0)
            {
                ShowError("No stock available to use.");
                return;
            }

            var vm = new UseDialogViewModel(sheet);
            var dialog = new UseDialogView(vm) { Owner = Application.Current.MainWindow };
            dialog.ShowDialog();

            if (vm.DialogResult == true && vm.Quantity > 0)
            {
                try
                {
                    var oldUsed = sheet.Used;
                    await _repo.UseStockAsync(sheet.Id, vm.Quantity);
                    var value = vm.Quantity * sheet.SqmPerSheet * sheet.SellPricePerSqm;

                    var jobRef = string.IsNullOrWhiteSpace(vm.JobOrderReference)
                        ? ""
                        : $" [Job: {vm.JobOrderReference.Trim()}]";

                    await _historyRepo.LogAsync(
                        sheet.Id, DescribeSheet(sheet), "Used",
                        $"Consumed {vm.Quantity} sheets.{jobRef} Used: {oldUsed} → {oldUsed + vm.Quantity}. Value: {value:N2} AED.",
                        vm.Quantity, value);
                    await LoadAsync();
                }
                catch (Exception ex) { ShowError(ex.Message); }
            }
        }

        // ============ HISTORY ============

        private void ShowHistory()
        {
            var vm = new HistoryViewModel();
            var dialog = new HistoryView(vm) { Owner = Application.Current.MainWindow };
            dialog.ShowDialog();
        }
    }
}