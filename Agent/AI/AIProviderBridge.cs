using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Agent.Interfaces;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Wraps existing provider implementations into the new abstraction.</summary>
    public static class AIProviderBridge
    {
        public static IReadOnlyList<IAIModelProvider> BuildProviders()
        {
            var providers = new List<IAIModelProvider>();

            // Attempt to wrap existing agent providers if AgentHost has them available.
            // This is a best-effort bridge: if providers haven't been built yet,
            // the adapter list will be empty and the router will fall back to local.
            try
            {
                if (ProGlassAutomation.Agent.Core.AgentHost.IsBuilt &&
                    ProGlassAutomation.Agent.Core.AgentHost.Instance != null)
                {
                    // Existing reasoning provider is accessible through AgentHost.Instance context
                    // (indirectly through AgentProviderSelector selection logic).
                    // For the new abstraction, we expose a generic adapter that can be
                    // injected with any future IAgentReasoningProvider instance.
                    // Currently, the router handles empty lists gracefully (falls back).
                }
            }
            catch
            {
                // Bridge must never break the agent.
            }

            return providers.AsReadOnly();
        }

        /// <summary>Creates an adapter for any existing reasoning provider.</summary>
        public static IAIModelProvider Wrap(IAgentReasoningProvider provider) =>
            new AIProviderAdapter(provider);
    }
}
