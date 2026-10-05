// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/ExecutiveReport/ExecutiveReportViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Executive Summary (Order-to-Cash Funnel)
//
// SURFACES the one-page cross-module view the engine already computes
// (GetExecutiveSummaryAsync) but which the Reports Hub never exposed — only
// the AI agent could reach it. Every existing report answers ONE module;
// this answers the only question the owner actually asks: "where is work
// getting stuck between quoting and getting paid?"
//
//      Proforma → Job Order → Delivery → Tax Invoice → Paid
//
// All figures come from the same per-module summaries under ONE shared
// filter, so the executive page and the module reports can never disagree.
//
// This report is a single computed summary (not a paged grid), so it does
// NOT extend ReportBaseViewModel; it manages its own filter + load with the
// same cancellation discipline. Export is a formatted plain-text/CSV recap.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.DependencyInjection;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.Reports.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.ExecutiveReport
{
    public class ExecutiveReportViewModel : ViewModelBase
    {
        private readonly IReportingDataService _service;
        private CancellationTokenSource? _cts;

        // Monotonic run id: a stale load (superseded while its await was in
        // flight) must never touch state that belongs to the newer run —
        // previously its finally block cleared IsLoading mid-load and an
        // out-of-date result could land after the fresh one.
        private int _runId;

        public ExecutiveReportViewModel() : this(
            App.ServiceProvider?.GetService<IDialogService>() ?? new DialogService(),
            App.ServiceProvider?.GetService<IReportingDataService>())
        {
        }

        public ExecutiveReportViewModel(IDialogService? dialogs, IReportingDataService? service = null)
        {
            // dialogs is accepted for signature parity with the other report
            // VMs (and future export/share) but not needed yet.
            _ = dialogs;
            _service = service ?? new ReportingDataService();

            RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
            ClearFilterCommand = new RelayCommand(_ =>
            {
                _dateFrom = null; _dateTo = null;
                OnPropertyChanged(nameof(DateFrom)); OnPropertyChanged(nameof(DateTo));
                _ = LoadAsync();
            });

            _ = LoadAsync();
        }

        // ── Filter (date range only — the funnel is a period view) ────
        private DateTime? _dateFrom;
        public DateTime? DateFrom { get => _dateFrom; set { if (_dateFrom == value) return; _dateFrom = value; OnPropertyChanged(); _ = LoadAsync(); } }

        private DateTime? _dateTo;
        public DateTime? DateTo { get => _dateTo; set { if (_dateTo == value) return; _dateTo = value; OnPropertyChanged(); _ = LoadAsync(); } }

        public ICommand RefreshCommand { get; }
        public ICommand ClearFilterCommand { get; }

        // ── State ─────────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading { get => _isLoading; private set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(IsContentVisible)); } }

        private bool _hasError;
        public bool HasError { get => _hasError; private set { if (_hasError == value) return; _hasError = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(IsContentVisible)); } }

        private string _errorMessage = "";
        public string ErrorMessage { get => _errorMessage; private set { _errorMessage = value; OnPropertyChanged(); } }

        private bool _hasData;
        public bool HasData { get => _hasData; private set { if (_hasData == value) return; _hasData = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(IsContentVisible)); } }

        public bool IsEmpty => !IsLoading && !HasError && !HasData;
        public bool IsContentVisible => !IsLoading && !HasError && HasData;

        // ── The summary + projections ─────────────────────────────────
        private ExecutiveSummary _summary = new();
        public ExecutiveSummary Summary
        {
            get => _summary;
            private set
            {
                _summary = value ?? new ExecutiveSummary();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Funnel));
                OnPropertyChanged(nameof(TopSpecifications));
                OnPropertyChanged(nameof(TopCustomers));
                OnPropertyChanged(nameof(PeriodDescription));
                OnPropertyChanged(nameof(HasCostGap));
                OnPropertyChanged(nameof(CostGapNote));
                OnPropertyChanged(nameof(SummaryCards));
                OnPropertyChanged(nameof(HealthCards));
            }
        }

        public string PeriodDescription => _summary.PeriodDescription;
        public ObservableCollection<FunnelStage> Funnel => new(_summary.Funnel);
        public ObservableCollection<MixRow> TopSpecifications => new(_summary.TopSpecifications);
        public ObservableCollection<MixRow> TopCustomers => new(_summary.TopCustomers);

        public bool HasCostGap => _summary.HasCostGap;
        public string CostGapNote =>
            _summary.InvoicesWithoutCost == 0
                ? ""
                : $"{_summary.InvoicesWithoutCost} invoice(s) have no cost data — the margin below is understated until they are costed.";

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("QUOTED", $"AED {_summary.QuotedValue:N0}", "total proforma value", "AccentBrush"),
            new KpiItem("INVOICED", $"AED {_summary.InvoicedValue:N0}", "billed to customers", "InfoBrush"),
            new KpiItem("PROFIT", $"AED {_summary.Profit:N0}", $"{_summary.MarginPct:0.#}% margin", _summary.Profit >= 0 ? "SuccessBrush" : "DangerBrush"),
            new KpiItem("OUTSTANDING", $"AED {_summary.OutstandingValue:N0}", $"{_summary.OverdueInvoices} invoices overdue", "WarningBrush"),
        };

        public ObservableCollection<KpiItem> HealthCards => new()
        {
            new KpiItem("UNCONVERTED", _summary.UnconvertedQuotes.ToString("N0"), "quotes never became jobs", "WarningBrush"),
            new KpiItem("UNINVOICED", _summary.UninvoicedConfirmed.ToString("N0"), "confirmed, not yet billed", "WarningBrush"),
            new KpiItem("DELIVERY", $"{_summary.DeliveryCompletionPct:0.#}%", "completion across period", "InfoBrush"),
            new KpiItem("CRITICAL PENDING", _summary.CriticalPendingQty.ToString("N0"), "pcs past the threshold", "DangerBrush"),
        };

        public async Task LoadAsync()
        {
            int run = ++_runId;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsLoading = true; HasError = false;
            try
            {
                var filter = new ReportingFilter
                {
                    DateFrom = _dateFrom,
                    DateTo = _dateTo
                };
                var summary = await _service.GetExecutiveSummaryAsync(filter, token);
                if (run != _runId || token.IsCancellationRequested) return;

                Summary = summary;
                HasData = summary.HasData;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                if (run != _runId) return;
                HasError = true; ErrorMessage = ExceptionMessages.Deep(ex);
                AppLogService.Warning($"ExecutiveReport LoadAsync error: {ExceptionMessages.Deep(ex)}");
            }
            finally
            {
                // Only the newest run may end the loading state.
                if (run == _runId) IsLoading = false;
            }
        }
    }
}
