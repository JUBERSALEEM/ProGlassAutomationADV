using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Models.TaxInvoice.TaxInvoiceFamily;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceList
{
    /// <summary>
    /// ViewModel for the Tax Invoice list landing page.
    ///
    /// 5 tabs: All | Draft | Issued | Paid | By Project
    /// 5 hero cards: dynamic — dataset stats or selected invoice stats.
    ///
    /// FIXES vs first delivery:
    ///   • Removed View reference from VM (MVVM violation fixed)
    ///   • ChangeStatusCommand tuple → IssueRowCommand + MarkPaidRowCommand
    ///   • CreateInvoiceCommand fires OnCreateInvoiceRequested callback only
    ///   • All missing using statements added
    /// </summary>
    public class TaxInvoiceListViewModel : ViewModelBase
    {
        private int _loadingFlag = 0;

        // ── Callbacks to shell VM ─────────────────────────────────────────
        public Action<int>? OnInvoiceOpenRequested { get; set; }

        /// <summary>
        /// Shell VM subscribes to this — opens ProformaPickerDialog
        /// from TaxInvoiceViewModel (shell), not from here.
        /// MVVM: Views/dialogs opened by shell VM only.
        /// </summary>
        public Action? OnCreateInvoiceRequested { get; set; }

        /// <summary>
        /// Shell VM subscribes — called with proformaId after picker confirms.
        /// </summary>
        public Action<string>? OnProformaSelectedForInvoice { get; set; }

        // ══════════════════════════════════════════════════════════════════
        //  SELECTION
        // ══════════════════════════════════════════════════════════════════

        private TaxInvoiceRowViewModel? _selectedRow;
        public TaxInvoiceRowViewModel? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (_selectedRow == value) return;
                if (_selectedRow != null) _selectedRow.IsSelected = false;
                _selectedRow = value;
                if (_selectedRow != null) _selectedRow.IsSelected = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(HeroCard1Value));
                OnPropertyChanged(nameof(HeroCard1Label));
                OnPropertyChanged(nameof(HeroCard2Value));
                OnPropertyChanged(nameof(HeroCard2Label));
                OnPropertyChanged(nameof(HeroCard3Value));
                OnPropertyChanged(nameof(HeroCard3Label));
                OnPropertyChanged(nameof(HeroCard4Value));
                OnPropertyChanged(nameof(HeroCard4Label));
                OnPropertyChanged(nameof(HeroCard5Value));
                OnPropertyChanged(nameof(HeroCard5Label));
                OnPropertyChanged(nameof(SelectedInvoiceMetaLine));
            }
        }

        public bool HasSelection => _selectedRow != null;

        // ── Hero cards ────────────────────────────────────────────────────

        public string HeroCard1Value => _selectedRow != null
            ? _selectedRow.GrandTotalDisplay
            : $"AED {_totalInvoicedAmount:N2}";
        public string HeroCard1Label => _selectedRow != null
            ? "Invoice Total" : "Total Invoiced (AED)";

        public string HeroCard2Value => _selectedRow != null
            ? _selectedRow.VatDisplay
            : DraftCount.ToString();
        public string HeroCard2Label => _selectedRow != null
            ? "VAT Amount" : "Draft Invoices";

        public string HeroCard3Value => _selectedRow != null
            ? _selectedRow.SubtotalDisplay
            : IssuedCount.ToString();
        public string HeroCard3Label => _selectedRow != null
            ? "Subtotal (ex VAT)" : "Issued Invoices";

        public string HeroCard4Value => _selectedRow != null
            ? _selectedRow.PaymentTerms
            : PaidCount.ToString();
        public string HeroCard4Label => _selectedRow != null
            ? "Payment Terms" : "Paid Invoices";

        public string HeroCard5Value => _selectedRow != null
            ? _selectedRow.DueDateLabel
            : OverdueCount.ToString();
        public string HeroCard5Label => _selectedRow != null
            ? "Due Date" : "Overdue Invoices";

        public string SelectedInvoiceMetaLine => _selectedRow == null ? "" :
            $"{_selectedRow.InvoiceNumber}  ·  " +
            $"📋 {_selectedRow.ProformaId}  ·  " +
            $"📅 {_selectedRow.IssueDateDisplay}  ·  " +
            $"{_selectedRow.StatusIcon} {_selectedRow.StatusLabel}";

        // ══════════════════════════════════════════════════════════════════
        //  TAB INDEX
        // ══════════════════════════════════════════════════════════════════

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (_selectedTabIndex == value) return;
                _selectedTabIndex = value;
                OnPropertyChanged();
                SelectedRow = null;
                _ = LoadTabAsync(value);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  TAB COLLECTIONS
        // ══════════════════════════════════════════════════════════════════

        private ObservableCollection<TaxInvoiceRowViewModel> _allItems = new();
        public ObservableCollection<TaxInvoiceRowViewModel> AllItems
        {
            get => _allItems;
            private set
            {
                _allItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AllCount));
            }
        }

        private ObservableCollection<TaxInvoiceRowViewModel> _draftItems = new();
        public ObservableCollection<TaxInvoiceRowViewModel> DraftItems
        {
            get => _draftItems;
            private set { _draftItems = value; OnPropertyChanged(); }
        }

        private ObservableCollection<TaxInvoiceRowViewModel> _issuedItems = new();
        public ObservableCollection<TaxInvoiceRowViewModel> IssuedItems
        {
            get => _issuedItems;
            private set { _issuedItems = value; OnPropertyChanged(); }
        }

        private ObservableCollection<TaxInvoiceRowViewModel> _paidItems = new();
        public ObservableCollection<TaxInvoiceRowViewModel> PaidItems
        {
            get => _paidItems;
            private set { _paidItems = value; OnPropertyChanged(); }
        }

        private ObservableCollection<InvoiceFamilyRowViewModel> _familyItems = new();
        public ObservableCollection<InvoiceFamilyRowViewModel> FamilyItems
        {
            get => _familyItems;
            private set
            {
                _familyItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FamilyCount));
            }
        }

        // ── Tab badge counts ──────────────────────────────────────────────

        public int AllCount => _allItems.Count;
        public int FamilyCount => _familyItems.Count;

        private int _draftCount;
        public int DraftCount
        {
            get => _draftCount;
            private set
            {
                _draftCount = value;
                OnPropertyChanged();
                if (_selectedRow == null) OnPropertyChanged(nameof(HeroCard2Value));
            }
        }

        private int _issuedCount;
        public int IssuedCount
        {
            get => _issuedCount;
            private set
            {
                _issuedCount = value;
                OnPropertyChanged();
                if (_selectedRow == null) OnPropertyChanged(nameof(HeroCard3Value));
            }
        }

        private int _paidCount;
        public int PaidCount
        {
            get => _paidCount;
            private set
            {
                _paidCount = value;
                OnPropertyChanged();
                if (_selectedRow == null) OnPropertyChanged(nameof(HeroCard4Value));
            }
        }

        private int _overdueCount;
        public int OverdueCount
        {
            get => _overdueCount;
            private set
            {
                _overdueCount = value;
                OnPropertyChanged();
                if (_selectedRow == null) OnPropertyChanged(nameof(HeroCard5Value));
            }
        }

        // ── KPI ───────────────────────────────────────────────────────────

        private decimal _totalInvoicedAmount;
        public decimal TotalInvoicedAmount
        {
            get => _totalInvoicedAmount;
            private set
            {
                _totalInvoicedAmount = value;
                OnPropertyChanged();
                if (_selectedRow == null) OnPropertyChanged(nameof(HeroCard1Value));
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOADING / ERROR
        // ══════════════════════════════════════════════════════════════════

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); }
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            private set { _hasError = value; OnPropertyChanged(); }
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            private set { _errorMessage = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════════════
        //  FILTERS
        // ══════════════════════════════════════════════════════════════════

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                SelectedRow = null;
                ApplyFilters();
            }
        }

        private string _selectedStatusFilter = "All Statuses";
        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                if (_selectedStatusFilter == value) return;
                _selectedStatusFilter = value;
                OnPropertyChanged();
                SelectedRow = null;
                ApplyFilters();
            }
        }

        private string _selectedTypeFilter = "All Types";
        public string SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set
            {
                if (_selectedTypeFilter == value) return;
                _selectedTypeFilter = value;
                OnPropertyChanged();
                SelectedRow = null;
                ApplyFilters();
            }
        }

        public List<string> StatusFilterOptions { get; } = new()
        {
            "All Statuses", "Draft", "Issued", "Paid",
            "PartiallyPaid", "Overdue", "Cancelled", "Void"
        };

        public List<string> TypeFilterOptions { get; } = new()
        {
            "All Types", "Full", "Partial"
        };

        // Raw source lists
        private List<TaxInvoiceRowViewModel> _rawAll = new();
        private List<TaxInvoiceRowViewModel> _rawDraft = new();
        private List<TaxInvoiceRowViewModel> _rawIssued = new();
        private List<TaxInvoiceRowViewModel> _rawPaid = new();

        // ══════════════════════════════════════════════════════════════════
        //  COMMANDS
        // ══════════════════════════════════════════════════════════════════

        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand DeleteCommand { get; }

        /// <summary>
        /// Issues a Draft invoice — takes TaxInvoiceRowViewModel.
        /// Replaces the broken ValueTuple ChangeStatusCommand pattern.
        /// </summary>
        public ICommand IssueRowCommand { get; }

        /// <summary>
        /// Marks an Issued invoice as Paid — takes TaxInvoiceRowViewModel.
        /// Replaces the broken ValueTuple ChangeStatusCommand pattern.
        /// </summary>
        public ICommand MarkPaidRowCommand { get; }

        public ICommand SelectRowCommand { get; }
        public ICommand ClearSelectionCommand { get; }

        /// <summary>
        /// Fires OnCreateInvoiceRequested callback to shell VM.
        /// Shell VM opens ProformaPickerDialog — NOT this VM.
        /// </summary>
        public ICommand CreateInvoiceCommand { get; }

        public ICommand OpenFamilyCommand { get; }

        // ══════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════════

        public TaxInvoiceListViewModel()
        {
            RefreshCommand = new RelayCommand(async _ =>
            {
                SelectedRow = null;
                await LoadAsync();
            });

            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

            OpenCommand = new RelayCommand(param =>
            {
                if (param is TaxInvoiceRowViewModel row)
                    OnInvoiceOpenRequested?.Invoke(row.Id);
            });

            SelectRowCommand = new RelayCommand(param =>
            {
                if (param is TaxInvoiceRowViewModel row)
                    SelectedRow = SelectedRow == row ? null : row;
            });

            ClearSelectionCommand = new RelayCommand(_ => SelectedRow = null);

            DeleteCommand = new RelayCommand(async param =>
            {
                if (param is TaxInvoiceRowViewModel row)
                    await DeleteAsync(row);
            });

            // FIX: separate commands — no ValueTuple from XAML
            IssueRowCommand = new RelayCommand(async param =>
            {
                if (param is TaxInvoiceRowViewModel row)
                    await ChangeStatusAsync(row, "Issued");
            },
            param => param is TaxInvoiceRowViewModel r && r.IsDraft);

            MarkPaidRowCommand = new RelayCommand(async param =>
            {
                if (param is TaxInvoiceRowViewModel row)
                    await ChangeStatusAsync(row, "Paid");
            },
            param => param is TaxInvoiceRowViewModel r && r.IsIssued);

            // FIX: no View reference — fire callback to shell VM only
            CreateInvoiceCommand = new RelayCommand(_ =>
                OnCreateInvoiceRequested?.Invoke());

            OpenFamilyCommand = new RelayCommand(param =>
            {
                if (param is InvoiceFamilyRowViewModel family
                    && family.LatestInvoiceId > 0)
                    OnInvoiceOpenRequested?.Invoke(family.LatestInvoiceId);
            });
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOAD
        // ══════════════════════════════════════════════════════════════════

        public async Task LoadAsync()
        {
            if (Interlocked.CompareExchange(ref _loadingFlag, 1, 0) != 0)
                return;

            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = "";

                if (App.TaxInvoices == null)
                {
                    HasError = true;
                    ErrorMessage = "Tax Invoices database not initialized.";
                    return;
                }

                var allTask = App.TaxInvoices.GetAllAsync();
                var draftTask = App.TaxInvoices.GetDraftAsync();
                var issuedTask = App.TaxInvoices.GetIssuedAsync();
                var paidTask = App.TaxInvoices.GetPaidAsync();
                var overdueTask = App.TaxInvoices.GetOverdueAsync();
                var famTask = App.TaxInvoices.GetAllFamiliesAsync();

                await Task.WhenAll(
                    allTask, draftTask, issuedTask,
                    paidTask, overdueTask, famTask);

                _rawAll = allTask.Result
                    .Select(x => new TaxInvoiceRowViewModel(x)).ToList();
                _rawDraft = draftTask.Result
                    .Select(x => new TaxInvoiceRowViewModel(x)).ToList();
                _rawIssued = issuedTask.Result
                    .Select(x => new TaxInvoiceRowViewModel(x)).ToList();
                _rawPaid = paidTask.Result
                    .Select(x => new TaxInvoiceRowViewModel(x)).ToList();

                var families = InvoiceFamilyRowViewModel
                    .FromFamilies(famTask.Result);

                AllItems = new ObservableCollection<TaxInvoiceRowViewModel>(_rawAll);
                DraftItems = new ObservableCollection<TaxInvoiceRowViewModel>(_rawDraft);
                IssuedItems = new ObservableCollection<TaxInvoiceRowViewModel>(_rawIssued);
                PaidItems = new ObservableCollection<TaxInvoiceRowViewModel>(_rawPaid);
                FamilyItems = new ObservableCollection<InvoiceFamilyRowViewModel>(families);

                DraftCount = _rawDraft.Count;
                IssuedCount = _rawIssued.Count;
                PaidCount = _rawPaid.Count;
                OverdueCount = overdueTask.Result.Count;

                TotalInvoicedAmount = _rawAll
                    .Where(x => !x.IsDraft)
                    .Sum(x => x.GrandTotal);

                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load invoices: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                Interlocked.Exchange(ref _loadingFlag, 0);
            }
        }

        private async Task LoadTabAsync(int tabIndex)
        {
            if (App.TaxInvoices == null) return;

            try
            {
                switch (tabIndex)
                {
                    case 1:
                        var draft = await App.TaxInvoices.GetDraftAsync();
                        _rawDraft = draft
                            .Select(x => new TaxInvoiceRowViewModel(x)).ToList();
                        DraftItems = new ObservableCollection<TaxInvoiceRowViewModel>(
                            _rawDraft);
                        DraftCount = _rawDraft.Count;
                        break;

                    case 2:
                        var issued = await App.TaxInvoices.GetIssuedAsync();
                        _rawIssued = issued
                            .Select(x => new TaxInvoiceRowViewModel(x)).ToList();
                        IssuedItems = new ObservableCollection<TaxInvoiceRowViewModel>(
                            _rawIssued);
                        IssuedCount = _rawIssued.Count;
                        break;

                    case 3:
                        var paid = await App.TaxInvoices.GetPaidAsync();
                        _rawPaid = paid
                            .Select(x => new TaxInvoiceRowViewModel(x)).ToList();
                        PaidItems = new ObservableCollection<TaxInvoiceRowViewModel>(
                            _rawPaid);
                        PaidCount = _rawPaid.Count;
                        break;

                    case 4:
                        var fams = await App.TaxInvoices.GetAllFamiliesAsync();
                        FamilyItems = new ObservableCollection<InvoiceFamilyRowViewModel>(
                            InvoiceFamilyRowViewModel.FromFamilies(fams));
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to reload tab: {ex.Message}",
                    "Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  FILTER
        // ══════════════════════════════════════════════════════════════════

        private void ApplyFilters()
        {
            AllItems = new ObservableCollection<TaxInvoiceRowViewModel>(
                Filter(_rawAll));
            DraftItems = new ObservableCollection<TaxInvoiceRowViewModel>(
                Filter(_rawDraft));
            IssuedItems = new ObservableCollection<TaxInvoiceRowViewModel>(
                Filter(_rawIssued));
            PaidItems = new ObservableCollection<TaxInvoiceRowViewModel>(
                Filter(_rawPaid));
        }

        private IEnumerable<TaxInvoiceRowViewModel> Filter(
            IEnumerable<TaxInvoiceRowViewModel> source)
        {
            IEnumerable<TaxInvoiceRowViewModel> q = source;

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string s = _searchText.Trim().ToLowerInvariant();
                q = q.Where(x =>
                    x.InvoiceNumber.ToLowerInvariant().Contains(s) ||
                    x.ProformaId.ToLowerInvariant().Contains(s) ||
                    x.CustomerName.ToLowerInvariant().Contains(s) ||
                    x.ProjectName.ToLowerInvariant().Contains(s) ||
                    x.PurchaseOrderNumber.ToLowerInvariant().Contains(s) ||
                    x.DeliveryNoteReference.ToLowerInvariant().Contains(s));
            }

            if (_selectedStatusFilter != "All Statuses")
                q = q.Where(x => string.Equals(
                    x.Status, _selectedStatusFilter,
                    StringComparison.OrdinalIgnoreCase));

            if (_selectedTypeFilter != "All Types")
                q = q.Where(x => string.Equals(
                    x.InvoiceType, _selectedTypeFilter,
                    StringComparison.OrdinalIgnoreCase));

            return q;
        }

        private void ClearFilters()
        {
            _searchText = "";
            _selectedStatusFilter = "All Statuses";
            _selectedTypeFilter = "All Types";
            SelectedRow = null;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedStatusFilter));
            OnPropertyChanged(nameof(SelectedTypeFilter));
            ApplyFilters();
        }

        // ══════════════════════════════════════════════════════════════════
        //  MUTATIONS
        // ══════════════════════════════════════════════════════════════════

        private async Task DeleteAsync(TaxInvoiceRowViewModel row)
        {
            var result = MessageBox.Show(
                $"Delete invoice '{row.InvoiceNumber}'?\n\n" +
                $"Customer : {row.CustomerName}\n" +
                $"Amount   : {row.GrandTotalDisplay}\n\n" +
                "This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (SelectedRow == row) SelectedRow = null;
                await App.TaxInvoices!.DeleteAsync(row.Id);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to delete:\n\n{ex.Message}",
                    "Delete Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task ChangeStatusAsync(
            TaxInvoiceRowViewModel row, string newStatus)
        {
            try
            {
                await App.TaxInvoices!.UpdateStatusAsync(
                    row.InvoiceNumber, newStatus);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to change status:\n\n{ex.Message}",
                    "Update Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}