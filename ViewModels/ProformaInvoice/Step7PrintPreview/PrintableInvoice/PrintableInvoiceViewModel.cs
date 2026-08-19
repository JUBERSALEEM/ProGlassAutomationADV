// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceViewModel.cs
// ═══════════════════════════════════════════════════════════════════════
// CHAT 15 — NAMESPACE FIX
//   using Step2ProjectSpecs → Step2ClientProfile
//   using Step3ClientProfile → Step3ProjectSpecs
//   Constructor param order: (company, CLIENT, PROJECT, ...)
//     was: (company, ProjectSpecsViewModel, ClientProfileViewModel, ...)
//     now: (company, ClientProfileViewModel, ProjectSpecsViewModel, ...)
//   Property types updated to new namespaces
//   ALL print/render/export logic preserved exactly — zero changes
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Win32;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.ProformaInvoice.Export;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.ProformaInvoice.SavedInvoicesList;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step1CompanySetup;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step2ClientProfile;   // FIXED: was Step2ProjectSpecs
using ProGlassAutomation.ViewModels.ProformaInvoice.Step3ProjectSpecs;    // FIXED: was Step3ClientProfile
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

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.PrintableInvoice
{
    /// <summary>
    /// PrintableInvoice ViewModel — hosts the A4 preview, print engine,
    /// save/load coordinator, and PDF/Excel/CSV export coordinators.
    ///
    /// BUG-60 [CRITICAL]: async void handlers replaced with SafeFireAsync.
    /// BUG-61 [HIGH]: Print errors simplified.
    /// BUG-62 [HIGH]: LoadInvoice uses SafeFireAsync.
    /// BUG-63 [HIGH]: BuildPrintableDocument measurement pass documented.
    /// BUG-64 [MEDIUM]: ApplyVectorTextRendering called once on root.
    /// BUG-65 [MEDIUM]: File split deferred.
    /// BUG-66 [LOW]: FontFamily instance cached.
    /// BUG-68 [COSMETIC]: Static brushes Frozen() for perf.
    /// CHAT 15: Namespace fix — Step2=Client, Step3=Project.
    /// </summary>
    public class PrintableInvoiceViewModel : ViewModelBase
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

        private string _preparedByStatusOverride;
        public string PreparedByStatus
        {
            get => string.IsNullOrWhiteSpace(_preparedByStatusOverride)
                ? (Company?.ProformaStatus ?? "Draft")
                : _preparedByStatusOverride;
            set
            {
                if (_preparedByStatusOverride == value) return;
                _preparedByStatusOverride = value;
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
        /// CHAT 15: Parameter order corrected.
        /// Was: (company, ProjectSpecsViewModel project, ClientProfileViewModel client, ...)
        /// Now: (company, ClientProfileViewModel client, ProjectSpecsViewModel project, ...)
        /// Matches PrintPreviewViewModel which receives Step2VM=Client, Step3VM=Project.
        /// </summary>
        public PrintableInvoiceViewModel(
            CompanySetupViewModel company,
            ClientProfileViewModel client,        // FIXED: was ProjectSpecsViewModel
            ProjectSpecsViewModel project,       // FIXED: was ClientProfileViewModel
            DataGridViewModel dataGrid,
            OtherChargesViewModel otherCharges,
            InvoiceMainSummaryViewModel mainSummary,
            TermsAndConditionsViewModel terms)
        {
            Company = company ?? throw new ArgumentNullException(nameof(company));
            Client = client ?? throw new ArgumentNullException(nameof(client));
            Project = project ?? throw new ArgumentNullException(nameof(project));
            DataGrid = dataGrid ?? throw new ArgumentNullException(nameof(dataGrid));
            OtherCharges = otherCharges ?? throw new ArgumentNullException(nameof(otherCharges));
            MainSummary = mainSummary ?? throw new ArgumentNullException(nameof(mainSummary));
            Terms = terms ?? throw new ArgumentNullException(nameof(terms));

            // BUG-60: all handlers now wrapped safely
            PrintCommand = new RelayCommand(_ => ExecutePrint());
            ExportPdfCommand = new RelayCommand(_ => SafeFireAsync(ExecuteExportPdfAsync));
            SaveInvoiceCommand = new RelayCommand(_ => SafeFireAsync(ExecuteSaveInvoiceAsync));
            LoadInvoiceCommand = new RelayCommand(_ => SafeFireAsync(ExecuteLoadInvoiceAsync));
            SendEmailCommand = new RelayCommand(_ => ShowComingSoon("Send Email", "Phase 7.20"));
            ExportExcelCommand = new RelayCommand(_ => SafeFireAsync(ExecuteExportExcelAsync));
            ExportCsvCommand = new RelayCommand(_ => SafeFireAsync(ExecuteExportCsvAsync));

            Client.PropertyChanged += OnClientChanged;
            Company.PropertyChanged += OnCompanyChanged;
        }

        // ══════════════════════════════════════════════════════════════════
        //  BUG-60: Safe async fire wrapper
        // ══════════════════════════════════════════════════════════════════

        private static async void SafeFireAsync(Func<Task> action)
        {
            try { await action(); }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unexpected error:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

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
            if (e.PropertyName == nameof(CompanySetupViewModel.ProformaStatus)
                && string.IsNullOrWhiteSpace(_preparedByStatusOverride))
            {
                OnPropertyChanged(nameof(PreparedByStatus));
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  NATIVE PRINT (BUG-61)
        // ══════════════════════════════════════════════════════════════════

        private void ExecutePrint()
        {
            try
            {
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

        private static void ShowPrintError(string title, Exception ex)
        {
            string msg = $"{title.ToUpper()}:\n\n" +
                         $"Type: {ex.GetType().Name}\n" +
                         $"Message: {ex.Message}";

            if (ex.InnerException != null)
                msg += $"\n\nInner: {ex.InnerException.Message}";

            Debug.WriteLine($"[Print] {title}: {ex}");
            MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
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
                if (App.Invoices == null) return false;
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

                if (await App.Invoices.ExistsByProformaIdAsync(entity.ProformaId))
                    await App.Invoices.UpdateAsync(entity);
                else
                    await App.Invoices.InsertAsync(entity);

                return true;
            }
            catch
            {
                return false; // never block navigation on a draft save
            }
        }

        private async Task ExecuteSaveInvoiceAsync()
        {
            if (App.Invoices == null)
            {
                MessageBox.Show(
                    "Invoice database is not initialized. Please restart the app.",
                    "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(Company.ProformaId))
            {
                MessageBox.Show(
                    "Cannot save — Proforma ID is empty.\n\n" +
                    "Please set a Proforma ID in Step 1 (Company Details) before saving.",
                    "Save Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // v2 permission gate — the operation itself verifies access.
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.ProformaCreate,
                "save a proforma invoice"))
                return;

            // CHAT 15: BuildEntityFromWizard now expects (company, client, project, ...)
            var entity = ProformaInvoiceMapper.BuildEntityFromWizard(
                Company, Client, Project, DataGrid, OtherCharges,
                MainSummary, Terms, this);

            bool alreadyExists =
                await App.Invoices.ExistsByProformaIdAsync(entity.ProformaId);

            if (alreadyExists)
            {
                string suggestedId =
                    await App.Invoices.SuggestNextIdAsync(entity.ProformaId);

                var result = MessageBox.Show(
                    $"An invoice with ID '{entity.ProformaId}' already exists.\n\n" +
                    $"• YES → Update the existing invoice (overwrite)\n" +
                    $"• NO → Save as new invoice with ID '{suggestedId}'\n" +
                    $"• CANCEL → Return to editing without saving",
                    "Duplicate Proforma ID",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel) return;

                if (result == MessageBoxResult.Yes)
                {
                    if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                        ProGlassAutomation.Services.Audit.AppPermissions.ProformaEdit,
                        "update an existing invoice"))
                        return;

                    await App.Invoices.UpdateAsync(entity);
                    await StampSaveArtifactsAsync(entity, isUpdate: true);
                    ShowSaveSuccess(entity.ProformaId, isUpdate: true);
                    return;
                }

                entity.ProformaId = suggestedId;
                Company.ProformaId = suggestedId;
                await App.Invoices.InsertAsync(entity);
                await StampSaveArtifactsAsync(entity, isUpdate: false);
                ShowSaveSuccess(entity.ProformaId, isUpdate: false);
                return;
            }

            await App.Invoices.InsertAsync(entity);
            await StampSaveArtifactsAsync(entity, isUpdate: false);
            ShowSaveSuccess(entity.ProformaId, isUpdate: false);
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

                Debug.WriteLine(
                    $"[PI] Version {version} stored for {entity.ProformaId}");
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "[PI] Save version-history artifact failed", ex);
            }
        }

        private static void ShowSaveSuccess(string proformaId, bool isUpdate)
        {
            // Single audit point — all three save paths funnel through
            // here, so one line covers create AND update.
            AuditService.Log(
                AuditSections.Proforma,
                isUpdate ? "Invoice updated" : "Invoice saved",
                proformaId, "PI", proformaId);

            string verb = isUpdate ? "updated" : "saved";
            MessageBox.Show(
                $"Invoice '{proformaId}' was {verb} successfully.\n\n" +
                "You can load it later using the LOAD INVOICE button.",
                "Save Successful",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOAD INVOICE (BUG-60/62)
        // ══════════════════════════════════════════════════════════════════

        private async Task ExecuteLoadInvoiceAsync()
        {
            if (App.Invoices == null)
            {
                MessageBox.Show(
                    "Invoice database is not initialized. Please restart the app.",
                    "Load Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool hasCurrentData =
                !string.IsNullOrWhiteSpace(Company.CompanyName) ||
                !string.IsNullOrWhiteSpace(Client.CustomerName) ||
                DataGrid.SpecGroups.Any(g => g.ItemRows.Count > 0);

            if (hasCurrentData)
            {
                var choice = MessageBox.Show(
                    "Loading a saved invoice will replace your current wizard data.\n\n" +
                    "• YES → Save current invoice first, then load\n" +
                    "• NO → Discard current data and load\n" +
                    "• CANCEL → Return to editing",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (choice == MessageBoxResult.Cancel) return;
                if (choice == MessageBoxResult.Yes)
                    await ExecuteSaveInvoiceAsync();
            }

            var dialog = new SavedInvoicesListDialog
            {
                Owner = Application.Current.MainWindow
            };

            bool? ok = dialog.ShowDialog();
            if (ok != true || dialog.SelectedInvoiceId <= 0) return;

            var entity = await App.Invoices.GetByIdAsync(dialog.SelectedInvoiceId);
            if (entity == null)
            {
                MessageBox.Show(
                    "Selected invoice could not be found.",
                    "Load Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            MessageBox.Show(
                $"Invoice '{entity.ProformaId}' loaded successfully.\n\n" +
                "All wizard steps have been updated. Review each step and make changes as needed.",
                "Load Successful",
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
                MessageBox.Show(
                    $"Cannot export — Proforma ID is empty.\n\n" +
                    "Please set a Proforma ID in Step 1 (Company Details) before exporting.",
                    $"{formatLabel} Export Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private static void ShowExportFailureIfAny(ExportResult result, string formatLabel)
        {
            if (result.Success || result.Cancelled) return;
            MessageBox.Show(
                $"{formatLabel} export failed:\n\n{result.ErrorMessage}",
                "Export Failed",
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

            Debug.WriteLine(
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

        private Grid BuildClientProjectBlock(double width)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // CLIENT block — reads from Client (ClientProfileViewModel)
            var clientStack = new StackPanel();
            clientStack.Children.Add(MakeText(
                "CLIENT / BILL TO:", 10, FontWeights.Bold, DarkBrush, marginBottom: 4));
            AddLabelValueRow(clientStack, "Customer Name: ", Client.CustomerName);
            AddLabelValueRow(clientStack, "TRN/VAT Number: ", Client.CustomerTrn);
            AddLabelValueRow(clientStack, "Attention Name: ", Client.AttentionExecutiveName);
            AddLabelValueRow(clientStack, "Contact Details: ", Client.ClientContactPhone);
            AddLabelValueRow(clientStack, "Billing Address: ", Client.BillingAddress);
            AddLabelValueRow(clientStack, "Client Reference: ", Client.CustomerReferenceCode);

            Grid.SetColumn(clientStack, 0);
            grid.Children.Add(clientStack);

            // PROJECT block — reads from Project (ProjectSpecsViewModel)
            var projStack = new StackPanel();
            projStack.Children.Add(MakeText(
                "PROJECT ESTIMATE METRIC:", 10, FontWeights.Bold, DarkBrush, marginBottom: 4));
            AddLabelValueRow(projStack, "Project: ", Project.ProjectReference);
            AddLabelValueRow(projStack, "Contract No: ", Project.ProjectContractNo);
            AddLabelValueRow(projStack, "Site Delivery: ", Project.SiteDeliveryLocation);
            AddLabelValueRow(projStack, "LPO / PO Reference: ", Project.LpoNumber);
            AddLabelValueRow(projStack, "Assigned Salesman: ", Client.SalesRepresentative);
            AddLabelValueRow(projStack, "Valid Until / Due: ",
                $"{Company.ValidUntil:yyyy-MM-dd} • Status: {Company.ProformaStatus}");

            Grid.SetColumn(projStack, 2);
            grid.Children.Add(projStack);

            return grid;
        }

        private StackPanel BuildSpecTable(SpecGroup group)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, SECTION_GAP) };

            var header = new Border
            {
                Background = AccentBrush,
                Padding = new Thickness(8, 5, 8, 5)
            };
            string specLabel = !string.IsNullOrWhiteSpace(group.SelectedSpecName)
                ? group.SelectedSpecName
                : (group.GroupName ?? $"Spec Group {group.GroupNumber}");
            header.Child = MakeText(
                $"SPECIFICATION {group.GroupNumber}: {specLabel}",
                10, FontWeights.Bold, Brushes.White);
            stack.Children.Add(header);

            var widths = new[] { 24.0, 1.4, 32.0, 32.0, 32.0, 32.0, 32.0, 0.70, 0.75, 0.65, 0.80 };
            var headerLabels = new[]
            {
                "#", "GLASS REF / DESCRIPTION",
                "W1", "H1", "W2", "H2", "QTY",
                "AREA (sqm)", "TOTAL AREA", "RATE (AED)", "TOTAL (AED)"
            };
            var alignments = new[]
            {
                HorizontalAlignment.Center, HorizontalAlignment.Left,
                HorizontalAlignment.Right,  HorizontalAlignment.Right,
                HorizontalAlignment.Right,  HorizontalAlignment.Right,
                HorizontalAlignment.Center, HorizontalAlignment.Right,
                HorizontalAlignment.Right,  HorizontalAlignment.Right,
                HorizontalAlignment.Right
            };

            var tblHeader = new Border { Background = TableHeaderBrush };
            var thGrid = MakeGridWithColumns(widths);
            for (int i = 0; i < headerLabels.Length; i++)
            {
                var cell = MakeText(
                    headerLabels[i], 8, FontWeights.Bold, Brushes.White,
                    horizontalAlignment: alignments[i]);
                cell.Padding = new Thickness(4, 4, 4, 4);
                Grid.SetColumn(cell, i);
                thGrid.Children.Add(cell);
            }
            tblHeader.Child = thGrid;
            stack.Children.Add(tblHeader);

            foreach (var row in group.ItemRows)
            {
                var rowBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
                    BorderThickness = new Thickness(0, 0, 0, 1)
                };
                var rowGrid = MakeGridWithColumns(widths);

                // Phase 5: single authoritative panel SQM (round-3 + 0.5 floor).
                decimal unitArea =
                    ProGlassAutomation.Utilities.SqmCalculator.CalculatePanel(
                        row.W1, row.H1);
                decimal totalArea = Math.Round(unitArea * row.Qty, 3);

                AddTableCell(rowGrid, 0, row.SerialNumber.ToString(), FontWeights.Normal, HorizontalAlignment.Center);
                AddTableCell(rowGrid, 1, row.GlassRef ?? "", FontWeights.Normal, HorizontalAlignment.Left);
                AddTableCell(rowGrid, 2, row.W1.ToString(), FontWeights.Normal, HorizontalAlignment.Right);
                AddTableCell(rowGrid, 3, row.H1.ToString(), FontWeights.Normal, HorizontalAlignment.Right);
                AddTableCell(rowGrid, 4, row.W2.ToString(), FontWeights.Normal, HorizontalAlignment.Right);
                AddTableCell(rowGrid, 5, row.H2.ToString(), FontWeights.Normal, HorizontalAlignment.Right);
                AddTableCell(rowGrid, 6, row.Qty.ToString(), FontWeights.Normal, HorizontalAlignment.Center);
                AddTableCell(rowGrid, 7, unitArea.ToString("0.000"), FontWeights.Normal, HorizontalAlignment.Right);
                AddTableCell(rowGrid, 8, totalArea.ToString("0.000"), FontWeights.Normal, HorizontalAlignment.Right);
                AddTableCell(rowGrid, 9, row.Rate.ToString("N2"), FontWeights.Normal, HorizontalAlignment.Right);
                AddTableCell(rowGrid, 10, row.Value.ToString("N2"), FontWeights.Bold, HorizontalAlignment.Right);

                rowBorder.Child = rowGrid;
                stack.Children.Add(rowBorder);
            }

            var subTotal = new Border
            {
                Background = LightGrayBrush,
                BorderBrush = LineBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(6, 5, 6, 5)
            };
            var stGrid = new Grid();
            stGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            stGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = GridLength.Auto });

            var stLeft = new StackPanel { Orientation = Orientation.Horizontal };
            stLeft.Children.Add(MakeText("SPEC SUB-TOTALS:", 9, FontWeights.Bold, AccentBrush));
            stLeft.Children.Add(MakeText(
                $"  QTY: {group.FooterTotalQty}", 9, FontWeights.Bold, DarkBrush, marginLeft: 6));
            stLeft.Children.Add(MakeText(
                $"  •  AREA: {group.FooterTotalSqm:0.000} sqm",
                9, FontWeights.Bold, DarkBrush));
            Grid.SetColumn(stLeft, 0);
            stGrid.Children.Add(stLeft);

            var stRight = new StackPanel { Orientation = Orientation.Horizontal };
            stRight.Children.Add(MakeText("VALUE: AED ", 9, FontWeights.Bold, AccentBrush));
            stRight.Children.Add(MakeText(
                group.FooterTotalValue.ToString("N2"), 10, FontWeights.Bold, GreenBrush));
            Grid.SetColumn(stRight, 1);
            stGrid.Children.Add(stRight);

            subTotal.Child = stGrid;
            stack.Children.Add(subTotal);

            return stack;
        }

        private Border BuildGrandSpecSummary()
        {
            int specCount = DataGrid.SpecGroups.Count;
            int totalQty = DataGrid.SpecGroups.Sum(g => g.FooterTotalQty);
            decimal totalArea = DataGrid.SpecGroups.Sum(g => g.FooterTotalSqm);
            decimal totalValue = DataGrid.SpecGroups.Sum(g => g.FooterTotalValue);

            var border = new Border
            {
                Background = AccentBrush,
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, SECTION_GAP)
            };

            var g = new Grid();
            g.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(
                new ColumnDefinition { Width = GridLength.Auto });

            var left = MakeText(
                $"GRAND TOTAL:  {specCount} Spec{(specCount != 1 ? "s" : "")}  •  " +
                $"QTY: {totalQty}  •  AREA: {totalArea:0.000} sqm",
                10, FontWeights.Bold, Brushes.White);
            Grid.SetColumn(left, 0);
            g.Children.Add(left);

            var right = MakeText(
                $"VALUE: AED {totalValue:N2}", 11, FontWeights.Bold, Brushes.White);
            Grid.SetColumn(right, 1);
            g.Children.Add(right);

            border.Child = g;
            return border;
        }

        private StackPanel BuildOtherChargesSection()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };

            var header = new Border
            {
                Background = AccentBrush,
                Padding = new Thickness(8, 5, 8, 5)
            };
            header.Child = MakeText(
                "SUPPLEMENTARY / FABRICATION OTHER CHARGES",
                10, FontWeights.Bold, Brushes.White);
            stack.Children.Add(header);

            var container = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
                BorderThickness = new Thickness(1, 0, 1, 1),
                Padding = new Thickness(8)
            };
            var itemsStack = new StackPanel();

            foreach (var row in OtherCharges.ChargeRows)
            {
                if (!row.IsActive) continue;
                itemsStack.Children.Add(BuildChargeLine(
                    row.ChargeName,
                    $"Qty: {row.MetricQty:0.##} {row.Unit}",
                    $"Rate: AED {row.Rate:N2}",
                    $"AED {row.ResultAmount:N2}"));
            }

            foreach (var row in OtherCharges.WastagesSection.WastageRows)
            {
                if (!row.IsActive) continue;
                itemsStack.Children.Add(BuildChargeLine(
                    $"Wastage — {row.ColorName}",
                    $"Qty: {row.TotalSqm:0.###} sqm",
                    $"Rate: AED {row.Rate:N2}",
                    $"AED {row.ResultAmount:N2}"));
            }

            foreach (var row in OtherCharges.CustomChargesSection.AddedCharges)
            {
                if (!row.IsActive) continue;
                itemsStack.Children.Add(BuildChargeLine(
                    row.ChargeName,
                    $"Qty: {row.Qty:0.##} {row.Unit}",
                    $"Rate: AED {row.Rate:N2}",
                    $"AED {row.ResultAmount:N2}"));
            }

            container.Child = itemsStack;
            stack.Children.Add(container);
            return stack;
        }

        private Grid BuildChargeLine(string name, string qty, string rate, string total)
        {
            var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            g.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            g.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            g.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var t0 = MakeText(name, 9, FontWeights.SemiBold, DarkBrush);
            Grid.SetColumn(t0, 0); g.Children.Add(t0);

            var t1 = MakeText(qty, 9, FontWeights.Normal, MutedBrush);
            Grid.SetColumn(t1, 1); g.Children.Add(t1);

            var t2 = MakeText(rate, 9, FontWeights.Normal, MutedBrush);
            Grid.SetColumn(t2, 2); g.Children.Add(t2);

            var t3 = MakeText(total, 10, FontWeights.Bold, DarkBrush,
                horizontalAlignment: HorizontalAlignment.Right);
            Grid.SetColumn(t3, 3); g.Children.Add(t3);

            return g;
        }

        private Border BuildFinancialSummary(double width)
        {
            var wrap = new Border
            {
                BorderBrush = LineBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 10, 0, 0),
                Margin = new Thickness(0, 10, 0, 0)
            };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                MinWidth = 320
            };

            AddSummaryLine(stack, "Glass Materials Total:",
                $"{MainSummary.GlassMaterialsSubtotal:N2} AED", FontWeights.SemiBold);
            AddSummaryLine(stack, "Other Processing/Delivery Total:",
                $"{MainSummary.OtherChargesSubtotal:N2} AED", FontWeights.SemiBold);

            stack.Children.Add(new Border
            {
                Background = LineBrush,
                Height = 1,
                Margin = new Thickness(0, 3, 0, 3)
            });

            AddSummaryLine(stack, "Estimated Net Subtotal:",
                $"{MainSummary.NetSubtotal:N2} AED", FontWeights.Bold, size: 10);

            if (MainSummary.DiscountAmount > 0m)
                AddSummaryLine(stack, "Discount:",
                    $"- {MainSummary.DiscountAmount:N2} AED",
                    FontWeights.SemiBold, brush: RedBrush);

            if (MainSummary.IsVatApplicable)
                AddSummaryLine(stack,
                    $"Standard UAE VAT ({MainSummary.VatPercent:0.##}%):",
                    $"{MainSummary.VatAmount:N2} AED", FontWeights.SemiBold);

            var grandBand = new Border
            {
                Background = AccentBrush,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 6, 0, 0)
            };
            var gg = new Grid();
            gg.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gg.ColumnDefinitions.Add(
                new ColumnDefinition { Width = GridLength.Auto });

            var gLbl = MakeText(
                "TOTAL EST VALUE (VAT INC):", 12, FontWeights.Bold, Brushes.White);
            Grid.SetColumn(gLbl, 0); gg.Children.Add(gLbl);

            var gVal = MakeText(
                $"{MainSummary.GrandTotalVatInclusive:N2} AED",
                13, FontWeights.Bold, Brushes.White);
            Grid.SetColumn(gVal, 1); gg.Children.Add(gVal);

            grandBand.Child = gg;
            stack.Children.Add(grandBand);

            wrap.Child = stack;
            return wrap;
        }

        private StackPanel BuildTermsAndConditionsSection()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 8) };

            var header = new Border
            {
                Background = AccentBrush,
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 8)
            };
            header.Child = MakeText(
                "TERMS & CONDITIONS", 10, FontWeights.Bold, Brushes.White);
            stack.Children.Add(header);

            if (Terms == null)
            {
                stack.Children.Add(MakeText(
                    "[Terms VM not initialized]",
                    9, FontWeights.Normal, RedBrush, fontStyle: FontStyles.Italic));
                return stack;
            }

            if (Terms.Clauses == null || Terms.Clauses.Count == 0)
            {
                stack.Children.Add(MakeText(
                    "No terms configured. Add clauses in the Terms editor above.",
                    9, FontWeights.Normal, MutedBrush, fontStyle: FontStyles.Italic));
                return stack;
            }

            System.Collections.Generic.List<
                Models.ProformaInvoice.Step7PrintPreview
                    .TermsAndConditions.TermClause> activeClauses;

            try
            {
                activeClauses = Terms.GetActivePrintableClauses().ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[T&C] GetActivePrintableClauses failed: {ex}");
                stack.Children.Add(MakeText(
                    $"[Error loading clauses: {ex.Message}]",
                    9, FontWeights.Normal, RedBrush, fontStyle: FontStyles.Italic));
                return stack;
            }

            if (activeClauses.Count == 0)
            {
                stack.Children.Add(MakeText(
                    $"No active clauses. ({Terms.Clauses.Count} total, all inactive — " +
                    "tick checkboxes in the Terms editor above.)",
                    9, FontWeights.Normal, MutedBrush, fontStyle: FontStyles.Italic));
                return stack;
            }

            foreach (var clause in activeClauses)
            {
                try
                {
                    var clauseBox = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

                    clauseBox.Children.Add(MakeText(
                        $"{clause.Order}. {(clause.Title ?? "").ToUpper()}",
                        9, FontWeights.Bold, DarkBrush, marginBottom: 2));

                    if (!string.IsNullOrWhiteSpace(clause.Body))
                    {
                        clauseBox.Children.Add(MakeText(
                            clause.Body, 9, FontWeights.Normal, DarkBrush,
                            marginLeft: 12));
                    }

                    if (clause.HasSubLines && clause.SubLines != null)
                    {
                        foreach (var line in clause.SubLines)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            var bulletRow = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Margin = new Thickness(24, 1, 0, 0)
                            };
                            bulletRow.Children.Add(MakeText(
                                "• ", 9, FontWeights.Bold, AccentBrush));
                            bulletRow.Children.Add(MakeText(
                                line, 9, FontWeights.Normal, DarkBrush));
                            clauseBox.Children.Add(bulletRow);
                        }
                    }

                    stack.Children.Add(clauseBox);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        $"[T&C] Failed to render clause {clause?.Order}: {ex}");
                }
            }

            return stack;
        }

        private StackPanel BuildTermsFooter()
        {
            var s = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            s.Children.Add(MakeText(
                FooterDisclaimer, 8, FontWeights.Normal, MutedBrush,
                fontStyle: FontStyles.Italic));

            var terms = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 0)
            };
            terms.Children.Add(MakeText(
                "Terms: ", 8, FontWeights.Normal, MutedBrush,
                fontStyle: FontStyles.Italic));
            terms.Children.Add(MakeText(
                TermsText, 8, FontWeights.Normal, MutedBrush,
                fontStyle: FontStyles.Italic));
            s.Children.Add(terms);

            return s;
        }

        private Grid BuildSignatureBlocks(double width)
        {
            var g = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            g.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(16) });
            g.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var pbBox = new Border
            {
                BorderBrush = LineBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(10, 8, 10, 8)
            };
            var pbStack = new StackPanel();
            pbStack.Children.Add(MakeText(
                "PREPARED BY (ERP AUTHENTICATED)",
                9, FontWeights.Bold, AccentBrush, marginBottom: 4));

            string nameForPrint = string.IsNullOrWhiteSpace(PreparedByName)
                ? "—" : PreparedByName;
            pbStack.Children.Add(MakeText(
                nameForPrint, 10, FontWeights.SemiBold, DarkBrush));

            if (!string.IsNullOrWhiteSpace(PreparedByDept))
                pbStack.Children.Add(MakeText(
                    PreparedByDept, 9, FontWeights.Normal, MutedBrush, marginTop: 2));

            var status = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 0, 0)
            };
            status.Children.Add(MakeText("Status: ", 9, FontWeights.Normal, MutedBrush));
            string statusForPrint = string.IsNullOrWhiteSpace(PreparedByStatus)
                ? "Draft" : PreparedByStatus;
            status.Children.Add(MakeText(
                statusForPrint, 9, FontWeights.Bold, GreenBrush));
            pbStack.Children.Add(status);

            pbBox.Child = pbStack;
            Grid.SetColumn(pbBox, 0);
            g.Children.Add(pbBox);

            var csBox = new Border
            {
                BorderBrush = LineBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(10, 8, 10, 8)
            };
            var csStack = new StackPanel();
            csStack.Children.Add(MakeText(
                "CLIENT APPROVAL SIGN & MOB NUMBER",
                9, FontWeights.Bold, AccentBrush, marginBottom: 4));

            var sign = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 0, 0)
            };
            sign.Children.Add(MakeText(
                "Approval Sign:", 9, FontWeights.Normal, MutedBrush));
            sign.Children.Add(new Border
            {
                Background = LineBrush,
                Height = 1,
                MinWidth = 140,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(6, 0, 0, 3)
            });
            csStack.Children.Add(sign);

            var mob = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 0, 0)
            };
            mob.Children.Add(MakeText(
                "Mobile Number:", 9, FontWeights.Normal, MutedBrush));
            mob.Children.Add(new Border
            {
                Background = LineBrush,
                Height = 1,
                MinWidth = 140,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(6, 0, 0, 3)
            });
            csStack.Children.Add(mob);

            csStack.Children.Add(MakeText(
                "Authorized Name & Stamp",
                9, FontWeights.Normal, MutedBrush, marginTop: 4));

            csBox.Child = csStack;
            Grid.SetColumn(csBox, 2);
            g.Children.Add(csBox);

            return g;
        }

        private Border BuildDivider(double top, double bottom) => new Border
        {
            Background = LineBrush,
            Height = 1,
            Margin = new Thickness(0, top, 0, bottom)
        };

        private void AddLabelValueRow(Panel parent, string label, string value)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 1, 0, 0)
            };
            row.Children.Add(MakeText(label, 9, FontWeights.Normal, MutedBrush));
            row.Children.Add(MakeText(
                string.IsNullOrWhiteSpace(value) ? "—" : value,
                9, FontWeights.SemiBold, DarkBrush));
            parent.Children.Add(row);
        }

        private void AddSummaryLine(
            Panel parent, string label, string value,
            FontWeight weight, double size = 9, Brush brush = null)
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            g.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(
                new ColumnDefinition { Width = GridLength.Auto });

            var b = brush ?? DarkBrush;
            var lbl = MakeText(label, size, weight, b);
            Grid.SetColumn(lbl, 0); g.Children.Add(lbl);

            var val = MakeText(
                value, size, weight, b,
                horizontalAlignment: HorizontalAlignment.Right);
            Grid.SetColumn(val, 1); g.Children.Add(val);

            parent.Children.Add(g);
        }

        private Grid MakeGridWithColumns(double[] widths)
        {
            var g = new Grid();
            foreach (var w in widths)
            {
                g.ColumnDefinitions.Add(w > 5
                    ? new ColumnDefinition { Width = new GridLength(w) }
                    : new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });
            }
            return g;
        }

        private void AddTableCell(
            Grid grid, int col, string text,
            FontWeight weight, HorizontalAlignment align)
        {
            var cell = MakeText(text, 8, weight, DarkBrush,
                horizontalAlignment: align);
            cell.Padding = new Thickness(4, 3, 4, 3);
            cell.VerticalAlignment = VerticalAlignment.Center;
            cell.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(cell, col);
            grid.Children.Add(cell);
        }

        // BUG-66: cached _fontFamily
        private TextBlock MakeText(
            string text,
            double fontSize,
            FontWeight weight,
            Brush foreground,
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left,
            FontStyle? fontStyle = null,
            double marginTop = 0,
            double marginBottom = 0,
            double marginLeft = 0,
            double marginRight = 0)
        {
            var tb = new TextBlock
            {
                Text = text ?? "",
                FontFamily = _fontFamily,
                FontSize = fontSize,
                FontWeight = weight,
                Foreground = foreground,
                HorizontalAlignment = horizontalAlignment,
                Margin = new Thickness(marginLeft, marginTop, marginRight, marginBottom),
                TextWrapping = TextWrapping.Wrap
            };
            if (fontStyle.HasValue) tb.FontStyle = fontStyle.Value;
            return tb;
        }

        private static void ApplyVectorTextRendering(DependencyObject element)
        {
            if (element == null) return;
            TextOptions.SetTextFormattingMode(element, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(element, TextRenderingMode.Auto);
            TextOptions.SetTextHintingMode(element, TextHintingMode.Fixed);
            RenderOptions.SetBitmapScalingMode(element, BitmapScalingMode.Fant);
            // BUG-64: WPF inherits attached props to children — no recursive walk
        }

        private static void ShowComingSoon(string featureName, string phase)
        {
            MessageBox.Show(
                $"{featureName} is coming in {phase}.\n\n" +
                "For now, use the Print button (top-right) — you can 'Print to PDF' " +
                "via Microsoft Print to PDF to save as PDF.",
                $"{featureName} — Coming Soon",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}