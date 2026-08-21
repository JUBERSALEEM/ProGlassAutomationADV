using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Enhanced agent tool contract with typed parameters, authorization, cancellation, timeout, audit.</summary>
    public interface IAgentTypedTool
    {
        /// <summary>Same contract as base tool definition.</summary>
        Agent.Interfaces.IAgentTool BaseTool { get; }

        /// <summary>Typed parameter schema for this tool.</summary>
        IReadOnlyList<AgentTypedParameter> Parameters { get; }

        /// <summary>Validates argument against typed schema.</summary>
        Task<bool> ValidateArgumentAsync(string argument, CancellationToken ct);

        /// <summary>Runs with authorization, cancellation, timeout, audit.</summary>
        Task<Agent.Interfaces.ToolResult> ExecuteSafeAsync(
            string argument, string username, CancellationToken ct);
    }
}
