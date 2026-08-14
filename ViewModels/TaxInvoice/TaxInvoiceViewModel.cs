using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels.Shared;
using ProGlassAutomation.ViewModels.TaxInvoice.GodView;
using ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceDetail;
using ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceList;
using ProGlassAutomation.Views.TaxInvoice.TaxInvoiceList;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.TaxInvoice
{
    /// <summary>
    /// Shell ViewModel for the Tax Invoice module.
    ///
    /// Manages list ↔ detail toggle. Same pattern as DeliveryNoteViewModel.
    ///
    /// ENTRY POINTS:
    ///   • Navigation sidebar          → constructor fires
    ///   • ListVM.OnInvoiceOpenRequested   → LoadDetailAsync(id)
    ///   • ListVM.OnCreateInvoiceRequested → OpenProformaPickerAsync()
    ///   • BackToListCommand           → ReturnToList()
    ///
    /// CROSS-MODULE ENTRY POINTS (called by MainViewModel from DN module):
    ///   • OpenInvoiceAsync(id)                    — open specific saved invoice
    ///   • CreateFromProformaAsync(proformaId)     — Master Invoice from DN List
    ///                                                (imports all delivered qty
    ///                                                 across sibling DNs)
    ///   • CreateFromDeliveryNoteAsync(dnId)       — Convert from DN Detail
    ///                                                (imports only THIS DN's
    ///                                                 delivered qty)
    ///
    /// PARTIAL INVOICING RULE:
    ///   Full PI import (all rows regardless of delivery) is only used when
    ///   creating an invoice from the PI picker dialog directly — before
    ///   any deliveries exist. All DN-driven flows use delivered qty only.
    /// </summary>
    public class TaxInvoiceViewModel : ViewModelBase
    {
        public string Title { get; } = "Tax Invoices";
        public string Subtitle { get; } =
            "Create and manage UAE VAT-compliant tax invoices";

        // ── Tab strip keys (Workspace | God View | Activity) ─────────────

        public const string TabWorkspace = "Workspace";
        public const string TabGodView = "GodView";
        public const string TabActivity = "Activity";

        // ── Child VMs ─────────────────────────────────────────────────────

        public TaxInvoiceListViewModel ListVM { get; }
        public TaxInvoiceDetailViewModel DetailVM { get; }

        /// <summary>God View tab — every tax invoice in the system.</summary>
        public TaxInvoiceGodViewViewModel GodViewVM { get; }

        /// <summary>Activity tab — live audit feed for this section.</summary>
        public SectionActivityViewModel ActivityVM { get; }

        // ── View toggle ───────────────────────────────────────────────────

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

        // ── Tab strip state ───────────────────────────────────────────────

        private string _activeTab = TabWorkspace;

        /// <summary>Which tab is active: Workspace / GodView / Activity.</summary>
        public string ActiveTab
        {
            get => _activeTab;
            set
            {
                if (_activeTab == value) return;
                _activeTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsWorkspaceTab));
                OnPropertyChanged(nameof(IsGodViewTab));
                OnPropertyChanged(nameof(IsActivityTab));
            }
        }

        public bool IsWorkspaceTab => ActiveTab == TabWorkspace;
        public bool IsGodViewTab => ActiveTab == TabGodView;
        public bool IsActivityTab => ActiveTab == TabActivity;

        // ── Commands ──────────────────────────────────────────────────────

        public ICommand BackToListCommand { get; }

        /// <summary>Switches tabs. Parameter = tab key constant.</summary>
        public ICommand SwitchTabCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public TaxInvoiceViewModel()
        {
            ListVM = new TaxInvoiceListViewModel();
            DetailVM = new TaxInvoiceDetailViewModel();

            // Wire list → detail navigation
            ListVM.OnInvoiceOpenRequested = id =>
                SafeFireAsync(() => LoadDetailAsync(id));

            // Shell opens ProformaPickerDialog — ListVM never touches Views
            ListVM.OnCreateInvoiceRequested = () =>
                SafeFireAsync(OpenProformaPickerAsync);

            // Wire detail → list back navigation
            DetailVM.OnRequestBackToList = () =>
                SafeFireAsync(ReturnToList);

            BackToListCommand = new RelayCommand(
                _ => SafeFireAsync(ReturnToList));

            // ── God View tab: row-open hands off to detail flow ───────
            GodViewVM = new TaxInvoiceGodViewViewModel();
            GodViewVM.OnOpenRequested = id =>
                SafeFireAsync(() => LoadDetailAsync(id));

            // ── Activity tab: live audit feed for this section ────────
            ActivityVM = new SectionActivityViewModel(
                AuditSections.TaxInvoice);

            SwitchTabCommand = new RelayCommand(param =>
                SwitchTab(param as string ?? TabWorkspace));

            // Initial list load
            SafeFireAsync(SafeLoadListAsync);
        }

        // ═════════════════════════════════════════════════════════════════
        //  TAB SWITCHING
        // ═════════════════════════════════════════════════════════════════

        private void SwitchTab(string tabKey)
        {
            ActiveTab = tabKey;

            switch (tabKey)
            {
                case TabGodView:
                    GodViewVM.LoadAsync();
                    break;
                case TabActivity:
                    ActivityVM.LoadAsync();
                    break;
            }
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

        private async Task LoadDetailAsync(int invoiceId)
        {
            await DetailVM.LoadAsync(invoiceId);
            IsListView = false;
        }

        private async Task ReturnToList()
        {
            IsListView = true;
            await SafeLoadListAsync();
        }

        // ═════════════════════════════════════════════════════════════════
        //  PUBLIC ENTRY POINTS — cross-module navigation
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by MainViewModel when navigating directly to a saved invoice.
        /// e.g. clicking "Open Invoice" from DN detail history sidebar.
        /// </summary>
        public async Task OpenInvoiceAsync(int invoiceId)
        {
            await DetailVM.LoadAsync(invoiceId);
            IsListView = false;
            await SafeLoadListAsync();
        }

        /// <summary>
        /// MASTER INVOICE from DN List (per-JO group row).
        ///
        /// Imports ALL delivered qty across ALL sibling DNs for this PI.
        /// Only rows with total delivered > 0 are included.
        /// Rate from PI, Qty from aggregated DN deliveries.
        ///
        /// If a draft invoice already exists for this PI, prompts user to
        /// open the existing draft or create a new one.
        /// </summary>
        public async Task CreateFromProformaAsync(string proformaId)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return;

            try
            {
                var existingDraft = await CheckForExistingDraftAsync(proformaId);
                if (existingDraft.OpenExisting) return;

                // Master Invoice — aggregate all sibling DNs' delivered qty
                await DetailVM.InitFromDeliveryNoteFamilyAsync(proformaId);
                IsListView = false;
                await SafeLoadListAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to create Master invoice:\n\n{ex.Message}",
                    "Create Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// CONVERT SINGLE DN → Tax Invoice (from DN Detail view).
        ///
        /// Imports ONLY this DN's delivered qty. Rate from PI, Qty from DN.
        /// Rows with 0 delivered in this DN are excluded.
        ///
        /// Prompts if a draft invoice already exists for the same PI.
        /// </summary>
        public async Task CreateFromDeliveryNoteAsync(string deliveryNoteId)
        {
            if (string.IsNullOrWhiteSpace(deliveryNoteId)) return;
            if (App.DeliveryNotes == null) return;

            try
            {
                // Look up the DN's ProformaId first — needed for draft check
                var dn = await App.DeliveryNotes
                    .GetByDeliveryNoteIdAsync(deliveryNoteId);

                if (dn == null)
                {
                    MessageBox.Show(
                        $"Delivery Note '{deliveryNoteId}' not found.",
                        "Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(dn.ProformaId))
                {
                    MessageBox.Show(
                        $"DN '{deliveryNoteId}' has no linked Proforma Invoice.\n\n" +
                        "Tax Invoices require a PI reference.",
                        "No PI Reference",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var existingDraft = await CheckForExistingDraftAsync(dn.ProformaId);
                if (existingDraft.OpenExisting) return;

                // Convert single DN — only its delivered qty
                await DetailVM.InitFromDeliveryNoteAsync(deliveryNoteId);
                IsListView = false;
                await SafeLoadListAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to convert DN to invoice:\n\n{ex.Message}",
                    "Convert Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  PROFORMA PICKER — opens dialog for direct PI-only invoicing
        //  (rare case — normally invoices are created from DN context)
        // ═════════════════════════════════════════════════════════════════

        private async Task OpenProformaPickerAsync()
        {
            if (App.Invoices == null)
            {
                MessageBox.Show(
                    "Proforma Invoice database not initialized.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            await Task.Yield();

            var dialog = new ProformaPickerDialog
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true) return;

            string piId = dialog.SelectedProformaId;
            if (string.IsNullOrWhiteSpace(piId)) return;

            // From picker → try Master Invoice first (uses delivered qty).
            // If no deliveries exist, falls back to full PI import.
            await CreateFromProformaOrFullPiAsync(piId);
        }

        /// <summary>
        /// From PI picker: prefer Master Invoice (delivered qty).
        /// If no deliveries → full PI import as fallback.
        /// </summary>
        private async Task CreateFromProformaOrFullPiAsync(string proformaId)
        {
            try
            {
                var existingDraft = await CheckForExistingDraftAsync(proformaId);
                if (existingDraft.OpenExisting) return;

                // Check if any DN exists for this PI
                bool hasDeliveries = false;
                if (App.DeliveryNotes != null)
                {
                    var allDns = await App.DeliveryNotes.GetAllAsync();
                    hasDeliveries = allDns.Any(d =>
                        string.Equals(d.ProformaId, proformaId,
                            StringComparison.OrdinalIgnoreCase)
                        && d.TotalDeliveredQty > 0);
                }

                if (hasDeliveries)
                {
                    // Master Invoice path — sum delivered across sibling DNs
                    await DetailVM.InitFromDeliveryNoteFamilyAsync(proformaId);
                }
                else
                {
                    // No deliveries yet — full PI import (advance invoice case)
                    var choice = MessageBox.Show(
                        $"No delivered items found for '{proformaId}'.\n\n" +
                        "Create invoice using FULL Proforma Invoice qty?\n\n" +
                        "  Yes → Import all PI rows (advance invoicing)\n" +
                        "  No  → Cancel",
                        "No Deliveries Yet",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (choice != MessageBoxResult.Yes) return;

                    await DetailVM.InitNewAsync(proformaId);
                }

                IsListView = false;
                await SafeLoadListAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to create invoice:\n\n{ex.Message}",
                    "Create Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  EXISTING DRAFT CHECK — shared by all creation paths
        // ═════════════════════════════════════════════════════════════════

        private async Task<(bool OpenExisting, bool ProceedWithNew)>
            CheckForExistingDraftAsync(string proformaId)
        {
            if (App.TaxInvoices == null)
                return (false, true);

            var existing = await App.TaxInvoices
                .GetByProformaIdAsync(proformaId);

            var existingDraft = existing.Count > 0
                ? existing.Find(i => string.Equals(
                    i.Status, "Draft",
                    StringComparison.OrdinalIgnoreCase))
                : null;

            if (existingDraft == null)
                return (false, true);

            var choice = MessageBox.Show(
                $"A Draft invoice already exists for '{proformaId}':\n\n" +
                $"   🧾  {existingDraft.InvoiceNumber}\n" +
                $"   📅  {existingDraft.IssueDate:dd MMM yyyy}\n" +
                $"   💰  AED {existingDraft.GrandTotal:N2}\n\n" +
                "What would you like to do?\n\n" +
                "  Yes    → Open the existing Draft\n" +
                "  No     → Create a NEW invoice (will be Partial: A/B/C)\n" +
                "  Cancel → Go back",
                "Draft Invoice Exists",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (choice == MessageBoxResult.Cancel)
                return (true, false);  // "Open existing" flag set to skip further work

            if (choice == MessageBoxResult.Yes)
            {
                await DetailVM.LoadAsync(existingDraft.Id);
                IsListView = false;
                await SafeLoadListAsync();
                return (true, false);
            }

            // No → proceed to create new
            return (false, true);
        }

        // ═════════════════════════════════════════════════════════════════
        //  SAFE LIST RELOAD
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
                    $"[TaxInvoiceViewModel] ListVM.LoadAsync failed: {ex.Message}");
            }
        }
    }
}