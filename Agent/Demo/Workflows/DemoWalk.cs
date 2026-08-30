using ProGlassAutomation.Agent.Demo.Automation;
using ProGlassAutomation.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Shared pacing, live TTS and UI-thread helpers for every recorded module.
    /// </summary>
    internal static class DemoWalk
    {
        /// <summary>
        /// Thread-local Random for jitter. Random is NOT thread-safe; each
        /// thread gets its own instance to avoid contention when the demo
        /// runs tasks on background threads.
        /// </summary>
        private static readonly ThreadLocal<Random> _jitter = new(() => new());

        /// <summary>Base beat between narrated steps (applied with jitter).</summary>
        public static int BeatMs => Jitter(2200);

        /// <summary>Hold time for important screens (applied with jitter).</summary>
        public static int HoldMs => Jitter(2800);

        /// <summary>Jitter a nominal millisecond delay ±20%.</summary>
        private static int Jitter(int ms)
        {
            var rng = _jitter.Value ?? new Random();
            return ms + (int)(ms * (rng.NextDouble() * 0.40 - 0.20));
        }

        /// <summary>
        /// Human-like typing delay: 60–160ms per keystroke. Call between
        /// each field fill so the demo doesn't set 9 fields at once.
        /// </summary>
        public static Task TypeDelayAsync(CancellationToken ct)
        {
            int delay = 60 + (_jitter.Value?.Next(100) ?? 60);
            return Task.Delay(delay, ct);
        }

        public static bool Navigate(string target)
        {
            try
            {
                if (ProGlassAutomation.Agent.Core.AgentHost.Navigate == null)
                    return false;
                ProGlassAutomation.Agent.Core.AgentHost.Navigate(target);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Lands the live shell on the catalog module BEFORE the first
        /// spoken beat. Features and License live inside Settings — a
        /// plain Navigate("Settings") would leave the Demo Center page
        /// on screen while the voice talks about the wrong section.
        /// </summary>
        public static bool LandOnModule(string? moduleId)
        {
            try
            {
                string id = (moduleId ?? "").Trim();
                if (string.Equals(id, "Features", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(id, "License", StringComparison.OrdinalIgnoreCase))
                {
                    return OpenSettingsPage(id);
                }
                // Generate is launched from Demo Center. A plain Navigate
                // to Settings would keep this page on screen. Land on
                // Appearance so the recording actually shows Settings.
                if (string.Equals(id, "Settings", StringComparison.OrdinalIgnoreCase))
                    return OpenSettingsPage("Appearance");

                var wf = DemoWorkflowCatalog.Find(id);
                string target = wf?.NavigateTarget;
                if (string.IsNullOrWhiteSpace(target))
                    target = string.IsNullOrWhiteSpace(id) ? "Dashboard" : id;
                return Navigate(target);
            }
            catch
            {
                return false;
            }
        }

        public static bool OpenSettingsPage(string page)
        {
            try
            {
                if (Application.Current?.MainWindow?.DataContext is MainViewModel main)
                {
                    main.OpenSettingsPage(page);
                    return true;
                }
                return Navigate("Settings");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Switches the Features page to one module's field toggles so
        /// the recording shows Proforma, Inquiry, Job Order, Delivery
        /// Note or Tax Invoice instead of a stacked wall of switches.
        /// </summary>
        public static bool SwitchFeaturesModule(string module)
        {
            try
            {
                if (Application.Current?.MainWindow?.DataContext is MainViewModel main)
                {
                    var cmd = main.SettingsVM?.SwitchFeaturesModuleCommand;
                    if (cmd != null && cmd.CanExecute(module))
                    {
                        cmd.Execute(module);
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static Task<MainViewModel?> MainAsync(CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var dc = Application.Current?.MainWindow?.DataContext;
                return dc as MainViewModel;
            }, ct);

        public static async Task PresentAsync(
            List<VerifiedEvent> events,
            string id, string title, string narration, string evidence,
            int dwellMs, CancellationToken ct)
        {
            DemoStage.Now(title + " — " + narration);
            // Let the just-navigated page paint before speech starts.
            // Without this the voice describes a screen the recorder has
            // not yet captured (voice ahead of the picture).
            await WpfUiDriver.WaitReadyAsync(220, ct).ConfigureAwait(false);
            var beatStartedUtc = DateTime.UtcNow;
            var spoken = await SpeakAndWaitAsync(narration, ct).ConfigureAwait(false);
            events.Add(new VerifiedEvent
            {
                Id = id,
                Title = title,
                Narration = narration,
                Evidence = evidence,
                Utc = beatStartedUtc,
                SpokenDuration = spoken
            });
            int settle = spoken > TimeSpan.Zero
                ? Math.Min(450, Math.Max(200, dwellMs / 6))
                : Math.Max(dwellMs, BeatMs);
            await WpfUiDriver.DwellAsync(settle, ct).ConfigureAwait(false);
        }

        public static void SpeakLive(string text)
        {
            try { App.TextToSpeech?.SpeakAsync(text); }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DemoWalk] SpeakLive: live voice optional: " + ex.Message); }
        }

        /// <summary>
        /// Cancel any queued speech, speak this line, and wait until it
        /// finishes. Fire-and-forget SpeakAsync was still reading step 1
        /// while the walk was already on step 5.
        /// </summary>
        public static async Task<TimeSpan> SpeakAndWaitAsync(
            string text, CancellationToken ct)
        {
            var tts = App.TextToSpeech;
            if (tts == null || !tts.IsAvailable || string.IsNullOrWhiteSpace(text))
                return TimeSpan.Zero;

            try { tts.Stop(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DemoWalk] SpeakAndWaitAsync: cancel previous beat: " + ex.Message); }
            var started = DateTime.UtcNow;
            try { tts.SpeakAsync(text); }
            catch { return TimeSpan.Zero; }

            int words = text.Split((char[]?)null,
                StringSplitOptions.RemoveEmptyEntries).Length;
            // ~150 words/minute plus a short breath. Used when SAPI never
            // raises IsSpeaking so the picture does not jump ahead of the
            // still-queued sentence.
            int estimatedMs = Math.Clamp(words * 400, 2800, 90_000);

            var startUntil = DateTime.UtcNow.AddMilliseconds(1200);
            while (!tts.IsSpeaking && DateTime.UtcNow < startUntil)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(40, ct).ConfigureAwait(false);
            }

            if (!tts.IsSpeaking)
            {
                await Task.Delay(estimatedMs, ct).ConfigureAwait(false);
                var silentTook = DateTime.UtcNow - started;
                return silentTook > TimeSpan.FromMilliseconds(200)
                    ? silentTook
                    : TimeSpan.FromMilliseconds(estimatedMs);
            }

            // Long technical narration regularly exceeded the fixed 40-second
            // cutoff. The next beat then called Stop(), cutting the sentence
            // while the picture advanced. Budget from word count with a safe
            // ceiling instead of truncating legitimate agent speech.
            double budgetSeconds = Math.Clamp(words / 1.7 + 15, 45, 120);
            var hardStop = DateTime.UtcNow.AddSeconds(budgetSeconds);
            while (tts.IsSpeaking && DateTime.UtcNow < hardStop)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(80, ct).ConfigureAwait(false);
            }

            var took = DateTime.UtcNow - started;
            return took > TimeSpan.FromMilliseconds(200) ? took : TimeSpan.Zero;
        }

        public static async Task<bool> WaitUntilAsync(
            Func<bool> test, int timeoutMs, CancellationToken ct)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < until)
            {
                ct.ThrowIfCancellationRequested();
                if (await WpfUiDriver.OnUiAsync(test, ct).ConfigureAwait(false))
                    return true;
                await Task.Delay(150, ct).ConfigureAwait(false);
            }
            return await WpfUiDriver.OnUiAsync(test, ct).ConfigureAwait(false);
        }

        public static async Task TryCommandAsync(
            ICommand? command, object? parameter, CancellationToken ct)
        {
            if (command == null) return;
            await WpfUiDriver.OnUiAsync(() =>
            {
                try
                {
                    if (command.CanExecute(parameter))
                        command.Execute(parameter);
                }
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[DemoWalk] TryCommandAsync: command must not kill the walk: " + ex.Message); }
                return true;
            }, ct).ConfigureAwait(false);
        }
    }
}
