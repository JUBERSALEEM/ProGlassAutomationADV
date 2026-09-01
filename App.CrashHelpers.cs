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
    }
}
