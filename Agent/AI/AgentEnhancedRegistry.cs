using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Enhanced tool registry with typed validation and authorization tracking.</summary>
    public static class AgentEnhancedRegistry
    {
        /// <summary>Builds enhanced typed tool descriptors from base tool definitions.</summary>
        public static IReadOnlyList<AgentTypedParameter> BuildParametersForTool(string toolName)
        {
            // Type mappings driven by existing tool names; extensible per phase.
            var list = new List<AgentTypedParameter>();
            if (toolName.ToLowerInvariant() == "get_inventory" || toolName.ToLowerInvariant() == "searchproformatool")
            {
                list.Add(new AgentTypedParameter { Name = "searchTerm", Type = "string", Required = false, ValidationRule = "" });
            }
            else if (toolName.ToLowerInvariant() == "updatesheetstocktool")
            {
                list.Add(new AgentTypedParameter { Name = "sheetId", Type = "string", Required = true, ValidationRule = "^[A-Za-z0-9_-]+$" });
                list.Add(new AgentTypedParameter { Name = "quantity", Type = "int", Required = true, ValidationRule = @"^\d+$" });
            }
            return list.AsReadOnly();
        }
    }
}
