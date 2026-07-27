// ═══════════════════════════════════════════════════════════════════════
// FILE: App.xaml.cs
// PROJECT: ProGlassAutomation
//
// CHAT 16: Added IStatementRepository + StatementRepository registration.
//          Initialized after TaxInvoices (step 9).
//          All other code UNCHANGED.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIConnectivity;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Database.DatabaseBackup;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using ProGlassAutomation.Views.Splash;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ProGlassAutomation
{
    public partial class App : Application
    {
        // ── Global service instances (simple service locator pattern) ─────

        public static AppConfig Config { get; private set; }
        public static IAiService Ai { get; private set; }
        public static IConnectivityService Connectivity { get; private set; }

        // Proforma Invoice — spec library (separate SQLite: specifications.db)
        public static ISpecificationsRepository Specs { get; private set; }

        // Proforma Invoice — saved invoices (separate SQLite: proformainvoices.db)
        public static IProformaInvoiceRepository Invoices { get; private set; }

        // Job Order — saved job orders (separate SQLite: joborders.db)
        public static IJobOrderRepository Jobs { get; private set; }

        // Delivery Notes — saved delivery notes (separate SQLite: deliverynotes.db)
        public static IDeliveryNoteRepository DeliveryNotes { get; private set; }

        // Master Data — companies, clients, projects, salesmen (separate SQLite: masterdata.db)
        public static IMasterDataRepository MasterData { get; private set; }

        /// <summary>
        /// Tax Invoices — saved tax invoices (separate SQLite: taxinvoices.db)
        /// Added in Chat 14 (Tax Invoice module).
        /// Initialized after DeliveryNotes in Application_Startup.
        /// </summary>
        public static ITaxInvoiceRepository TaxInvoices { get; private set; }

        /// <summary>
        /// Statements — saved account statements (separate SQLite: statements.db)
        /// Added in Chat 16 (Sales Report / Account Statement module).
        /// Initialized after TaxInvoices in Application_Startup.
        /// </summary>
        public static IStatementRepository Statements { get; private set; }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            // ══════════════════════════════════════════════════════════════
            //  TEMPORARY DIAGNOSTIC: Global exception handler
            //  Catches WPF dispatcher crashes (layout/render pass errors)
            //  that bypass normal try/catch blocks.
            // ══════════════════════════════════════════════════════════════
            DispatcherUnhandledException += (s, args) =>
            {
                string msg =
                    $"UNHANDLED EXCEPTION:\n\n" +
                    $"Type: {args.Exception.GetType().Name}\n" +
                    $"Message: {args.Exception.Message}";

                if (args.Exception.InnerException != null)
                    msg += $"\n\nInner: {args.Exception.InnerException.Message}";

                string stack = args.Exception.StackTrace ?? "";
                if (stack.Length > 600)
                    stack = stack.Substring(0, 600) + "...";
                msg += $"\n\nStack:\n{stack}";

                MessageBox.Show(msg, "App Crash — Debug",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

            // 5) Initialize Job Orders database (joborders.db)
            Jobs = new JobOrderRepository();
            await Jobs.InitializeAsync();

            // 6) Initialize Delivery Notes database (deliverynotes.db)
            DeliveryNotes = new DeliveryNoteRepository();
            await DeliveryNotes.InitializeAsync();

            // 7) Initialize Master Data database (masterdata.db)
            //    Companies, Clients, Projects, Salesmen — auto-populated
            //    from Proforma Invoice saves. Powers card auto-fill and
            //    salesman dropdown across the app.
            MasterData = new MasterDataRepository();
            await MasterData.InitializeAsync();

            // 8) Initialize Tax Invoices database (taxinvoices.db)
            //    Chat 14 — Tax Invoice module.
            //    Initialized after DeliveryNotes (Tax Invoices reference DNs).
            TaxInvoices = new TaxInvoiceRepository();
            await TaxInvoices.InitializeAsync();

            // 9) Initialize Statements database (statements.db)
            //    Chat 16 — Sales Report / Account Statement module.
            //    Initialized after TaxInvoices (statements aggregate invoices).
            Statements = new StatementRepository();
            await Statements.InitializeAsync();

            // 10) Initialize AI + Connectivity services
            Ai = new GeminiAiService(Config.Ai);
            Connectivity = new ConnectivityService();
            Connectivity.StartMonitoring();

            // 11) Show splash
            var splash = new SplashView();
            splash.Show();

            // 12) Loading time for splash animations
            await Task.Delay(2800);

            // 13) Create main window (not shown yet)
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
                mainWindow.Topmost = false; // immediately release
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