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
            SpeakLive(narration);
            events.Add(new VerifiedEvent
            {
                Id = id,
                Title = title,
                Narration = narration,
                Evidence = evidence
            });
            await WpfUiDriver.DwellAsync(dwellMs, ct).ConfigureAwait(false);
        }

        public static void SpeakLive(string text)
        {
            try { App.TextToSpeech?.SpeakAsync(text); }
            catch { /* live voice optional */ }
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
