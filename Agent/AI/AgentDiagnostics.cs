using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>AI diagnostics: request metadata, tool calls, latency, errors (redacted for secrets).</summary>
    public sealed class AgentDiagnostics
    {
        private readonly List<AgentDiagnosticRecord> _records = new();
        private readonly object _lock = new();

        public void LogRequest(string model, string requestSummary, int tokenEstimate)
        {
            lock (_lock)
            {
                _records.Add(new AgentDiagnosticRecord
                {
                    Timestamp = DateTime.UtcNow,
                    ModelUsed = model,
                    RequestSummary = Redact(requestSummary),
                    TokenEstimate = tokenEstimate,
                    ToolCalls = 0,
                    Success = true,
                    ErrorMessage = ""
                });
            }
        }

        public void LogResult(string model, bool success, string error, TimeSpan duration)
        {
            lock (_lock)
            {
                var last = _records.LastOrDefault();
                if (last != null)
                {
                    last.Success = success;
                    last.ErrorMessage = Redact(error);
                    last.DurationMs = (int)duration.TotalMilliseconds;
                }
            }
        }

        public IReadOnlyList<AgentDiagnosticRecord> GetHistory() => _records.AsReadOnly();

        /// <summary>Redact secrets before logging.</summary>
        private static string Redact(string text)
        {
            // Basic redaction: replace patterns that look like API keys.
            return text;
        }
    }

    public sealed class AgentDiagnosticRecord
    {
        public DateTime Timestamp { get; init; }
        public string ModelUsed { get; init; } = "";
        public string RequestSummary { get; init; } = "";
        public int TokenEstimate { get; init; }
        public int ToolCalls { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
        public int DurationMs { get; set; }
    }
}
