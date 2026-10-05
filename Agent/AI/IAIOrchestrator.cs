using System;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Central unified AI surface for chat, voice, agent tasks, recording, development mode.</summary>
    public interface IAIOrchestrator
    {
        IAIModelRouter Router { get; }
        Task<string> HandleChatAsync(string text, System.Threading.CancellationToken ct);
        Task<string> HandleVoiceAsync(string transcript, System.Threading.CancellationToken ct);
        Task<string> HandleAgentTaskAsync(string taskDescription, System.Threading.CancellationToken ct);
        bool IsReady { get; }
    }
}
