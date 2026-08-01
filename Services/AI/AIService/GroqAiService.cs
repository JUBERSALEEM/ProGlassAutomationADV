// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIService/GroqAiService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Service (Chat 24 v1.1 — NEW provider)
//
// PURPOSE:
//   Groq AI service implementation.
//   Free tier: 30 req/min, 14,400 req/day — most generous free tier.
//   Fastest inference speed of any free provider (~200ms response time).
//   Uses Llama 3.1 70B by default.
//   Sign up: https://console.groq.com/
//
// FOLDER: Services/AI/AIService/    (same folder as other providers)
// NAMESPACE: ProGlassAutomation.Services.AI.AIService
//
// API:
//   Groq uses an OpenAI-compatible REST API format:
//   POST https://api.groq.com/openai/v1/chat/completions
//   Authorization: Bearer {api_key}
//   Body: { model, messages: [{role, content}], temperature, max_tokens }
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
    /// Groq AI provider — fastest free inference.
    /// Uses OpenAI-compatible chat completions API.
    /// </summary>
    public class GroqAiService : IAiService
    {
        private const string BaseUrl =
            "https://api.groq.com/openai/v1/chat/completions";

        private readonly HttpClient _http;
        private readonly AiSettings _settings;

        public string ProviderName => "Groq";
        public string ProviderIcon => "⚡";
        public AiProvider Provider => AiProvider.Groq;
        public bool IsEnabled => _settings.Enabled &&
                                  _settings.ActiveProvider == AiProvider.Groq;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.GroqApiKey);

        public GroqAiService(AiSettings settings)
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
                    Message = "Groq API key is not configured.",
                    ProviderName = ProviderName
                };

            var res = await AskInternalAsync(null, "Reply with the single word: OK");
            return new AiTestResult
            {
                Success = res.Success,
                Message = res.Success
                    ? "Connection successful. Groq responded."
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
                    ErrorMessage = "Groq API key not configured.",
                    ProviderName = ProviderName
                };

            try
            {
                // Build OpenAI-compatible messages array
                var messages = new List<object>();

                if (!string.IsNullOrWhiteSpace(systemPrompt))
                {
                    messages.Add(new { role = "system", content = systemPrompt });
                }

                messages.Add(new { role = "user", content = userPrompt });

                var payload = new
                {
                    model = _settings.GroqModel,
                    messages = messages.ToArray(),
                    temperature = _settings.Temperature,
                    max_tokens = _settings.MaxTokens
                };

                var json = JsonSerializer.Serialize(payload);

                using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _settings.GroqApiKey);
                request.Content = new StringContent(
                    json, Encoding.UTF8, "application/json");

                var response = await _http.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new AiResponse
                    {
                        Success = false,
                        ErrorMessage = $"Groq API error ({(int)response.StatusCode}): " +
                                       TruncateError(body),
                        ProviderName = ProviderName
                    };
                }

                // Parse OpenAI-format response
                using var doc = JsonDocument.Parse(body);
                var text = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

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
                    ErrorMessage = "Groq request timed out (30s). " +
                                   "Check your internet connection.",
                    ProviderName = ProviderName
                };
            }
            catch (Exception ex)
            {
                return new AiResponse
                {
                    Success = false,
                    ErrorMessage = $"Groq exception: {ex.Message}",
                    ProviderName = ProviderName
                };
            }
        }

        private static string TruncateError(string body) =>
            body.Length > 300 ? body.Substring(0, 300) + "..." : body;
    }
}