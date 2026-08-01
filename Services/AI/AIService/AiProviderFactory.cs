// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIService/AiProviderFactory.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Service (Chat 24 v1.1 — corrected folder/namespace)
//
// PURPOSE:
//   Factory that creates the correct IAiService implementation based
//   on the user's AiSettings.ActiveProvider selection. Used by:
//     1. App.xaml.cs at startup — creates App.Ai
//     2. SettingsViewModel when user switches provider — recreates App.Ai
//     3. Settings UI to test connection for non-active providers
//
// FOLDER: Services/AI/AIService/    (same folder as all service files)
// NAMESPACE: ProGlassAutomation.Services.AI.AIService
//
// DESIGN:
//   Simple factory pattern. Each provider instantiated with the SAME
//   AiSettings instance — they read their own key/model from it.
//   Only the active provider responds to IsEnabled checks.
//
// REGRESSION RISK: ZERO — new file, no existing code depends on it.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.AI.AIService
{
    /// <summary>
    /// Creates AI service instances based on provider selection.
    /// </summary>
    public static class AiProviderFactory
    {
        /// <summary>
        /// Creates the IAiService for the currently active provider.
        /// Primary entry point — called by App.xaml.cs at startup
        /// and by SettingsViewModel when user switches providers.
        /// </summary>
        public static IAiService CreateActive(AiSettings settings)
        {
            return settings.ActiveProvider switch
            {
                AiProvider.Gemini => new GeminiAiService(settings),
                AiProvider.Groq => new GroqAiService(settings),
                AiProvider.Cohere => new CohereAiService(settings),
                _ => new GeminiAiService(settings)
            };
        }

        /// <summary>
        /// Creates an IAiService for a SPECIFIC provider, regardless of
        /// which is currently active. Used by Settings to test connections
        /// for non-active providers.
        /// </summary>
        public static IAiService CreateForProvider(
            AiProvider provider, AiSettings settings)
        {
            return provider switch
            {
                AiProvider.Gemini => new GeminiAiService(settings),
                AiProvider.Groq => new GroqAiService(settings),
                AiProvider.Cohere => new CohereAiService(settings),
                _ => new GeminiAiService(settings)
            };
        }

        /// <summary>
        /// Returns metadata for all known providers, enriched with
        /// runtime status (IsActive, IsConfigured) from current settings.
        /// Used by Settings UI to render provider cards with status badges.
        /// </summary>
        public static AiProviderInfo[] GetProviderInfos(AiSettings settings)
        {
            return AiProviderInfo.AllProviders
                .Select(p => p with
                {
                    IsActive = p.Provider == settings.ActiveProvider,
                    IsConfigured = !string.IsNullOrWhiteSpace(
                        settings.GetApiKeyForProvider(p.Provider))
                })
                .ToArray();
        }
    }
}