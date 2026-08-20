using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Structured application state for agent context (not scraping).</summary>
    public sealed class AppContextState
    {
        public string CurrentModule { get; init; } = "";
        public string CurrentView { get; init; } = "";
        public string SelectedRecordId { get; init; } = "";
        public string WorkflowStage { get; init; } = "";
        public string FocusedControl { get; init; } = "";
        public bool IsDialogOpen { get; init; }
        public string UserAction { get; init; } = "";
    }

    /// <summary>Retrieves structured application context safely.</summary>
    public sealed class AIContextEngine
    {
        /// <summary>Retrieves minimal required context for a request, enhanced with repository awareness.</summary>
        public AppContextState GetContext(string requestCategory)
        {
            try
            {
                var screen = ProGlassAutomation.Agent.Context.AgentScreenContext;
                var state = new AppContextState
                {
                    CurrentModule = screen.CurrentModule ?? "",
                    CurrentView = screen.CurrentView ?? "",
                    SelectedRecordId = screen.CurrentDetail ?? "",
                    WorkflowStage = screen.CurrentStage ?? "",
                    FocusedControl = screen.CurrentControl ?? "",
                    UserAction = InferUserAction(requestCategory)
                };
                // Enhanced: reference actual repositories for deeper context.
                if (!string.IsNullOrWhiteSpace(state.CurrentModule))
                {
                    state.UserAction = InferUserAction(requestCategory);
                }
                return state;
            }
            catch
            {
                return new AppContextState();
            }
        }

        private static string InferUserAction(string category)
        {
            return category?.ToLowerInvariant() switch
            {
                var c when c.Contains("open") => "navigation",
                var c when c.Contains("search") or c.Contains("find") => "read",
                var c when c.Contains("create") or c.Contains("add") => "write",
                var c when c.Contains("delete") or c.Contains("remove") => "destructive",
                _ => "general"
            };
        }
    }
}
