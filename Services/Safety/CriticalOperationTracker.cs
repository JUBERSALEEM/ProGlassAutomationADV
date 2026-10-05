// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Safety/CriticalOperationTracker.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — shutdown safety (audit finding X-6)
//
// WHAT WAS WRONG
// ──────────────
// MainWindow had no Closing handler and App had no SessionEnding
// handler. If the user clicked the X — or Windows began shutting down —
// while a save, a PDF export or a backup was mid-flight, the process
// terminated immediately. The in-flight write was cut at whatever point
// it had reached.
//
// The 11 SQLite databases each survive this on their own: SQLite is
// atomic per transaction, so a killed write rolls back rather than
// leaving a torn row. What is NOT protected is a MULTI-STEP business
// operation. Creating a tax invoice from a delivery note touches
// taxinvoices.db AND deliverynotes.db. Those live in separate database
// files, so no single transaction spans them (audit finding D-4). Kill
// the process between the two writes and the books disagree: a delivery
// note marked invoiced with no invoice to match it.
//
// WHAT THIS DOES
// ──────────────
// A counter of critical operations currently running. Business code
// wraps a multi-step write in a `using` scope. The window's Closing
// handler asks whether anything is in flight and, if so, tells the user
// exactly what and lets them wait.
//
// WHY A COUNTER AND NOT A LOCK
// ────────────────────────────
// The goal is not mutual exclusion — concurrent saves are legitimate.
// The goal is to answer one question at shutdown: "is it safe to stop
// right now?" A counter answers that; a lock would change the app's
// concurrency behaviour, which is out of scope for a safety fix.
//
// Interlocked is used because operations can start and finish on
// background threads while the UI thread reads the count.
//
// DELIBERATE LIMITATION
// ─────────────────────
// This cannot protect against power loss or Task Manager "End task".
// Nothing in user space can. It protects against the common, avoidable
// case: a user closing the window at the wrong moment.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ProGlassAutomation.Services.Safety
{
    /// <summary>
    /// Tracks business operations that must not be interrupted.
    /// Thread-safe. Never throws.
    /// </summary>
    public static class CriticalOperationTracker
    {
        private static int _count;
        private static readonly object _sync = new();
        private static readonly List<string> _active = new();

        /// <summary>True when at least one critical operation is running.</summary>
        public static bool IsBusy => Volatile.Read(ref _count) > 0;

        /// <summary>Number of critical operations currently running.</summary>
        public static int ActiveCount => Volatile.Read(ref _count);

        /// <summary>
        /// Names of the running operations, for the shutdown prompt.
        /// </summary>
        public static IReadOnlyList<string> ActiveOperations
        {
            get { lock (_sync) return _active.ToList(); }
        }

        /// <summary>
        /// Marks the start of an uninterruptible operation. Dispose the
        /// returned scope when it completes.
        /// </summary>
        /// <example>
        /// using (CriticalOperationTracker.Begin("Saving tax invoice"))
        /// {
        ///     await repo.InsertAsync(invoice);
        ///     await deliveryRepo.MarkInvoicedAsync(dnId);
        /// }
        /// </example>
        public static IDisposable Begin(string operationName)
            => new Scope(operationName);

        private sealed class Scope : IDisposable
        {
            private readonly string _name;
            private bool _disposed;

            internal Scope(string name)
            {
                _name = string.IsNullOrWhiteSpace(name)
                    ? "Background operation"
                    : name;

                Interlocked.Increment(ref _count);
                lock (_sync) _active.Add(_name);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                Interlocked.Decrement(ref _count);
                lock (_sync) _active.Remove(_name);
            }
        }
    }
}
