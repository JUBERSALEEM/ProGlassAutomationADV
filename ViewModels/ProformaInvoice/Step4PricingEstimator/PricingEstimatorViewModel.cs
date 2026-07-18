using ProGlassAutomation.Commands;
using System.Collections.ObjectModel;
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
            }
        }

        // === Processing Treatments (shared across all glass types) ===

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

        // === Commands ===
        public ICommand SelectGlassTypeCommand { get; }

        public PricingEstimatorViewModel()
        {
            // Initialize panel VMs
            SguVM = new SguParameters.SguParametersViewModel();

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

            SelectGlassTypeCommand = new RelayCommand(param =>
            {
                if (param is GlassTypeCard card)
                    SelectedGlassType = card;
            });

            // Default selection = SGU
            SelectedGlassType = GlassTypes[0];

            // Default: no treatments checked
            PolishedEdges = false;
            DrillCutouts = false;
            CountersunkHoles = false;
            EdgeDeletionLayer = false;
        }
    }
}