// ═══════════════════════════════════════════════════════════════════════
// FILE: App.CrashHelpers.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.5 — App partial split. This part owns the CrashHelpers members.
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
        // ══════════════════════════════════════════════════════════════
        // CRASH LATCH (2026-10-06, ERR-7F77C4)
        //
        // A crash report recovered from the field showed ~90 "Unhandled UI
        // exception" entries in 4.3 seconds, each followed by "Forcing exit",
        // and the process still had not died. The cause is an amplification
        // loop, not 90 separate faults:
        //
        //   render pass throws → DispatcherUnhandledException → log Critical
        //   + build a full crash report + write it to disk + fire an HTTP
        //   POST + construct and ShowDialog a CrashReportDialog (a Window,
        //   which needs to render) + arm another watchdog thread
        //   → args.Handled = true keeps the dispatcher pumping
        //   → the next render pass throws → repeat
        //
        // When the original exception is OutOfMemoryException this is
        // actively destructive: every iteration allocates to log, to build
        // the report and to build a dialog, so the handler itself consumes
        // the memory whose absence caused the crash. The fault that should
        // have produced one report and a fast exit instead produced ~90 of
        // each and a four-second death spiral.
        //
        // The latch makes the FIRST crash do the full treatment and every
        // later one return immediately, so the process can actually exit.
        // ══════════════════════════════════════════════════════════════

        /// <summary>0 until the first crash has been taken; 1 afterwards.</summary>
        private static int _crashTaken;

        /// <summary>Crashes swallowed after the first. Counted, not logged.</summary>
        private static int _suppressedCrashes;

        /// <summary>Ensures the "suppressing further" line is written once.</summary>
        private static int _suppressionNoted;

        /// <summary>Ensures only one watchdog thread is ever started.</summary>
        private static int _watchdogArmed;

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

        /// <summary>
        /// Deterministic process exit after an unhandled UI-thread crash.
        /// The dispatcher is in an undefined state once a crash has been
        /// swallowed, so continuing leaves the process lingering in Task
        /// Manager after the window closes. Request a clean shutdown first
        /// (OnExit still flushes config / audit / backup), then arm a
        /// background watchdog that force-exits if anything keeps the
        /// process alive past the grace period.
        /// </summary>
        private static void ForceExitAfterCrash(string errorId)
        {
            try
            {
                ProGlassAutomation.Services.Logging.AppLogService.Info(
                    "Forcing exit after unhandled UI crash (error " + errorId + ")",
                    new ProGlassAutomation.Services.Logging.LogContext
                    {
                        Operation = "UnhandledException"
                    });

                Application.Current?.Shutdown();
            }
            catch
            {
                // Shutdown may itself fail on a wedged dispatcher — the
                // watchdog below is the hard guarantee.
            }

            // One watchdog, ever. This method is reachable from four crash
            // entry points and was previously called once per unhandled
            // exception, so a crash storm spawned a thread per exception.
            if (Interlocked.Exchange(ref _watchdogArmed, 1) != 0) return;

            try
            {
                var watchdog = new Thread(() =>
                {
                    Thread.Sleep(3000);
                    try
                    {
                        // Non-zero exit code: an abnormal termination, so
                        // Task Scheduler / startup policies treat it as a
                        // crash rather than a clean close.
                        Environment.Exit(1);
                    }
                    catch
                    {
                        // Last-resort path — nothing further we can do.
                    }
                })
                {
                    IsBackground = true,
                    Name = "CrashForceExit"
                };
                watchdog.Start();
            }
            catch
            {
                // Watchdog spawn failure — Shutdown above is the only
                // remaining exit path.
            }
        }
    }
}
