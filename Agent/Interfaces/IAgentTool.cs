// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Interfaces/IAgentTool.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// A "tool" is the ONLY way the agent is allowed to touch the application.
// It cannot call repositories directly, cannot navigate directly, and has
// no code path that reaches the ERP except through a registered tool.
//
// Every tool declares its risk up-front so the permission gate and the
// confirmation gate can reason about it WITHOUT understanding what the
// tool does. That is deliberate: adding a dangerous tool later cannot
// bypass the gates by omission — Risk is required, and anything above
// Read is denied without an explicit permission and confirmation.
// ═══════════════════════════════════════════════════════════════════════

using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Interfaces
{
    /// <summary>How dangerous a tool is. Drives permission + confirmation.</summary>
    public enum ToolRisk
    {
        /// <summary>Reads data only. Never mutates anything.</summary>
        Read = 0,

        /// <summary>Changes the current view. No data is touched.</summary>
        Navigation = 1,

        /// <summary>Creates or modifies records.</summary>
        Write = 2,

        /// <summary>Deletes or otherwise irreversibly changes records.</summary>
        Destructive = 3
    }

    /// <summary>Static description of a tool — its contract with the gates.</summary>
    public sealed class ToolDefinition
    {
        /// <summary>Stable identifier used in intents ("get_inventory").</summary>
        public string Name { get; init; } = "";

        /// <summary>One sentence the agent can quote back to the user.</summary>
        public string Description { get; init; } = "";

        /// <summary>Risk tier — see <see cref="ToolRisk"/>.</summary>
        public ToolRisk Risk { get; init; } = ToolRisk.Read;

        /// <summary>
        /// AppPermissions key the acting user must hold ("SheetStore.View").
        /// Empty means no permission is required — only valid for
        /// Navigation tools.
        /// </summary>
        public string RequiredPermission { get; init; } = "";

        /// <summary>
        /// True when the user must explicitly confirm before execution.
        /// Write and Destructive tools are ALWAYS confirmed regardless of
        /// this flag (the policy enforces it); this can force confirmation
        /// on a lower tier too.
        /// </summary>
        public bool RequiresConfirmation { get; init; }
    }

    /// <summary>Result of running a tool.</summary>
    public sealed class ToolResult
    {
        public bool Success { get; init; }

        /// <summary>Text the agent speaks / shows. Must be truthful.</summary>
        public string Message { get; init; } = "";

        /// <summary>Navigation key when the tool wants a view change.</summary>
        public string NavigateTarget { get; init; } = "";

        public static ToolResult Ok(string message, string navigate = "") =>
            new() { Success = true, Message = message, NavigateTarget = navigate };

        public static ToolResult Fail(string message) =>
            new() { Success = false, Message = message };
    }

    /// <summary>One capability the agent can invoke.</summary>
    public interface IAgentTool
    {
        /// <summary>Contract used by the permission and confirmation gates.</summary>
        ToolDefinition Definition { get; }

        /// <summary>
        /// Runs the tool. Implementations must never throw — return
        /// ToolResult.Fail with a truthful message instead.
        /// </summary>
        /// <param name="argument">Free-text argument (a search term, ...).</param>
        Task<ToolResult> ExecuteAsync(string argument);
    }
}
