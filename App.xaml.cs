using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIConnectivity;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Database.DatabaseBackup;
using ProGlassAutomation.Services.JobOrder.Repository;
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

        // Job Order — saved job orders (separate SQLite: joborders.db) — Phase 9
        public static IJobOrderRepository Jobs { get; private set; }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            // ══════════════════════════════════════════════════════════════
            //  TEMPORARY DIAGNOSTIC: Global exception handler
            //  Catches WPF dispatcher crashes (layout/render pass errors)
            //  that bypass normal try/catch blocks.
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
                args.Handled = true;
            };

            // 1) Load persisted config (%AppData%\ProGlassAutomation\config.json)
            Config = ConfigService.Load();

            // 2) Initialize main database (proglass.db)
            DatabaseInitializer.Initialize();

            // 3) Initialize Specifications database (specifications.db)
            Specs = new SpecificationsRepository();
            await Specs.InitializeAsync();

            // 4) Initialize Proforma Invoices database (proformainvoices.db)
            Invoices = new ProformaInvoiceRepository();
            await Invoices.InitializeAsync();

            // 5) Phase 9 — Initialize Job Orders database (joborders.db)
            Jobs = new JobOrderRepository();
            await Jobs.InitializeAsync();

            // 6) Initialize AI + Connectivity services
            Ai = new GeminiAiService(Config.Ai);
            Connectivity = new ConnectivityService();
            Connectivity.StartMonitoring();

            // 7) Show splash
            var splash = new SplashView();
            splash.Show();

            // 8) Loading time for splash animations
            await Task.Delay(2800);

            // 9) Create main window (not shown yet)
            var mainWindow = new MainWindow();

            // ══════════════════════════════════════════════════════════════
            //  Phase 8.2c — Proper splash-to-main handoff
            //  1. Register main window as app's primary BEFORE showing
            //  2. Splash fades out (releases Topmost internally)
            //  3. On fade complete: show + activate + focus main window,
            //     then close splash
            //  Prevents the main window from opening BEHIND the splash
            //  and requiring a taskbar click to bring to foreground.
            // ══════════════════════════════════════════════════════════════
            MainWindow = mainWindow;

            splash.FadeOutAndClose(() =>
            {
                mainWindow.Show();
                mainWindow.Activate();      // bring to foreground
                mainWindow.Focus();         // grab keyboard focus
                mainWindow.Topmost = true;  // force above splash residue
                mainWindow.Topmost = false; // immediately release so other apps can go on top
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