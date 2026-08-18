using ProGlassAutomation.Agent.Demo;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Settings.Sections
{
    public partial class SettingsDemoSection : UserControl
    {
        public SettingsDemoSection()
        {
            InitializeComponent();
            // Local value — parent SettingsViewModel must not steal command bindings.
            DataContext = new DemoCenterViewModel();
        }
    }
}
