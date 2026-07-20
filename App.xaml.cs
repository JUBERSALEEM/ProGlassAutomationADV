using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIConnectivity;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Database.DatabaseBackup;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
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

        // Proforma Invoice — spec library (separate SQLite: specifications.db)
        public static ISpecificationsRepository Specs { get; private set; }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            // 1) Load persisted config (%AppData%\ProGlassAutomation\config.json)
            Config = ConfigService.Load();

            // 2) Initialize main database (creates proglass.db + tables on first run)
            //    Located at: %AppData%\ProGlassAutomation\proglass.db
            DatabaseInitializer.Initialize();

            // 3) Initialize Specifications database (separate specifications.db)
            //    Stores reusable glass specs generated from Step 4 for use in Step 5
            //    Located at: %AppData%\ProGlassAutomation\specifications.db
            Specs = new SpecificationsRepository();
            await Specs.InitializeAsync();

            // 4) Initialize AI + Connectivity services
            Ai = new GeminiAiService(Config.Ai);
            Connectivity = new ConnectivityService();
            Connectivity.StartMonitoring();

            // 5) Show splash
            var splash = new SplashView();
            splash.Show();

            // 6) Simulate loading time (allows animations to play)
            //    Replace this with real init work later (DB connect, config load, etc.)
            await Task.Delay(2800);

            // 7) Create main window (but don't show yet)
            var mainWindow = new MainWindow();

            // 8) Fade out splash then show main window
            splash.FadeOutAndClose(() =>
            {
                mainWindow.Show();
                MainWindow = mainWindow;
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Backup database before closing (keeps last 7 copies)
            BackupService.CreateBackup();

            // Persist config + cleanup
            if (Config != null) ConfigService.Save(Config);
            Connectivity?.StopMonitoring();
            base.OnExit(e);
        }
    }
}