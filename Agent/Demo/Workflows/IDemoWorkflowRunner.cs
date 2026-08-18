using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    public interface IDemoWorkflowRunner
    {
        string ModuleId { get; }
        string OutputFileName { get; }
        Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct);
    }
}
