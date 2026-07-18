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

        // === Parameter Row 2: Cutting, Tempering, Yield, Margin ===

        private decimal _cuttingCostPerSqm;
        public decimal CuttingCostPerSqm
        {
            get => _cuttingCostPerSqm;
            set { _cuttingCostPerSqm = value; OnPropertyChanged(); }
        }

        private decimal _temperingCostPerSqm;
        public decimal TemperingCostPerSqm
        {
            get => _temperingCostPerSqm;
            set { _temperingCostPerSqm = value; OnPropertyChanged(); }
        }

        private decimal _yieldFactor;
        public decimal YieldFactor
        {
            get => _yieldFactor;
            set { _yieldFactor = value; OnPropertyChanged(); }
        }

        private decimal _targetMarginPercent;
        public decimal TargetMarginPercent
        {
            get => _targetMarginPercent;
            set { _targetMarginPercent = value; OnPropertyChanged(); }
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
            // === Row 1 Defaults ===
            SelectedThickness = "6mm";
            GlassColor = "Clear";
            SelectedTreatment = "FT Glass";
            BasePricePerSqm = 46m;

            // === Row 2 Defaults ===
            CuttingCostPerSqm = 15m;
            TemperingCostPerSqm = 25m;
            YieldFactor = 0.85m;
            TargetMarginPercent = 15m;

            // Row 3 (Polish) will be added in Sub-Phase 6.4d
        }
    }
}