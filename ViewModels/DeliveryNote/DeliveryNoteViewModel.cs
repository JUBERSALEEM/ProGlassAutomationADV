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
    /// CHAT 14 CROSS-MODULE INTEGRATION:
    ///   Two distinct callbacks bubble up to MainViewModel for Tax Invoice
    ///   creation — each with different semantics:
    ///
    ///   1. OnRequestOpenTaxInvoiceForDeliveryNote (dnId)
    ///      Fired by: DN Detail "Convert to Tax Invoice" button
    ///      Purpose: Invoice ONLY that specific DN's delivered qty
    ///
    ///   2. OnRequestOpenTaxInvoiceForMasterFromProforma (proformaId)
    ///      Fired by: DN List "Master Invoice" button on JO group row
    ///      Purpose: Invoice ALL delivered qty across ALL sibling DNs
    /// </summary>
    public class DeliveryNoteViewModel : ViewModelBase
    {
        public string Title { get; } = "Delivery Notes";
        public string Subtitle { get; } =
            "Track glass deliveries, partial shipments and balance quantities";

        public DeliveryNoteListViewModel ListVM { get; }
        public DeliveryNoteDetailViewModel DetailVM { get; }

        // ── Cross-module callbacks (Chat 14) ──────────────────────────────

        /// <summary>
        /// Fired from DN Detail "Convert to Tax Invoice".
        /// Argument = DN ID. Result: single-DN-scoped invoice.
        /// </summary>
        public Func<string, Task>? OnRequestOpenTaxInvoiceForDeliveryNote { get; set; }

        /// <summary>
        /// Fired from DN List "Master Invoice" on JO group row.
        /// Argument = ProformaId. Result: aggregated invoice across all
        /// sibling DNs (Master invoice covering all delivered pieces).
        /// </summary>
        public Func<string, Task>? OnRequestOpenTaxInvoiceForMasterFromProforma { get; set; }

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

            // Chat 14: DN List Master Invoice → PI-scoped aggregation
            ListVM.OnRequestOpenTaxInvoiceForProforma = async piId =>
            {
                try
                {
                    await RelayMasterInvoiceRequestAsync(piId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to open Master Invoice:\n\n{ex.Message}",
                        "Navigation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };

            DetailVM.OnRequestBackToList = () => SafeFireAsync(ReturnToList);

            // Chat 14: DN Detail Convert → single-DN-scoped invoice
            DetailVM.OnRequestOpenTaxInvoiceForDeliveryNote = async dnId =>
            {
                try
                {
                    await RelayConvertDnRequestAsync(dnId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to convert DN to Tax Invoice:\n\n{ex.Message}",
                        "Navigation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };

            BackToListCommand = new RelayCommand(
                _ => SafeFireAsync(ReturnToList));
        }

        // ═════════════════════════════════════════════════════════════════
        //  CHAT 14 — TAX INVOICE RELAYS
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Relay for "Master Invoice" button on DN List JO group row.
        /// Bubbles ProformaId up to MainViewModel which triggers
        /// TaxInvoiceVM.CreateFromProformaAsync (aggregates all sibling DNs).
        /// </summary>
        private async Task RelayMasterInvoiceRequestAsync(string proformaId)
        {
            if (string.IsNullOrWhiteSpace(proformaId))
            {
                MessageBox.Show(
                    "Cannot create Master Invoice — no Proforma Invoice reference.\n\n" +
                    "This JO has no linked PI.",
                    "No PI Reference",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (OnRequestOpenTaxInvoiceForMasterFromProforma == null)
            {
                MessageBox.Show(
                    "Tax Invoice navigation is not wired up.\n\n" +
                    "This is a configuration error — please restart the app.",
                    "Not Wired",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            await OnRequestOpenTaxInvoiceForMasterFromProforma(proformaId);
        }

        /// <summary>
        /// Relay for "Convert to Tax Invoice" button on DN Detail history row.
        /// Bubbles DN ID up to MainViewModel which triggers
        /// TaxInvoiceVM.CreateFromDeliveryNoteAsync (single-DN-scoped).
        /// </summary>
        private async Task RelayConvertDnRequestAsync(string deliveryNoteId)
        {
            if (string.IsNullOrWhiteSpace(deliveryNoteId))
            {
                MessageBox.Show(
                    "Cannot convert to Tax Invoice — no DN reference.",
                    "No DN Reference",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (OnRequestOpenTaxInvoiceForDeliveryNote == null)
            {
                MessageBox.Show(
                    "Tax Invoice navigation is not wired up.\n\n" +
                    "This is a configuration error — please restart the app.",
                    "Not Wired",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            await OnRequestOpenTaxInvoiceForDeliveryNote(deliveryNoteId);
        }

        // ═════════════════════════════════════════════════════════════════
        //  SAFE ASYNC FIRE
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

        public async Task OpenDeliveryNoteAsync(int deliveryNoteId)
        {
            await DetailVM.LoadAsync(deliveryNoteId);
            IsListView = false;
            await SafeLoadListAsync();
        }

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
        // ═════════════════════════════════════════════════════════════════

        private async Task CreateDnFromJobOrderAsync(SavedJobOrder jobOrder)
        {
            if (App.DeliveryNotes == null) return;

            try
            {
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

                JobOrderSnapshot? joSnapshot = null;

                if (!string.IsNullOrWhiteSpace(jobOrder.FullSnapshotJson)
                    && jobOrder.FullSnapshotJson != "{}")
                {
                    try
                    {
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

                var dnEntity = DeliveryNoteMapper.FromJobOrder(
                    jobOrder, joSnapshot, dnId, nextSeq);

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