// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIService/CohereAiService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Service (Chat 25 v3.0 — model update + better error logging)
//
// CHANGES vs Chat 24 v1.1:
//   ✓ FIX: Model reads from _settings.CohereModel which now defaults to
//     "command-r-plus" (better quality, free tier eligible).
//   ✓ FIX: HTTP 429 rate limit — friendly message.
//   ✓ FIX: HTTP 401/403 — clear key invalid message.
//   ✓ FIX: Silent parse failure now logs body to Debug output so
//     format changes can be diagnosed.
//   ✓ FIX: ParseCohereResponse returns the raw body in the error
//     message so format mismatches are visible in debug output.
//   ✓ Added Error() helper to reduce repetition.
//   ✓ All other logic preserved exactly.
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
    /// Uses Cohere v2 Chat API.
    /// </summary>
    public class CohereAiService : IAiService
    {
        private const string BaseUrl = "https://api.cohere.com/v2/chat";

        private readonly HttpClient _http;
        private readonly AiSettings _settings;

        public string ProviderName => "Cohere";
        public string ProviderIcon => "🟣";
        public AiProvider Provider => AiProvider.Cohere;

        public bool IsEnabled =>
            _settings.Enabled &&
            _settings.ActiveProvider == AiProvider.Cohere;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_settings.CohereApiKey);

        public CohereAiService(AiSettings settings)
        {
            _settings = settings ?? new AiSettings();
            _http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
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

            var res = await AskInternalAsync(null, "Reply with exactly: OK");
            return new AiTestResult
            {
                Success = res.Success,
                Message = res.Success
                    ? "✓ Connection successful. Cohere responded."
                    : res.ErrorMessage ?? "Unknown error.",
                ProviderName = ProviderName
            };
        }

        public Task<AiResponse> AskAsync(string prompt) =>
            AskInternalAsync(null, prompt);

        public Task<AiResponse> AskAsync(
            string systemPrompt, string userPrompt) =>
            AskInternalAsync(systemPrompt, userPrompt);

        private async Task<AiResponse> AskInternalAsync(
            string systemPrompt, string userPrompt)
        {
            if (!IsConfigured)
                return Error(
                    "Cohere API key not configured. " +
                    "Go to Settings → AI Providers to add your key.");

            try
            {
                var messages = new List<object>();

                if (!string.IsNullOrWhiteSpace(systemPrompt))
                    messages.Add(new { role = "system", content = systemPrompt });

                messages.Add(new { role = "user", content = userPrompt });

                var payload = new
                {
                    model = _settings.CohereModel,
                    messages = messages.ToArray(),
                    temperature = _settings.Temperature,
                    max_tokens = _settings.MaxTokens
                };

                var json = JsonSerializer.Serialize(payload);

                using var request = new HttpRequestMessage(
                    HttpMethod.Post, BaseUrl);
                request.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer", _settings.CohereApiKey);
                request.Content = new StringContent(
                    json, Encoding.UTF8, "application/json");

                var response = await _http.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if ((int)response.StatusCode == 429)
                    return Error(
                        "Cohere rate limit reached. " +
                        "Free trial: 100 requests/minute. " +
                        "Please wait a moment and try again.");

                if ((int)response.StatusCode == 401 ||
                    (int)response.StatusCode == 403)
                    return Error(
                        "Cohere API key is invalid or expired. " +
                        "Go to Settings → AI Providers to update your key.");

                if (!response.IsSuccessStatusCode)
                    return Error(
                        $"Cohere API error ({(int)response.StatusCode}): " +
                        Truncate(body));

                // FIX: Log body to Debug so format changes are diagnosable
                System.Diagnostics.Debug.WriteLine(
                    $"[CohereAI] Response body: {Truncate(body)}");

                var text = ParseCohereResponse(body);

                if (string.IsNullOrEmpty(text))
                {
                    // FIX: Include truncated body in error for debugging
                    System.Diagnostics.Debug.WriteLine(
                        $"[CohereAI] Failed to parse response. " +
                        $"Full body: {body}");

                    return Error(
                        "Cohere returned an unrecognized response format. " +
                        "This may indicate an API change. " +
                        $"Debug: {Truncate(body)}");
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
                return Error(
                    "Cohere request timed out (30s). " +
                    "Check your internet connection and try again.");
            }
            catch (Exception ex)
            {
                return Error($"Cohere error: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses Cohere API response. Handles three formats:
        ///   1. v2: { message: { content: [{ text: "..." }] } }
        ///   2. OpenAI-compat: { choices: [{ message: { content: "..." } }] }
        ///   3. Legacy v1: { text: "..." }
        /// </summary>
        private static string ParseCohereResponse(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // Format 1: Cohere v2
                if (root.TryGetProperty("message", out var message))
                {
                    if (message.TryGetProperty("content", out var arr) &&
                        arr.ValueKind == JsonValueKind.Array &&
                        arr.GetArrayLength() > 0)
                    {
                        if (arr[0].TryGetProperty("text", out var t))
                        {
                            var text = t.GetString();
                            if (!string.IsNullOrEmpty(text)) return text;
                        }
                    }
                }

                // Format 2: OpenAI-compatible
                if (root.TryGetProperty("choices", out var choices) &&
                    choices.ValueKind == JsonValueKind.Array &&
                    choices.GetArrayLength() > 0)
                {
                    var text = choices[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();
                    if (!string.IsNullOrEmpty(text)) return text;
                }

                // Format 3: Legacy v1
                if (root.TryGetProperty("text", out var legacyText))
                {
                    var text = legacyText.GetString();
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CohereAI] JSON parse failed: {ex.Message}");
            }

            return null;
        }

        private AiResponse Error(string message) => new AiResponse
        {
            Success = false,
            ErrorMessage = message,
            ProviderName = ProviderName
        };

        private static string Truncate(string s) =>
            s?.Length > 300 ? s.Substring(0, 300) + "..." : s;
    }
}