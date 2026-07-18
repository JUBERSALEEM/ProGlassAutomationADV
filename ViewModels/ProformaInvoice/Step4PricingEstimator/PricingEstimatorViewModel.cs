using ProGlassAutomation.Commands;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator
{
    /// <summary>
    /// Represents a glass structure type option (SGU / DGU / LAM / DGU+LAM).
    /// </summary>
    public class GlassTypeCard : ViewModelBase
    {
        public string Code { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string AccentColor { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
    }

    /// <summary>
    /// Represents a specification target group (pulled from Step 5 data grid).
    /// </summary>
    public class SpecTargetGroup : ViewModelBase
    {
        public string Name { get; set; }
        public string Description { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
    }

    /// <summary>
    /// ViewModel for Step 4 of the Proforma Invoice wizard.
    /// Handles the Smart Pricing Estimator: glass type selection,
    /// per-glass-type parameter panels, processing treatments,
    /// spec target groups, and live rate calculation.
    /// </summary>
    public class PricingEstimatorViewModel : ViewModelBase
    {
        // === Panel ViewModels (one per glass type) ===
        public SguParameters.SguParametersViewModel SguVM { get; }

        // === Glass Type Cards ===
        public ObservableCollection<GlassTypeCard> GlassTypes { get; }

        private GlassTypeCard _selectedGlassType;
        public GlassTypeCard SelectedGlassType
        {
            get => _selectedGlassType;
            set
            {
                if (_selectedGlassType == value) return;

                if (_selectedGlassType != null)
                    _selectedGlassType.IsSelected = false;

                _selectedGlassType = value;

                if (_selectedGlassType != null)
                    _selectedGlassType.IsSelected = true;

                OnPropertyChanged();

                // Recalculate rate when glass type changes
                RecalculateEstimatedRate();
            }
        }

        // === Processing Treatments (shared) ===

        private bool _polishedEdges;
        public bool PolishedEdges
        {
            get => _polishedEdges;
            set { _polishedEdges = value; OnPropertyChanged(); }
        }

        private bool _drillCutouts;
        public bool DrillCutouts
        {
            get => _drillCutouts;
            set { _drillCutouts = value; OnPropertyChanged(); }
        }

        private bool _countersunkHoles;
        public bool CountersunkHoles
        {
            get => _countersunkHoles;
            set { _countersunkHoles = value; OnPropertyChanged(); }
        }

        private bool _edgeDeletionLayer;
        public bool EdgeDeletionLayer
        {
            get => _edgeDeletionLayer;
            set { _edgeDeletionLayer = value; OnPropertyChanged(); }
        }

        // === Specification Target Groups ===
        public ObservableCollection<SpecTargetGroup> SpecGroups { get; }

        public int TotalSpecsActive => SpecGroups?.Count(g => g.IsSelected) ?? 0;

        // === Estimated Rate (calculated live) ===

        private decimal _estimatedRatePerSqm;
        public decimal EstimatedRatePerSqm
        {
            get => _estimatedRatePerSqm;
            set
            {
                _estimatedRatePerSqm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCalculatedRate));
            }
        }

        public bool HasCalculatedRate => EstimatedRatePerSqm > 0m;

        // === Commands ===
        public ICommand SelectGlassTypeCommand { get; }
        public ICommand ToggleSpecGroupCommand { get; }
        public ICommand ApplyRateCommand { get; }

        public PricingEstimatorViewModel()
        {
            // Initialize panel VMs
            SguVM = new SguParameters.SguParametersViewModel();

            // Subscribe to SGU parameter changes to auto-recalculate rate
            SguVM.PropertyChanged += OnSguParametersChanged;

            // Seed 4 glass type options
            GlassTypes = new ObservableCollection<GlassTypeCard>
            {
                new GlassTypeCard
                {
                    Code = "SGU",
                    Title = "Single Glass Unit (SGU)",
                    Description = "Monolithic pane. Direct pricing with custom color & treatments.",
                    Icon = "\U0001F4E6",
                    AccentColor = "#6366F1"
                },
                new GlassTypeCard
                {
                    Code = "DGU",
                    Title = "Double Glass Unit (DGU)",
                    Description = "Insulated glass assembly. Configures spacers, gas insertion & outer/inner treatments.",
                    Icon = "\U0001F4DA",
                    AccentColor = "#A855F7"
                },
                new GlassTypeCard
                {
                    Code = "LAM",
                    Title = "Laminated Assembly (LAM)",
                    Description = "Multi-layer safety sandwich. Customizes PVB thickness, outer/inner prices & margins.",
                    Icon = "\U0001F517",
                    AccentColor = "#22C55E"
                },
                new GlassTypeCard
                {
                    Code = "DGU+LAM",
                    Title = "DGU + Laminated (Hybrid)",
                    Description = "Double glazed unit with one laminated layer. Combines thermal and safety performance.",
                    Icon = "\U0001F3D7",
                    AccentColor = "#F97316"
                }
            };

            // Seed sample spec groups
            SpecGroups = new ObservableCollection<SpecTargetGroup>
            {
                new SpecTargetGroup
                {
                    Name = "Spec Group 1",
                    Description = "Default spec group",
                    IsSelected = true
                }
            };

            SelectGlassTypeCommand = new RelayCommand(param =>
            {
                if (param is GlassTypeCard card)
                    SelectedGlassType = card;
            });

            ToggleSpecGroupCommand = new RelayCommand(param =>
            {
                if (param is SpecTargetGroup group)
                {
                    group.IsSelected = !group.IsSelected;
                    OnPropertyChanged(nameof(TotalSpecsActive));
                }
            });

            // Apply calculated rate to selected specs
            // TODO: In Step 5 wiring, push rate to Step 5 spec groups
            ApplyRateCommand = new RelayCommand(
                _ =>
                {
                    var selectedCount = SpecGroups.Count(g => g.IsSelected);
                    MessageBox.Show(
                        $"Applying rate to {selectedCount} spec group(s):\n\n" +
                        $"Rate: AED {EstimatedRatePerSqm:N2} / sqm\n\n" +
                        $"(Full push to Step 5 grid will be wired when Step 5 is built.)",
                        "Rate Applied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                },
                _ => TotalSpecsActive > 0 && EstimatedRatePerSqm > 0m);

            // Default selection = SGU
            SelectedGlassType = GlassTypes[0];

            // Default treatments
            PolishedEdges = false;
            DrillCutouts = false;
            CountersunkHoles = false;
            EdgeDeletionLayer = false;

            // Initial calculation with default SGU values
            RecalculateEstimatedRate();
        }

        // ==================== LIVE CALCULATION ====================

        /// <summary>
        /// Called whenever any SGU parameter changes. Triggers rate recalculation.
        /// </summary>
        private void OnSguParametersChanged(object sender, PropertyChangedEventArgs e)
        {
            // Only recalculate on fields that actually affect the price
            switch (e.PropertyName)
            {
                case nameof(SguParameters.SguParametersViewModel.BasePricePerSqm):
                case nameof(SguParameters.SguParametersViewModel.YieldFactor):
                case nameof(SguParameters.SguParametersViewModel.CuttingCostPerSqm):
                case nameof(SguParameters.SguParametersViewModel.TemperingCostPerSqm):
                case nameof(SguParameters.SguParametersViewModel.TargetMarginPercent):
                    RecalculateEstimatedRate();
                    break;
            }
        }

        /// <summary>
        /// Master calculation entry point.
        /// Routes to the correct formula based on the selected glass type.
        /// </summary>
        private void RecalculateEstimatedRate()
        {
            if (SelectedGlassType == null)
            {
                EstimatedRatePerSqm = 0m;
                return;
            }

            switch (SelectedGlassType.Code)
            {
                case "SGU":
                    EstimatedRatePerSqm = CalculateSguRate();
                    break;

                case "DGU":
                    // TODO: Wire when Phase 6.6 DGU calculation is built
                    EstimatedRatePerSqm = 0m;
                    break;

                case "LAM":
                    // TODO: Wire when Phase 6.7 LAM calculation is built
                    EstimatedRatePerSqm = 0m;
                    break;

                case "DGU+LAM":
                    // Blank for now (per business decision)
                    EstimatedRatePerSqm = 0m;
                    break;

                default:
                    EstimatedRatePerSqm = 0m;
                    break;
            }
        }

        /// <summary>
        /// SGU (Single Glass Unit) pricing formula:
        ///   Step 1: raw = Base Price per SQM
        ///   Step 2: cost = raw / Yield Factor       (adjusts for wastage)
        ///   Step 3: cost = cost + Cutting Cost
        ///   Step 4: cost = cost + Tempering Cost
        ///   Step 5: rate = cost * (1 + Margin%/100)
        ///
        /// Example: Base=25, Yield=0.85, Cut=10, Temp=10, Margin=15%
        ///   25 / 0.85           = 29.41
        ///   29.41 + 10          = 39.41
        ///   39.41 + 10          = 49.41
        ///   49.41 * 1.15        = 56.82  ← rate/sqm
        /// </summary>
        private decimal CalculateSguRate()
        {
            var vm = SguVM;

            decimal basePrice = vm.BasePricePerSqm;
            decimal yieldFactor = vm.YieldFactor;
            decimal cuttingCost = vm.CuttingCostPerSqm;
            decimal temperingCost = vm.TemperingCostPerSqm;
            decimal marginPercent = vm.TargetMarginPercent;

            // Safety: prevent divide-by-zero if user clears yield field
            if (yieldFactor <= 0m)
                yieldFactor = 1m;

            decimal step1 = basePrice;                                    // 25.00
            decimal step2 = step1 / yieldFactor;                          // 29.41
            decimal step3 = step2 + cuttingCost;                          // 39.41
            decimal step4 = step3 + temperingCost;                        // 49.41
            decimal step5 = step4 * (1m + (marginPercent / 100m));        // 56.82

            return Math.Round(step5, 2);
        }
    }
}