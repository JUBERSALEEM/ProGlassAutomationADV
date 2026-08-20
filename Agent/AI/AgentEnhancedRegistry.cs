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
            return toolName.ToLowerInvariant() switch
            {
                "get_inventory" or "searchproformatool" => new List<AgentTypedParameter>
                {
                    new() { Name = "searchTerm", Type = "string", Required = false, ValidationRule = "" }
                },
                "updatesheetstocktool" => new List<AgentTypedParameter>
                {
                    new() { Name = "sheetId", Type = "string", Required = true, ValidationRule = "^[A-Za-z0-9_-]+$" },
                    new() { Name = "quantity", Type = "int", Required = true, ValidationRule = @"^\d+$" }
                },
                _ => new List<AgentTypedParameter>()
            }.AsReadOnly();
        }
    }
}
