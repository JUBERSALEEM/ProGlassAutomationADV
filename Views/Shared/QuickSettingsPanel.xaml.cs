// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Shared/QuickSettingsPanel.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared — quick-settings popup
//
// Non-blocking popup for the top-bar Settings icon. Each row navigates to
// the Settings module with that section active. The popup itself is
// dismissed by the shell (StaysOpen + click-outside), never by navigating
// away, so it does not interfere with the module content behind it.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Shared
{
    public partial class QuickSettingsPanel : UserControl
    {
        public QuickSettingsPanel()
        {
            InitializeComponent();
        }

        }
}
