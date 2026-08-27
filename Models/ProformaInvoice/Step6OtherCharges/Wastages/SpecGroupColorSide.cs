using System;
using ProGlassAutomation.ViewModels;

namespace ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages
{
    public class SpecGroupColorSide : ViewModelBase
    {
        public int SpecNumber { get; set; }
        public string SpecName { get; set; } = "";
        public string SideLabel { get; set; } = ""; // "Outer (W1/H1)", "Inner (W2/H2)", "Lam Outer (W2/H2)", or "Lam Inner (W2/H2)"
        public string ColorName { get; set; } = "";
        public int SheetWidthMm { get; set; }
        public int SheetHeightMm { get; set; }
        public decimal PricePerSqm { get; set; }

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

        public string DisplayLabel =>
            string.IsNullOrWhiteSpace(ColorName)
                ? $"Spec {SpecNumber} ({SpecName}) → {SideLabel}"
                : $"Spec {SpecNumber} ({SpecName}) → {SideLabel} · {ColorName}";

        public string ShortLabel
        {
            get
            {
                if (!string.IsNullOrEmpty(SideLabel))
                {
                    if (SideLabel.Contains("Lam Inner", StringComparison.OrdinalIgnoreCase))
                        return $"Spec {SpecNumber} Lam Inner";
                    if (SideLabel.Contains("Lam Outer", StringComparison.OrdinalIgnoreCase))
                        return $"Spec {SpecNumber} Lam Outer";
                    if (SideLabel.Contains("Inner", StringComparison.OrdinalIgnoreCase))
                        return $"Spec {SpecNumber} Inner";
                }

                return $"Spec {SpecNumber} Outer";
            }
        }
    }
}