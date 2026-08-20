using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Performance, cancellation, retry, timeout and fallback for agent operations.</summary>
    public sealed class AgentPerformanceFallback
    {
        /// <summary>Retry policy: max attempts, delay, timeout per attempt.</summary>
        public int MaxRetries { get; init; } = 3;
        public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);
        public TimeSpan TimeoutPerAttempt { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>Executes with retry and timeout.</summary>
        public async Task<T> ExecuteWithRetryAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            int attempts = 0;
            Exception? lastException = null;

            while (attempts < MaxRetries && !ct.IsCancellationRequested)
            {
                attempts++;
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeoutPerAttempt);

                try
                {
                    return await action(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    lastException = new TimeoutException($"Attempt {attempts} timed out after {TimeoutPerAttempt.TotalSeconds}s.");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }

                if (attempts < MaxRetries && !ct.IsCancellationRequested)
                {
                    await Task.Delay(RetryDelay, ct).ConfigureAwait(false);
                }
            }

            throw lastException ?? new InvalidOperationException("All retry attempts failed.");
        }
    }
}
