using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Mock provider for testing agent lifecycle without real APIs.</summary>
    public sealed class MockAIProvider : IAIModelProvider
    {
        public string ProviderId => "mock";
        public string ProviderName => "Mock Provider";
        public bool IsLocal => true;
        public bool IsAvailable => true;

        public Task<string> GenerateAsync(string prompt, CancellationToken ct) =>
            Task.FromResult($"Mock response to: {prompt}");

        public Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct) =>
            Task.FromResult($"Mock response (system: {systemPrompt}, user: {userPrompt})");
    }
}
