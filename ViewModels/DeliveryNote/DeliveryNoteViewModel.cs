using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.DeliveryNote.Mapper;
using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail;
using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.DeliveryNote
{
    /// <summary>
    /// Shell ViewModel for the Delivery Note module.
    ///
    /// BUG-29: CreateDnFromJobOrderAsync was async void — changed to
    ///         async Task called via SafeFireAsync wrapper.
    /// BUG-30: LoadDetailAsync + ReturnToList were async void — same fix.
    /// BUG-31: ListVM.LoadAsync() fire-and-forget now wrapped in try/catch.
    /// BUG-32: Shared static JsonSerializerOptions — no per-call allocation.
    /// </summary>
    public class DeliveryNoteViewModel : ViewModelBase
    {
        public string Title { get; } = "Delivery Notes";
        public string Subtitle { get; } =
            "Track glass deliveries, partial shipments and balance quantities";

        public DeliveryNoteListViewModel ListVM { get; }
        public DeliveryNoteDetailViewModel DetailVM { get; }

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

        public ICommand BackToListCommand { get; }

        // BUG-32: shared static options — allocated once, reused everywhere
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public DeliveryNoteViewModel()
        {
            ListVM = new DeliveryNoteListViewModel();
            DetailVM = new DeliveryNoteDetailViewModel();

            ListVM.OnDeliveryNoteOpenRequested = id => SafeFireAsync(
                () => LoadDetailAsync(id));
            ListVM.OnCreateDnFromJobOrder = jo => SafeFireAsync(
                () => CreateDnFromJobOrderAsync(jo));

            DetailVM.OnRequestBackToList = () => SafeFireAsync(ReturnToList);

            BackToListCommand = new RelayCommand(
                _ => SafeFireAsync(ReturnToList));
        }

        // ═════════════════════════════════════════════════════════════════
        //  SAFE ASYNC FIRE — wraps async Task to prevent unhandled exceptions
        //  BUG-29 / BUG-30: replaces all async void methods
        // ═════════════════════════════════════════════════════════════════

        private static async void SafeFireAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unexpected error:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  NAVIGATION
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadDetailAsync(int deliveryNoteId)
        {
            await DetailVM.LoadAsync(deliveryNoteId);
            IsListView = false;
        }

        private async Task ReturnToList()
        {
            IsListView = true;
            await SafeLoadListAsync();
        }

        // ═════════════════════════════════════════════════════════════════
        //  PUBLIC — cross-module entry point (called by MainViewModel)
        // ═════════════════════════════════════════════════════════════════

        public async Task OpenDeliveryNoteAsync(int deliveryNoteId)
        {
            await DetailVM.LoadAsync(deliveryNoteId);
            IsListView = false;
            await SafeLoadListAsync();
        }

        // ═════════════════════════════════════════════════════════════════
        //  SAFE LIST RELOAD — BUG-31: fire-and-forget with error swallow
        // ═════════════════════════════════════════════════════════════════

        private async Task SafeLoadListAsync()
        {
            try
            {
                await ListVM.LoadAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[DeliveryNoteViewModel] ListVM.LoadAsync failed: {ex.Message}");
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  CREATE DN FROM JO PICKER
        //  BUG-29: was async void — now async Task called via SafeFireAsync
        // ═════════════════════════════════════════════════════════════════

        private async Task CreateDnFromJobOrderAsync(SavedJobOrder jobOrder)
        {
            if (App.DeliveryNotes == null) return;

            try
            {
                // ── Step 1: Check for existing Draft DNs ──────────────────
                var existingDns = await App.DeliveryNotes
                    .GetByJobOrderIdAsync(jobOrder.JobOrderId);

                var existingDraft = existingDns
                    .FirstOrDefault(d => string.Equals(
                        d.Status, "Draft",
                        StringComparison.OrdinalIgnoreCase));

                if (existingDraft != null)
                {
                    var choice = MessageBox.Show(
                        $"A Draft delivery note already exists for this Job Order:\n\n" +
                        $"   📋  {existingDraft.DeliveryNoteId}\n" +
                        $"   📅  {existingDraft.DeliveryDate:dd MMM yyyy}\n" +
                        $"   📦  {existingDraft.TotalOrderedQty} ordered  ·  " +
                        $"{existingDraft.TotalBalanceQty} balance\n\n" +
                        "Would you like to open the existing Draft?\n\n" +
                        "  Yes  → Open existing Draft\n" +
                        "  No   → Create a new DN anyway\n" +
                        "  Cancel → Go back",
                        "Draft Already Exists",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (choice == MessageBoxResult.Cancel) return;

                    if (choice == MessageBoxResult.Yes)
                    {
                        await DetailVM.LoadAsync(existingDraft.Id);
                        IsListView = false;
                        await SafeLoadListAsync();
                        return;
                    }
                }

                // ── Step 2: Deserialize JO snapshot ───────────────────────
                JobOrderSnapshot? joSnapshot = null;

                if (!string.IsNullOrWhiteSpace(jobOrder.FullSnapshotJson)
                    && jobOrder.FullSnapshotJson != "{}")
                {
                    try
                    {
                        // BUG-32: use shared static options
                        joSnapshot = JsonSerializer.Deserialize<JobOrderSnapshot>(
                            jobOrder.FullSnapshotJson, _jsonOptions);
                    }
                    catch
                    {
                        // Snapshot malformed — proceed with null
                    }
                }

                if (joSnapshot == null)
                {
                    MessageBox.Show(
                        $"Could not read Job Order data for " +
                        $"'{jobOrder.JobOrderId}'.\n\n" +
                        "The job order snapshot may be missing or corrupted.",
                        "Snapshot Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // ── Step 3: Get next sequence + build DN ID ───────────────
                int nextSeq = await App.DeliveryNotes
                    .GetNextSequenceAsync(jobOrder.JobOrderId);

                string dnId = await App.DeliveryNotes
                    .BuildDeliveryNoteIdAsync(jobOrder.JobOrderId, nextSeq);

                bool exists = await App.DeliveryNotes
                    .ExistsByDeliveryNoteIdAsync(dnId);

                if (exists)
                {
                    MessageBox.Show(
                        $"Delivery note '{dnId}' already exists.\n\n" +
                        "A DN was already created for this sequence number.",
                        "Already Exists",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // ── Step 4: Create DN entity from JO + snapshot ───────────
                var dnEntity = DeliveryNoteMapper.FromJobOrder(
                    jobOrder, joSnapshot, dnId, nextSeq);

                // ── Step 5: Insert + open detail ──────────────────────────
                var saved = await App.DeliveryNotes.InsertAsync(dnEntity);

                await DetailVM.LoadAsync(saved.Id);
                IsListView = false;

                await SafeLoadListAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to create delivery note:\n\n{ex.Message}",
                    "Create Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}