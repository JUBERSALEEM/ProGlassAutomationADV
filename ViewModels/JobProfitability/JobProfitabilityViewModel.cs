// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/JobProfitability/JobProfitabilityViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Job Profitability (PHASE C)
//
// WHAT IT IS
//   A portfolio view of the Step 9 Profit & Loss data. Step 9 answers
//   "is THIS invoice profitable?"; this module answers "which confirmed
//   jobs, customers and salesmen are making us money?".
//
// DATA SOURCE
//   ONLY proforma invoices whose stored Status is "Approved" are read —
//   that is the exact value the Saved PI list persists when the operator
//   marks a PI as "PI Confirmed". Draft ("PI created"), Sent ("PI sent"),
//   Rejected, Cancelled and Expired rows are never included, so the
//   report shows only the business the customer actually confirmed.
//
//   Every figure on a row comes straight from the persisted Step 9 P&L:
//   NetExVat, Profit and MarginPercent are Step 9's own numbers; cost is
//   Step 9's TotalGlassCost + TotalProductionCost. No margin is recomputed
//   from the invoice header, and no VAT divisor lives here.
//
// FILTERS
//   CompanyFilter / SalesmanFilter / CustomerFilter — "All" plus every
//   distinct value found among CONFIRMED PIs
//   SearchText — matches proforma ID, customer or project (case-insensitive)
//   Totals and the loss count reflect the FILTERED set, so drilling into
//   one salesman immediately re-totals for that salesman. Uncosted rows
//   stay visible (greyed) but are EXCLUDED from totals: including their
//   revenue with zero profit is what used to dilute the overall margin.
//
// SAFETY: LoadAsync never throws. A DB failure leaves the list empty and
// says so in StatusMessage.
// ═══════════════════════════════════════════════════════════════════════
using ProGlassAutomation.Commands;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Models.ProformaInvoice.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.JobProfitability
{
    public class JobProfitabilityViewModel : ViewModelBase
    {
        private readonly IProformaInvoiceRepository _invoices = null!;

        // Portfolio analysis ViewModels (Phase 1-3)
        public PortfolioServiceProfitabilityViewModel PortfolioServiceProfitability { get; }
        public PortfolioWastageAnalysisViewModel PortfolioWastageAnalysis { get; }
        public PortfolioGlassTypeComparisonViewModel PortfolioGlassTypeComparison { get; }

        /// <summary>Detail sidebar VM — loads full P&L breakdown for selected row.</summary>
        public JobDetailViewModel JobDetail { get; }

        public JobProfitabilityViewModel(IProformaInvoiceRepository? invoices)
        {
            _invoices = invoices!;
        
            // Initialize portfolio analysis VMs
            PortfolioServiceProfitability = new PortfolioServiceProfitabilityViewModel(_invoices);
            PortfolioWastageAnalysis = new PortfolioWastageAnalysisViewModel(_invoices);
            PortfolioGlassTypeComparison = new PortfolioGlassTypeComparisonViewModel(_invoices);

            // Initialize detail sidebar VM
            JobDetail = new JobDetailViewModel(_invoices);

            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

        public string Title { get; } = "JOB PROFITABILITY";

        public string Subtitle { get; } =
            "Profit, cost and margin for PI-confirmed proformas only — " +
            "figures are read straight from the Step 9 Profit & Loss snapshot.";

        /// <summary>Sentinel used by both filter dropdowns.</summary>
        public const string AllFilter = "All";

        // Full unfiltered set; Rows is the filtered projection of this.
        private readonly List<JobProfitabilityRow> _allRows = new();

        /// <summary>Filtered rows shown in the list.</summary>
        public BulkObservableCollection<JobProfitabilityRow> Rows { get; } = new();

        /// <summary>Distinct companies, "All" first.</summary>
        public ObservableCollection<string> CompanyFilterOptions { get; } = new();

        /// <summary>Distinct salesmen, "All" first.</summary>
        public ObservableCollection<string> SalesmanFilterOptions { get; } = new();

        /// <summary>Distinct customers, "All" first.</summary>
        public ObservableCollection<string> CustomerFilterOptions { get; } = new();

        // ── Filters ───────────────────────────────────────────────────────

        private string _companyFilter = AllFilter;
        public string CompanyFilter
        {
            get => _companyFilter;
            set
            {
                if (_companyFilter == value) return;
                _companyFilter = value ?? AllFilter;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        private string _salesmanFilter = AllFilter;
        public string SalesmanFilter
        {
            get => _salesmanFilter;
            set
            {
                if (_salesmanFilter == value) return;
                _salesmanFilter = value ?? AllFilter;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        private string _customerFilter = AllFilter;
        public string CustomerFilter
        {
            get => _customerFilter;
            set
            {
                if (_customerFilter == value) return;
                _customerFilter = value ?? AllFilter;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        private string _dateRangeFilter = "All";
        public string DateRangeFilter
        {
            get => _dateRangeFilter;
            set
            {
                if (_dateRangeFilter == value) return;
                _dateRangeFilter = value ?? "All";
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        /// <summary>Date range options.</summary>
        public ObservableCollection<string> DateRangeOptions { get; } = new()
        {
            "All", "This Week", "This Month", "This Quarter", "This Year", "Last 30 Days", "Last 90 Days"
        };

        // ── Margin vs the company BASE expected margin (Settings default) ──

        private decimal _baseExpectedMarginPercent = 15m;

        /// <summary>Company base expected margin (%) read from Settings
        /// (Step 9 → Profit margin %) when the list loads.</summary>
        public decimal BaseExpectedMarginPercent => _baseExpectedMarginPercent;

        public string BaseExpectedMarginDisplay => $"{_baseExpectedMarginPercent:0.#}%";

        /// <summary>Uplift factor of the base (15% → ×1.15), shown under the
        /// required-target card like Step 9 does.</summary>
        public string BaseExpectedMarginFactorLabel =>
            $"×{1m + (_baseExpectedMarginPercent / 100m):0.##}";

        /// <summary>Filter values: All / Above / At / Below the base.</summary>
        public ObservableCollection<string> MarginBandFilterOptions { get; } = new();

        private string _marginBandFilter = AllFilter;
        public string MarginBandFilter
        {
            get => _marginBandFilter;
            set
            {
                if (_marginBandFilter == value) return;
                _marginBandFilter = value ?? AllFilter;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        // ── Sort ─────────────────────────────────────────────────────────

        /// <summary>Sort options for the job table.</summary>
        public ObservableCollection<string> SortByOptions { get; } = new()
        {
            "Date (newest first)",
            "Margin % (highest first)",
            "Margin % (lowest first)"
        };

        private string _sortBy = "Date (newest first)";
        public string SortBy
        {
            get => _sortBy;
            set
            {
                if (_sortBy == value) return;
                _sortBy = value ?? SortByOptions[0];
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        private string _searchText = "";
        /// <summary>Free text over proforma ID, customer and project.</summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value ?? "";
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        // ── Totals (always reflect the FILTERED set) ──────────────────────

        private decimal _totalNet;
        public decimal TotalNet
        {
            get => _totalNet;
            private set
            {
                if (_totalNet == value) return;
                _totalNet = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalNetDisplay));
                OnPropertyChanged(nameof(NetMarginPercentDisplay));
            }
        }

        private decimal _totalCost;
        public decimal TotalCost
        {
            get => _totalCost;
            private set
            {
                if (_totalCost == value) return;
                _totalCost = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalCostDisplay));
                OnPropertyChanged(nameof(OverallMarginPercentDisplay));
            }
        }

        private decimal _totalProfit;
        public decimal TotalProfit
        {
            get => _totalProfit;
            private set
            {
                if (_totalProfit == value) return;
                _totalProfit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalProfitDisplay));
                OnPropertyChanged(nameof(TotalProfitLabel));
            }
        }

        private int _lossCount;
        /// <summary>How many filtered jobs lost money.</summary>
        public int LossCount
        {
            get => _lossCount;
            private set
            {
                if (_lossCount == value) return;
                _lossCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LossCountDisplay));
                OnPropertyChanged(nameof(HasLosses));
            }
        }

        private decimal _overallMarginPercent;
        /// <summary>
        /// Cost-based margin: (TotalGross - TotalCost) / TotalCost × 100
        /// This is the MAIN KPI shown in the portfolio summary.
        /// </summary>
        public decimal OverallMarginPercent
        {
            get => _overallMarginPercent;
            private set
            {
                if (_overallMarginPercent == value) return;
                _overallMarginPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OverallMarginDisplay));
                OnPropertyChanged(nameof(OverallMarginPercentDisplay));
            }
        }

        public string OverallMarginDisplay => $"AED {TotalProfit:N0} | {OverallMarginPercent:0.#}%";

        /// <summary>
        /// Cost-based overall margin as a bare percent for the KPI tile.
        /// "—" (not a fake 0%) when the filtered set has no costed rows.
        /// </summary>
        public string OverallMarginPercentDisplay => TotalCost > 0m
            ? $"{OverallMarginPercent:0.#}%"
            : "—";

        private decimal _overallPriceBasedMarginPercent;
        /// <summary>
        /// Price-based margin: (TotalGross - TotalCost) / TotalGross × 100
        /// This is the SECONDARY KPI shown in the portfolio summary.
        /// </summary>
        public decimal OverallPriceBasedMarginPercent
        {
            get => _overallPriceBasedMarginPercent;
            private set
            {
                if (_overallPriceBasedMarginPercent == value) return;
                _overallPriceBasedMarginPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OverallPriceBasedMarginDisplay));
                OnPropertyChanged(nameof(NetMarginPercentDisplay));
            }
        }

        public string OverallPriceBasedMarginDisplay => $"{OverallPriceBasedMarginPercent:0.#}% | AED {TotalProfit:N0}";

        /// <summary>
        /// Net margin (profit ÷ revenue) as a bare percent for the KPI tile.
        /// "—" (not a fake 0%) when the filtered set has no revenue.
        /// </summary>
        public string NetMarginPercentDisplay => TotalNet > 0m
            ? $"{OverallPriceBasedMarginPercent:0.#}%"
            : "—";

        private decimal _averageMarginPercent;
        /// <summary>
        /// Simple average of the Margin % shown on the displayed, costed rows:
        /// Σ (row MarginPercent) ÷ number of valid rows. No amount-based
        /// math — this deliberately weights every job equally, which is a
        /// different question from OverallMarginPercent (portfolio
        /// TotalProfit ÷ TotalNet, which Large jobs dominate).
        /// </summary>
        public decimal AverageMarginPercent
        {
            get => _averageMarginPercent;
            private set
            {
                if (_averageMarginPercent == value) return;
                _averageMarginPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AverageMarginDisplay));
                OnPropertyChanged(nameof(IsAverageLoss));
            }
        }

        private int _averageMarginCount;
        /// <summary>How many rows contributed to the average (0 = no valid rows).</summary>
        public int AverageMarginCount
        {
            get => _averageMarginCount;
            private set
            {
                if (_averageMarginCount == value) return;
                _averageMarginCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AverageMarginDisplay));
                OnPropertyChanged(nameof(IsAverageLoss));
            }
        }

        /// <summary>
        /// "12.3%" when at least one valid row exists; "—" when every
        /// displayed row is uncosted or the list is empty.
        /// </summary>
        public string AverageMarginDisplay => AverageMarginCount > 0
            ? $"{AverageMarginPercent:0.#}%"
            : "—";

        /// <summary>
        /// True when the AVERAGE itself is negative. Deliberately NOT the
        /// portfolio's IsPortfolioLoss: a net-loss portfolio can still have
        /// a positive average margin (and vice versa), so the colour on this
        /// tile must mirror the number it displays.
        /// </summary>
        public bool IsAverageLoss => AverageMarginCount > 0
            && AverageMarginPercent < 0m;

        // ── Verdict vs the company BASE expected margin (Settings default) ─
        // Plain average and value-weighted overall margin, each compared
        // against the base. This is the reconciliation view: PI1 at 16% and
        // PI2 at 14% (equal size) must come out at ≈15% AT the base.

        private JobMarginBaseBand AverageVsBaseBandValue => AverageMarginCount > 0
            ? JobMarginBands.Classify(AverageMarginPercent, _baseExpectedMarginPercent)
            : JobMarginBaseBand.Unknown;

        /// <summary>String band for XAML triggers: AboveBase / AtBase /
        /// BelowBase, empty when nothing to compare.</summary>
        public string AverageVsBaseBand => AverageVsBaseBandValue == JobMarginBaseBand.Unknown
            ? ""
            : AverageVsBaseBandValue.ToString();

        /// <summary>One-line verdict under the AVERAGE JOB MARGIN tile.</summary>
        public string AverageVsBaseVerdict
        {
            get
            {
                var band = AverageVsBaseBandValue;
                return band switch
                {
                    JobMarginBaseBand.AboveBase =>
                        $"▲ {AverageMarginPercent:0.#}% — ABOVE the {BaseExpectedMarginDisplay} base",
                    JobMarginBaseBand.BelowBase =>
                        $"▼ {AverageMarginPercent:0.#}% — BELOW the {BaseExpectedMarginDisplay} base",
                    JobMarginBaseBand.AtBase =>
                        $"＝ {AverageMarginPercent:0.#}% — AT the {BaseExpectedMarginDisplay} base",
                    _ => ""
                };
            }
        }

        private JobMarginBaseBand OverallVsBaseBandValue => TotalCost > 0m
            ? JobMarginBands.Classify(OverallMarginPercent, _baseExpectedMarginPercent)
            : JobMarginBaseBand.Unknown;

        /// <summary>String band for XAML triggers (Overall Margin tile).</summary>
        public string OverallVsBaseBand => OverallVsBaseBandValue == JobMarginBaseBand.Unknown
            ? ""
            : OverallVsBaseBandValue.ToString();

        /// <summary>One-line verdict under the OVERALL MARGIN tile.</summary>
        public string OverallVsBaseVerdict
        {
            get
            {
                var band = OverallVsBaseBandValue;
                return band switch
                {
                    JobMarginBaseBand.AboveBase =>
                        $"▲ {OverallMarginPercent:0.#}% — ABOVE the {BaseExpectedMarginDisplay} base",
                    JobMarginBaseBand.BelowBase =>
                        $"▼ {OverallMarginPercent:0.#}% — BELOW the {BaseExpectedMarginDisplay} base",
                    JobMarginBaseBand.AtBase =>
                        $"＝ {OverallMarginPercent:0.#}% — AT the {BaseExpectedMarginDisplay} base",
                    _ => ""
                };
            }
        }

        /// <summary>True when the portfolio total is negative.</summary>
        public bool IsPortfolioLoss => TotalProfit < 0m;

        /// <summary>True when at least one filtered job is loss-making.</summary>
        public bool HasLosses => LossCount > 0;

        public string TotalNetDisplay => $"AED {TotalNet:N2}";
        public string TotalCostDisplay => $"AED {TotalCost:N2}";
        public string TotalProfitDisplay => $"AED {Math.Abs(TotalProfit):N2}";
        public string TotalProfitLabel => IsPortfolioLoss ? "TOTAL LOSS" : "TOTAL PROFIT";
        public string LossCountDisplay => $"{LossCount}";

        // ══════════════════════════════════════════════════════════════
        //  PORTFOLIO — TARGET-PRICE KPIs (approved model)
        //
        //  Portfolio margin against target = Σ difference ÷ Σ required target
        //  Average job margin against target = plain mean of VALID job margins
        //  (two deliberately different measures — see TargetProfitabilityAggregator)
        // ══════════════════════════════════════════════════════════════

        private int _confirmedJobCount;
        public int ConfirmedJobCount
        {
            get => _confirmedJobCount;
            private set
            {
                if (_confirmedJobCount == value) return;
                _confirmedJobCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConfirmedJobCountDisplay));
            }
        }

        public string ConfirmedJobCountDisplay => $"{ConfirmedJobCount}";

        private int _validTargetJobCount;
        /// <summary>Jobs whose target margin is valid (used by the average).</summary>
        public int ValidTargetJobCount
        {
            get => _validTargetJobCount;
            private set
            {
                if (_validTargetJobCount == value) return;
                _validTargetJobCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ValidTargetJobCountDisplay));
            }
        }

        public string ValidTargetJobCountDisplay => $"{ValidTargetJobCount}";

        private int _incompleteTargetJobCount;
        public int IncompleteTargetJobCount
        {
            get => _incompleteTargetJobCount;
            private set
            {
                if (_incompleteTargetJobCount == value) return;
                _incompleteTargetJobCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IncompleteTargetJobCountDisplay));
            }
        }

        public string IncompleteTargetJobCountDisplay => $"{IncompleteTargetJobCount}";

        private int _staleTargetJobCount;
        public int StaleTargetJobCount
        {
            get => _staleTargetJobCount;
            private set
            {
                if (_staleTargetJobCount == value) return;
                _staleTargetJobCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StaleTargetJobCountDisplay));
            }
        }

        public string StaleTargetJobCountDisplay => $"{StaleTargetJobCount}";

        private decimal _targetTotalGivenValue;
        public decimal TargetTotalGivenValue
        {
            get => _targetTotalGivenValue;
            private set
            {
                if (_targetTotalGivenValue == value) return;
                _targetTotalGivenValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TargetTotalGivenValueDisplay));
            }
        }

        public string TargetTotalGivenValueDisplay => HasAnyTargetData
            ? $"AED {TargetTotalGivenValue:N2}"
            : "—";

        private decimal _targetTotalRequiredValue;
        public decimal TargetTotalRequiredValue
        {
            get => _targetTotalRequiredValue;
            private set
            {
                if (_targetTotalRequiredValue == value) return;
                _targetTotalRequiredValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TargetTotalRequiredValueDisplay));
            }
        }

        public string TargetTotalRequiredValueDisplay => HasAnyTargetData
            ? $"AED {TargetTotalRequiredValue:N2}"
            : "—";

        private decimal _targetTotalDifferenceValue;
        public decimal TargetTotalDifferenceValue
        {
            get => _targetTotalDifferenceValue;
            private set
            {
                if (_targetTotalDifferenceValue == value) return;
                _targetTotalDifferenceValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TargetTotalDifferenceValueDisplay));
            }
        }

        public string TargetTotalDifferenceValueDisplay
        {
            get
            {
                if (!HasAnyTargetData)
                    return "—";
                if (_targetTotalDifferenceValue < 0m)
                    return $"-AED {Math.Abs(_targetTotalDifferenceValue):N2}";
                return $"AED {_targetTotalDifferenceValue:N2}";
            }
        }

        private decimal? _portfolioTargetMarginPercent;
        /// <summary>Σ difference ÷ Σ required target across jobs with target data.</summary>
        public decimal? PortfolioTargetMarginPercent
        {
            get => _portfolioTargetMarginPercent;
            private set
            {
                if (_portfolioTargetMarginPercent == value) return;
                _portfolioTargetMarginPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PortfolioTargetMarginDisplay));
                OnPropertyChanged(nameof(PortfolioTargetStatusLabel));
                OnPropertyChanged(nameof(IsPortfolioTargetBelow));
                OnPropertyChanged(nameof(HasPortfolioTargetMargin));
            }
        }

        public bool HasPortfolioTargetMargin => PortfolioTargetMarginPercent.HasValue;

        public string PortfolioTargetMarginDisplay =>
            PortfolioTargetMarginPercent switch
            {
                null => "—",
                var m => $"{m.Value:0.##}%"
            };

        public bool IsPortfolioTargetBelow => (PortfolioTargetMarginPercent ?? 0m) < 0m;

        /// <summary>Pill label for the MARGIN AGAINST TARGET card (Step 9
        /// style): ABOVE / AT / BELOW TARGET, or "—" when no valid margin.</summary>
        public string PortfolioTargetStatusLabel
        {
            get
            {
                var m = PortfolioTargetMarginPercent;
                if (!m.HasValue) return "—";
                if (m.Value < -0.05m) return "BELOW TARGET";
                if (m.Value > 0.05m) return "ABOVE TARGET";
                return "AT TARGET";
            }
        }

        /// <summary>True when the portfolio is below target: required target
        /// value exceeds Step 5 given value (a positive shortfall).</summary>
        public bool HasTargetShortfall =>
            HasAnyTargetData && TargetTotalDifferenceValue > 0m;

        private decimal? _averageJobTargetMarginPercent;
        private int _averageJobTargetMarginCount;

        /// <summary>Plain average of the VALID job target margins.</summary>
        public decimal? AverageJobTargetMarginPercent
        {
            get => _averageJobTargetMarginPercent;
            private set
            {
                if (_averageJobTargetMarginPercent == value) return;
                _averageJobTargetMarginPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AverageJobTargetMarginDisplay));
                OnPropertyChanged(nameof(IsAverageJobTargetBelow));
                OnPropertyChanged(nameof(HasAverageJobTargetMargin));
            }
        }

        public bool HasAverageJobTargetMargin => AverageJobTargetMarginPercent.HasValue;

        public int AverageJobTargetMarginCount
        {
            get => _averageJobTargetMarginCount;
            private set
            {
                if (_averageJobTargetMarginCount == value) return;
                _averageJobTargetMarginCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AverageJobTargetMarginCountDisplay));
            }
        }

        public string AverageJobTargetMarginCountDisplay => $"{_averageJobTargetMarginCount}";

        public string AverageJobTargetMarginDisplay =>
            AverageJobTargetMarginPercent switch
            {
                null => "—",
                var m => $"{m.Value:0.##}%"
            };

        public bool IsAverageJobTargetBelow => (AverageJobTargetMarginPercent ?? 0m) < 0m;

        public string PortfolioStatusLine
        {
            get
            {
                if (ConfirmedJobCount == 0)
                    return "No confirmed jobs to analyse.";

                var parts = new List<string>();
                parts.Add($"{ConfirmedJobCount} confirmed job(s)");

                if (ValidTargetJobCount > 0)
                    parts.Add($"{ValidTargetJobCount} with a valid target margin");
                if (IncompleteTargetJobCount > 0)
                    parts.Add($"{IncompleteTargetJobCount} incomplete");
                if (StaleTargetJobCount > 0)
                    parts.Add($"{StaleTargetJobCount} stale (saved before the target model)");

                return string.Join(" · ", parts) + ".";
            }
        }

        private bool _hasAnyTargetData;
        public bool HasAnyTargetData
        {
            get => _hasAnyTargetData;
            private set
            {
                if (_hasAnyTargetData == value) return;
                _hasAnyTargetData = value;
                OnPropertyChanged();

                // The three target-value cards read HasAnyTargetData so they
                // can show an honest "—" instead of "AED 0.00" when every job
                // predates the target model.
                OnPropertyChanged(nameof(TargetTotalGivenValueDisplay));
                OnPropertyChanged(nameof(TargetTotalRequiredValueDisplay));
                OnPropertyChanged(nameof(TargetTotalDifferenceValueDisplay));
                OnPropertyChanged(nameof(HasTargetShortfall));
            }
        }

        /// <summary>
        /// Explained status line under the target cards: what the two target
        /// margins actually cover and how many jobs are stale/incomplete.
        /// </summary>
        public string TargetDataBasisNote
        {
            get
            {
                if (ConfirmedJobCount == 0)
                    return "No confirmed jobs yet.";

                if (!HasAnyTargetData)
                    return "None of the confirmed jobs carry target-model data yet — reopen each in Step 9 and save (target model v2.0). Actual P&L above is read from the saved Step 9 snapshot.";

                int missing = StaleTargetJobCount + IncompleteTargetJobCount;
                return missing > 0
                    ? $"{ValidTargetJobCount} job(s) with a valid target margin; {missing} without (stale/incomplete) excluded from the portfolio target margin."
                    : $"Portfolio target margin uses {ValidTargetJobCount} job(s) with a valid margin.";
            }
        }

        // ── State ─────────────────────────────────────────────────────────

        /// <summary>
        /// Set when a refresh arrives while a load is already running, so
        /// the request is re-run instead of dropped. See LoadAsync.
        /// </summary>
        private bool _reloadRequested;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        /// <summary>True when the filtered list has rows (drives the empty state).</summary>
        public bool HasRows => Rows.Count > 0;

        private string _statusMessage = "Loading job profitability…";
        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (_statusMessage == value) return;
                _statusMessage = value ?? "";
                OnPropertyChanged();
            }
        }

        // ── Commands ──────────────────────────────────────────────────────

        /// <summary>Reloads everything from the invoice database.</summary>
        public ICommand RefreshCommand { get; private set; }

        /// <summary>Selects a row and opens the detail sidebar.</summary>
        public ICommand SelectRowCommand { get; private set; }

        /// <summary>Closes the detail sidebar.</summary>
        public ICommand CloseDetailCommand { get; private set; }

        /// <summary>Returns the top cards to the portfolio aggregate.</summary>
        public ICommand ClearRowSelectionCommand { get; private set; }

        // ── Selected row ──────────────────────────────────────────────────
        // The top KPI cards mirror Step 9 for ONE PI. Click a row in the
        // jobs table and the four cards switch from the portfolio aggregate
        // to that row's own figures (required target, margin against target,
        // overall margin). No selection → portfolio aggregate.

        private JobProfitabilityRow? _selectedRow;

        public JobProfitabilityRow? SelectedRow
        {
            get => _selectedRow;
            private set
            {
                if (ReferenceEquals(_selectedRow, value)) return;
                _selectedRow = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasRowSelection));
                OnPropertyChanged(nameof(SelectionScopeLabel));
                OnPropertyChanged(nameof(TopScopeSummary));
                NotifyTopCardsChanged();
            }
        }

        public bool HasRowSelection => _selectedRow != null;

        public string SelectionScopeLabel => _selectedRow != null
            ? $"PI {_selectedRow.ProformaId}"
            : "All confirmed PIs";

        /// <summary>One line under the KPI cards explaining the current scope.</summary>
        public string TopScopeSummary => _selectedRow != null
            ? $"Top cards show {SelectionScopeLabel} — the totals and counts below always cover all {ConfirmedJobCount} confirmed PI(s) shown."
            : $"Top cards show all {ConfirmedJobCount} confirmed PI(s) — click any row in the list to inspect that PI here.";

        private void SetSelectedRow(JobProfitabilityRow? row)
        {
            SelectedRow = row;
            foreach (var r in Rows)
                r.IsSelectedRow = ReferenceEquals(r, row);
        }

        /// <summary>Raises change notifications for every top-card property.</summary>
        private void NotifyTopCardsChanged()
        {
            OnPropertyChanged(nameof(TopRequiredTargetValueDisplay));
            OnPropertyChanged(nameof(TopRequiredTargetScope));
            OnPropertyChanged(nameof(TopTargetMarginDisplay));
            OnPropertyChanged(nameof(TopTargetStatusLabel));
            OnPropertyChanged(nameof(IsTopTargetBelow));
            OnPropertyChanged(nameof(HasTopTargetMargin));
            OnPropertyChanged(nameof(IsTopTargetIncomplete));
            OnPropertyChanged(nameof(IsTopTargetStale));
            OnPropertyChanged(nameof(TopOverallMarginDisplay));
            OnPropertyChanged(nameof(IsTopOverallLoss));
            OnPropertyChanged(nameof(TopOverallVerdict));
            OnPropertyChanged(nameof(IsTopOverallAboveBase));
            OnPropertyChanged(nameof(IsTopOverallBelowBase));
        }

        // ── Top KPI cards — dynamic (selected PI when a row is clicked) ──

        /// <summary>CARD 1 — required target value.</summary>
        public string TopRequiredTargetValueDisplay
        {
            get
            {
                if (SelectedRow != null)
                    return SelectedRow.HasTargetData
                        ? $"AED {SelectedRow.TargetRequiredValue:N2}"
                        : "—";
                return TargetTotalRequiredValueDisplay;
            }
        }

        public string TopRequiredTargetScope => SelectedRow != null
            ? $"Final required target — {SelectionScopeLabel}"
            : "Σ final required target — all confirmed PIs";

        /// <summary>CARD 3 — margin against target (the ONLY margin that
        /// drives Job Profitability; Step 9's approved KPI).</summary>
        public decimal? TopTargetMarginPercent => SelectedRow != null
            ? (SelectedRow.HasValidTargetMargin ? SelectedRow.TargetMarginAgainstTargetPercent : (decimal?)null)
            : PortfolioTargetMarginPercent;

        public string TopTargetMarginDisplay => TopTargetMarginPercent switch
        {
            null => "—",
            var m => $"{m.Value:0.##}%"
        };

        public string TopTargetStatusLabel => SelectedRow != null
            ? SelectedRow.TargetStatusLabel
            : PortfolioTargetStatusLabel;

        public bool IsTopTargetBelow => SelectedRow != null
            ? SelectedRow.IsBelowTarget
            : IsPortfolioTargetBelow;

        public bool HasTopTargetMargin => SelectedRow != null
            ? SelectedRow.HasValidTargetMargin
            : HasPortfolioTargetMargin;

        public bool IsTopTargetIncomplete => SelectedRow != null
            ? SelectedRow.IsTargetIncomplete
            : (!HasPortfolioTargetMargin && ValidTargetJobCount == 0);

        public bool IsTopTargetStale => SelectedRow != null
            ? SelectedRow.IsTargetStale
            : (StaleTargetJobCount > 0 && ValidTargetJobCount == 0);

        /// <summary>CARD 4 — overall margin (actual P&amp;L, Σ profit ÷ Σ cost on
        /// NET revenue ex-VAT). Row-selected = that PI's achieved margin.</summary>
        public string TopOverallMarginDisplay => SelectedRow != null
            ? (SelectedRow.IsCosted ? $"{SelectedRow.EffectiveMarginPercent:0.#}%" : "not costed")
            : OverallMarginPercentDisplay;

        public bool IsTopOverallLoss => SelectedRow != null
            ? (SelectedRow.IsCosted && SelectedRow.EffectiveMarginPercent < 0m)
            : IsPortfolioLoss;

        public string TopOverallVerdict => SelectedRow != null
            ? (SelectedRow.HasBaseMarginComparison
                ? JobMarginBands.Chip(SelectedRow.EffectiveMarginPercent, SelectedRow.BaseExpectedMarginPercent)
                : "")
            : OverallVsBaseVerdict;

        public bool IsTopOverallAboveBase => SelectedRow != null
            ? SelectedRow.IsMarginAboveBase
            : OverallVsBaseBand == "AboveBase";

        public bool IsTopOverallBelowBase => SelectedRow != null
            ? SelectedRow.IsMarginBelowBase
            : OverallVsBaseBand == "BelowBase";

        public JobProfitabilityViewModel()
         : this(
            App.ServiceProvider?.GetService<IProformaInvoiceRepository>()!){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            RefreshCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(() => FireRefreshAsync(), "JobProfitability FireRefresh"));
            SelectRowCommand = new RelayCommand(param =>
            {
                if (param is JobProfitabilityRow row)
                {
                    // Selecting a row drives the TOP KPI CARDS to that PI's
                    // own Step 9 figures. Clicking the same row again toggles
                    // the sidebar; the cards keep showing the row.
                    SetSelectedRow(row);
                    if (JobDetail.IsPanelVisible && JobDetail.ProformaId == row.ProformaId)
                        JobDetail.ClearSelection();
                    else
                        JobDetail.LoadJobDetail(row.ProformaId);
                }
            });
            CloseDetailCommand = new RelayCommand(_ => JobDetail.ClearSelection());
            ClearRowSelectionCommand = new RelayCommand(_ => SetSelectedRow(null));

            Rows.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasRows));

            CompanyFilterOptions.Add(AllFilter);
            SalesmanFilterOptions.Add(AllFilter);
            CustomerFilterOptions.Add(AllFilter);

            // Margin-vs-base filter values are band names (the base % itself
            // is shown on the page header, live from Settings).
            MarginBandFilterOptions.Add(AllFilter);
            MarginBandFilterOptions.Add("Above");
            MarginBandFilterOptions.Add("At");
            MarginBandFilterOptions.Add("Below");

            // ══════════════════════════════════════════════════════════
            //  LIVE UPDATES
            //
            //  Entering the module reloaded it, but saving an invoice while
            //  this screen was already open left the figures stale with
            //  nothing on screen saying so. The repository now raises a
            //  signal on every insert, update and delete, so the list
            //  follows the data instead of the navigation.
            //
            //  The handler marshals to the UI thread: a save may complete on
            //  a background thread, and rebuilding an ObservableCollection
            //  bound to a DataGrid from off-thread throws.
            //
            //  The subscription is never removed. This view model lives for
            //  the whole application session (it is a shell singleton), so
            //  there is no leak to unwind — and unsubscribing on a screen
            //  change is exactly what would reintroduce the stale list.
            // ══════════════════════════════════════════════════════════
            // The namespace and the class share the name
            // "ProformaInvoiceRepository", so a partial qualification is
            // ambiguous. The using at the top of this file already imports
            // the namespace, so the bare class name is what resolves.
            ProformaInvoiceRepository.SavedInvoicesChanged += OnSavedInvoicesChanged;

            // First load is fire-and-forget so construction stays cheap and
            // the shell can build every module VM without blocking.
            AsyncUtility.SafeFireAndForget(() => FireRefreshAsync(), "JobProfitability FireRefresh");
        }

        private void OnSavedInvoicesChanged()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;

            if (dispatcher == null || dispatcher.CheckAccess())
                AsyncUtility.SafeFireAndForget(() => FireRefreshAsync(), "JobProfitability FireRefresh");
            else
                dispatcher.BeginInvoke(new Action(() => AsyncUtility.SafeFireAndForget(() => FireRefreshAsync(), "JobProfitability FireRefresh")));
        }

        private async Task FireRefreshAsync()
        {
            await LoadAsync().ConfigureAwait(true);
            // Refresh all 3 portfolio analysis tabs in parallel
            // so they show the same data as the P&L Summary
            await Task.WhenAll(
                PortfolioServiceProfitability.RefreshAnalysisAsync(),
                PortfolioWastageAnalysis.RefreshAnalysisAsync(),
                PortfolioGlassTypeComparison.RefreshAnalysisAsync()
            ).ConfigureAwait(true);
        }

        // ══════════════════════════════════════════════════════════════
        //  LOAD
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Reads ONLY proforma invoices whose stored Status is "Approved"
        /// (the value the Saved PI list writes when the operator marks a PI
        /// as "PI Confirmed" — Draft / "PI created", Sent / "PI sent",
        /// Rejected, Cancelled and Expired never qualify), projects each to
        /// a profitability row and rebuilds the filter option lists. Never
        /// throws.
        /// </summary>
        public async Task LoadAsync()
        {
            // ══════════════════════════════════════════════════════════
            //  WHY THIS IS NOT JUST "if (IsBusy) return;"
            //
            //  The constructor kicks off a fire-and-forget first load, and
            //  Navigate("JobProfitability") calls RefreshCommand on entry.
            //  Open the module while that first load is still running - or
            //  save an invoice and come straight back - and the refresh hit
            //  the IsBusy guard and was SILENTLY DROPPED. The user saw
            //  stale figures and no indication anything had been skipped,
            //  which is exactly the "not updating instantly" symptom.
            //
            //  Now a refresh that arrives mid-load is REMEMBERED and re-run
            //  once the in-flight load finishes, so the last request always
            //  wins and the view ends up consistent with the database.
            // ══════════════════════════════════════════════════════════
            if (IsBusy)
            {
                _reloadRequested = true;
                return;
            }

            IsBusy = true;

            try
            {
                var repo = _invoices;
                if (repo == null)
                {
                    StatusMessage = "Invoice repository is not available.";
                    return;
                }

                // The saved entity stores the lifecycle exactly as the Saved
                // PI list writes it: the "PI Confirmed" action persists the
                // literal status "Approved"; everything else — Draft (shown
                // as "PI created"), Sent (shown as "PI sent"), Rejected,
                // Cancelled, Expired — stays out. Read the indexed Status
                // column first so drafts / sent / cancelled rows never even
                // leave the database with their full snapshot blob, then
                // re-check case-insensitively so a hand-edited value can
                // never slip in.
                var invoices = await repo.GetByStatusAsync(ProformaInvoiceStatus.Approved)
                    .ConfigureAwait(true);

                // The company base expected margin (%) every row chip and the
                // portfolio verdict compare against — read live from Settings
                // (Features → Proforma Invoice → Step 9 → Profit margin %,
                // 15% default). Change the setting and Refresh the page to
                // re-judge the whole portfolio against the new base.
                _baseExpectedMarginPercent =
                    ProGlassAutomation.Services.Features.Step9PricingDefaults.ExpectedProfitMarginPercent;
                OnPropertyChanged(nameof(BaseExpectedMarginPercent));
                OnPropertyChanged(nameof(BaseExpectedMarginDisplay));

                _allRows.Clear();

                foreach (var inv in invoices ?? new List<Models.ProformaInvoice.SavedInvoice.SavedProformaInvoice>())
                {
                    if (inv == null) continue;

                    if (!string.Equals(
                            inv.Status, ProformaInvoiceStatus.Approved,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    // DeserializeSnapshot already guards malformed JSON and
                    // returns an empty snapshot rather than throwing.
                    var snap = ProformaInvoiceMapper.DeserializeSnapshot(inv);

                    var pl = snap?.ProfitLoss;

                    decimal glass = pl?.TotalGlassCost ?? 0m;
                    decimal production = pl?.TotalProductionCost ?? 0m;
                    decimal cost = glass + production;

                    // An invoice confirmed before Step 9 existed has no rows
                    // and no totals — flag it instead of pretending it is
                    // pure profit.
                    // ══════════════════════════════════════════════════
                    //  WHY COST / PROFIT / MARGIN CAME BACK EMPTY
                    //
                    //  These figures are read ONLY from the persisted Step 9
                    //  snapshot. ProformaInvoiceMapper writes that snapshot
                    //  from whatever the Step 9 view-model happens to hold at
                    //  save time - and Step 9 only fills itself in when the
                    //  user actually opens the tab and it recalculates.
                    //
                    //  So an invoice quoted and saved without visiting Step 9
                    //  is stored with TotalGlassCost = TotalProductionCost =
                    //  Profit = 0. The grid then rendered "AED 0.00" and
                    //  "not costed" for every such job, which reads as an
                    //  empty column.
                    //
                    //  That is a REPORTING gap, not a data-entry mistake by
                    //  the user, so the honest fix is to say which it is
                    //  rather than silently show zeros. IsCosted already
                    //  drives the greyed-out styling; the status line now
                    //  reports how many jobs are affected so the emptiness
                    //  is explained instead of just observed.
                    // ══════════════════════════════════════════════════
                    // Detect figures produced before the inner-pane fix.
                    // Read-only: it reports, it never rewrites saved money.
                    var integrity = ProfitLossIntegrityCheck.ScanSnapshot(pl);

                    bool costed = pl != null
                                  && (pl.Rows.Count > 0
                                      || cost > 0m
                                      || pl.NetExVat > 0m);


                    // ══════════════════════════════════════════════════
                    //  JOB PROFITABILITY READS STEP 9 — IT DOES NOT
                    //  RECOMPUTE.
                    //
                    //  Step 9 is the single source of truth for money:
                    //  NetExVat, Profit and MarginPercent are persisted in
                    //  the snapshot as they were shown to the user, and
                    //  Step 9's own margin is Profit ÷ NetExVat (already
                    //  rounded to one decimal in the snapshot).
                    //
                    //  The previous pattern recomputed here — it divided the
                    //  invoice header gross by 1.05 for step 9's net and
                    //  derived profit from net − cost, then re-derived margin
                    //  from those two. Step 9 stores the same numbers, so
                    //  this module could quote a DIFFERENT margin than the
                    //  user saw when the costing was taken (the header is
                    //  re-editable after Step 9, snapshots can be stale, and
                    //  Step 9 rounds profit/net before persisting it). No
                    //  VAT divisor or margin formula lives here any more.
                    //
                    //  Uncosted (confirmed before Step 9 existed, no rows, no
                    //  totals) → net/profit/margin 0 with the greyed "not
                    //  costed" treatment and EXCLUSION from totals, instead
                    //  of a header-derived guess.
                    // ══════════════════════════════════════════════════
                    decimal net = costed ? pl!.NetExVat : 0m;
                    decimal profit = costed ? pl!.Profit : 0m;
                    decimal margin = costed ? pl!.MarginPercent : 0m;

                    // ── Target-price model (persisted Step 9 target result) ─
                    // Saved before the target model existed → SpecTargets is
                    // empty and totals are zero. That job is STALE (it HAS a
                    // P&L) or INCOMPLETE (no P&L at all) — never a 0% margin.
                    bool hasTargetData = pl != null
                                         && ((pl.SpecTargets != null && pl.SpecTargets.Count > 0)
                                             || pl.TargetTotalRequiredValue > 0m
                                             || !string.IsNullOrWhiteSpace(pl.TargetStatus));

                    bool snapshotStale = costed && !hasTargetData;

                    decimal targetGiven = hasTargetData ? pl!.TargetTotalGivenValue : 0m;
                    decimal targetRequired = hasTargetData ? pl!.TargetTotalRequiredValue : 0m;
                    decimal targetDifference = hasTargetData ? pl!.TargetTotalDifferenceValue : 0m;
                    decimal targetGivenPerSqm = hasTargetData ? pl!.TargetAverageGivenPricePerSqm : 0m;
                    decimal targetRequiredPerSqm = hasTargetData ? pl!.TargetAverageFinalRequiredTargetPerSqm : 0m;
                    decimal? targetMargin = hasTargetData ? pl!.TargetMarginAgainstTargetPercent : null;

                    string targetStatus = hasTargetData
                        ? (pl!.TargetStatus ?? "")
                        : (snapshotStale ? "SnapshotStale" : "CalculationIncomplete");

                    // ── AUTO-CORRECT the margin against target ─────────────
                    // The authoritative per-PI figure is (given − required) ÷
                    // required, and the snapshot always stores the two totals
                    // that derive it. Older builds could persist a stale or
                    // outdated aggregate margin, so whenever both totals are
                    // intact we recompute here — fixing the KPI column, the
                    // top cards and the portfolio average without a data
                    // migration. Rows without usable totals keep the stored
                    // value (null = genuinely incomplete → never a fake 0%).
                    if (hasTargetData && targetGiven > 0m && targetRequired > 0m)
                    {
                        targetMargin = Math.Round(
                            ((targetGiven - targetRequired) * 100m) / targetRequired, 2);
                        targetStatus = targetMargin.Value > 0.05m ? "AboveTarget"
                            : targetMargin.Value < -0.05m ? "BelowTarget"
                            : "AtTarget";
                    }

                    _allRows.Add(new JobProfitabilityRow
                    {
                        ProformaId = inv.ProformaId ?? "",
                        CompanyName = inv.CompanyName ?? "",
                        CustomerName = inv.CustomerName ?? "",
                        ProjectName = inv.ProjectName ?? "",
                        Salesman = snap?.Client?.SalesRepresentative ?? "",
                        IssueDate = inv.IssueDate,
                        NetExVat = net,
                        TotalCost = cost,
                        Profit = profit,
                        MarginPercent = margin,
                        OverallMarginPercent = costed ? pl!.OverallMarginPercent : 0m,
                        NetMarginPercent = costed ? pl!.NetMarginPercent : 0m,
                        IsLoss = costed && profit < 0m,
                        IsCosted = costed,
                        NeedsRecalculation = costed && integrity.NeedsRecalculation,
                        RecalculationWarning = integrity.Reason,
                        BaseExpectedMarginPercent = _baseExpectedMarginPercent,
                        HasTargetData = hasTargetData,
                        TargetGivenValue = targetGiven,
                        TargetRequiredValue = targetRequired,
                        TargetDifferenceValue = targetDifference,
                        TargetGivenPerSqm = targetGivenPerSqm,
                        TargetRequiredPerSqm = targetRequiredPerSqm,
                        TargetMarginAgainstTargetPercent = targetMargin,
                        UInsertTreatmentSummary = hasTargetData ? (pl!.TargetUInsertTreatmentSummary ?? "") : "",
                        TargetStatus = targetStatus,
                        TargetValidSpecCount = hasTargetData ? pl!.TargetValidSpecCount : 0,
                        TargetIncompleteSpecCount = hasTargetData ? pl!.TargetIncompleteSpecCount : 0,
                        IsSnapshotStale = snapshotStale
                    });
                }

                RebuildFilterOptions();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[JobProfitabilityViewModel] Load failed: {ExceptionMessages.Deep(ex)}");
                StatusMessage = "Could not load job profitability — check the invoice database.";
            }
            finally
            {
                IsBusy = false;
            }

            // A refresh that arrived mid-load runs now. Cleared BEFORE the
            // re-entry so a load that keeps getting interrupted cannot spin
            // forever - at most one extra pass per dropped request.
            if (_reloadRequested)
            {
                _reloadRequested = false;
                await LoadAsync().ConfigureAwait(true);
            }
        }

        /// <summary>
        /// Refreshes both dropdowns from the loaded data, preserving the
        /// current selection when it still exists.
        /// </summary>
        private void RebuildFilterOptions()
        {
            string company = CompanyFilter;
            string salesman = SalesmanFilter;

            CompanyFilterOptions.Clear();
            CompanyFilterOptions.Add(AllFilter);
            foreach (var c in _allRows
                         .Select(r => r.CompanyName)
                         .Where(v => !string.IsNullOrWhiteSpace(v))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            {
                CompanyFilterOptions.Add(c);
            }

            SalesmanFilterOptions.Clear();
            SalesmanFilterOptions.Add(AllFilter);
            foreach (var s in _allRows
                         .Select(r => r.Salesman)
                         .Where(v => !string.IsNullOrWhiteSpace(v))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            {
                SalesmanFilterOptions.Add(s);
            }

            // Customer filter options
            string customer = CustomerFilter;
            CustomerFilterOptions.Clear();
            CustomerFilterOptions.Add(AllFilter);
            foreach (var cu in _allRows
                         .Select(r => r.CustomerName)
                         .Where(v => !string.IsNullOrWhiteSpace(v))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            {
                CustomerFilterOptions.Add(cu);
            }

            // Restore selections without re-triggering a filter pass.
            _companyFilter = CompanyFilterOptions.Contains(company) ? company : AllFilter;
            _salesmanFilter = SalesmanFilterOptions.Contains(salesman) ? salesman : AllFilter;
            _customerFilter = CustomerFilterOptions.Contains(customer) ? customer : AllFilter;
            OnPropertyChanged(nameof(CompanyFilter));
            OnPropertyChanged(nameof(SalesmanFilter));
            OnPropertyChanged(nameof(CustomerFilter));
        }

        // ══════════════════════════════════════════════════════════════
        //  FILTER + TOTALS
        // ══════════════════════════════════════════════════════════════

        private void ApplyFilters()
        {
            IEnumerable<JobProfitabilityRow> query = _allRows;

            if (!string.Equals(CompanyFilter, AllFilter, StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r =>
                    string.Equals(r.CompanyName, CompanyFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(SalesmanFilter, AllFilter, StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r =>
                    string.Equals(r.Salesman, SalesmanFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(CustomerFilter, AllFilter, StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r =>
                    string.Equals(r.CustomerName, CustomerFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Date range filter
            if (DateRangeFilter != "All")
            {
                var now = DateTime.Now;
                DateTime cutoff = DateRangeFilter switch
                {
                    "This Week" => now.AddDays(-(int)now.DayOfWeek),
                    "This Month" => new DateTime(now.Year, now.Month, 1),
                    "This Quarter" => new DateTime(now.Year, ((now.Month - 1) / 3) * 3 + 1, 1),
                    "This Year" => new DateTime(now.Year, 1, 1),
                    "Last 30 Days" => now.AddDays(-30),
                    "Last 90 Days" => now.AddDays(-90),
                    _ => DateTime.MinValue
                };
                if (cutoff > DateTime.MinValue)
                    query = query.Where(r => r.IssueDate >= cutoff);
            }

            string term = (SearchText ?? "").Trim();
            if (term.Length > 0)
            {
                query = query.Where(r =>
                    Utilities.SearchHelper.ContainsIgnoreCase(r.ProformaId, term) ||
                    Utilities.SearchHelper.ContainsIgnoreCase(r.CustomerName, term) ||
                    Utilities.SearchHelper.ContainsIgnoreCase(r.ProjectName, term));
            }

            // Margin vs base band filter. Uncosted jobs match none of the
            // bands, so picking Above/At/Below naturally keeps only costed
            // jobs that sit in that band.
            if (!string.Equals(MarginBandFilter, AllFilter, StringComparison.OrdinalIgnoreCase))
            {
                query = MarginBandFilter switch
                {
                    "Above" => query.Where(r => r.IsMarginAboveBase),
                    "Below" => query.Where(r => r.IsMarginBelowBase),
                    _ => query.Where(r => r.IsMarginAtBase)
                };
            }

            // Sort (default: newest first, matching the original behaviour).
            query = SortBy switch
            {
                "Margin % (highest first)" =>
                    query.OrderByDescending(r => r.IsCosted)
                         .ThenByDescending(r => r.EffectiveMarginPercent),
                "Margin % (lowest first)" =>
                    query.OrderByDescending(r => r.IsCosted)
                         .ThenBy(r => r.EffectiveMarginPercent),
                _ => query.OrderByDescending(r => r.IssueDate)
            };

            var result = query.ToList();

            // Keep the row selection valid: if the selected PI is filtered
            // out, return the top cards to the portfolio scope.
            if (_selectedRow != null && !result.Contains(_selectedRow))
                SetSelectedRow(null);

            Rows.ClearSilent();
            Rows.AddRangeSilent(result);
            foreach (var r in Rows)
                r.IsSelectedRow = ReferenceEquals(r, _selectedRow);
            Rows.RaiseReset();
            OnPropertyChanged(nameof(HasRows));

            RecalculateTotals(result);
            StatusMessage = BuildStatus(result);
        }

        private void RecalculateTotals(List<JobProfitabilityRow> rows)
        {
            // ONLY costed rows enter the totals. Uncosted (confirmed before
            // Step 9 existed) rows carry zero profit AND zero net now, so
            // even their exclusion from the sum is already reflected — but
            // the filter keeps the intent explicit: a job with no P&L data
            // must never dilute the portfolio margin or the loss count.
            var costedRows = rows.Where(r => r.IsCosted).ToList();

            TotalNet = Math.Round(costedRows.Sum(r => r.NetExVat), 2);
            TotalCost = Math.Round(costedRows.Sum(r => r.TotalCost), 2);
            TotalProfit = Math.Round(costedRows.Sum(r => r.Profit), 2);
            LossCount = rows.Count(r => r.IsLoss);

            // Portfolio margins mirror Step 9's REAL job-profitability basis
            // (Step 9 OverallMarginPercent):
            //
            //   OVERALL MARGIN (main): Σ Profit ÷ Σ Total Cost × 100
            //     each job's Profit = NetExVat − actual total cost (glass +
            //     production incl. wastage + U-insert). Summing first, then
            //     dividing, weights big jobs by cost — with one job it equals
            //     the Step 9 figure exactly.
            //
            //   NET MARGIN (secondary): Σ Profit ÷ Σ NetExVat × 100
            //
            // Deliberately NOT NetExVat × 1.05 (VAT added back): Step 9
            // measures every margin on NET revenue ex-VAT. Adding the 5% VAT
            // here inflated the portfolio by ~5 points and contradicted the
            // per-job margins the MARGIN column shows — the source of the
            // "overall margin looks wrong" reports. The profit amount used is
            // the same number the PROFIT tile sums (stored Step 9 Profit), so
            // Σ Profit ÷ Σ cost reconciles with the rows above it.
            //
            // This is computed from the TOTALS, not averaged from the
            // individual margins — an average of percentages weights tiny
            // jobs the same as large ones.
            //
            // Edge case: if TotalCost is very small relative to TotalProfit,
            // the cost-based margin can exceed ±100% (e.g., cost=10, profit=121 →
            // 1210%). This is mathematically correct and matches what
            // Step 9 would show for that single PI.
            OverallMarginPercent = TotalCost > 0m
                ? Math.Round((TotalProfit * 100m) / TotalCost, 1)
                : 0m;

            OverallPriceBasedMarginPercent = TotalNet > 0m
                ? Math.Round((TotalProfit * 100m) / TotalNet, 1)
                : 0m;

            // Average Margin %: prefer OverallMarginPercent (with U-insert)
            // for rows that have it; fall back to MarginPercent for old snapshots.
            AverageMarginCount = costedRows.Count;
            AverageMarginPercent = costedRows.Count > 0
                ? Math.Round(costedRows.Average(r => r.OverallMarginPercent != 0m ? r.OverallMarginPercent : r.MarginPercent), 1)
                : 0m;

            // ── TARGET-PRICE PORTFOLIO KPIs ──────────────────────────────
            // Valid = the job snapshot carries target data AND a calculable
            // margin. Incomplete/stale/uncosted jobs are counted but NEVER
            // averaged or allowed to fake a 0%.
            var targetRows = rows.Where(r => r.HasTargetData).ToList();
            var validRows = targetRows.Where(r => r.TargetMarginAgainstTargetPercent.HasValue).ToList();

            ConfirmedJobCount = rows.Count;
            ValidTargetJobCount = validRows.Count;
            IncompleteTargetJobCount = rows.Count(r =>
                (!r.HasTargetData && !r.IsCosted)          // uncosted = incomplete
                || (r.HasTargetData && !r.TargetMarginAgainstTargetPercent.HasValue));
            StaleTargetJobCount = rows.Count(r => r.IsSnapshotStale || r.IsTargetStale);

            TargetTotalGivenValue = Math.Round(targetRows.Sum(r => r.TargetGivenValue), 2);
            TargetTotalRequiredValue = Math.Round(targetRows.Sum(r => r.TargetRequiredValue), 2);

            // TOTAL DIFFERENCE is derived from the two totals ABOVE it (never
            // summed from stored per-job differences, which can be 0/missing
            // in snapshots saved by older builds):
            //
            //     difference = required target − Step 5 given value
            //
            // e.g. required 5,839.44 − given 5,472.55 → +366.89 (a shortfall
            // below target). Positive = below target, negative = above target.
            // The MARGIN AGAINST TARGET card still uses given−required for its
            // percentage, which is the Step 9 convention.
            TargetTotalDifferenceValue = Math.Round(
                TargetTotalRequiredValue - TargetTotalGivenValue, 2);

            HasAnyTargetData = targetRows.Count > 0;

            // Portfolio margin (value-weighted over target-data jobs) is only
            // meaningful when EVERY job carries a valid margin; otherwise the
            // summary says how many jobs are incomplete/stale instead.
            if (targetRows.Count > 0
                && targetRows.All(r => r.TargetMarginAgainstTargetPercent.HasValue)
                && TargetTotalRequiredValue > 0m)
            {
                PortfolioTargetMarginPercent = Math.Round(
                    (TargetTotalGivenValue - TargetTotalRequiredValue)
                    / TargetTotalRequiredValue * 100m, 2);
            }
            else
            {
                PortfolioTargetMarginPercent = null;
            }

            AverageJobTargetMarginCount = validRows.Count;
            AverageJobTargetMarginPercent = validRows.Count > 0
                ? Math.Round(validRows.Average(r => r.TargetMarginAgainstTargetPercent!.Value), 2)
                : (decimal?)null;

            OnPropertyChanged(nameof(IsPortfolioLoss));
            OnPropertyChanged(nameof(PortfolioStatusLine));
            OnPropertyChanged(nameof(TargetDataBasisNote));

            // Margin-vs-base verdict lines (derived, so raise explicitly).
            OnPropertyChanged(nameof(AverageVsBaseBand));
            OnPropertyChanged(nameof(AverageVsBaseVerdict));
            OnPropertyChanged(nameof(OverallVsBaseBand));
            OnPropertyChanged(nameof(OverallVsBaseVerdict));
            OnPropertyChanged(nameof(BaseExpectedMarginPercent));
            OnPropertyChanged(nameof(BaseExpectedMarginDisplay));
            OnPropertyChanged(nameof(BaseExpectedMarginFactorLabel));
            OnPropertyChanged(nameof(HasTargetShortfall));
            OnPropertyChanged(nameof(PortfolioTargetStatusLabel));
            OnPropertyChanged(nameof(TopScopeSummary));
            NotifyTopCardsChanged();
        }

        private string BuildStatus(List<JobProfitabilityRow> rows)
        {
            if (_allRows.Count == 0)
                return "No PI-confirmed proforma invoices yet.";

            if (rows.Count == 0)
                return "No PI-confirmed jobs match the current filters.";

            int uncosted = rows.Count(r => !r.IsCosted);

            string basis = $"{rows.Count} of {_allRows.Count} PI-confirmed job(s) shown.";

            // Stale costings first: an overstated profit is more dangerous
            // than a blank one, because it looks like a real number.
            int stale = rows.Count(r => r.NeedsRecalculation);

            if (stale > 0)
            {
                basis += $"  ⚠ {stale} job(s) show OVERSTATED profit — costed " +
                         "before the inner-pane fix. Reopen each, recalculate " +
                         "Step 9, then save.";
            }

            // Target model: a costed job saved before the upgrade has P&L
            // rows but no target-price block — STALE, not a 0% margin.
            int targetStale = rows.Count(r => r.IsSnapshotStale);
            if (targetStale > 0)
            {
                basis += $"  ⚠ {targetStale} job(s) were saved before the " +
                         "target-price model — their target margin is unavailable " +
                         "until each job is reopened in Step 9 and saved again.";
            }

            if (uncosted > 0)
            {
                // The original wording blamed old invoices, but the usual
                // cause is different and far more common: Step 9 only fills
                // in when the tab is opened, so ANY invoice saved without
                // visiting Step 9 persists zero cost. Name both causes.
                basis += $" {uncosted} job(s) have no Profit & Loss data, so " +
                         "their cost, profit and margin are blank — open each " +
                         "invoice, visit Step 9 to recalculate, then save.";
            }

            // Margin vs the company base expected margin (Settings default).
            // Answers "how are we doing against the 15% plan" at a glance:
            //   PI1 16% → above, PI2 14% → below, both together → at (≈15%).
            var costed = rows.Where(r => r.IsCosted).ToList();
            if (costed.Count > 0)
            {
                int above = costed.Count(r => r.IsMarginAboveBase);
                int at = costed.Count(r => r.IsMarginAtBase);
                int below = costed.Count(r => r.IsMarginBelowBase);
                basis += $" Margin vs {BaseExpectedMarginDisplay} base: {above} above · {at} at · {below} below.";
            }

            return basis;
        }
    }
}
