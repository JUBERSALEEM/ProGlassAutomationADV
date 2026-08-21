using System;
using System.Threading;
using System.Threading.Tasks;
using ProGlassAutomation.Agent.Interfaces;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Wraps an existing IAgentReasoningProvider into the new IAIModelProvider abstraction.</summary>
    public sealed class AIProviderAdapter : IAIModelProvider
    {
        private readonly IAgentReasoningProvider _provider;

        public AIProviderAdapter(IAgentReasoningProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public string ProviderId => _provider.ProviderName.ToLowerInvariant().Replace(" ", "_");
        public string ProviderName => _provider.ProviderName;
        public bool IsLocal => _provider.IsLocal;
        public bool IsAvailable => true; // Existing providers are considered available when constructed.

        public async Task<string> GenerateAsync(string prompt, CancellationToken ct)
        {
            return await _provider.ReasonAsync(prompt ?? "").ConfigureAwait(false);
        }

        public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct)
        {
            // Combine system + user for providers that don't separate them.
            string combined = !string.IsNullOrWhiteSpace(systemPrompt)
                ? $"{systemPrompt}\n\n{userPrompt ?? ""}"
                : (userPrompt ?? "");
            return await _provider.ReasonAsync(combined).ConfigureAwait(false);
        }
    }
}
