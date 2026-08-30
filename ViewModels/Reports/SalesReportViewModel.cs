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
using ProGlassAutomation.ViewModels.Reports.DeliveryReport;
using ProGlassAutomation.ViewModels.Reports.JoReport;
using ProGlassAutomation.ViewModels.Reports.PiReport;
using ProGlassAutomation.ViewModels.Reports.StatementReport;
using ProGlassAutomation.ViewModels.Reports.TaxInvoiceReport;
using ProGlassAutomation.ViewModels.Reports.VatSubmission;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports
{
    public class SalesReportViewModel : ViewModelBase
    {
        private readonly IReportingDataService _reportingService;

        // ── Central reporting modules ─────────────────────────────────────
        public PiReportViewModel PiReport { get; }
        public JoReportViewModel JoReport { get; }
        public DeliveryReportViewModel DeliveryReport { get; }
        public TaxInvoiceReportViewModel TaxInvoiceReport { get; }
        public StatementReportViewModel StatementReport { get; }
        public VatSubmissionViewModel VatSubmission { get; }

        // ── Hub navigation ────────────────────────────────────────────────
        public class ReportNavItem
        {
            public string Key { get; set; } = "";
            public string Title { get; set; } = "";
            public string Icon { get; set; } = "";
            public string Subtitle { get; set; } = "";
        }

        public List<ReportNavItem> NavItems { get; } = new()
        {
            new ReportNavItem{ Key="Overview", Title="Overview", Icon="\uE9D2", Subtitle="KPI dashboard" },
            new ReportNavItem{ Key="Pi", Title="Proforma Invoice", Icon="\uE8A5", Subtitle="Quotes & conversion" },
            new ReportNavItem{ Key="Jo", Title="Job Order", Icon="\uE8B7", Subtitle="Production & status" },
            new ReportNavItem{ Key="Delivery", Title="Delivery", Icon="\uE814", Subtitle="Dispatched / pending" },
            new ReportNavItem{ Key="TaxInvoice", Title="Tax Invoice", Icon="\uE8C7", Subtitle="Billed & overdue" },
            new ReportNavItem{ Key="Statement", Title="Statement", Icon="\uE9F7", Subtitle="Customer statements" },
            new ReportNavItem{ Key="VatSubmission", Title="VAT Submission", Icon="\uE8C7", Subtitle="5% VAT for FTA" },
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
            "Pi" => "Proforma Invoice Report",
            "Jo" => "Job Order Report",
            "Delivery" => "Delivery Note Report",
            "TaxInvoice" => "Tax Invoice Report",
            "Statement" => "Statement Report",
            "VatSubmission" => "VAT Submission Report",
            _ => "Reports Overview"
        };

        public ViewModelBase? ActiveViewModel => _activeKey switch
        {
            "Pi" => PiReport,
            "Jo" => JoReport,
            "Delivery" => DeliveryReport,
            "TaxInvoice" => TaxInvoiceReport,
            "Statement" => StatementReport,
            "VatSubmission" => VatSubmission,
            _ => null
        };

        public bool IsOverview => _activeKey == "Overview";
        public ICommand NavigateReportCommand { get; }

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
        {
            _reportingService = App.Reporting ?? new ReportingDataService();

            ToggleRailCommand = new RelayCommand(_ => IsRailOpen = !IsRailOpen);

            PiReport = new PiReportViewModel(_reportingService);
            JoReport = new JoReportViewModel(_reportingService);
            DeliveryReport = new DeliveryReportViewModel(_reportingService);
            TaxInvoiceReport = new TaxInvoiceReportViewModel(_reportingService);
            StatementReport = new StatementReportViewModel(_reportingService);
            VatSubmission = new VatSubmissionViewModel(_reportingService);

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
                }
            });

            _ = StatementReport.LoadAsync();
        }
    }
}
