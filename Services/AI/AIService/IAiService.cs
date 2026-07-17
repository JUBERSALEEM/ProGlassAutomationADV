using System.Threading.Tasks;

namespace ProGlassAutomation.Services.AI.AIService
{
    /// <summary>
    /// Contract for AI service integration. Any provider (Gemini, Groq, OpenAI)
    /// can implement this interface — ViewModels stay decoupled from provider.
    /// </summary>
    public interface IAiService
    {
        bool IsEnabled { get; }
        bool IsConfigured { get; }
        string ProviderName { get; }

        Task<AiTestResult> TestConnectionAsync();
        Task<AiResponse> AskAsync(string prompt);
        Task<AiResponse> AskAsync(string systemPrompt, string userPrompt);
    }

    public class AiResponse
    {
        public bool Success { get; set; }
        public string Text { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class AiTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}