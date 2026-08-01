// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIService/IAiService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Service (Chat 24 v1.1 — corrected, same folder/namespace)
//
// PURPOSE:
//   Contract for AI service integration. Any provider (Gemini, Groq,
//   Cohere, future OpenRouter etc.) implements this interface.
//   ViewModels stay decoupled from provider implementation.
//
// FOLDER: Services/AI/AIService/    (SAME as before — no move)
// NAMESPACE: ProGlassAutomation.Services.AI.AIService  (UNCHANGED)
//
// CHANGES vs previous version:
//   ✓ Added Provider property (AiProvider enum — identifies which provider)
//   ✓ Added ProviderIcon property (emoji for compact display)
//   ✓ Existing properties preserved: IsEnabled, IsConfigured, ProviderName
//   ✓ Existing methods preserved: TestConnectionAsync, AskAsync (both overloads)
//   ✓ AiResponse/AiTestResult types moved to AiResponse.cs (same folder, same namespace)
//
// BACKWARD COMPATIBILITY:
//   File stays in SAME folder with SAME namespace. Existing code that has
//   `using ProGlassAutomation.Services.AI.AIService;` sees this interface
//   without any changes. New properties (Provider, ProviderIcon) are additive.
//
// REGRESSION RISK: LOW
//   Additive interface change. Existing GeminiAiService needs 2 new
//   property getters added (delivered in enhanced GeminiAiService).
// ═══════════════════════════════════════════════════════════════════════

using System.Threading.Tasks;

namespace ProGlassAutomation.Services.AI.AIService
{
    /// <summary>
    /// Contract for AI service integration. Any provider (Gemini, Groq,
    /// Cohere) can implement this interface — ViewModels stay decoupled.
    ///
    /// Each implementation handles:
    ///   • API key validation
    ///   • HTTP request/response to the provider's endpoint
    ///   • Model selection and parameter tuning
    ///   • Error handling and rate-limit awareness
    /// </summary>
    public interface IAiService
    {
        /// <summary>True if the user has enabled this AI provider in Settings.</summary>
        bool IsEnabled { get; }

        /// <summary>True if an API key is configured (non-empty).</summary>
        bool IsConfigured { get; }

        /// <summary>Human-readable provider name (e.g. "Google Gemini").</summary>
        string ProviderName { get; }

        /// <summary>Emoji icon for compact display (e.g. "🔵").</summary>
        string ProviderIcon { get; }

        /// <summary>Which provider enum value this service represents.</summary>
        AiProvider Provider { get; }

        /// <summary>
        /// Tests the connection by sending a trivial prompt and checking
        /// for a valid response. Used by Settings "Test Connection" button.
        /// </summary>
        Task<AiTestResult> TestConnectionAsync();

        /// <summary>Sends a user prompt to the AI. No system prompt.</summary>
        Task<AiResponse> AskAsync(string prompt);

        /// <summary>
        /// Sends a system prompt + user prompt to the AI.
        /// The system prompt provides context (app knowledge, user data summary).
        /// </summary>
        Task<AiResponse> AskAsync(string systemPrompt, string userPrompt);
    }
}