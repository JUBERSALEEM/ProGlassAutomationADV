using System.Collections.Generic;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.SguParameters
{
    /// <summary>
    /// ViewModel for SGU (Single Glass Unit) parameter panel.
    /// Handles thickness, color, treatment, pricing factors,
    /// processing treatments, and live rate calculation.
    /// </summary>
    public class SguParametersViewModel : ViewModelBase
    {
        // === Parameter Row 1: Thickness, Color, Treatment, Base Price ===

        private string _selectedThickness;
        public string SelectedThickness
        {
            get => _selectedThickness;
            set { _selectedThickness = value; OnPropertyChanged(); }
        }

        private string _glassColor;
        public string GlassColor
        {
            get => _glassColor;
            set { _glassColor = value; OnPropertyChanged(); }
        }

        private string _selectedTreatment;
        public string SelectedTreatment
        {
            get => _selectedTreatment;
            set { _selectedTreatment = value; OnPropertyChanged(); }
        }

        private decimal _basePricePerSqm;
        public decimal BasePricePerSqm
        {
            get => _basePricePerSqm;
            set { _basePricePerSqm = value; OnPropertyChanged(); }
        }

        // === Dropdown Options ===

        public List<string> ThicknessOptions { get; } = new List<string>
        {
            "3mm", "4mm", "5mm", "6mm", "8mm", "10mm", "12mm", "15mm", "19mm"
        };

        public List<string> ColorOptions { get; } = new List<string>
        {
            "Clear",
            "HD Grey",
            "Bronze",
            "Green",
            "Blue",
            "Silver",
            "Reflective Grey",
            "Ford Blue",
            "Dark Grey"
        };

        public List<string> TreatmentOptions { get; } = new List<string>
        {
            "FT Glass",
            "Annealed",
            "Heat Strengthened",
            "Heat Soaked"
        };

        public SguParametersViewModel()
        {
            // Default values matching typical SGU config
            SelectedThickness = "6mm";
            GlassColor = "Clear";
            SelectedTreatment = "FT Glass";
            BasePricePerSqm = 46m;

            // Row 2 & 3 fields will be added in sub-phases 6.4c, 6.4d
        }
    }
}