// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIService/AiProviderInfo.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Service (Chat 24 v1.1 — corrected folder/namespace)
//
// PURPOSE:
//   Immutable metadata record describing an AI provider's identity,
//   capabilities, and current status. Used by the Settings page
//   provider selector and the chat header provider indicator.
//
// FOLDER: Services/AI/AIService/
// NAMESPACE: ProGlassAutomation.Services.AI.AIService
//   (same folder as IAiService.cs, GeminiAiService.cs, etc.)
//
// DESIGN:
//   Sealed record — immutable after construction, value-equality
//   semantics, JIT-inlinable. Contains only display/metadata fields,
//   no service logic.
//
// REGRESSION RISK: ZERO — new file in existing folder, no code depends on it yet.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Services.AI.AIService
{
    /// <summary>
    /// Display metadata for an AI provider shown in Settings + Chat UI.
    /// </summary>
    public sealed record AiProviderInfo
    {
        /// <summary>Provider enum value.</summary>
        public AiProvider Provider { get; init; }

        /// <summary>Human-readable name (e.g. "Google Gemini").</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Emoji icon for compact display (e.g. "🔵").</summary>
        public string Icon { get; init; } = string.Empty;

        /// <summary>Short description of the provider.</summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>Free tier quota summary (e.g. "15 req/min, 1500/day").</summary>
        public string FreeTier { get; init; } = string.Empty;

        /// <summary>API key input hint/placeholder (e.g. "AIza...").</summary>
        public string ApiKeyHint { get; init; } = string.Empty;

        /// <summary>URL where users can get a free API key.</summary>
        public string SignupUrl { get; init; } = string.Empty;

        /// <summary>True if this provider is currently the active one.</summary>
        public bool IsActive { get; init; }

        /// <summary>True if an API key is configured for this provider.</summary>
        public bool IsConfigured { get; init; }

        /// <summary>
        /// Static registry of all known providers with their metadata.
        /// Used by Settings UI to render provider cards.
        /// Runtime status (IsActive, IsConfigured) is enriched by
        /// <see cref="AiProviderFactory.GetProviderInfos"/>.
        /// </summary>
        public static AiProviderInfo[] AllProviders => new[]
        {
            new AiProviderInfo
            {
                Provider    = AiProvider.Gemini,
                Name        = "Google Gemini",
                Icon        = "🔵",
                Description = "Best overall quality. Excellent at reasoning, code, and glass industry knowledge.",
                FreeTier    = "15 req/min · 1,500 req/day",
                ApiKeyHint  = "AIza...",
                SignupUrl   = "https://aistudio.google.com"
            },
            new AiProviderInfo
            {
                Provider    = AiProvider.Groq,
                Name        = "Groq",
                Icon        = "⚡",
                Description = "Fastest inference speed. Uses Llama 3.1 70B. Great for quick answers.",
                FreeTier    = "30 req/min · 14,400 req/day",
                ApiKeyHint  = "gsk_...",
                SignupUrl   = "https://console.groq.com"
            },
            new AiProviderInfo
            {
                Provider    = AiProvider.Cohere,
                Name        = "Cohere",
                Icon        = "🟣",
                Description = "Optimized for search and retrieval. Uses Command-R model. Good for data lookup.",
                FreeTier    = "100 req/min · Trial key",
                ApiKeyHint  = "co_...",
                SignupUrl   = "https://dashboard.cohere.com"
            }
        };
    }
}