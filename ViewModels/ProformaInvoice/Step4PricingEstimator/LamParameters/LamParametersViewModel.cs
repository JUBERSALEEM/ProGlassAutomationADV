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

        // ===================== Row 2: Inner Glass =====================

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

        // ===================== Row 3: PVB Interlayer Specs =====================

        private string _pvbThickness;
        public string PvbThickness
        {
            get => _pvbThickness;
            set { _pvbThickness = value; OnPropertyChanged(); }
        }

        private string _pvbColor;
        public string PvbColor
        {
            get => _pvbColor;
            set { _pvbColor = value; OnPropertyChanged(); }
        }

        private decimal _pvbMaterialRate;
        public decimal PvbMaterialRate
        {
            get => _pvbMaterialRate;
            set { _pvbMaterialRate = value; OnPropertyChanged(); }
        }

        private string _laminationPolish;
        public string LaminationPolish
        {
            get => _laminationPolish;
            set { _laminationPolish = value; OnPropertyChanged(); }
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

        // ===================== LAM-Specific Dropdown Options =====================

        public List<string> PvbThicknessOptions { get; } = new()
        {
            "0.38mm",
            "0.76mm",
            "1.14mm",
            "1.52mm",
            "2.28mm",
            "3.04mm"
        };

        public List<string> PvbColorOptions { get; } = new()
        {
            "Clear",
            "White",
            "Frosted",
            "Milky White",
            "Black",
            "Grey",
            "Bronze"
        };

        public List<string> LaminationPolishOptions { get; } = new()
        {
            "None (Standard)",
            "With All Side Polish",
            "With Edge Polish Only",
            "With Beveled Edges"
        };

        public LamParametersViewModel()
        {
            // === Row 1 Defaults ===
            OuterThickness = "6mm";
            OuterGlassColor = "Clear";
            OuterTreatment = "FT";
            OuterBasePrice = 46m;

            // === Row 2 Defaults ===
            InnerThickness = "6mm";
            InnerGlassColor = "Clear";
            InnerTreatment = "FT";
            InnerBasePrice = 46m;

            // === Row 3 Defaults ===
            PvbThickness = "1.52mm";
            PvbColor = "Clear";
            PvbMaterialRate = 80m;
            LaminationPolish = "None (Standard)";

            // Rows 4, 5 will be added in Sub-Phases 6.7e, 6.7f
        }
    }
}