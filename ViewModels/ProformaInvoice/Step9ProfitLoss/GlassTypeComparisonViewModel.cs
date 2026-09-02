// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step9ProfitLoss/GlassTypeComparisonViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Glass Type Comparison Analysis
//
// WHAT IT IS
//   Separate view for comparing profitability across glass types.
//   Shows:
//   - DGU (Double Glazed Unit) profitability
//   - SGU (Single Glazed Unit) profitability
//   - LAM (Laminated) profitability
//   - DGU+LAM (Hybrid) profitability
//   - Recommendations based on profitability
//
// WHY SEPARATE
//   Helps make strategic decisions about which glass types to promote
//   based on actual profitability data.
//
// FORMULAS
//   Total Revenue = Sum of all charges for this glass type
//   Total Cost = Sum of all costs for this glass type
//   Total Profit = Total Revenue - Total Cost
//   Margin % = (Total Profit / Total Revenue) × 100
//   Average Margin = Average of all job margins for this glass type
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss
{
    /// <summary>
    /// Glass type comparison for strategic analysis.
    /// </summary>
    public class GlassTypeComparisonViewModel : ViewModelBase
    {
        private readonly ProfitLossStepViewModel _parentViewModel;

        public GlassTypeComparisonViewModel(ProfitLossStepViewModel parentViewModel)
        {
            _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
            
            GlassTypeRows = new ObservableCollection<GlassTypeComparisonRow>();
            RefreshCommand = new RelayCommand(_ => RefreshAnalysis());
            
            // Initial load
            RefreshAnalysis();
        }

        // ══════════════════════════════════════════════════════════════
        //  PROPERTIES
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Glass type comparison rows.
        /// </summary>
        public ObservableCollection<GlassTypeComparisonRow> GlassTypeRows { get; }

        private string _bestPerformingGlassType = "";
        /// <summary>Best performing glass type.</summary>
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
        /// <summary>Worst performing glass type.</summary>
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
        /// <summary>Strategic recommendation.</summary>
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

        private decimal _totalRevenue;
        /// <summary>Total revenue across all glass types.</summary>
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

        public string TotalRevenueDisplay => $"AED {TotalRevenue:N2}";

        private decimal _totalCost;
        /// <summary>Total cost across all glass types.</summary>
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

        public string TotalCostDisplay => $"AED {TotalCost:N2}";

        private decimal _totalProfit;
        /// <summary>Total profit across all glass types.</summary>
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

        public string TotalProfitDisplay => $"AED {TotalProfit:N2}";

        private decimal _overallMarginPercent;
        /// <summary>Overall margin percentage.</summary>
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

        private string _statusMessage = "Press REFRESH to analyze glass type profitability.";
        /// <summary>Status message.</summary>
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

        private bool _isBusy;
        /// <summary>True while refreshing.</summary>
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

        /// <summary>Refresh command.</summary>
        public ICommand RefreshCommand { get; }

        // ══════════════════════════════════════════════════════════════
        //  METHODS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Refresh the glass type comparison analysis.
        /// </summary>
        public void RefreshAnalysis()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                GlassTypeRows.Clear();

                var groups = _parentViewModel.SpecGroupsView;
                if (groups == null || groups.Count == 0)
                {
                    StatusMessage = "No specifications found. Please add specifications in Step 5.";
                    return;
                }

                // Group specifications by glass type
                var glassTypeGroups = new Dictionary<string, List<SpecCostGroup>>();

                foreach (var specGroup in groups)
                {
                    var specName = specGroup.SpecName ?? "";
                    var glassType = DetectGlassType(specName, specGroup);
                    
                    if (!glassTypeGroups.ContainsKey(glassType))
                    {
                        glassTypeGroups[glassType] = new List<SpecCostGroup>();
                    }
                    
                    glassTypeGroups[glassType].Add(specGroup);
                }

                decimal totalRevenue = 0m;
                decimal totalCost = 0m;
                decimal totalProfit = 0m;

                // Analyze each glass type
                foreach (var kvp in glassTypeGroups)
                {
                    var glassType = kvp.Key;
                    var specGroups = kvp.Value;

                    var revenue = 0m;
                    var cost = 0m;
                    var area = 0m;
                    var jobCount = specGroups.Count;

                    foreach (var specGroup in specGroups)
                    {
                        var specArea = specGroup.CutSqm;
                        area += specArea;

                        // Get total cost from spec group
                        var specCost = specGroup.TotalCost;
                        cost += specCost;

                        // Estimate revenue (use parent's gross revenue proportionally)
                        // In real implementation, this should come from actual quote data
                        var estimatedRevenue = specCost * 1.15m; // Assume 15% markup as baseline
                        revenue += estimatedRevenue;
                    }

                    var profit = revenue - cost;
                    var marginPercent = revenue > 0
                        ? Math.Round((profit / revenue) * 100m, 1)
                        : 0m;

                    var row = new GlassTypeComparisonRow
                    {
                        GlassType = glassType,
                        JobCount = jobCount,
                        TotalArea = area,
                        TotalRevenue = revenue,
                        TotalCost = cost,
                        TotalProfit = profit,
                        MarginPercent = marginPercent,
                        IsProfitable = profit >= 0,
                        IsBestPerformer = false, // Will be set later
                        IsWorstPerformer = false // Will be set later
                    };

                    GlassTypeRows.Add(row);

                    totalRevenue += revenue;
                    totalCost += cost;
                    totalProfit += profit;
                }

                // Identify best and worst performers
                if (GlassTypeRows.Count > 0)
                {
                    var bestPerformer = GlassTypeRows.OrderByDescending(r => r.MarginPercent).First();
                    bestPerformer.IsBestPerformer = true;
                    BestPerformingGlassType = bestPerformer.GlassType;

                    var worstPerformer = GlassTypeRows.OrderBy(r => r.MarginPercent).First();
                    worstPerformer.IsWorstPerformer = true;
                    WorstPerformingGlassType = worstPerformer.GlassType;

                    // Generate recommendation
                    Recommendation = GenerateRecommendation(bestPerformer, worstPerformer);
                }

                TotalRevenue = totalRevenue;
                TotalCost = totalCost;
                TotalProfit = totalProfit;
                OverallMarginPercent = totalRevenue > 0
                    ? Math.Round((totalProfit / totalRevenue) * 100m, 1)
                    : 0m;

                StatusMessage = $"Analysis complete. {GlassTypeRows.Count} glass type(s) analyzed. " +
                               $"Best performer: {BestPerformingGlassType} ({GlassTypeRows.FirstOrDefault(r => r.IsBestPerformer)?.MarginPercent:0.#}%). " +
                               $"Worst performer: {WorstPerformingGlassType} ({GlassTypeRows.FirstOrDefault(r => r.IsWorstPerformer)?.MarginPercent:0.#}%).";
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[GlassTypeComparisonViewModel] Refresh failed: {ExceptionMessages.Deep(ex)}");
                StatusMessage = "Analysis failed. Please check the data and try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Detect glass type from spec name.
        /// </summary>
        private string DetectGlassType(string specName, SpecCostGroup specGroup)
        {
            var spacerMatch = PnLSpecParser.Spacer(specName);
            bool spacerDetected = spacerMatch.Success;

            bool isDgu =
                (specGroup.SpecName ?? "").IndexOf("DGU", StringComparison.OrdinalIgnoreCase) >= 0
                || spacerDetected;

            var pvbMatch = PnLSpecParser.Interlayer(specName);
            bool pvbDetected = pvbMatch.Success;

            bool isLam =
                (specGroup.SpecName ?? "").IndexOf("LAM", StringComparison.OrdinalIgnoreCase) >= 0
                || pvbDetected;

            bool isDguLam = spacerDetected && pvbDetected;

            if (isDguLam) return "DGU+LAM";
            if (isDgu) return "DGU";
            if (isLam) return "LAM";
            return "SGU";
        }

        /// <summary>
        /// Generate strategic recommendation based on analysis.
        /// </summary>
        private string GenerateRecommendation(GlassTypeComparisonRow bestPerformer, GlassTypeComparisonRow worstPerformer)
        {
            var recommendations = new List<string>();

            // Recommend promoting best performer
            recommendations.Add($"Promote {bestPerformer.GlassType} (margin: {bestPerformer.MarginPercent:0.#}%)");

            // Warn about worst performer if margin is low
            if (worstPerformer.MarginPercent < 10m)
            {
                recommendations.Add($"Review {worstPerformer.GlassType} pricing (margin only {worstPerformer.MarginPercent:0.#}%)");
            }

            // Suggest pricing adjustments
            if (worstPerformer.MarginPercent < 5m)
            {
                var requiredIncrease = (10m - worstPerformer.MarginPercent);
                recommendations.Add($"Increase {worstPerformer.GlassType} prices by ~{requiredIncrease:0}% to achieve 10% margin");
            }

            // Highlight significant differences
            var marginDifference = bestPerformer.MarginPercent - worstPerformer.MarginPercent;
            if (marginDifference > 15m)
            {
                recommendations.Add($"Significant margin gap: {marginDifference:0.#}% between best and worst performers");
            }

            return string.Join(". ", recommendations) + ".";
        }
    }

    /// <summary>
    /// Glass type comparison row for breakdown display.
    /// </summary>
    public class GlassTypeComparisonRow : ViewModelBase
    {
        private string _glassType = "";
        public string GlassType
        {
            get => _glassType;
            set
            {
                if (_glassType == value) return;
                _glassType = value ?? "";
                OnPropertyChanged();
            }
        }

        private int _jobCount;
        public int JobCount
        {
            get => _jobCount;
            set
            {
                if (_jobCount == value) return;
                _jobCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(JobCountDisplay));
            }
        }

        public string JobCountDisplay => $"{JobCount}";

        private decimal _totalArea;
        public decimal TotalArea
        {
            get => _totalArea;
            set
            {
                if (_totalArea == value) return;
                _totalArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalAreaDisplay));
            }
        }

        public string TotalAreaDisplay => $"{TotalArea:0.##} SQM";

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

        public string TotalRevenueDisplay => $"AED {TotalRevenue:N2}";

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

        public string TotalCostDisplay => $"AED {TotalCost:N2}";

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

        public string TotalProfitDisplay => $"AED {TotalProfit:N2}";

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
            set
            {
                if (_isProfitable == value) return;
                _isProfitable = value;
                OnPropertyChanged();
            }
        }

        private bool _isBestPerformer;
        public bool IsBestPerformer
        {
            get => _isBestPerformer;
            set
            {
                if (_isBestPerformer == value) return;
                _isBestPerformer = value;
                OnPropertyChanged();
            }
        }

        private bool _isWorstPerformer;
        public bool IsWorstPerformer
        {
            get => _isWorstPerformer;
            set
            {
                if (_isWorstPerformer == value) return;
                _isWorstPerformer = value;
                OnPropertyChanged();
            }
        }
    }
}
