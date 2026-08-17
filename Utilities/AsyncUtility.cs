// ═══════════════════════════════════════════════════════════════════════
// FILE: Utilities/AsyncUtility.cs
// PROJECT: ProGlassAutomation
//
// A single, reusable fire-and-forget helper. Before this existed, several
// ViewModels each declared their own private "static async void
// FireAndForget(Func<Task>)" that only differed by a log-prefix string.
// This centralises that pattern so:
//   • an exception in a background task is always logged (never silently
//     swallowed, and never crashing the caller),
//   • there is one implementation to keep consistent,
//   • callers stay on the UI thread where WPF bindings require it.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Threading.Tasks;

namespace ProGlassAutomation.Utilities
{
    /// <summary>Async helper utilities.</summary>
    public static class AsyncUtility
    {
        /// <summary>
        /// Runs <paramref name="work"/> without awaiting it, catching and
        /// logging any exception so it cannot crash the caller and is not
        /// silently dropped. <paramref name="logMessage"/> prefixes the log
        /// entry so a failure is attributable to the caller.
        /// </summary>
        public static async void SafeFireAndForget(
            Func<Task> work, string logMessage)
        {
            try
            {
                if (work == null) return;
                await work().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ProGlassAutomation.Services.Logging.AppLogService.Error(
                    logMessage ?? "Fire-and-forget task failed", ex);
            }
        }
    }
}
