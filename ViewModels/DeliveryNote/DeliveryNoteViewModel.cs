using ProGlassAutomation.Commands;
using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail;
using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.DeliveryNote
{
    /// <summary>
    /// Shell ViewModel for the Delivery Note module.
    ///
    /// Owns:
    ///   - ListVM   : the 5-tab list landing page
    ///   - DetailVM : the two-column detail editor
    ///
    /// View state toggled by IsListView / IsDetailView.
    /// Shell view (DeliveryNoteView.xaml) uses ContentControl + DataTemplate
    /// to render whichever child VM is active.
    ///
    /// Phase 10.5: Added OpenDeliveryNoteAsync(int) public method — called
    /// by MainViewModel when a new DN is created from a Job Order and
    /// needs to be opened in the detail view immediately.
    /// </summary>
    public class DeliveryNoteViewModel : ViewModelBase
    {
        public string Title { get; } = "Delivery Notes";
        public string Subtitle { get; } = "Track glass deliveries, partial shipments and balance quantities";

        // ── Child VMs — created once, reused for the lifetime of the shell
        public DeliveryNoteListViewModel ListVM { get; }
        public DeliveryNoteDetailViewModel DetailVM { get; }

        // ── View-state toggle ────────────────────────────────────────────
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

        // ── Commands ─────────────────────────────────────────────────────
        public ICommand BackToListCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public DeliveryNoteViewModel()
        {
            ListVM = new DeliveryNoteListViewModel();
            DetailVM = new DeliveryNoteDetailViewModel();

            // Wire callbacks between child VMs and the shell
            ListVM.OnDeliveryNoteOpenRequested = LoadDetailAsync;
            DetailVM.OnRequestBackToList = ReturnToList;

            BackToListCommand = new RelayCommand(_ => ReturnToList());
        }

        // ═════════════════════════════════════════════════════════════════
        //  INTERNAL — triggered by ListVM.OpenCommand
        //  Load detail data FIRST, then flip view state.
        //  Guarantees the view sees a fully-populated VM on first render.
        // ═════════════════════════════════════════════════════════════════

        private async void LoadDetailAsync(int deliveryNoteId)
        {
            await DetailVM.LoadAsync(deliveryNoteId);
            IsListView = false;
        }

        // ═════════════════════════════════════════════════════════════════
        //  INTERNAL — triggered by DetailVM.BackCommand or shell back button
        //  Flip back to list, refresh list in case DN was mutated.
        // ═════════════════════════════════════════════════════════════════

        private async void ReturnToList()
        {
            IsListView = true;
            await ListVM.LoadAsync();
        }

        // ═════════════════════════════════════════════════════════════════
        //  PUBLIC — cross-module entry point
        //  Called by MainViewModel after a new DN is created from a
        //  Job Order. Opens the DN directly in detail view.
        //
        //  Load detail first (populates all display fields), then switch
        //  view state. List refreshes in background after — non-blocking.
        // ═════════════════════════════════════════════════════════════════

        public async Task OpenDeliveryNoteAsync(int deliveryNoteId)
        {
            // 1. Load detail data — fully populates all display fields
            await DetailVM.LoadAsync(deliveryNoteId);

            // 2. Switch to detail view — data is already ready
            IsListView = false;

            // 3. Refresh list in background (does not block UI)
            _ = ListVM.LoadAsync();
        }
    }
}