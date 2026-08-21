using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Agent.Configuration;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Routes tasks to the best available provider.</summary>
    public sealed class AIModelRouter : IAIModelRouter
    {
        private readonly IReadOnlyList<IAIModelProvider> _providers;

        public AIModelRouter(IReadOnlyList<IAIModelProvider> providers)
        {
            _providers = providers ?? new List<IAIModelProvider>();
        }

        public IReadOnlyList<string> AvailableProviders =>
            _providers.Select(p => p.ProviderId).ToList().AsReadOnly();

        public IAIModelProvider Select(AITaskCategory category)
        {
            // Prefer cloud for complex reasoning/coding/long-context if consented.
            // Prefer local for conversation/voice/ERP/query when local-only.
            // Always fall back to first available provider.
            var settings = AgentConfigurationService.Current;
            if (settings.LocalOnly || !settings.CloudConsentGiven)
            {
                return _providers.FirstOrDefault(p => p.IsLocal) ?? _providers.First();
            }

            return category switch
            {
                AITaskCategory.Reasoning or AITaskCategory.Coding or AITaskCategory.LongContext or AITaskCategory.DevelopmentAgent =>
                    _providers.FirstOrDefault(p => !p.IsLocal && p.IsAvailable) ?? _providers.First(),
                _ => _providers.FirstOrDefault(p => p.IsAvailable) ?? _providers.First()
            };
        }
    }
}
