using ProGlassAutomation.Services.AI.AIService;

namespace ProGlassAutomation.Services.AI.AIConfig
{
    /// <summary>
    /// Root config container persisted to disk as JSON.
    ///
    /// SECURITY (v2): API keys never reach the JSON file. They live in
    /// the DPAPI-encrypted SecretVault and are hydrated into AiSettings
    /// in memory by ConfigService. With RememberApiKeys=false keys exist
    /// only for the running session.
    /// </summary>
    public class AppConfig
    {
        public AiSettings Ai { get; set; } = new AiSettings();
        public bool OnlineFeaturesEnabled { get; set; } = true;
    }
}
