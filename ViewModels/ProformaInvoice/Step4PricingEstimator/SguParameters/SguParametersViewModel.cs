using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.SguParameters
{
    /// <summary>
    /// ViewModel for SGU (Single Glass Unit) parameter panel.
    /// Handles thickness, color, treatment, pricing factors,
    /// processing treatments, and live rate calculation.
    ///
    /// FIX 1: GeneratedSpecName no longer appends " Glass" —
    ///        TreatmentOptions already contain "Glass" suffix
    ///        (e.g. "FT Glass") producing correct "6mm Clear FT Glass".
    ///        Polish suffix appended when not "None (Standard)".
    ///
    /// FIX 2: PolishOptions expanded to cover real UAE glass industry
    ///        fabrication types — holes, cutouts, DWG refs, edge polish.
    /// </summary>
    public partial class SguParametersViewModel : ViewModelBase
    {
        // === Parameter Row 1: Thickness, Color, Treatment, Base Price ===

        private string _selectedThickness;
        public string SelectedThickness
        {
            get => _selectedThickness;
            set
            {
                _selectedThickness = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GeneratedSpecName));
                OnGlassChanged();   // PHASE A: Sheet Store refresh
            }
        }

        private string _glassColor;
        public string GlassColor
        {
            get => _glassColor;
            set
            {
                _glassColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GeneratedSpecName));
                OnGlassChanged();   // PHASE A: Sheet Store refresh
            }
        }

        private string _selectedTreatment;
        public string SelectedTreatment
        {
            get => _selectedTreatment;
            set
            {
                _selectedTreatment = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GeneratedSpecName));
            }
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

        // === Parameter Row 3: Polish / Detail Option ===

        private string _selectedPolishOption;
        public string SelectedPolishOption
        {
            get => _selectedPolishOption;
            set
            {
                _selectedPolishOption = value;
                OnPropertyChanged();
                // FIX 1: polish also affects the generated spec name
                OnPropertyChanged(nameof(GeneratedSpecName));
            }
        }

        // ===================== Auto-Generated Spec Name =====================

        /// <summary>
        /// Human-readable spec name auto-built from current parameters.
        /// Format: "{Thickness} {Color} {Treatment}[ {Polish}]"
        /// Examples:
        ///   Base:            "6mm Clear FT Glass"
        ///   With polish:     "6mm Clear FT Glass With Fanhole"
        ///   With DWG ref:    "6mm Clear FT Glass With Holes As Per DWG"
        /// Saved to specifications.db when user clicks APPLY in main VM.
        ///
        /// FIX 1: No longer appends redundant " Glass" — TreatmentOptions
        ///        already contain "Glass" suffix (e.g. "FT Glass").
        /// FIX 2: Polish suffix appended only when meaningful
        ///        (skipped for "None (Standard)").
        /// </summary>
        public string GeneratedSpecName
        {
            get
            {
                string baseName =
                    $"{SelectedThickness} {GlassColor} {SelectedTreatment}"
                    .Trim();

                // Append polish suffix only when it adds meaning
                if (!string.IsNullOrWhiteSpace(SelectedPolishOption) &&
                    SelectedPolishOption != "None (Standard)")
                {
                    baseName += $" {SelectedPolishOption}";
                }

                return baseName;
            }
        }

        // === Dropdown Options ===

        public ObservableCollection<string> ThicknessOptions { get; } = new ObservableCollection<string>
        {
            "3mm", "4mm", "5mm", "6mm", "8mm", "10mm", "12mm", "15mm", "19mm"
        };

        public ObservableCollection<string> ColorOptions { get; } = new ObservableCollection<string>
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

        /// <summary>
        /// UAE glass industry standard fabrication options.
        /// Selected value is appended to spec name when != "None (Standard)".
        /// Grouped logically:
        ///   • Default            : None (Standard)
        ///   • Edge finishes      : Polished Edges, Beveled, Mitring
        ///   • Surface treatments : Frosted, Sandblasted
        ///   • Fabrication holes  : Fanhole, Holes, Holes & Cutouts, Cutouts
        ///   • Polish variants    : All Side, Both Height Side
        ///   • DWG references     : "As Per DWG" suffix variants
        /// </summary>
        public List<string> PolishOptions { get; } = new List<string>
        {
            // Default
            "None (Standard)",

            // Edge finishes
            "Polished Edges",
            "Beveled Edges",
            "Mitring (45° Bevel)",

            // Surface treatments
            "Frosted",
            "Sandblasted",

            // Fabrication — Fanhole
            "With Fanhole",
            "With Fanhole As Per DWG",

            // Fabrication — Holes
            "With Holes",
            "With Holes As Per DWG",

            // Fabrication — Holes + Cutouts
            "With Holes And Cutouts",
            "With Holes And Cutouts As Per DWG",

            // Fabrication — Cutouts
            "With Cutouts",
            "With Cutouts As Per DWG",

            // Polish variants
            "With All Side Polish",
            "With Both Height Side Polish",
            "With Both Height Side Polish As Per DWG"
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

            // === Row 3 Defaults ===
            SelectedPolishOption = "None (Standard)";
        }
    }
}