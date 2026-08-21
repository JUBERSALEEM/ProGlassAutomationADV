// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/About/AboutView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: About — tabbed shell (v5)
//
// The shell has no logic: it hosts the hero + tab strip and switches
// between the three section UserControls. The email contact handler lives
// in Sections/DeveloperSection.xaml.cs so that section stays self-contained.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.About
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            InitializeComponent();
        }
    }
}
