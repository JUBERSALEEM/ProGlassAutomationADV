using System;
using System.Collections.Generic;
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

        public int PendingCount
        {
            get { lock (_gate) return _pending.Count; }
        }

        public Task EnqueueAsync(string? argument)
        {
            lock (_gate)
            {
                _pending.Enqueue(argument ?? "");
                if (_pumping) return Task.CompletedTask;
                _pumping = true;
            }
            return PumpAsync();
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
