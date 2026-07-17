using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIConnectivity;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Views.Splash;
using System.Threading.Tasks;
using System.Windows;

namespace ProGlassAutomation
{
    public partial class App : Application
    {
        // Global service instances (simple service locator pattern)
        public static AppConfig Config { get; private set; }
        public static IAiService Ai { get; private set; }
        public static IConnectivityService Connectivity { get; private set; }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            // 1) Load persisted config (%AppData%\ProGlassAutomation\config.json)
            Config = ConfigService.Load();

            // 2) Initialize AI + Connectivity services
            Ai = new GeminiAiService(Config.Ai);
            Connectivity = new ConnectivityService();
            Connectivity.StartMonitoring();

            // 3) Show splash
            var splash = new SplashView();
            splash.Show();

            // 4) Simulate loading time (allows animations to play)
            //    Replace this with real init work later (DB connect, config load, etc.)
            await Task.Delay(2800);

            // 5) Create main window (but don't show yet)
            var mainWindow = new MainWindow();

            // 6) Fade out splash then show main window
            splash.FadeOutAndClose(() =>
            {
                mainWindow.Show();
                MainWindow = mainWindow;
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Persist config + cleanup
            if (Config != null) ConfigService.Save(Config);
            Connectivity?.StopMonitoring();
            base.OnExit(e);
        }
    }
}