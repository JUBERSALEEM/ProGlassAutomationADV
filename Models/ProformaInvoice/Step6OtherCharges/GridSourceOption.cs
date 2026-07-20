using ProGlassAutomation.ViewModels;

namespace ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges
{
    /// <summary>
    /// Represents one option in the "Target Grid Source" dropdown for an Other Charge row.
    /// Example labels:
    ///   "LM1 (Glass 1 Perimeter): 303.33 lm"
    ///   "SQM1 + SQM2 (Combined Area): 179.52 sqm"
    ///   "Total Pieces: 55 pcs"
    /// </summary>
    public class GridSourceOption : ViewModelBase
    {
        /// <summary>Internal key used for lookup (e.g. "LM1", "LM2", "LM_COMBINED", "SQM1", "TOTAL_PIECES").</summary>
        public string Key { get; set; } = "";

        /// <summary>Human-readable label shown in the dropdown.</summary>
        private string _displayLabel = "";
        public string DisplayLabel
        {
            get => _displayLabel;
            set { if (_displayLabel == value) return; _displayLabel = value; OnPropertyChanged(); }
        }

        /// <summary>Live numeric value pulled from DataGrid.</summary>
        private decimal _value;
        public decimal Value
        {
            get => _value;
            set { if (_value == value) return; _value = value; OnPropertyChanged(); }
        }

        /// <summary>Unit of measurement — determines which charges can use this source.</summary>
        public OtherChargeUnit Unit { get; set; }

        /// <summary>Short description shown after the formula, e.g. "(W1 H1 PERIMETER (LM1))".</summary>
        public string FormulaHint { get; set; } = "";

        public override string ToString() => DisplayLabel;
    }
}