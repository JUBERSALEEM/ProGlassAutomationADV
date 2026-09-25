// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Production/ProductionStageChip.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — eight-station chip shared by the process and control boards
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.ViewModels.Production
{
    public sealed class ProductionStageChip : ViewModelBase
    {
        public ProductionStageChip(int stage, string label, string tip)
        {
            Stage = stage;
            Label = label;
            Tip = tip;
        }

        public int Stage { get; }
        public string Label { get; }
        public string Tip { get; }

        private int _count;
        public int Count
        {
            get => _count;
            set
            {
                if (_count == value) return;
                _count = value;
                OnPropertyChanged();
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public static ProductionStageChip[] CreateBoard() => new[]
        {
            new ProductionStageChip(1, "Cutting", "Cutting"),
            new ProductionStageChip(2, "Holes", "Holes and cutouts"),
            new ProductionStageChip(3, "Polish", "Polish"),
            new ProductionStageChip(4, "Edge Del", "Edge deletion"),
            new ProductionStageChip(5, "Mitring", "Mitring"),
            new ProductionStageChip(6, "Temper", "Tempering"),
            new ProductionStageChip(7, "Pairing", "Pairing"),
            new ProductionStageChip(8, "DGU", "Doubling and lamination")
        };
    }
}
