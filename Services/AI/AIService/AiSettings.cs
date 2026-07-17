namespace ProGlassAutomation.Services.AI.AIService
{
    public class AiSettings
    {
        public bool Enabled { get; set; } = false;
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gemini-1.5-flash";
        public int MaxTokens { get; set; } = 1024;
        public double Temperature { get; set; } = 0.7;
    }
}