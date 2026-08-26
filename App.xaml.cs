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
        public static IStatementRepository Statements { get; private set; }

        /// <summary>
        /// Central reporting / data-intelligence layer.
        /// Single source of truth for all Reports modules and the AI
        /// reporting tool (GetReportingTool). Never fabricates.
        /// </summary>
        public static IReportingDataService Reporting { get; private set; }

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
        //  ENTRY POINT — Velopack bootstrap BEFORE anything else
        //  (audit fix for vpk warning: "VelopackApp.Run() was found
        //  in method StartupCoreAsync.MoveNext(), which does not look
        //  like your application's entry point. It is strongly
        //  recommended that you move this to the very beginning of
        //  your Main() method.")
        //
        //  Velopack's Run() must be the FIRST line of the process entry
        //  point so that:
        //    • During install / update / uninstall hooks, the
        //      fast-exit arguments are handled before any WPF / DB
        //      / DI startup cost is paid.
        //    • During a normal launch, Run() returns immediately and
        //      the rest of Main() runs as before.
        //    • A pending update can re-launch the new version with
        //      the correct working directory and command-line args
        //      before the rest of the WPF process state is built.
        //
        //  WPF normally generates Main() automatically from the
        //  App.xaml ApplicationDefinition. To put our own Main in
        //  front of it, App.xaml's Build Action is set to "Page"
        //  (see ProGlassAutomation.csproj) and the Startup event
        //  handler below still wires Application_Startup → the same
        //  crash-guarded StartupCoreAsync() as before. No behavioural
        //  change other than the Velopack hook running first.
        // ══════════════════════════════════════════════════════════════
        [STAThread]
        public static void Main(string[] args)
        {
            // 1) Velopack hook — must be first, before any other
            //    startup work. In dev mode (running from
            //    bin/Debug without an installed package) this is a
            //    no-op; in a real install it handles pending
            //    updates, first-run callbacks, and uninstall hooks.
            Velopack.VelopackApp.Build().Run();

            // 2) Hand off to the normal WPF Application bootstrap.
            //    app.InitializeComponent() loads App.xaml (and
            //    wires the Startup="Application_Startup" handler
            //    that runs StartupCoreAsync); app.Run() starts
            //    the dispatcher loop.
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }

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
                MessageBox.Show(
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

        private async Task StartupCoreAsync(
            object sender, StartupEventArgs e)
        {
            // Velopack bootstrap was moved to Main() so it runs
            // as the very first line of the process entry point
            // (suppresses the vpk "Run() was not found in the
            // entry point" warning, and lets install / update
            // / uninstall hooks exit before any WPF startup
            // cost). The hook is a no-op when the app is run
            // from a dev bin/Debug folder, so behaviour in
            // development is unchanged.

            // Work-area caps + Ctrl+Wheel zoom. Does not Viewbox-lock
            // the shell — layouts reflow in native pixels.
            ViewportFitBehavior.Install();

            // 0) Technical logging first — everything after can report here.
            AppLogService.Initialize();

            // ── Global unhandled exception catcher ──────────────────────
            // Writes a FULL crash report (logs\crash-latest.txt) and shows
            // a user-safe dialog with the ErrorId, so any crash can be
            // diagnosed without guessing.
            DispatcherUnhandledException += (s, args) =>
            {
                // ── A-1: authorization denials are NOT crashes ─────────
                // OperationGuard throws PermissionDeniedException from the
                // service layer. That is an expected, designed outcome —
                // the security boundary doing its job — not a fault. It
                // must not produce a crash report or a scary "Something
                // went wrong" dialog telling the user to contact support.
                // Show the precise, already-sanitised explanation instead.
                // The denial itself was audited inside OperationGuard.
                if (args.Exception is Services.Audit.PermissionDeniedException denied)
                {
                    AppLogService.Warning(
                        $"Blocked unauthorized operation: {denied.Operation} " +
                        $"(needs {denied.Permission})",
                        new LogContext
                        {
                            Module = "Security",
                            Operation = "PermissionDenied"
                        });

                    MessageBox.Show(
                        denied.UserMessage, "Permission Denied",
                        MessageBoxButton.OK, MessageBoxImage.Warning);

                    args.Handled = true;
                    return;
                }

                string errorId = AppLogService.Critical(
                    "Unhandled UI exception", args.Exception,
                    new LogContext { Operation = "UnhandledException" });

                string crashPath = WriteCrashReport(errorId, args.Exception);

                // Show the rich crash dialog instead of
                // a plain MessageBox. The dialog has
                // buttons for Restart, Copy Error ID,
                // Open Crash Folder, and Close.
                try
                {
                    var dialog = new ProGlassAutomation.Views
                        .CrashReports.CrashReportDialog(
                            errorId,
                            crashPath,
                            args.Exception.GetType().Name,
                            args.Exception.Message ?? "");
                    dialog.ShowDialog();
                }
                catch
                {
                    // Dialog itself must never crash.
                }
                args.Handled = true;
            };

            // ── Non-UI-thread crashes (audit finding X-3) ───────────────
            // DispatcherUnhandledException only covers the UI thread. An
            // exception on a Task.Run worker, a timer callback or any
            // background thread bypassed it entirely and killed the
            // process with no log line at all. This cannot mark the
            // exception handled — the CLR is already tearing down — but
            // it guarantees the crash is recorded and diagnosable.
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                try
                {
                    var ex = args.ExceptionObject as Exception
                             ?? new Exception(
                                 "Non-Exception throw: " +
                                 args.ExceptionObject);

                    string errorId = AppLogService.Critical(
                        args.IsTerminating
                            ? "Unhandled background exception (terminating)"
                            : "Unhandled background exception",
                        ex,
                        new LogContext
                        {
                            Operation = "UnhandledException"
                        });

                    WriteCrashReport(errorId, ex);
                }
                catch
                {
                    // Never throw from the last-chance handler.
                }
            };

            // ── Windows shutdown / logoff (audit finding X-6) ───────────
            // MainWindow.Closing does NOT run for a Windows shutdown or
            // sign-out; the session ends through this event instead. Same
            // protection, but the wording differs because refusing a
            // Windows shutdown is far more intrusive than refusing a
            // window close — so this only warns and never blocks by
            // default, letting the user decide.
            SessionEnding += (_, args) =>
            {
                try
                {
                    if (!Services.Safety.CriticalOperationTracker.IsBusy)
                        return;

                    var running = string.Join(
                        "\n   • ",
                        Services.Safety.CriticalOperationTracker
                            .ActiveOperations);

                    AppLogService.Warning(
                        $"Session ending ({args.ReasonSessionEnding}) " +
                        $"during: {running}",
                        new LogContext
                        {
                            Module = "Safety",
                            Operation = "SessionEnding"
                        });

                    var answer = MessageBox.Show(
                        "ProGlass is still finishing work that must not " +
                        "be interrupted:\n\n   • " + running + "\n\n" +
                        "Postpone shutdown so it can finish?",
                        "ProGlass — Operation In Progress",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning,
                        MessageBoxResult.Yes);

                    if (answer == MessageBoxResult.Yes)
                        args.Cancel = true;
                }
                catch
                {
                    // Never trap a Windows shutdown.
                }
            };

            // ── Faulted tasks nobody awaited (audit finding X-2) ────────
            // A Task that faults and is never awaited raises this when it
            // is finalised. Left unobserved these are invisible: a failed
            // fire-and-forget save looks like success. Observing the
            // exception also prevents the process-level escalation that
            // some configurations still apply.
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                try
                {
                    string unobsId = AppLogService.Error(
                        "Unobserved task exception", args.Exception,
                        new LogContext
                        {
                            Operation = "UnobservedTaskException"
                        });

                    // Same crash-report file as the other
                    // exception sinks, so support has one
                    // place to look. The task is faulted but
                    // not yet finalised; this is the last
                    // chance to record it before the GC
                    // disposes the task object.
                    WriteCrashReport(unobsId, args.Exception);

                    // Mark observed: the failure is now recorded in the
                    // log + crash report, so escalating it to a process
                    // kill would lose more information than it preserves.
                    args.SetObserved();
                }
                catch
                {
                    // Never throw from the last-chance handler.
                }
            };

            AppLogService.Info(
                $"ProGlass {Services.Updates.AppInfo.Version} starting",
                new LogContext { Operation = "Startup" });

            // 1) Load persisted config
            Config = ConfigService.Load();

            // 2) Main database
            DatabaseInitializer.Initialize();

            // 3–9) Module databases.
            //      PERFORMANCE: every repository owns its own SQLite
            //      file and has no dependencies on the others, so they
            //      initialize IN PARALLEL — startup no longer pays nine
            //      sequential EnsureCreated round-trips.
            // ── DI composition root (audit fix: no DI → 311 static calls) ──
            // Build once, expose for constructor injection in new code.
            // Legacy App.* statics remain for migration; new ViewModels
            // (reporting hub etc.) resolve IReportingDataService via DI.
            try
            {
                ServiceProvider = Services.DependencyInjection.ServiceRegistration.BuildProGlassProvider();
                Specs = ServiceProvider.GetRequiredService<ISpecificationsRepository>();
                Inquiries = ServiceProvider.GetRequiredService<IInquiryRepository>();
                Invoices = ServiceProvider.GetRequiredService<IProformaInvoiceRepository>();
                Jobs = ServiceProvider.GetRequiredService<IJobOrderRepository>();
                DeliveryNotes = ServiceProvider.GetRequiredService<IDeliveryNoteRepository>();
                MasterData = ServiceProvider.GetRequiredService<IMasterDataRepository>();
                TaxInvoices = ServiceProvider.GetRequiredService<ITaxInvoiceRepository>();
                Statements = ServiceProvider.GetRequiredService<IStatementRepository>();
                Reporting = ServiceProvider.GetRequiredService<IReportingDataService>();
                DashboardMetrics = ServiceProvider.GetRequiredService<IDashboardMetricsService>();
                DashboardFilter = ServiceProvider.GetRequiredService<IDashboardFilterService>();
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"DI container build failed: {ex.Message}");
                // Fallback to manual construction (never break startup)
                Specs = new SpecificationsRepository();
                Inquiries = new InquiryRepository();
                Invoices = new ProformaInvoiceRepository();
                Jobs = new JobOrderRepository();
                DeliveryNotes = new DeliveryNoteRepository();
                MasterData = new MasterDataRepository();
                TaxInvoices = new TaxInvoiceRepository();
                Statements = new StatementRepository();
                Reporting = new ReportingDataService();
                DashboardMetrics = new DashboardMetricsService();
                DashboardFilter = new DashboardFilterService();
            }

            await Task.WhenAll(
                Specs.InitializeAsync(),
                Inquiries.InitializeAsync(),
                Invoices.InitializeAsync(),
                Jobs.InitializeAsync(),
                DeliveryNotes.InitializeAsync(),
                MasterData.InitializeAsync(),
                TaxInvoices.InitializeAsync(),
                Statements.InitializeAsync(),

                // 9b) Audit & user system — own DB (audit.db), safe in
                //     parallel. Never throws internally.
                AuditService.InitializeAsync(),

                // 9c-1) AI learning memory (aimemory.db)
                AiMemoryService.InitializeAsync());

            // 9c-2) Cross-database integrity sweep (Phase 2 — Result + Correlation):
            //       reconcile Tax Invoice → Delivery Note invoiced links on
            //       startup, so any link left broken by a crash / failed write
            //       is automatically healed. Runs in the background and never
            //       blocks startup; failures are logged with CorrelationId, never fatal.
            //       Now uses Result pattern: returns Result<int> with repaired count.
            ProGlassAutomation.Utilities.AsyncUtility.SafeFireAndForget(
                async () =>
                {
                    var corrId = ProGlassAutomation.Services.Common.CorrelationContext.NewIdFor("Reconciler", "Startup");
                    using var scope = ProGlassAutomation.Services.Common.CorrelationContext.BeginScope(corrId, null, "Reconciler");
                    var result = await ProGlassAutomation.Services.TaxInvoice.Repository
                        .TaxInvoiceDnReconciler.ReconcileWithResultAsync(corrId);
                    if (result.IsFailure)
                    {
                        AppLogService.Warning(
                            $"Reconciliation sweep failed [{corrId}]: {result.Error} (ErrorId={result.ErrorId})",
                            new LogContext
                            {
                                Module = "TaxInvoice",
                                Operation = "StartupReconcile",
                                CorrelationId = corrId
                            });
                    }
                    else if (result.Value > 0)
                    {
                        AppLogService.Info(
                            $"Reconciliation sweep healed {result.Value} DN link(s) [{corrId}]",
                            new LogContext
                            {
                                Module = "TaxInvoice",
                                Operation = "StartupReconcile",
                                CorrelationId = corrId,
                                Extra = $"Repaired={result.Value}"
                            });
                    }
                },
                "Tax-invoice → delivery-note reconciliation sweep failed");

            // 9c-2) Same-network user presence (Users page).
            // Start the service now, but do NOT broadcast an identity until
            // LoginWindow verifies a password. Starting as the seeded admin
            // made colleagues see "admin online" during the splash.
            // Phase 4: No lambda subscription to static event — named handler + detachable
            try
            {
                Presence = new UserPresenceService();
                Presence.Start("", "");

                AuditService.CurrentUserChanged += OnCurrentUserChanged;
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Presence service unavailable: {ex.Message}",
                    new LogContext { Module = "Network", Operation = "Startup" });
            }

            // 9c) Hardware-adaptive motion profile.
            //     Detects the display refresh rate and snaps to the
            //     nearest tier in 60/90/120/144/165 fps, then publishes
            //     MotionDuration* / TargetFps resources for the XAML.
            PerformanceProfileService.Apply();

            // 9d) Visual theme family + wallpaper
            //     (Normal / Monet / Professional / Glass / Glassy / Ocean).
            //
            //     The gradient-surface preference is restored FIRST:
            //     Monet's glass/window surfaces read it while building.
            ProGlassAutomation.Services.ThemeManager.ApplyGradientPreference(
                Config.Appearance?.UseGradientSurfaces ?? true);

            VisualThemeEngine.Apply(
                Config.Appearance?.VisualTheme ?? "Normal",
                Config.Appearance?.WallpaperPath ?? "",
                Config.Appearance?.WallpaperOpacity ?? 0.85);

            // 9e) Restore the persisted accent — preset palette key OR
            //     the free-pick custom hex — and re-derive the full
            //     accent family (hover/tint/contrast) from it so the UI
            //     keeps the colour between sessions.
            string persistedPreset = Config.Appearance?.AccentPreset ?? "GlassBlue";
            System.Windows.Media.Color? persistedAccent =
                persistedPreset == ProGlassAutomation.Services.Theme.AccentPresets.Custom
                    ? ProGlassAutomation.Services.Theme.AccentPresets.TryParseHex(
                        Config.Appearance?.CustomAccentHex)
                    : ProGlassAutomation.Services.Theme.AccentPresets.ColorFor(persistedPreset);
            ProGlassAutomation.Services.ThemeManager.ApplyAccent(
                persistedAccent,
                raiseEvent: false);

            // 9f) Restore the persisted card/pill outline choice. Must run
            //     AFTER ApplyAccent, because both write into the same live
            //     resource dictionary and this one must win for OutlineBrush.
            ProGlassAutomation.Services.ThemeManager.ApplyOutline(
                Config.Appearance?.UseDefinedOutline ?? false);

            // 9g) Restore the persisted global card border thickness so the
            //     saved Appearance > Border Thickness value applies on launch.
            ProGlassAutomation.Services.ThemeManager.ApplyCardBorderThickness(
                Config.Appearance?.CardBorderThickness ?? 1.5);

            // 9h) Restore the persisted card corner radius (default 12).
            ProGlassAutomation.Services.ThemeManager.ApplyCardCornerRadius(
                Config.Appearance?.CardCornerRadius ?? 12);

            // 9i) Restore the persisted global UI font (family + weight +
            //     style) via FontService. Imported fonts are re-registered
            //     for this session from the app Fonts folder.
            ProGlassAutomation.Services.Fonts.FontService.ApplySavedFont();

            string density = Config.Appearance?.DisplayDensity ?? "Compact";
            try
            {
                density = Services.Viewport.ViewportService.RecommendDensity(
                    density, SystemParameters.WorkArea.Height);
            }
            catch { /* design-time / no desktop */ }

            ProGlassAutomation.Services.ThemeManager.ApplyDisplayDensity(density);
            ProGlassAutomation.Services.ThemeManager.ApplyUiScale(
                Config.Appearance?.UiScale ?? 1.0);

            ProGlassAutomation.Services.Export.Pdf.DocumentStyle.Apply(
                Config.Appearance?.PdfLayoutId);

            AuditService.Log(
                AuditSections.App, "App started",
                $"Motion tier {PerformanceProfileService.TargetFps} fps");

            // 10) AI via factory (multi-provider)
            Ai = AiProviderFactory.CreateActive(Config.Ai);

            AppLogService.Debug(
                $"[App] AI: {Ai?.ProviderName} | " +
                $"Enabled: {Config.Ai.Enabled} | " +
                $"Configured: {Ai?.IsConfigured}");

            // ══════════════════════════════════════════════════════════
            //  10b) SPEECH-TO-TEXT — NON-BLOCKING INITIALIZATION
            //
            //  CRITICAL FIX: WhisperSpeechService internally creates
            //  AudioCaptureService which calls NAudio's
            //  WaveInEvent.DeviceCount. On machines with broken audio
            //  drivers, USB audio hot-plug issues, Bluetooth audio in
            //  bad state, or Realtek driver bugs, this call can HANG
            //  the UI thread indefinitely — freezing app startup.
            //
            //  Fix: Run Whisper init on background thread with 10s
            //  timeout. If it hangs or fails, fall back to System.Speech
            //  (which uses a different audio subsystem) or null.
            //  App startup continues in all cases.
            // ══════════════════════════════════════════════════════════
            SpeechToText = await InitializeSpeechToTextAsync();

            // 10c) Text-to-Speech
            //      Also wrap in try/catch — TTS init can rarely hang
            //      on machines with corrupted Windows Speech Platform
            try
            {
                TextToSpeech = await Task.Run(() =>
                {
                    try { return new TextToSpeechService(); }
                    catch (Exception ex)
                    {
                        AppLogService.Debug(
                            $"[App] TTS init failed: {ex.Message}");
                        return null;
                    }
                });
            }
            catch
            {
                TextToSpeech = null;
            }

            // 11) Connectivity monitoring
            Connectivity = new ConnectivityService();
            Connectivity.StartMonitoring();

            // Pin shutdown BEFORE the first window. Default OnLastWindowClose
            // would kill the process if splash closed during the delay or
            // during sign-in. Restored once MainWindow exists.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 11) License check (Phase 3, slice 4 of RELEASE_AUDIT.md).
            //     Commit 4 of the licensing refactor: the pre-login
            //     modal is GONE. The app now always boots into the
            //     main shell, regardless of license status. When
            //     the license is missing / expired, the main
            //     window shows a LicenseActivationView overlay
            //     that blocks the content area but leaves the
            //     shell (sidebar, top bar) usable. The user can
            //     navigate to About / Settings to inspect the
            //     status, but cannot do productive work until a
            //     license is installed.
            //
            //     The auto-trial (Commit 2) still fires inside
            //     LicenseService.Initialize() on first launch, so
            //     a customer with a real trial-key pair boots
            //     into a working app without any user action. A
            //     developer's machine with placeholder keys
            //     lands on the activation overlay; the License
            //     Generator tool + a real key pair fixes that.
            //
            //     LicenseService.Initialize() is still called so
            //     the auto-trial path can run, and so the
            //     LicenseStatusViewModel (used by the main
            //     window's overlay trigger) has a current value
            //     to bind against.
            // The license check is BEST-EFFORT: it must never block or crash
            // app startup. Licensing writes several trial-ledger anchors
            // (config / %ProgramData% / registry / DPAPI) on this path; any
            // of them can throw for IO / permission / corporate-policy reasons,
            // and an unhandled throw here would cascade to HandleFatalStartup-
            // Failure and shut the app down right after the splash — exactly
            // the "app does not open after the splash" symptom. Guard it.
            try
            {
                ProGlassAutomation.Services.Licensing.LicenseService.Initialize();
            }
            catch (Exception licEx)
            {
                AppLogService.Warning(
                    "License check failed on startup; continuing (best-effort): " +
                    licEx.Message,
                    new LogContext
                    {
                        Module = "Licensing",
                        Operation = "StartupLicenseCheck"
                    });
            }
            var licenseStatus = ProGlassAutomation.Services.Licensing
                .LicenseService.Current;
            AppLogService.Info(
                $"License check on startup: status={licenseStatus.Status}, " +
                $"CanRun={licenseStatus.CanRun}");

            // Force the LicenseStatusViewModel (which the main
            // window's overlay trigger binds to) to refresh
            // before the main window loads. Without this,
            // a fast machine can race the subscription setup
            // and the overlay stays hidden for a frame.
            try
            {
                ProGlassAutomation.ViewModels.Licensing
                    .LicenseStatusViewModel.Instance.Refresh();
            }
            catch { /* VM not built yet — safe to skip */ }

            // 12) Splash screen — this one window owns terms AND login.
            var splash = new SplashView();
            splash.Show();

            // 13) Loading delay for splash animations
            await Task.Delay(2800);

            // 13b) TERMS GATE + AUTH-1 — both happen ON the splash.
            // A second maximized LoginWindow was the recurring break:
            // splash is Topmost, login is not, Activate() fights z-order
            // and Windows flashes the desktop between them.
            if (!SplashView.HasAcceptedCurrentTerms())
            {
                var termsWait = new TaskCompletionSource<bool>();
                splash.TermsResolved += () => termsWait.TrySetResult(true);
                splash.Closed += (_, __) => termsWait.TrySetResult(true);
                splash.ShowTermsGate();
                await termsWait.Task;

                if (!splash.TermsAccepted)
                {
                    Shutdown(0);
                    return;
                }
            }

            if (!splash.IsVisible)
            {
                Shutdown(0);
                return;
            }

            // Commit 5 of the licensing refactor: check
            // for an empty Users table on first launch.
            // If empty, show the interactive first-run
            // setup wizard INSTEAD of the regular login
            // form. The wizard creates the first
            // administrator and then hands the splash
            // off to the normal login flow with the
            // new username pre-filled, so the same
            // operator can sign in with the credentials
            // they just chose. The old admin/admin
            // seed (AuditService.InitializeAsync) is
            // removed; this branch is the only path
            // that creates the first user.
            bool hasAnyUsers = await AuditService.HasAnyUsersAsync();
            bool signedIn = hasAnyUsers
                ? await splash.RunLoginAsync()
                : await splash.RunFirstRunSetupAsync();
            if (!signedIn)
            {
                Shutdown(0);
                return;
            }

            Presence?.UpdateIdentity(
                AuditService.CurrentUsername,
                AuditService.CurrentUser?.DisplayName ?? "");

            // 14) Main window handoff — splash stays Topmost and covers
            // the shell until it fades. WPF forbids ShowActivated=false
            // on a Maximized window (InvalidOperationException).
            // Phase 5: MainViewModel resolved via DI (no `new` in XAML) — testable
            //
            // SAFETY: the shell MUST appear even if the DI ViewModel cannot be
            // resolved (or its construction throws). A failure here would
            // otherwise cascade to HandleFatalStartupFailure and shut the app
            // down right when the splash fades — the "app won't open" symptom.
            // We resolve the DI VM first (so the XAML fallback instance is not
            // constructed needlessly), then build the window; if the resolve
            // throws, we retry via the parameterless ctor and still show.
            ViewModels.MainViewModel? mainVm = null;
            try
            {
                mainVm = ServiceProvider.GetRequiredService<ViewModels.MainViewModel>();
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"DI MainViewModel resolve failed, using parameterless ctor: {ex.Message}",
                    new LogContext { Module = "Main", Operation = "ResolveMainViewModel" });
            }

            MainWindow mainWindow;
            try
            {
                mainWindow = new MainWindow();
                if (mainVm != null) mainWindow.DataContext = mainVm;
            }
            catch (Exception ex)
            {
                // Last resort: a fresh parameterless window. If even that
                // throws, let it surface (nothing more we can do safely).
                AppLogService.Warning(
                    $"MainWindow construction failed, retrying fresh: {ex.Message}",
                    new LogContext { Module = "Main", Operation = "ConstructMainWindow" });
                mainWindow = new MainWindow();
                if (mainVm != null) mainWindow.DataContext = mainVm;
            }

            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnLastWindowClose;

            // Senior audit (Aug 2026): sign-out from the shell.
            // AuthSessionService has already cleared the pill vault +
            // audit-logged; all we do here is close the main window
            // and shut the process down. The user re-launches to sign
            // in as another operator. A full "restart splash without
            // process exit" flow would be nicer, but the ShutdownMode
            // = OnLastWindowClose path we already use is the least
            // risky teardown that guarantees per-session state cannot
            // bleed between operators.
            Services.Security.AuthSessionService.SignOutRequested -= OnSignOutRequested;
            Services.Security.AuthSessionService.SignOutRequested += OnSignOutRequested;

            mainWindow.WindowState = WindowState.Maximized;
            mainWindow.Show();
            await WaitForWindowReadyAsync(mainWindow);

            if (splash.IsVisible)
            {
                // 2026-08-25 senior fix — launch sync. The old code resumed
                // the startup from FadeOutAndClose's progress callback,
                // which fires BEFORE the splash's Close() executes. The
                // Activate() below then raced the close of a fullscreen
                // transparent Topmost window, and Windows' foreground
                // handoff glitched — the shell landed minimized/inactive
                // after the splash. Waiting for the real Closed event
                // gives the handoff a settled state to work with.
                var closed = new TaskCompletionSource<bool>();
                splash.Closed += (_, __) => closed.TrySetResult(true);
                splash.FadeOutAndClose(() => { });

                // ROBUSTNESS (2026-08-26): a Topmost + AllowsTransparency
                // window's fade-completed → Close() chain can occasionally
                // fail to raise Closed (or raise it before we subscribe is
                // impossible, but a teardown race can miss it). Never leave
                // the shell stuck behind a phantom splash: race the Closed
                // event against a short fallback that proceeds anyway, then
                // explicitly close the splash if it is still around.
                var splashGone = await Task.WhenAny(
                    closed.Task,
                    Task.Delay(1500));
                if (splashGone == closed.Task)
                    await closed.Task;
                else
                {
                    try { if (splash.IsVisible) splash.Close(); }
                    catch { /* best-effort teardown */ }
                }
            }

            // Claim the foreground AFTER the splash is fully gone. This is
            // the part that frequently failed: a full-screen Topmost +
            // AllowsTransparency splash close races WPF's Activate()/Focus(),
            // and the shell lands minimized, inactive, or behind the taskbar.
            // WPF's Activate() is not reliable for this handoff, so we ALSO
            // force the Win32 foreground (ShowWindow + SetForegroundWindow)
            // directly on the HWND and retry across a few dispatcher ticks
            // until the window is actually in the foreground.
            await BringMainWindowToForegroundAsync(mainWindow);

            // 15) v3 OTA §18 — daily background update check.
            // Fully async + offline-safe; never delays or blocks
            // startup, never affects normal operation (spec §17).
            _ = Services.Updates.UpdateService.StartupCheckAsync();

            // 2026-08-25: pre-warm the machine fingerprint OFF the UI
            // thread. License checks and the About page all read it; the
            // cache in MachineFingerprint makes every later call instant,
            // so no UI-thread consumer ever pays the NIC/volume-probe cost.
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try { Services.Licensing.MachineFingerprint.ComputeCurrent(); }
                catch { /* best-effort — consumers tolerate "—" */ }
            });
        }

        /// <summary>
        /// Waits until <paramref name="window"/> has loaded and painted
        /// at least one frame, so a covering splash can fade onto real
        /// pixels instead of an empty HWND.
        /// </summary>
        private static async Task WaitForWindowReadyAsync(Window window)
        {
            if (!window.IsLoaded)
            {
                var loaded = new TaskCompletionSource<bool>();
                void OnLoaded(object sender, RoutedEventArgs e)
                {
                    window.Loaded -= OnLoaded;
                    loaded.TrySetResult(true);
                }
                window.Loaded += OnLoaded;
                if (!window.IsLoaded)
                    await loaded.Task;
            }

            var frame = new TaskCompletionSource<bool>();
            window.Dispatcher.BeginInvoke(
                new Action(() => frame.TrySetResult(true)),
                System.Windows.Threading.DispatcherPriority.Render);
            await frame.Task;
        }

        // ── Win32 foreground handoff ─────────────────────────────────
        // WPF's Window.Activate()/Focus() is unreliable for the
        // topmost-transparent-splash → maximized-shell transition: the
        // shell can land minimized, behind other windows, or on the
        // taskbar without focus. These P/Invoke helpers force the HWND
        // to the foreground/top of the z-order, which Activate() cannot
        // guarantee. The splash-to-shell handoff in StartupCoreAsync uses
        // them with a short retry loop so a transient foreground-lock does
        // not leave the app invisible.

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const int SW_SHOWMAXIMIZED = 3;

        /// <summary>
        /// Reliably shows, maximises and activates <paramref name="window"/>
        /// after the splash closes. This method NEVER blocks startup: every
        /// await is a wall-clock Task.Delay (not a dispatcher-priority yield),
        /// so it cannot starve on a busy UI thread. WPF's Activate()/Focus()
        /// is complemented by forcing the Win32 foreground so a full-screen
        /// Topmost splash close cannot leave the shell inactive or behind.
        /// </summary>
        private static async Task BringMainWindowToForegroundAsync(Window window)
        {
            try
            {
                // WPF side — make sure it is not minimized and is the active
                // window in the app.
                if (window.WindowState == WindowState.Minimized)
                    window.WindowState = WindowState.Maximized;
                window.Show();
                window.Activate();

                // Win32 side — force the HWND to the foreground, maximised.
                var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SW_SHOWMAXIMIZED);
                    BringWindowToTop(hwnd);

                    // Retry SetForegroundWindow across a few short wall-clock
                    // delays (NOT Dispatcher.Yield, which can starve on a busy
                    // dispatcher). Windows can deny the foreground once right
                    // after the Topmost splash closes; the splash is gone by
                    // now, so a retry wins the permission.
                    for (int i = 0; i < 5; i++)
                    {
                        SetForegroundWindow(hwnd);
                        if (GetForegroundWindow() == hwnd) break;
                        await Task.Delay(40);
                    }
                }

                window.Focus();
            }
            catch (System.Exception ex)
            {
                // Never let foreground handoff break startup.
                AppLogService.Warning(
                    "Splash→main foreground handoff failed; continuing: " +
                    ex.Message,
                    new LogContext
                    {
                        Module = "Main",
                        Operation = "BringMainWindowToForegroundAsync"
                    });
            }
        }

        /// <summary>
        /// Handles a sign-out request from
        /// <see cref="Services.Security.AuthSessionService.SignOutRequested"/>.
        /// Closes the main window on the UI thread so the standard
        /// OnLastWindowClose shutdown mode runs. The user re-launches
        /// the app to sign in as another operator; the auto-sign-in
        /// blob and skip-login flags have already been cleared by
        /// AuthSessionService.SignOutAsync before the event fires,
        /// so the next launch always shows the login form.
        /// </summary>
        private static void OnSignOutRequested(object? sender, EventArgs e)
        {
            try
            {
                var app = Current;
                if (app == null) return;

                app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        app.MainWindow?.Close();
                    }
                    catch (Exception ex)
                    {
                        AppLogService.Warning(
                            "Sign-out main-window close failed: " + ex.Message,
                            new LogContext
                            {
                                Module = "Auth",
                                Operation = "OnSignOutRequested"
                            });
                    }
                }));
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "OnSignOutRequested dispatch failed: " + ex.Message,
                    new LogContext
                    {
                        Module = "Auth",
                        Operation = "OnSignOutRequested"
                    });
            }
        }

        // Phase 4: Named handler for static event — detachable, no lambda leak
        private static void OnCurrentUserChanged(object? sender, EventArgs e)
        {
            try
            {
                Presence?.UpdateIdentity(
                    AuditService.CurrentUsername,
                    AuditService.CurrentUser?.DisplayName ?? "");
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Presence identity update failed: {ex.Message}",
                    new LogContext { Module = "Network", Operation = "Presence.UpdateIdentity" });
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  SPEECH-TO-TEXT INITIALIZATION — background + timeout + fallback
        //
        //  Priority chain:
        //    1. Try Whisper (if model downloaded) — 10s timeout
        //    2. On Whisper timeout/failure → try System.Speech
        //    3. On System.Speech failure → null (voice disabled)
        //
        //  Each attempt runs on background thread with timeout so
        //  UI thread never blocks. App startup ALWAYS completes.
        // ══════════════════════════════════════════════════════════════

        private static async Task<ISpeechToTextService>
            InitializeSpeechToTextAsync()
        {
            // ── Try Whisper first if model file exists ────────────────
            if (WhisperModelManager.IsModelDownloaded)
            {
                var whisper = await TryCreateWithTimeoutAsync(
                    factory: () => new WhisperSpeechService(),
                    timeoutMs: 10000,
                    engineName: "Whisper");

                if (whisper != null)
                {
                    AppLogService.Debug(
                        $"[App] STT: Whisper " +
                        $"({WhisperModelManager.SelectedModelInfo.DisplayName})");
                    return whisper;
                }

                AppLogService.Debug(
                    "[App] STT: Whisper init failed or timed out. " +
                    "Falling back to System.Speech.");
            }

            // ── Fall back to System.Speech ────────────────────────────
            var systemSpeech = await TryCreateWithTimeoutAsync(
                factory: () => new SpeechToTextService(),
                timeoutMs: 5000,
                engineName: "System.Speech");

            if (systemSpeech != null)
            {
                AppLogService.Debug(
                    "[App] STT: System.Speech fallback active. " +
                    "Voice available but accuracy limited to ~70%. " +
                    "Download Whisper model in Settings for 95%+ accuracy.");
                return systemSpeech;
            }

            // ── All attempts failed ───────────────────────────────────
            AppLogService.Debug(
                "[App] STT: All engines failed. Voice input disabled. " +
                "Check audio drivers and Windows Speech Platform.");
            return null;
        }

        /// <summary>
        /// Attempts to construct a service on background thread with
        /// timeout. Returns null if construction hangs, times out,
        /// or throws.
        /// 
        /// Critical for STT services because underlying audio device
        /// enumeration (NAudio WaveInEvent.DeviceCount, Windows Speech
        /// Recognizer init) can block indefinitely on machines with
        /// audio driver issues.
        /// </summary>
        private static async Task<ISpeechToTextService>
            TryCreateWithTimeoutAsync(
                Func<ISpeechToTextService> factory,
                int timeoutMs,
                string engineName)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);

                // Run constructor on background thread
                var createTask = Task.Run(() =>
                {
                    try
                    {
                        return factory();
                    }
                    catch (Exception ex)
                    {
                        AppLogService.Debug(
                            $"[App] {engineName} constructor threw: " +
                            $"{ex.GetType().Name}: {ex.Message}");
                        return (ISpeechToTextService)null;
                    }
                }, cts.Token);

                // Race constructor against timeout
                var completedTask = await Task.WhenAny(
                    createTask,
                    Task.Delay(timeoutMs, cts.Token));

                if (completedTask == createTask)
                {
                    // Constructor finished before timeout
                    return await createTask;
                }

                // Timeout won the race — constructor is still hanging
                // We cannot forcibly abort it (Thread.Abort deprecated)
                // but we can abandon it. GC will clean up eventually.
                AppLogService.Debug(
                    $"[App] {engineName} constructor TIMED OUT after " +
                    $"{timeoutMs}ms — abandoning and falling through.");

                return null;
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[App] {engineName} init exception: " +
                    $"{ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  CRASH REPORT — full technical dump for diagnosis
        // ══════════════════════════════════════════════════════════════

        private static string CrashReportPath =>
            ProGlassAutomation.Services.Logging.CrashReportService
                .CrashReportLatestPath;

        /// <summary>
        /// Writes a complete crash report (exception chain + stack +
        /// environment + recent log lines + license status) so any
        /// crash can be diagnosed from the file alone. Never throws
        /// — a failure here must not mask the original error.
        ///
        /// The body delegates to CrashReportService; the
        /// private method exists only so the four call
        /// sites in this file (UI thread, background
        /// thread, fatal-startup, unobserved-task) can
        /// keep using the local <c>WriteCrashReport(id,
        /// ex)</c> shape.
        /// </summary>
        private static string WriteCrashReport(string errorId, Exception root)
        {
            try
            {
                return ProGlassAutomation.Services.Logging.CrashReportService
                    .HandleCrash(errorId, root);
            }
            catch
            {
                // Reporting failure must never propagate.
                return "";
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Phase 4: Detach static events — no leak
            try { AuditService.CurrentUserChanged -= OnCurrentUserChanged; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[App] OnExit event-detach failed: " + ex.Message); }

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
            base.OnExit(e);
        }
    }
}