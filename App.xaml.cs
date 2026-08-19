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
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Services.Dashboard.Core;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using ProGlassAutomation.Views.Splash;
using System;
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
        public static IProformaInvoiceRepository Invoices { get; private set; }
        public static IJobOrderRepository Jobs { get; private set; }
        public static IDeliveryNoteRepository DeliveryNotes { get; private set; }
        public static IMasterDataRepository MasterData { get; private set; }
        public static ITaxInvoiceRepository TaxInvoices { get; private set; }
        public static IStatementRepository Statements { get; private set; }

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
            // OTA bootstrap FIRST (Velopack): handles pending updates and
            // restart plumbing before any UI is created. No-op in dev mode.
            Velopack.VelopackApp.Build().Run();

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

                WriteCrashReport(errorId, args.Exception);

                string msg =
                    $"Something went wrong.\n\n" +
                    $"Error ID: {errorId}\n" +
                    $"Type:    {args.Exception.GetType().Name}\n" +
                    $"Message: {args.Exception.Message}\n\n" +
                    "Full technical details were saved to:\n" +
                    $"{CrashReportPath}\n\n" +
                    "Share that file (or the Error ID) with support.";

                MessageBox.Show(msg, "ProGlass — Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
                    AppLogService.Error(
                        "Unobserved task exception", args.Exception,
                        new LogContext
                        {
                            Operation = "UnobservedTaskException"
                        });

                    // Mark observed: the failure is now recorded in the
                    // log, so escalating it to a process kill would lose
                    // more information than it preserves.
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
            Specs = new SpecificationsRepository();
            Invoices = new ProformaInvoiceRepository();
            Jobs = new JobOrderRepository();
            DeliveryNotes = new DeliveryNoteRepository();
            MasterData = new MasterDataRepository();
            TaxInvoices = new TaxInvoiceRepository();
            Statements = new StatementRepository();

            // Dashboard service layer — stateless, safe as singletons.
            DashboardMetrics = new DashboardMetricsService();
            DashboardFilter  = new DashboardFilterService();

            await Task.WhenAll(
                Specs.InitializeAsync(),
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

            // 9c-2) Cross-database integrity sweep (Phase 2):
            //       reconcile Tax Invoice → Delivery Note invoiced links on
            //       startup, so any link left broken by a crash / failed write
            //       is automatically healed. Runs in the background and never
            //       blocks startup; failures are logged, never fatal.
            ProGlassAutomation.Utilities.AsyncUtility.SafeFireAndForget(
                () => ProGlassAutomation.Services.TaxInvoice.Repository
                    .TaxInvoiceDnReconciler.ReconcileAsync(),
                "Tax-invoice → delivery-note reconciliation sweep failed");

            // 9c-2) Same-network user presence (Users page).
            // Start the service now, but do NOT broadcast an identity until
            // LoginWindow verifies a password. Starting as the seeded admin
            // made colleagues see "admin online" during the splash.
            try
            {
                Presence = new UserPresenceService();
                Presence.Start("", "");

                AuditService.CurrentUserChanged += (_, __) =>
                    Presence.UpdateIdentity(
                        AuditService.CurrentUsername,
                        AuditService.CurrentUser?.DisplayName ?? "");
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
            //     (Normal / Monet / Professional).
            VisualThemeEngine.Apply(
                Config.Appearance?.VisualTheme ?? "Normal",
                Config.Appearance?.WallpaperPath ?? "",
                Config.Appearance?.WallpaperOpacity ?? 0.85);

            // 9e) Restore the persisted accent preset — re-derives the
            //     full accent family (hover/tint/contrast) from the
            //     saved colour so the UI keeps it between sessions.
            ProGlassAutomation.Services.ThemeManager.ApplyAccent(
                ProGlassAutomation.Services.Theme.AccentPresets.ColorFor(
                    Config.Appearance?.AccentPreset ?? "GlassBlue"),
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

            ProGlassAutomation.Services.ThemeManager.ApplyDisplayDensity(
                Config.Appearance?.DisplayDensity ?? "Compact");

            ProGlassAutomation.Services.Export.Pdf.DocumentStyle.Apply(
                Config.Appearance?.PdfLayoutId);

            AuditService.Log(
                AuditSections.App, "App started",
                $"Motion tier {PerformanceProfileService.TargetFps} fps");

            // 10) AI via factory (multi-provider)
            Ai = AiProviderFactory.CreateActive(Config.Ai);

            System.Diagnostics.Debug.WriteLine(
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
                        System.Diagnostics.Debug.WriteLine(
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

            // 12) Splash screen
            var splash = new SplashView();
            splash.Show();

            // 13) Loading delay for splash animations
            await Task.Delay(2800);

            // 13b) TERMS GATE + AUTH-1 — both happen ON the splash, before
            // the main window exists.
            //
            // Order is deliberate and was wrong before: the splash IS the
            // first screen, so it must stay on screen while the user accepts
            // the terms. Only then does sign-in appear.
            //
            // ShutdownMode MUST be pinned to OnExplicitShutdown across this
            // handover. The default is OnLastWindowClose, and there are
            // moments below with ZERO windows open (splash closed, login not
            // yet shown) which would otherwise terminate the process before
            // anyone could sign in. Restored once MainWindow exists.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Terms are shown on the splash itself. A previous acceptance of
            // THIS version is remembered, so it is asked once, not every run.
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

            // Sign-in. Previously the app went straight to MainWindow as
            // whichever active user had the lowest Id, which on a fresh
            // install is an Administrator, so the A-1 service-layer guards
            // were authorizing an identity nobody had ever established.
            //
            // Show login UNDER the splash, then fade the splash out. Closing
            // the splash first left a frame of desktop and made the two
            // screens feel unsynced. Splash is Topmost, so login used to
            // appear behind it if we showed both without releasing Topmost.
            var login = new Views.Login.LoginWindow();
            login.Show();
            login.Activate();

            if (splash.IsVisible)
            {
                var faded = new TaskCompletionSource<bool>();
                splash.FadeOutAndClose(() => faded.TrySetResult(true));
                await faded.Task;
            }

            if (login.IsVisible)
            {
                var signedIn = new TaskCompletionSource<bool>();
                login.Closed += (_, __) => signedIn.TrySetResult(true);
                await signedIn.Task;
            }

            if (!login.SignedIn)
            {
                Shutdown(0);
                return;
            }

            Presence?.UpdateIdentity(
                AuditService.CurrentUsername,
                AuditService.CurrentUser?.DisplayName ?? "");

            // 14) Main window handoff
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;

            // AUTH-1: main window exists, so restore the normal shutdown
            // behaviour — closing it quits the app, exactly as before.
            ShutdownMode = ShutdownMode.OnLastWindowClose;

            mainWindow.Show();
            mainWindow.Activate();
            mainWindow.Focus();
            mainWindow.Topmost = true;
            mainWindow.Topmost = false;

            // 15) v3 OTA §18 — daily background update check.
            // Fully async + offline-safe; never delays or blocks
            // startup, never affects normal operation (spec §17).
            _ = Services.Updates.UpdateService.StartupCheckAsync();
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
                    System.Diagnostics.Debug.WriteLine(
                        $"[App] STT: Whisper " +
                        $"({WhisperModelManager.SelectedModelInfo.DisplayName})");
                    return whisper;
                }

                System.Diagnostics.Debug.WriteLine(
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
                System.Diagnostics.Debug.WriteLine(
                    "[App] STT: System.Speech fallback active. " +
                    "Voice available but accuracy limited to ~70%. " +
                    "Download Whisper model in Settings for 95%+ accuracy.");
                return systemSpeech;
            }

            // ── All attempts failed ───────────────────────────────────
            System.Diagnostics.Debug.WriteLine(
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
                        System.Diagnostics.Debug.WriteLine(
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
                System.Diagnostics.Debug.WriteLine(
                    $"[App] {engineName} constructor TIMED OUT after " +
                    $"{timeoutMs}ms — abandoning and falling through.");

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[App] {engineName} init exception: " +
                    $"{ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  CRASH REPORT — full technical dump for diagnosis
        // ══════════════════════════════════════════════════════════════

        private static string CrashReportPath => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "logs", "crash-latest.txt");

        /// <summary>
        /// Writes a complete crash report (exception chain + stack +
        /// environment) so any crash can be diagnosed from the file alone.
        /// Never throws — a failure here must not mask the original error.
        /// </summary>
        private static void WriteCrashReport(string errorId, Exception root)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(CrashReportPath)!;
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("═══════════════════════════════════════════════════════");
                sb.AppendLine(" ProGlass Automation — crash report");
                sb.AppendLine("═══════════════════════════════════════════════════════");
                sb.AppendLine($"Error ID : {errorId}");
                sb.AppendLine($"Time     : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine($"Version  : {Services.Updates.AppInfo.Version}");
                sb.AppendLine($"User     : {Services.Audit.AuditService.CurrentUsername}");
                sb.AppendLine($"Session  : {Services.Audit.AuditService.SessionId}");
                sb.AppendLine($"OS       : {Environment.OSVersion}");
                sb.AppendLine(".NET     : " + Environment.Version);
                sb.AppendLine();

                var current = root;
                int depth = 0;
                while (current != null && depth < 6)
                {
                    if (depth > 0) sb.AppendLine("── caused by ──");
                    sb.AppendLine($"[{depth}] {current.GetType().FullName}");
                    sb.AppendLine($"    Message : {current.Message}");
                    if (!string.IsNullOrEmpty(current.StackTrace))
                        sb.AppendLine("    Stack   :\n" + current.StackTrace);
                    current = current.InnerException;
                    depth++;
                }

                System.IO.File.WriteAllText(CrashReportPath, sb.ToString());
            }
            catch
            {
                // Reporting failure must never propagate.
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
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
            Connectivity?.StopMonitoring();
            Presence?.Dispose();
            SpeechToText?.Dispose();
            TextToSpeech?.Dispose();
            base.OnExit(e);
        }
    }
}