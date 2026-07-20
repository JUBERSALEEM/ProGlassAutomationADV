using ProGlassAutomation.ViewModels;

namespace ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages
{
    public class SpecGroupColorSide : ViewModelBase
    {
        public int SpecNumber { get; set; }
        public string SpecName { get; set; } = "";
        public string SideLabel { get; set; } = ""; // "Outer (W1/H1)" or "Inner (W2/H2)"

        private decimal _sqmValue;
        public decimal SqmValue
        {
            get => _sqmValue;
            set { if (_sqmValue == value) return; _sqmValue = value; OnPropertyChanged(); }
        }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set { if (_isChecked == value) return; _isChecked = value; OnPropertyChanged(); }
        }

        public string DisplayLabel => $"Spec {SpecNumber} ({SpecName}) → {SideLabel}";
    }
}