// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIService/GeminiAiService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Service (Chat 25 v3.0 — proper systemInstruction API)
//
// CHANGES vs Chat 24 v1.1:
//   ✓ FIX CRITICAL: System prompt now sent via Gemini's dedicated
//     "systemInstruction" field instead of being concatenated with
//     user message. Previous approach caused system prompt to be
//     treated as user text — instructions were weakly followed.
//     New structure:
//       { systemInstruction: { parts: [{text: "...system..."}] },
//         contents: [{role:"user", parts:[{text:"...user..."}]}] }
//   ✓ FIX: Model reads from _settings.Model (was already correct).
//   ✓ FIX: IsEnabled checks ActiveProvider == Gemini (preserved).
//   ✓ FIX: Uses GeminiApiKey specifically (preserved).
//   ✓ Added HTTP 429 rate limit detection with friendly message.
//   ✓ Added response safety check — handles blocked responses.
//   ✓ All error handling preserved and enhanced.
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
    /// Uses Generative Language API v1beta with proper systemInstruction.
    /// </summary>
    public class GeminiAiService : IAiService
    {
        private readonly HttpClient _http;
        private readonly AiSettings _settings;

        public string ProviderName => "Google Gemini";
        public string ProviderIcon => "🔵";
        public AiProvider Provider => AiProvider.Gemini;

        public bool IsEnabled =>
            _settings.Enabled &&
            _settings.ActiveProvider == AiProvider.Gemini;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_settings.GeminiApiKey);

        public GeminiAiService(AiSettings settings)
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
                    Message = "Gemini API key is not configured.",
                    ProviderName = ProviderName
                };

            var res = await AskInternalAsync(null, "Reply with exactly: OK");
            return new AiTestResult
            {
                Success = res.Success,
                Message = res.Success
                    ? "✓ Connection successful. Gemini responded."
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
                return Error("Gemini API key not configured. " +
                    "Go to Settings → AI Providers to add your key.");

            try
            {
                var model = _settings.Model;
                var url =
                    $"https://generativelanguage.googleapis.com/v1beta/" +
                    $"models/{model}:generateContent" +
                    $"?key={_settings.GeminiApiKey}";

                // ─────────────────────────────────────────────────────
                // FIX: Use Gemini's systemInstruction field properly.
                //
                // Previous (WRONG):
                //   Combined both prompts into one user turn.
                //   Model treated system instructions as user text.
                //   Instructions weakly followed or ignored.
                //
                // Correct:
                //   systemInstruction → sets model's behavior/persona
                //   contents[user]    → the actual user question
                //
                // The systemInstruction is processed separately by Gemini
                // before the conversation begins — it has full authority
                // over the model's response style and knowledge focus.
                // ─────────────────────────────────────────────────────

                object payload;

                if (!string.IsNullOrWhiteSpace(systemPrompt))
                {
                    // Structured payload with system instruction
                    payload = new
                    {
                        systemInstruction = new
                        {
                            parts = new[]
                            {
                                new { text = systemPrompt }
                            }
                        },
                        contents = new[]
                        {
                            new
                            {
                                role  = "user",
                                parts = new[]
                                {
                                    new { text = userPrompt }
                                }
                            }
                        },
                        generationConfig = new
                        {
                            temperature = _settings.Temperature,
                            maxOutputTokens = _settings.MaxTokens,
                            candidateCount = 1
                        },
                        safetySettings = new[]
                        {
                            new { category = "HARM_CATEGORY_HARASSMENT",
                                  threshold = "BLOCK_ONLY_HIGH" },
                            new { category = "HARM_CATEGORY_HATE_SPEECH",
                                  threshold = "BLOCK_ONLY_HIGH" },
                            new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT",
                                  threshold = "BLOCK_ONLY_HIGH" },
                            new { category = "HARM_CATEGORY_DANGEROUS_CONTENT",
                                  threshold = "BLOCK_ONLY_HIGH" }
                        }
                    };
                }
                else
                {
                    // No system prompt — simpler payload
                    payload = new
                    {
                        contents = new[]
                        {
                            new
                            {
                                role  = "user",
                                parts = new[]
                                {
                                    new { text = userPrompt }
                                }
                            }
                        },
                        generationConfig = new
                        {
                            temperature = _settings.Temperature,
                            maxOutputTokens = _settings.MaxTokens,
                            candidateCount = 1
                        }
                    };
                }

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(
                    json, Encoding.UTF8, "application/json");

                var response = await _http.PostAsync(url, content);
                var body = await response.Content.ReadAsStringAsync();

                // Rate limit — friendly message
                if ((int)response.StatusCode == 429)
                    return Error(
                        "Gemini rate limit reached. " +
                        "Free tier: 15 requests/minute, 1500/day. " +
                        "Please wait a moment and try again.");

                // API key invalid
                if ((int)response.StatusCode == 401 ||
                    (int)response.StatusCode == 403)
                    return Error(
                        "Gemini API key is invalid or expired. " +
                        "Go to Settings → AI Providers to update your key.");

                if (!response.IsSuccessStatusCode)
                    return Error(
                        $"Gemini API error ({(int)response.StatusCode}): " +
                        Truncate(body));

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // Check for blocked response (safety filters)
                if (root.TryGetProperty("promptFeedback", out var feedback))
                {
                    if (feedback.TryGetProperty("blockReason", out var reason))
                    {
                        string reasonStr = reason.GetString() ?? "unknown";
                        if (reasonStr != "BLOCK_REASON_UNSPECIFIED")
                            return Error(
                                $"Response blocked by Gemini safety filter: " +
                                $"{reasonStr}. Please rephrase your question.");
                    }
                }

                // Extract text from candidates
                if (!root.TryGetProperty("candidates", out var candidates) ||
                    candidates.GetArrayLength() == 0)
                    return Error(
                        "Gemini returned no candidates. " +
                        "The response may have been filtered.");

                var candidate = candidates[0];

                // Check finish reason
                if (candidate.TryGetProperty("finishReason", out var finish))
                {
                    string finishStr = finish.GetString() ?? "";
                    if (finishStr == "SAFETY")
                        return Error(
                            "Gemini blocked this response for safety. " +
                            "Please rephrase your question.");
                    if (finishStr == "RECITATION")
                        return Error(
                            "Gemini stopped due to potential copyright. " +
                            "Please rephrase.");
                }

                var text = candidate
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
                return Error(
                    "Gemini request timed out (30s). " +
                    "Check your internet connection and try again.");
            }
            catch (Exception ex)
            {
                return Error($"Gemini error: {ex.Message}");
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