namespace ProGlassAutomation.Services.AI.AIService
{
    public class AiSettings
    {
        public bool Enabled { get; set; } = false;
        public string ApiKey { get; set; } = string.Empty;

        // 'latest' models auto-track newest version — never deprecated
        // Alternatives: gemini-flash-lite-latest, gemini-pro-latest
        public string Model { get; set; } = "gemini-flash-latest";

        public int MaxTokens { get; set; } = 1024;
        public double Temperature { get; set; } = 0.7;
    }
}