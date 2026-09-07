// ═══════════════════════════════════════════════════════════════════════
// FILE: PrintableInvoiceViewModel.Part1.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.ProformaInvoice.Export;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.ProformaInvoice.SavedInvoicesList;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step1CompanySetup;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step2ClientProfile;   // FIXED: was Step2ProjectSpecs
using ProGlassAutomation.ViewModels.ProformaInvoice.Step3ProjectSpecs;    // FIXED: was Step3ClientProfile
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.InvoiceMainSummary;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;   // PHASE B
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.TermsAndConditions;
using ProGlassAutomation.Views.ProformaInvoice.SavedInvoicesList;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.PrintableInvoice
{
        public partial class PrintableInvoiceViewModel : ViewModelBase
    {
            // FIXED: property types now use new namespaces
        public CompanySetupViewModel Company { get; }
        public ClientProfileViewModel Client { get; }  // was ProjectSpecsViewModel
        public ProjectSpecsViewModel Project { get; }  // was ClientProfileViewModel
        public DataGridViewModel DataGrid { get; }
        public OtherChargesViewModel OtherCharges { get; }
        public InvoiceMainSummaryViewModel MainSummary { get; }
        public TermsAndConditionsViewModel Terms { get; }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 7.16a — Prepared By fields
        // ══════════════════════════════════════════════════════════════════

        private string _preparedByNameOverride;
        public string PreparedByName
        {
            get => string.IsNullOrWhiteSpace(_preparedByNameOverride)
                ? (Client?.DocumentPreparedBy ?? "")
                : _preparedByNameOverride;
            set
            {
                if (_preparedByNameOverride == value) return;
                _preparedByNameOverride = value;
                OnPropertyChanged();
            }
        }

        private string _preparedByDept = "";
        public string PreparedByDept
        {
            get => _preparedByDept;
            set
            {
                if (_preparedByDept == value) return;
                _preparedByDept = value;
                OnPropertyChanged();
            }
        }

        public string PreparedByStatus
        {
            get => string.IsNullOrWhiteSpace(Company?.ProformaStatus)
                ? "Draft"
                : Company.ProformaStatus;
            set
            {
                string next = string.IsNullOrWhiteSpace(value)
                    ? "Draft"
                    : value.Trim();

                if (string.Equals(Company?.ProformaStatus, next,
                        StringComparison.OrdinalIgnoreCase))
                    return;

                if (Company != null)
                    Company.ProformaStatus = next;

                OnPropertyChanged();
            }
        }

        private string _termsText =
            "Standard warranties apply to manufactured glass products upon POD signature clearance.";
        public string TermsText
        {
            get => _termsText;
            set
            {
                if (_termsText == value) return;
                _termsText = value;
                OnPropertyChanged();
            }
        }

        private string _footerDisclaimer =
            "Confidential Document generated automatically by ProGlass Automation ERP platform.";
        public string FooterDisclaimer
        {
            get => _footerDisclaimer;
            set
            {
                if (_footerDisclaimer == value) return;
                _footerDisclaimer = value;
                OnPropertyChanged();
            }
        }

        // ── Commands ──────────────────────────────────────────────────────

        public ICommand PrintCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand SaveInvoiceCommand { get; }
        public ICommand LoadInvoiceCommand { get; }
        public ICommand SendEmailCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportCsvCommand { get; }

        public FrameworkElement PrintTarget { get; set; }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE B — Step 9 Profit & Loss link
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Step 9 Profit &amp; Loss view-model, assigned by the wizard shell
        /// after both step VMs exist. Optional and nullable: the printable
        /// invoice renders exactly as before when it is null — the mapper
        /// simply persists an empty P&amp;L block in that case.
        /// </summary>
        public ProfitLossStepViewModel? ProfitLoss
        {
            get => _profitLoss;
            set
            {
                if (ReferenceEquals(_profitLoss, value)) return;
                _profitLoss = value;
                OnPropertyChanged();
            }
        }
        private ProfitLossStepViewModel? _profitLoss;

        /// <summary>
        /// Optional link back to Step 4 so save/load can persist the active
        /// pricing-editor state without making the printable VM own Step 4.
        /// </summary>
        public PricingEstimatorViewModel? Step4Pricing { get; set; }

        /// <summary>
        /// Furthest wizard step the operator reached. Set by the wizard
        /// shell (ProformaInvoiceViewModel) before every save so the
        /// snapshot can persist step-completion progress. Zero means
        /// "not set" — the mapper treats it as step 1 on restore.
        /// </summary>
        public int FurthestStepReached { get; set; }

        /// <summary>
        /// CHAT 15: Parameter order corrected.
        /// Was: (company, ProjectSpecsViewModel project, ClientProfileViewModel client, ...)
        /// Now: (company, ClientProfileViewModel client, ProjectSpecsViewModel project, ...)
        /// Matches PrintPreviewViewModel which receives Step2VM=Client, Step3VM=Project.
        /// </summary>
                private readonly IDialogService _dialogs = null!;
        private readonly IProformaInvoiceRepository _invoices = null!;

public PrintableInvoiceViewModel(
            IDialogService? dialogs,
            IProformaInvoiceRepository? invoices,
            CompanySetupViewModel company,
            ClientProfileViewModel client,        // FIXED: was ProjectSpecsViewModel
            ProjectSpecsViewModel project,       // FIXED: was ClientProfileViewModel
            DataGridViewModel dataGrid,
            OtherChargesViewModel otherCharges,
            InvoiceMainSummaryViewModel mainSummary,
            TermsAndConditionsViewModel terms)
        {
            _dialogs = dialogs!;
            _invoices = invoices!;
            Company = company ?? throw new ArgumentNullException(nameof(company));
            Client = client ?? throw new ArgumentNullException(nameof(client));
            Project = project ?? throw new ArgumentNullException(nameof(project));
            DataGrid = dataGrid ?? throw new ArgumentNullException(nameof(dataGrid));
            OtherCharges = otherCharges ?? throw new ArgumentNullException(nameof(otherCharges));
            MainSummary = mainSummary ?? throw new ArgumentNullException(nameof(mainSummary));
            Terms = terms ?? throw new ArgumentNullException(nameof(terms));

            // BUG-60: all handlers now wrapped safely
            PrintCommand = new RelayCommand(_ => ExecutePrint());
            ExportPdfCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(ExecuteExportPdfAsync, "PI Step7"));
            SaveInvoiceCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(ExecuteSaveInvoiceAsync, "PI Step7"));
            LoadInvoiceCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(ExecuteLoadInvoiceAsync, "PI Step7"));
            SendEmailCommand = new RelayCommand(_ => ShowComingSoon("Send Email", "Phase 7.20"));
            ExportExcelCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(ExecuteExportExcelAsync, "PI Step7"));
            ExportCsvCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(ExecuteExportCsvAsync, "PI Step7"));

            Client.PropertyChanged += OnClientChanged;
            Company.PropertyChanged += OnCompanyChanged;
        }

        // ══════════════════════════════════════════════════════════════════
        //  BUG-60: Safe async fire wrapper
        // ══════════════════════════════════════════════════════════════════

        

        private void OnClientChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ClientProfileViewModel.DocumentPreparedBy)
                && string.IsNullOrWhiteSpace(_preparedByNameOverride))
            {
                OnPropertyChanged(nameof(PreparedByName));
            }
        }

        private void OnCompanyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CompanySetupViewModel.ProformaStatus))
                OnPropertyChanged(nameof(PreparedByStatus));
        }

        // ══════════════════════════════════════════════════════════════════
        //  NATIVE PRINT (BUG-61)
        // ══════════════════════════════════════════════════════════════════

        private void ExecutePrint()
        {
            try
            {
                if (!EnsureCustomerDocumentCalculationsValid("print")) return;

                var dialog = new System.Windows.Controls.PrintDialog();
                if (dialog.ShowDialog() != true) return;

                FixedDocument fixedDoc;
                try
                {
                    fixedDoc = BuildPrintableDocument(dialog);
                }
                catch (Exception buildEx)
                {
                    ShowPrintError("Print build error", buildEx);
                    return;
                }

                try
                {
                    dialog.PrintDocument(
                        fixedDoc.DocumentPaginator,
                        $"Proforma Invoice - {Company.ProformaId}");
                }
                catch (Exception printEx)
                {
                    ShowPrintError("Print send error", printEx);
                }
            }
            catch (Exception ex)
            {
                ShowPrintError("Print error (outer)", ex);
            }
        }

        private void ShowPrintError(string title, Exception ex)
        {
            string msg = $"{title.ToUpper()}:\n\n" +
                         $"Type: {ex.GetType().Name}\n" +
                         $"Message: {ExceptionMessages.Deep(ex)}";

            if (ex.InnerException != null)
                msg += $"\n\nInner: {ex.InnerException.Message}";

            AppLogService.Debug($"[Print] {title}: {ex}");
            _dialogs.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // ══════════════════════════════════════════════════════════════════
        //  SAVE INVOICE (BUG-60)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// AUTO-DRAFT: silent save used when the user leaves the wizard
        /// without pressing SAVE. Never shows dialogs, never blocks
        /// navigation, never throws. Only persists when the wizard
        /// holds meaningful data (client name or at least one row).
        /// </summary>
        public async Task<bool> SilentDraftSaveAsync()
        {
            try
            {
                if (_invoices == null) return false;
                if (string.IsNullOrWhiteSpace(Company?.ProformaId))
                    return false;

                bool meaningful =
                    !string.IsNullOrWhiteSpace(Client?.CustomerName) ||
                    DataGrid?.SpecGroups?.Any(g =>
                        g.ItemRows?.Any(r => r.Qty > 0) == true) == true;
                if (!meaningful) return false;

                var entity = ProformaInvoiceMapper.BuildEntityFromWizard(
                    Company, Client, Project, DataGrid, OtherCharges,
                    MainSummary, Terms, this);

                if (string.IsNullOrWhiteSpace(entity.ProformaId))
                    return false;

                var existing = await _invoices.GetByProformaIdAsync(entity.ProformaId);
                entity.Status = NormalizeSaveOnlyStatus(existing?.Status);
                Company.ProformaStatus = entity.Status;
                OnPropertyChanged(nameof(PreparedByStatus));

                if (existing != null)
                    await _invoices.UpdateAsync(entity);
                else
                    await _invoices.InsertAsync(entity);

                return true;
            }
            catch
            {
                return false; // never block navigation on a draft save
            }
        }

        // ── CROSS-STEP VALIDATION SNAPSHOT ─────────────────────────
        // Builds a snapshot of the current invoice state for the
        // CrossStepValidator. Decoupled from the VMs so the validator
        // can be tested independently.
        // ────────────────────────────────────────────────────────────
        private ProGlassAutomation.Services.ProformaInvoice.Validation.InvoiceValidationSnapshot
            BuildValidationSnapshot()
        {
            return new ProGlassAutomation.Services.ProformaInvoice.Validation.InvoiceValidationSnapshot
            {
                // Step 1: Company
                ProformaId = Company?.ProformaId,
                CompanyName = Company?.CompanyName,
                IssueDate = Company?.IssueDate ?? DateTime.Now,
                ValidUntil = Company?.ValidUntil ?? DateTime.Now.AddDays(7),

                // Step 2: Client
                CustomerName = Client?.CustomerName,

                // Step 5: Data grid
                SpecGroupCount = DataGrid?.SpecGroups?.Count ?? 0,
                TotalSqm = DataGrid?.SummaryViewModel?.GrandTotalSqm ?? 0m,
                TotalPieces = DataGrid?.SummaryViewModel?.GrandTotalQty ?? 0m,
                TotalAmount = DataGrid?.SummaryViewModel?.GrandTotalValue ?? 0m,

                // Step 6: Other charges
                TotalOtherCharges = OtherCharges?.TotalOtherCharges ?? 0m,
                ActiveChargeRows = OtherCharges?.ChargeRows?.Count(r => r.IsActive) ?? 0,
                HasWastageRows = OtherCharges?.WastagesSection?.WastageRows?.Any(r => r.IsActive) ?? false,

                // Step 7: Summary — include the full waterfall so save can
                // fail closed if a stale snapshot or missed recalculation ever
                // makes Step 7 disagree with Step 5/6 or VAT law.
                SummaryGlassMaterialsSubtotal = MainSummary?.GlassMaterialsSubtotal ?? 0m,
                SummaryOtherChargesSubtotal = MainSummary?.OtherChargesSubtotal ?? 0m,
                NetSubtotal = MainSummary?.NetSubtotal ?? 0m,
                DiscountAmount = MainSummary?.DiscountAmount ?? 0m,
                SubtotalBeforeVat = MainSummary?.SubtotalBeforeVat ?? 0m,
                GrandTotal = MainSummary?.GrandTotalVatInclusive ?? 0m,
                VatAmount = MainSummary?.VatAmount ?? 0m,
                VatPercent = MainSummary?.VatPercent ?? 0m,
                IsVatApplicable = MainSummary?.IsVatApplicable ?? false,
                DiscountPercent = (MainSummary?.NetSubtotal ?? 0m) > 0m
                    ? Math.Round((MainSummary?.DiscountAmount ?? 0m) /
                        (MainSummary?.NetSubtotal ?? 1m) * 100m, 2)
                    : 0m,

                // Step 9: Profit & Loss
                ProfitMarginPercent = ProfitLoss?.MarginPercent ?? 0m,
                TotalCost = (ProfitLoss?.TotalGlassCost ?? 0m) + (ProfitLoss?.TotalProductionCost ?? 0m),
                TotalRevenue = ProfitLoss?.GrossVatInclusive ?? 0m
            };
        }
        // ── END CROSS-STEP VALIDATION SNAPSHOT ─────────────────────

        /// <summary>
        /// Customer-facing output gate. Printing/exporting without this check
        /// is as dangerous as saving a bad invoice: it can send a wrong total
        /// to a customer before anyone notices. Errors block; warnings require
        /// explicit operator confirmation.
        /// </summary>
        private bool EnsureCustomerDocumentCalculationsValid(string operationLabel)
        {
            var validationResult = ProGlassAutomation.Services.ProformaInvoice
                .Validation.CrossStepValidator.Validate(BuildValidationSnapshot());

            if (!validationResult.IsValid)
            {
                _dialogs.Show(
                    $"Cannot {operationLabel} until these issues are fixed:\n\n" +
                    string.Join("\n\n", validationResult.Errors),
                    "Calculation Validation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (validationResult.HasWarnings)
            {
                var proceed = _dialogs.Show(
                    $"Review these warnings before {operationLabel}:\n\n" +
                    string.Join("\n\n", validationResult.Warnings) +
                    "\n\nProceed anyway?",
                    "Calculation Warnings",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                return proceed == MessageBoxResult.Yes;
            }

            return true;
        }

        private async Task ExecuteSaveInvoiceAsync()
        {
            if (_invoices == null)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.InvoiceDatabaseIsNotInitialized"),
                    LocalizationService.Instance.T("Msg.CapSaveFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(Company.ProformaId))
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.CannotSaveProformaIDIs"),
                    LocalizationService.Instance.T("Msg.CapSaveFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ── CROSS-STEP VALIDATION ──────────────────────────────
            // Before saving, validate business rules that span multiple
            // steps. Errors block the save; warnings show a confirm dialog.
            try
            {
                var snapshot = BuildValidationSnapshot();
                var validationResult = ProGlassAutomation.Services.ProformaInvoice
                    .Validation.CrossStepValidator.Validate(snapshot);

                if (!validationResult.IsValid)
                {
                    string errorMsg = string.Join("\n\n", validationResult.Errors);
                    _dialogs.Show(
                        "Please fix the following issues before saving:\n\n" + errorMsg,
                        "Validation Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (validationResult.HasWarnings)
                {
                    string warnMsg = string.Join("\n\n", validationResult.Warnings);
                    var proceed = _dialogs.Show(
                        "The following warnings were detected:\n\n" + warnMsg +
                        "\n\nDo you want to proceed with saving?",
                        "Validation Warnings",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (proceed != MessageBoxResult.Yes)
                        return;
                }
            }
            catch (Exception valEx)
            {
                // Fail closed: if the industrial validator itself cannot
                // evaluate the invoice, saving/exporting a potentially wrong
                // financial document is riskier than asking the operator to
                // retry after fixing the underlying state.
                ProGlassAutomation.Services.Logging.AppLogService.Debug(
                    $"[PI Save] Cross-step validation error: {ExceptionMessages.Deep(valEx)}");
                _dialogs.Show(
                    "Unable to validate invoice calculations. Save is blocked to prevent an incorrect customer document. Reopen the invoice or recalculate the affected steps, then try again.",
                    "Validation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            // ── END CROSS-STEP VALIDATION ──────────────────────────

            // ── DUPLICATE DETECTION ────────────────────────────────
            // Check if company/client/project already exist in master data.
            // Show a warning so the user knows existing records will be updated.
            try
            {
                var masterData = App.ServiceProvider?.GetService<IMasterDataRepository>();
                if (masterData != null)
                {
                    var duplicates = new List<string>();

                    // Check company
                    if (!string.IsNullOrWhiteSpace(Company.CompanyName))
                    {
                        var existingCompany = await masterData.FindCompanyByNameAsync(Company.CompanyName);
                        if (existingCompany != null)
                        {
                            duplicates.Add($"• Company '{Company.CompanyName}' already exists and will be updated");
                        }
                    }

                    // Check client
                    if (!string.IsNullOrWhiteSpace(Client.CustomerName))
                    {
                        var existingClient = await masterData.FindClientByNameAsync(Client.CustomerName);
                        if (existingClient != null)
                        {
                            duplicates.Add($"• Client '{Client.CustomerName}' already exists and will be updated");
                        }
                    }

                    // Check project
                    if (!string.IsNullOrWhiteSpace(Project.ProjectIdentifierName) && 
                        !string.IsNullOrWhiteSpace(Client.CustomerName))
                    {
                        var existingProject = await masterData.FindProjectByNameAsync(
                            Project.ProjectIdentifierName, Client.CustomerName);
                        if (existingProject != null)
                        {
                            duplicates.Add($"• Project '{Project.ProjectIdentifierName}' already exists and will be updated");
                        }
                    }

                    // Show duplicate warning if any found
                    if (duplicates.Count > 0)
                    {
                        string duplicateMsg = "The following master data records already exist:\n\n" +
                            string.Join("\n", duplicates) +
                            "\n\nSaving will UPDATE these existing records with the new information.\n\n" +
                            "Do you want to proceed?";

                        var proceedWithDuplicates = _dialogs.Show(
                            duplicateMsg,
                            "Duplicate Records Detected",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (proceedWithDuplicates != MessageBoxResult.Yes)
                            return;
                    }
                }
            }
            catch (Exception dupEx)
            {
                // Duplicate detection failed — log but don't block save.
                // The upsert logic in the repository will handle duplicates correctly.
                ProGlassAutomation.Services.Logging.AppLogService.Debug(
                    $"[PI Save] Duplicate detection error: {ExceptionMessages.Deep(dupEx)}");
            }
            // ── END DUPLICATE DETECTION ────────────────────────────

            // v2 permission gate — the operation itself verifies access.
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.ProformaCreate,
                "save a proforma invoice"))
                return;

            // CHAT 15: BuildEntityFromWizard now expects (company, client, project, ...)
            var entity = ProformaInvoiceMapper.BuildEntityFromWizard(
                Company, Client, Project, DataGrid, OtherCharges,
                MainSummary, Terms, this);

            var existing = await _invoices.GetByProformaIdAsync(entity.ProformaId);
            entity.Status = NormalizeSaveOnlyStatus(existing?.Status);
            Company.ProformaStatus = entity.Status;
            OnPropertyChanged(nameof(PreparedByStatus));

            bool alreadyExists = existing != null;

            if (alreadyExists)
            {
                string suggestedId =
                    await _invoices.SuggestNextIdAsync(entity.ProformaId);

                var result = _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.InvoiceWithIDXAlready"), entity.ProformaId, suggestedId),
                    LocalizationService.Instance.T("Msg.CapDuplicateProformaID"),
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel) return;

                if (result == MessageBoxResult.Yes)
                {
                    if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                        ProGlassAutomation.Services.Audit.AppPermissions.ProformaEdit,
                        "update an existing invoice"))
                        return;

                    await _invoices.UpdateAsync(entity);
                    await StampSaveArtifactsAsync(entity, isUpdate: true);
                    ShowSaveSuccess(entity.ProformaId, isUpdate: true);
                    return;
                }

                entity.ProformaId = suggestedId;
                entity.Status = NormalizeSaveOnlyStatus(null);
                Company.ProformaId = suggestedId;
                Company.ProformaStatus = entity.Status;
                OnPropertyChanged(nameof(PreparedByStatus));
                await _invoices.InsertAsync(entity);
                await StampSaveArtifactsAsync(entity, isUpdate: false);
                ShowSaveSuccess(entity.ProformaId, isUpdate: false);
                return;
            }

            await _invoices.InsertAsync(entity);
            await StampSaveArtifactsAsync(entity, isUpdate: false);
            ShowSaveSuccess(entity.ProformaId, isUpdate: false);
        }

        private string NormalizeSaveOnlyStatus(string? currentStoredStatus)
        {
            string current = NormalizeWorkflowStatus(currentStoredStatus);
            if (string.Equals(current, "Approved", StringComparison.OrdinalIgnoreCase))
                return "Approved";

            string requested = NormalizeWorkflowStatus(Company?.ProformaStatus);
            if (string.Equals(requested, "Approved", StringComparison.OrdinalIgnoreCase))
                return current;

            return requested;
        }

        private static string NormalizeWorkflowStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return "Draft";

            return status.Trim();
        }

        /// <summary>
        /// v2: after every successful save — record created/updated
        /// metadata and append a restorable version snapshot.
        /// Never allowed to break the save itself.
        /// </summary>
        private static async Task StampSaveArtifactsAsync(
            SavedProformaInvoice entity, bool isUpdate)
        {
            try
            {
                if (isUpdate)
                    await RecordMetadataService.StampUpdatedAsync(
                        "PI", entity.ProformaId);
                else
                    await RecordMetadataService.StampCreatedAsync(
                        "PI", entity.ProformaId);

                int version = await VersionHistoryService.AddVersionAsync(
                    "PI", entity.ProformaId,
                    isUpdate ? "Invoice updated" : "Invoice created",
                    entity.FullSnapshotJson);

                AppLogService.Debug(
                    $"[PI] Version {version} stored for {entity.ProformaId}");
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "[PI] Save version-history artifact failed", ex);
            }
        }

        private void ShowSaveSuccess(string proformaId, bool isUpdate)
        {
            // Single audit point — all three save paths funnel through
            // here, so one line covers create AND update.
            AuditService.Log(
                AuditSections.Proforma,
                isUpdate ? "Invoice updated" : "Invoice saved",
                proformaId, "PI", proformaId);

            string verb = isUpdate ? "updated" : "saved";
            _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.InvoiceXWasYSuccessfully"), proformaId, verb),
                LocalizationService.Instance.T("Msg.CapSaveSuccessful"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOAD INVOICE (BUG-60/62)
        // ══════════════════════════════════════════════════════════════════

        private async Task ExecuteLoadInvoiceAsync()
        {
            if (_invoices == null)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.InvoiceDatabaseIsNotInitialized"),
                    LocalizationService.Instance.T("Msg.CapLoadFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool hasCurrentData =
                !string.IsNullOrWhiteSpace(Company.CompanyName) ||
                !string.IsNullOrWhiteSpace(Client.CustomerName) ||
                DataGrid.SpecGroups.Any(g => g.ItemRows.Count > 0);

            if (hasCurrentData)
            {
                var choice = _dialogs.Show(
                    LocalizationService.Instance.T("Msg.LoadingASavedInvoiceWill"),
                    LocalizationService.Instance.T("Inquiry.UnsavedChanges"),
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (choice == MessageBoxResult.Cancel) return;
                if (choice == MessageBoxResult.Yes)
                    await ExecuteSaveInvoiceAsync();
            }

            var dialog = _dialogs.Create<SavedInvoicesListDialog>();

            bool? ok = dialog.ShowDialog();
            if (ok != true || dialog.SelectedInvoiceId <= 0) return;

            var entity = await _invoices.GetByIdAsync(dialog.SelectedInvoiceId);
            if (entity == null)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.SelectedInvoiceCouldNotBe"),
                    LocalizationService.Instance.T("Msg.CapLoadFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);

            MainSummary.BeginBatchUpdate();
            try
            {
                // CHAT 15: RestoreWizardFromSnapshot now expects (snap, company, client, project, ...)
                ProformaInvoiceMapper.RestoreWizardFromSnapshot(
                    snapshot,
                    Company, Client, Project, DataGrid, OtherCharges,
                    MainSummary, Terms, this);
            }
            finally
            {
                MainSummary.EndBatchUpdate();
            }

            _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.InvoiceXLoadedSuccessfullyAll"), entity.ProformaId),
                LocalizationService.Instance.T("Msg.CapLoadSuccessful"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ══════════════════════════════════════════════════════════════════
        //  EXPORT COMMANDS (BUG-60)
        // ══════════════════════════════════════════════════════════════════

        private async Task ExecuteExportPdfAsync()
        {
            if (!EnsureExportable("PDF")) return;

            // CHAT 15: BuildEntityFromWizard param order corrected
            var entity = ProformaInvoiceMapper.BuildEntityFromWizard(
                Company, Client, Project, DataGrid, OtherCharges,
                MainSummary, Terms, this);
            var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);

            var layout = new ProformaInvoicePdfLayout(entity, snapshot);
            var engine = new PdfExportEngine();
            var request = new ExportRequest { PromptForLocation = true, OpenAfterSave = true };

            var result = await engine.ExportAsync(layout, request);
            ShowExportFailureIfAny(result, "PDF");
        }

        private async Task ExecuteExportExcelAsync()
        {
            if (!EnsureExportable("Excel")) return;

            var entity = ProformaInvoiceMapper.BuildEntityFromWizard(
                Company, Client, Project, DataGrid, OtherCharges,
                MainSummary, Terms, this);
            var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);

            var layout = new ProformaInvoiceExcelLayout(entity, snapshot);
            var engine = new ExcelExportEngine();
            var request = new ExportRequest { PromptForLocation = true, OpenAfterSave = true };

            var result = await engine.ExportAsync(layout, request);
            ShowExportFailureIfAny(result, "Excel");
        }

        private async Task ExecuteExportCsvAsync()
        {
            if (!EnsureExportable("CSV")) return;

            var entity = ProformaInvoiceMapper.BuildEntityFromWizard(
                Company, Client, Project, DataGrid, OtherCharges,
                MainSummary, Terms, this);
            var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);

            var layout = new ProformaInvoiceCsvLayout(entity, snapshot);
            var engine = new CsvExportEngine();
            var request = new ExportRequest { PromptForLocation = true, OpenAfterSave = true };

            var result = await engine.ExportAsync(layout, request);
            ShowExportFailureIfAny(result, "CSV");
        }

        private bool EnsureExportable(string formatLabel)
        {
            if (string.IsNullOrWhiteSpace(Company.ProformaId))
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.CannotExportProformaIDIs"),
                    LocalizationService.Instance.T("Msg.CapXExportFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            return EnsureCustomerDocumentCalculationsValid($"export {formatLabel}");
        }

        private void ShowExportFailureIfAny(ExportResult result, string formatLabel)
        {
            if (result.Success || result.Cancelled) return;
            _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.XExportFailedY"), formatLabel, result.ErrorMessage),
                LocalizationService.Instance.T("Msg.CapExportFailed"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        // ══════════════════════════════════════════════════════════════════
        //  LAYOUT CONSTANTS & CACHED RESOURCES
        // ══════════════════════════════════════════════════════════════════

        private const double PAGE_MARGIN = 40;
        private const double SECTION_GAP = 10;
        private const string FONT_FAMILY_NAME = "Segoe UI";

        // BUG-66: cached FontFamily
        private static readonly FontFamily _fontFamily =
            new FontFamily(FONT_FAMILY_NAME);

        // BUG-68: frozen brushes
        private static readonly Brush AccentBrush =
            FreezeBrush(new SolidColorBrush(Color.FromRgb(0x4F, 0x46, 0xE5)));
        private static readonly Brush DarkBrush =
            FreezeBrush(new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)));
        private static readonly Brush MutedBrush =
            FreezeBrush(new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)));
        private static readonly Brush LineBrush =
            FreezeBrush(new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)));
        private static readonly Brush LightGrayBrush =
            FreezeBrush(new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)));
        private static readonly Brush TableHeaderBrush =
            FreezeBrush(new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)));
        private static readonly Brush GreenBrush =
            FreezeBrush(new SolidColorBrush(Color.FromRgb(0x04, 0x78, 0x57)));
        private static readonly Brush RedBrush =
            FreezeBrush(new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C)));

        private static Brush FreezeBrush(SolidColorBrush b)
        {
            if (b.CanFreeze) b.Freeze();
            return b;
        }

        // ══════════════════════════════════════════════════════════════════
        //  PRINT DOC BUILDER (BUG-63/64)
        // ══════════════════════════════════════════════════════════════════

        private FixedDocument BuildPrintableDocument(
            System.Windows.Controls.PrintDialog dialog)
        {
            double pageWidth = dialog.PrintableAreaWidth;
            double pageHeight = dialog.PrintableAreaHeight;
            double contentWidth = pageWidth - (PAGE_MARGIN * 2);

            var doc = new FixedDocument();
            doc.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);

            // MEASUREMENT PASS
            var measureContent = BuildFullContentStack(contentWidth);
            ApplyVectorTextRendering(measureContent);
            measureContent.Measure(new Size(contentWidth, double.PositiveInfinity));
            measureContent.Arrange(new Rect(
                0, 0, contentWidth, measureContent.DesiredSize.Height));
            measureContent.UpdateLayout();

            double totalHeight = measureContent.DesiredSize.Height;
            double usableHeight = pageHeight - (PAGE_MARGIN * 2);
            int totalPages = Math.Max(1,
                (int)Math.Ceiling(totalHeight / usableHeight));

            AppLogService.Debug(
                $"[PRINT] totalHeight={totalHeight}, " +
                $"usableHeight={usableHeight}, totalPages={totalPages}");

            for (int i = 0; i < totalPages; i++)
            {
                double sliceY = i * usableHeight;

                var pageContent = BuildFullContentStack(contentWidth);
                ApplyVectorTextRendering(pageContent);
                pageContent.Measure(new Size(contentWidth, double.PositiveInfinity));
                pageContent.Arrange(new Rect(
                    0, 0, contentWidth, pageContent.DesiredSize.Height));
                pageContent.UpdateLayout();

                var clipCanvas = new Canvas
                {
                    Width = contentWidth,
                    Height = usableHeight,
                    ClipToBounds = true
                };
                Canvas.SetLeft(pageContent, 0);
                Canvas.SetTop(pageContent, -sliceY);
                clipCanvas.Children.Add(pageContent);

                var fixedPage = new FixedPage
                {
                    Width = pageWidth,
                    Height = pageHeight,
                    Background = Brushes.White
                };
                FixedPage.SetLeft(clipCanvas, PAGE_MARGIN);
                FixedPage.SetTop(clipCanvas, PAGE_MARGIN);
                fixedPage.Children.Add(clipCanvas);

                fixedPage.Measure(new Size(pageWidth, pageHeight));
                fixedPage.Arrange(new Rect(0, 0, pageWidth, pageHeight));
                fixedPage.UpdateLayout();

                var pageWrap = new PageContent();
                ((IAddChild)pageWrap).AddChild(fixedPage);
                doc.Pages.Add(pageWrap);
            }

            return doc;
        }

        private StackPanel BuildFullContentStack(double width)
        {
            var root = new StackPanel { Width = width, Background = Brushes.White };

            root.Children.Add(BuildCompanyHeader());
            root.Children.Add(BuildDivider(top: 8, bottom: 8));
            root.Children.Add(BuildClientProjectBlock(width));
            root.Children.Add(BuildDivider(top: 8, bottom: 8));

            foreach (var group in DataGrid.SpecGroups)
                root.Children.Add(BuildSpecTable(group));

            if (DataGrid.SpecGroups.Count > 0)
                root.Children.Add(BuildGrandSpecSummary());

            root.Children.Add(BuildOtherChargesSection());
            root.Children.Add(BuildFinancialSummary(width));
            root.Children.Add(BuildDivider(top: 10, bottom: 6));
            root.Children.Add(BuildTermsAndConditionsSection());
            root.Children.Add(BuildTermsFooter());
            root.Children.Add(BuildSignatureBlocks(width));

            return root;
        }

        // ══════════════════════════════════════════════════════════════════
        //  ALL RENDER HELPERS — preserved exactly from original
        // ══════════════════════════════════════════════════════════════════

        private Grid BuildCompanyHeader()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(220) });

            var left = new StackPanel();
            left.Children.Add(MakeText(
                string.IsNullOrWhiteSpace(Company.CompanyName)
                    ? "PROGLASS AUTOMATION ERP"
                    : Company.CompanyName,
                18, FontWeights.Bold, AccentBrush));
            left.Children.Add(MakeText(
                "Premium Glass Glazing, High-Yield Manufacturing & Logistics Spec",
                10, FontWeights.Normal, MutedBrush, marginTop: 2));

            var loc = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 1, 0, 0)
            };
            loc.Children.Add(MakeText(
                string.IsNullOrWhiteSpace(Company.CompanyLocation)
                    ? "United Arab Emirates"
                    : Company.CompanyLocation,
                10, FontWeights.Normal, MutedBrush));
            loc.Children.Add(MakeText(
                " • www.proglass-automation.com",
                10, FontWeights.Normal, MutedBrush));
            left.Children.Add(loc);

            if (!string.IsNullOrWhiteSpace(Company.CompanyTrn))
            {
                var trn = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                trn.Children.Add(MakeText("TRN: ", 10, FontWeights.SemiBold, MutedBrush));
                trn.Children.Add(MakeText(
                    Company.CompanyTrn, 10, FontWeights.SemiBold, DarkBrush));
                left.Children.Add(trn);
            }

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            var badge = new Border
            {
                Background = LightGrayBrush,
                BorderBrush = LineBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(12, 8, 12, 8)
            };
            var badgeStack = new StackPanel();
            badgeStack.Children.Add(MakeText(
                "PROFORMA INVOICE", 12, FontWeights.Bold, DarkBrush,
                horizontalAlignment: HorizontalAlignment.Center));

            var refGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            refGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = GridLength.Auto });
            refGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            refGrid.RowDefinitions.Add(new RowDefinition());
            refGrid.RowDefinitions.Add(new RowDefinition());

            var refLbl = MakeText("Ref:", 10, FontWeights.Normal, MutedBrush, marginRight: 6);
            Grid.SetRow(refLbl, 0); Grid.SetColumn(refLbl, 0);
            refGrid.Children.Add(refLbl);

            var refVal = MakeText(
                Company.ProformaId ?? "—", 10, FontWeights.SemiBold, DarkBrush);
            Grid.SetRow(refVal, 0); Grid.SetColumn(refVal, 1);
            refGrid.Children.Add(refVal);

            var dateLbl = MakeText(
                "Date:", 10, FontWeights.Normal, MutedBrush,
                marginRight: 6, marginTop: 2);
            Grid.SetRow(dateLbl, 1); Grid.SetColumn(dateLbl, 0);
            refGrid.Children.Add(dateLbl);

            var dateVal = MakeText(
                Company.IssueDate.ToString("yyyy-MM-dd"),
                10, FontWeights.SemiBold, DarkBrush, marginTop: 2);
            Grid.SetRow(dateVal, 1); Grid.SetColumn(dateVal, 1);
            refGrid.Children.Add(dateVal);

            badgeStack.Children.Add(refGrid);
            badge.Child = badgeStack;

            Grid.SetColumn(badge, 1);
            grid.Children.Add(badge);

            return grid;
        }
    }
}
