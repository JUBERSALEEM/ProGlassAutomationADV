// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Production/Controls/ProductionUnitDetailControl.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — shared full-page unit detail overlay (code-behind)
//
// Pure view: all state and behaviour live on ProductionStageViewModelBase
// (inherited DataContext). This control only hosts the overlay XAML.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Production.Controls
{
    public partial class ProductionUnitDetailControl : UserControl
    {
        public ProductionUnitDetailControl()
        {
            InitializeComponent();
        }
    }
}
