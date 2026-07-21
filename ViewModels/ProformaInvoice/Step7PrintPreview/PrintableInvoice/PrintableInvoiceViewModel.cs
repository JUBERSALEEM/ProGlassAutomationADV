using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.CustomCharges;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.ProformaInvoice.SavedInvoicesList;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step1CompanySetup;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step2ProjectSpecs;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step3ClientProfile;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step7PrintPreview.InvoiceMainSummary;
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
    public class PrintableInvoiceViewModel : ViewModelBase
    {
        public CompanySetupViewModel Company { get; }
        public ProjectSpecsViewModel Project { get; }
        public ClientProfileViewModel Client { get; }
        public DataGridViewModel DataGrid { get; }
        public OtherChargesViewModel OtherCharges { get; }
        public InvoiceMainSummaryViewModel MainSummary { get; }
        public TermsAndConditionsViewModel Terms { get; }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 7.16a — Prepared By fields auto-populate from wizard data
        //  but remain editable in the A4 preview for per-invoice overrides.
        //
        //  When the user has NOT manually edited a field (still null/empty),
        //  the getter returns the source data from the wizard steps.
        //  Once the user types anything, the local override kicks in.
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
            set { if (_preparedByDept == value) return; _preparedByDept = value; OnPropertyChanged(); }
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

        private string _termsText = "Standard warranties apply to manufactured glass products upon POD signature clearance.";
        public string TermsText
        {
            get => _termsText;
            set { if (_termsText == value) return; _termsText = value; OnPropertyChanged(); }
        }

        private string _footerDisclaimer = "Confidential Document generated automatically by ProGlass Automation ERP platform.";
        public string FooterDisclaimer
        {
            get => _footerDisclaimer;
            set { if (_footerDisclaimer == value) return; _footerDisclaimer = value; OnPropertyChanged(); }
        }

        // ── Commands ────────────────────────────────────────────────────────

        public ICommand PrintCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand SaveInvoiceCommand { get; }
        public ICommand LoadInvoiceCommand { get; }
        public ICommand SendEmailCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportCsvCommand { get; }

        public FrameworkElement PrintTarget { get; set; }

        public PrintableInvoiceViewModel(
            CompanySetupViewModel company,
            ProjectSpecsViewModel project,
            ClientProfileViewModel client,
            DataGridViewModel dataGrid,
            OtherChargesViewModel otherCharges,
            InvoiceMainSummaryViewModel mainSummary,
            TermsAndConditionsViewModel terms)
        {
            Company = company ?? throw new ArgumentNullException(nameof(company));
            Project = project ?? throw new ArgumentNullException(nameof(project));
            Client = client ?? throw new ArgumentNullException(nameof(client));
            DataGrid = dataGrid ?? throw new ArgumentNullException(nameof(dataGrid));
            OtherCharges = otherCharges ?? throw new ArgumentNullException(nameof(otherCharges));
            MainSummary = mainSummary ?? throw new ArgumentNullException(nameof(mainSummary));
            Terms = terms ?? throw new ArgumentNullException(nameof(terms));

            PrintCommand = new RelayCommand(_ => ExecutePrint());
            ExportPdfCommand = new RelayCommand(_ => ExecuteExportPdf());
            SaveInvoiceCommand = new RelayCommand(_ => ExecuteSaveInvoice());
            LoadInvoiceCommand = new RelayCommand(_ => ExecuteLoadInvoice());
            SendEmailCommand = new RelayCommand(_ => ShowComingSoon("Send Email", "Phase 7.20"));
            ExportExcelCommand = new RelayCommand(_ => ExecuteExportExcel());
            ExportCsvCommand = new RelayCommand(_ => ExecuteExportCsv());

            // Phase 7.16a: propagate upstream changes so preview reflects live
            // updates when user edits Step 1 or Step 3
            Client.PropertyChanged += OnClientChanged;
            Company.PropertyChanged += OnCompanyChanged;
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
        //  NATIVE PRINT — builds FixedDocument from scratch in code
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
                    string msg = $"BUILD FAILED:\n\nType: {buildEx.GetType().Name}\nMessage: {buildEx.Message}";
                    if (buildEx.InnerException != null)
                        msg += $"\n\nInner: {buildEx.InnerException.Message}";
                    msg += $"\n\nStack (first 600 chars):\n{(buildEx.StackTrace ?? "").Substring(0, Math.Min(600, (buildEx.StackTrace ?? "").Length))}";
                    MessageBox.Show(msg, "Print Build Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    dialog.PrintDocument(fixedDoc.DocumentPaginator, $"Proforma Invoice - {Company.ProformaId}");
                }
                catch (Exception printEx)
                {
                    string msg = $"PRINT SEND FAILED:\n\nType: {printEx.GetType().Name}\nMessage: {printEx.Message}";
                    if (printEx.InnerException != null)
                        msg += $"\n\nInner: {printEx.InnerException.Message}";
                    msg += $"\n\nStack (first 600 chars):\n{(printEx.StackTrace ?? "").Substring(0, Math.Min(600, (printEx.StackTrace ?? "").Length))}";
                    MessageBox.Show(msg, "Print Send Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                string msg = $"PRINT FAILED (OUTER):\n\nType: {ex.GetType().Name}\nMessage: {ex.Message}";
                if (ex.InnerException != null)
                    msg += $"\n\nInner: {ex.InnerException.Message}";
                msg += $"\n\nStack (first 600 chars):\n{(ex.StackTrace ?? "").Substring(0, Math.Min(600, (ex.StackTrace ?? "").Length))}";
                MessageBox.Show(msg, "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 7.15 — SAVE INVOICE TO DATABASE
        // ══════════════════════════════════════════════════════════════════

        private async void ExecuteSaveInvoice()
        {
            if (App.Invoices == null)
            {
                MessageBox.Show(
                    "Invoice database is not initialized. Please restart the app.",
                    "Save Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(Company.ProformaId))
            {
                MessageBox.Show(
                    "Cannot save — Proforma ID is empty.\n\n" +
                    "Please set a Proforma ID in Step 1 (Company Details) before saving.",
                    "Save Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                var entity = ProformaInvoiceMapper.BuildEntityFromWizard(
                    Company, Project, Client, DataGrid, OtherCharges,
                    MainSummary, Terms, this);

                bool alreadyExists = await App.Invoices.ExistsByProformaIdAsync(entity.ProformaId);

                if (alreadyExists)
                {
                    string suggestedId = await App.Invoices.SuggestNextIdAsync(entity.ProformaId);

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
                        await App.Invoices.UpdateAsync(entity);
                        ShowSaveSuccess(entity.ProformaId, isUpdate: true);
                        return;
                    }

                    entity.ProformaId = suggestedId;
                    Company.ProformaId = suggestedId;
                    await App.Invoices.InsertAsync(entity);
                    ShowSaveSuccess(entity.ProformaId, isUpdate: false);
                    return;
                }

                await App.Invoices.InsertAsync(entity);
                ShowSaveSuccess(entity.ProformaId, isUpdate: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to save invoice:\n\n{ex.Message}",
                    "Save Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static void ShowSaveSuccess(string proformaId, bool isUpdate)
        {
            string verb = isUpdate ? "updated" : "saved";
            MessageBox.Show(
                $"Invoice '{proformaId}' was {verb} successfully.\n\n" +
                "You can load it later using the LOAD INVOICE button.",
                "Save Successful",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 7.16 — LOAD SAVED INVOICE
        // ══════════════════════════════════════════════════════════════════

        private async void ExecuteLoadInvoice()
        {
            if (App.Invoices == null)
            {
                MessageBox.Show(
                    "Invoice database is not initialized. Please restart the app.",
                    "Load Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
                {
                    ExecuteSaveInvoice();
                }
            }

            var dialog = new SavedInvoicesListDialog
            {
                Owner = Application.Current.MainWindow
            };

            bool? ok = dialog.ShowDialog();
            if (ok != true || dialog.SelectedInvoiceId <= 0) return;

            try
            {
                var entity = await App.Invoices.GetByIdAsync(dialog.SelectedInvoiceId);
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

                ProformaInvoiceMapper.RestoreWizardFromSnapshot(
                    snapshot,
                    Company, Project, Client, DataGrid, OtherCharges,
                    MainSummary, Terms, this);

                MessageBox.Show(
                    $"Invoice '{entity.ProformaId}' loaded successfully.\n\n" +
                    "All wizard steps have been updated. Review each step and make changes as needed.",
                    "Load Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load invoice:\n\n{ex.Message}",
                    "Load Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 7.17 — QUEST PDF BRANDED EXPORT
        //  Builds a PdfExportContext from wizard VMs, generates the PDF via
        //  QuestPDF, saves to user-chosen location, and auto-opens the file.
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteExportPdf()
        {
            try
            {
                // Validate
                if (string.IsNullOrWhiteSpace(Company.ProformaId))
                {
                    MessageBox.Show(
                        "Cannot export — Proforma ID is empty.\n\n" +
                        "Please set a Proforma ID in Step 1 (Company Details) before exporting.",
                        "Export Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Build safe default filename
                string safeName = Company.ProformaId.Replace("/", "-").Replace("\\", "-").Replace(":", "-");
                string defaultFileName = $"{safeName}_{Company.IssueDate:yyyy-MM-dd}.pdf";

                // SaveFileDialog
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Proforma Invoice as PDF",
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    FileName = defaultFileName,
                    InitialDirectory = System.IO.Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                        "ProGlassAutomation", "PDFs")
                };

                // Ensure default directory exists
                if (!System.IO.Directory.Exists(dialog.InitialDirectory))
                    System.IO.Directory.CreateDirectory(dialog.InitialDirectory);

                if (dialog.ShowDialog() != true) return;

                string outputPath = dialog.FileName;

                // Build the context from live wizard VMs
                var ctx = Services.ProformaInvoice.PdfExport.PdfExportContextBuilder.Build(
                    Company, Project, Client, DataGrid, OtherCharges,
                    MainSummary, Terms, this);

                // Generate the PDF
                var pdfService = new Services.ProformaInvoice.PdfExport.ProformaInvoicePdfService();
                pdfService.GeneratePdf(ctx, outputPath);

                // Success — ask to open
                var result = MessageBox.Show(
                    $"PDF exported successfully!\n\n" +
                    $"File: {outputPath}\n\n" +
                    "Would you like to open the PDF now?",
                    "Export Complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = outputPath,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        // Silently fail if no PDF viewer is installed
                    }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"PDF export failed:\n\n{ex.Message}",
                    "Export Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 7.18 — EXCEL EXPORT (.xlsx)
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteExportExcel()
        {
            try
            {
                string safeName = (Company.ProformaId ?? "Invoice").Replace("/", "-").Replace("\\", "-").Replace(":", "-");
                string defaultFileName = $"{safeName}_{Company.IssueDate:yyyy-MM-dd}.xlsx";

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Invoice as Excel",
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    DefaultExt = ".xlsx",
                    FileName = defaultFileName,
                    InitialDirectory = System.IO.Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                        "ProGlassAutomation", "Exports")
                };

                if (!System.IO.Directory.Exists(dialog.InitialDirectory))
                    System.IO.Directory.CreateDirectory(dialog.InitialDirectory);

                if (dialog.ShowDialog() != true) return;

                var ctx = Services.ProformaInvoice.PdfExport.PdfExportContextBuilder.Build(
                    Company, Project, Client, DataGrid, OtherCharges, MainSummary, Terms, this);

                var service = new Services.ProformaInvoice.InvoiceExportImport.InvoiceExcelExportService();
                service.ExportToExcel(ctx, dialog.FileName);

                var result = MessageBox.Show(
                    $"Excel exported successfully!\n\nFile: {dialog.FileName}\n\nOpen now?",
                    "Export Complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dialog.FileName, UseShellExecute = true }); }
                    catch { }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Excel export failed:\n\n{ex.Message}", "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 7.18 — CSV EXPORT (.csv)
        // ══════════════════════════════════════════════════════════════════

        private void ExecuteExportCsv()
        {
            try
            {
                string safeName = (Company.ProformaId ?? "Invoice").Replace("/", "-").Replace("\\", "-").Replace(":", "-");
                string defaultFileName = $"{safeName}_{Company.IssueDate:yyyy-MM-dd}.csv";

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Invoice as CSV",
                    Filter = "CSV Files (*.csv)|*.csv",
                    DefaultExt = ".csv",
                    FileName = defaultFileName,
                    InitialDirectory = System.IO.Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                        "ProGlassAutomation", "Exports")
                };

                if (!System.IO.Directory.Exists(dialog.InitialDirectory))
                    System.IO.Directory.CreateDirectory(dialog.InitialDirectory);

                if (dialog.ShowDialog() != true) return;

                var ctx = Services.ProformaInvoice.PdfExport.PdfExportContextBuilder.Build(
                    Company, Project, Client, DataGrid, OtherCharges, MainSummary, Terms, this);

                var service = new Services.ProformaInvoice.InvoiceExportImport.InvoiceCsvExportService();
                service.ExportToCsv(ctx, dialog.FileName);

                var result = MessageBox.Show(
                    $"CSV exported successfully!\n\nFile: {dialog.FileName}\n\nOpen now?",
                    "Export Complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dialog.FileName, UseShellExecute = true }); }
                    catch { }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"CSV export failed:\n\n{ex.Message}", "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Layout constants ────────────────────────────────────────────────

        private const double PAGE_MARGIN = 40;
        private const double SECTION_GAP = 10;
        private const string FONT_FAMILY = "Segoe UI";

        private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0x4F, 0x46, 0xE5));
        private static readonly Brush DarkBrush = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
        private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
        private static readonly Brush LineBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
        private static readonly Brush LightGrayBrush = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
        private static readonly Brush TableHeaderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
        private static readonly Brush GreenBrush = new SolidColorBrush(Color.FromRgb(0x04, 0x78, 0x57));
        private static readonly Brush RedBrush = new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C));

        private FixedDocument BuildPrintableDocument(System.Windows.Controls.PrintDialog dialog)
        {
            double pageWidth = dialog.PrintableAreaWidth;
            double pageHeight = dialog.PrintableAreaHeight;
            double contentWidth = pageWidth - (PAGE_MARGIN * 2);

            var doc = new FixedDocument();
            doc.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);

            var fullContent = BuildFullContentStack(contentWidth);
            ApplyVectorTextRendering(fullContent);

            fullContent.Measure(new Size(contentWidth, double.PositiveInfinity));
            fullContent.Arrange(new Rect(0, 0, contentWidth, fullContent.DesiredSize.Height));
            fullContent.UpdateLayout();

            double totalHeight = fullContent.DesiredSize.Height;
            double usableHeight = pageHeight - (PAGE_MARGIN * 2);

            int totalPages = Math.Max(1, (int)Math.Ceiling(totalHeight / usableHeight));

            Debug.WriteLine($"[PRINT] totalHeight={totalHeight}, usableHeight={usableHeight}, totalPages={totalPages}");

            for (int i = 0; i < totalPages; i++)
            {
                double sliceY = i * usableHeight;

                var pageContent = BuildFullContentStack(contentWidth);
                ApplyVectorTextRendering(pageContent);

                pageContent.Measure(new Size(contentWidth, double.PositiveInfinity));
                pageContent.Arrange(new Rect(0, 0, contentWidth, pageContent.DesiredSize.Height));
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

            root.Children.Add(BuildOtherChargesSection());
            root.Children.Add(BuildFinancialSummary(width));
            root.Children.Add(BuildDivider(top: 10, bottom: 6));

            root.Children.Add(BuildTermsAndConditionsSection());

            root.Children.Add(BuildTermsFooter());
            root.Children.Add(BuildSignatureBlocks(width));

            return root;
        }

        private Grid BuildCompanyHeader()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

            var left = new StackPanel();
            left.Children.Add(MakeText(
                string.IsNullOrWhiteSpace(Company.CompanyName) ? "PROGLASS AUTOMATION ERP" : Company.CompanyName,
                18, FontWeights.Bold, AccentBrush));
            left.Children.Add(MakeText(
                "Premium Glass Glazing, High-Yield Manufacturing & Logistics Spec",
                10, FontWeights.Normal, MutedBrush, marginTop: 2));

            var loc = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 0) };
            loc.Children.Add(MakeText(
                string.IsNullOrWhiteSpace(Company.CompanyLocation) ? "United Arab Emirates" : Company.CompanyLocation,
                10, FontWeights.Normal, MutedBrush));
            loc.Children.Add(MakeText(" • www.proglass-automation.com", 10, FontWeights.Normal, MutedBrush));
            left.Children.Add(loc);

            if (!string.IsNullOrWhiteSpace(Company.CompanyTrn))
            {
                var trn = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 0) };
                trn.Children.Add(MakeText("TRN: ", 10, FontWeights.SemiBold, MutedBrush));
                trn.Children.Add(MakeText(Company.CompanyTrn, 10, FontWeights.SemiBold, DarkBrush));
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
            badgeStack.Children.Add(MakeText("PROFORMA INVOICE", 12, FontWeights.Bold, DarkBrush,
                horizontalAlignment: HorizontalAlignment.Center));

            var refGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            refGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            refGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            refGrid.RowDefinitions.Add(new RowDefinition());
            refGrid.RowDefinitions.Add(new RowDefinition());

            var refLbl = MakeText("Ref:", 10, FontWeights.Normal, MutedBrush, marginRight: 6);
            Grid.SetRow(refLbl, 0); Grid.SetColumn(refLbl, 0);
            refGrid.Children.Add(refLbl);

            var refVal = MakeText(Company.ProformaId ?? "—", 10, FontWeights.SemiBold, DarkBrush);
            Grid.SetRow(refVal, 0); Grid.SetColumn(refVal, 1);
            refGrid.Children.Add(refVal);

            var dateLbl = MakeText("Date:", 10, FontWeights.Normal, MutedBrush, marginRight: 6, marginTop: 2);
            Grid.SetRow(dateLbl, 1); Grid.SetColumn(dateLbl, 0);
            refGrid.Children.Add(dateLbl);

            var dateVal = MakeText(Company.IssueDate.ToString("yyyy-MM-dd"), 10, FontWeights.SemiBold, DarkBrush, marginTop: 2);
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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var clientStack = new StackPanel();
            clientStack.Children.Add(MakeText("CLIENT / BILL TO:", 10, FontWeights.Bold, DarkBrush, marginBottom: 4));
            AddLabelValueRow(clientStack, "Customer Name: ", Client.CustomerName);
            AddLabelValueRow(clientStack, "TRN/VAT Number: ", Client.CustomerTrn);
            AddLabelValueRow(clientStack, "Attention Name: ", Client.AttentionExecutiveName);
            AddLabelValueRow(clientStack, "Contact Details: ", Client.ClientContactPhone);
            AddLabelValueRow(clientStack, "Billing Address: ", Client.BillingAddress);
            AddLabelValueRow(clientStack, "Client Reference: ", Client.CustomerReferenceCode);

            Grid.SetColumn(clientStack, 0);
            grid.Children.Add(clientStack);

            var projStack = new StackPanel();
            projStack.Children.Add(MakeText("PROJECT ESTIMATE METRIC:", 10, FontWeights.Bold, DarkBrush, marginBottom: 4));
            AddLabelValueRow(projStack, "Project Designation: ", Project.ProjectIdentifierName);
            AddLabelValueRow(projStack, "Project Code / ID: ", Project.ProjectContractNo);
            AddLabelValueRow(projStack, "Site Delivery: ", Project.SiteDeliveryLocation);
            AddLabelValueRow(projStack, "LPO / PO Reference: ", Project.LpoNumber);
            AddLabelValueRow(projStack, "Assigned Salesman: ", Client.SalesRepresentative);
            AddLabelValueRow(projStack, "Valid Until / Due: ", $"{Company.ValidUntil:yyyy-MM-dd} • Status: {Company.ProformaStatus}");

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

            var widths = new[] { 28.0, 1.8, 1.0, 1.0, 42.0, 0.85, 0.75, 0.95 };
            var headerLabels = new[]
            {
                "#", "GLASS REF / DESCRIPTION", "SIZE W1×H1 (mm)", "SIZE W2×H2 (mm)",
                "QTY", "AREA (sqm)", "RATE (AED)", "TOTAL (AED)"
            };
            var alignments = new[]
            {
                HorizontalAlignment.Center, HorizontalAlignment.Left, HorizontalAlignment.Left, HorizontalAlignment.Left,
                HorizontalAlignment.Center, HorizontalAlignment.Right, HorizontalAlignment.Right, HorizontalAlignment.Right
            };

            var tblHeader = new Border { Background = TableHeaderBrush };
            var thGrid = MakeGridWithColumns(widths);
            for (int i = 0; i < headerLabels.Length; i++)
            {
                var cell = MakeText(headerLabels[i], 9, FontWeights.Bold, Brushes.White,
                    horizontalAlignment: alignments[i]);
                cell.Padding = new Thickness(6, 5, 6, 5);
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

                AddTableCell(rowGrid, 0, row.SerialNumber.ToString(), FontWeights.Normal, HorizontalAlignment.Center);
                AddTableCell(rowGrid, 1, row.GlassRef ?? "", FontWeights.Normal, HorizontalAlignment.Left);
                AddTableCell(rowGrid, 2, $"{row.W1}×{row.H1}", FontWeights.Normal, HorizontalAlignment.Left);
                AddTableCell(rowGrid, 3, $"{row.W2}×{row.H2}", FontWeights.Normal, HorizontalAlignment.Left);
                AddTableCell(rowGrid, 4, row.Qty.ToString(), FontWeights.Normal, HorizontalAlignment.Center);
                AddTableCell(rowGrid, 5, row.TotalSqm.ToString("0.000"), FontWeights.Normal, HorizontalAlignment.Right);
                AddTableCell(rowGrid, 6, row.Rate.ToString("N2"), FontWeights.Normal, HorizontalAlignment.Right);
                AddTableCell(rowGrid, 7, row.Value.ToString("N2"), FontWeights.Bold, HorizontalAlignment.Right);

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
            stGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            stGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stLeft = new StackPanel { Orientation = Orientation.Horizontal };
            stLeft.Children.Add(MakeText("SPEC SUB-TOTALS:", 9, FontWeights.Bold, AccentBrush));
            stLeft.Children.Add(MakeText($"  QTY: {group.FooterTotalQty}", 9, FontWeights.Bold, DarkBrush, marginLeft: 6));
            stLeft.Children.Add(MakeText($"  •  AREA: {group.FooterTotalSqm:0.000} sqm", 9, FontWeights.Bold, DarkBrush));
            Grid.SetColumn(stLeft, 0);
            stGrid.Children.Add(stLeft);

            var stRight = new StackPanel { Orientation = Orientation.Horizontal };
            stRight.Children.Add(MakeText("VALUE: AED ", 9, FontWeights.Bold, AccentBrush));
            stRight.Children.Add(MakeText(group.FooterTotalValue.ToString("N2"), 10, FontWeights.Bold, GreenBrush));
            Grid.SetColumn(stRight, 1);
            stGrid.Children.Add(stRight);

            subTotal.Child = stGrid;
            stack.Children.Add(subTotal);

            return stack;
        }

        private StackPanel BuildOtherChargesSection()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };

            var header = new Border
            {
                Background = AccentBrush,
                Padding = new Thickness(8, 5, 8, 5)
            };
            header.Child = MakeText("SUPPLEMENTARY / FABRICATION OTHER CHARGES", 10, FontWeights.Bold, Brushes.White);
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
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var t0 = MakeText(name, 9, FontWeights.SemiBold, DarkBrush);
            Grid.SetColumn(t0, 0); g.Children.Add(t0);

            var t1 = MakeText(qty, 9, FontWeights.Normal, MutedBrush);
            Grid.SetColumn(t1, 1); g.Children.Add(t1);

            var t2 = MakeText(rate, 9, FontWeights.Normal, MutedBrush);
            Grid.SetColumn(t2, 2); g.Children.Add(t2);

            var t3 = MakeText(total, 10, FontWeights.Bold, DarkBrush, horizontalAlignment: HorizontalAlignment.Right);
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

            AddSummaryLine(stack, "Glass Materials Total:", $"{MainSummary.GlassMaterialsSubtotal:N2} AED", FontWeights.SemiBold);
            AddSummaryLine(stack, "Other Processing/Delivery Total:", $"{MainSummary.OtherChargesSubtotal:N2} AED", FontWeights.SemiBold);

            var divider = new Border
            {
                Background = LineBrush,
                Height = 1,
                Margin = new Thickness(0, 3, 0, 3)
            };
            stack.Children.Add(divider);

            AddSummaryLine(stack, "Estimated Net Subtotal:", $"{MainSummary.NetSubtotal:N2} AED", FontWeights.Bold, size: 10);

            if (MainSummary.DiscountAmount > 0m)
            {
                AddSummaryLine(stack, "Discount:", $"- {MainSummary.DiscountAmount:N2} AED", FontWeights.SemiBold, brush: RedBrush);
            }

            if (MainSummary.IsVatApplicable)
            {
                AddSummaryLine(stack, $"Standard UAE VAT ({MainSummary.VatPercent:0.##}%):",
                    $"{MainSummary.VatAmount:N2} AED", FontWeights.SemiBold);
            }

            var grandBand = new Border
            {
                Background = AccentBrush,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 6, 0, 0)
            };
            var gg = new Grid();
            gg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var gLbl = MakeText("TOTAL EST VALUE (VAT INC):", 12, FontWeights.Bold, Brushes.White);
            Grid.SetColumn(gLbl, 0); gg.Children.Add(gLbl);

            var gVal = MakeText($"{MainSummary.GrandTotalVatInclusive:N2} AED", 13, FontWeights.Bold, Brushes.White);
            Grid.SetColumn(gVal, 1); gg.Children.Add(gVal);

            grandBand.Child = gg;
            stack.Children.Add(grandBand);

            wrap.Child = stack;
            return wrap;
        }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 7.14 + 7.16a — Terms & Conditions section renderer
        //  Defensive version with try/catch + always-visible header.
        // ══════════════════════════════════════════════════════════════════

        private StackPanel BuildTermsAndConditionsSection()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 8) };

            var header = new Border
            {
                Background = AccentBrush,
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 8)
            };
            header.Child = MakeText("TERMS & CONDITIONS", 10, FontWeights.Bold, Brushes.White);
            stack.Children.Add(header);

            if (Terms == null)
            {
                stack.Children.Add(MakeText(
                    "[Terms VM not initialized]",
                    9, FontWeights.Normal, RedBrush,
                    fontStyle: FontStyles.Italic));
                return stack;
            }

            if (Terms.Clauses == null || Terms.Clauses.Count == 0)
            {
                stack.Children.Add(MakeText(
                    "No terms configured. Add clauses in the Terms editor above.",
                    9, FontWeights.Normal, MutedBrush,
                    fontStyle: FontStyles.Italic));
                return stack;
            }

            System.Collections.Generic.List<Models.ProformaInvoice.Step7PrintPreview.TermsAndConditions.TermClause> activeClauses;

            try
            {
                activeClauses = Terms.GetActivePrintableClauses().ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[T&C] GetActivePrintableClauses failed: {ex}");
                stack.Children.Add(MakeText(
                    $"[Error loading clauses: {ex.Message}]",
                    9, FontWeights.Normal, RedBrush,
                    fontStyle: FontStyles.Italic));
                return stack;
            }

            if (activeClauses.Count == 0)
            {
                stack.Children.Add(MakeText(
                    $"No active clauses. ({Terms.Clauses.Count} total, all inactive — tick checkboxes in the Terms editor above.)",
                    9, FontWeights.Normal, MutedBrush,
                    fontStyle: FontStyles.Italic));
                return stack;
            }

            foreach (var clause in activeClauses)
            {
                try
                {
                    var clauseBox = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

                    clauseBox.Children.Add(MakeText(
                        $"{clause.Order}. {(clause.Title ?? "").ToUpper()}",
                        9, FontWeights.Bold, DarkBrush,
                        marginBottom: 2));

                    if (!string.IsNullOrWhiteSpace(clause.Body))
                    {
                        clauseBox.Children.Add(MakeText(
                            clause.Body,
                            9, FontWeights.Normal, DarkBrush,
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
                            bulletRow.Children.Add(MakeText("• ", 9, FontWeights.Bold, AccentBrush));
                            bulletRow.Children.Add(MakeText(line, 9, FontWeights.Normal, DarkBrush));
                            clauseBox.Children.Add(bulletRow);
                        }
                    }

                    stack.Children.Add(clauseBox);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[T&C] Failed to render clause {clause?.Order}: {ex}");
                }
            }

            return stack;
        }

        private StackPanel BuildTermsFooter()
        {
            var s = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            s.Children.Add(MakeText(FooterDisclaimer, 8, FontWeights.Normal, MutedBrush, fontStyle: FontStyles.Italic));

            var terms = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            terms.Children.Add(MakeText("Terms: ", 8, FontWeights.Normal, MutedBrush, fontStyle: FontStyles.Italic));
            terms.Children.Add(MakeText(TermsText, 8, FontWeights.Normal, MutedBrush, fontStyle: FontStyles.Italic));
            s.Children.Add(terms);

            return s;
        }

        private Grid BuildSignatureBlocks(double width)
        {
            var g = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var pbBox = new Border
            {
                BorderBrush = LineBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(10, 8, 10, 8)
            };
            var pbStack = new StackPanel();
            pbStack.Children.Add(MakeText("PREPARED BY (ERP AUTHENTICATED)", 9, FontWeights.Bold, AccentBrush, marginBottom: 4));

            // Phase 7.16a: read live PreparedByName which auto-falls-back to Client.DocumentPreparedBy
            string nameForPrint = string.IsNullOrWhiteSpace(PreparedByName) ? "—" : PreparedByName;
            pbStack.Children.Add(MakeText(nameForPrint, 10, FontWeights.SemiBold, DarkBrush));

            if (!string.IsNullOrWhiteSpace(PreparedByDept))
            {
                pbStack.Children.Add(MakeText(PreparedByDept, 9, FontWeights.Normal, MutedBrush, marginTop: 2));
            }

            var status = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
            status.Children.Add(MakeText("Status: ", 9, FontWeights.Normal, MutedBrush));
            // Phase 7.16a: read live PreparedByStatus which auto-falls-back to Company.ProformaStatus
            string statusForPrint = string.IsNullOrWhiteSpace(PreparedByStatus) ? "Draft" : PreparedByStatus;
            status.Children.Add(MakeText(statusForPrint, 9, FontWeights.Bold, GreenBrush));
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
            csStack.Children.Add(MakeText("CLIENT APPROVAL SIGN & MOB NUMBER", 9, FontWeights.Bold, AccentBrush, marginBottom: 4));

            var sign = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
            sign.Children.Add(MakeText("Approval Sign:", 9, FontWeights.Normal, MutedBrush));
            sign.Children.Add(new Border
            {
                Background = LineBrush,
                Height = 1,
                MinWidth = 140,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(6, 0, 0, 3)
            });
            csStack.Children.Add(sign);

            var mob = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
            mob.Children.Add(MakeText("Mobile Number:", 9, FontWeights.Normal, MutedBrush));
            mob.Children.Add(new Border
            {
                Background = LineBrush,
                Height = 1,
                MinWidth = 140,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(6, 0, 0, 3)
            });
            csStack.Children.Add(mob);

            csStack.Children.Add(MakeText("Authorized Name & Stamp", 9, FontWeights.Normal, MutedBrush, marginTop: 4));

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
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 0) };
            row.Children.Add(MakeText(label, 9, FontWeights.Normal, MutedBrush));
            row.Children.Add(MakeText(string.IsNullOrWhiteSpace(value) ? "—" : value, 9, FontWeights.SemiBold, DarkBrush));
            parent.Children.Add(row);
        }

        private void AddSummaryLine(Panel parent, string label, string value, FontWeight weight, double size = 9, Brush brush = null)
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var b = brush ?? DarkBrush;
            var lbl = MakeText(label, size, weight, b);
            Grid.SetColumn(lbl, 0); g.Children.Add(lbl);

            var val = MakeText(value, size, weight, b, horizontalAlignment: HorizontalAlignment.Right);
            Grid.SetColumn(val, 1); g.Children.Add(val);

            parent.Children.Add(g);
        }

        private Grid MakeGridWithColumns(double[] widths)
        {
            var g = new Grid();
            foreach (var w in widths)
            {
                if (w > 5)
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
                else
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });
            }
            return g;
        }

        private void AddTableCell(Grid grid, int col, string text, FontWeight weight, HorizontalAlignment align)
        {
            var cell = MakeText(text, 9, weight, DarkBrush, horizontalAlignment: align);
            cell.Padding = new Thickness(6, 4, 6, 4);
            cell.VerticalAlignment = VerticalAlignment.Center;
            cell.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(cell, col);
            grid.Children.Add(cell);
        }

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
                FontFamily = new FontFamily(FONT_FAMILY),
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

            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
                ApplyVectorTextRendering(VisualTreeHelper.GetChild(element, i));
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