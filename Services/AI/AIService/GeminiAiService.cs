// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIService/GeminiAiService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Service (Chat 24 v1.1 — enhanced, SAME folder/namespace)
//
// PURPOSE:
//   Google Gemini AI service implementation.
//   Free tier: 15 req/min, 1500 req/day.
//   Sign up: https://aistudio.google.com/
//
// FOLDER: Services/AI/AIService/    (SAME as before — no move)
// NAMESPACE: ProGlassAutomation.Services.AI.AIService  (UNCHANGED)
//
// CHANGES vs previous:
//   ✓ Added Provider property (AiProvider.Gemini)
//   ✓ Added ProviderIcon property ("🔵")
//   ✓ AiResponse now includes ProviderName on every response
//   ✓ AiTestResult now includes ProviderName
//   ✓ Uses GeminiApiKey instead of generic ApiKey
//   ✓ IsEnabled checks ActiveProvider == Gemini
//   ✓ IsConfigured checks GeminiApiKey specifically
//   ✓ Added TaskCanceledException catch (timeout → friendly message)
//   ✓ Added error body truncation to 300 chars
//   ✓ All other logic preserved verbatim
//
// REGRESSION RISK: LOW
//   Same HTTP logic, same endpoint, same JSON shape.
//   Two new interface properties added. Everything else identical.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.AI.AIService
{
    /// <summary>
    /// Google Gemini AI provider.
    /// Uses the Generative Language API v1beta.
    /// </summary>
    public class GeminiAiService : IAiService
    {
        private readonly HttpClient _http;
        private readonly AiSettings _settings;

        public string ProviderName => "Google Gemini";
        public string ProviderIcon => "🔵";
        public AiProvider Provider => AiProvider.Gemini;
        public bool IsEnabled => _settings.Enabled && _settings.ActiveProvider == AiProvider.Gemini;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.GeminiApiKey);

        public GeminiAiService(AiSettings settings)
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
                    Message = "Gemini API key is not configured.",
                    ProviderName = ProviderName
                };

            var res = await AskInternalAsync(null, "Reply with the single word: OK");
            return new AiTestResult
            {
                Success = res.Success,
                Message = res.Success
                    ? "Connection successful. Gemini responded."
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
                    ErrorMessage = "Gemini API key not configured.",
                    ProviderName = ProviderName
                };

            try
            {
                var model = _settings.Model;
                var url = $"https://generativelanguage.googleapis.com/v1beta/" +
                          $"models/{model}:generateContent?key={_settings.GeminiApiKey}";

                var combinedPrompt = string.IsNullOrWhiteSpace(systemPrompt)
                    ? userPrompt
                    : $"{systemPrompt}\n\n{userPrompt}";

                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = combinedPrompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = _settings.Temperature,
                        maxOutputTokens = _settings.MaxTokens
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _http.PostAsync(url, content);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new AiResponse
                    {
                        Success = false,
                        ErrorMessage = $"Gemini API error ({(int)response.StatusCode}): " +
                                       TruncateError(body),
                        ProviderName = ProviderName
                    };
                }

                using var doc = JsonDocument.Parse(body);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
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
                    ErrorMessage = "Gemini request timed out (30s). " +
                                   "Check your internet connection.",
                    ProviderName = ProviderName
                };
            }
            catch (Exception ex)
            {
                return new AiResponse
                {
                    Success = false,
                    ErrorMessage = $"Gemini exception: {ex.Message}",
                    ProviderName = ProviderName
                };
            }
        }

        private static string TruncateError(string body) =>
            body.Length > 300 ? body.Substring(0, 300) + "..." : body;
    }
}