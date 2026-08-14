// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIService/IAiStreamingService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Service — streaming capability (v3 JARVIS pass)
//
// ADDITIVE: providers that can stream tokens implement this OPTIONAL
// interface. The chat view-model checks "ai is IAiStreamingService"
// and falls back to IAiService.AskAsync for providers that don't.
// Nothing existing changes; Groq/Cohere keep working untouched.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.AI.AIService
{
    /// <summary>
    /// Optional streaming capability for AI providers. Streaming shows
    /// the answer token-by-token as it is generated — perceived latency
    /// drops from "wait for the whole answer" (10-30s of dead silence)
    /// to "first words appear in ~1-2s".
    /// </summary>
    public interface IAiStreamingService
    {
        /// <summary>
        /// Streams a completion. <paramref name="onDelta"/> is invoked
        /// with the FULL accumulated text after every chunk arrives
        /// (the UI simply replaces the bubble text). Called on a
        /// background thread — receivers must marshal to the UI thread.
        /// </summary>
        Task<AiStreamingResult> AskStreamingAsync(
            string systemPrompt,
            string userPrompt,
            Action<string> onDelta,
            CancellationToken cancellationToken);
    }

    /// <summary>Result of a streaming request.</summary>
    public sealed class AiStreamingResult
    {
        public bool Success { get; set; }

        /// <summary>Full (or partial, when cancelled/interrupted) text.</summary>
        public string Text { get; set; } = string.Empty;

        public string ErrorMessage { get; set; }

        public string ProviderName { get; set; }

        /// <summary>True when the user cancelled the generation.</summary>
        public bool WasCancelled { get; set; }
    }
}
