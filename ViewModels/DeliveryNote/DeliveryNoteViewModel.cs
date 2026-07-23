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
    /// Phase 10.5 : Added OpenDeliveryNoteAsync(int) — cross-module entry.
    /// Phase 10.7a: Added CreateDnFromJobOrderAsync(SavedJobOrder) —
    ///              creates a Draft DN from a selected JO, opens detail view.
    ///              Warns if a Draft DN already exists for the same JO —
    ///              user can open existing, create new, or cancel.
    ///              DeliveredQty is already clamped by DnItemRowViewModel
    ///              to never exceed (OrderedQty - PreviouslyDeliveredQty).
    /// </summary>
    public class DeliveryNoteViewModel : ViewModelBase
    {
        public string Title { get; } = "Delivery Notes";
        public string Subtitle { get; } = "Track glass deliveries, partial shipments and balance quantities";

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

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public DeliveryNoteViewModel()
        {
            ListVM = new DeliveryNoteListViewModel();
            DetailVM = new DeliveryNoteDetailViewModel();

            ListVM.OnDeliveryNoteOpenRequested = LoadDetailAsync;
            ListVM.OnCreateDnFromJobOrder = CreateDnFromJobOrderAsync;

            DetailVM.OnRequestBackToList = ReturnToList;

            BackToListCommand = new RelayCommand(_ => ReturnToList());
        }

        // ═════════════════════════════════════════════════════════════════
        //  INTERNAL — triggered by ListVM.OpenCommand
        // ═════════════════════════════════════════════════════════════════

        private async void LoadDetailAsync(int deliveryNoteId)
        {
            await DetailVM.LoadAsync(deliveryNoteId);
            IsListView = false;
        }

        // ═════════════════════════════════════════════════════════════════
        //  INTERNAL — triggered by DetailVM.BackCommand
        // ═════════════════════════════════════════════════════════════════

        private async void ReturnToList()
        {
            IsListView = true;
            await ListVM.LoadAsync();
        }

        // ═════════════════════════════════════════════════════════════════
        //  PUBLIC — cross-module entry point (called by MainViewModel)
        // ═════════════════════════════════════════════════════════════════

        public async Task OpenDeliveryNoteAsync(int deliveryNoteId)
        {
            await DetailVM.LoadAsync(deliveryNoteId);
            IsListView = false;
            _ = ListVM.LoadAsync();
        }

        // ═════════════════════════════════════════════════════════════════
        //  Phase 10.7a — CREATE DN FROM JO PICKER
        //
        //  Flow:
        //  1. Check if a Draft DN already exists for this JO
        //     → Yes: ask user to Open Existing / Create New / Cancel
        //     → No:  proceed to create
        //  2. Deserialize JobOrderSnapshot from JO entity
        //  3. Get next sequence + build DN ID
        //  4. Create Draft DN via DeliveryNoteMapper.FromJobOrder
        //  5. Insert + open in detail view
        //
        //  DeliveredQty is clamped in DnItemRowViewModel to never
        //  exceed (OrderedQty - PreviouslyDeliveredQty), so users
        //  cannot deliver more than the remaining balance.
        // ═════════════════════════════════════════════════════════════════

        private async void CreateDnFromJobOrderAsync(SavedJobOrder jobOrder)
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
                        System.StringComparison.OrdinalIgnoreCase));

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
                        // Open the existing Draft DN
                        await DetailVM.LoadAsync(existingDraft.Id);
                        IsListView = false;
                        _ = ListVM.LoadAsync();
                        return;
                    }

                    // choice == No → fall through to create new DN
                }

                // ── Step 2: Deserialize JO snapshot ───────────────────────
                JobOrderSnapshot? joSnapshot = null;

                if (!string.IsNullOrWhiteSpace(jobOrder.FullSnapshotJson)
                    && jobOrder.FullSnapshotJson != "{}")
                {
                    try
                    {
                        joSnapshot = JsonSerializer.Deserialize<JobOrderSnapshot>(
                            jobOrder.FullSnapshotJson,
                            new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                    }
                    catch
                    {
                        // Snapshot malformed — proceed with null
                    }
                }

                if (joSnapshot == null)
                {
                    MessageBox.Show(
                        $"Could not read Job Order data for '{jobOrder.JobOrderId}'.\n\n" +
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

                // Safety check
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
                    jobOrder,
                    joSnapshot,
                    dnId,
                    nextSeq);

                // ── Step 5: Insert + open detail ──────────────────────────
                var saved = await App.DeliveryNotes.InsertAsync(dnEntity);

                await DetailVM.LoadAsync(saved.Id);
                IsListView = false;

                _ = ListVM.LoadAsync();
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