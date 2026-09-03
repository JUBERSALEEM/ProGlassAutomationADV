// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/JobProfitability/PortfolioGlassTypeComparisonViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Portfolio Glass Type Comparison (Aggregated Across All PIs)
//
// WHAT IT IS
//   Aggregates glass type performance data across ALL confirmed PIs.
//   Compares profitability of DGU, SGU, LAM, DGU+LAM across the portfolio.
//
// DATA SOURCE
//   Reads Step 5 spec group data from all "Approved" (confirmed) invoices.
//   Aggregates glass type performance for portfolio-wide analysis.
//
// USE CASE
//   Answer: "Which glass types are most profitable across our entire business?"
//   Helps identify which products to promote or adjust pricing for.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.JobProfitability
{
    /// <summary>
    /// Portfolio-level glass type comparison.
    /// Aggregates data from all confirmed PIs.
    /// </summary>
    public class PortfolioGlassTypeComparisonViewModel : ViewModelBase
    {
        private readonly IProformaInvoiceRepository _invoices;

        public PortfolioGlassTypeComparisonViewModel(IProformaInvoiceRepository invoices)
        {
            _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
            
            GlassTypeRows = new ObservableCollection<PortfolioGlassTypeRow>();
            RefreshCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(RefreshAnalysisAsync, "PortfolioGlassType Refresh"));
            
            // Live sync: re-analyze whenever an invoice is saved/deleted
            ProformaInvoiceRepository.SavedInvoicesChanged += OnSavedInvoicesChanged;
            
            // Auto-load on construction
            AsyncUtility.SafeFireAndForget(RefreshAnalysisAsync, "PortfolioGlassType InitialLoad");
        }

        private void OnSavedInvoicesChanged()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                AsyncUtility.SafeFireAndForget(RefreshAnalysisAsync, "PortfolioGlassType LiveSync");
            else
                dispatcher.BeginInvoke(new Action(() =>
                    AsyncUtility.SafeFireAndForget(RefreshAnalysisAsync, "PortfolioGlassType LiveSync")));
        }

        // ══════════════════════════════════════════════════════════════
        //  PROPERTIES
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<PortfolioGlassTypeRow> GlassTypeRows { get; }

        private int _totalJobsAnalyzed;
        public int TotalJobsAnalyzed
        {
            get => _totalJobsAnalyzed;
            private set
            {
                if (_totalJobsAnalyzed == value) return;
                _totalJobsAnalyzed = value;
                OnPropertyChanged();
            }
        }

        private decimal _totalRevenue;
        public decimal TotalRevenue
        {
            get => _totalRevenue;
            private set
            {
                if (_totalRevenue == value) return;
                _totalRevenue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalRevenueDisplay));
            }
        }
        public string TotalRevenueDisplay => $"AED {TotalRevenue:N0}";

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
            }
        }
        public string TotalCostDisplay => $"AED {TotalCost:N0}";

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
            }
        }
        public string TotalProfitDisplay => $"AED {TotalProfit:N0}";

        private decimal _overallMarginPercent;
        public decimal OverallMarginPercent
        {
            get => _overallMarginPercent;
            private set
            {
                if (_overallMarginPercent == value) return;
                _overallMarginPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OverallMarginDisplay));
            }
        }
        public string OverallMarginDisplay => $"{OverallMarginPercent:0.#}%";

        private string _bestPerformingGlassType = "";
        public string BestPerformingGlassType
        {
            get => _bestPerformingGlassType;
            private set
            {
                if (_bestPerformingGlassType == value) return;
                _bestPerformingGlassType = value ?? "";
                OnPropertyChanged();
            }
        }

        private string _worstPerformingGlassType = "";
        public string WorstPerformingGlassType
        {
            get => _worstPerformingGlassType;
            private set
            {
                if (_worstPerformingGlassType == value) return;
                _worstPerformingGlassType = value ?? "";
                OnPropertyChanged();
            }
        }

        private string _recommendation = "";
        public string Recommendation
        {
            get => _recommendation;
            private set
            {
                if (_recommendation == value) return;
                _recommendation = value ?? "";
                OnPropertyChanged();
            }
        }

        private string _statusMessage = "Press REFRESH to analyze portfolio glass type performance.";
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

        private string _emptyStateTitle = "No glass type data available";
        /// <summary>Title of the empty-state panel (only visible when there are no rows).</summary>
        public string EmptyStateTitle
        {
            get => _emptyStateTitle;
            private set
            {
                if (_emptyStateTitle == value) return;
                _emptyStateTitle = value ?? "";
                OnPropertyChanged();
            }
        }

        private string _emptyStateMessage =
            "Glass type comparison appears after confirming PIs with glass specifications.";
        /// <summary>
        /// Explains WHY the table is empty whenever it is — jobs that exist
        /// but were saved before the Step 9 P&amp;L / target model are reported
        /// here as unavailable (reopen + save), never as a fake 0% or a bare
        /// "no data" that hides the real rows in Job Profitability Tab 1.
        /// </summary>
        public string EmptyStateMessage
        {
            get => _emptyStateMessage;
            private set
            {
                if (_emptyStateMessage == value) return;
                _emptyStateMessage = value ?? "";
                OnPropertyChanged();
            }
        }

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

        public ICommand RefreshCommand { get; }

        // ══════════════════════════════════════════════════════════════
        //  METHODS
        // ══════════════════════════════════════════════════════════════

        public async Task RefreshAnalysisAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                GlassTypeRows.Clear();

                // Load all confirmed PIs
                var invoices = await _invoices.GetByStatusAsync("Approved").ConfigureAwait(true);
                if (invoices == null || invoices.Count == 0)
                {
                    TotalJobsAnalyzed = 0;
                    TotalRevenue = 0m;
                    TotalCost = 0m;
                    TotalProfit = 0m;
                    OverallMarginPercent = 0m;
                    BestPerformingGlassType = "";
                    WorstPerformingGlassType = "";
                    Recommendation = "";
                    StatusMessage = "No PI-confirmed invoices found.";
                    EmptyStateTitle = "No glass type data available";
                    EmptyStateMessage =
                        "Glass type comparison appears after confirming PIs with glass specifications.";
                    return;
                }

                TotalJobsAnalyzed = invoices.Count;

                // ── Evidence counters (feed honest empty-state text) ─────
                // A confirmed job can exist while this tab still has nothing
                // to show (legacy snapshot without Step 9 P&L rows, or spec
                // groups that cannot be mapped to saved cost rows). Those
                // jobs are reported as unavailable — never silently skipped
                // and never invented as SGU with a fake margin.
                int jobsWithSavedProfitLoss = 0;
                int specsScanned = 0;
                int specsWithCostRows = 0;

                // Aggregate glass type data across all PIs
                var glassTypeData = new Dictionary<string, (decimal revenue, decimal cost, int jobCount)>();

                foreach (var inv in invoices)
                {
                    var snap = ProformaInvoiceMapper.DeserializeSnapshot(inv);
                    if (snap == null) continue;

                    // Get Step 9 P&L data
                    var pl = snap.ProfitLoss;
                    if (pl == null || pl.Rows.Count == 0) continue;

                    jobsWithSavedProfitLoss++;

                    // Analyze each spec group
                    foreach (var spec in snap.SpecGroups)
                    {
                        specsScanned++;

                        // Get P&L rows for this spec
                        var specRows = pl.Rows.Where(r => r.SpecNumber == spec.GroupNumber).ToList();
                        if (specRows.Count == 0) continue; // cannot attribute cost → not evidence

                        specsWithCostRows++;

                        // Classify from real evidence, newest first: the Step 4
                        // glass-type code persisted with the spec, then the
                        // spec name text, then the P&L line descriptions.
                        // Only when NOTHING carries a marker does the spec land
                        // in an explicit "Unclassified" bucket — it is never
                        // silently dropped or guessed as SGU.
                        var glassType = DetectGlassType(spec.SelectedGlassType);
                        if (string.IsNullOrEmpty(glassType))
                            glassType = DetectGlassType(
                                $"{spec.SelectedSpecName} {spec.GroupName}");
                        if (string.IsNullOrEmpty(glassType))
                            glassType = DetectGlassType(
                                string.Join(" ", specRows.Select(r => r.SpecName ?? "")));
                        if (string.IsNullOrEmpty(glassType))
                            glassType = "Unclassified";

                        var cost = specRows.Sum(r => r.Amount);
                        if (cost <= 0m) continue; // a zero-cost row is not evidence either
                        var revenue = cost * 1.15m; // Estimate 15% markup (legacy view basis)

                        if (!glassTypeData.ContainsKey(glassType))
                            glassTypeData[glassType] = (0m, 0m, 0);

                        var (existingRevenue, existingCost, existingCount) = glassTypeData[glassType];
                        glassTypeData[glassType] = (
                            existingRevenue + revenue,
                            existingCost + cost,
                            existingCount + 1
                        );
                    }
                }

                // Build glass type comparison rows
                decimal totalRevenue = 0m;
                decimal totalCost = 0m;
                decimal totalProfit = 0m;

                foreach (var kvp in glassTypeData.OrderByDescending(x => x.Value.revenue))
                {
                    var glassType = kvp.Key;
                    var (revenue, cost, jobCount) = kvp.Value;
                    var profit = revenue - cost;
                    var marginPercent = revenue > 0 ? Math.Round((profit / revenue) * 100m, 1) : 0m;

                    var row = new PortfolioGlassTypeRow
                    {
                        GlassType = glassType,
                        JobCount = jobCount,
                        TotalRevenue = revenue,
                        TotalCost = cost,
                        TotalProfit = profit,
                        MarginPercent = marginPercent,
                        IsProfitable = profit >= 0,
                        IsBestPerformer = false,
                        IsWorstPerformer = false
                    };

                    GlassTypeRows.Add(row);
                    totalRevenue += revenue;
                    totalCost += cost;
                    totalProfit += profit;
                }

                // Identify best and worst performers
                if (GlassTypeRows.Count > 0)
                {
                    var best = GlassTypeRows.OrderByDescending(r => r.MarginPercent).First();
                    best.IsBestPerformer = true;
                    BestPerformingGlassType = best.GlassType;

                    var worst = GlassTypeRows.OrderBy(r => r.MarginPercent).First();
                    worst.IsWorstPerformer = true;
                    WorstPerformingGlassType = worst.GlassType;

                    Recommendation = GenerateRecommendation(best, worst);
                }
                else
                {
                    Recommendation = "";
                }

                TotalRevenue = totalRevenue;
                TotalCost = totalCost;
                TotalProfit = totalProfit;
                OverallMarginPercent = totalRevenue > 0
                    ? Math.Round((totalProfit / totalRevenue) * 100m, 1)
                    : 0m;

                // ── Status + empty-state honesty ─────────────────────────
                if (GlassTypeRows.Count > 0)
                {
                    EmptyStateTitle = "No glass type data available";
                    EmptyStateMessage =
                        "Glass type comparison appears after confirming PIs with glass specifications.";

                    string basis = $"Analysis complete. {jobsWithSavedProfitLoss} job(s) with a saved Step 9 P&L, " +
                                   $"{GlassTypeRows.Count} glass type(s) identified.";

                    int skipped = specsScanned - specsWithCostRows;
                    if (skipped > 0 || jobsWithSavedProfitLoss < TotalJobsAnalyzed)
                    {
                        basis += "  Note: " +
                                 $"{TotalJobsAnalyzed - jobsWithSavedProfitLoss} confirmed job(s) have no saved Step 9 P&L, " +
                                 $"and {skipped} spec group(s) could not be matched to cost rows — those are excluded " +
                                 "(legacy jobs need reopening in Step 9 and saving).";
                    }

                    StatusMessage = basis;
                }
                else
                {
                    EmptyStateTitle = "Nothing to compare yet";

                    if (specsScanned == 0)
                    {
                        EmptyStateMessage =
                            $"{jobsWithSavedProfitLoss} confirmed job(s) carry a saved Step 9 P&L but none contain " +
                            "spec groups to classify. Reopen a job in the wizard and save it to refresh.";
                    }
                    else
                    {
                        EmptyStateMessage =
                            $"{jobsWithSavedProfitLoss} of {TotalJobsAnalyzed} confirmed job(s) carry a saved Step 9 P&L. " +
                            $"{specsScanned} spec group(s) were scanned but none could be matched to saved cost rows " +
                            $"with a detectable glass type (DGU / SGU / LAM / DGU+LAM). " +
                            "Reopen each job in Step 9, recalculate and save — jobs saved before the P&L target model " +
                            "are shown here as unavailable, never as a fake 0%.";
                    }

                    StatusMessage = EmptyStateMessage;
                }
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[PortfolioGlassTypeComparisonViewModel] Refresh failed: {ExceptionMessages.Deep(ex)}");
                StatusMessage = "Analysis failed. Please check the data and try again.";
                EmptyStateTitle = "Analysis failed";
                EmptyStateMessage = "Glass type comparison could not be completed. Please check the data and try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string? DetectGlassType(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var upper = raw.ToUpperInvariant();
            if (upper.Contains("DGU") && upper.Contains("LAM")) return "DGU+LAM";
            if (upper.Contains("DGU")) return "DGU";
            if (upper.Contains("LAM")) return "LAM";
            if (upper.Contains("SGU")) return "SGU";

            // A marker-free but present code/name is real evidence of a spec
            // (e.g. "Tempered 6mm Clear" with no build token in the text) —
            // classify it explicitly instead of guessing a build.
            return null;
        }

        private string GenerateRecommendation(PortfolioGlassTypeRow best, PortfolioGlassTypeRow worst)
        {
            var recommendations = new List<string>();

            recommendations.Add($"Promote {best.GlassType} (margin: {best.MarginPercent:0.#}%)");

            if (worst.MarginPercent < 10m)
            {
                recommendations.Add($"Review {worst.GlassType} pricing (margin only {worst.MarginPercent:0.#}%)");
            }

            if (worst.MarginPercent < 5m)
            {
                var requiredIncrease = (10m - worst.MarginPercent);
                recommendations.Add($"Increase {worst.GlassType} prices by ~{requiredIncrease:0}% to achieve 10% margin");
            }

            var marginDifference = best.MarginPercent - worst.MarginPercent;
            if (marginDifference > 15m)
            {
                recommendations.Add($"Significant margin gap: {marginDifference:0.#}% between best and worst performers");
            }

            return string.Join(". ", recommendations) + ".";
        }
    }

    /// <summary>
    /// Portfolio glass type row.
    /// </summary>
    public class PortfolioGlassTypeRow : ViewModelBase
    {
        private string _glassType = "";
        public string GlassType
        {
            get => _glassType;
            set { if (_glassType == value) return; _glassType = value ?? ""; OnPropertyChanged(); }
        }

        private int _jobCount;
        public int JobCount
        {
            get => _jobCount;
            set { if (_jobCount == value) return; _jobCount = value; OnPropertyChanged(); }
        }

        private decimal _totalRevenue;
        public decimal TotalRevenue
        {
            get => _totalRevenue;
            set
            {
                if (_totalRevenue == value) return;
                _totalRevenue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalRevenueDisplay));
            }
        }
        public string TotalRevenueDisplay => $"AED {TotalRevenue:N0}";

        private decimal _totalCost;
        public decimal TotalCost
        {
            get => _totalCost;
            set
            {
                if (_totalCost == value) return;
                _totalCost = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalCostDisplay));
            }
        }
        public string TotalCostDisplay => $"AED {TotalCost:N0}";

        private decimal _totalProfit;
        public decimal TotalProfit
        {
            get => _totalProfit;
            set
            {
                if (_totalProfit == value) return;
                _totalProfit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalProfitDisplay));
            }
        }
        public string TotalProfitDisplay => $"AED {TotalProfit:N0}";

        private decimal _marginPercent;
        public decimal MarginPercent
        {
            get => _marginPercent;
            set
            {
                if (_marginPercent == value) return;
                _marginPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MarginDisplay));
            }
        }
        public string MarginDisplay => $"{MarginPercent:0.#}%";

        private bool _isProfitable = true;
        public bool IsProfitable
        {
            get => _isProfitable;
            set { if (_isProfitable == value) return; _isProfitable = value; OnPropertyChanged(); }
        }

        private bool _isBestPerformer;
        public bool IsBestPerformer
        {
            get => _isBestPerformer;
            set { if (_isBestPerformer == value) return; _isBestPerformer = value; OnPropertyChanged(); }
        }

        private bool _isWorstPerformer;
        public bool IsWorstPerformer
        {
            get => _isWorstPerformer;
            set { if (_isWorstPerformer == value) return; _isWorstPerformer = value; OnPropertyChanged(); }
        }
    }
}
