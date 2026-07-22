using ProGlassAutomation.Commands;
using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.DeliveryNote
{
    /// <summary>
    /// Shell ViewModel for the Delivery Note module.
    /// Owns the list ↔ detail view toggle and both sub-VMs.
    ///
    /// Landing page = DeliveryNoteListViewModel (IsListView = true).
    /// User clicks OPEN on a row → switches to detail view.
    ///
    /// Mirrors JobOrderViewModel architecture exactly.
    /// </summary>
    public class DeliveryNoteViewModel : ViewModelBase
    {
        public string Title { get; } = "Delivery Notes";
        public string Subtitle { get; } = "Track glass deliveries, partial shipments and balance quantities";

        // ── sub-VMs ───────────────────────────────────────────────────────

        public DeliveryNoteListViewModel ListVM { get; }

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

        public DeliveryNoteViewModel()
        {
            ListVM = new DeliveryNoteListViewModel();

            // Wired in Phase 10.4 when detail VM is built
            // ListVM.OnDeliveryNoteOpenRequested = LoadDeliveryNoteIntoDetail;

            BackToListCommand = new RelayCommand(_ => ReturnToList());
        }

        // ═════════════════════════════════════════════════════════════════
        //  NAVIGATION
        // ═════════════════════════════════════════════════════════════════

        private async void ReturnToList()
        {
            IsListView = true;
            await ListVM.LoadAsync();
        }
    }
}