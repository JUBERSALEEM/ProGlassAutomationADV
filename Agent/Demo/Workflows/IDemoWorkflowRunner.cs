using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    public interface IDemoWorkflowRunner
    {
        string ModuleId { get; }
        string OutputFileName { get; }

        /// <summary>
        /// Event ids that prove the walk did more than change the screen.
        /// Director fails unless at least one of these is verified.
        /// </summary>
        IReadOnlyList<string> DeepEvidenceIds { get; }

        Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct);
    }
}
