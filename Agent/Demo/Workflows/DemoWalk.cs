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
        public const int BeatMs = 2200;
        public const int HoldMs = 2800;

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
            var spoken = await SpeakAndWaitAsync(narration, ct).ConfigureAwait(false);
            events.Add(new VerifiedEvent
            {
                Id = id,
                Title = title,
                Narration = narration,
                Evidence = evidence,
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
            catch { /* live voice optional */ }
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

            try { tts.Stop(); } catch { /* cancel previous beat */ }
            var started = DateTime.UtcNow;
            try { tts.SpeakAsync(text); }
            catch { return TimeSpan.Zero; }

            var startUntil = DateTime.UtcNow.AddMilliseconds(900);
            while (!tts.IsSpeaking && DateTime.UtcNow < startUntil)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(40, ct).ConfigureAwait(false);
            }

            var hardStop = DateTime.UtcNow.AddSeconds(40);
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
                catch { /* command must not kill the walk */ }
                return true;
            }, ct).ConfigureAwait(false);
        }
    }
}
