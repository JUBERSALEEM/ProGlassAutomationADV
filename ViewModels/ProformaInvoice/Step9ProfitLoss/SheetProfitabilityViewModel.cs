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
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages;
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
            SelectRowCommand = new RelayCommand(_ => SetSelectedRow(_));
            BackCommand = new RelayCommand(_ => BackToSheetList());
            
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

        private decimal _wastageArea;
        /// <summary>Actual wastage area from Step 6 (SQM).</summary>
        public decimal WastageArea
        {
            get => _wastageArea;
            private set
            {
                if (_wastageArea == value) return;
                _wastageArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WastageAreaDisplay));
            }
        }

        public string WastageAreaDisplay => $"{WastageArea:0.##} SQM";

        private decimal _wastageCharge;
        /// <summary>Actual wastage charge from Step 6 (AED).</summary>
        public decimal WastageCharge
        {
            get => _wastageCharge;
            private set
            {
                if (_wastageCharge == value) return;
                _wastageCharge = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WastageChargeDisplay));
            }
        }

        public string WastageChargeDisplay => $"AED {WastageCharge:N2}";

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

        private decimal _wastageChargedToCustomer;
        /// <summary>Wastage amount charged to customer (AED).</summary>
        public decimal WastageChargedToCustomer
        {
            get => _wastageChargedToCustomer;
            private set
            {
                if (_wastageChargedToCustomer == value) return;
                _wastageChargedToCustomer = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WastageChargedToCustomerDisplay));
            }
        }

        public string WastageChargedToCustomerDisplay => $"AED {WastageChargedToCustomer:N2}";

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

        private decimal _totalUsedPrice;
        /// <summary>Total value of the glass actually used (used SQM × rate).</summary>
        public decimal TotalUsedPrice
        {
            get => _totalUsedPrice;
            private set
            {
                if (_totalUsedPrice == value) return;
                _totalUsedPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalUsedPriceDisplay));
            }
        }

        public string TotalUsedPriceDisplay => $"AED {TotalUsedPrice:N2}";

        private decimal _totalOffcutPrice;
        /// <summary>Total value of the offcut left over (offcut SQM × rate).</summary>
        public decimal TotalOffcutPrice
        {
            get => _totalOffcutPrice;
            private set
            {
                if (_totalOffcutPrice == value) return;
                _totalOffcutPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalOffcutPriceDisplay));
            }
        }

        public string TotalOffcutPriceDisplay => $"AED {TotalOffcutPrice:N2}";

        /// <summary>Share of the total sheet price that is actually used.</summary>
        public decimal UsedPriceSharePercent => TotalPurchaseCost > 0m
            ? Math.Round(TotalUsedPrice / TotalPurchaseCost * 100m, 1)
            : 0m;

        public string UsedPriceShareDisplay => $"{UsedPriceSharePercent:0.#}%";

        /// <summary>Share of the total sheet price lost to offcut.</summary>
        public decimal OffcutPriceSharePercent => TotalPurchaseCost > 0m
            ? Math.Round(TotalOffcutPrice / TotalPurchaseCost * 100m, 1)
            : 0m;

        public string OffcutPriceShareDisplay => $"{OffcutPriceSharePercent:0.#}%";

        /// <summary>Wastage charged to the customer minus the offcut value.</summary>
        public decimal NetWastageVsOffcut => WastageCharge - TotalOffcutPrice;

        public string NetWastageVsOffcutDisplay => $"AED {NetWastageVsOffcut:N2}";

        /// <summary>Plain reading of the wastage-vs-offcut comparison.</summary>
        public string NetWastageVsOffcutLabel => NetWastageVsOffcut >= 0m
            ? "recovered over the offcut value"
            : "short of the offcut value";

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

        /// <summary>Opens the full-screen detail for the clicked sheet row.</summary>
        public ICommand SelectRowCommand { get; }

        /// <summary>Returns from the detail screen back to the sheet list.</summary>
        public ICommand BackCommand { get; }

        private SheetProfitRow? _selectedRow;
        /// <summary>The sheet row whose full-screen detail is open (null = list).</summary>
        public SheetProfitRow? SelectedRow
        {
            get => _selectedRow;
            private set
            {
                if (ReferenceEquals(_selectedRow, value)) return;
                _selectedRow = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedRow));
            }
        }

        /// <summary>True while a sheet row's detail screen is open.</summary>
        public bool HasSelectedRow => SelectedRow != null;

        // ══════════════════════════════════════════════════════════════
        //  METHODS
        // ══════════════════════════════════════════════════════════════

        private void SetSelectedRow(object? parameter)
        {
            var row = parameter as SheetProfitRow;
            foreach (var r in SheetBreakdown)
                r.IsSelected = ReferenceEquals(r, row);

            SelectedRow = row;
        }

        private void BackToSheetList()
        {
            foreach (var r in SheetBreakdown)
                r.IsSelected = false;

            SelectedRow = null;
        }

        /// <summary>
        /// Refresh the sheet profitability analysis.
        /// Reads ACTUAL wastage charge data from Step 6 (not estimated "reusable offcuts").
        /// We charge wastage and throw away small offcuts, so this shows the real wastage cost impact.
        /// </summary>
        public void RefreshAnalysis()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                SheetBreakdown.Clear();
                SelectedRow = null;

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
                decimal totalUsedPrice = 0m;
                decimal totalOffcutPrice = 0m;
                decimal totalWastageChargeAmount = 0m;
                decimal totalWastageArea = 0m;

                // Analyze each specification group
                foreach (var specGroup in groups)
                {
                    var specName = specGroup.SpecName ?? "";
                    var area = specGroup.CutSqm;
                    if (area <= 0) continue;

                    // Build family of this specification ("SGU"/"DGU"/"LAM"/
                    // "DGU+LAM") — every sheet row of the spec shares it.
                    var glassFamilyLabel = FamilyLabelFor(specName);

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
                        var usedAreaForThisRow = area; // this pane covers the whole cut area

                        // Get price per SQM and the three sheet-price figures
                        var pricePerSqm = glassRow.PricePerSqm;
                        var purchaseCostForThisRow = totalSheetAreaForThisRow * pricePerSqm; // TOTAL sheet price
                        var usedPriceForThisRow = usedAreaForThisRow * pricePerSqm;           // USED sheet price
                        var offcutPriceForThisRow = (totalSheetAreaForThisRow - usedAreaForThisRow) * pricePerSqm; // OFFCUT value

                        // Calculate wastage from ACTUAL Step 6 wastage charges for this pane
                        var wastageAreaForThisRow = 0m;
                        var wastageChargeForThisRow = 0m;
                        
                        // Find matching wastage rows from Step 6
                        // Match by spec number and side (Outer/Inner)
                        var paneSide = glassRow.Description?.Contains("Inner") == true ? "Inner" : "Outer";
                        var matchingWastageRows = GetWastageRowsForPane(specGroup.SpecNumber, paneSide);
                        
                        foreach (var wastageRow in matchingWastageRows)
                        {
                            if (wastageRow.IsActive)
                            {
                                wastageAreaForThisRow += wastageRow.TotalSqm;
                                wastageChargeForThisRow += wastageRow.ResultAmount;
                            }
                        }

                        // Calculate efficiency
                        var efficiencyPercent = totalSheetAreaForThisRow > 0
                            ? Math.Round((usedAreaForThisRow / totalSheetAreaForThisRow) * 100m, 1)
                            : 0m;

                        // Add row to breakdown
                        var row = new SheetProfitRow
                        {
                            SpecName = specName,
                            GlassTypeLabel = glassFamilyLabel,
                            SheetSize = $"{sheetWidth} × {sheetHeight} mm",
                            SheetSqm = sheetSqm,
                            SheetsPurchased = sheetsPurchased,
                            TotalSheetArea = totalSheetAreaForThisRow,
                            UsedArea = usedAreaForThisRow,
                            OffcutArea = totalSheetAreaForThisRow - usedAreaForThisRow,
                            WastageArea = wastageAreaForThisRow, // ACTUAL wastage area from Step 6
                            WasteOffcutArea = (totalSheetAreaForThisRow - usedAreaForThisRow) - wastageAreaForThisRow,
                            ReusablePercent = totalSheetAreaForThisRow > 0 
                                ? Math.Round((wastageAreaForThisRow / totalSheetAreaForThisRow) * 100m, 1) 
                                : 0m,
                            PricePerSqm = pricePerSqm,
                            PurchaseCost = purchaseCostForThisRow,      // TOTAL sheet price
                            UsedPrice = usedPriceForThisRow,            // USED sheet price
                            OffcutPrice = offcutPriceForThisRow,        // OFFCUT value
                            WastageCharge = wastageChargeForThisRow,    // WASTAGE sheet price (Step 6)
                            RealCost = purchaseCostForThisRow, // Real cost is purchase cost (wastage is charged to customer)
                            UsageEfficiencyPercent = efficiencyPercent,
                            IsEfficient = efficiencyPercent >= 70m
                        };

                        SheetBreakdown.Add(row);

                        // Accumulate totals
                        totalSheets += sheetsPurchased;
                        totalSheetArea += totalSheetAreaForThisRow;
                        totalUsedArea += usedAreaForThisRow;
                        totalPurchaseCost += purchaseCostForThisRow;
                        totalUsedPrice += usedPriceForThisRow;
                        totalOffcutPrice += offcutPriceForThisRow;
                        totalWastageChargeAmount += wastageChargeForThisRow;
                        totalWastageArea += wastageAreaForThisRow;
                    }
                }

                // Set totals
                TotalSheetsPurchased = totalSheets;
                TotalSheetArea = totalSheetArea;
                TotalUsedArea = totalUsedArea;
                TotalOffcutArea = totalSheetArea - totalUsedArea;
                WastageArea = totalWastageArea; // Actual wastage area from Step 6
                WastageCharge = totalWastageChargeAmount; // Wastage sheet price from Step 6
                TotalPurchaseCost = totalPurchaseCost;     // Total sheet price
                TotalUsedPrice = totalUsedPrice;           // Used sheet price
                TotalOffcutPrice = totalOffcutPrice;       // Offcut value
                RealCost = totalPurchaseCost; // Real cost is purchase cost
                WastageChargedToCustomer = totalWastageChargeAmount; // Wastage charged to customer
                UsageEfficiencyPercent = totalSheetArea > 0
                    ? Math.Round((totalUsedArea / totalSheetArea) * 100m, 1)
                    : 0m;

                // Re-raise the computed price-comparison figures (they derive
                // from the totals set just above).
                OnPropertyChanged(nameof(UsedPriceSharePercent));
                OnPropertyChanged(nameof(UsedPriceShareDisplay));
                OnPropertyChanged(nameof(OffcutPriceSharePercent));
                OnPropertyChanged(nameof(OffcutPriceShareDisplay));
                OnPropertyChanged(nameof(NetWastageVsOffcut));
                OnPropertyChanged(nameof(NetWastageVsOffcutDisplay));
                OnPropertyChanged(nameof(NetWastageVsOffcutLabel));

                StatusMessage = $"Analysis complete. {SheetBreakdown.Count} sheet type(s) analyzed. " +
                               $"Usage efficiency: {UsageEfficiencyPercent:0.#}%. " +
                               $"Wastage charged to customer: AED {WastageChargedToCustomer:N2}.";
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

        /// <summary>
        /// Get wastage rows from Step 6 that match a specific pane (spec number + side).
        /// </summary>
        private List<WastageRow> GetWastageRowsForPane(int specNumber, string paneSide)
        {
            var result = new List<WastageRow>();
            
            var wastagesSection = _parentViewModel._otherChargesVm?.WastagesSection;
            if (wastagesSection?.WastageRows == null) return result;

            foreach (var wastageRow in wastagesSection.WastageRows)
            {
                // Check if this wastage row targets the specified pane
                foreach (var source in wastageRow.AvailableSources)
                {
                    if (source.IsChecked && 
                        source.SpecNumber == specNumber &&
                        source.SideLabel.Contains(paneSide, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(wastageRow);
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Build family shown in the GLASS TYPE column: "SGU", "DGU",
        /// "LAM" or "DGU+LAM". Spacer/interlayer evidence in the spec name
        /// drives DGU / LAM / DGU+LAM; anything without a marker (or a named
        /// spec the wizard cannot parse) defaults to SGU, exactly like the
        /// Glass Type Comparison analysis. Only the build family is shown —
        /// never the outer/inner pane identity.
        /// </summary>
        private static string FamilyLabelFor(string specName)
        {
            specName ??= "";
            bool dgu = PnLSpecParser.Spacer(specName).Success
                       || specName.IndexOf("DGU", StringComparison.OrdinalIgnoreCase) >= 0;
            bool lam = PnLSpecParser.Interlayer(specName).Success
                       || specName.IndexOf("LAM", StringComparison.OrdinalIgnoreCase) >= 0;

            if (dgu && lam) return "DGU+LAM";
            if (dgu) return "DGU";
            if (lam) return "LAM";
            return "SGU";
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

        /// <summary>
        /// Build family of the specification this sheet belongs to:
        /// "SGU", "DGU", "LAM" or "DGU+LAM". Kept as its own column so the
        /// first thing a reader sees is what the unit is. The outer/inner
        /// pane identity is intentionally NOT shown here.
        /// </summary>
        private string _glassTypeLabel = "";
        public string GlassTypeLabel
        {
            get => _glassTypeLabel;
            set
            {
                if (_glassTypeLabel == value) return;
                _glassTypeLabel = value ?? "";
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

        private decimal _wastageArea;
        public decimal WastageArea
        {
            get => _wastageArea;
            set
            {
                if (_wastageArea == value) return;
                _wastageArea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WastageAreaDisplay));
            }
        }

        public string WastageAreaDisplay => $"{WastageArea:0.##} SQM";

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

        private decimal _usedPrice;
        /// <summary>Used sheet price: used SQM × rate.</summary>
        public decimal UsedPrice
        {
            get => _usedPrice;
            set
            {
                if (_usedPrice == value) return;
                _usedPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UsedPriceDisplay));
            }
        }

        public string UsedPriceDisplay => $"AED {UsedPrice:N2}";

        private decimal _offcutPrice;
        /// <summary>Offcut value: offcut SQM × rate.</summary>
        public decimal OffcutPrice
        {
            get => _offcutPrice;
            set
            {
                if (_offcutPrice == value) return;
                _offcutPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OffcutPriceDisplay));
            }
        }

        public string OffcutPriceDisplay => $"AED {OffcutPrice:N2}";

        /// <summary>Share of this sheet's total price that is actually used.</summary>
        public decimal UsedPriceSharePercent => PurchaseCost > 0m
            ? Math.Round(UsedPrice / PurchaseCost * 100m, 1)
            : 0m;

        public string UsedPriceShareDisplay => $"{UsedPriceSharePercent:0.#}%";

        /// <summary>Share of this sheet's total price lost to offcut.</summary>
        public decimal OffcutPriceSharePercent => PurchaseCost > 0m
            ? Math.Round(OffcutPrice / PurchaseCost * 100m, 1)
            : 0m;

        public string OffcutPriceShareDisplay => $"{OffcutPriceSharePercent:0.#}%";

        private decimal _wastageCharge;
        public decimal WastageCharge
        {
            get => _wastageCharge;
            set
            {
                if (_wastageCharge == value) return;
                _wastageCharge = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WastageChargeDisplay));
            }
        }

        public string WastageChargeDisplay => $"AED {WastageCharge:N2}";

        /// <summary>Wastage charged to the customer minus this sheet's offcut value.</summary>
        public decimal NetWastageVsOffcut => WastageCharge - OffcutPrice;

        public string NetWastageVsOffcutDisplay => $"AED {NetWastageVsOffcut:N2}";

        /// <summary>Plain reading of the wastage-vs-offcut comparison for this row.</summary>
        public string NetWastageVsOffcutLabel => NetWastageVsOffcut >= 0m
            ? "recovered over the offcut value"
            : "short of the offcut value";

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

        private bool _isSelected;
        /// <summary>True while this row's full-screen detail is open.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }
}
