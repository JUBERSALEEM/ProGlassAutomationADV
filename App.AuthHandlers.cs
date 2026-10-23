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
        private static int _taxInvoiceReconciliationScheduled;

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

            // The startup sweep can repair DN links, so it is a write path.
            // Schedule it only for an active user with the narrow finance-link
            // permission; a viewer signing in first must not consume the one
            // successful-sweep slot. A failed sweep clears the slot so a later
            // eligible CurrentUserChanged event can retry it.
            if (AuditService.CurrentUser?.IsActive == true
                && PermissionService.Can(AppPermissions.DeliveryNoteInvoiceLink)
                && System.Threading.Interlocked.CompareExchange(
                    ref _taxInvoiceReconciliationScheduled, 1, 0) == 0)
            {
                ProGlassAutomation.Utilities.AsyncUtility.SafeFireAndForget(
                    async () =>
                    {
                        bool succeeded = false;
                        try
                        {
                            var corrId = ProGlassAutomation.Services.Common
                                .CorrelationContext.NewIdFor("Reconciler", "PostLogin");
                            using var scope = ProGlassAutomation.Services.Common
                                .CorrelationContext.BeginScope(corrId, null, "Reconciler");
                            var result = await ProGlassAutomation.Services.TaxInvoice.Repository
                                .TaxInvoiceDnReconciler.ReconcileWithResultAsync(corrId);
                            succeeded = result.IsSuccess;
                            if (result.IsFailure)
                            {
                                AppLogService.Warning(
                                    $"Post-login TI↔DN reconciliation failed [{corrId}]: " +
                                    $"{result.Error} (ErrorId={result.ErrorId})",
                                    new LogContext
                                    {
                                        Module = "TaxInvoice",
                                        Operation = "PostLoginReconcile",
                                        CorrelationId = corrId
                                    });
                            }
                            else if (result.Value > 0)
                            {
                                AppLogService.Info(
                                    $"Post-login TI↔DN reconciliation repaired {result.Value} link(s) [{corrId}]",
                                    new LogContext
                                    {
                                        Module = "TaxInvoice",
                                        Operation = "PostLoginReconcile",
                                        CorrelationId = corrId,
                                        Extra = $"Repaired={result.Value}"
                                    });
                            }
                        }
                        finally
                        {
                            if (!succeeded)
                                System.Threading.Interlocked.Exchange(
                                    ref _taxInvoiceReconciliationScheduled, 0);
                        }
                    },
                    "Post-login TI↔DN reconciliation failed");
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
