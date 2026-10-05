// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/JobProfitability/PortfolioServiceProfitabilityViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Portfolio Service Profitability Analysis (Aggregated Across All PIs)
//
// WHAT IT IS
//   Aggregates service profitability data across ALL confirmed PIs.
//   Shows total profit from each service type across the entire portfolio.
//
// DATA SOURCE
//   Reads Step 9 P&L data from all "Approved" (confirmed) invoices.
//   Aggregates service-level profit data for portfolio-wide analysis.
//
// USE CASE
//   Answer: "Which services are most profitable across our entire business?"
//   Helps identify which services to promote or adjust pricing for.
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
    /// Portfolio-level service profitability analysis.
    /// Aggregates data from all confirmed PIs.
    /// </summary>
    public class PortfolioServiceProfitabilityViewModel : ViewModelBase
    {
        private readonly IProformaInvoiceRepository _invoices;

        public PortfolioServiceProfitabilityViewModel(IProformaInvoiceRepository invoices)
        {
            _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
            
            ServiceBreakdown = new ObservableCollection<PortfolioServiceProfitRow>();
            RefreshCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(RefreshAnalysisAsync, "PortfolioServiceProfit Refresh"));
            
            // Live sync: re-analyze whenever an invoice is saved/deleted
            ProformaInvoiceRepository.SavedInvoicesChanged += OnSavedInvoicesChanged;
            
            // Auto-load on construction
            AsyncUtility.SafeFireAndForget(RefreshAnalysisAsync, "PortfolioServiceProfit InitialLoad");
        }

        private void OnSavedInvoicesChanged()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                AsyncUtility.SafeFireAndForget(RefreshAnalysisAsync, "PortfolioServiceProfit LiveSync");
            else
                dispatcher.BeginInvoke(new Action(() =>
                    AsyncUtility.SafeFireAndForget(RefreshAnalysisAsync, "PortfolioServiceProfit LiveSync")));
        }

        // ══════════════════════════════════════════════════════════════
        //  PROPERTIES
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<PortfolioServiceProfitRow> ServiceBreakdown { get; }

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

        private decimal _totalServiceCharge;
        public decimal TotalServiceCharge
        {
            get => _totalServiceCharge;
            private set
            {
                if (_totalServiceCharge == value) return;
                _totalServiceCharge = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalServiceChargeDisplay));
            }
        }
        public string TotalServiceChargeDisplay => $"AED {TotalServiceCharge:N0}";

        private decimal _totalServiceProfit;
        public decimal TotalServiceProfit
        {
            get => _totalServiceProfit;
            private set
            {
                if (_totalServiceProfit == value) return;
                _totalServiceProfit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalServiceProfitDisplay));
            }
        }
        public string TotalServiceProfitDisplay => $"AED {TotalServiceProfit:N0}";

        private decimal _overallServiceMarginPercent;
        public decimal OverallServiceMarginPercent
        {
            get => _overallServiceMarginPercent;
            private set
            {
                if (_overallServiceMarginPercent == value) return;
                _overallServiceMarginPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OverallServiceMarginDisplay));
            }
        }
        public string OverallServiceMarginDisplay => $"{OverallServiceMarginPercent:0.#}%";

        private string _bestPerformingService = "";
        public string BestPerformingService
        {
            get => _bestPerformingService;
            private set
            {
                if (_bestPerformingService == value) return;
                _bestPerformingService = value ?? "";
                OnPropertyChanged();
            }
        }

        private string _worstPerformingService = "";
        public string WorstPerformingService
        {
            get => _worstPerformingService;
            private set
            {
                if (_worstPerformingService == value) return;
                _worstPerformingService = value ?? "";
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

        private string _statusMessage = "Press REFRESH to analyze portfolio service profitability.";
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
                ServiceBreakdown.Clear();

                // Load all confirmed PIs
                var invoices = await _invoices.GetByStatusAsync("Approved").ConfigureAwait(true);
                if (invoices == null || invoices.Count == 0)
                {
                    StatusMessage = "No confirmed invoices found.";
                    return;
                }

                TotalJobsAnalyzed = invoices.Count;

                // Aggregate service data across all PIs
                var serviceData = new Dictionary<string, (decimal charge, decimal cost, int jobCount)>();

                foreach (var inv in invoices)
                {
                    var snap = ProformaInvoiceMapper.DeserializeSnapshot(inv);
                    var pl = snap?.ProfitLoss;
                    if (pl == null || pl.Rows.Count == 0) continue;

                    // Analyze each P&L row
                    foreach (var row in pl.Rows)
                    {
                        var serviceType = CategorizeServiceType(row.Kind, row.Description, row.SpecNumber, snap.SpecGroups);
                        if (string.IsNullOrEmpty(serviceType)) continue;

                        var charge = row.Amount;
                        var cost = EstimateServiceCost(row.Kind, row.Description, charge);

                        if (!serviceData.ContainsKey(serviceType))
                            serviceData[serviceType] = (0m, 0m, 0);

                        var (existingCharge, existingCost, existingCount) = serviceData[serviceType];
                        serviceData[serviceType] = (
                            existingCharge + charge,
                            existingCost + cost,
                            existingCount + 1
                        );
                    }
                }

                // Build service breakdown rows
                decimal totalCharge = 0m;
                decimal totalProfit = 0m;

                foreach (var kvp in serviceData.OrderBy(x => x.Key))
                {
                    var serviceType = kvp.Key;
                    var (charge, cost, jobCount) = kvp.Value;
                    var profit = charge - cost;
                    var marginPercent = charge > 0 ? Math.Round((profit / charge) * 100m, 1) : 0m;

                    var row = new PortfolioServiceProfitRow
                    {
                        ServiceName = serviceType,
                        JobCount = jobCount,
                        TotalCharge = charge,
                        TotalCost = cost,
                        TotalProfit = profit,
                        MarginPercent = marginPercent,
                        IsProfitable = profit >= 0,
                        IsBestPerformer = false,
                        IsWorstPerformer = false
                    };

                    ServiceBreakdown.Add(row);
                    totalCharge += charge;
                    totalProfit += profit;
                }

                // Identify best and worst performers
                if (ServiceBreakdown.Count > 0)
                {
                    var best = ServiceBreakdown.OrderByDescending(r => r.MarginPercent).First();
                    best.IsBestPerformer = true;
                    BestPerformingService = best.ServiceName;

                    var worst = ServiceBreakdown.OrderBy(r => r.MarginPercent).First();
                    worst.IsWorstPerformer = true;
                    WorstPerformingService = worst.ServiceName;

                    Recommendation = GenerateRecommendation(best, worst);
                }

                TotalServiceCharge = totalCharge;
                TotalServiceProfit = totalProfit;
                OverallServiceMarginPercent = totalCharge > 0
                    ? Math.Round((totalProfit / totalCharge) * 100m, 1)
                    : 0m;

                StatusMessage = $"Analysis complete. {TotalJobsAnalyzed} job(s) analyzed, {ServiceBreakdown.Count} service type(s) identified.";
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[PortfolioServiceProfitabilityViewModel] Refresh failed: {ExceptionMessages.Deep(ex)}");
                StatusMessage = "Analysis failed. Please check the data and try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string CategorizeServiceType(string kind, string description, int specNumber, List<Models.ProformaInvoice.SavedInvoice.SpecGroupSnapshot> specGroups)
        {
            // Get the spec group to determine glass type
            var specGroup = specGroups?.FirstOrDefault(s => s.GroupNumber == specNumber);
            var glassType = specGroup?.SelectedGlassType?.ToUpperInvariant() ?? "";

            if (kind == "Glass") return "Glass Margin";

            if (kind == "Spacer")
            {
                // DGU: Spacer charge INCLUDES cutting + tempering + production
                // Don't categorize separately - it's a bundled service
                return "Spacer/ASP Service (incl. cutting, tempering, production)";
            }

            if (kind == "Production")
            {
                var desc = description ?? "";

                // Check if this is for DGU (spacer already includes these)
                if (glassType.Contains("DGU") && !glassType.Contains("LAM"))
                {
                    // DGU: These are already included in spacer charge
                    // But if they appear as separate rows, they're extras
                    if (desc.Contains("U-Insert") || desc.Contains("U Insert"))
                        return "U-Insert Service";
                    if (desc.Contains("Hole") || desc.Contains("Cutout"))
                        return "Holes/Cutouts Service";
                    // Skip cutting/tempering for DGU (already in spacer)
                    return null;
                }

                // SGU or LAM: Cutting and tempering are SEPARATE charges
                if (desc.Contains("Cutting"))
                    return "Cutting Service";
                if (desc.Contains("Tempering"))
                    return "Tempering Service";
                if (desc.Contains("interlayer") || desc.Contains("PVB"))
                    return "PVB Interlayer Service (outsourced)";
                if (desc.Contains("Hole") || desc.Contains("Cutout"))
                    return "Holes/Cutouts Service";
                if (desc.Contains("Polish"))
                    return "Polishing Service";

                return "Other Production Service";
            }

            if (kind == "UInsert") return "U-Insert Service";

            return null;
        }

        private decimal EstimateServiceCost(string kind, string description, decimal charge)
        {
            // Estimate actual cost based on service type
            if (kind == "Glass") return charge * 0.95m; // 5% margin on glass
            if (kind == "Spacer") return charge * 0.60m; // 40% margin on spacer
            if (kind == "Production")
            {
                var desc = description ?? "";
                if (desc.Contains("Cutting")) return charge * 1.25m; // Cutting often loses money
                if (desc.Contains("Tempering")) return charge * 1.25m; // Tempering often loses money
                if (desc.Contains("interlayer")) return charge * 0.625m; // PVB is profitable
            }
            if (kind == "UInsert") return charge * 1.0m; // Break-even on U-insert
            return charge; // Default: assume break-even
        }

        private string GenerateRecommendation(PortfolioServiceProfitRow best, PortfolioServiceProfitRow worst)
        {
            var recommendations = new List<string>();

            recommendations.Add($"Promote {best.ServiceName} (margin: {best.MarginPercent:0.#}%)");

            if (worst.MarginPercent < 10m)
            {
                recommendations.Add($"Review {worst.ServiceName} pricing (margin only {worst.MarginPercent:0.#}%)");
            }

            if (worst.MarginPercent < 0m)
            {
                recommendations.Add($"{worst.ServiceName} is losing money — increase prices immediately");
            }

            return string.Join(". ", recommendations) + ".";
        }
    }

    /// <summary>
    /// Portfolio service profit row.
    /// </summary>
    public class PortfolioServiceProfitRow : ViewModelBase
    {
        private string _serviceName = "";
        public string ServiceName
        {
            get => _serviceName;
            set { if (_serviceName == value) return; _serviceName = value ?? ""; OnPropertyChanged(); }
        }

        private int _jobCount;
        public int JobCount
        {
            get => _jobCount;
            set { if (_jobCount == value) return; _jobCount = value; OnPropertyChanged(); }
        }

        private decimal _totalCharge;
        public decimal TotalCharge
        {
            get => _totalCharge;
            set
            {
                if (_totalCharge == value) return;
                _totalCharge = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalChargeDisplay));
            }
        }
        public string TotalChargeDisplay => $"AED {TotalCharge:N0}";

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
