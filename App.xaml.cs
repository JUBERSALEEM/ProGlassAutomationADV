using ProGlassAutomation.Views.Splash;
using System.Threading.Tasks;
using System.Windows;

namespace ProGlassAutomation
{
    public partial class App : Application
    {
        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            // 1) Show splash
            var splash = new SplashView();
            splash.Show();

            // 2) Simulate loading time (allows animations to play)
            //    Replace this with real init work later (DB connect, config load, etc.)
            await Task.Delay(2800);

            // 3) Create main window (but don't show yet)
            var mainWindow = new MainWindow();

            // 4) Fade out splash then show main window
            splash.FadeOutAndClose(() =>
            {
                mainWindow.Show();
                MainWindow = mainWindow;
            });
        }
    }
}