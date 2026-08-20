using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Unified command router — typed chat, voice transcript, agent task all go through the same core.</summary>
    public sealed class AIUnifiedCommandRouter
    {
        private readonly IAIOrchestrator _orchestrator;

        public AIUnifiedCommandRouter(IAIOrchestrator orchestrator)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        }

        /// <summary>Typed user message from chat UI.</summary>
        public Task<string> RouteChatAsync(string text, CancellationToken ct) =>
            _orchestrator.HandleChatAsync(text, ct);

        /// <summary>Voice transcript from microphone.</summary>
        public Task<string> RouteVoiceAsync(string transcript, CancellationToken ct) =>
            _orchestrator.HandleVoiceAsync(transcript, ct);

        /// <summary>Agent task description from automation/workflow.</summary>
        public Task<string> RouteAgentTaskAsync(string task, CancellationToken ct) =>
            _orchestrator.HandleAgentTaskAsync(task, ct);

        public bool IsReady => _orchestrator.IsReady;
        public IAIModelRouter Router => _orchestrator.Router;
    }
}
