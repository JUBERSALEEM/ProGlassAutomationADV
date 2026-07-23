using ProGlassAutomation.Commands;
using ProGlassAutomation.ViewModels.JobOrder.JobOrderDetail;
using ProGlassAutomation.ViewModels.JobOrder.SavedJobOrdersList;
using System;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.JobOrder
{
    /// <summary>
    /// Shell ViewModel for the Job Order module.
    /// Owns the list ↔ detail view toggle and both sub-VMs.
    ///
    /// Phase 10.5: Added OnRequestOpenDeliveryNote callback to propagate
    /// DN navigation requests up to MainViewModel.
    /// </summary>
    public class JobOrderViewModel : ViewModelBase
    {
        public string Title { get; } = "Job Orders";
        public string Subtitle { get; } = "Track production status of approved invoices";

        // ── sub-VMs ───────────────────────────────────────────────────────

        public SavedJobOrdersListViewModel ListVM { get; }
        public JobOrderDetailViewModel DetailVM { get; }

        // ── view state toggle ─────────────────────────────────────────────

        private bool _isListView = true;
        public bool IsListView
        {
            get => _isListView;
            set
            {
                if (_isListView == value) return;
                _isListView = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDetailView));
            }
        }

        public bool IsDetailView => !_isListView;

        // ── commands ──────────────────────────────────────────────────────

        public ICommand BackToListCommand { get; }

        // ── callback for main shell (Phase 10.5) ──────────────────────────

        /// <summary>
        /// Fired when JO detail view requests to open a newly created DN.
        /// MainViewModel wires this to navigate to DN module + open detail.
        /// Parameter = DN database Id.
        /// </summary>
        public Action<int>? OnRequestOpenDeliveryNote { get; set; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public JobOrderViewModel()
        {
            ListVM = new SavedJobOrdersListViewModel();
            DetailVM = new JobOrderDetailViewModel();

            // When user clicks OPEN on a list row → load detail
            ListVM.OnJobOrderOpenRequested = LoadJobOrderIntoDetail;

            // When user clicks Save/Back in detail → return to list
            DetailVM.OnRequestBackToList = ReturnToList;

            // Phase 10.5: When user clicks Create DN → propagate to shell
            DetailVM.OnRequestOpenDeliveryNote = dnId =>
                OnRequestOpenDeliveryNote?.Invoke(dnId);

            BackToListCommand = new RelayCommand(_ => ReturnToList());
        }

        // ═════════════════════════════════════════════════════════════════
        //  NAVIGATION
        // ═════════════════════════════════════════════════════════════════

        private async void LoadJobOrderIntoDetail(int jobOrderId)
        {
            await DetailVM.LoadAsync(jobOrderId);
            IsListView = false;
        }

        private async void ReturnToList()
        {
            IsListView = true;
            await ListVM.LoadJobOrdersAsync();
        }
    }
}