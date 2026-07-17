using ProGlassAutomation.Services.AI.AIService;

namespace ProGlassAutomation.Services.AI.AIConfig
{
    /// <summary>Root config container persisted to disk as JSON.</summary>
    public class AppConfig
    {
        public AiSettings Ai { get; set; } = new AiSettings();
        public bool OnlineFeaturesEnabled { get; set; } = true;
    }
}