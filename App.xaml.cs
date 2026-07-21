using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIConnectivity;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Database.DatabaseBackup;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
using ProGlassAutomation.Views.Splash;
using System;
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

        // Proforma Invoice — saved invoices (separate SQLite: proformainvoices.db)
        public static IProformaInvoiceRepository Invoices { get; private set; }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            // ══════════════════════════════════════════════════════════════
            //  TEMPORARY DIAGNOSTIC: Global exception handler
            //  Catches WPF dispatcher crashes (layout/render pass errors)
            //  that bypass normal try/catch blocks.
            //  Shows a MessageBox with full error details instead of
            //  silently crashing the app. Remove after debugging.
            // ══════════════════════════════════════════════════════════════
            DispatcherUnhandledException += (s, args) =>
            {
                string msg = $"UNHANDLED EXCEPTION:\n\n" +
                    $"Type: {args.Exception.GetType().Name}\n" +
                    $"Message: {args.Exception.Message}";

                if (args.Exception.InnerException != null)
                    msg += $"\n\nInner: {args.Exception.InnerException.Message}";

                string stack = args.Exception.StackTrace ?? "";
                if (stack.Length > 600) stack = stack.Substring(0, 600) + "...";
                msg += $"\n\nStack:\n{stack}";

                MessageBox.Show(msg, "App Crash — Debug", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true; // Prevent app from closing
            };

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

            // 4) Initialize Proforma Invoices database (separate proformainvoices.db)
            //    Stores saved invoices with full wizard-state snapshots
            //    Located at: %AppData%\ProGlassAutomation\proformainvoices.db
            Invoices = new ProformaInvoiceRepository();
            await Invoices.InitializeAsync();

            // 5) Initialize AI + Connectivity services
            Ai = new GeminiAiService(Config.Ai);
            Connectivity = new ConnectivityService();
            Connectivity.StartMonitoring();

            // 6) Show splash
            var splash = new SplashView();
            splash.Show();

            // 7) Simulate loading time (allows animations to play)
            //    Replace this with real init work later (DB connect, config load, etc.)
            await Task.Delay(2800);

            // 8) Create main window (but don't show yet)
            var mainWindow = new MainWindow();

            // 9) Fade out splash then show main window
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