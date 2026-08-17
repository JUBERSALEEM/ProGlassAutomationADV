// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Shared/AboutPanel.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared — About popup
//
// Shows real app identity from AppInfo. Screenshot capture is handled by the
// dedicated top-bar camera button (Views/Shared/ScreenshotPanel), not here.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using ProGlassAutomation.Services.Updates;

namespace ProGlassAutomation.Views.Shared
{
    public partial class AboutPanel : UserControl
    {
        public AboutPanel()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                VersionText.Text = $"Version {AppInfo.Version}";
                DeveloperText.Text = AppInfo.Developer;
                CopyrightText.Text = AppInfo.Copyright;
            };
        }
    }
}
