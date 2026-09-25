// ═══════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/Cards/DemoFfmpegCard.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v3 — DemoFfmpegCard: pure presentation card.
// ═══════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using ProGlassAutomation.Views.Setup;

namespace ProGlassAutomation.Views.Settings.Sections.Cards
{
    public partial class DemoFfmpegCard : UserControl
    {
        public DemoFfmpegCard()
        {
            InitializeComponent();
        }

        private void OpenInstaller_Click(object sender, RoutedEventArgs e)
        {
            ComponentInstallerWindow.Show(Window.GetWindow(this));
        }
    }
}
