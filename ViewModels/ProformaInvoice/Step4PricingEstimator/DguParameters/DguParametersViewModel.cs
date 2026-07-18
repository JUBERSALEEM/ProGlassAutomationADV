using System.Collections.Generic;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguParameters
{
    /// <summary>
    /// ViewModel for DGU (Double Glass Unit) parameter panel.
    /// Handles outer/inner glass parameters, spacer configuration,
    /// U-insert & overlap options, yield factor, and margin.
    /// </summary>
    public class DguParametersViewModel : ViewModelBase
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

        // ===================== Row 2: Spacer + U-Insert + Overlap =====================

        private string _spacerWidth;
        public string SpacerWidth
        {
            get => _spacerWidth;
            set { _spacerWidth = value; OnPropertyChanged(); }
        }

        private string _spacerMaterial;
        public string SpacerMaterial
        {
            get => _spacerMaterial;
            set { _spacerMaterial = value; OnPropertyChanged(); }
        }

        private string _uInsertOption;
        public string UInsertOption
        {
            get => _uInsertOption;
            set { _uInsertOption = value; OnPropertyChanged(); }
        }

        private string _overlapOption;
        public string OverlapOption
        {
            get => _overlapOption;
            set { _overlapOption = value; OnPropertyChanged(); }
        }

        // ===================== Row 3: Inner Glass =====================

        private string _innerThickness;
        public string InnerThickness
        {
            get => _innerThickness;
            set { _innerThickness = value; OnPropertyChanged(); }
        }

        private string _innerGlassColor;
        public string InnerGlassColor
        {
            get => _innerGlassColor;
            set { _innerGlassColor = value; OnPropertyChanged(); }
        }

        private string _innerTreatment;
        public string InnerTreatment
        {
            get => _innerTreatment;
            set { _innerTreatment = value; OnPropertyChanged(); }
        }

        private decimal _innerBasePrice;
        public decimal InnerBasePrice
        {
            get => _innerBasePrice;
            set { _innerBasePrice = value; OnPropertyChanged(); }
        }

        // ===================== Row 4: Yield + Profit (next phase) =====================

        private decimal _yieldFactor;
        public decimal YieldFactor
        {
            get => _yieldFactor;
            set { _yieldFactor = value; OnPropertyChanged(); }
        }

        private decimal _marginProfitPercent;
        public decimal MarginProfitPercent
        {
            get => _marginProfitPercent;
            set { _marginProfitPercent = value; OnPropertyChanged(); }
        }

        // ===================== Options =====================

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

        public List<string> SpacerWidthOptions { get; } = new()
        {
            "6mm ASP",
            "8mm ASP",
            "10mm ASP",
            "12mm ASP",
            "14mm ASP",
            "16mm ASP",
            "18mm ASP",
            "20mm ASP",
            "22mm ASP",
            "24mm ASP",
            "26mm ASP",
            "28mm ASP",
            "30mm ASP",
            "32mm ASP"
        };

        public List<string> SpacerMaterialOptions { get; } = new()
        {
            "Normal Mill Aluminum",
            "Black Aluminum",
            "Warm Edge (Super Spacer)",
            "Stainless Steel",
            "TGI Spacer"
        };

        public List<string> UInsertOptions { get; } = new()
        {
            "None",
            "With U-Insert",
            "With U-Insert As Per DWG"
        };

        public List<string> OverlapOptions { get; } = new()
        {
            "None (empty spec only)",
            "With Overlap As Per DWG"
        };

        public DguParametersViewModel()
        {
            // Row 1 defaults
            OuterThickness = "6mm";
            OuterGlassColor = "Clear";
            OuterTreatment = "FT";
            OuterBasePrice = 46m;

            // Row 2 defaults
            SpacerWidth = "12mm ASP";
            SpacerMaterial = "Normal Mill Aluminum";
            UInsertOption = "None";
            OverlapOption = "None (empty spec only)";

            // Row 3 defaults
            InnerThickness = "6mm";
            InnerGlassColor = "Clear";
            InnerTreatment = "FT";
            InnerBasePrice = 46m;

            // Row 4 defaults (UI next phase)
            YieldFactor = 0.85m;
            MarginProfitPercent = 15m;
        }
    }
}