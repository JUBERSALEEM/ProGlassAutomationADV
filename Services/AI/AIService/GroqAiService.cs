// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIService/GroqAiService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Service (Chat 25 v3.0 — updated model + rate limit handling)
//
// CHANGES vs Chat 24 v1.1:
//   ✓ FIX: Default model reference updated — reads from AiSettings.GroqModel
//     which now defaults to "llama-3.3-70b-versatile" (current Groq model).
//     "llama-3.1-70b-versatile" was deprecated by Groq late 2024.
//   ✓ FIX: HTTP 429 rate limit — friendly message with quota info.
//   ✓ FIX: HTTP 401/403 — clear key invalid message.
//   ✓ FIX: Groq uses "max_tokens" not "max_completion_tokens" — verified
//     against current Groq OpenAI-compatible API docs. Preserved.
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
    /// Groq AI provider — fastest free inference.
    /// OpenAI-compatible chat completions API.
    /// </summary>
    public class GroqAiService : IAiService
    {
        private const string BaseUrl =
            "https://api.groq.com/openai/v1/chat/completions";

        // ══════════════════════════════════════════════════════════════
        //  AUDIT H-1 — ONE SHARED HttpClient, NOT ONE PER INSTANCE
        //
        //  Each provider used to allocate its own HttpClient in the
        //  constructor and never dispose it. Provider instances are
        //  created far more often than "once at startup":
        //
        //    • AiProviderFactory.CreateActive on every provider switch,
        //    • CreateForProvider on every "Test connection" click —
        //      three separate commands in Settings, each allocating a
        //      fresh service and client per press.
        //
        //  An undisposed HttpClient keeps its HttpMessageHandler, and the
        //  handler keeps its connection pool and sockets alive until the
        //  finalizer eventually runs. Repeatedly creating them is the
        //  textbook cause of socket exhaustion (sockets stranded in
        //  TIME_WAIT) and, because DNS is cached per handler, of stale
        //  DNS after a network change.
        //
        //  The client is stateless here: the API key travels on each
        //  HttpRequestMessage, never on DefaultRequestHeaders, so one
        //  static instance is safe to share across every service object
        //  and across provider switches.
        // ══════════════════════════════════════════════════════════════
        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private readonly AiSettings _settings;

        public string ProviderName => "Groq";
        public string ProviderIcon => "⚡";
        public AiProvider Provider => AiProvider.Groq;

        public bool IsEnabled =>
            _settings.Enabled &&
            _settings.ActiveProvider == AiProvider.Groq;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_settings.GroqApiKey);

        public GroqAiService(AiSettings settings)
        {
            _settings = settings ?? new AiSettings();
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

            var res = await AskInternalAsync(null, "Reply with exactly: OK");
            return new AiTestResult
            {
                Success = res.Success,
                Message = res.Success
                    ? "✓ Connection successful. Groq responded."
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
                    "Groq API key not configured. " +
                    "Go to Settings → AI Providers to add your key.");

            try
            {
                var messages = new List<object>();

                // Groq (OpenAI-compatible) supports role:"system" properly
                if (!string.IsNullOrWhiteSpace(systemPrompt))
                    messages.Add(new { role = "system", content = systemPrompt });

                messages.Add(new { role = "user", content = userPrompt });

                var payload = new
                {
                    model = _settings.GroqModel,
                    messages = messages.ToArray(),
                    temperature = _settings.Temperature,
                    max_tokens = _settings.MaxTokens
                };

                var json = JsonSerializer.Serialize(payload);

                using var request = new HttpRequestMessage(
                    HttpMethod.Post, BaseUrl);
                request.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer", _settings.GroqApiKey);
                request.Content = new StringContent(
                    json, Encoding.UTF8, "application/json");

                var response = await _http.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if ((int)response.StatusCode == 429)
                    return Error(
                        "Groq rate limit reached. " +
                        "Free tier: 30 requests/minute, 14,400/day. " +
                        "Please wait a moment and try again.");

                if ((int)response.StatusCode == 401 ||
                    (int)response.StatusCode == 403)
                    return Error(
                        "Groq API key is invalid or expired. " +
                        "Go to Settings → AI Providers to update your key.");

                if (!response.IsSuccessStatusCode)
                    return Error(
                        $"Groq API error ({(int)response.StatusCode}): " +
                        Truncate(body));

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
                return Error(
                    "Groq request timed out (30s). " +
                    "Check your internet connection and try again.");
            }
            catch (Exception ex)
            {
                return Error($"Groq error: {ex.Message}");
            }
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