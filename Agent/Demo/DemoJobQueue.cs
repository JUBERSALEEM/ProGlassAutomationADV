using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo
{
    /// <summary>
    /// One demo at a time. Two jobs must not fight over the live window.
    /// </summary>
    public sealed class DemoJobQueue
    {
        public static DemoJobQueue Shared { get; } = new();

        private readonly object _gate = new();
        private readonly Queue<string> _pending = new();
        private bool _pumping;

        // 2026-08-25: Record-All cancel. DemoDirector.Cancel sets this;
        // the pump drops everything still queued instead of starting the
        // next module after the current one stops.
        private bool _stopAll;

        public int PendingCount
        {
            get { lock (_gate) return _pending.Count; }
        }

        /// <summary>Stops the batch: no further queued jobs will start.</summary>
        public void StopAll()
        {
            lock (_gate) _stopAll = true;
        }

        public Task EnqueueAsync(string? argument)
        {
            lock (_gate)
            {
                // A fresh Generate press re-arms the batch.
                _stopAll = false;
                foreach (string item in Expand(argument))
                    _pending.Enqueue(item);
                if (_pumping) return Task.CompletedTask;
                _pumping = true;
            }
            return PumpAsync();
        }

        /// <summary>
        /// Generate All becomes one recorded job per catalog module.
        /// An empty argument stays a single default (Proforma Invoice).
        /// </summary>
        public static IReadOnlyList<string> Expand(string? argument)
        {
            string raw = (argument ?? "").Trim();
            if (string.Equals(raw, "All", StringComparison.OrdinalIgnoreCase))
                return DemoWorkflowCatalog.All.Select(w => w.Id).ToList();
            return new[] { raw };
        }

        private async Task PumpAsync()
        {
            try
            {
                while (true)
                {
                string next;
                lock (_gate)
                {
                    if (_stopAll)
                    {
                        _pending.Clear();
                        _pumping = false;
                        return;
                    }
                    if (_pending.Count == 0)
                    {
                        _pumping = false;
                        return;
                    }
                    next = _pending.Dequeue();
                }

                    try
                    {
                        await Tools.Demo.CreateDemoPackageTool.SharedDirector
                            .RunAsync(next)
                            .ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // Director already records Failed. Keep the queue moving.
                    }
                }
            }
            finally
            {
                lock (_gate) _pumping = false;
            }
        }
    }
}
