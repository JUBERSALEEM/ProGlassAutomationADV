// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/About/AboutView.xaml.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Views.About
// VERSION: Chat 23 v2.9 — Handler unchanged, wired to centered email pill
//
// PRESERVED (verbatim logic):
//   • Email_MouseLeftButtonDown handler
//   • DataContext AboutViewModel cast
//   • Silent-fail mailto (Rule E01)
//
// WIRING NOTE:
//   MouseLeftButtonDown is attached to the centered Email PILL (Border)
//   in the hero card. In v2.9, the pill lives inside the middle column
//   of the new triptych layout (LeftPillars | HeroContent | RightPillars).
//   Handler signature and behavior identical across v2.5–v2.9.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.About;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.About
{
    /// <summary>
    /// Code-behind for the About view.
    /// Only owns the email pill click handler — all other logic lives
    /// in <see cref="AboutViewModel"/> (strict MVVM).
    /// </summary>
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles left-click on the centered email pill in the hero card.
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