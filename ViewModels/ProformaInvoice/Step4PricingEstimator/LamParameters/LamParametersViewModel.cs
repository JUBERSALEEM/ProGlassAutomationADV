using System.Collections.Generic;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.LamParameters
{
    /// <summary>
    /// ViewModel for LAM (Laminated Assembly) parameter panel.
    /// Handles outer/inner glass, PVB interlayer configuration,
    /// lamination costs (layup + autoclave), yield factor, and margin.
    /// </summary>
    public class LamParametersViewModel : ViewModelBase
    {
        // ===================== Row 1: Outer Glass =====================

        private string _outerThickness;
        public string OuterThickness
        {
            get => _outerThickness;
            set { _outerThickness = value; OnPropertyChanged(); }
        }

        private string _outerGlassColor;
        public string OuterGlassColor
        {
            get => _outerGlassColor;
            set { _outerGlassColor = value; OnPropertyChanged(); }
        }

        private string _outerTreatment;
        public string OuterTreatment
        {
            get => _outerTreatment;
            set { _outerTreatment = value; OnPropertyChanged(); }
        }

        private decimal _outerBasePrice;
        public decimal OuterBasePrice
        {
            get => _outerBasePrice;
            set { _outerBasePrice = value; OnPropertyChanged(); }
        }

        // ===================== Shared Dropdown Options =====================

        public List<string> ThicknessOptions { get; } = new()
        {
            "3mm", "4mm", "5mm", "6mm", "8mm", "10mm", "12mm", "15mm", "19mm"
        };

        public List<string> ColorOptions { get; } = new()
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

        public List<string> TreatmentOptions { get; } = new()
        {
            "FT",
            "Annealed",
            "Heat Strengthened",
            "Heat Soaked"
        };

        public LamParametersViewModel()
        {
            // === Row 1 Defaults ===
            OuterThickness = "6mm";
            OuterGlassColor = "Clear";
            OuterTreatment = "FT";
            OuterBasePrice = 46m;

            // Rows 2, 3, 4 will be added in Sub-Phases 6.7c, 6.7d, 6.7e
        }
    }
}