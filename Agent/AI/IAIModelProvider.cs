using System;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Provider-independent AI model interface.</summary>
    public interface IAIModelProvider
    {
        string ProviderId { get; }
        string ProviderName { get; }
        bool IsLocal { get; }
        Task<string> GenerateAsync(string prompt, System.Threading.CancellationToken ct);
        Task<string> GenerateAsync(string systemPrompt, string userPrompt, System.Threading.CancellationToken ct);
        bool IsAvailable { get; }
    }
}
