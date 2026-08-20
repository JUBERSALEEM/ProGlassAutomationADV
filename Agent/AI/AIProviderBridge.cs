using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Wraps existing provider implementations into the new abstraction.</summary>
    public static class AIProviderBridge
    {
        public static IReadOnlyList<IAIModelProvider> BuildProviders()
        {
            // Existing providers will be wrapped; for now we expose the abstraction.
            return new List<IAIModelProvider>();
        }
    }
}
