// ═══════════════════════════════════════════════════════════════════════
// FILE: App.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Application startup (Chat 25 v4.0 — non-blocking Whisper init)
//
// CHANGES vs Chat 25 v1.2:
//   ✓ FIX CRITICAL (STARTUP HANG): WhisperSpeechService constructor
//     internally calls new AudioCaptureService() which invokes
//     NAudio's WaveInEvent.DeviceCount. On some Windows machines
//     (broken audio drivers, USB audio, Bluetooth issues, Realtek
//     bugs) this call blocks the UI thread indefinitely.
//     
//     Previous behavior: App startup HANGS at step 10b if Whisper
//     model file exists. User must delete the model to recover.
//     
//     New behavior: Whisper initialization runs on background thread
//     with 10-second timeout. If it hangs, times out, or crashes,
//     app automatically falls back to System.Speech and continues.
//     App startup NEVER blocks on audio device enumeration.
//   
//   ✓ System.Speech fallback also wrapped in try/catch as before.
//   
//   ✓ Debug output shows exactly which path was taken so user can
//     diagnose STT selection.
//   
//   ✓ All other startup steps preserved in exact order.
//   ✓ All existing properties, events, methods preserved.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIConnectivity;
using ProGlassAutomation.Services.AI.AIMemory;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Database.DatabaseBackup;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Network;
using ProGlassAutomation.Services.Performance;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Services.OrderReport.Repository;
using ProGlassAutomation.Services.Dashboard.Core;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using ProGlassAutomation.Views.Splash;
using ProGlassAutomation.Behaviors;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.InteropServices;
using Velopack;
using System.Threading;
using System.Threading.Tasks;
using ProGlassAutomation.Services.Dialogs;
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

        public static IConnectivityService Connectivity { get; private set; }

        /// <summary>
        /// Speech-to-Text service.
        /// Public set: SettingsViewModel swaps this after model
        /// download/delete without requiring app restart.
        /// May be null if all initialization attempts fail.
        /// </summary>
        public static ISpeechToTextService SpeechToText { get; set; }

        /// <summary>Text-to-Speech — Windows System.Speech. Nullable.</summary>
        public static ITextToSpeechService TextToSpeech { get; private set; }

        public static ISpecificationsRepository Specs { get; private set; }
        public static IInquiryRepository Inquiries { get; private set; }
        public static IProformaInvoiceRepository Invoices { get; private set; }
        public static IJobOrderRepository Jobs { get; private set; }
        public static IDeliveryNoteRepository DeliveryNotes { get; private set; }
        public static IMasterDataRepository MasterData { get; private set; }
        public static ITaxInvoiceRepository TaxInvoices { get; private set; }
        public static Services.CreditNote.Repository.ICreditNoteRepository CreditNotes { get; private set; }
        public static IStatementRepository Statements { get; private set; }
        public static IOrderReportRepository OrderReports { get; private set; }

        /// <summary>
        /// Central reporting / data-intelligence layer.
        /// Single source of truth for all Reports modules and the AI
        /// reporting tool (GetReportingTool). Never fabricates.
        /// </summary>
        public static IReportingDataService Reporting { get; private set; }
        public static Services.Workflow.IStatusService Status { get; private set; } = null!;
        public static Services.Dialogs.IDialogService Dialogs { get; private set; } = null!;

        /// <summary>
        /// DI composition root — built once at startup. New code must
        /// resolve services via constructor injection; legacy App.* statics
        /// remain for migration. Tests can build their own provider with
        /// fakes via ServiceRegistration.BuildProGlassProvider().
        /// </summary>
        public static System.IServiceProvider ServiceProvider { get; private set; } = null!;

        // ── Dashboard service layer ──────────────────────────────────────
        //
        // The dashboards used to call repository GetAllAsync() and
        // aggregate in memory: opening Overview loaded four entire tables,
        // including the FullSnapshotJson blob on every proforma row, just
        // to produce a few counts and sums.
        //
        // DashboardMetrics runs projected AsNoTracking queries that read
        // only the columns each figure needs. DashboardFilter holds the
        // one selected date range so widgets stop inventing their own.

        /// <summary>Aggregated, projected dashboard queries.</summary>
        public static IDashboardMetricsService DashboardMetrics { get; private set; }

        /// <summary>Shared dashboard date-range state.</summary>
        public static IDashboardFilterService DashboardFilter { get; private set; }

        /// <summary>
        /// Same-network user presence (Users page shows colleagues on
        /// the LAN). Created during startup, disposed on exit.
        /// </summary>
        public static UserPresenceService Presence { get; private set; }

        // ══════════════════════════════════════════════════════════════
        //  CS0017 — one Main only.
        //  App.xaml stays ApplicationDefinition so WPF emits Main and
        //  InitializeComponent. Do not declare Main in this file (a
        //  second Main is CS0017 when the generated one is also present).
        //  Do not reclassify App.xaml as Page (CS1061 InitializeComponent).
        //  Velopack runs at the start of Application_Startup.
        // ══════════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════════
        //  STARTUP — CRASH-GUARDED ENTRY POINT  (audit finding C-2)
        //
        //  THE PROBLEM THIS SOLVES
        //  Application_Startup is `async void`, which is unavoidable for a
        //  WPF Startup handler. But an exception thrown AFTER the first
        //  `await` inside an async void method is posted to the
        //  SynchronizationContext and does NOT pass through
        //  DispatcherUnhandledException — the handler installed a few
        //  lines below could never see it. The nine database
        //  initialisations run inside a `Task.WhenAll` with no try/catch,
        //  so a corrupt, locked, or read-only SQLite file produced a bare
        //  OS-level crash at launch: no log entry, no error ID, no
        //  message the user could act on, and no way to diagnose it
        //  remotely.
        //
        //  THE FIX
        //  The entire original body moved unchanged into
        //  StartupCoreAsync(). This wrapper awaits it inside a try/catch
        //  so any startup fault is logged, written to the crash report,
        //  and shown to the user in the same professional format used by
        //  the dispatcher handler. Startup logic itself is untouched —
        //  this only adds the safety net around it.
        // ══════════════════════════════════════════════════════════════
        private async void Application_Startup(
            object sender, StartupEventArgs e)
        {
            try
            {
                // Dev bin/Debug: no-op. Installed package: install/update hooks.
                Velopack.VelopackApp.Build().Run();
                await StartupCoreAsync(sender, e);
            }
            catch (Exception ex)
            {
                HandleFatalStartupFailure(ex);
            }
        }

        /// <summary>
        /// Last-resort startup failure path. Logs, writes the crash report
        /// and tells the user what actually happened, instead of letting
        /// the process die silently.
        /// </summary>
        private void HandleFatalStartupFailure(Exception ex)
        {
            string errorId;

            try
            {
                errorId = AppLogService.Critical(
                    "Fatal error during application startup", ex,
                    new LogContext { Operation = "Startup" });

                WriteCrashReport(errorId, ex);
            }
            catch
            {
                // Logging itself failed (disk full, permissions). Still
                // show the user something rather than vanishing.
                errorId = "UNLOGGED";
            }

            try
            {
                Prompts.Show(
                    "ProGlass could not finish starting up.\n\n" +
                    $"Error ID: {errorId}\n" +
                    $"Type:    {ex.GetType().Name}\n" +
                    $"Message: {ex.Message}\n\n" +
                    "Your data has not been changed. This most often " +
                    "means a database file is missing, locked by another " +
                    "copy of the app, or read-only.\n\n" +
                    "Technical details were saved to:\n" +
                    $"{CrashReportPath}",
                    "ProGlass — Startup Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // No UI available — nothing further we can do.
            }

            Shutdown(1);
        }

        /// </summary>


        protected override void OnExit(ExitEventArgs e)
        {
            // FIRST: write any debounced settings change.
            //
            // Settings toggles now queue their save (ConfigService
            // .SaveDebounced) instead of blocking the UI thread on disk
            // I/O. If the user flips a switch and closes the app inside
            // the debounce window, the timer never fires -- so the flush
            // must happen here, before anything else tears down, or that
            // change is silently lost. Non-throwing by contract.
            try
            {
                Services.AI.AIConfig.ConfigService.FlushPending();
            }
            catch (System.Exception ex)
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug(
                    "[App] OnExit config flush failed: " + ex.Message);
            }

            // Phase 4: Detach static events — no leak
            try { AuditService.CurrentUserChanged -= OnCurrentUserChanged; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[App] OnExit event-detach failed: " + ex.Message); }

            // Ledger: unsubscribe from invoice writes (cancels any pending
            // debounced pass so shutdown never waits on it).
            try { Services.Specifications.SpecificationLedgerService.Stop(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[App] OnExit ledger stop failed: " + ex.Message); }

            // Record shutdown in the audit trail (bounded wait — the
            // app must still exit promptly even if the DB is locked).
            try
            {
                AuditService.LogSessionBoundary(login: false);
                AuditService.LogAsync(
                    AuditSections.App, "App closed", "").Wait(1500);
            }
            catch { /* exit must never be blocked by audit errors */ }

            AppLogService.Info("Shutdown — creating backup set",
                new LogContext { Operation = "Shutdown" });

            BackupService.CreateBackup();
            if (Config != null) ConfigService.Save(Config);

            // M-3: StopMonitoring only stops the poll timer. The service
            // also holds a hook on the static
            // NetworkChange.NetworkAvailabilityChanged event, so it must be
            // disposed to actually release it.
            Connectivity?.StopMonitoring();
            (Connectivity as IDisposable)?.Dispose();

            Presence?.Dispose();
            SpeechToText?.Dispose();
            TextToSpeech?.Dispose();

            // LAST act of a clean shutdown: remove the "session is open"
            // marker. If the process dies hard (StackOverflowException,
            // native fault, Task Manager kill) this never runs, and the next
            // startup logs that the previous session crashed. Keep it after
            // every flush above so a failure mid-shutdown still reads as
            // unclean on the next launch.
            AppLogService.MarkCleanShutdown();

            base.OnExit(e);
        }
    }
}