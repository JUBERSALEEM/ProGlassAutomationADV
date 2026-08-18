using ProGlassAutomation.Agent.Demo;
using ProGlassAutomation.Agent.Interfaces;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Tools.Demo
{
    /// <summary>
    /// Records a real module walkthrough into a verified MP4.
    /// Never deletes ERP data. Seeds only the isolated demo folder
    /// when that folder is empty. Success requires a valid MP4.
    /// </summary>
    public sealed class CreateDemoPackageTool : IAgentTool
    {
        public static DemoDirector SharedDirector { get; } = new();

        public ToolDefinition Definition { get; } = new()
        {
            Name = "create_demo_package",
            Description = "Record a live module walkthrough and write a verified MP4 (fails if ffmpeg/TTS/frames are missing)",
            Risk = ToolRisk.Navigation,
            RequiredPermission = "",
            RequiresConfirmation = true
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            try
            {
                if (SharedDirector.State is DemoJobState.Recording
                    or DemoJobState.Preparing or DemoJobState.Analyzing
                    or DemoJobState.Narrating or DemoJobState.Rendering
                    or DemoJobState.Validating or DemoJobState.Queued)
                {
                    return ToolResult.Fail(
                        "A demo is already running. Cancel it first — two jobs " +
                        "must not fight over the same window.");
                }

                await DemoJobQueue.Shared.EnqueueAsync(argument).ConfigureAwait(false);
                return SharedDirector.State == DemoJobState.Completed
                    ? ToolResult.Ok(SharedDirector.Status)
                    : ToolResult.Fail(SharedDirector.Status);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[create_demo_package] {ex.Message}");
                return ToolResult.Fail("The demo step failed, so I stopped.");
            }
        }
    }
}
