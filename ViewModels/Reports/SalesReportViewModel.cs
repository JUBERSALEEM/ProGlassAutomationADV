// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/SalesReportViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Hub (replaces Chat 16 shell)
//
// PURPOSE:
//   Central Reports hub: owns the read-only report ViewModels. All report
//   ViewModels share ONE IReportingDataService instance (central
//   data-intelligence layer). The interactive Statement Generator
//   (list ↔ wizard) moved out to its own module (StatementModuleViewModel)
//   in the Dispatch section.
//
// ARCHITECTURE:
//   MainWindow → SalesReportViewModel (hub) → ActiveReportViewModel
//   Each report is a separate module with its own purpose/layout but
//   reuses this centralized service for relationships/joins/filtering/
//   aggregation/row-level data/spec matching/quantity reconciliation.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.CompanyReport;
using ProGlassAutomation.ViewModels.Reports.CustomerReport;
using ProGlassAutomation.ViewModels.Reports.DeliveryReport;
using ProGlassAutomation.ViewModels.Reports.GlassSpecReport;
using ProGlassAutomation.ViewModels.Reports.ProjectReport;
using ProGlassAutomation.ViewModels.Reports.JoReport;
using ProGlassAutomation.ViewModels.Reports.PiReport;
using ProGlassAutomation.ViewModels.Reports.ProfitabilityReport;
using ProGlassAutomation.ViewModels.Reports.SalesmanReport;
using ProGlassAutomation.ViewModels.Reports.StatementReport;
using ProGlassAutomation.ViewModels.Reports.TaxInvoiceReport;
using ProGlassAutomation.ViewModels.Reports.VatSubmission;
using System;
using System.Collections.Generic;
using System.Windows.Input;

using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using ProGlassAutomation.Services.Database;
namespace ProGlassAutomation.ViewModels.Reports
{
    public class SalesReportViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogs = null!;
        private readonly IReportingDataService _reporting = null!;
        private readonly IDatabaseServices _database = null!;

        public SalesReportViewModel(IDialogService? dialogs,
            IReportingDataService? reporting,
            IDatabaseServices? database)
        {
            _dialogs = dialogs!;
            _reporting = reporting!;
            _database = database!;
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

        // Not 'readonly': assigned in InitializeCore(), and C# only allows
        // readonly fields to be assigned in a declaration or a constructor
        // (CS0191). Still assigned exactly once, on the single construction
        // path, before any consumer runs.
        private IReportingDataService _reportingService;

        // ── Central reporting modules ─────────────────────────────────────
        public PiReportViewModel PiReport { get; private set; }
        public CustomerReportViewModel CustomerReport { get; private set; }
        public SalesmanReportViewModel SalesmanReport { get; private set; }
        public CompanyReportViewModel CompanyReport { get; private set; }
        public ProjectReportViewModel ProjectReport { get; private set; }
        public GlassSpecReportViewModel GlassSpecReport { get; private set; }
        public ProfitabilityReportViewModel ProfitabilityReport { get; private set; }
        public JoReportViewModel JoReport { get; private set; }
        public DeliveryReportViewModel DeliveryReport { get; private set; }
        public TaxInvoiceReportViewModel TaxInvoiceReport { get; private set; }
        public StatementReportViewModel StatementReport { get; private set; }
        public VatSubmissionViewModel VatSubmission { get; private set; }

        // ── Hub navigation ────────────────────────────────────────────────
        /// <summary>
        /// One card in the Reports hub rail.
        ///
        /// TitleKey/SubtitleKey are LOCALIZATION RESOURCE KEYS, not literal
        /// text. The XAML resolves them through ResourceKeyToStringConverter
        /// so these data-driven cards keep live language switching — binding
        /// literal strings here would silently leave the rail in English
        /// while the rest of the app translated.
        ///
        /// Group lets the rail render its "Documents" separator from data
        /// instead of a hand-placed TextBlock between two copied blocks.
        /// </summary>
        public class ReportNavItem
        {
            public string Key { get; set; } = "";
            public string TitleKey { get; set; } = "";
            public string Icon { get; set; } = "";
            public string SubtitleKey { get; set; } = "";
            public string Group { get; set; } = "";
        }

        // SINGLE SOURCE OF TRUTH for the hub rail (2026-09-02).
        //
        // This collection already existed but was DEAD CODE — nothing bound
        // to it, and the view instead hand-copied one ~26-line <Button> block
        // per report. The two drifted, which is exactly the failure mode of
        // hardcoded cards: the list below has SEVEN entries but the view only
        // ever rendered SIX. "VatSubmission" is a fully implemented report
        // (it has a title, a view model and a nav target) that no button
        // could reach. Binding the rail to this list both removes the
        // duplication and restores access to that report.
        public List<ReportNavItem> NavItems { get; } = new()
        {
            new ReportNavItem{ Key="Overview",   Icon="\uE9D2", TitleKey="Nav.Overview",           SubtitleKey="Reports.KPIDashboard" },
            new ReportNavItem{ Key="Customer",   Icon="\uE716", TitleKey="Reports.Nav.Customers", SubtitleKey="Reports.Nav.CustomersSub",   Group="Reports.Relationships" },
            new ReportNavItem{ Key="Salesman",   Icon="\uE77B", TitleKey="Reports.Nav.Salesmen",  SubtitleKey="Reports.Nav.SalesmenSub" },
            new ReportNavItem{ Key="Company",    Icon="\uE821", TitleKey="Reports.Nav.Companies", SubtitleKey="Reports.Nav.CompaniesSub" },
            new ReportNavItem{ Key="Project",    Icon="\uE8A5", TitleKey="Reports.Nav.Projects",  SubtitleKey="Reports.Nav.ProjectsSub" },
            new ReportNavItem{ Key="Pi",         Icon="\uE8A5", TitleKey="Module.ProformaInvoice", SubtitleKey="Reports.QuotesConversion",   Group="Reports.Documents" },
            new ReportNavItem{ Key="Jo",         Icon="\uE8B7", TitleKey="Common.JobOrder",        SubtitleKey="Reports.ProductionStatus" },
            new ReportNavItem{ Key="Delivery",   Icon="\uE814", TitleKey="Nav.Delivery",           SubtitleKey="Reports.DispatchedPending" },
            new ReportNavItem{ Key="TaxInvoice", Icon="\uE8C7", TitleKey="Module.TaxInvoice",      SubtitleKey="Reports.BilledOverdue" },
            new ReportNavItem{ Key="GlassSpec",  Icon="\uE7C1", TitleKey="Reports.Nav.GlassSpecs", SubtitleKey="Reports.Nav.GlassSpecsSub" },
            new ReportNavItem{ Key="Profit",     Icon="\uE9F9", TitleKey="Reports.Nav.Profitability", SubtitleKey="Reports.Nav.ProfitabilitySub", Group="Reports.Financial" },
            new ReportNavItem{ Key="Statement",  Icon="\uE9F7", TitleKey="Module.Statement",       SubtitleKey="Reports.CustomerStatements" },
            new ReportNavItem{ Key="VatSubmission", Icon="\uE8C7", TitleKey="Nav.VatSubmission",   SubtitleKey="Reports.VatSubmission.5FTARate" },
        };

        private string _activeKey = "Overview";
        public string ActiveKey { get => _activeKey; set { if (_activeKey == value) return; _activeKey = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActiveViewModel)); OnPropertyChanged(nameof(IsOverview)); OnPropertyChanged(nameof(CurrentReportTitle)); } }

        // ── Collapsible navigation rail (hamburger) ───────────────────────
        // Same pattern as the main window's menu toggle: the rail collapses
        // so the open report uses the full display width.
        private bool _isRailOpen = true;
        public bool IsRailOpen
        {
            get => _isRailOpen;
            set { if (_isRailOpen == value) return; _isRailOpen = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsRailCollapsed)); }
        }
        public bool IsRailCollapsed => !_isRailOpen;
        public ICommand ToggleRailCommand { get; private set; } = null!;

        /// <summary>Human title of the active report, for the module header strip.</summary>
        public string CurrentReportTitle => _activeKey switch
        {
            "Customer" => "Customer Directory & Receivables",
            "Salesman" => "Salesman Performance",
            "Company" => "Company Book & Receivables",
            "Project" => "Project Register & Progress",
            "Pi" => "Proforma Invoice Report",
            "Jo" => "Job Order Report",
            "Delivery" => "Delivery Note Report",
            "TaxInvoice" => "Tax Invoice Report",
            "GlassSpec" => "Glass Specifications & Traceability",
            "Profit" => "Profitability & P&L",
            "Statement" => "Statement Report",
            "VatSubmission" => "VAT Submission Report",
            _ => "Reports Overview"
        };

        public ViewModelBase? ActiveViewModel => _activeKey switch
        {
            "Customer" => CustomerReport,
            "Salesman" => SalesmanReport,
            "Company" => CompanyReport,
            "Project" => ProjectReport,
            "Pi" => PiReport,
            "Jo" => JoReport,
            "Delivery" => DeliveryReport,
            "TaxInvoice" => TaxInvoiceReport,
            "GlassSpec" => GlassSpecReport,
            "Profit" => ProfitabilityReport,
            "Statement" => StatementReport,
            "VatSubmission" => VatSubmission,
            _ => null
        };

        public bool IsOverview => _activeKey == "Overview";
        public ICommand NavigateReportCommand { get; private set; }

        // ── Open-original-document callbacks ──────────────────────────────
        // Wired by MainViewModel so a report sidebar can jump to the REAL
        // PI / JO / TI (the module owns the editor; Reports never duplicates
        // it). Each report VM raises these with the row's DB id.
        public Action<int>? OnOpenProforma { get; set; }
        public Action<int>? OnOpenJobOrder { get; set; }
        public Action<int>? OnOpenTaxInvoice { get; set; }

        // Delivery / Statement rows carry a document REFERENCE (string) rather
        // than a DB id, so they route through by-number callbacks that the
        // shell resolves before navigating.
        public Action<string>? OnOpenJobOrderByNumber { get; set; }
        public Action<string>? OnOpenTaxInvoiceByNumber { get; set; }

        public SalesReportViewModel()
         : this(
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<IReportingDataService>()!,
            App.ServiceProvider?.GetService<IDatabaseServices>()!){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            _reportingService = _reporting ?? new ReportingDataService();

            ToggleRailCommand = new RelayCommand(_ => IsRailOpen = !IsRailOpen);

            PiReport = new PiReportViewModel(_dialogs, _reportingService, _database);
            CustomerReport = new CustomerReportViewModel(_dialogs, _reportingService, _database);
            SalesmanReport = new SalesmanReportViewModel(_dialogs, _reportingService, _database);
            CompanyReport = new CompanyReportViewModel(_dialogs, _reportingService, _database);
            ProjectReport = new ProjectReportViewModel(_dialogs, _reportingService, _database);
            GlassSpecReport = new GlassSpecReportViewModel(_dialogs, _reportingService, _database);
            ProfitabilityReport = new ProfitabilityReportViewModel(_dialogs, _reportingService, _database);
            JoReport = new JoReportViewModel(_dialogs, _reportingService, _database);
            DeliveryReport = new DeliveryReportViewModel(_dialogs, _reportingService, _database);
            TaxInvoiceReport = new TaxInvoiceReportViewModel(_dialogs, _reportingService, _database);
            StatementReport = new StatementReportViewModel(_dialogs, _reportingService, _database);
            VatSubmission = new VatSubmissionViewModel(_dialogs, _reportingService, _database);

            // Route "open original document" requests from report sidebars to
            // the shell callbacks (MainViewModel wires OnOpenProforma/...).
            PiReport.OpenDocumentRequested = id => OnOpenProforma?.Invoke(id);
            JoReport.OpenDocumentRequested = id => OnOpenJobOrder?.Invoke(id);
            TaxInvoiceReport.OpenDocumentRequested = id => OnOpenTaxInvoice?.Invoke(id);
            DeliveryReport.OpenJobOrderRequested = joId => OnOpenJobOrderByNumber?.Invoke(joId);
            StatementReport.OpenTaxInvoiceRequested = invNum => OnOpenTaxInvoiceByNumber?.Invoke(invNum);

            NavigateReportCommand = new RelayCommand(p =>
            {
                if (p is string key && !string.IsNullOrWhiteSpace(key))
                {
                    ActiveKey = key;
                    OnPropertyChanged(nameof(IsOverview));
                    // 2026-09-05 owner directive: choosing a report from the
                    // rail CLOSES the rail automatically so the report gets
                    // the full module width (drawer behaviour; re-opens via
                    // the floating menu button or the header toggle).

                    // Statements can be generated in the Dispatch section now;
                    // refresh the read-only Statement report when it opens so it
                    // reflects the latest generated statements.
                    if (key == "Statement")
                        _ = StatementReport.LoadAsync();

                    // Customer & Salesman reports merge the master-data roster
                    // with live analytics; reload on open so a client/salesman
                    // saved since the hub was built shows immediately.
                    if (key == "Customer")
                        _ = CustomerReport.LoadAsync();
                    if (key == "Salesman")
                        _ = SalesmanReport.LoadAsync();
                    if (key == "Company")
                        _ = CompanyReport.LoadAsync();
                    if (key == "Project")
                        _ = ProjectReport.LoadAsync();
                    if (key == "GlassSpec")
                        _ = GlassSpecReport.LoadAsync();
                    if (key == "Profit")
                        _ = ProfitabilityReport.LoadAsync();

                    IsRailOpen = false; // auto-close (see above)
                }
            });

            _ = StatementReport.LoadAsync();

            // LIVE LANGUAGE SWITCH for the data-driven hub rail.
            //
            // The rail's labels are resource KEYS resolved by
            // ResourceKeyToStringConverter at bind time. A converter does not
            // re-run when the merged language dictionary is swapped, so the
            // rail would keep the old language until the view was rebuilt.
            // Re-raising the collection's change notification forces every
            // item binding — and therefore every converter call — to
            // re-evaluate.
            //
            // The hand-copied cards this replaced used {DynamicResource}
            // directly, which WPF refreshed automatically; this restores that
            // behaviour for the data-driven version.
            Services.Localization.LocalizationService.Instance.LanguageChanged
                += (_, __) => OnPropertyChanged(nameof(NavItems));
        }
    }
}
