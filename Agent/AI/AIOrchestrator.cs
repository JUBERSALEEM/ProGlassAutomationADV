using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Wraps AgentHost and provider selection into one unified surface.</summary>
    public sealed class AIOrchestrator : IAIOrchestrator
    {
        private readonly IAIModelRouter _router;

        public AIOrchestrator(IAIModelRouter router)
        {
            _router = router ?? new AIModelRouter(new System.Collections.Generic.List<IAIModelProvider>());
        }

        public IAIModelRouter Router => _router;

        public bool IsReady => ProGlassAutomation.Agent.Core.AgentHost.IsBuilt;

        public async Task<string> HandleChatAsync(string text, CancellationToken ct)
        {
            var provider = _router.Select(AITaskCategory.Conversation);
            return await provider.GenerateAsync(text, ct).ConfigureAwait(false);
        }

        public async Task<string> HandleVoiceAsync(string transcript, CancellationToken ct)
        {
            var provider = _router.Select(AITaskCategory.VoiceCommand);
            return await provider.GenerateAsync("Voice: " + transcript, ct).ConfigureAwait(false);
        }

        public async Task<string> HandleAgentTaskAsync(string taskDescription, CancellationToken ct)
        {
            var provider = _router.Select(AITaskCategory.Reasoning);
            return await provider.GenerateAsync("Agent task: " + taskDescription, ct).ConfigureAwait(false);
        }
    }
}
