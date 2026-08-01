// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIService/AiSettings.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Service (Chat 25 v1.0 — Enabled default + Whisper model selection)
//
// CHANGES vs Chat 24 v1.1:
//   ✓ FIX: Enabled default changed from false → true.
//     Industrial ERP — AI is a feature, not a trap. Users expect the
//     chat button to work immediately after pasting their API key.
//     Previous default (false) caused "AI not responding" on every
//     fresh install because users didn't know to toggle it on.
//   ✓ NEW: SelectedWhisperModel property (persisted to config.json).
//     Supports Base / Medium / Large-v3 model selection.
//     Default: "base" — reasonable download size (142MB), good accuracy.
//   ✓ ALL other properties preserved exactly.
//
// REGRESSION RISK: LOW
//   Existing config.json files with "Enabled": false will still load
//   correctly (JSON deserialization honors the persisted value).
//   Only NEW installs (no config.json yet) default to Enabled=true.
// ═══════════════════════════════════════════════════════════════════════

using System.Text.Json.Serialization;

namespace ProGlassAutomation.Services.AI.AIService
{
    /// <summary>
    /// AI configuration persisted to config.json.
    /// Supports multiple providers with per-provider API keys.
    /// </summary>
    public class AiSettings
    {
        /// <summary>
        /// Master AI enable/disable toggle.
        /// Chat 25: Default changed to TRUE — AI should work out of the
        /// box once user pastes their API key. Toggle exists for users
        /// who explicitly want to disable AI features.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Which AI provider is currently active.
        /// Only one provider processes requests at a time (single-active model).
        /// Default: Gemini.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AiProvider ActiveProvider { get; set; } = AiProvider.Gemini;

        // ══════════════════════════════════════════════════════════════
        //  PER-PROVIDER API KEYS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Google Gemini API key (get at aistudio.google.com).</summary>
        public string GeminiApiKey { get; set; } = string.Empty;

        /// <summary>Groq API key (get at console.groq.com).</summary>
        public string GroqApiKey { get; set; } = string.Empty;

        /// <summary>Cohere API key (get at dashboard.cohere.com).</summary>
        public string CohereApiKey { get; set; } = string.Empty;

        // ══════════════════════════════════════════════════════════════
        //  BACKWARD COMPATIBILITY — ApiKey redirects to active provider
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// BACKWARD COMPAT: Gets/sets the API key for the currently active
        /// provider. Existing code calling App.Config.Ai.ApiKey = "..."
        /// will set the active provider's key (default = Gemini).
        ///
        /// JsonIgnore prevents this from being serialized separately —
        /// the per-provider keys are the source of truth.
        /// </summary>
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

        // ══════════════════════════════════════════════════════════════
        //  PER-PROVIDER MODELS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Gemini model name. 'latest' aliases auto-track newest version.
        /// Also serves as the backward-compatible "Model" property.
        /// </summary>
        public string Model { get; set; } = "gemini-flash-latest";

        /// <summary>Groq model name.</summary>
        public string GroqModel { get; set; } = "llama-3.1-70b-versatile";

        /// <summary>Cohere model name.</summary>
        public string CohereModel { get; set; } = "command-r";

        // ══════════════════════════════════════════════════════════════
        //  SHARED TUNING PARAMETERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Maximum output tokens per response.</summary>
        public int MaxTokens { get; set; } = 1024;

        /// <summary>Temperature (creativity) parameter.</summary>
        public double Temperature { get; set; } = 0.7;

        // ══════════════════════════════════════════════════════════════
        //  RAG (Real App Data) SETTING
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// When true, AI system prompt includes real-time business data summary.
        /// </summary>
        public bool UseRealData { get; set; } = true;

        // ══════════════════════════════════════════════════════════════
        //  WHISPER MODEL SELECTION (Chat 25 v1.0 — NEW)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Selected Whisper model for local speech-to-text.
        /// Persisted to config.json. Values: "base", "medium", "large-v3".
        /// Default: "base" — 142MB download, 95%+ accuracy, fast inference.
        ///
        /// Users can change this in Settings → Voice Assistant → Model dropdown.
        /// Changing model requires re-download if the selected model's file
        /// is not already present on disk.
        /// </summary>
        public string SelectedWhisperModel { get; set; } = "base";

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Returns the API key for a specific provider.</summary>
        public string GetApiKeyForProvider(AiProvider provider) => provider switch
        {
            AiProvider.Gemini => GeminiApiKey,
            AiProvider.Groq => GroqApiKey,
            AiProvider.Cohere => CohereApiKey,
            _ => string.Empty
        };

        /// <summary>Returns the model name for a specific provider.</summary>
        public string GetModelForProvider(AiProvider provider) => provider switch
        {
            AiProvider.Gemini => Model,
            AiProvider.Groq => GroqModel,
            AiProvider.Cohere => CohereModel,
            _ => Model
        };
    }
}