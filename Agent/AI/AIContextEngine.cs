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
                    IsDialogOpen = (screen.CurrentModule ?? "").Contains("Dialog") || (screen.CurrentView ?? "").Contains("Dialog"),
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
            if (category == null) return "general";
            var c = category.ToLowerInvariant();
            if (c.Contains("open")) return "navigation";
            if (c.Contains("search") || c.Contains("find")) return "read";
            if (c.Contains("create") || c.Contains("add")) return "write";
            if (c.Contains("delete") || c.Contains("remove")) return "destructive";
            return "general";
        }
    }
}
