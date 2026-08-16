// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs
// ═══════════════════════════════════════════════════════════════════════
// CHAT 15 — PHASE 3
// Changes:
//   S-01  Step2VM → Step2ClientProfile.ClientProfileViewModel (swap)
//   S-02  Step3VM → Step3ProjectSpecs.ProjectSpecsViewModel (swap)
//   S-03  WizardSteps titles — Step 2=Client, Step 3=Project
//   S-04  StartNewInvoiceAsync resets in correct Client→Project order
//   S-05  StartNewInvoiceAsync calls LoadCardsAsync on all 3 step VMs
//   S-06  LoadSalesmanOptionsAsync called on Step2VM (client)
//   S-07  PropertyChanged subscription: Step2VM.SelectedClientName
//          → Step3VM.SetClientFilter() — wired once in constructor
//   S-08  LoadInvoiceIntoWizardAsync Step2/Step3 refs corrected
//   S-09  BUG-77/78/79/80/81 all preserved
//   S-10  Step7VM constructor args corrected for new Step2/Step3 types
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Step7PrintPreview.TermsAndConditions;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice
{
    /// <summary>
    /// Temporary list item — Dashboard contract placeholder.
    /// BUG-79 [MEDIUM]: Real data comes from Phase 20+ Dashboard analytics.
    /// </summary>
    public class InvoiceItem : ViewModelBase
    {
        public string InvoiceNo { get; set; }
        public string CustomerName { get; set; }
        public string Date { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Pending";
    }

    /// <summary>
    /// Represents a single step in the wizard sidebar.
    /// BUG-81: Kept in shell file — splitting breaks XAML clr-namespace bindings.
    /// </summary>
    public class WizardStep : ViewModelBase
    {
        public int StepNumber { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        private bool _isCompleted;
        public bool IsCompleted
        {
            get => _isCompleted;
            set { _isCompleted = value; OnPropertyChanged(); }
        }
    }

    /// <summary>
    /// Main ViewModel for the Proforma Invoice module.
    /// Phase 7.19: List-first architecture.
    /// Phase 8.2:  TriggerNewInvoice() for Dashboard quick action.
    /// Chat 15:    Step swap (Client=2, Project=3) + card wiring.
    ///
    /// BUG-77 [HIGH]:   LoadInvoiceIntoWizard async void → async Task via SafeFireAsync.
    /// BUG-78 [HIGH]:   StartNewInvoice clean spec-group rebuild (no handler leak).
    /// BUG-79 [MEDIUM]: InvoiceItem as Dashboard placeholder.
    /// BUG-80 [LOW]:    Dashboard totals return 0 — Phase 20+.
    /// BUG-81 [COSMETIC]: File structure preserved.
    /// S-07:            Client→Project filter wired via PropertyChanged subscription.
    ///                  Subscription is made ONCE in constructor — safe for VM lifetime.
    /// </summary>
    public class ProformaInvoiceViewModel : ViewModelBase,
        ProGlassAutomation.Agent.Context.IAgentContextAware
    {
        public string Title { get; } = "Proforma Invoice Builder";

        // ══════════════════════════════════════════════════════════════
        //  AGENT CONTEXT (opt-in, read-only)
        //
        //  Publishes ONLY the proforma number, so the agent can resolve
        //  "how much is still pending?" against the invoice on screen
        //  instead of asking which one you meant.
        //
        //  Deliberately excludes customer, project and totals: those are
        //  ERP values and must go through an audited read tool with a
        //  permission check, never leak through a context label.
        //
        //  Empty while a new invoice has no number yet - an empty string
        //  is honest, a stale number would make the agent answer about
        //  the wrong document.
        // ══════════════════════════════════════════════════════════════
        public string AgentContextDetail
        {
            get
            {
                try
                {
                    var id = Step1VM?.ProformaId;
                    return string.IsNullOrWhiteSpace(id) ? "" : $"editing {id}";
                }
                catch { return ""; }
            }
        }
        public string Subtitle { get; } = "Create professional glass invoices in 8 simple steps";

        /// <summary>
        /// Total wizard steps. Single source of truth — progress text,
        /// percent and navigation all derive from this constant.
        /// (Raised 7 → 8 when the Attachments step was added.)
        /// </summary>
        public const int TotalSteps = 9;   // PHASE B: Step 9 = Profit & Loss

        // ── Dashboard Contract (preserved — DashboardViewModel binding) ─
        public ObservableCollection<InvoiceItem> Invoices { get; }
            = new ObservableCollection<InvoiceItem>();

        // BUG-80: Real values from Phase 20+ Dashboard service.
        public decimal TotalSales => 0m;
        public int InvoiceCount => 0;
        public int PendingCount => 0;
        public bool HasInvoices => false;

        // ═══════════════════════════════════════════════════════════════
        //  STEP VIEW MODELS
        //
        //  S-01/S-02: Step2VM is now ClientProfileViewModel (was Project).
        //             Step3VM is now ProjectSpecsViewModel  (was Client).
        //             File names unchanged — only namespace + type changed.
        //
        //  IMPORTANT FOR PRINT PREVIEW (Step7VM):
        //    Step7VM constructor receives Step2VM (Client) and Step3VM (Project).
        //    PrintPreviewViewModel must use client fields from Step2VM
        //    and project fields from Step3VM. If PrintPreviewViewModel
        //    was previously referencing Step2VM for project data, it will
        //    now get client data instead — which is CORRECT after the swap.
        //    Verify PrintPreviewViewModel property access matches field names.
        // ═══════════════════════════════════════════════════════════════

        public Step1CompanySetup.CompanySetupViewModel Step1VM { get; }
        public Step2ClientProfile.ClientProfileViewModel Step2VM { get; }  // S-01: was ProjectSpecs
        public Step3ProjectSpecs.ProjectSpecsViewModel Step3VM { get; }  // S-02: was ClientProfile
        public Step4PricingEstimator.PricingEstimatorViewModel Step4VM { get; }
        public Step5DataGrid.DataGridViewModel Step5VM { get; }
        public Step6OtherCharges.OtherChargesViewModel Step6VM { get; }
        public Step7PrintPreview.PrintPreviewViewModel Step7VM { get; }

        /// <summary>
        /// Step 8 — Attachments (site photos, drawings, approvals).
        /// Mirrors the Delivery Note attachments system.
        /// </summary>
        public Step8Attachments.AttachmentsStepViewModel Step8VM { get; }

        /// <summary>
        /// PHASE B — Step 9: Profit &amp; Loss. Costs the invoice against
        /// Sheet Store purchase prices and the Step 4 production rates.
        /// Bound to its data sources in the constructor via Bind().
        /// </summary>
        public Step9ProfitLoss.ProfitLossStepViewModel Step9VM { get; }

        // ═══════════════════════════════════════════════════════════════
        //  WIZARD STEPS
        // ═══════════════════════════════════════════════════════════════

        public ObservableCollection<WizardStep> WizardSteps { get; }

        private WizardStep _currentStep;
        public WizardStep CurrentStep
        {
            get => _currentStep;
            set
            {
                if (_currentStep == value) return;

                if (_currentStep != null)
                    _currentStep.IsActive = false;

                _currentStep = value;

                if (_currentStep != null)
                    _currentStep.IsActive = true;

                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentStepNumber));
                OnPropertyChanged(nameof(StepProgressText));
                OnPropertyChanged(nameof(ProgressPercent));
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(IsLastStep));

                CommandManager.InvalidateRequerySuggested();
            }
        }

        public int CurrentStepNumber => CurrentStep?.StepNumber ?? 1;
        public string StepProgressText =>
            $"STEP  {CurrentStepNumber}  OF  {TotalSteps}";
        public double ProgressPercent =>
            (double)CurrentStepNumber / TotalSteps;
        public bool CanGoBack => CurrentStepNumber > 1;
        public bool CanGoNext => CurrentStepNumber < TotalSteps;
        public bool IsLastStep => CurrentStepNumber == TotalSteps;

        // ═══════════════════════════════════════════════════════════════
        //  PHASE 7.19 — LIST ↔ WIZARD
        // ═══════════════════════════════════════════════════════════════

        private bool _isListView = true;
        public bool IsListView
        {
            get => _isListView;
            set
            {
                if (_isListView == value) return;
                _isListView = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsWizardView));
            }
        }

        public bool IsWizardView => !_isListView;

        /// <summary>Saved invoices list VM — module landing page.</summary>
        public SavedInvoicesList.SavedInvoicesListViewModel SavedInvoicesListVM { get; }

        // ═══════════════════════════════════════════════════════════════
        //  COMMANDS
        // ═══════════════════════════════════════════════════════════════

        public ICommand NextStepCommand { get; }
        public ICommand PreviousStepCommand { get; }
        public ICommand GoToStepCommand { get; }
        public ICommand NewInvoiceCommand { get; }
        public ICommand BackToListCommand { get; }

        // ═══════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════

        public ProformaInvoiceViewModel()
        {
            // ── Step VMs (S-01/S-02: swapped types) ──────────────────
            Step1VM = new Step1CompanySetup.CompanySetupViewModel(
                stepNumber => GoToStep(stepNumber));

            Step2VM = new Step2ClientProfile.ClientProfileViewModel();   // S-01
            Step3VM = new Step3ProjectSpecs.ProjectSpecsViewModel();     // S-02

            Step4VM = new Step4PricingEstimator.PricingEstimatorViewModel();
            Step5VM = new Step5DataGrid.DataGridViewModel();
            Step6VM = new Step6OtherCharges.OtherChargesViewModel(Step5VM);

            // S-10: Step7VM receives Step2VM (Client) and Step3VM (Project).
            // PrintPreviewViewModel must use correct property names —
            // Step2VM has CustomerName, CustomerTrn, BillingAddress etc.
            // Step3VM has ProjectIdentifierName, LpoNumber etc.
            Step7VM = new Step7PrintPreview.PrintPreviewViewModel(
                Step1VM, Step2VM, Step3VM, Step5VM, Step6VM);

            // Step 8 — Attachments. Reads the live Proforma ID from
            // Step 1 so files stay keyed to the invoice being edited.
            Step8VM = new Step8Attachments.AttachmentsStepViewModel(Step1VM);

            // ── PHASE B: Step 9 — Profit & Loss ──────────────────────
            // Bound after every dependency exists. The gross provider is
            // a lambda (not a cached value) so the step always reads the
            // CURRENT Step 7 grand total, even after Step 6 edits.
            Step9VM = new Step9ProfitLoss.ProfitLossStepViewModel();
            Step9VM.Bind(
                Step5VM,
                Step4VM.DguVM,
                Step4VM.SguVM,
                () => Step7VM.InvoiceMainSummary.GrandTotalVatInclusive,
                Step4VM.LamVM);   // PHASE J: laminated costing

            // Let the printable invoice reach the P&L so the mapper can
            // persist it inside the snapshot (additive, nullable link).
            Step7VM.PrintableInvoice.ProfitLoss = Step9VM;

            // ── S-03: Wizard step titles — corrected after swap ───────
            WizardSteps = new ObservableCollection<WizardStep>
            {
                new WizardStep
                {
                    StepNumber = 1,
                    Title      = "Company details",
                    Subtitle   = "TRN, location and billing"
                },
                new WizardStep
                {
                    StepNumber = 2,
                    Title      = "Client profile",           // S-03: was "Project"
                    Subtitle   = "Customer, salesman and billing"
                },
                new WizardStep
                {
                    StepNumber = 3,
                    Title      = "Project specs",            // S-03: was "Other references"
                    Subtitle   = "Reference numbers, PO and LPO"
                },
                new WizardStep
                {
                    StepNumber = 4,
                    Title      = "Glass type & specs",
                    Subtitle   = "Estimators, materials & treatments"
                },
                new WizardStep
                {
                    StepNumber = 5,
                    Title      = "Data grid",
                    Subtitle   = "Multi-spec row configurations"
                },
                new WizardStep
                {
                    StepNumber = 6,
                    Title      = "Other charges",
                    Subtitle   = "Supplementary, prep, delivery"
                },
                new WizardStep
                {
                    StepNumber = 7,
                    Title      = "Print preview",
                    Subtitle   = "Review, terms and ERP conversion"
                },
                new WizardStep
                {
                    StepNumber = 8,
                    Title      = "Attachments",
                    Subtitle   = "Photos, drawings & approvals"
                },
                new WizardStep
                {
                    StepNumber = 9,
                    Title      = "Profit & Loss",
                    Subtitle   = "Real cost, margin and profitability"
                }
            };

            CurrentStep = WizardSteps[0];

            // ── Navigation commands ───────────────────────────────────
            NextStepCommand = new RelayCommand(
                _ => GoToStep(CurrentStepNumber + 1),
                _ => CanGoNext);

            PreviousStepCommand = new RelayCommand(
                _ => GoToStep(CurrentStepNumber - 1),
                _ => CanGoBack);

            GoToStepCommand = new RelayCommand(param =>
            {
                if (param is WizardStep step)
                    CurrentStep = step;
            });

            // BUG-77/78: SafeFireAsync wrappers
            NewInvoiceCommand = new RelayCommand(
                _ => SafeFireAsync(StartNewInvoiceAsync));

            BackToListCommand = new RelayCommand(
                _ => SafeFireAsync(ReturnToListAsync));

            // ── Saved Invoices List VM ────────────────────────────────
            SavedInvoicesListVM =
                new SavedInvoicesList.SavedInvoicesListViewModel();

            SavedInvoicesListVM.OnInvoiceLoadRequested = id =>
                SafeFireAsync(() => LoadInvoiceIntoWizardAsync(id));

            // v2 version history: open an old version's snapshot
            SavedInvoicesListVM.OnSnapshotRestoreRequested = json =>
                SafeFireAsync(() => RestoreSnapshotIntoWizardAsync(json));

            // ── S-07: Client → Project filter wiring ─────────────────
            // Subscribed ONCE here — safe for VM lifetime (no dispose needed
            // since shell VM and step VMs have identical lifetimes).
            // When user clicks a client card in Step 2, SelectedClientName
            // changes → we push it to Step3VM.SetClientFilter().
            Step2VM.PropertyChanged += OnStep2PropertyChanged;
        }

        // ═══════════════════════════════════════════════════════════════
        //  S-07: CLIENT → PROJECT FILTER HANDLER
        // ═══════════════════════════════════════════════════════════════

        private void OnStep2PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Step2ClientProfile.ClientProfileViewModel.SelectedClientName))
                return;

            // Push selected client name to project step as filter
            Step3VM.SetClientFilter(Step2VM.SelectedClientName);
        }

        // ═══════════════════════════════════════════════════════════════
        //  BUG-77 [HIGH]: SafeFireAsync wrapper
        // ═══════════════════════════════════════════════════════════════

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

        private void GoToStep(int stepNumber)
        {
            var step = WizardSteps.FirstOrDefault(s => s.StepNumber == stepNumber);
            if (step == null) return;

            CurrentStep = step;

            // PHASE A/J: Step 4 prices come from live Sheet Store stock,
            // which the user may have edited since the wizard opened.
            if (stepNumber == 4)
                Step4VM.RefreshSheetStoreData();

            // Step 8 shows disk state — refresh every time it is
            // entered so external changes are never shown stale.
            if (stepNumber == 8)
                SafeFireAsync(() => Step8VM.LoadAsync());

            // Step 9 costs live data (Sheet Store + Step 5 + Step 7 total)
            // so it recalculates on every entry rather than showing stale
            // figures from an earlier visit.
            if (stepNumber == 9)
                SafeFireAsync(() => Step9VM.RecalculateAsync());
        }

        // ═══════════════════════════════════════════════════════════════
        //  PHASE 7.19 — LIST ↔ WIZARD NAVIGATION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// BUG-78 [HIGH]: Clean spec-group rebuild — no event handler leak.
        /// BUG-33 integration: async unique ID via GenerateUniqueProformaIdAsync.
        /// S-04/S-05/S-06: Resets in Client→Project order. Loads all cards.
        /// </summary>
        private async Task StartNewInvoiceAsync()
        {
            // ── Reset Step 1 ──────────────────────────────────────────
            Step1VM.CompanyName = "";
            Step1VM.CompanyTrn = "";
            Step1VM.CompanyLocation = "";
            Step1VM.IssueDate = DateTime.Now;
            Step1VM.ValidUntil = DateTime.Now.AddDays(7);
            Step1VM.ProformaStatus = "Draft";

            // BUG-33: DB-verified unique ID
            await Step1VM.GenerateUniqueProformaIdAsync();

            // ── Reset Step 2 (Client) — S-04 ─────────────────────────
            Step2VM.CustomerName = "";
            Step2VM.CustomerTrn = "";
            Step2VM.CustomerReferenceCode = "";
            Step2VM.SalesRepresentative = "";
            Step2VM.DocumentPreparedBy = "";
            Step2VM.BillingAddress = "";
            Step2VM.AttentionExecutiveName = "";
            Step2VM.ClientContactPhone = "";

            // ── Reset Step 3 (Project) — S-04 ────────────────────────
            Step3VM.ProjectIdentifierName = "";
            Step3VM.ProjectContractNo = "";
            Step3VM.SiteDeliveryLocation = "";
            Step3VM.LpoNumber = "";

            // ── Reset Step 5 — clean spec-group rebuild (BUG-78) ─────
            Step5VM.SpecGroups.Clear();
            Step5VM.SpecGroups.Add(new Step5DataGrid.SpecGroup
            {
                GroupNumber = 1,
                GroupName = "Spec Group 1",
                BaseRate = 150m,
                SurchargePercent = 0m,
                SelectedGlassType = "SGU"
            });

            // ── S-05: Load cards from masterdata.db ───────────────────
            // Run all 3 card loads concurrently — faster than sequential.
            // LoadSalesmanOptionsAsync runs alongside for Step2 dropdown.
            await Task.WhenAll(
                Step1VM.LoadCardsAsync(),
                Step2VM.LoadCardsAsync(),
                Step2VM.LoadSalesmanOptionsAsync(),  // S-06
                Step3VM.LoadCardsAsync()
            );

            // ── Step 8 follows the fresh Proforma ID automatically ────
            // (its PropertyChanged hook on Step1VM triggers the reload).
            await Step8VM.LoadAsync();

            // ── Navigate to Step 1 and show wizard ────────────────────
            CurrentStep = WizardSteps[0];
            IsListView = false;
        }

        /// <summary>
        /// Returns to the saved invoices list.
        /// BUG-77: async Task via SafeFireAsync.
        /// </summary>
        private async Task ReturnToListAsync()
        {
            // AUTO-DRAFT: never lose wizard work — silently persist a
            // Draft before showing the list (no-op when empty).
            await Step7VM.PrintableInvoice.SilentDraftSaveAsync();

            IsListView = true;
            await SavedInvoicesListVM.LoadInvoicesAsync();
        }

        /// <summary>
        /// Loads saved invoice into wizard for editing.
        /// BUG-77 [HIGH]: was async void — now async Task via SafeFireAsync.
        /// S-08: Step2VM = Client fields, Step3VM = Project fields (corrected).
        /// </summary>
        private async Task LoadInvoiceIntoWizardAsync(int invoiceId)
        {
            if (App.Invoices == null) return;

            var entity = await App.Invoices.GetByIdAsync(invoiceId);
            if (entity == null)
            {
                MessageBox.Show(
                    "Selected invoice could not be found.",
                    "Load Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);

            // BUG-57: batch update so Recalculate fires ONCE
            Step7VM.InvoiceMainSummary.BeginBatchUpdate();
            try
            {
                // S-08: Step2VM=ClientProfileVM, Step3VM=ProjectSpecsVM
                ProformaInvoiceMapper.RestoreWizardFromSnapshot(
                    snapshot,
                    Step1VM, Step2VM, Step3VM, Step5VM, Step6VM,
                    Step7VM.InvoiceMainSummary,
                    Step7VM.Terms,
                    Step7VM.PrintableInvoice);
            }
            finally
            {
                Step7VM.InvoiceMainSummary.EndBatchUpdate();
            }

            // Also reload cards so they're fresh when user browses
            await Task.WhenAll(
                Step1VM.LoadCardsAsync(),
                Step2VM.LoadCardsAsync(),
                Step2VM.LoadSalesmanOptionsAsync(),
                Step3VM.LoadCardsAsync()
            );

            // Step 8 — show the loaded invoice's attachments
            await Step8VM.LoadAsync();

            CurrentStep = WizardSteps[0];
            IsListView = false;
        }

        /// <summary>
        /// v2 VERSION RESTORE (§24): opens a stored version snapshot in
        /// the wizard. The user's next save creates a NEW version —
        /// history is append-only and never silently erased.
        /// </summary>
        private async Task RestoreSnapshotIntoWizardAsync(string snapshotJson)
        {
            // Reuse the existing mapper path: wrap the JSON in a
            // throwaway entity and deserialize through the normal API.
            var wrapper = new Models.ProformaInvoice.SavedInvoice
                .SavedProformaInvoice
            {
                FullSnapshotJson = snapshotJson
            };

            var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(wrapper);
            if (snapshot == null)
            {
                MessageBox.Show(
                    "This version could not be read.",
                    "Restore Version",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Step7VM.InvoiceMainSummary.BeginBatchUpdate();
            try
            {
                ProformaInvoiceMapper.RestoreWizardFromSnapshot(
                    snapshot,
                    Step1VM, Step2VM, Step3VM, Step5VM, Step6VM,
                    Step7VM.InvoiceMainSummary,
                    Step7VM.Terms,
                    Step7VM.PrintableInvoice);
            }
            finally
            {
                Step7VM.InvoiceMainSummary.EndBatchUpdate();
            }

            await Step8VM.LoadAsync();

            CurrentStep = WizardSteps[0];
            IsListView = false;
        }

        // ═══════════════════════════════════════════════════════════════
        //  PHASE 8.2 — Dashboard quick action trigger
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by MainViewModel when Dashboard "New Invoice" is clicked.
        /// Delegates to StartNewInvoiceAsync via SafeFireAsync (BUG-77).
        /// </summary>
        public void TriggerNewInvoice() =>
            SafeFireAsync(StartNewInvoiceAsync);
    }
}