// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/About/Sections/DeveloperSection.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: About → "Developer" tab
//
// Owns the email contact click handler (moved here from AboutView so the
// Developer section is self-contained and editable on its own).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.About;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.About.Sections
{
    public partial class DeveloperSection : UserControl
    {
        public DeveloperSection()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Opens the system's default mailto handler with the developer's
        /// address. Silently fails if no handler is registered (Rule E01).
        /// </summary>
        private void Email_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AboutViewModel vm)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"mailto:{vm.Email}",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // mailto handler unavailable — silent fail (Rule E01 with reason)
                }
            }
        }
    }
}
