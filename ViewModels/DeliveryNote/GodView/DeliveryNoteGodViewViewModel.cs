// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/DeliveryNote/GodView/DeliveryNoteGodViewViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Delivery Note — God View tab
//
// The "God View" is a single read-only overview of EVERY delivery note
// in the system: KPI tiles + full searchable table. It lives behind
// the module tab strip (Workspace | God View | Activity) and never
// mutates data — opening a record hands off to the normal detail view.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels.Shared;
using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

using ProGlassAutomation.Services.DeliveryNote.Repository;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.DeliveryNote.GodView
{
    /// <summary>
    /// All-records overview for the Delivery Note module.
    /// </summary>
    public class DeliveryNoteGodViewViewModel : ViewModelBase
    {
        private readonly IDeliveryNoteRepository _deliveryNotes = null!;

        public DeliveryNoteGodViewViewModel(IDeliveryNoteRepository? deliveryNotes)
        {
            _deliveryNotes = deliveryNotes!;
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

        private ObservableCollection<DeliveryNoteListItem> _allRows = new();

        public BulkObservableCollection<DeliveryNoteListItem> Rows { get; } = new();

        public ObservableCollection<GodViewKpiViewModel> Kpis { get; } = new();

        /// <summary>Raised with the DN database Id when a row is opened.</summary>
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

        public DeliveryNoteGodViewViewModel()
         : this(
            App.ServiceProvider?.GetService<IDeliveryNoteRepository>()!){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            RefreshCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(() => LoadAsync(), "GodView LoadAsync"));

            OpenCommand = new RelayCommand(param =>
            {
                if (param is DeliveryNoteListItem item)
                    OnOpenRequested?.Invoke(item.Id);
            });
        }

        /// <summary>Loads every delivery note and recomputes KPIs.</summary>
        public async Task LoadAsync()
        {
            if (_deliveryNotes == null) return;

            try
            {
                UiDispatch.Run(() => IsLoading = true);

                var items = await _deliveryNotes.GetAllAsync();

                // PERFORMANCE: sort on background thread (not UI thread).
                // For 2000+ records, OrderByDescending is measurable.
                var sorted = items.OrderByDescending(i => i.CreatedAt).ToList();

                UiDispatch.Run(() =>
                {
                    _allRows = new ObservableCollection<DeliveryNoteListItem>(sorted);
                    RebuildKpis();
                    ApplyFilter();
                    IsLoading = false;
                });

                AuditService.Log(
                    AuditSections.DeliveryNote,
                    "God View opened",
                    $"{_allRows.Count} delivery notes listed");
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[DN GodView] Load failed: {ExceptionMessages.Deep(ex)}");
                UiDispatch.Run(() => IsLoading = false);
            }
        }

        private void ApplyFilter()
        {
            var filter = (SearchText ?? "").Trim();

            // PERFORMANCE: build filtered list then bulk-add, fires ONE
            // CollectionChanged(Reset) instead of N+1 events.
            var filtered = filter.Length == 0
                ? _allRows.ToList()
                : _allRows.Where(r => Matches(r, filter)).ToList();

            Rows.ClearSilent();
            Rows.AddRangeSilent(filtered);
            Rows.RaiseReset();
        }

        private static bool Matches(DeliveryNoteListItem r, string filter)
        {
            return Utilities.SearchHelper.MatchesAny(filter,
                r.DeliveryNoteId, r.JobOrderId, r.ProformaId,
                r.CustomerName, r.ProjectName, r.Status,
                r.DriverName, r.VehicleNumber);
        }

        private void RebuildKpis()
        {
            Kpis.Clear();

            // Real delivery notes only — exclude Master DNs AND auto-generated
            // Dummy balance DNs so the counts match what an operator sees.
            var real = _allRows.Where(i => !i.IsMasterDn &&
                        !string.Equals(i.DeliveryType, "Dummy",
                            StringComparison.OrdinalIgnoreCase)).ToList();

            Kpis.Add(new GodViewKpiViewModel
            {
                Icon = "🚚",
                Value = real.Count.ToString("N0"),
                Label = "Delivery notes",
                TintBrushKey = "TintIndigoBrush"
            });

            Kpis.Add(new GodViewKpiViewModel
            {
                Icon = "✅",
                Value = real.Count(i => i.Status == "Delivered"
                                     || i.Status == "Signed"
                                     || i.Status == "Received")
                           .ToString("N0"),
                Label = "Delivered / signed",
                TintBrushKey = "TintGreenBrush",
                ValueBrushKey = "SuccessBrush"
            });

            Kpis.Add(new GodViewKpiViewModel
            {
                Icon = "📦",
                Value = real.Count(i => i.Status == "Dispatched")
                           .ToString("N0"),
                Label = "Dispatched",
                TintBrushKey = "TintBlueBrush",
                ValueBrushKey = "InfoBrush"
            });

            Kpis.Add(new GodViewKpiViewModel
            {
                Icon = "⚖️",
                Value = real.Count(i => i.TotalBalanceQty > 0)
                           .ToString("N0"),
                Label = "With balance remaining",
                TintBrushKey = "TintAmberBrush",
                ValueBrushKey = "WarningBrush"
            });
        }
    }
}
