using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Adapter connecting AgentTaskEngine to existing ProGlassAgent.</summary>
    public sealed class AgentTaskAdapter : IAgentTaskEngine
    {
        private readonly AgentTaskEngine _engine = new();

        public AgentTaskResult PlanTask(string taskDescription, CancellationToken ct) =>
            _engine.PlanTask(taskDescription, ct);

        public async Task<AgentTaskResult> ExecuteAsync(
            Func<string, CancellationToken, Task<bool>> executeStep, CancellationToken ct)
        {
            return await _engine.ExecuteAsync(executeStep, ct).ConfigureAwait(false);
        }

        public TaskEngineState CurrentState => _engine.CurrentState;
        public System.Collections.Generic.IReadOnlyList<string> PlannedSteps => _engine.PlannedSteps;
    }
}
