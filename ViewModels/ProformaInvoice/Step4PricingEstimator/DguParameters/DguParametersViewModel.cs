using System.Collections.Generic;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguParameters
{
    /// <summary>
    /// ViewModel for DGU (Double Glass Unit) parameter panel.
    /// Handles outer/inner glass parameters, spacer configuration (with
    /// auto-suggested pricing based on width + material surcharge),
    /// U-insert & overlap options, yield factor, and margin.
    /// </summary>
    public class DguParametersViewModel : ViewModelBase
    {
        // Guard flag to prevent recursion when auto-updating spacer price
        private bool _isSuggestingSpacerPrice;

        // ===================== Row 1: Outer Glass =====================

        private string _outerThickness;
        public string OuterThickness
        {
            get => _outerThickness;
            set
            {
                _outerThickness = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GeneratedSpecName));
            }
        }

        private string _outerGlassColor;
        public string OuterGlassColor
        {
            get => _outerGlassColor;
            set
            {
                _outerGlassColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GeneratedSpecName));
            }
        }

        private string _outerTreatment;
        public string OuterTreatment
        {
            get => _outerTreatment;
            set
            {
                _outerTreatment = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GeneratedSpecName));
            }
        }

        private decimal _outerBasePrice;
        public decimal OuterBasePrice
        {
            get => _outerBasePrice;
            set { _outerBasePrice = value; OnPropertyChanged(); }
        }

        // ===================== Row 2: Spacer Width + Material + Price =====================

        private string _spacerWidth;
        public string SpacerWidth
        {
            get => _spacerWidth;
            set
            {
                _spacerWidth = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GeneratedSpecName));
                SuggestSpacerPrice();
            }
        }

        private string _spacerMaterial;
        public string SpacerMaterial
        {
            get => _spacerMaterial;
            set
            {
                _spacerMaterial = value;
                OnPropertyChanged();
                SuggestSpacerPrice();
            }
        }

        private decimal _spacerPrice;
        public decimal SpacerPrice
        {
            get => _spacerPrice;
            set { _spacerPrice = value; OnPropertyChanged(); }
        }

        // ===================== Row 3: U-Insert + Overlap =====================

        private string _uInsertOption;
        public string UInsertOption
        {
            get => _uInsertOption;
            set
            {
                _uInsertOption = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GeneratedSpecName));
            }
        }

        private string _overlapOption;
        public string OverlapOption
        {
            get => _overlapOption;
            set
            {
                _overlapOption = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GeneratedSpecName));
            }
        }

        // ===================== Row 4: Inner Glass =====================

        private string _innerThickness;
        public string InnerThickness
        {
            get => _innerThickness;
            set
            {
                _innerThickness = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GeneratedSpecName));
            }
        }

        private string _innerGlassColor;
        public string InnerGlassColor
        {
            get => _innerGlassColor;
            set
            {
                _innerGlassColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GeneratedSpecName));
            }
        }

        private string _innerTreatment;
        public string InnerTreatment
        {
            get => _innerTreatment;
            set
            {
                _innerTreatment = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GeneratedSpecName));
            }
        }

        private decimal _innerBasePrice;
        public decimal InnerBasePrice
        {
            get => _innerBasePrice;
            set { _innerBasePrice = value; OnPropertyChanged(); }
        }

        // ===================== Row 5: Yield + Margin =====================

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

        // ===================== Auto-Generated Spec Name =====================

        /// <summary>
        /// Human-readable spec name auto-built from current DGU parameters.
        /// Format: "{Outer Glass} + {Spacer}[+ U-Insert Variant] + {Inner Glass}[+ Overlap]"
        /// Examples:
        ///   "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass"
        ///   "6mm HD Grey FT Glass + 12mm ASP With U-Insert + 6mm Clear FT Glass"
        ///   "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass With Overlap As Per DWG"
        /// Saved to specifications.db when user clicks APPLY in main VM.
        /// </summary>
        public string GeneratedSpecName
        {
            get
            {
                // Outer glass segment
                var outer = $"{OuterThickness} {OuterGlassColor} {OuterTreatment} Glass";

                // Spacer segment (may include U-Insert variant)
                var spacer = SpacerWidth ?? "";
                if (!string.IsNullOrWhiteSpace(UInsertOption) && UInsertOption != "None")
                    spacer += $" {UInsertOption}";

                // Inner glass segment (may include Overlap variant)
                var inner = $"{InnerThickness} {InnerGlassColor} {InnerTreatment} Glass";
                if (!string.IsNullOrWhiteSpace(OverlapOption) && OverlapOption != "None (empty spec only)")
                    inner += $" {OverlapOption}";

                return $"{outer} + {spacer} + {inner}";
            }
        }

        // ===================== Dropdown Options =====================

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

        // ===================== Constructor =====================

        public DguParametersViewModel()
        {
            // Row 1 defaults
            _outerThickness = "6mm";
            _outerGlassColor = "Clear";
            _outerTreatment = "FT";
            _outerBasePrice = 46m;

            // Row 2 defaults
            _spacerWidth = "12mm ASP";
            _spacerMaterial = "Normal Mill Aluminum";
            _spacerPrice = CalculateSuggestedSpacerPrice(_spacerWidth, _spacerMaterial);

            // Row 3 defaults
            _uInsertOption = "None";
            _overlapOption = "None (empty spec only)";

            // Row 4 defaults
            _innerThickness = "6mm";
            _innerGlassColor = "Clear";
            _innerTreatment = "FT";
            _innerBasePrice = 46m;

            // Row 5 defaults
            _yieldFactor = 0.85m;
            _marginProfitPercent = 15m;
        }

        // ===================== Auto-Suggest Spacer Price =====================

        private void SuggestSpacerPrice()
        {
            if (_isSuggestingSpacerPrice) return;
            _isSuggestingSpacerPrice = true;
            SpacerPrice = CalculateSuggestedSpacerPrice(_spacerWidth, _spacerMaterial);
            _isSuggestingSpacerPrice = false;
        }

        private decimal CalculateSuggestedSpacerPrice(string widthText, string material)
        {
            int widthMm = ParseWidthMm(widthText);
            decimal basePrice = 37.5m + ((widthMm - 6) / 2m) * 2.5m;
            decimal materialSurcharge = GetMaterialSurcharge(material);
            return basePrice + materialSurcharge;
        }

        private int ParseWidthMm(string widthText)
        {
            if (string.IsNullOrWhiteSpace(widthText)) return 12;
            var mmIndex = widthText.IndexOf("mm", System.StringComparison.OrdinalIgnoreCase);
            if (mmIndex <= 0) return 12;
            var digits = widthText.Substring(0, mmIndex).Trim();
            return int.TryParse(digits, out int mm) ? mm : 12;
        }

        private decimal GetMaterialSurcharge(string material)
        {
            return material switch
            {
                "Normal Mill Aluminum" => 0m,
                "Black Aluminum" => 5m,
                "Warm Edge (Super Spacer)" => 15m,
                "Stainless Steel" => 10m,
                "TGI Spacer" => 12m,
                _ => 0m
            };
        }
    }
}