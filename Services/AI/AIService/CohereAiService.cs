// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIService/CohereAiService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Service (Chat 24 v1.1 — NEW provider)
//
// PURPOSE:
//   Cohere AI service implementation.
//   Free trial tier: 100 req/min.
//   Optimized for search and retrieval (RAG) workflows.
//   Uses Command-R model by default.
//   Sign up: https://dashboard.cohere.com/
//
// FOLDER: Services/AI/AIService/    (same folder as other providers)
// NAMESPACE: ProGlassAutomation.Services.AI.AIService
//
// API:
//   POST https://api.cohere.com/v2/chat
//   Authorization: Bearer {api_key}
//   Body: { model, messages: [{role, content}], temperature, max_tokens }
//
// NOTE:
//   Cohere v2 API uses a message-based format similar to OpenAI.
//   Response format differs slightly — this service handles multiple
//   response shapes (v2 message.content[], OpenAI choices[], legacy text).
//
// REGRESSION RISK: ZERO — new file, no existing code depends on it.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.AI.AIService
{
    /// <summary>
    /// Cohere AI provider — optimized for search/RAG.
    /// Uses the Cohere v2 Chat API.
    /// </summary>
    public class CohereAiService : IAiService
    {
        private const string BaseUrl = "https://api.cohere.com/v2/chat";

        private readonly HttpClient _http;
        private readonly AiSettings _settings;

        public string ProviderName => "Cohere";
        public string ProviderIcon => "🟣";
        public AiProvider Provider => AiProvider.Cohere;
        public bool IsEnabled => _settings.Enabled &&
                                  _settings.ActiveProvider == AiProvider.Cohere;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.CohereApiKey);

        public CohereAiService(AiSettings settings)
        {
            _settings = settings ?? new AiSettings();
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public async Task<AiTestResult> TestConnectionAsync()
        {
            if (!IsConfigured)
                return new AiTestResult
                {
                    Success = false,
                    Message = "Cohere API key is not configured.",
                    ProviderName = ProviderName
                };

            var res = await AskInternalAsync(null, "Reply with the single word: OK");
            return new AiTestResult
            {
                Success = res.Success,
                Message = res.Success
                    ? "Connection successful. Cohere responded."
                    : res.ErrorMessage ?? "Unknown error.",
                ProviderName = ProviderName
            };
        }

        public Task<AiResponse> AskAsync(string prompt) =>
            AskInternalAsync(null, prompt);

        public Task<AiResponse> AskAsync(string systemPrompt, string userPrompt) =>
            AskInternalAsync(systemPrompt, userPrompt);

        private async Task<AiResponse> AskInternalAsync(
            string systemPrompt, string userPrompt)
        {
            if (!IsConfigured)
                return new AiResponse
                {
                    Success = false,
                    ErrorMessage = "Cohere API key not configured.",
                    ProviderName = ProviderName
                };

            try
            {
                // Build Cohere v2 messages array
                var messages = new List<object>();

                if (!string.IsNullOrWhiteSpace(systemPrompt))
                {
                    messages.Add(new { role = "system", content = systemPrompt });
                }

                messages.Add(new { role = "user", content = userPrompt });

                var payload = new
                {
                    model = _settings.CohereModel,
                    messages = messages.ToArray(),
                    temperature = _settings.Temperature,
                    max_tokens = _settings.MaxTokens
                };

                var json = JsonSerializer.Serialize(payload);

                using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _settings.CohereApiKey);
                request.Content = new StringContent(
                    json, Encoding.UTF8, "application/json");

                var response = await _http.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new AiResponse
                    {
                        Success = false,
                        ErrorMessage = $"Cohere API error ({(int)response.StatusCode}): " +
                                       TruncateError(body),
                        ProviderName = ProviderName
                    };
                }

                // Parse response — handle multiple Cohere response formats
                string text = ParseCohereResponse(body);

                if (string.IsNullOrEmpty(text))
                {
                    return new AiResponse
                    {
                        Success = false,
                        ErrorMessage = "Cohere returned an unrecognized response format: " +
                                       TruncateError(body),
                        ProviderName = ProviderName
                    };
                }

                return new AiResponse
                {
                    Success = true,
                    Text = text,
                    ProviderName = ProviderName
                };
            }
            catch (TaskCanceledException)
            {
                return new AiResponse
                {
                    Success = false,
                    ErrorMessage = "Cohere request timed out (30s). " +
                                   "Check your internet connection.",
                    ProviderName = ProviderName
                };
            }
            catch (Exception ex)
            {
                return new AiResponse
                {
                    Success = false,
                    ErrorMessage = $"Cohere exception: {ex.Message}",
                    ProviderName = ProviderName
                };
            }
        }

        /// <summary>
        /// Parses Cohere API response body. Handles three response formats:
        ///   1. Cohere v2: { message: { content: [{ text: "..." }] } }
        ///   2. OpenAI-compat: { choices: [{ message: { content: "..." } }] }
        ///   3. Legacy v1: { text: "..." }
        /// Returns null if none match.
        /// </summary>
        private static string ParseCohereResponse(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // Format 1: Cohere v2 — message.content[0].text
                if (root.TryGetProperty("message", out var message))
                {
                    if (message.TryGetProperty("content", out var contentArr) &&
                        contentArr.ValueKind == JsonValueKind.Array &&
                        contentArr.GetArrayLength() > 0)
                    {
                        var firstContent = contentArr[0];
                        if (firstContent.TryGetProperty("text", out var textProp))
                        {
                            var text = textProp.GetString();
                            if (!string.IsNullOrEmpty(text))
                                return text;
                        }
                    }
                }

                // Format 2: OpenAI-compatible — choices[0].message.content
                if (root.TryGetProperty("choices", out var choices) &&
                    choices.ValueKind == JsonValueKind.Array &&
                    choices.GetArrayLength() > 0)
                {
                    var text = choices[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }

                // Format 3: Legacy v1 — { text: "..." }
                if (root.TryGetProperty("text", out var legacyText))
                {
                    var text = legacyText.GetString();
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }
            }
            catch
            {
                // JSON parse error — return null, caller handles it
            }

            return null;
        }

        private static string TruncateError(string body) =>
            body.Length > 300 ? body.Substring(0, 300) + "..." : body;
    }
}