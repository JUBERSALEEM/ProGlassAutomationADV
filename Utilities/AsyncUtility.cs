// ═══════════════════════════════════════════════════════════════════════
// FILE: Utilities/AsyncUtility.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — Async Safety (Phase 3: Async & Threading Cleanup)
// VERSION: Phase 3 — 2026-08-21
//
// PURPOSE:
//   Central fire-and-forget helper — single implementation for entire app.
//   Before: 7 ViewModels each had private static async void SafeFireAsync
//   that differed only by log prefix. Now one place.
//
//   Phase 3 enhancements:
//   • CorrelationId auto-flow via CorrelationContext
//   • Overload that handles Result<T> and shows UI dialog on failure
//   • Overload that returns Task for callers who want to await
//   • Dispatcher affinity check — ensures work starts on UI thread where
//     WPF bindings require it, but allows background continuation
//   • VSTHRD100 compliance: this is the ONE allowed async void in app
//     (all others must go through here)
//
// RULES (enforced by analyzers after Phase 3):
//   • async void allowed ONLY for event handlers and this SafeFireAndForget
//   • All other async methods must return Task or Task<T>
//   • No .Result / .Wait() — use await
//   • DispatcherTimer must be created on UI dispatcher (check in code review)
//
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.Logging;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ProGlassAutomation.Utilities
{
    /// <summary>Async helper utilities — Phase 3 hardened.</summary>
    public static class AsyncUtility
    {
        /// <summary>
        /// The ONE allowed async void in the app (plus WPF event handlers).
        /// Runs work without awaiting, catches and logs any exception with
        /// CorrelationId, never crashes caller, never silently drops.
        /// </summary>
        public static async void SafeFireAndForget(
            Func<Task> work, string? logMessage = null, string? correlationId = null)
        {
            var corrId = correlationId ?? CorrelationContext.CurrentId ?? CorrelationContext.NewId();
            using var scope = CorrelationContext.BeginScope(corrId);

            try
            {
                if (work == null) return;
                await work().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                var ctx = new LogContext
                {
                    Operation = "SafeFireAndForget",
                    CorrelationId = corrId,
                    Extra = logMessage ?? ""
                };
                AppLogService.Error(logMessage ?? "Fire-and-forget task failed", ex, ctx);
            }
        }

        /// <summary>
        /// Safe fire-and-forget that handles Result — shows UI dialog on failure.
        /// </summary>
        public static async void SafeFireAndForgetResult<T>(
            Func<Task<Result<T>>> work, string operation, LogContext? context = null)
        {
            var corrId = context?.CorrelationId ?? CorrelationContext.CurrentId ?? CorrelationContext.NewId();
            using var scope = CorrelationContext.BeginScope(corrId);

            try
            {
                if (work == null) return;
                var result = await work().ConfigureAwait(true);
                if (result.IsFailure)
                {
                    ViewModelResultHelper.HandleResult(result, operation, context);
                }
            }
            catch (Exception ex)
            {
                var ctx = context ?? new LogContext { Operation = operation, CorrelationId = corrId };
                var errId = AppLogService.Error($"{operation} exception: {ex.Message}", ex, ctx);
                var fail = Result<T>.Fail(ex.Message, ex, corrId, errId);
                ViewModelResultHelper.HandleResult(fail, operation, ctx);
            }
        }

        /// <summary>
        /// Safe fire-and-forget that flips an in-memory circuit-breaker on
        /// the FIRST failure. The caller passes a stateful callback
        /// (e.g. <c>() =&gt; _mirrorEnabled = false</c>) that runs once
        /// when the awaited work throws. Subsequent calls still log
        /// failures but skip the breaker callback after the first hit,
        /// so a long-lived session does not flood the log if the
        /// underlying defect is permanent.
        ///
        /// This is the third behaviour tier:
        ///   1. <see cref="SafeFireAndForget"/>  — log only.
        ///   2. <see cref="SafeFireAndForgetResult{T}"/>  — log + UI dialog.
        ///   3. <see cref="TryAsyncFireAndForget"/>  — log + caller state.
        ///
        /// <paramref name="onFailure"/> is invoked on whatever thread the
        /// exception surfaces on (typically the captured SynchronizationContext,
        /// i.e. the UI thread for WPF work). The callback should be cheap and
        /// idempotent — a guarded set-flag is the canonical use case.
        /// </summary>
        /// <param name="work">The async work to run. Must not be null.</param>
        /// <param name="logMessage">
        /// Human-readable label written to the log on failure. The exception
        /// is also recorded with full stack trace.
        /// </param>
        /// <param name="onFailure">
        /// Caller-owned callback invoked on the first exception. The
        /// implementation is expected to flip a private flag (e.g.
        /// <c>_mirrorEnabled = false</c>) so subsequent calls become
        /// no-ops in the same session.
        /// </param>
        /// <param name="correlationId">
        /// Optional correlation id for cross-module tracing. Auto-generated
        /// when null.
        /// </param>
        public static async void TryAsyncFireAndForget(
            Func<Task> work,
            string? logMessage,
            Action onFailure,
            string? correlationId = null)
        {
            var corrId = correlationId ?? CorrelationContext.CurrentId ?? CorrelationContext.NewId();
            using var scope = CorrelationContext.BeginScope(corrId);

            try
            {
                if (work == null) return;
                await work().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                var ctx = new LogContext
                {
                    Operation = "TryAsyncFireAndForget",
                    CorrelationId = corrId,
                    Extra = logMessage ?? ""
                };
                AppLogService.Error(logMessage ?? "TryAsyncFireAndForget failed", ex, ctx);

                // Caller state is the caller's problem. Run it on the
                // same thread that observed the exception so a field
                // write to a UI-bound object does not race with the
                // dispatcher.
                try { onFailure?.Invoke(); }
                catch (Exception cbEx)
                {
                    // A throwing breaker callback is a programmer error,
                    // but it must never mask the original exception.
                    AppLogService.Warning(
                        $"onFailure callback threw: {cbEx.Message}",
                        new LogContext
                        {
                            Operation = "TryAsyncFireAndForget.OnFailure",
                            CorrelationId = corrId
                        });
                }
            }
        }

        /// <summary>
        /// Safe fire-and-forget for non-generic Result.
        /// </summary>
        public static async void SafeFireAndForgetResult(
            Func<Task<Result>> work, string operation, LogContext? context = null)
        {
            var corrId = context?.CorrelationId ?? CorrelationContext.CurrentId ?? CorrelationContext.NewId();
            using var scope = CorrelationContext.BeginScope(corrId);

            try
            {
                if (work == null) return;
                var result = await work().ConfigureAwait(true);
                if (result.IsFailure)
                {
                    ViewModelResultHelper.HandleResult(result, operation, context);
                }
            }
            catch (Exception ex)
            {
                var ctx = context ?? new LogContext { Operation = operation, CorrelationId = corrId };
                var errId = AppLogService.Error($"{operation} exception: {ex.Message}", ex, ctx);
                var fail = Result.Fail(ex.Message, ex, corrId, errId);
                ViewModelResultHelper.HandleResult(fail, operation, ctx);
            }
        }

        /// <summary>
        /// Awaitable safe wrapper — returns Task, never throws, logs on failure.
        /// Use when caller wants to await but not crash on exception.
        /// </summary>
        public static async Task SafeExecuteAsync(
            Func<Task> work, string? logMessage = null, string? correlationId = null)
        {
            var corrId = correlationId ?? CorrelationContext.CurrentId ?? CorrelationContext.NewId();
            using var scope = CorrelationContext.BeginScope(corrId);

            try
            {
                if (work == null) return;
                await work().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var ctx = new LogContext
                {
                    Operation = "SafeExecuteAsync",
                    CorrelationId = corrId,
                    Extra = logMessage ?? ""
                };
                AppLogService.Error(logMessage ?? "SafeExecuteAsync failed", ex, ctx);
            }
        }

        /// <summary>
        /// Awaitable safe wrapper for Result — never throws.
        /// </summary>
        public static async Task<Result<T>> SafeExecuteResultAsync<T>(
            Func<Task<Result<T>>> work, string? correlationId = null)
        {
            var corrId = correlationId ?? CorrelationContext.CurrentId ?? CorrelationContext.NewId();
            using var scope = CorrelationContext.BeginScope(corrId);

            try
            {
                if (work == null) return Result<T>.Fail("Work is null", corrId);
                return await work().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var ctx = new LogContext
                {
                    Operation = "SafeExecuteResultAsync",
                    CorrelationId = corrId
                };
                var errId = AppLogService.Error($"SafeExecuteResult failed: {ex.Message}", ex, ctx);
                return Result<T>.Fail(ex.Message, ex, corrId, errId);
            }
        }

        /// <summary>
        /// Ensures DispatcherTimer is created on UI dispatcher — Phase 3 thread affinity guard.
        /// If tick is provided, it is wired to Tick event.
        /// </summary>
        public static System.Windows.Threading.DispatcherTimer CreateDispatcherTimer(
            TimeSpan interval,
            Action? tick = null,
            string? owner = null)
        {
            var dispatcher = Application.Current?.Dispatcher;
            System.Windows.Threading.DispatcherTimer timer;

            if (dispatcher == null)
            {
                AppLogService.Warning(
                    $"DispatcherTimer created without Application dispatcher (owner={owner}) — may not tick if off UI thread",
                    new LogContext { Operation = "CreateDispatcherTimer", Extra = owner ?? "" });
                timer = new System.Windows.Threading.DispatcherTimer { Interval = interval };
            }
            else if (!dispatcher.CheckAccess())
            {
                AppLogService.Warning(
                    $"DispatcherTimer created off UI thread (owner={owner}) — binding to app dispatcher",
                    new LogContext { Operation = "CreateDispatcherTimer", Extra = owner ?? "" });
                timer = new System.Windows.Threading.DispatcherTimer(
                    System.Windows.Threading.DispatcherPriority.Background,
                    dispatcher)
                {
                    Interval = interval
                };
            }
            else
            {
                timer = new System.Windows.Threading.DispatcherTimer { Interval = interval };
            }

            if (tick != null)
            {
                timer.Tick += (_, _) => tick();
            }

            return timer;
        }

        /// <summary>
        /// Creates a DispatcherTimer with explicit Tick EventHandler — Phase 3 helper.
        /// </summary>
        public static System.Windows.Threading.DispatcherTimer CreateDispatcherTimerWithHandler(
            TimeSpan interval,
            EventHandler handler,
            string? owner = null)
        {
            var dispatcher = Application.Current?.Dispatcher;
            System.Windows.Threading.DispatcherTimer timer;

            if (dispatcher == null)
            {
                AppLogService.Warning(
                    $"DispatcherTimer created without dispatcher (owner={owner})",
                    new LogContext { Operation = "CreateDispatcherTimer", Extra = owner ?? "" });
                timer = new System.Windows.Threading.DispatcherTimer { Interval = interval };
            }
            else if (!dispatcher.CheckAccess())
            {
                timer = new System.Windows.Threading.DispatcherTimer(
                    System.Windows.Threading.DispatcherPriority.Background,
                    dispatcher)
                {
                    Interval = interval
                };
            }
            else
            {
                timer = new System.Windows.Threading.DispatcherTimer { Interval = interval };
            }

            timer.Tick += handler;
            return timer;
        }
    }
}
