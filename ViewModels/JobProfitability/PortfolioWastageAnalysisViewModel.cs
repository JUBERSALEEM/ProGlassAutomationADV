// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/JobProfitability/PortfolioWastageAnalysisViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Portfolio Wastage Analysis (Aggregated Across All PIs)
//
// WHAT IT IS
//   Aggregates wastage charge data across ALL confirmed PIs.
//   Shows total wastage charges, areas, and impact on profitability.
//
// DATA SOURCE
//   Reads Step 6 wastage charge data from all "Approved" (confirmed) invoices.
//   Aggregates wastage data for portfolio-wide analysis.
//
// USE CASE
//   Answer: "How much wastage are we charging across our entire business?"
//   Helps identify wastage patterns and optimize sheet usage.
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
    /// Portfolio-level wastage analysis.
    /// Aggregates data from all confirmed PIs.
    /// </summary>
    public class PortfolioWastageAnalysisViewModel : ViewModelBase
    {
        private readonly IProformaInvoiceRepository _invoices;

        public PortfolioWastageAnalysisViewModel(IProformaInvoiceRepository invoices)
        {
            _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
            
            WastageBreakdown = new ObservableCollection<PortfolioWastageRow>();
            RefreshCommand = new RelayCommand(_ => AsyncUtility.SafeFireAndForget(RefreshAnalysisAsync, "PortfolioWastage Refresh"));
        }

        // ══════════════════════════════════════════════════════════════
        //  PROPERTIES
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<PortfolioWastageRow> WastageBreakdown { get; }

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

        private decimal _totalWastageCharge;
        public decimal TotalWastageCharge
        {
            get => _totalWastageCharge;
            private set
            {
                if (_totalWastageCharge == value) return;
                _totalWastageCharge = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalWastageChargeDisplay));
            }
        }
        public string TotalWastageChargeDisplay => $"AED {TotalWastageCharge:N0}";

        private decimal _totalWastageArea;
        public decimal TotalWastageArea
        {
            get => _totalWastageArea;
            private set
            {
                if (_totalWastageArea == value) return;
                _totalWastageArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalWastageAreaDisplay));
            }
        }
        public string TotalWastageAreaDisplay => $"{TotalWastageArea:0.##} SQM";

        private decimal _totalSheetArea;
        public decimal TotalSheetArea
        {
            get => _totalSheetArea;
            private set
            {
                if (_totalSheetArea == value) return;
                _totalSheetArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalSheetAreaDisplay));
            }
        }
        public string TotalSheetAreaDisplay => $"{TotalSheetArea:0.##} SQM";

        private decimal _overallWastagePercent;
        public decimal OverallWastagePercent
        {
            get => _overallWastagePercent;
            private set
            {
                if (_overallWastagePercent == value) return;
                _overallWastagePercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OverallWastageDisplay));
            }
        }
        public string OverallWastageDisplay => $"{OverallWastagePercent:0.#}%";

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

        private string _statusMessage = "Press REFRESH to analyze portfolio wastage.";
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
                WastageBreakdown.Clear();

                // Load all confirmed PIs
                var invoices = await _invoices.GetByStatusAsync("Approved").ConfigureAwait(true);
                if (invoices == null || invoices.Count == 0)
                {
                    StatusMessage = "No confirmed invoices found.";
                    return;
                }

                TotalJobsAnalyzed = invoices.Count;

                // Aggregate wastage data across all PIs
                var wastageData = new Dictionary<string, (decimal charge, decimal area, int jobCount)>();
                decimal totalSheetArea = 0m;

                foreach (var inv in invoices)
                {
                    var snap = ProformaInvoiceMapper.DeserializeSnapshot(inv);
                    if (snap == null) continue;

                    // Get Step 5 spec groups to calculate total sheet area
                    foreach (var spec in snap.SpecGroups)
                    {
                        foreach (var item in spec.ItemRows)
                        {
                            var area = item.TotalSqm;
                            totalSheetArea += area;
                        }
                    }

                    // Get Step 6 wastage charges
                    var wastages = snap.OtherCharges?.Wastages;
                    if (wastages == null || wastages.Count == 0) continue;

                    foreach (var wastage in wastages)
                    {
                        if (!wastage.IsActive) continue;

                        var colorName = wastage.ColorName ?? "Unknown";
                        var charge = CalculateWastageCharge(wastage);
                        var area = EstimateWastageArea(wastage);

                        if (!wastageData.ContainsKey(colorName))
                            wastageData[colorName] = (0m, 0m, 0);

                        var (existingCharge, existingArea, existingCount) = wastageData[colorName];
                        wastageData[colorName] = (
                            existingCharge + charge,
                            existingArea + area,
                            existingCount + 1
                        );
                    }
                }

                // Build wastage breakdown rows
                decimal totalCharge = 0m;
                decimal totalArea = 0m;

                foreach (var kvp in wastageData.OrderByDescending(x => x.Value.charge))
                {
                    var colorName = kvp.Key;
                    var (charge, area, jobCount) = kvp.Value;

                    var row = new PortfolioWastageRow
                    {
                        ColorName = colorName,
                        JobCount = jobCount,
                        TotalCharge = charge,
                        TotalArea = area,
                        WastagePercent = totalSheetArea > 0 
                            ? Math.Round((area / totalSheetArea) * 100m, 1) 
                            : 0m,
                        IsHighWastage = (area / totalSheetArea) > 0.15m // >15% is high
                    };

                    WastageBreakdown.Add(row);
                    totalCharge += charge;
                    totalArea += area;
                }

                TotalWastageCharge = totalCharge;
                TotalWastageArea = totalArea;
                TotalSheetArea = totalSheetArea;
                OverallWastagePercent = totalSheetArea > 0
                    ? Math.Round((totalArea / totalSheetArea) * 100m, 1)
                    : 0m;

                Recommendation = GenerateRecommendation();

                StatusMessage = $"Analysis complete. {TotalJobsAnalyzed} job(s) analyzed, {WastageBreakdown.Count} color(s) with wastage charges.";
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[PortfolioWastageAnalysisViewModel] Refresh failed: {ExceptionMessages.Deep(ex)}");
                StatusMessage = "Analysis failed. Please check the data and try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private decimal CalculateWastageCharge(Models.ProformaInvoice.SavedInvoice.WastageRowSnapshot wastage)
        {
            if (wastage.UseOptimizationFormula)
            {
                // Optimization formula: (FormulaPrice / OptimizationPercent) × Area × SheetQty
                if (wastage.FormulaPricePerSqm > 0 && wastage.OptimizationPercent > 0)
                {
                    var rate = wastage.FormulaPricePerSqm / (wastage.OptimizationPercent / 100m);
                    // Estimate area (we don't have exact area in snapshot, use rate as proxy)
                    return rate * wastage.SheetQuantity;
                }
                return 0m;
            }
            else
            {
                // Manual rate: Rate × Area × SheetQty
                // We don't have exact area in snapshot, estimate from rate
                return wastage.Rate * wastage.SheetQuantity;
            }
        }

        private decimal EstimateWastageArea(Models.ProformaInvoice.SavedInvoice.WastageRowSnapshot wastage)
        {
            // Estimate wastage area from sheet quantity
            // Assume average sheet is ~7 SQM (3210 × 2250 mm)
            return wastage.SheetQuantity * 7m;
        }

        private string GenerateRecommendation()
        {
            if (OverallWastagePercent > 20m)
            {
                return $"Overall wastage is {OverallWastagePercent:0.#}% — consider optimizing cutting patterns and sheet usage.";
            }
            else if (OverallWastagePercent > 15m)
            {
                return $"Overall wastage is {OverallWastagePercent:0.#}% — acceptable but could be improved.";
            }
            else
            {
                return $"Overall wastage is {OverallWastagePercent:0.#}% — good efficiency.";
            }
        }
    }

    /// <summary>
    /// Portfolio wastage row.
    /// </summary>
    public class PortfolioWastageRow : ViewModelBase
    {
        private string _colorName = "";
        public string ColorName
        {
            get => _colorName;
            set { if (_colorName == value) return; _colorName = value ?? ""; OnPropertyChanged(); }
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

        private decimal _wastagePercent;
        public decimal WastagePercent
        {
            get => _wastagePercent;
            set
            {
                if (_wastagePercent == value) return;
                _wastagePercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WastagePercentDisplay));
            }
        }
        public string WastagePercentDisplay => $"{WastagePercent:0.#}%";

        private bool _isHighWastage;
        public bool IsHighWastage
        {
            get => _isHighWastage;
            set { if (_isHighWastage == value) return; _isHighWastage = value; OnPropertyChanged(); }
        }
    }
}
