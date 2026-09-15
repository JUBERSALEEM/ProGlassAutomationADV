// ═══════════════════════════════════════════════════════════════════
// FILE: Views/About/Sections/SystemSection.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: About → "System" tab (2026-09-15 iOS-26 redesign)
//
// Pure view — every value comes from AboutViewModel live sources.
// No inline Storyboards (crash safety), no hex colors, no literal
// CornerRadius.
// ═══════════════════════════════════════════════════════════════════
using System.Windows.Controls;

namespace ProGlassAutomation.Views.About.Sections
{
    public partial class SystemSection : UserControl
    {
        public SystemSection()
        {
            InitializeComponent();
        }
    }
}
