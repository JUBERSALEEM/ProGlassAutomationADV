using ProGlassAutomation.Commands;
using ProGlassAutomation.ViewModels.JobOrder.JobOrderDetail;
using ProGlassAutomation.ViewModels.JobOrder.SavedJobOrdersList;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.JobOrder
{
    /// <summary>
    /// Shell ViewModel for the Job Order module.
    /// Owns the list ↔ detail view toggle and both sub-VMs.
    ///
    /// Landing page = SavedJobOrdersList (IsListView = true).
    /// User clicks OPEN on a row → switches to JobOrderDetail
    /// (IsDetailView = true) with the selected job order loaded.
    ///
    /// Mirrors ProformaInvoiceViewModel architecture but simpler —
    /// no wizard, only list + detail.
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

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public JobOrderViewModel()
        {
            ListVM = new SavedJobOrdersListViewModel();
            DetailVM = new JobOrderDetailViewModel();

            // When user clicks OPEN on a list row → load detail view
            ListVM.OnJobOrderOpenRequested = LoadJobOrderIntoDetail;

            // When user clicks Save/Back in detail view → return to list
            DetailVM.OnRequestBackToList = ReturnToList;

            BackToListCommand = new RelayCommand(_ => ReturnToList());
        }

        // ═════════════════════════════════════════════════════════════════
        //  NAVIGATION
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called when user clicks OPEN on a job order row.
        /// Loads the selected job order into the detail VM and switches
        /// view state.
        /// </summary>
        private async void LoadJobOrderIntoDetail(int jobOrderId)
        {
            await DetailVM.LoadAsync(jobOrderId);
            IsListView = false;
        }

        /// <summary>
        /// Called when user clicks Back to List in detail view, or after
        /// a successful save. Refreshes the list in case the detail view
        /// changed anything (status, priority, delivery date).
        /// </summary>
        private async void ReturnToList()
        {
            IsListView = true;
            await ListVM.LoadJobOrdersAsync();
        }
    }
}