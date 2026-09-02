// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/GodView/TaxInvoiceGodViewViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Tax Invoice — God View tab
//
// Read-only overview of EVERY tax invoice: KPI tiles + searchable
// table. Lives behind the module tab strip; opening a row hands off
// to the normal Tax Invoice detail view.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels.Shared;
using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

using ProGlassAutomation.Services.TaxInvoice.Repository;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.TaxInvoice.GodView
{
    /// <summary>All-records overview for the Tax Invoice module.</summary>
    public class TaxInvoiceGodViewViewModel : ViewModelBase
    {
        private readonly ITaxInvoiceRepository _taxInvoices = null!;

        // ── ERR-633EEB CLASS FIX (2026-09-02) ──────────────────────
        // Initialization used to live in the PARAMETERLESS ctor while
        // DI selected this parameterized overload (MS.DI always picks
        // the greediest satisfiable ctor). Every DI-resolved instance
        // was therefore half-built — null child view models / null
        // commands — and threw NullReferenceException on first use.
        // The body was moved HERE so all construction routes (DI,
        // Activator fallback, XAML design-time) initialize fully.
        public TaxInvoiceGodViewViewModel(ITaxInvoiceRepository? taxInvoices)
        {
            _taxInvoices = taxInvoices!;

            RefreshCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(() => LoadAsync(), "GodView LoadAsync"));

            OpenCommand = new RelayCommand(param =>
            {
                if (param is TaxInvoiceListItem item)
                    OnOpenRequested?.Invoke(item.Id);
            });
        }

        private ObservableCollection<TaxInvoiceListItem> _allRows = new();

        public ObservableCollection<TaxInvoiceListItem> Rows { get; } = new();

        public ObservableCollection<GodViewKpiViewModel> Kpis { get; } = new();

        /// <summary>Raised with the TI database Id when a row is opened.</summary>
        public Action<int>? OnOpenRequested { get; set; }

        public ICommand RefreshCommand { get; }
        public ICommand OpenCommand { get; }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); }
        }

        public TaxInvoiceGodViewViewModel()
         : this(
            App.ServiceProvider?.GetService<ITaxInvoiceRepository>()!){
        }

        /// <summary>Loads every tax invoice and recomputes KPIs.</summary>
        public async Task LoadAsync()
        {
            if (_taxInvoices == null) return;

            try
            {
                UiDispatch.Run(() => IsLoading = true);

                var items = await _taxInvoices.GetAllAsync();

                UiDispatch.Run(() =>
                {
                    _allRows = new ObservableCollection<TaxInvoiceListItem>(
                        items.OrderByDescending(i => i.CreatedAt));
                    RebuildKpis();
                    ApplyFilter();
                    IsLoading = false;
                });

                AuditService.Log(
                    AuditSections.TaxInvoice,
                    "God View opened",
                    $"{_allRows.Count} tax invoices listed");
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[TI GodView] Load failed: {ex.Message}");
                UiDispatch.Run(() => IsLoading = false);
            }
        }

        private void ApplyFilter()
        {
            Rows.Clear();

            var filter = (SearchText ?? "").Trim();

            foreach (var row in _allRows)
            {
                if (filter.Length == 0 || Matches(row, filter))
                    Rows.Add(row);
            }
        }

        private static bool Matches(TaxInvoiceListItem r, string filter)
        {
            return Contains(r.InvoiceNumber, filter)
                || Contains(r.ProformaId, filter)
                || Contains(r.JobOrderId, filter)
                || Contains(r.CustomerName, filter)
                || Contains(r.ProjectName, filter)
                || Contains(r.Status, filter)
                || Contains(r.InvoiceType, filter);
        }

        private static bool Contains(string? text, string filter) =>
            text?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

        private void RebuildKpis()
        {
            Kpis.Clear();

            Kpis.Add(new GodViewKpiViewModel
            {
                Icon = "🧾",
                Value = _allRows.Count.ToString("N0"),
                Label = "Tax invoices",
                TintBrushKey = "TintIndigoBrush"
            });

            Kpis.Add(new GodViewKpiViewModel
            {
                Icon = "📨",
                Value = _allRows.Count(i => i.Status == "Issued").ToString("N0"),
                Label = "Issued",
                TintBrushKey = "TintBlueBrush",
                ValueBrushKey = "InfoBrush"
            });

            Kpis.Add(new GodViewKpiViewModel
            {
                Icon = "💵",
                Value = _allRows.Count(i => i.Status == "Paid").ToString("N0"),
                Label = "Paid",
                TintBrushKey = "TintGreenBrush",
                ValueBrushKey = "SuccessBrush"
            });

            Kpis.Add(new GodViewKpiViewModel
            {
                Icon = "⏰",
                Value = _allRows.Count(i => i.Status == "Overdue").ToString("N0"),
                Label = "Overdue",
                TintBrushKey = "TintRedBrush",
                ValueBrushKey = "DangerBrush"
            });

            decimal total = _allRows.Sum(i => i.GrandTotal);
            Kpis.Add(new GodViewKpiViewModel
            {
                Icon = "💰",
                Value = total.ToString("N0"),
                Label = "Total billed (AED)",
                TintBrushKey = "TintAmberBrush",
                ValueBrushKey = "WarningBrush"
            });
        }
    }
}
