// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Personal/PersonalSheetStoreView.xaml.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// GB-P8 (2026-09-18) — Glass Blush Personal Sheet Store (glass-05).
// Binds to the SAME SheetStoreViewModel. InitializeComponent only.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Personal
{
    /// <summary>
    /// Glass Blush Personal Sheet Store (glass-05 reference): glass card
    /// grid over the same SheetStoreViewModel.
    /// </summary>
    public partial class PersonalSheetStoreView : UserControl
    {
        public PersonalSheetStoreView()
        {
            InitializeComponent();
        }
    }
}
