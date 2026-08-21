using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Multi-step agent task execution interface.</summary>
    public interface IAgentTaskEngine
    {
        /// <summary>Decomposes a user request into executable steps.</summary>
        AgentTaskResult PlanTask(string taskDescription, CancellationToken ct);

        /// <summary>Runs planned steps with observe-verify-correct loop.</summary>
        Task<AgentTaskResult> ExecuteAsync(System.Func<string, CancellationToken, Task<bool>> executeStep, CancellationToken ct);

        /// <summary>Current engine lifecycle state.</summary>
        TaskEngineState CurrentState { get; }

        /// <summary>Steps planned for the current task.</summary>
        IReadOnlyList<string> PlannedSteps { get; }
    }
}
