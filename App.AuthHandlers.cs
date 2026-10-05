// ═══════════════════════════════════════════════════════════════════════
// FILE: App.AuthHandlers.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.5 — App partial split. This part owns the AuthHandlers members.
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

    }
}
