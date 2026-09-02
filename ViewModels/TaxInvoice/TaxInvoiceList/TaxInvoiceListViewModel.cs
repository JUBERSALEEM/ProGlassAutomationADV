using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Models.TaxInvoice.TaxInvoiceFamily;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Licensing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceList
{
    /// <summary>
    /// ViewModel for the Tax Invoice list landing page.
    ///
    /// ARCHITECTURE (mirrors DN List module):
    ///   Every tab shows ONE row per Proforma Invoice (family view).
    ///   Never shows individual invoices in the main list.
    ///   Partial invoices (A/B/C) are seen inside the Detail view sidebar.
    ///
    /// 4 tabs, all family-grouped:
    ///   All      — every PI family
    ///   Draft    — families containing at least one Draft invoice
    ///   Issued   — families containing at least one Issued invoice
    ///   Paid     — families where ALL members are Paid
    ///
    /// 5 hero cards: dynamic — dataset stats or selected family stats.
    /// </summary>
    public class TaxInvoiceListViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogs = null!;
        private readonly IDatabaseServices _database = null!;
        private readonly ITaxInvoiceRepository _taxInvoices = null!;

        // ── ERR-633EEB CLASS FIX (2026-09-02) ──────────────────────
        // Initialization used to live in the PARAMETERLESS ctor while
        // DI selected this parameterized overload (MS.DI always picks
        // the greediest satisfiable ctor). Every DI-resolved instance
        // was therefore half-built — null child view models / null
        // commands — and threw NullReferenceException on first use.
        // The body was moved HERE so all construction routes (DI,
        // Activator fallback, XAML design-time) initialize fully.
        public TaxInvoiceListViewModel(IDialogService? dialogs, ITaxInvoiceRepository? taxInvoices, IDatabaseServices? database)
        {
            _dialogs = dialogs!;
            _taxInvoices = taxInvoices!;
            _database = database!;

            RefreshCommand = new RelayCommand(async _ =>
            {
                SelectedFamily = null;
                await LoadAsync();
            });

            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

            OpenFamilyCommand = new RelayCommand(param =>
            {
                if (param is InvoiceFamilyRowViewModel family
                    && family.LatestInvoiceId > 0)
                    OnInvoiceOpenRequested?.Invoke(family.LatestInvoiceId);
            });

            SelectFamilyCommand = new RelayCommand(param =>
            {
                if (param is InvoiceFamilyRowViewModel family)
                    SelectedFamily = SelectedFamily == family ? null : family;
            });

            ClearSelectionCommand = new RelayCommand(_ => SelectedFamily = null);

            CreateInvoiceCommand = new RelayCommand(_ =>
                OnCreateInvoiceRequested?.Invoke());

            DeleteFamilyCommand = new RelayCommand(async param =>
            {
                if (param is not InvoiceFamilyRowViewModel fam) return;
                if (_taxInvoices == null) return;

                // NEVER delete directly — explicit confirmation first.
                var result = _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.DeleteTaxInvoiceFamilyX"), fam.ProformaId, fam.InvoiceCount),
                    LocalizationService.Instance.T("Msg.CapConfirmDelete"),
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes) return;

                try
                {
                    foreach (var m in fam.Members)
                        await _taxInvoices.DeleteAsync(m.Id);

                    ProGlassAutomation.Services.Audit.AuditService.Log(
                        ProGlassAutomation.Services.Audit.AuditSections.TaxInvoice,
                        "Invoice family deleted",
                        $"{fam.ProformaId} ({fam.InvoiceCount} invoice(s))");

                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.DeleteFailedX"), ex.Message),
                        LocalizationService.Instance.T("Msg.CapDeleteFailed"),
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            });

            _database.SubscribeDocumentsChanged(LoadAsync);
        }

        private int _loadingFlag = 0;
        private int _reloadQueued = 0;

        // ── Callbacks to shell VM ─────────────────────────────────────────

        /// <summary>
        /// Fires with the LATEST invoice ID in a family (opens detail view
        /// where user can then navigate to any family member).
        /// </summary>
        public Action<int>? OnInvoiceOpenRequested { get; set; }

        /// <summary>
        /// Shell VM opens ProformaPickerDialog when this fires.
        /// </summary>
        public Action? OnCreateInvoiceRequested { get; set; }

        // ══════════════════════════════════════════════════════════════════
        //  SELECTION
        // ══════════════════════════════════════════════════════════════════

        private InvoiceFamilyRowViewModel? _selectedFamily;
        public InvoiceFamilyRowViewModel? SelectedFamily
        {
            get => _selectedFamily;
            set
            {
                if (_selectedFamily == value) return;
                if (_selectedFamily != null) _selectedFamily.IsSelected = false;
                _selectedFamily = value;
                if (_selectedFamily != null) _selectedFamily.IsSelected = true;
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
                OnPropertyChanged(nameof(SelectedFamilyMetaLine));
            }
        }

        public bool HasSelection => _selectedFamily != null;

        // ── Hero cards — family-aware ────────────────────────────────────

        public string HeroCard1Value => _selectedFamily != null
            ? _selectedFamily.InvoicedTotalDisplay
            : $"AED {_totalInvoicedAmount:N2}";
        public string HeroCard1Label => _selectedFamily != null
            ? "Family Invoiced Total" : "Total Invoiced (AED)";

        public string HeroCard2Value => _selectedFamily != null
            ? _selectedFamily.InvoiceCount.ToString()
            : DraftCount.ToString();
        public string HeroCard2Label => _selectedFamily != null
            ? "Invoices in Family" : "Families with Draft";

        public string HeroCard3Value => _selectedFamily != null
            ? _selectedFamily.BalanceTotalDisplay
            : IssuedCount.ToString();
        public string HeroCard3Label => _selectedFamily != null
            ? "Remaining Balance" : "Families with Issued";

        public string HeroCard4Value => _selectedFamily != null
            ? _selectedFamily.FamilyStatus
            : PaidCount.ToString();
        public string HeroCard4Label => _selectedFamily != null
            ? "Family Status" : "Fully Paid Families";

        public string HeroCard5Value => _selectedFamily != null
            ? $"{_selectedFamily.ProgressPercent:0.#}%"
            : OverdueCount.ToString();
        public string HeroCard5Label => _selectedFamily != null
            ? "Invoiced Progress" : "Families with Overdue";

        public string SelectedFamilyMetaLine => _selectedFamily == null ? "" :
            $"📋 {_selectedFamily.ProformaId}  ·  " +
            $"{_selectedFamily.CustomerName}  ·  " +
            $"{_selectedFamily.InvoiceCount} invoice(s)  ·  " +
            $"Latest: {_selectedFamily.LatestInvoiceNumber}";

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
                SelectedFamily = null;
                RebuildActiveTab();
            }
        }

        /// <summary>
        /// Only rebuilds the collection for the NOW-ACTIVE tab.
        /// Uses cached _filteredFamilies from last ApplyFilters() call.
        /// No DB query — pure in-memory filter. Near-instant.
        /// </summary>
        private void RebuildActiveTab()
        {
            var source = _filteredFamilies;
            if (source == null || source.Count == 0)
                source = _rawFamilies;

            switch (_selectedTabIndex)
            {
                case 0:
                    AllFamilies = new ObservableCollection<InvoiceFamilyRowViewModel>(
                        FilterBySearchAndStatus(source));
                    break;
                case 1:
                    DraftFamilies = new ObservableCollection<InvoiceFamilyRowViewModel>(
                        FilterBySearchAndStatus(source).Where(f => f.AnyDraft));
                    break;
                case 2:
                    IssuedFamilies = new ObservableCollection<InvoiceFamilyRowViewModel>(
                        FilterBySearchAndStatus(source).Where(f => f.AnyIssued));
                    break;
                case 3:
                    PaidFamilies = new ObservableCollection<InvoiceFamilyRowViewModel>(
                        FilterBySearchAndStatus(source).Where(f => f.AllPaid));
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  FAMILY-BASED COLLECTIONS (one row per PI)
        // ══════════════════════════════════════════════════════════════════

        private ObservableCollection<InvoiceFamilyRowViewModel> _allFamilies = new();
        public ObservableCollection<InvoiceFamilyRowViewModel> AllFamilies
        {
            get => _allFamilies;
            private set
            {
                _allFamilies = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AllCount));
            }
        }

        private ObservableCollection<InvoiceFamilyRowViewModel> _draftFamilies = new();
        public ObservableCollection<InvoiceFamilyRowViewModel> DraftFamilies
        {
            get => _draftFamilies;
            private set { _draftFamilies = value; OnPropertyChanged(); }
        }

        private ObservableCollection<InvoiceFamilyRowViewModel> _issuedFamilies = new();
        public ObservableCollection<InvoiceFamilyRowViewModel> IssuedFamilies
        {
            get => _issuedFamilies;
            private set { _issuedFamilies = value; OnPropertyChanged(); }
        }

        private ObservableCollection<InvoiceFamilyRowViewModel> _paidFamilies = new();
        public ObservableCollection<InvoiceFamilyRowViewModel> PaidFamilies
        {
            get => _paidFamilies;
            private set { _paidFamilies = value; OnPropertyChanged(); }
        }

        // ── Tab badge counts ──────────────────────────────────────────────

        public int AllCount => _allCount > 0 ? _allCount : _allFamilies.Count;

        private int _draftCount;
        public int DraftCount
        {
            get => _draftCount;
            private set
            {
                _draftCount = value;
                OnPropertyChanged();
                if (_selectedFamily == null)
                    OnPropertyChanged(nameof(HeroCard2Value));
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
                if (_selectedFamily == null)
                    OnPropertyChanged(nameof(HeroCard3Value));
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
                if (_selectedFamily == null)
                    OnPropertyChanged(nameof(HeroCard4Value));
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
                if (_selectedFamily == null)
                    OnPropertyChanged(nameof(HeroCard5Value));
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
                if (_selectedFamily == null)
                    OnPropertyChanged(nameof(HeroCard1Value));
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
                SelectedFamily = null;
                ApplyFilters();
            }
        }

        public List<string> StatusFilterOptions { get; } = new()
        {
            "All Statuses", "Has Draft", "Has Issued", "All Paid",
            "Has Overdue", "Has Cancelled"
        };

        private string _selectedStatusFilter = "All Statuses";
        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                if (_selectedStatusFilter == value) return;
                _selectedStatusFilter = value;
                OnPropertyChanged();
                SelectedFamily = null;
                ApplyFilters();
            }
        }

        // Raw unfiltered families (loaded once, filtered in memory)
        private List<InvoiceFamilyRowViewModel> _rawFamilies = new();

        // ══════════════════════════════════════════════════════════════════
        //  COMMANDS
        // ══════════════════════════════════════════════════════════════════

        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand OpenFamilyCommand { get; }
        public ICommand SelectFamilyCommand { get; }
        public ICommand ClearSelectionCommand { get; }
        public ICommand CreateInvoiceCommand { get; }

        /// <summary>Deletes a family after explicit Yes/No confirmation.</summary>
        public ICommand DeleteFamilyCommand { get; }

        // ══════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════════

        public TaxInvoiceListViewModel()
         : this(
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<ITaxInvoiceRepository>()!,
            App.ServiceProvider?.GetService<IDatabaseServices>()!){
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOAD — single call fetches all families, then filter in memory
        // ══════════════════════════════════════════════════════════════════

        public async Task LoadAsync()
        {
            if (Interlocked.CompareExchange(ref _loadingFlag, 1, 0) != 0)
            {
                Interlocked.Exchange(ref _reloadQueued, 1);
                return;
            }

            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = "";

                if (_taxInvoices == null)
                {
                    HasError = true;
                    ErrorMessage = "Tax Invoices database not initialized.";
                    return;
                }

                // Heavy work on background thread
                var familyVms = await Task.Run(async () =>
                {
                    var families = await _taxInvoices.GetAllFamiliesAsync();
                    return InvoiceFamilyRowViewModel
                        .FromFamilies(families)
                        .ToList();
                });

                // Everything below runs on UI thread (no ConfigureAwait)
                _rawFamilies = familyVms;
                _filteredFamilies = familyVms;
                ApplyFilters();

                // KPI aggregations — safe on UI thread after assignment
                DraftCount = familyVms.Count(f => f.AnyDraft);
                IssuedCount = familyVms.Count(f => f.AnyIssued);
                PaidCount = familyVms.Count(f => f.AllPaid);
                OverdueCount = familyVms.Count(f => f.AnyOverdue);
                TotalInvoicedAmount = familyVms.Sum(f => f.InvoicedTotal);

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

        // ══════════════════════════════════════════════════════════════════
        //  FILTERING
        //  Combines: tab index + search text + status filter
        // ══════════════════════════════════════════════════════════════════

        private void ApplyFilters()
        {
            var filtered = FilterBySearchAndStatus(_rawFamilies).ToList();

            // Only rebuild the ACTIVE tab collection — avoids 4x layout passes.
            // Inactive tabs rebuild lazily when user switches to them.
            switch (_selectedTabIndex)
            {
                case 0:
                    AllFamilies = new ObservableCollection<InvoiceFamilyRowViewModel>(filtered);
                    break;
                case 1:
                    DraftFamilies = new ObservableCollection<InvoiceFamilyRowViewModel>(
                        filtered.Where(f => f.AnyDraft));
                    break;
                case 2:
                    IssuedFamilies = new ObservableCollection<InvoiceFamilyRowViewModel>(
                        filtered.Where(f => f.AnyIssued));
                    break;
                case 3:
                    PaidFamilies = new ObservableCollection<InvoiceFamilyRowViewModel>(
                        filtered.Where(f => f.AllPaid));
                    break;
            }

            // Cache filtered list for lazy tab switching
            _filteredFamilies = filtered;

            // Update counts without triggering collection rebuilds
            _allCount = filtered.Count;
            OnPropertyChanged(nameof(AllCount));
        }

        // Cached filtered list for lazy tab switching
        private List<InvoiceFamilyRowViewModel> _filteredFamilies = new();
        private int _allCount;

        private IEnumerable<InvoiceFamilyRowViewModel> FilterBySearchAndStatus(
            IEnumerable<InvoiceFamilyRowViewModel> source)
        {
            IEnumerable<InvoiceFamilyRowViewModel> q = source;

            // Search text filter
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string s = _searchText.Trim().ToLowerInvariant();
                q = q.Where(f =>
                    f.ProformaId.ToLowerInvariant().Contains(s) ||
                    f.CustomerName.ToLowerInvariant().Contains(s) ||
                    f.ProjectName.ToLowerInvariant().Contains(s) ||
                    f.LatestInvoiceNumber.ToLowerInvariant().Contains(s) ||
                    f.Members.Any(m =>
                        m.InvoiceNumber.ToLowerInvariant().Contains(s) ||
                        m.PurchaseOrderNumber.ToLowerInvariant().Contains(s) ||
                        m.DeliveryNoteReference.ToLowerInvariant().Contains(s)));
            }

            // Status filter
            q = _selectedStatusFilter switch
            {
                "Has Draft" => q.Where(f => f.AnyDraft),
                "Has Issued" => q.Where(f => f.AnyIssued),
                "All Paid" => q.Where(f => f.AllPaid),
                "Has Overdue" => q.Where(f => f.AnyOverdue),
                "Has Cancelled" => q.Where(f =>
                    string.Equals(f.FamilyStatus, "HasCancelled",
                        StringComparison.OrdinalIgnoreCase)),
                _ => q
            };

            return q;
        }

        private void ClearFilters()
        {
            _searchText = "";
            _selectedStatusFilter = "All Statuses";
            SelectedFamily = null;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedStatusFilter));
            ApplyFilters();
        }
    }
}