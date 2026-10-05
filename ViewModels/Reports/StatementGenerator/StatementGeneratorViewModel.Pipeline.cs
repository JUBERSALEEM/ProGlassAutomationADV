// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.Pipeline.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.3 — StatementGeneratorViewModel partial split. This part owns the
// Pipeline members; fields + constructor live in StatementGeneratorViewModel.cs.
// ═══════════════════════════════════════════════════════════════════════

using PdfSharp.Pdf;
using ProGlassAutomation.Services.Logging;
using PdfSharp.Pdf.IO;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Reconciliation;
using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Statement.Aggregation;
using ProGlassAutomation.Services.Statement.Pdf;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Services.Statement.Workflow;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Views.Reports.Workflow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.Database;

namespace ProGlassAutomation.ViewModels.Reports.StatementGenerator
{
    public partial class StatementGeneratorViewModel : ViewModelBase
    {
        // ═════════════════════════════════════════════════════════════════
        //  SECTION 3 — PIPELINE VISUALIZATION
        // ═════════════════════════════════════════════════════════════════

        public ObservableCollection<WorkflowStepState> LastWorkflowSteps { get; } = new();

        private double _lastWorkflowProgressPercent;
        public double LastWorkflowProgressPercent
        {
            get => _lastWorkflowProgressPercent;
            private set
            {
                if (Math.Abs(_lastWorkflowProgressPercent - value) < 0.01) return;
                _lastWorkflowProgressPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastWorkflowProgressDisplay));
            }
        }

        public string LastWorkflowProgressDisplay
            => $"{(int)Math.Round(_lastWorkflowProgressPercent)}%";

        private string _lastWorkflowStatusText = "Ready to Generate";
        public string LastWorkflowStatusText
        {
            get => _lastWorkflowStatusText;
            private set { if (_lastWorkflowStatusText == value) return; _lastWorkflowStatusText = value; OnPropertyChanged(); }
        }

        private bool _hasWorkflowRun;
        public bool HasWorkflowRun
        {
            get => _hasWorkflowRun;
            private set { if (_hasWorkflowRun == value) return; _hasWorkflowRun = value; OnPropertyChanged(); }
        }

        private bool _isLastWorkflowComplete;
        public bool IsLastWorkflowComplete
        {
            get => _isLastWorkflowComplete;
            private set
            {
                if (_isLastWorkflowComplete == value) return;
                _isLastWorkflowComplete = value;
                OnPropertyChanged();
                // [CHAT 22 v3] Notify navigation dependents
                OnPropertyChanged(nameof(IsStep3Complete));
                OnPropertyChanged(nameof(CanNavigateToStep4));
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  SECTION 4 — PROCESSING LOG + METRICS + WARNINGS
        // ═════════════════════════════════════════════════════════════════

        public ObservableCollection<ProGlassAutomation.Models.Statement.Workflow.LogEntry> ProcessingLog { get; } = new();
        public ObservableCollection<WorkflowWarningItem> WorkflowWarnings { get; } = new();

        private int _orderedQtyMetric;
        public int OrderedQtyMetric
        {
            get => _orderedQtyMetric;
            private set { if (_orderedQtyMetric == value) return; _orderedQtyMetric = value; OnPropertyChanged(); OnPropertyChanged(nameof(OrderedQtyDisplay)); }
        }
        public string OrderedQtyDisplay => $"{_orderedQtyMetric:N0}";

        private int _deliveredQtyMetric;
        public int DeliveredQtyMetric
        {
            get => _deliveredQtyMetric;
            private set { if (_deliveredQtyMetric == value) return; _deliveredQtyMetric = value; OnPropertyChanged(); OnPropertyChanged(nameof(DeliveredQtyDisplay)); }
        }
        public string DeliveredQtyDisplay => $"{_deliveredQtyMetric:N0}";

        private int _balanceQtyMetric;
        public int BalanceQtyMetric
        {
            get => _balanceQtyMetric;
            private set { if (_balanceQtyMetric == value) return; _balanceQtyMetric = value; OnPropertyChanged(); OnPropertyChanged(nameof(BalanceQtyDisplay)); }
        }
        public string BalanceQtyDisplay => $"{_balanceQtyMetric:N0}";

        private double _deliveryRatePercent;
        public double DeliveryRatePercent
        {
            get => _deliveryRatePercent;
            private set { if (Math.Abs(_deliveryRatePercent - value) < 0.01) return; _deliveryRatePercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(DeliveryRateDisplay)); }
        }
        public string DeliveryRateDisplay
            => _deliveredQtyMetric == 0 && _orderedQtyMetric == 0
                ? "—"
                : $"{_deliveryRatePercent:N1}%";

        private int _warningCount;
        public int WarningCount
        {
            get => _warningCount;
            private set { if (_warningCount == value) return; _warningCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(WarningSummaryDisplay)); OnPropertyChanged(nameof(HasWarnings)); }
        }

        private int _errorCount;
        public int ErrorCount
        {
            get => _errorCount;
            private set { if (_errorCount == value) return; _errorCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(WarningSummaryDisplay)); OnPropertyChanged(nameof(HasWarnings)); }
        }

        private int _infoCount;
        public int InfoCount
        {
            get => _infoCount;
            private set { if (_infoCount == value) return; _infoCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(WarningSummaryDisplay)); OnPropertyChanged(nameof(HasWarnings)); }
        }

        public string WarningSummaryDisplay
        {
            get
            {
                var parts = new List<string>(3);
                if (_errorCount > 0) parts.Add($"{_errorCount} error{(_errorCount == 1 ? "" : "s")}");
                if (_warningCount > 0) parts.Add($"{_warningCount} warning{(_warningCount == 1 ? "" : "s")}");
                if (_infoCount > 0) parts.Add($"{_infoCount} info");
                return parts.Count == 0 ? "No warnings" : string.Join(" · ", parts);
            }
        }

        /// <summary>True when the last run produced warnings or errors — drives
        /// the Review step's banner (warning state vs clean success).</summary>
        public bool HasWarnings => _warningCount > 0 || _errorCount > 0;

        // ── Last PDF metadata ────────────────────────────────────────────

        private string _lastPdfPath = "";
        public string LastPdfPath
        {
            get => _lastPdfPath;
            private set
            {
                if (_lastPdfPath == value) return;
                _lastPdfPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastPdfFileName));
                OnPropertyChanged(nameof(HasLastPdf));
                // [CHAT 22 v3] Notify Step 4 navigation gate
                OnPropertyChanged(nameof(IsStep4Complete));
                OnPropertyChanged(nameof(CanNavigateToStep4));
            }
        }

        public string LastPdfFileName
            => string.IsNullOrWhiteSpace(_lastPdfPath) ? "" : Path.GetFileName(_lastPdfPath);

        private string _lastPdfSize = "";
        public string LastPdfSize
        {
            get => _lastPdfSize;
            private set { if (_lastPdfSize == value) return; _lastPdfSize = value; OnPropertyChanged(); }
        }

        private int _lastPdfPageCount;
        public int LastPdfPageCount
        {
            get => _lastPdfPageCount;
            private set { if (_lastPdfPageCount == value) return; _lastPdfPageCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastPdfPageInfo)); }
        }

        public string LastPdfPageInfo
            => _lastPdfPageCount == 0 ? "" : $"{_lastPdfPageCount:N0} page{(_lastPdfPageCount == 1 ? "" : "s")}";

        private double _lastPdfGenerationSec;
        public double LastPdfGenerationSec
        {
            get => _lastPdfGenerationSec;
            private set { if (Math.Abs(_lastPdfGenerationSec - value) < 0.01) return; _lastPdfGenerationSec = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastPdfGenerationDisplay)); }
        }

        public string LastPdfGenerationDisplay
            => _lastPdfGenerationSec <= 0 ? "" : $"Generated in {_lastPdfGenerationSec:N1}s";

        private DateTime _lastPdfGeneratedAt;
        public DateTime LastPdfGeneratedAt
        {
            get => _lastPdfGeneratedAt;
            private set { _lastPdfGeneratedAt = value; OnPropertyChanged(); }
        }

        public bool HasLastPdf =>
            !string.IsNullOrWhiteSpace(_lastPdfPath) && File.Exists(_lastPdfPath);

        // ═════════════════════════════════════════════════════════════════
        //  SECTION 6 — PDF SECTION BROWSER
        // ═════════════════════════════════════════════════════════════════

        public ObservableCollection<PdfSection> MergedPdfSections { get; } = new();
        public ObservableCollection<PdfSection> BalancePdfSections { get; } = new();
        public ObservableCollection<PdfSection> AlertsPdfSections { get; } = new();

        public int MergedSectionCount => MergedPdfSections.Count;
        public int BalanceSectionCount => BalancePdfSections.Count;
        public int AlertsSectionCount => AlertsPdfSections.Count;

        // ── Deep-detail result data (Review step) ─────────────────────────
        // Populated in LiftWorkflowResultAfterCompletion so the Review step can
        // show REAL computed output — financials, aging, per-invoice rows and
        // per-PI line-item reconciliation — instead of static estimates.

        /// <summary>The completed statement snapshot (financials, aging, rows).</summary>
        private StatementSnapshot? _lastSnapshot;
        public StatementSnapshot? LastSnapshot
        {
            get => _lastSnapshot;
            private set
            {
                _lastSnapshot = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasLastSnapshot));
                OnPropertyChanged(nameof(LastFinancial));
                OnPropertyChanged(nameof(LastAging));
                OnPropertyChanged(nameof(LastCustomer));
                OnPropertyChanged(nameof(LastHeader));
            }
        }

        public bool HasLastSnapshot => _lastSnapshot != null;
        public StatementFinancialSummary LastFinancial =>
            _lastSnapshot?.Financial ?? new StatementFinancialSummary();
        public StatementAgingBuckets LastAging =>
            _lastSnapshot?.Aging ?? new StatementAgingBuckets();
        public StatementCustomerInfo LastCustomer =>
            _lastSnapshot?.CustomerInfo ?? new StatementCustomerInfo();
        public StatementHeader LastHeader =>
            _lastSnapshot?.Header ?? new StatementHeader();

        /// <summary>Per-PI line-item reconciliation (real, computed in Step 6).</summary>
        public ObservableCollection<OrderReconciliationSummary> LastReconciliationSummaries { get; } = new();

        /// <summary>
        /// Flattened line items that ARE part of the statement — delivered and/or
        /// invoiced (Included in Statement).</summary>
        public ObservableCollection<OrderReconciliationRow> LastIncludedLines { get; } = new();

        /// <summary>
        /// Flattened line items still DUE — not yet delivered and not yet
        /// tax-invoiced (Balance). These are NOT in the statement yet.</summary>
        public ObservableCollection<OrderReconciliationRow> LastDueLines { get; } = new();

        /// <summary>Grouped "included" lines by specification (spec header → rows).</summary>
        public ObservableCollection<ReconciliationSpecGroup> IncludedGroups { get; } = new();

        /// <summary>Grouped "due" lines by specification (spec header → rows).</summary>
        public ObservableCollection<ReconciliationSpecGroup> DueGroups { get; } = new();

        public int IncludedTotalQty => IncludedGroups.Sum(g => g.TotalOrderedQty);
        public decimal IncludedTotalSqm => IncludedGroups.Sum(g => g.TotalSqm);
        public int DueTotalQty => DueGroups.Sum(g => g.TotalOrderedQty);
        public decimal DueTotalSqm => DueGroups.Sum(g => g.TotalSqm);

        /// <summary>True when at least one PI has a billing discrepancy.</summary>
        public bool LastHasReconciliationIssue =>
            LastReconciliationSummaries.Any(s => s.HasDiscrepancies);

        /// <summary>First Job Order id referenced by the reconciliation (for the
        /// "View Job Order / Prepare Delivery Note" actions).</summary>
        public string FirstReconciliationJobOrderId
        {
            get
            {
                foreach (var s in LastReconciliationSummaries)
                {
                    var ids = (s.JobOrderIds ?? "")
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var id in ids)
                    {
                        var t = id.Trim();
                        if (!string.IsNullOrWhiteSpace(t)) return t;
                    }
                }
                return "";
            }
        }

        public bool HasReconciliationJobOrder =>
            !string.IsNullOrWhiteSpace(FirstReconciliationJobOrderId);

        // ── Reconciliation tab state (Included vs Due) ───────────────────
        private string _activeReconciliationTab = "Included";
        public string ActiveReconciliationTab
        {
            get => _activeReconciliationTab;
            set
            {
                if (_activeReconciliationTab == value) return;
                _activeReconciliationTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowIncludedReconciliation));
                OnPropertyChanged(nameof(ShowDueReconciliation));
            }
        }
        public bool ShowIncludedReconciliation => _activeReconciliationTab == "Included";
        public bool ShowDueReconciliation => _activeReconciliationTab == "Due";
        public ICommand SelectReconciliationTabCommand { get; private set; } = null!;

        private string _activePdfTab = "Merged";
        public string ActivePdfTab
        {
            get => _activePdfTab;
            set
            {
                if (_activePdfTab == value) return;
                _activePdfTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowMergedTab));
                OnPropertyChanged(nameof(ShowBalanceTab));
                OnPropertyChanged(nameof(ShowAlertsTab));
            }
        }

        public bool ShowMergedTab => _activePdfTab == "Merged";
        public bool ShowBalanceTab => _activePdfTab == "Balance";
        public bool ShowAlertsTab => _activePdfTab == "Alerts";

        // ═════════════════════════════════════════════════════════════════
        //  SECTION 7 — FOOTER
        // ═════════════════════════════════════════════════════════════════

        public bool IsEngineOperational => true;
        public string EngineStatusDisplay => "Engine Operational";

    }
}
