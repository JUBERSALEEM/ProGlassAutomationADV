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

namespace ProGlassAutomation.ViewModels.JobOrder.GodView
{
    /// <summary>All-records overview for the Job Order module.</summary>
    public class JobOrderGodViewViewModel : ViewModelBase
    {
        private ObservableCollection<JobOrderListRow> _allRows = new();

        public ObservableCollection<JobOrderListRow> Rows { get; } = new();

        public ObservableCollection<GodViewKpiViewModel> Kpis { get; } = new();

        /// <summary>Raised with the JO database Id when a row is opened.</summary>
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

        public JobOrderGodViewViewModel()
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
            if (App.Jobs == null) return;

            try
            {
                UiDispatch.Run(() => IsLoading = true);

                var items = await App.Jobs.GetListRowsAsync();

                UiDispatch.Run(() =>
                {
                    _allRows = new ObservableCollection<JobOrderListRow>(
                        items.OrderByDescending(i => i.CreatedAt));
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
                    $"[JO GodView] Load failed: {ex.Message}");
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

        private static bool Matches(JobOrderListRow r, string filter)
        {
            return Contains(r.JobOrderId, filter)
                || Contains(r.SourceProformaId, filter)
                || Contains(r.CustomerName, filter)
                || Contains(r.ProjectName, filter)
                || Contains(r.Status, filter)
                || Contains(r.Priority, filter);
        }

        private static bool Contains(string? text, string filter) =>
            text?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

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
