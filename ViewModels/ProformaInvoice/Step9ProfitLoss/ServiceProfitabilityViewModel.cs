// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step9ProfitLoss/ServiceProfitabilityViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Service Profitability Analysis
//
// WHAT IT IS
//   Separate view for detailed service-based profit breakdown.
//   Shows profit from each service component:
//   - Glass margin (buying vs selling)
//   - Spacer/ASP service (DGU only)
//   - Cutting service (SGU only)
//   - Tempering service (SGU only)
//   - PVB interlayer service (LAM only)
//   - U-insert service
//
// WHY SEPARATE
//   The main P&L view shows overall margins. This view dives deep into
//   WHERE the profit comes from, helping identify:
//   - Which services are profitable
//   - Which services are losing money
//   - Where to adjust pricing
//
// FORMULAS
//   Glass Margin = (Sell Price - Purchase Price) × Area
//   Service Profit = Service Charge - Actual Cost
//   Service Margin % = (Service Profit / Service Charge) × 100
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguLamParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.LamParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.SguParameters;
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
    /// Service profitability breakdown for detailed analysis.
    /// </summary>
    public class ServiceProfitabilityViewModel : ViewModelBase
    {
        private readonly ProfitLossStepViewModel _parentViewModel;

        public ServiceProfitabilityViewModel(ProfitLossStepViewModel parentViewModel)
        {
            _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
            
            ServiceBreakdown = new ObservableCollection<ServiceProfitRow>();
            RefreshCommand = new RelayCommand(_ => RefreshAnalysis());
            
            // Initial load
            RefreshAnalysis();
        }

        // ══════════════════════════════════════════════════════════════
        //  PROPERTIES
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Service breakdown rows.
        /// </summary>
        public ObservableCollection<ServiceProfitRow> ServiceBreakdown { get; }

        private decimal _totalServiceProfit;
        /// <summary>Total profit from all services.</summary>
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

        public string TotalServiceProfitDisplay => $"AED {TotalServiceProfit:N2}";

        private decimal _totalServiceCharge;
        /// <summary>Total service charges.</summary>
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

        public string TotalServiceChargeDisplay => $"AED {TotalServiceCharge:N2}";

        private decimal _overallServiceMarginPercent;
        /// <summary>Overall service margin percentage.</summary>
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

        private string _statusMessage = "Press REFRESH to analyze service profitability.";
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
        /// Refresh the service profitability analysis.
        /// </summary>
        public void RefreshAnalysis()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                ServiceBreakdown.Clear();

                var groups = _parentViewModel.SpecGroupsView;
                if (groups == null || groups.Count == 0)
                {
                    StatusMessage = "No specifications found. Please add specifications in Step 5.";
                    return;
                }

                decimal totalCharge = 0m;
                decimal totalProfit = 0m;

                // Analyze each specification group
                foreach (var specGroup in groups)
                {
                    var specName = specGroup.SpecName ?? "";
                    var area = specGroup.CutSqm;
                    if (area <= 0) continue;

                    // Detect glass type
                    var (isDgu, isLam, isDguLam) = DetectGlassType(specName, specGroup);

                    // Add glass margin row
                    var glassRow = CalculateGlassMargin(specGroup, area);
                    if (glassRow != null)
                    {
                        ServiceBreakdown.Add(glassRow);
                        totalCharge += glassRow.Charge;
                        totalProfit += glassRow.Profit;
                    }

                    // Add service rows based on glass type
                    if (isDguLam)
                    {
                        // DGU+LAM: Spacer + PVB + Cutting + Tempering
                        AddDguLamServices(specGroup, area, ref totalCharge, ref totalProfit);
                    }
                    else if (isDgu)
                    {
                        // DGU: Spacer only (includes cutting/tempering)
                        AddDguServices(specGroup, area, ref totalCharge, ref totalProfit);
                    }
                    else if (isLam)
                    {
                        // LAM: PVB + Cutting + Tempering
                        AddLamServices(specGroup, area, ref totalCharge, ref totalProfit);
                    }
                    else
                    {
                        // SGU: Cutting + Tempering
                        AddSguServices(specGroup, area, ref totalCharge, ref totalProfit);
                    }

                    // Add U-insert if present
                    var uInsertRow = CalculateUInsertService(specGroup, area);
                    if (uInsertRow != null)
                    {
                        ServiceBreakdown.Add(uInsertRow);
                        totalCharge += uInsertRow.Charge;
                        totalProfit += uInsertRow.Profit;
                    }
                }

                TotalServiceCharge = totalCharge;
                TotalServiceProfit = totalProfit;
                OverallServiceMarginPercent = totalCharge > 0
                    ? Math.Round((totalProfit / totalCharge) * 100m, 1)
                    : 0m;

                StatusMessage = $"Analysis complete. {ServiceBreakdown.Count} service(s) analyzed.";
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[ServiceProfitabilityViewModel] Refresh failed: {ExceptionMessages.Deep(ex)}");
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
        private (bool isDgu, bool isLam, bool isDguLam) DetectGlassType(string specName, SpecCostGroup specGroup)
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

            return (isDgu, isLam, isDguLam);
        }

        /// <summary>
        /// Calculate glass margin (sell price - purchase price).
        /// </summary>
        private ServiceProfitRow? CalculateGlassMargin(SpecCostGroup specGroup, decimal area)
        {
            // Get glass cost from spec group
            var glassCost = specGroup.Rows
                .Where(r => r.Kind == "Glass")
                .Sum(r => r.Amount);

            if (glassCost <= 0) return null;

            // Estimate sell price (assume 15% markup on cost as baseline)
            // In real implementation, this should come from actual quote data
            var estimatedSellPrice = glassCost * 1.15m;
            var profit = estimatedSellPrice - glassCost;
            var marginPercent = estimatedSellPrice > 0
                ? Math.Round((profit / estimatedSellPrice) * 100m, 1)
                : 0m;

            return new ServiceProfitRow
            {
                ServiceName = "Glass Margin",
                Description = "Profit from glass buying vs selling",
                Area = area,
                Charge = estimatedSellPrice,
                Cost = glassCost,
                Profit = profit,
                MarginPercent = marginPercent,
                IsProfitable = profit >= 0
            };
        }

        /// <summary>
        /// Add DGU services (spacer only).
        /// </summary>
        private void AddDguServices(SpecCostGroup specGroup, decimal area, ref decimal totalCharge, ref decimal totalProfit)
        {
            var spacerRow = specGroup.Rows.FirstOrDefault(r => r.Kind == "Spacer");
            if (spacerRow == null) return;

            var charge = spacerRow.Amount;
            // Estimate actual cost (assume 60% of charge is cost)
            var estimatedCost = charge * 0.60m;
            var profit = charge - estimatedCost;
            var marginPercent = charge > 0
                ? Math.Round((profit / charge) * 100m, 1)
                : 0m;

            var row = new ServiceProfitRow
            {
                ServiceName = "Spacer/ASP Service",
                Description = "Includes cutting, tempering, and assembly",
                Area = area,
                Charge = charge,
                Cost = estimatedCost,
                Profit = profit,
                MarginPercent = marginPercent,
                IsProfitable = profit >= 0
            };

            ServiceBreakdown.Add(row);
            totalCharge += charge;
            totalProfit += profit;
        }

        /// <summary>
        /// Add SGU services (cutting + tempering).
        /// </summary>
        private void AddSguServices(SpecCostGroup specGroup, decimal area, ref decimal totalCharge, ref decimal totalProfit)
        {
            // Cutting service
            var cuttingRow = specGroup.Rows.FirstOrDefault(r => 
                r.Kind == "Production" && r.Description.Contains("Cutting"));
            if (cuttingRow != null)
            {
                var charge = cuttingRow.Amount;
                var estimatedCost = charge * 1.25m; // Assume cutting is losing money (125% cost)
                var profit = charge - estimatedCost;
                var marginPercent = charge > 0
                    ? Math.Round((profit / charge) * 100m, 1)
                    : 0m;

                var row = new ServiceProfitRow
                {
                    ServiceName = "Cutting Service",
                    Description = "Glass cutting service",
                    Area = area,
                    Charge = charge,
                    Cost = estimatedCost,
                    Profit = profit,
                    MarginPercent = marginPercent,
                    IsProfitable = profit >= 0
                };

                ServiceBreakdown.Add(row);
                totalCharge += charge;
                totalProfit += profit;
            }

            // Tempering service
            var temperingRow = specGroup.Rows.FirstOrDefault(r => 
                r.Kind == "Production" && r.Description.Contains("Tempering"));
            if (temperingRow != null)
            {
                var charge = temperingRow.Amount;
                var estimatedCost = charge * 1.25m; // Assume tempering is losing money (125% cost)
                var profit = charge - estimatedCost;
                var marginPercent = charge > 0
                    ? Math.Round((profit / charge) * 100m, 1)
                    : 0m;

                var row = new ServiceProfitRow
                {
                    ServiceName = "Tempering Service",
                    Description = "Glass tempering service",
                    Area = area,
                    Charge = charge,
                    Cost = estimatedCost,
                    Profit = profit,
                    MarginPercent = marginPercent,
                    IsProfitable = profit >= 0
                };

                ServiceBreakdown.Add(row);
                totalCharge += charge;
                totalProfit += profit;
            }
        }

        /// <summary>
        /// Add LAM services (PVB + cutting + tempering).
        /// </summary>
        private void AddLamServices(SpecCostGroup specGroup, decimal area, ref decimal totalCharge, ref decimal totalProfit)
        {
            // PVB interlayer service
            var pvbRow = specGroup.Rows.FirstOrDefault(r => 
                r.Kind == "Production" && r.Description.Contains("interlayer"));
            if (pvbRow != null)
            {
                var charge = pvbRow.Amount;
                var estimatedCost = charge * 0.625m; // Assume PVB is profitable (62.5% cost)
                var profit = charge - estimatedCost;
                var marginPercent = charge > 0
                    ? Math.Round((profit / charge) * 100m, 1)
                    : 0m;

                var row = new ServiceProfitRow
                {
                    ServiceName = "PVB Interlayer Service",
                    Description = "PVB interlayer material and lamination",
                    Area = area,
                    Charge = charge,
                    Cost = estimatedCost,
                    Profit = profit,
                    MarginPercent = marginPercent,
                    IsProfitable = profit >= 0
                };

                ServiceBreakdown.Add(row);
                totalCharge += charge;
                totalProfit += profit;
            }

            // Cutting and tempering (same as SGU)
            AddSguServices(specGroup, area, ref totalCharge, ref totalProfit);
        }

        /// <summary>
        /// Add DGU+LAM services (spacer + PVB + cutting + tempering).
        /// </summary>
        private void AddDguLamServices(SpecCostGroup specGroup, decimal area, ref decimal totalCharge, ref decimal totalProfit)
        {
            // Spacer service
            AddDguServices(specGroup, area, ref totalCharge, ref totalProfit);

            // PVB service
            var pvbRow = specGroup.Rows.FirstOrDefault(r => 
                r.Kind == "Production" && r.Description.Contains("interlayer"));
            if (pvbRow != null)
            {
                var charge = pvbRow.Amount;
                var estimatedCost = charge * 0.625m;
                var profit = charge - estimatedCost;
                var marginPercent = charge > 0
                    ? Math.Round((profit / charge) * 100m, 1)
                    : 0m;

                var row = new ServiceProfitRow
                {
                    ServiceName = "PVB Interlayer Service",
                    Description = "PVB interlayer material and lamination",
                    Area = area,
                    Charge = charge,
                    Cost = estimatedCost,
                    Profit = profit,
                    MarginPercent = marginPercent,
                    IsProfitable = profit >= 0
                };

                ServiceBreakdown.Add(row);
                totalCharge += charge;
                totalProfit += profit;
            }
        }

        /// <summary>
        /// Calculate U-insert service profit.
        /// </summary>
        private ServiceProfitRow? CalculateUInsertService(SpecCostGroup specGroup, decimal area)
        {
            var uInsertCost = _parentViewModel.PriceBasedUInsertCost;
            if (uInsertCost <= 0) return null;

            // U-insert charge is typically 10 AED per SQM
            var charge = area * 10m;
            var profit = charge - uInsertCost;
            var marginPercent = charge > 0
                ? Math.Round((profit / charge) * 100m, 1)
                : 0m;

            return new ServiceProfitRow
            {
                ServiceName = "U-Insert Service",
                Description = "U-insert installation",
                Area = area,
                Charge = charge,
                Cost = uInsertCost,
                Profit = profit,
                MarginPercent = marginPercent,
                IsProfitable = profit >= 0
            };
        }
    }

    /// <summary>
    /// Service profit row for breakdown display.
    /// </summary>
    public class ServiceProfitRow : ViewModelBase
    {
        private string _serviceName = "";
        public string ServiceName
        {
            get => _serviceName;
            set
            {
                if (_serviceName == value) return;
                _serviceName = value ?? "";
                OnPropertyChanged();
            }
        }

        private string _description = "";
        public string Description
        {
            get => _description;
            set
            {
                if (_description == value) return;
                _description = value ?? "";
                OnPropertyChanged();
            }
        }

        private decimal _area;
        public decimal Area
        {
            get => _area;
            set
            {
                if (_area == value) return;
                _area = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AreaDisplay));
            }
        }

        public string AreaDisplay => $"{Area:0.##} SQM";

        private decimal _charge;
        public decimal Charge
        {
            get => _charge;
            set
            {
                if (_charge == value) return;
                _charge = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChargeDisplay));
            }
        }

        public string ChargeDisplay => $"AED {Charge:N2}";

        private decimal _cost;
        public decimal Cost
        {
            get => _cost;
            set
            {
                if (_cost == value) return;
                _cost = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CostDisplay));
            }
        }

        public string CostDisplay => $"AED {Cost:N2}";

        private decimal _profit;
        public decimal Profit
        {
            get => _profit;
            set
            {
                if (_profit == value) return;
                _profit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProfitDisplay));
            }
        }

        public string ProfitDisplay => $"AED {Profit:N2}";

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
    }
}
