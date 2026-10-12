// ═══════════════════════════════════════════════════════════════════════
// FILE: App.StartupCore.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.5 — App partial split. This part owns the StartupCore members.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIConnectivity;
using ProGlassAutomation.Services.AI.AIMemory;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Database.SchemaMigrations;
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
using ProGlassAutomation.Controls.CrystalFluid;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.InteropServices;
using Velopack;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ProGlassAutomation.Services.Dialogs;

namespace ProGlassAutomation
{
    public partial class App : Application
    {
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

            // Ios-17pm global water material — one class-level handler on
            // Border.Loaded auto-attaches the animated liquid fill to every
            // glass card in the app (styled, inline-styled, or future
            // views); family-gated by CrystalWaterDefaultEnabled, so it is
            // inert under every non-crystal family. No per-style edits.
            CrystalWaterGlobal.Install();

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

                    Prompts.Show(
                        denied.UserMessage, "Permission Denied",
                        MessageBoxButton.OK, MessageBoxImage.Warning);

                    args.Handled = true;
                    return;
                }

                // ── CRASH LATCH (2026-10-06, ERR-7F77C4) ─────────────
                // Only the FIRST unhandled UI exception gets the full
                // treatment. Later ones are counted and dropped, because
                // args.Handled = true keeps the dispatcher pumping and a
                // fault that repeats per render pass would otherwise run
                // this whole path — log, crash report, HTTP post, dialog,
                // watchdog — once per failure. See App.CrashHelpers.cs.
                if (Interlocked.Exchange(ref _crashTaken, 1) != 0)
                {
                    int n = Interlocked.Increment(ref _suppressedCrashes);
                    if (n == 1 &&
                        Interlocked.Exchange(ref _suppressionNoted, 1) == 0)
                    {
                        // One line, written once, so the report says the
                        // storm was recognised rather than looking like 90
                        // independent faults. Never repeated.
                        AppLogService.Info(
                            "Further unhandled UI exceptions are being " +
                            "suppressed while the first crash report completes.",
                            new LogContext { Operation = "UnhandledException" });
                    }
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
                // OutOfMemoryException gets no dialog. CrashReportDialog is a
                // Window, so showing it means laying out and rendering — the
                // exact operation that just failed, in the ERR-7F77C4 stack
                // (MediaContext.CompleteRender → HwndTarget.OnResize). Trying
                // it allocates more of the memory whose absence caused the
                // crash, and the report showed it throwing
                // "Cannot set Visibility ... while a Window is closing" once
                // the shutdown was already under way. The ErrorId is already
                // in the log and in the crash report file, so nothing
                // diagnostic is lost by going straight to exit.
                if (args.Exception is OutOfMemoryException)
                {
                    AppLogService.Info(
                        "Skipping the crash dialog for OutOfMemoryException; " +
                        "error " + errorId + " is in the crash report.",
                        new LogContext { Operation = "UnhandledException" });
                }
                else
                {
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
                }
                args.Handled = true;

                // A UI-thread crash leaves the dispatcher in an undefined
                // state. Continuing from here is exactly why the process
                // lingered in Task Manager after the user closed the window.
                // Terminate deterministically: request a clean shutdown (so
                // OnExit still flushes config/audit/backup), then arm a
                // watchdog that force-exits if anything keeps the process
                // alive past the grace period.
                ForceExitAfterCrash(errorId);
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

                    var answer = Prompts.Show(
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

            // RC-014: log the FULL build identity, not just the version. Nine
            // days of behaviour-changing commits shipped under one frozen
            // 2.0.1, so "the version says 2.0.1" told nobody whether the
            // running binary contained a given fix. Version + the assembly
            // build timestamp is unique per build and is the first line of
            // every log.
            AppLogService.Info(
                $"ProGlass {Services.Updates.AppInfo.BuildStamp} starting",
                new LogContext { Operation = "Startup" });

            // 1) Load persisted config
            Config = ConfigService.Load();

            // 1a) DATA ROOT — must be applied BEFORE any DbContext opens.
            // When Settings enables a LAN/Wi-Fi shared data folder, every
            // business DbContext now resolves through AppDataRoot.CurrentFolder.
            // An unavailable share selects one explicit, visible local mode
            // for this whole startup; it never hot-switches during a session.
            // The persisted review marker prevents a later launch from
            // silently selecting a different dataset after offline writes.
            bool hadOfflineFallbackReview =
                Config.DataConnectivity?.SharedRootOfflineFallbackNeedsReview == true;
            bool hadFallbackNotice =
                Config.DataConnectivity?.SharedRootLocalModeNoticePending == true;
            bool hadReconnectNotice =
                Config.DataConnectivity?.SharedRootReconnectNoticeShown == true;
            AppDataRoot.ConfigureFrom(Config);
            bool fallbackStateChanged =
                (!hadOfflineFallbackReview &&
                 Config.DataConnectivity?.SharedRootOfflineFallbackNeedsReview == true)
                || (!hadFallbackNotice &&
                    Config.DataConnectivity?.SharedRootLocalModeNoticePending == true)
                || (!hadReconnectNotice &&
                    Config.DataConnectivity?.SharedRootReconnectNoticeShown == true);
            if (fallbackStateChanged)
            {
                // Persist the local-fallback history before any business data
                // can be written, so the next startup cannot hide offline
                // records by silently switching to a recovered network share.
                if (!ConfigService.TrySave(Config))
                {
                    const string saveFailure =
                        "ProGlass could not save the offline data-root review state. " +
                        "No business database has been opened. To avoid writing local data " +
                        "that a later startup could hide, ProGlass will close. Check that " +
                        "your Windows profile's AppData folder is writable, then launch again.";
                    AppLogService.Error(
                        "Startup stopped because the offline shared-root review state could not be persisted.",
                        null,
                        new LogContext { Module = "Database", Operation = "PersistOfflineRootReview" });
                    try
                    {
                        Prompts.Show(
                            saveFailure,
                            "Offline mode could not be saved",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                    catch (Exception promptEx)
                    {
                        AppLogService.Warning(
                            "Could not display the offline-mode persistence warning.",
                            promptEx,
                            new LogContext { Module = "Database", Operation = "PersistOfflineRootReview" });
                    }
                    Shutdown();
                    return;
                }
            }

            // 1b) GLOBAL LOCALE — language, currency, country.
            //     • GeoService: cached country first, IP refresh after.
            //     • CurrencyService: cached FX rates, live internet
            //       refresh after (offline → last fetched rate).
            //     • LocalizationService: apply the saved language, or
            //       suggest the country language (English stays the
            //       primary/default everywhere). Language and currency
            //       are INDEPENDENT — English UI + INR amounts works.
            //     Nothing here blocks or throws; every refresh is
            //     fire-and-forget.
            Services.Localization.GeoService.Instance.Initialize();
            Services.Localization.CurrencyService.Instance.Initialize();
            Services.Localization.LocalizationService.Instance.Initialize(Config.Localization);
            Services.Localization.GeoService.Instance.LocationResolved +=
                geo => Services.Localization.LocalizationService.Instance
                    .ApplyRegionalSuggestion(geo.CountryIso);
            _ = Services.Localization.GeoService.Instance.RefreshAsync();
            _ = Services.Localization.CurrencyService.Instance.RefreshAsync();

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
                CreditNotes = ServiceProvider.GetRequiredService<Services.CreditNote.Repository.ICreditNoteRepository>();
                Statements = ServiceProvider.GetRequiredService<IStatementRepository>();
                OrderReports = ServiceProvider.GetRequiredService<IOrderReportRepository>();
                Reporting = ServiceProvider.GetRequiredService<IReportingDataService>();
                Status = ServiceProvider.GetRequiredService<Services.Workflow.IStatusService>();
                Dialogs = ServiceProvider.GetRequiredService<Services.Dialogs.IDialogService>();
                DashboardMetrics = ServiceProvider.GetRequiredService<IDashboardMetricsService>();
                DashboardFilter = ServiceProvider.GetRequiredService<IDashboardFilterService>();
            }
            catch (Exception ex)
            {
                // The exception TYPE is the diagnostic part — "DI container
                // build failed: Value cannot be null" tells an operator nothing,
                // whereas the type names the missing registration. ex.ToString()
                // keeps the inner exception, which is where DI failures usually
                // actually live.
                // Error, not Warning. A container that will not build means the
                // application is running in a degraded mode it was never tested
                // in; Warning is what this codebase reserves for things that
                // recovered. Error also records the exception object and returns
                // an ErrorId, so a support request can quote one number instead
                // of pasting a stack.
                //
                // (This line said Warning when it was first written, and an
                // analysis note briefly claimed there was no Error level to use.
                // There always was — AppLogService.Error, line 75.)
                AppLogService.Error(
                    $"DI container build failed ({ex.GetType().Name}); falling back to " +
                    $"manual construction. Some services may behave differently.",
                    ex);

                // Fallback to manual construction (never break startup)
                Specs = new SpecificationsRepository();
                Inquiries = new InquiryRepository();
                Invoices = new ProformaInvoiceRepository();
                Jobs = new JobOrderRepository();
                DeliveryNotes = new DeliveryNoteRepository();
                MasterData = new MasterDataRepository();
                TaxInvoices = new TaxInvoiceRepository();
                CreditNotes = new Services.CreditNote.Repository.CreditNoteRepository();
                Statements = new StatementRepository();
                OrderReports = new Services.OrderReport.Repository.OrderReportRepository();
                Reporting = new ReportingDataService();
                DashboardMetrics = new DashboardMetricsService();
                DashboardFilter = new DashboardFilterService();

                // These two were MISSING from the fallback, which left them at
                // the `null!` they are initialised to in App.xaml.cs. Anything
                // calling App.Status.Show(...) or App.Dialogs.Show(...) after a
                // container failure then threw NullReferenceException — during
                // startup, in the one situation where the operator most needs a
                // message on screen. Both are sealed with implicit parameterless
                // constructors and are registered as singletons, so constructing
                // them here matches what the container would have produced.
                Status = new Services.Workflow.StatusService();
                Dialogs = new Services.Dialogs.DialogService();
            }

            // ── Records → Supabase sync (integration, dormant by default) ──
            // StartIfConfigured only reads two small local files and arms a
            // timer whose FIRST pass is 2 minutes away — no network, no
            // database, no UI on the startup path. Wrapped in its own
            // try/catch so a sync fault can never touch app startup.
            try
            {
                RecordsSync = ServiceProvider?.GetService<Services.RecordsSync.RecordsSyncService>();
                RecordsSync?.StartIfConfigured();
            }
            catch (Exception ex)
            {
                AppLogService.Error("[RecordsSync] startup hook failed; sync disabled.", ex);
            }

            // Named, and every failure logged rather than only the first.
            //
            // Task.WhenAll surfaces a single exception. With eleven EnsureCreated
            // calls running in parallel against eleven separate SQLite files, the
            // first to fail is rarely the only one — a full data folder, a locked
            // file or a disk fault hits several at once. A startup log naming one
            // database sends the operator to fix the wrong one, and the other ten
            // fail again on the next launch.
            //
            // Behaviour is unchanged: startup still fails, and the same first
            // exception still propagates to the caller. This only makes the log
            // complete before it does. Reading .Exception also marks each task
            // observed, so nothing surfaces later as UnobservedTaskException.
            var inits = new (string Name, Task Work)[]
            {
                ("specifications",  Specs.InitializeAsync()),
                ("inquiries",       Inquiries.InitializeAsync()),
                ("invoices",        Invoices.InitializeAsync()),
                ("jobs",            Jobs.InitializeAsync()),
                ("deliverynotes",   DeliveryNotes.InitializeAsync()),
                ("masterdata",      MasterData.InitializeAsync()),
                ("taxinvoices",     TaxInvoices.InitializeAsync()),
                ("creditnotes",     CreditNotes.InitializeAsync()),
                ("statements",      Statements.InitializeAsync()),
                ("orderreports",    OrderReports.InitializeAsync()),

                // 9b) Audit & user system — own DB (audit.db), safe in
                //     parallel. Never throws internally.
                ("audit",           AuditService.InitializeAsync()),

                // 9c-1) AI learning memory (aimemory.db)
                ("aimemory",        AiMemoryService.InitializeAsync())
            };

            try
            {
                await Task.WhenAll(Array.ConvertAll(inits, i => i.Work));
            }
            catch
            {
                foreach (var init in inits)
                {
                    if (init.Work.IsFaulted)
                        AppLogService.Warning(
                            $"Database initialisation failed for '{init.Name}': " +
                            $"{init.Work.Exception?.GetBaseException()}");
                }

                throw;
            }

            if (SchemaMigrationRunner.HasFailure)
            {
                AppLogService.Error(
                    "One or more database schema upgrades failed. Data files were left unchanged. " +
                    SchemaMigrationRunner.FailureSummary,
                    null,
                    new LogContext { Module = "Database", Operation = "StartupSchemaUpgrade" });
                try
                {
                    Prompts.Show(
                        "A database upgrade failed, so the file was left unchanged. " +
                        "Do not enter new invoices, job orders, or deliveries until this is fixed. " +
                        "A backup is next to the database when one could be made.\n\n" +
                        SchemaMigrationRunner.FailureSummary,
                        "Database upgrade failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                catch (Exception dialogEx)
                {
                    AppLogService.Warning(
                        "Could not show the schema-upgrade warning: " + dialogEx.Message);
                }
            }

            // The TI↔DN reconciliation sweep is scheduled after successful
            // sign-in in OnCurrentUserChanged. It must not run here before an
            // authenticated identity exists: link repairs are permission-gated.

            // 9c-2) Same-network user presence (Users page).
            // Start the service now, but do NOT broadcast an identity until
            // LoginWindow verifies a password. Starting as the seeded admin
            // made colleagues see "admin online" during the splash.
            // Phase 4: No lambda subscription to static event — named handler + detachable
            try
            {
                Presence = new UserPresenceService();
                Presence.Start("", "");
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Presence service unavailable: {ex.Message}",
                    new LogContext { Module = "Network", Operation = "Startup" });
            }

            // Authentication lifecycle work must not depend on optional
            // presence/network startup succeeding. Keep the named handler
            // attached independently so post-login finance reconciliation
            // still runs when Presence is unavailable.
            AuditService.CurrentUserChanged -= OnCurrentUserChanged;
            AuditService.CurrentUserChanged += OnCurrentUserChanged;

            // 9c-0) Specification ledger ground truth. Subscribes to invoice
            // writes and runs the initial heal on a background thread: spec
            // counts/history are derived from SAVED invoices only, purging
            // phantom Step-4-APPLY rows and deleted-PI references. Start()
            //       never throws and never blocks startup (background repair
            //       with explicit logging).
            try
            {
                ProGlassAutomation.Services.Specifications
                    .SpecificationLedgerService.Start();
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Specification ledger unavailable: {ex.Message}",
                    new LogContext { Module = "Specifications", Operation = "Startup" });
            }

            // 9c) Hardware-adaptive motion profile.
            //     Detects the display refresh rate and snaps to the
            //     nearest tier in 60/90/120/144/165 fps, then publishes
            //     MotionDuration* / TargetFps resources for the XAML.
            PerformanceProfileService.Apply();

            // 9d) Base colour mode (Light/Dark) + visual theme family +
            //     wallpaper (Normal / Normal (Industrial) / Monet /
            //     Ios-17pm). The base theme is restored FIRST so the
            //     family derives its surfaces against the right palette.
            //     Normal (Industrial) is the constant default family.
            ProGlassAutomation.Services.ThemeManager.ApplyTheme(
                Config.Appearance?.Theme == "Dark"
                    ? ProGlassAutomation.Services.AppTheme.Dark
                    : ProGlassAutomation.Services.AppTheme.Light);

            //     The gradient-surface preference is restored FIRST:
            //     Monet's glass/window surfaces read it while building.
            ProGlassAutomation.Services.ThemeManager.ApplyGradientPreference(
                Config.Appearance?.UseGradientSurfaces ?? true);

            VisualThemeEngine.Apply(
                Config.Appearance?.VisualTheme
                    ?? VisualThemeEngine.NormalIndustrial,
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
            //     The custom card-edge colour is restored FIRST so the
            //     ApplyOutline below paints with it.
            ProGlassAutomation.Services.ThemeManager.ApplyCardEdgeColor(
                Config.Appearance?.CardEdgeColor);
            ProGlassAutomation.Services.ThemeManager.ApplyOutline(
                Config.Appearance?.UseDefinedOutline ?? false);

            // 9g) Restore the persisted global card border thickness so the
            //     saved Appearance > Border Thickness value applies on launch.
            ProGlassAutomation.Services.ThemeManager.ApplyCardBorderThickness(
                Config.Appearance?.CardBorderThickness ?? 1.5);

            // 9g2) Restore the persisted card EDGE weight (Define Card Edges,
            //     1–100) so the corner-border line matches what the user set.
            ProGlassAutomation.Services.ThemeManager.ApplyCardEdgeThickness(
                Config.Appearance?.CardEdgeThickness ?? 2.0);

            // 9h) Restore the persisted card corner radius (default 12).
            ProGlassAutomation.Services.ThemeManager.ApplyCardCornerRadius(
                Config.Appearance?.CardCornerRadius ?? 12);

            // 9h2) Semantic surface layer (2026-09-14 — post-splash crash fix).
            // Materializes the Surface.*.BackgroundBrush / BorderBrush /
            // CornerRadius / BorderThickness / Effect keys on Application
            // resources from the saved appearance. MUST run before the first
            // window: the shell sidebar, dashboards and DataGrid chrome all
            // consume those keys (previously they only existed as
            // ResourceReferenceExpression markers in the merged dictionaries,
            // which crashed the first property assignment after the splash).
            try
            {
                ProGlassAutomation.Services.ThemeManager.ApplySemanticSurfaces();
            }
            catch (Exception semEx)
            {
                AppLogService.Warning(
                    "Semantic surfaces failed to apply on startup; continuing with token fallbacks: "
                    + semEx.Message,
                    new LogContext { Module = "Theme", Operation = "StartupSemanticSurfaces" });
            }

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

            // Mirror the active print layout into WPF resources so the Step 7
            // preview and the exported PDF read the same tokens. Installed
            // after Apply so the first publish already carries the saved
            // layout, not the catalog default.
            ProGlassAutomation.Services.Export.PrintPreview.PrintTokenService.Install();

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
            //
            // MUST be the SAME instance the DI container hands out.
            // Previously this created its own `new ConnectivityService()`
            // and started monitoring on it, while
            // ServiceRegistration registered a SEPARATE singleton that
            // nothing ever called StartMonitoring() on. MainViewModel got
            // the DI one, whose _isOnline stayed at its default `false`
            // forever and never raised ConnectivityChanged -- so the shell
            // pill read "Offline" on a perfectly good connection.
            //
            // Resolving from the provider means App.Connectivity and every
            // injected IConnectivityService are one object, monitored once.
            Connectivity =
                ServiceProvider?.GetService(typeof(IConnectivityService))
                    as IConnectivityService
                ?? new ConnectivityService();
            Connectivity.StartMonitoring();

            // Pin shutdown BEFORE the first window. Default OnLastWindowClose
            // would kill the process if splash closed during the delay or
            // during sign-in. Restored once MainWindow exists.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 11) License check.
            //
            //     Secure offline redesign: the app now expects a
            //     vendor-signed offline activation response stored in
            //     protected local state. On a fresh machine the shell
            //     still boots, but the user must generate an activation
            //     request and import the signed response through the
            //     LicenseActivationView overlay before productive work
            //     is allowed.
            //
            //     LicenseService.Initialize() is still called here so
            //     the protected activation state is loaded and the
            //     LicenseStatusViewModel (used by the main window's
            //     overlay trigger) has a current value to bind against.
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
            try
            {
                int activeSplashStyle = ProGlassAutomation.Services.Splash.SplashThemeManager.ResolveActiveStyle(Config);
                splash.ApplySplashStyle(activeSplashStyle);
            }
            catch (Exception ex)
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("[Startup] Splash style activation: " + ex.Message);
            }
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
                // ERROR, not Warning: if this fires, every binding in the
                // shell is about to fail silently. Log the FULL exception
                // (including inner) -- the message alone hid the real
                // cause during the 2026-09-02 dead-shell incident.
                AppLogService.Error(
                    "DI MainViewModel resolve failed, using parameterless ctor",
                    ex,
                    new LogContext { Module = "Main", Operation = "ResolveMainViewModel" });
            }

            // Fallback: never hand MainWindow a null DataContext. A null
            // DataContext makes every binding fail silently -- dashboards
            // render blank and any element whose Visibility is bound with
            // no FallbackValue reverts to its default (this is why the
            // agent panel appeared stuck open). Try the parameterless
            // ctor explicitly rather than leaving it null.
            if (mainVm == null)
            {
                try
                {
                    mainVm = new ViewModels.MainViewModel();
                }
                catch (Exception ex)
                {
                    AppLogService.Error(
                        "Parameterless MainViewModel ctor ALSO failed; the shell "
                        + "will open with no DataContext and will not respond",
                        ex,
                        new LogContext { Module = "Main", Operation = "ResolveMainViewModel" });
                }
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

            // OS window backdrop (2026-09-05): restore a saved Mica/
            // Acrylic preference (Windows 11 22H2+); silently no-ops for
            // "Off" or unsupported systems — the painted stage is default.
            ProGlassAutomation.Services.Theme.WallpaperOrchestrator
                .ApplyBackdropPreference(mainWindow);

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

            // FIRST-LAUNCH LANGUAGE OFFER.
            //
            // English is now the launch default everywhere. Previously the
            // app silently switched to the region's language -- once during
            // Initialize(), and again when GeoService resolved a second or
            // two later, flipping the whole UI mid-session. Neither asked.
            //
            // Shown here, after the shell is visible and has the foreground,
            // so it is a modal owned by a real window rather than a dialog
            // racing the splash. Asked at most ONCE per install: the flag is
            // written whichever way it is answered, including Escape or the
            // window's X.
            await ShowLanguageOfferIfPendingAsync(mainWindow);

            // A shared-root outage no longer blocks launch, but the operator
            // must know this session is using this PC's database. Show the
            // warning only after the shell is visible; never shut down or
            // interrupt the database initialization path.
            if (AppDataRoot.SharedRootFallbackActive &&
                Config.DataConnectivity?.SharedRootLocalModeNoticePending == true)
            {
                string localModeNotice = AppDataRoot.SharedRootFallbackReason +
                    "\n\nProGlass has continued in local mode, so you can use the application. " +
                    "Changes are saved on this workstation only and are not synchronized or merged " +
                    "automatically. Before reconnecting, review the dataset in Settings → Data. Use " +
                    "Prepare Folder only when the shared folder is empty and this local dataset should " +
                    "seed it; do not overwrite an existing company dataset.";
                try
                {
                    Prompts.Show(
                        localModeNotice,
                        "Local database mode",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    if (Config.DataConnectivity != null)
                    {
                        Config.DataConnectivity.SharedRootLocalModeNoticePending = false;
                        ConfigService.Save(Config);
                    }
                }
                catch (Exception noticeEx)
                {
                    // The informational warning must never turn a valid
                    // local-mode startup into a fatal startup failure.
                    AppLogService.Warning(
                        "Could not display the local-database-mode notice.",
                        noticeEx,
                        new LogContext { Module = "Database", Operation = "ShowLocalFallbackNotice" });
                }
            }

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
        /// <summary>
        /// Offers the detected region's language once, on first launch.
        ///
        /// LocalizationService.GetPendingRegionalSuggestion() owns every
        /// "should we ask?" rule; this method owns the UI and the
        /// persistence. It sets LanguagePromptShown regardless of the
        /// answer -- "keep English" is a real decision and must silence the
        /// prompt permanently -- and writes it BEFORE applying the language
        /// so a failure in the switch cannot cause the question to be asked
        /// again on the next launch.
        ///
        /// Never throws: a first-run nicety must not be able to break
        /// startup.
        /// </summary>
        private static async Task ShowLanguageOfferIfPendingAsync(Window owner)
        {
            try
            {
                var settings = Config?.Localization;
                if (settings == null) return;

                var suggested = Services.Localization.LocalizationService
                    .Instance.GetPendingRegionalSuggestion(settings);
                if (suggested == null) return;

                // Let the shell finish its first render before stealing
                // focus with a modal.
                await Task.Yield();

                var dialog = new Views.Shared.LanguageSuggestionDialog(suggested)
                {
                    Owner = owner
                };

                dialog.ShowDialog();

                // Asked. Never ask again, whichever way it went.
                settings.LanguagePromptShown = true;

                if (dialog.SwitchRequested)
                {
                    settings.Language = suggested.Code;
                    Services.Localization.LocalizationService.Instance
                        .ApplyLanguage(suggested.Code, persist: true);
                }

                Services.AI.AIConfig.ConfigService.Save(Config!);

                AppLogService.Info(
                    dialog.SwitchRequested
                        ? $"Language offer accepted: switched to {suggested.Code}."
                        : "Language offer declined: staying on English.",
                    new LogContext
                    {
                        Module = "Localization",
                        Operation = "FirstRunLanguageOffer"
                    });
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"First-run language offer failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Localization",
                        Operation = "FirstRunLanguageOffer"
                    });
            }
        }

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
    }
}
