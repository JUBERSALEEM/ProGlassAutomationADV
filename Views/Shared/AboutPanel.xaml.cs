// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Shared/AboutPanel.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared — About popup
//
// Non-blocking popup for the top-bar About icon. Shows real app identity
// from AppInfo (single source of truth — no duplicated version strings).
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
