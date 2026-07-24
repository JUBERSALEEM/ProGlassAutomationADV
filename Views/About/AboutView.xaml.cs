using ProGlassAutomation.ViewModels.About;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.About
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            InitializeComponent();
        }

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