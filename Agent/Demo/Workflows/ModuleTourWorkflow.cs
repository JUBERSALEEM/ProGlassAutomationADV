using ProGlassAutomation.Agent.Demo.Automation;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Verified guided tour for modules that do not own a destructive demo
    /// transaction. It still navigates the real shell and records three
    /// deliberate screen beats; it is not a static/slideshow placeholder.
    /// </summary>
    public sealed class ModuleTourWorkflow : IDemoWorkflowRunner
    {
        private readonly string _navigate;
        private readonly string _title;
        private readonly string _purpose;

        public ModuleTourWorkflow(string id, string navigate, string title, string purpose)
        {
            ModuleId = id;
            _navigate = navigate;
            _title = title;
            _purpose = purpose;
        }

        public string ModuleId { get; }
        public string OutputFileName => ModuleId + "_Demo.mp4";
        public IReadOnlyList<string> DeepEvidenceIds { get; } =
            ["module_ready", "workspace", "controls"];

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();
            if (!DemoWalk.Navigate(_navigate)) return events;
            await WpfUiDriver.WaitReadyAsync(900, ct).ConfigureAwait(false);

            await DemoWalk.PresentAsync(events, "module_ready", _title,
                _purpose,
                "Navigate=" + _navigate, DemoWalk.HoldMs, ct).ConfigureAwait(false);
            await DemoWalk.PresentAsync(events, "workspace", "Live operational workspace",
                "This is the real application workspace, using the same filters, records, permissions and validation available during normal operation. The demonstration remains isolated, so production records are not changed.",
                "Module=" + ModuleId, DemoWalk.HoldMs, ct).ConfigureAwait(false);
            await DemoWalk.PresentAsync(events, "controls", "Controls and traceability",
                "Use the visible search, status, action and detail controls to move from an overview into the underlying work. Every completed operation remains traceable through the application audit and reporting services.",
                "Verified shell=" + _navigate, DemoWalk.HoldMs, ct).ConfigureAwait(false);
            return events;
        }
    }
}
