// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/About/AboutView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: About — tabbed shell (v5)
//
// The shell hosts the hero + tab strip and switches between the three
// section UserControls. The email contact handler lives in
// Sections/DeveloperSection.xaml.cs so that section stays self-contained.
//
// 2026-08-25 SENIOR HARDENING: construction is now wrapped. If ANY part
// of the About XAML fails to load on a given machine (resource lookup,
// font, image, section ctor), the app must NOT crash and the page must
// NOT render transparent/empty: the exact exception is shown on the
// page itself and written to the app log, so the real cause is finally
// visible instead of a freeze + crash dialog.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProGlassAutomation.Views.About
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            try
            {
                InitializeComponent();
            }
            catch (System.Exception ex)
            {
                try
                {
                    ProGlassAutomation.Services.Logging.AppLogService.Critical(
                        "AboutView failed to load", ex,
                        new ProGlassAutomation.Services.Logging.LogContext
                        {
                            Module = "About",
                            Operation = "Load",
                        });
                }
                catch { /* logging must never throw */ }

                try { Content = BuildFailurePanel(ex); }
                catch { /* the fallback itself must never throw */ }
            }
        }

        /// <summary>
        /// Diagnostic surface shown when the About XAML cannot load.
        /// Opaque stage background (never transparent) + the exact error.
        /// </summary>
        private static Border BuildFailurePanel(System.Exception ex)
        {
            var title = new TextBlock
            {
                Text = "About page could not be loaded",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("OnAccentForegroundBrush"),
                Margin = new Thickness(36, 44, 36, 10),
            };

            var detail = new TextBlock
            {
                Text = ex.GetType().Name + ": " + ExceptionMessages.Deep(ex) +
                       "\n\nThe exact error is shown here and in the app log " +
                       "(Help → Diagnostics). Nothing else in the app is affected.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("OnAccentForegroundBrush"),
                Opacity = 0.85,
                Margin = new Thickness(36, 0, 36, 36),
            };

            var panel = new StackPanel();
            panel.Children.Add(title);
            panel.Children.Add(detail);

            var bg = (System.Windows.Media.Brush)Application.Current.FindResource("ModuleBackgroundBrush");

            return new Border
            {
                Background = bg,
                Child = panel,
            };
        }
    }
}
