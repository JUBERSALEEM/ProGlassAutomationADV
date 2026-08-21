using System;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Typed parameter definition for agent tool arguments.</summary>
    public sealed class AgentTypedParameter
    {
        /// <summary>Parameter name (matches tool definition argument key).</summary>
        public string Name { get; init; } = "";

        /// <summary>Expected data type (string, int, bool, enum key, etc.).</summary>
        public string Type { get; init; } = "string";

        /// <summary>True if the parameter must be provided.</summary>
        public bool Required { get; init; }

        /// <summary>Regex or simple validation rule description.</summary>
        public string ValidationRule { get; init; } = "";

        /// <summary>Default value when not provided.</summary>
        public string DefaultValue { get; init; } = "";
    }
}
