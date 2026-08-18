using ProGlassAutomation.Agent.Demo;
using ProGlassAutomation.Agent.Interfaces;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Tools.Demo
{
    /// <summary>
    /// Read/navigation-tier tool: walks live screens and writes a local
    /// storyboard. It never deletes ERP data. Seeding only happens when
    /// SampleDataService reports the database is empty.
    /// </summary>
    public sealed class CreateDemoPackageTool : IAgentTool
    {
        public static DemoDirector SharedDirector { get; } = new();

        public ToolDefinition Definition { get; } = new()
        {
            Name = "create_demo_package",
            Description = "Record a local demo storyboard of a module (or all modules)",
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
                    or DemoJobState.Narrating or DemoJobState.Rendering)
                {
                    return ToolResult.Fail(
                        "A demo is already running. Cancel it first — two jobs " +
                        "must not fight over the same window.");
                }

                string result = await SharedDirector.RunAsync(argument).ConfigureAwait(false);
                return SharedDirector.State == DemoJobState.Completed
                    ? ToolResult.Ok(result)
                    : ToolResult.Fail(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[create_demo_package] {ex.Message}");
                return ToolResult.Fail("The demo step failed, so I stopped.");
            }
        }
    }
}
