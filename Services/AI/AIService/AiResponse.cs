    // ═══════════════════════════════════════════════════════════════════════
    // FILE: Services/AI/AIService/AiResponse.cs
    // PROJECT: ProGlassAutomation
    // MODULE: AI Service (Chat 24 v1.1 — corrected folder/namespace)
    //
    // PURPOSE:
    //   Shared response types used by ALL AI providers. Extracted from the
    //   original IAiService.cs into a dedicated file for clean separation.
    //
    // FOLDER: Services/AI/AIService/
    // NAMESPACE: ProGlassAutomation.Services.AI.AIService
    //   (matches folder convention — same folder as IAiService.cs,
    //    GeminiAiService.cs, AiSettings.cs, etc.)
    //
    // TYPES:
    //   • AiProvider     — enum of supported AI providers
    //   • AiResponse     — result of an AI question (success text or error)
    //   • AiTestResult   — result of a connection test (success/fail + message)
    //
    // CHANGES vs original (was inline in IAiService.cs):
    //   ✓ Moved to dedicated file (same folder, same namespace)
    //   ✓ Added AiProvider enum (Gemini, Groq, Cohere)
    //   ✓ AiResponse: added ProviderName property (tracks which AI answered)
    //   ✓ AiTestResult: added ProviderName property
    //   ✓ Both classes: added XML documentation
    //
    // BACKWARD COMPATIBILITY:
    //   Namespace unchanged → every existing file that has
    //   `using ProGlassAutomation.Services.AI.AIService;` sees these types
    //   without any code change. Zero consumer impact.
    //
    // REGRESSION RISK: ZERO
    //   Same types, same namespace, new file in same folder.
    // ═══════════════════════════════════════════════════════════════════════

    namespace ProGlassAutomation.Services.AI.AIService
    {
        /// <summary>
        /// Supported AI providers. Each has its own service implementation,
        /// API key, and free-tier quota.
        /// </summary>
        public enum AiProvider
        {
            /// <summary>
            /// Google Gemini — 15 req/min, 1500/day free.
            /// Best overall quality. Default provider.
            /// </summary>
            Gemini,

            /// <summary>
            /// Groq — 30 req/min, 14400/day free.
            /// Fastest inference speed. Uses Llama/Mixtral models.
            /// </summary>
            Groq,

            /// <summary>
            /// Cohere — 100 req/min trial key.
            /// Good for search/RAG workflows. Uses Command-R model.
            /// </summary>
            Cohere
        }

        /// <summary>
        /// Result of an AI question. Contains either the successful text
        /// response or an error message explaining the failure.
        /// </summary>
        public class AiResponse
        {
            /// <summary>True if the AI returned a valid text response.</summary>
            public bool Success { get; set; }

            /// <summary>
            /// The AI's text response. Only populated when <see cref="Success"/> is true.
            /// May contain markdown formatting.
            /// </summary>
            public string Text { get; set; }

            /// <summary>
            /// Error description. Only populated when <see cref="Success"/> is false.
            /// Includes HTTP status codes, timeout info, or API error messages.
            /// </summary>
            public string ErrorMessage { get; set; }

            /// <summary>
            /// Which AI provider generated this response.
            /// Used by the chat UI to show provider attribution per message.
            /// </summary>
            public string ProviderName { get; set; }
        }

        /// <summary>
        /// Result of a connection test to an AI provider.
        /// Used by the Settings page "Test Connection" button.
        /// </summary>
        public class AiTestResult
        {
            /// <summary>True if the provider responded correctly.</summary>
            public bool Success { get; set; }

            /// <summary>Human-readable status message.</summary>
            public string Message { get; set; }

            /// <summary>Which provider was tested.</summary>
            public string ProviderName { get; set; }
        }
    }