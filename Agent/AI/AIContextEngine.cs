using System;

namespace ProGlassAutomation.Agent.AI
{
    public sealed class AppContextState
    {
        public string CurrentModule { get; init; } = "";
        public string SelectedRecordId { get; init; } = "";
        public bool IsDialogOpen { get; init; }
        public string UserAction { get; init; } = "";
    }

    public sealed class AIContextEngine
    {
        public AppContextState GetContext(string requestCategory)
        {
            try
            {
                var userAction = InferUserAction(requestCategory);
                var currentModule = ProGlassAutomation.Agent.Context.AgentScreenContext.CurrentModule ?? "";
                var state = new AppContextState
                {
                    CurrentModule = currentModule,
                    SelectedRecordId = ProGlassAutomation.Agent.Context.AgentScreenContext.CurrentDetail ?? "",
                    IsDialogOpen = currentModule.Contains("Dialog"),
                    UserAction = !string.IsNullOrWhiteSpace(currentModule) ? userAction : "general"
                };
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
