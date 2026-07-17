using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.AI.AIService
{
    /// <summary>
    /// Google Gemini AI service implementation.
    /// Free tier: 15 req/min, 1500 req/day. Sign up: https://aistudio.google.com/
    /// </summary>
    public class GeminiAiService : IAiService
    {
        private readonly HttpClient _http;
        private readonly AiSettings _settings;

        public string ProviderName => "Google Gemini";
        public bool IsEnabled => _settings.Enabled;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.ApiKey);

        public GeminiAiService(AiSettings settings)
        {
            _settings = settings ?? new AiSettings();
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public async Task<AiTestResult> TestConnectionAsync()
        {
            if (!IsConfigured)
                return new AiTestResult { Success = false, Message = "API key is not configured." };

            var res = await AskAsync("Reply with the single word: OK");
            if (res.Success)
                return new AiTestResult { Success = true, Message = "Connection successful. Gemini responded." };

            return new AiTestResult { Success = false, Message = res.ErrorMessage ?? "Unknown error." };
        }

        public Task<AiResponse> AskAsync(string prompt) => AskAsync(null, prompt);

        public async Task<AiResponse> AskAsync(string systemPrompt, string userPrompt)
        {
            if (!IsEnabled)
                return new AiResponse { Success = false, ErrorMessage = "AI is disabled." };

            if (!IsConfigured)
                return new AiResponse { Success = false, ErrorMessage = "API key not configured." };

            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_settings.Model}:generateContent?key={_settings.ApiKey}";

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
                        ErrorMessage = $"API error ({(int)response.StatusCode}): {body}"
                    };
                }

                using var doc = JsonDocument.Parse(body);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return new AiResponse { Success = true, Text = text };
            }
            catch (Exception ex)
            {
                return new AiResponse
                {
                    Success = false,
                    ErrorMessage = $"Exception: {ex.Message}"
                };
            }
        }
    }
}