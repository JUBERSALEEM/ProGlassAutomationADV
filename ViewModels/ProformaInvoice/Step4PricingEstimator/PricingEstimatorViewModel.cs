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
        public string Code { get; set; }              // "SGU", "DGU", "LAM", "DGU+LAM"
        public string Title { get; set; }             // "Single Glass Unit"
        public string Description { get; set; }       // Full descriptive text
        public string Icon { get; set; }              // Emoji or symbol
        public string AccentColor { get; set; }       // Hex color for the card accent

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
    /// glass parameters, processing treatments, and live rate calculation.
    /// </summary>
    public class PricingEstimatorViewModel : ViewModelBase
    {
        // === Glass Type Cards ===
        public ObservableCollection<GlassTypeCard> GlassTypes { get; }

        private GlassTypeCard _selectedGlassType;
        public GlassTypeCard SelectedGlassType
        {
            get => _selectedGlassType;
            set
            {
                if (_selectedGlassType == value) return;

                // Unmark old selection
                if (_selectedGlassType != null)
                    _selectedGlassType.IsSelected = false;

                _selectedGlassType = value;

                // Mark new selection
                if (_selectedGlassType != null)
                    _selectedGlassType.IsSelected = true;

                OnPropertyChanged();
            }
        }

        // === Commands ===
        public ICommand SelectGlassTypeCommand { get; }

        public PricingEstimatorViewModel()
        {
            // Seed 4 glass type options
            GlassTypes = new ObservableCollection<GlassTypeCard>
            {
                new GlassTypeCard
                {
                    Code = "SGU",
                    Title = "Single Glass Unit (SGU)",
                    Description = "Monolithic pane. Direct pricing with custom color & treatments.",
                    Icon = "\U0001F4E6",       // 📦
                    AccentColor = "#6366F1"    // Indigo
                },
                new GlassTypeCard
                {
                    Code = "DGU",
                    Title = "Double Glass Unit (DGU)",
                    Description = "Insulated glass assembly. Configures spacers, gas insertion & outer/inner treatments.",
                    Icon = "\U0001F4DA",       // 📚
                    AccentColor = "#A855F7"    // Purple
                },
                new GlassTypeCard
                {
                    Code = "LAM",
                    Title = "Laminated Assembly (LAM)",
                    Description = "Multi-layer safety sandwich. Customizes PVB thickness, outer/inner prices & margins.",
                    Icon = "\U0001F517",       // 🔗
                    AccentColor = "#22C55E"    // Green
                },
                new GlassTypeCard
                {
                    Code = "DGU+LAM",
                    Title = "DGU + Laminated (Hybrid)",
                    Description = "Double glazed unit with one laminated layer. Combines thermal and safety performance.",
                    Icon = "\U0001F3D7",       // 🏗️
                    AccentColor = "#F97316"    // Orange
                }
            };

            // Command: select a card
            SelectGlassTypeCommand = new RelayCommand(param =>
            {
                if (param is GlassTypeCard card)
                    SelectedGlassType = card;
            });

            // Default selection = SGU
            SelectedGlassType = GlassTypes[0];
        }
    }
}