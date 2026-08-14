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
        /// v2 FIX: "gemini-2.0-flash" was SHUT DOWN by Google on
        /// 2026-06-01. Default is now "gemini-3.5-flash", Google's
        /// recommended replacement and the current stable free-tier
        /// model. ConfigService auto-migrates any saved 2.0.x value.
        /// </summary>
        public string Model { get; set; } = "gemini-3.5-flash";

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

        // ── v3 JARVIS pass toggles ─────────────────────────────────────

        /// <summary>
        /// Stream answers token-by-token (providers that support it —
        /// Gemini). Falls back to one-shot responses automatically for
        /// providers without streaming. Default ON: first words appear
        /// in ~1-2s instead of a silent 10-30s wait.
        /// </summary>
        public bool StreamingEnabled { get; set; } = true;

        /// <summary>
        /// Voice UX: when speech recognition finishes, send the
        /// recognized text immediately (JARVIS-style instant response)
        /// instead of waiting for Enter. Default ON; users who prefer
        /// to review/edit transcriptions first can turn it off.
        /// </summary>
        public bool AutoSendVoice { get; set; } = true;

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