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
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels.Shared;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.DeliveryNote.GodView
{
    /// <summary>
    /// All-records overview for the Delivery Note module.
    /// </summary>
    public class DeliveryNoteGodViewViewModel : ViewModelBase
    {
        private ObservableCollection<DeliveryNoteListItem> _allRows = new();

        public ObservableCollection<DeliveryNoteListItem> Rows { get; } = new();

        public ObservableCollection<GodViewKpiViewModel> Kpis { get; } = new();

        /// <summary>Raised with the DN database Id when a row is opened.</summary>
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

        public DeliveryNoteGodViewViewModel()
        {
            RefreshCommand = new RelayCommand(_ => LoadAsync());

            OpenCommand = new RelayCommand(param =>
            {
                if (param is DeliveryNoteListItem item)
                    OnOpenRequested?.Invoke(item.Id);
            });
        }

        /// <summary>Loads every delivery note and recomputes KPIs.</summary>
        public async void LoadAsync()
        {
            if (App.DeliveryNotes == null) return;

            try
            {
                IsLoading = true;

                var items = await App.DeliveryNotes.GetAllAsync();

                _allRows = new ObservableCollection<DeliveryNoteListItem>(
                    items.OrderByDescending(i => i.CreatedAt));

                RebuildKpis();
                ApplyFilter();

                AuditService.Log(
                    AuditSections.DeliveryNote,
                    "God View opened",
                    $"{_allRows.Count} delivery notes listed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[DN GodView] Load failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
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

        private static bool Matches(DeliveryNoteListItem r, string filter)
        {
            return Contains(r.DeliveryNoteId, filter)
                || Contains(r.JobOrderId, filter)
                || Contains(r.ProformaId, filter)
                || Contains(r.CustomerName, filter)
                || Contains(r.ProjectName, filter)
                || Contains(r.Status, filter)
                || Contains(r.DriverName, filter)
                || Contains(r.VehicleNumber, filter);
        }

        private static bool Contains(string? text, string filter) =>
            text?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

        private void RebuildKpis()
        {
            Kpis.Clear();

            var real = _allRows.Where(i => !i.IsMasterDn).ToList();

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

            Kpis.Add(new GodViewKpiViewModel
            {
                Icon = "🧾",
                Value = real.Count(i => i.HasTaxInvoice).ToString("N0"),
                Label = "Invoiced",
                TintBrushKey = "TintPurpleBrush"
            });
        }
    }
}
