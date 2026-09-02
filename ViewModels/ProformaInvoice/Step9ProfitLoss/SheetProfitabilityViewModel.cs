// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Sheet Profitability Analysis
//
// WHAT IT IS
//   Separate view for sheet usage and offcut tracking.
//   Shows:
//   - Sheets purchased vs sheets used
//   - Offcut analysis (reusable vs waste)
//   - Real cost based on actual usage
//   - Profit impact of offcut reusability
//
// WHY SEPARATE
//   The main P&L uses purchase costs. This view shows the REAL cost
//   based on what was actually used, accounting for reusable offcuts.
//
// FORMULAS
//   Purchase Cost = Sheets Purchased × Sheet Price
//   Used Area = Total Area Required (with wastage)
//   Offcut Area = (Sheets Purchased × Sheet SQM) - Used Area
//   Reusable Offcut Value = Reusable Offcut Area × Sheet Price per SQM
//   Real Cost = Purchase Cost - Reusable Offcut Value
//   Usage Efficiency = (Used Area / Total Sheet Area) × 100
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
    /// Sheet profitability breakdown for detailed analysis.
    /// </summary>
    public class SheetProfitabilityViewModel : ViewModelBase
    {
        private readonly ProfitLossStepViewModel _parentViewModel;

        public SheetProfitabilityViewModel(ProfitLossStepViewModel parentViewModel)
        {
            _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
            
            SheetBreakdown = new ObservableCollection<SheetProfitRow>();
            RefreshCommand = new RelayCommand(_ => RefreshAnalysis());
            
            // Initial load
            RefreshAnalysis();
        }

        // ══════════════════════════════════════════════════════════════
        //  PROPERTIES
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Sheet breakdown rows.
        /// </summary>
        public ObservableCollection<SheetProfitRow> SheetBreakdown { get; }

        private decimal _totalSheetsPurchased;
        /// <summary>Total sheets purchased.</summary>
        public decimal TotalSheetsPurchased
        {
            get => _totalSheetsPurchased;
            private set
            {
                if (_totalSheetsPurchased == value) return;
                _totalSheetsPurchased = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalSheetsPurchasedDisplay));
            }
        }

        public string TotalSheetsPurchasedDisplay => $"{TotalSheetsPurchased:0}";

        private decimal _totalSheetArea;
        /// <summary>Total sheet area purchased (SQM).</summary>
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

        private decimal _totalUsedArea;
        /// <summary>Total area used (SQM).</summary>
        public decimal TotalUsedArea
        {
            get => _totalUsedArea;
            private set
            {
                if (_totalUsedArea == value) return;
                _totalUsedArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalUsedAreaDisplay));
            }
        }

        public string TotalUsedAreaDisplay => $"{TotalUsedArea:0.##} SQM";

        private decimal _totalOffcutArea;
        /// <summary>Total offcut area (SQM).</summary>
        public decimal TotalOffcutArea
        {
            get => _totalOffcutArea;
            private set
            {
                if (_totalOffcutArea == value) return;
                _totalOffcutArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalOffcutAreaDisplay));
            }
        }

        public string TotalOffcutAreaDisplay => $"{TotalOffcutArea:0.##} SQM";

        private decimal _reusableOffcutArea;
        /// <summary>Reusable offcut area (SQM).</summary>
        public decimal ReusableOffcutArea
        {
            get => _reusableOffcutArea;
            private set
            {
                if (_reusableOffcutArea == value) return;
                _reusableOffcutArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReusableOffcutAreaDisplay));
            }
        }

        public string ReusableOffcutAreaDisplay => $"{ReusableOffcutArea:0.##} SQM";

        private decimal _reusableOffcutValue;
        /// <summary>Value of reusable offcuts (AED).</summary>
        public decimal ReusableOffcutValue
        {
            get => _reusableOffcutValue;
            private set
            {
                if (_reusableOffcutValue == value) return;
                _reusableOffcutValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReusableOffcutValueDisplay));
            }
        }

        public string ReusableOffcutValueDisplay => $"AED {ReusableOffcutValue:N2}";

        private decimal _totalPurchaseCost;
        /// <summary>Total purchase cost (AED).</summary>
        public decimal TotalPurchaseCost
        {
            get => _totalPurchaseCost;
            private set
            {
                if (_totalPurchaseCost == value) return;
                _totalPurchaseCost = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPurchaseCostDisplay));
            }
        }

        public string TotalPurchaseCostDisplay => $"AED {TotalPurchaseCost:N2}";

        private decimal _realCost;
        /// <summary>Real cost after accounting for reusable offcuts (AED).</summary>
        public decimal RealCost
        {
            get => _realCost;
            private set
            {
                if (_realCost == value) return;
                _realCost = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RealCostDisplay));
            }
        }

        public string RealCostDisplay => $"AED {RealCost:N2}";

        private decimal _costSavings;
        /// <summary>Cost savings from reusable offcuts (AED).</summary>
        public decimal CostSavings
        {
            get => _costSavings;
            private set
            {
                if (_costSavings == value) return;
                _costSavings = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CostSavingsDisplay));
            }
        }

        public string CostSavingsDisplay => $"AED {CostSavings:N2}";

        private decimal _usageEfficiencyPercent;
        /// <summary>Sheet usage efficiency percentage.</summary>
        public decimal UsageEfficiencyPercent
        {
            get => _usageEfficiencyPercent;
            private set
            {
                if (_usageEfficiencyPercent == value) return;
                _usageEfficiencyPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UsageEfficiencyDisplay));
            }
        }

        public string UsageEfficiencyDisplay => $"{UsageEfficiencyPercent:0.#}%";

        private string _statusMessage = "Press REFRESH to analyze sheet profitability.";
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
        /// Refresh the sheet profitability analysis.
        /// </summary>
        public void RefreshAnalysis()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                SheetBreakdown.Clear();

                var groups = _parentViewModel.SpecGroupsView;
                if (groups == null || groups.Count == 0)
                {
                    StatusMessage = "No specifications found. Please add specifications in Step 5.";
                    return;
                }

                decimal totalSheets = 0m;
                decimal totalSheetArea = 0m;
                decimal totalUsedArea = 0m;
                decimal totalPurchaseCost = 0m;
                decimal totalReusableOffcutArea = 0m;
                decimal totalReusableOffcutValue = 0m;

                // Analyze each specification group
                foreach (var specGroup in groups)
                {
                    var specName = specGroup.SpecName ?? "";
                    var area = specGroup.CutSqm;
                    if (area <= 0) continue;

                    // Get glass rows from this spec
                    var glassRows = specGroup.Rows.Where(r => r.Kind == "Glass").ToList();
                    
                    foreach (var glassRow in glassRows)
                    {
                        // Get sheet dimensions
                        var sheetWidth = glassRow.SheetWidthMm;
                        var sheetHeight = glassRow.SheetHeightMm;
                        var sheetSqm = (sheetWidth * sheetHeight) / 1_000_000m;
                        if (sheetSqm <= 0) continue;

                        // Get quantity (sheets purchased)
                        var sheetsPurchased = glassRow.Quantity;
                        if (sheetsPurchased <= 0) continue;

                        // Calculate areas
                        var totalSheetAreaForThisRow = sheetsPurchased * sheetSqm;
                        var usedAreaForThisRow = area; // Assume all area is used for this pane
                        var offcutAreaForThisRow = totalSheetAreaForThisRow - usedAreaForThisRow;
                        
                        // Estimate reusable offcuts (assume 80% of offcuts are reusable)
                        var reusablePercent = 0.80m;
                        var reusableOffcutAreaForThisRow = offcutAreaForThisRow * reusablePercent;
                        var wasteOffcutAreaForThisRow = offcutAreaForThisRow - reusableOffcutAreaForThisRow;

                        // Get price per SQM
                        var pricePerSqm = glassRow.PricePerSqm;
                        var purchaseCostForThisRow = totalSheetAreaForThisRow * pricePerSqm;
                        var reusableOffcutValueForThisRow = reusableOffcutAreaForThisRow * pricePerSqm;

                        // Calculate efficiency
                        var efficiencyPercent = totalSheetAreaForThisRow > 0
                            ? Math.Round((usedAreaForThisRow / totalSheetAreaForThisRow) * 100m, 1)
                            : 0m;

                        // Add row to breakdown
                        var row = new SheetProfitRow
                        {
                            SpecName = specName,
                            GlassType = glassRow.Description ?? "Glass",
                            SheetSize = $"{sheetWidth} × {sheetHeight} mm",
                            SheetSqm = sheetSqm,
                            SheetsPurchased = sheetsPurchased,
                            TotalSheetArea = totalSheetAreaForThisRow,
                            UsedArea = usedAreaForThisRow,
                            OffcutArea = offcutAreaForThisRow,
                            ReusableOffcutArea = reusableOffcutAreaForThisRow,
                            WasteOffcutArea = wasteOffcutAreaForThisRow,
                            ReusablePercent = reusablePercent * 100m,
                            PricePerSqm = pricePerSqm,
                            PurchaseCost = purchaseCostForThisRow,
                            ReusableOffcutValue = reusableOffcutValueForThisRow,
                            RealCost = purchaseCostForThisRow - reusableOffcutValueForThisRow,
                            UsageEfficiencyPercent = efficiencyPercent,
                            IsEfficient = efficiencyPercent >= 70m
                        };

                        SheetBreakdown.Add(row);

                        // Accumulate totals
                        totalSheets += sheetsPurchased;
                        totalSheetArea += totalSheetAreaForThisRow;
                        totalUsedArea += usedAreaForThisRow;
                        totalPurchaseCost += purchaseCostForThisRow;
                        totalReusableOffcutArea += reusableOffcutAreaForThisRow;
                        totalReusableOffcutValue += reusableOffcutValueForThisRow;
                    }
                }

                // Set totals
                TotalSheetsPurchased = totalSheets;
                TotalSheetArea = totalSheetArea;
                TotalUsedArea = totalUsedArea;
                TotalOffcutArea = totalSheetArea - totalUsedArea;
                ReusableOffcutArea = totalReusableOffcutArea;
                ReusableOffcutValue = totalReusableOffcutValue;
                TotalPurchaseCost = totalPurchaseCost;
                RealCost = totalPurchaseCost - totalReusableOffcutValue;
                CostSavings = totalReusableOffcutValue;
                UsageEfficiencyPercent = totalSheetArea > 0
                    ? Math.Round((totalUsedArea / totalSheetArea) * 100m, 1)
                    : 0m;

                StatusMessage = $"Analysis complete. {SheetBreakdown.Count} sheet type(s) analyzed. " +
                               $"Usage efficiency: {UsageEfficiencyPercent:0.#}%. " +
                               $"Cost savings from reusable offcuts: AED {CostSavings:N2}.";
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[SheetProfitabilityViewModel] Refresh failed: {ExceptionMessages.Deep(ex)}");
                StatusMessage = "Analysis failed. Please check the data and try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>
    /// Sheet profit row for breakdown display.
    /// </summary>
    public class SheetProfitRow : ViewModelBase
    {
        private string _specName = "";
        public string SpecName
        {
            get => _specName;
            set
            {
                if (_specName == value) return;
                _specName = value ?? "";
                OnPropertyChanged();
            }
        }

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

        private string _sheetSize = "";
        public string SheetSize
        {
            get => _sheetSize;
            set
            {
                if (_sheetSize == value) return;
                _sheetSize = value ?? "";
                OnPropertyChanged();
            }
        }

        private decimal _sheetSqm;
        public decimal SheetSqm
        {
            get => _sheetSqm;
            set
            {
                if (_sheetSqm == value) return;
                _sheetSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SheetSqmDisplay));
            }
        }

        public string SheetSqmDisplay => $"{SheetSqm:0.##} SQM";

        private decimal _sheetsPurchased;
        public decimal SheetsPurchased
        {
            get => _sheetsPurchased;
            set
            {
                if (_sheetsPurchased == value) return;
                _sheetsPurchased = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SheetsPurchasedDisplay));
            }
        }

        public string SheetsPurchasedDisplay => $"{SheetsPurchased:0}";

        private decimal _totalSheetArea;
        public decimal TotalSheetArea
        {
            get => _totalSheetArea;
            set
            {
                if (_totalSheetArea == value) return;
                _totalSheetArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalSheetAreaDisplay));
            }
        }

        public string TotalSheetAreaDisplay => $"{TotalSheetArea:0.##} SQM";

        private decimal _usedArea;
        public decimal UsedArea
        {
            get => _usedArea;
            set
            {
                if (_usedArea == value) return;
                _usedArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UsedAreaDisplay));
            }
        }

        public string UsedAreaDisplay => $"{UsedArea:0.##} SQM";

        private decimal _offcutArea;
        public decimal OffcutArea
        {
            get => _offcutArea;
            set
            {
                if (_offcutArea == value) return;
                _offcutArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OffcutAreaDisplay));
            }
        }

        public string OffcutAreaDisplay => $"{OffcutArea:0.##} SQM";

        private decimal _reusableOffcutArea;
        public decimal ReusableOffcutArea
        {
            get => _reusableOffcutArea;
            set
            {
                if (_reusableOffcutArea == value) return;
                _reusableOffcutArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReusableOffcutAreaDisplay));
            }
        }

        public string ReusableOffcutAreaDisplay => $"{ReusableOffcutArea:0.##} SQM";

        private decimal _wasteOffcutArea;
        public decimal WasteOffcutArea
        {
            get => _wasteOffcutArea;
            set
            {
                if (_wasteOffcutArea == value) return;
                _wasteOffcutArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WasteOffcutAreaDisplay));
            }
        }

        public string WasteOffcutAreaDisplay => $"{WasteOffcutArea:0.##} SQM";

        private decimal _reusablePercent;
        public decimal ReusablePercent
        {
            get => _reusablePercent;
            set
            {
                if (_reusablePercent == value) return;
                _reusablePercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReusablePercentDisplay));
            }
        }

        public string ReusablePercentDisplay => $"{ReusablePercent:0.#}%";

        private decimal _pricePerSqm;
        public decimal PricePerSqm
        {
            get => _pricePerSqm;
            set
            {
                if (_pricePerSqm == value) return;
                _pricePerSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PricePerSqmDisplay));
            }
        }

        public string PricePerSqmDisplay => $"AED {PricePerSqm:N2}/SQM";

        private decimal _purchaseCost;
        public decimal PurchaseCost
        {
            get => _purchaseCost;
            set
            {
                if (_purchaseCost == value) return;
                _purchaseCost = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PurchaseCostDisplay));
            }
        }

        public string PurchaseCostDisplay => $"AED {PurchaseCost:N2}";

        private decimal _reusableOffcutValue;
        public decimal ReusableOffcutValue
        {
            get => _reusableOffcutValue;
            set
            {
                if (_reusableOffcutValue == value) return;
                _reusableOffcutValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReusableOffcutValueDisplay));
            }
        }

        public string ReusableOffcutValueDisplay => $"AED {ReusableOffcutValue:N2}";

        private decimal _realCost;
        public decimal RealCost
        {
            get => _realCost;
            set
            {
                if (_realCost == value) return;
                _realCost = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RealCostDisplay));
            }
        }

        public string RealCostDisplay => $"AED {RealCost:N2}";

        private decimal _usageEfficiencyPercent;
        public decimal UsageEfficiencyPercent
        {
            get => _usageEfficiencyPercent;
            set
            {
                if (_usageEfficiencyPercent == value) return;
                _usageEfficiencyPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UsageEfficiencyDisplay));
            }
        }

        public string UsageEfficiencyDisplay => $"{UsageEfficiencyPercent:0.#}%";

        private bool _isEfficient = true;
        public bool IsEfficient
        {
            get => _isEfficient;
            set
            {
                if (_isEfficient == value) return;
                _isEfficient = value;
                OnPropertyChanged();
            }
        }
    }
}
