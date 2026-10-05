// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/JobOrder/GodView/JobOrderGodViewViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Job Order — God View tab
//
// Read-only overview of EVERY job order: KPI tiles + searchable table.
// Lives behind the module tab strip; opening a row hands off to the
// normal Job Order detail view.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels.Shared;
using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

using ProGlassAutomation.Services.JobOrder.Repository;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.JobOrder.GodView
{
    /// <summary>All-records overview for the Job Order module.</summary>
    public class JobOrderGodViewViewModel : ViewModelBase
    {
        private readonly IJobOrderRepository _jobs = null!;

        public JobOrderGodViewViewModel(IJobOrderRepository? jobs)
        {
            _jobs = jobs!;
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

        private ObservableCollection<JobOrderListRow> _allRows = new();

        public BulkObservableCollection<JobOrderListRow> Rows { get; } = new();

        public ObservableCollection<GodViewKpiViewModel> Kpis { get; } = new();

        /// <summary>Raised with the JO database Id when a row is opened.</summary>
        public Action<int>? OnOpenRequested { get; set; }

        public ICommand RefreshCommand { get; private set; }
        public ICommand OpenCommand { get; private set; }

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

        public JobOrderGodViewViewModel()
         : this(
            App.ServiceProvider?.GetService<IJobOrderRepository>()!){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            RefreshCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(() => LoadAsync(), "GodView LoadAsync"));

            OpenCommand = new RelayCommand(param =>
            {
                if (param is JobOrderListRow jo)
                    OnOpenRequested?.Invoke(jo.Id);
            });
        }

        /// <summary>Loads every job order and recomputes KPIs.</summary>
        public async Task LoadAsync()
        {
            if (_jobs == null) return;

            try
            {
                UiDispatch.Run(() => IsLoading = true);

                var items = await _jobs.GetListRowsAsync();

                // Sort on background thread (not UI thread)
                var sorted = items.OrderByDescending(i => i.CreatedAt).ToList();

                UiDispatch.Run(() =>
                {
                    _allRows = new ObservableCollection<JobOrderListRow>(sorted);
                    RebuildKpis();
                    ApplyFilter();
                    IsLoading = false;
                });

                AuditService.Log(
                    AuditSections.JobOrder,
                    "God View opened",
                    $"{_allRows.Count} job orders listed");
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[JO GodView] Load failed: {ExceptionMessages.Deep(ex)}");
                UiDispatch.Run(() => IsLoading = false);
            }
        }

        private void ApplyFilter()
        {
            var filter = (SearchText ?? "").Trim();

            var filtered = filter.Length == 0
                ? _allRows.ToList()
                : _allRows.Where(r => Matches(r, filter)).ToList();

            Rows.ClearSilent();
            Rows.AddRangeSilent(filtered);
            Rows.RaiseReset();
        }

        private static bool Matches(JobOrderListRow r, string filter)
        {
            return Utilities.SearchHelper.MatchesAny(filter,
                r.JobOrderId, r.SourceProformaId, r.CustomerName,
                r.ProjectName, r.Status, r.Priority);
        }

        private void RebuildKpis()
        {
            Kpis.Clear();

            Kpis.Add(new GodViewKpiViewModel
            {
                Icon = "📋",
                Value = _allRows.Count.ToString("N0"),
                Label = "Job orders",
                TintBrushKey = "TintIndigoBrush"
            });

            Kpis.Add(new GodViewKpiViewModel
            {
                Icon = "🏭",
                Value = _allRows.Count(i => i.Status == "InProduction")
                           .ToString("N0"),
                Label = "In production",
                TintBrushKey = "TintBlueBrush",
                ValueBrushKey = "InfoBrush"
            });

            Kpis.Add(new GodViewKpiViewModel
            {
                Icon = "📅",
                Value = _allRows.Count(i => i.DeliveryDate.HasValue
                                     && i.DeliveryDate.Value.Date < DateTime.Today
                                     && i.Status != "Delivered")
                           .ToString("N0"),
                Label = "Overdue",
                TintBrushKey = "TintRedBrush",
                ValueBrushKey = "DangerBrush"
            });

            Kpis.Add(new GodViewKpiViewModel
            {
                Icon = "✅",
                Value = _allRows.Count(i => i.Status == "Completed"
                                     || i.Status == "Delivered")
                           .ToString("N0"),
                Label = "Completed / delivered",
                TintBrushKey = "TintGreenBrush",
                ValueBrushKey = "SuccessBrush"
            });

            decimal totalValue = _allRows.Sum(i => i.GrandTotalVatInclusive);
            Kpis.Add(new GodViewKpiViewModel
            {
                Icon = "💰",
                Value = totalValue.ToString("N0"),
                Label = "Total value (AED)",
                TintBrushKey = "TintAmberBrush",
                ValueBrushKey = "WarningBrush"
            });
        }
    }
}
