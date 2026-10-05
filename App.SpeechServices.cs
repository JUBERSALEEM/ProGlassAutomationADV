// ═══════════════════════════════════════════════════════════════════════
// FILE: App.SpeechServices.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.5 — App partial split. This part owns the SpeechServices members.
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
    }
}
