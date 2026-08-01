// ═══════════════════════════════════════════════════════════════════════
// FILE: App.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Application startup (Chat 25 v1.2 — SpeechToText public set)
//
// CHANGES vs Chat 24 v1.3:
//   ✓ FIX CS0272: SpeechToText { get; private set; } → { get; set; }
//     SettingsViewModel swaps STT service after model download/delete.
//     Matches pattern of App.Ai which is already public set.
//   ✓ FIX CS0428: WhisperModelManager.IsModelDownloaded resolves as
//     bool property (fixed in WhisperModelManager.cs).
//   ✓ Added App.AiProviderChanged static event for live provider swap.
//   ✓ AI Enabled defaults to true in AiSettings — no change needed here.
//   ✓ All startup steps preserved in exact order.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIConnectivity;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
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
        // ── Global service instances ──────────────────────────────────

        public static AppConfig Config { get; private set; }

        /// <summary>
        /// Active AI service — public set allows runtime provider swap
        /// from SettingsViewModel without app restart.
        /// </summary>
        public static IAiService Ai { get; set; }

        /// <summary>
        /// Raised when SettingsViewModel swaps the active AI provider.
        /// AiChatViewModel subscribes to refresh its provider display.
        /// </summary>
        public static event EventHandler AiProviderChanged;

        /// <summary>Raises AiProviderChanged so all subscribers refresh.</summary>
        public static void RaiseAiProviderChanged() =>
            AiProviderChanged?.Invoke(null, EventArgs.Empty);

        public static IConnectivityService Connectivity { get; private set; }

        /// <summary>
        /// Speech-to-Text service.
        /// ─────────────────────────────────────────────────────────────
        /// FIX CS0272: Changed from { get; private set; } to { get; set; }
        /// SettingsViewModel needs to swap this after model download:
        ///   • Download complete → swap System.Speech → WhisperSpeechService
        ///   • Model deleted    → swap WhisperSpeechService → System.Speech
        /// Pattern matches App.Ai which is already public set.
        /// ─────────────────────────────────────────────────────────────
        /// </summary>
        public static ISpeechToTextService SpeechToText { get; set; }

        /// <summary>Text-to-Speech — Windows System.Speech. Nullable.</summary>
        public static ITextToSpeechService TextToSpeech { get; private set; }

        public static ISpecificationsRepository Specs { get; private set; }
        public static IProformaInvoiceRepository Invoices { get; private set; }
        public static IJobOrderRepository Jobs { get; private set; }
        public static IDeliveryNoteRepository DeliveryNotes { get; private set; }
        public static IMasterDataRepository MasterData { get; private set; }
        public static ITaxInvoiceRepository TaxInvoices { get; private set; }
        public static IStatementRepository Statements { get; private set; }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            // ── Global unhandled exception catcher (debug) ────────────
            DispatcherUnhandledException += (s, args) =>
            {
                string msg =
                    $"UNHANDLED EXCEPTION:\n\n" +
                    $"Type:    {args.Exception.GetType().Name}\n" +
                    $"Message: {args.Exception.Message}";

                if (args.Exception.InnerException != null)
                    msg += $"\n\nInner: {args.Exception.InnerException.Message}";

                string stack = args.Exception.StackTrace ?? "";
                if (stack.Length > 600) stack = stack[..600] + "...";
                msg += $"\n\nStack:\n{stack}";

                MessageBox.Show(msg, "App Crash — Debug",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            // 1) Load persisted config
            Config = ConfigService.Load();

            // 2) Main database
            DatabaseInitializer.Initialize();

            // 3) Specifications
            Specs = new SpecificationsRepository();
            await Specs.InitializeAsync();

            // 4) Proforma Invoices
            Invoices = new ProformaInvoiceRepository();
            await Invoices.InitializeAsync();

            // 5) Job Orders
            Jobs = new JobOrderRepository();
            await Jobs.InitializeAsync();

            // 6) Delivery Notes
            DeliveryNotes = new DeliveryNoteRepository();
            await DeliveryNotes.InitializeAsync();

            // 7) Master Data
            MasterData = new MasterDataRepository();
            await MasterData.InitializeAsync();

            // 8) Tax Invoices
            TaxInvoices = new TaxInvoiceRepository();
            await TaxInvoices.InitializeAsync();

            // 9) Statements
            Statements = new StatementRepository();
            await Statements.InitializeAsync();

            // 10) AI via factory (multi-provider, reads ActiveProvider from config)
            Ai = AiProviderFactory.CreateActive(Config.Ai);

            System.Diagnostics.Debug.WriteLine(
                $"[App] AI: {Ai?.ProviderName} | " +
                $"Enabled: {Config.Ai.Enabled} | " +
                $"Configured: {Ai?.IsConfigured}");

            // 10b) Speech-to-Text
            //      Priority: Whisper (if model downloaded) → System.Speech fallback
            try
            {
                if (WhisperModelManager.IsModelDownloaded)
                {
                    SpeechToText = new WhisperSpeechService();
                    System.Diagnostics.Debug.WriteLine(
                        $"[App] STT: Whisper " +
                        $"({WhisperModelManager.SelectedModelInfo.DisplayName})");
                }
                else
                {
                    SpeechToText = new SpeechToTextService();
                    System.Diagnostics.Debug.WriteLine(
                        "[App] STT: System.Speech fallback. " +
                        "Download Whisper model in Settings for better accuracy.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[App] STT init failed: {ex.Message}");
                SpeechToText = null;
            }

            // 10c) Text-to-Speech
            try { TextToSpeech = new TextToSpeechService(); }
            catch { TextToSpeech = null; }

            // 11) Connectivity monitoring
            Connectivity = new ConnectivityService();
            Connectivity.StartMonitoring();

            // 12) Splash screen
            var splash = new SplashView();
            splash.Show();

            // 13) Loading delay for splash animations
            await Task.Delay(2800);

            // 14) Main window handoff
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;

            splash.FadeOutAndClose(() =>
            {
                mainWindow.Show();
                mainWindow.Activate();
                mainWindow.Focus();
                mainWindow.Topmost = true;
                mainWindow.Topmost = false;
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            BackupService.CreateBackup();
            if (Config != null) ConfigService.Save(Config);
            Connectivity?.StopMonitoring();
            SpeechToText?.Dispose();
            TextToSpeech?.Dispose();
            base.OnExit(e);
        }
    }
}