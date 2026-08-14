// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIService/AiSettings.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Service (Chat 25 v3.0 — correct model defaults)
//
// CHANGES vs Chat 25 v1.0:
//   ✓ FIX: Model default changed from "gemini-flash-latest" (vague alias)
//     to "gemini-2.0-flash" (current stable free-tier model).
//   ✓ FIX: GroqModel default changed from "llama-3.1-70b-versatile"
//     (deprecated by Groq) to "llama-3.3-70b-versatile" (current).
//   ✓ FIX: CohereModel default changed from "command-r" to
//     "command-r-plus" (free tier eligible, better quality).
//   ✓ ALL other properties preserved exactly.
// ═══════════════════════════════════════════════════════════════════════

using System.Text.Json.Serialization;

namespace ProGlassAutomation.Services.AI.AIService
{
    public class AiSettings
    {
        /// <summary>
        /// Master AI enable/disable toggle.
        /// Default TRUE — AI works out of the box once API key entered.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Which AI provider is currently active. Default: Gemini.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AiProvider ActiveProvider { get; set; } = AiProvider.Gemini;

        // ── Per-provider API keys ──────────────────────────────────────

        public string GeminiApiKey { get; set; } = string.Empty;
        public string GroqApiKey { get; set; } = string.Empty;
        public string CohereApiKey { get; set; } = string.Empty;

        // ── Backward compat ────────────────────────────────────────────

        [JsonIgnore]
        public string ApiKey
        {
            get => ActiveProvider switch
            {
                AiProvider.Gemini => GeminiApiKey,
                AiProvider.Groq => GroqApiKey,
                AiProvider.Cohere => CohereApiKey,
                _ => GeminiApiKey
            };
            set
            {
                switch (ActiveProvider)
                {
                    case AiProvider.Gemini: GeminiApiKey = value; break;
                    case AiProvider.Groq: GroqApiKey = value; break;
                    case AiProvider.Cohere: CohereApiKey = value; break;
                    default: GeminiApiKey = value; break;
                }
            }
        }

        // ── Per-provider models ────────────────────────────────────────

        /// <summary>
        /// Gemini model.
        /// FIX: Changed from "gemini-flash-latest" (vague alias that
        /// occasionally resolves to older models) to "gemini-2.0-flash"
        /// which is the current stable free-tier model with confirmed
        /// 15 req/min 1500/day quota.
        /// Upgrade path: "gemini-2.5-flash" for higher quality (same quota).
        /// </summary>
        public string Model { get; set; } = "gemini-2.0-flash";

        /// <summary>
        /// Groq model.
        /// FIX: Changed from "llama-3.1-70b-versatile" (deprecated by Groq
        /// as of late 2024) to "llama-3.3-70b-versatile" (current, faster,
        /// better quality, same free tier quota).
        /// </summary>
        public string GroqModel { get; set; } = "llama-3.3-70b-versatile";

        /// <summary>
        /// Cohere model.
        /// FIX: Changed from "command-r" to "command-r-plus".
        /// command-r-plus is free tier eligible and has significantly
        /// better reasoning, instruction following, and RAG performance.
        /// </summary>
        public string CohereModel { get; set; } = "command-r-plus";

        // ── Shared tuning ──────────────────────────────────────────────

        public int MaxTokens { get; set; } = 1024;
        public double Temperature { get; set; } = 0.7;

        // ── RAG toggle ─────────────────────────────────────────────────

        public bool UseRealData { get; set; } = true;

        // ── Whisper model selection ────────────────────────────────────

        /// <summary>
        /// Selected Whisper model key: "base", "medium", "large-v3".
        /// Default "base" — 142MB, 95%+ accuracy, fast inference.
        /// </summary>
        public string SelectedWhisperModel { get; set; } = "base";

        // ── Secret storage policy ──────────────────────────────────────

        /// <summary>
        /// v2 security: when true, API keys are kept in the DPAPI
        /// SecretVault between sessions (encrypted, current Windows user
        /// only). When false, keys live only in memory until the app
        /// closes — the user re-enters them next session.
        /// Default true so existing users are not logged out of their keys.
        /// </summary>
        public bool RememberApiKeys { get; set; } = true;

        // ── Helpers ────────────────────────────────────────────────────

        public string GetApiKeyForProvider(AiProvider provider) => provider switch
        {
            AiProvider.Gemini => GeminiApiKey,
            AiProvider.Groq => GroqApiKey,
            AiProvider.Cohere => CohereApiKey,
            _ => string.Empty
        };

        public string GetModelForProvider(AiProvider provider) => provider switch
        {
            AiProvider.Gemini => Model,
            AiProvider.Groq => GroqModel,
            AiProvider.Cohere => CohereModel,
            _ => Model
        };
    }
}