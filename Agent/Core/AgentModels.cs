// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Core/AgentModels.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// The data the agent takes in and gives back, plus the context object
// that carries its collaborators. Deliberately tiny and WPF-free.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using System.Collections.Generic;

namespace ProGlassAutomation.Agent.Core
{
    /// <summary>One thing the user asked for.</summary>
    public sealed class AgentRequest
    {
        /// <summary>Raw user text (typed or transcribed).</summary>
        public string Text { get; init; } = "";

        /// <summary>True when this came from the microphone.</summary>
        public bool FromVoice { get; init; }

        /// <summary>
        /// Who is asking. The permission gate refuses to act when this
        /// does not match the signed-in audit user.
        /// </summary>
        public string Username { get; init; } = "";
    }

    /// <summary>What the agent did / found.</summary>
    public sealed class AgentResponse
    {
        public bool Success { get; init; }

        /// <summary>Text shown in the UI.</summary>
        public string Message { get; init; } = "";

        /// <summary>
        /// Shorter text for text-to-speech. Falls back to Message when
        /// not set explicitly.
        /// </summary>
        public string SpeakText { get; init; } = "";

        /// <summary>Navigation key when the agent changed the view.</summary>
        public string NavigateTarget { get; init; } = "";

        public static AgentResponse Ok(
            string message, string speak = "", string navigate = "") =>
            new()
            {
                Success = true,
                Message = message,
                SpeakText = string.IsNullOrWhiteSpace(speak) ? message : speak,
                NavigateTarget = navigate
            };

        public static AgentResponse Fail(string message) =>
            new() { Success = false, Message = message, SpeakText = message };
    }

    /// <summary>Lifecycle state, surfaced to the UI.</summary>
    public enum AgentState
    {
        Idle = 0,
        Listening = 1,
        Thinking = 2,
        AwaitingConfirmation = 3,
        Acting = 4,
        Error = 5
    }

    /// <summary>
    /// Everything the agent needs, injected in one object so the core has
    /// a single constructor parameter and every collaborator is
    /// substitutable. Any member may be null — the agent degrades
    /// truthfully rather than crashing.
    /// </summary>
    public sealed class AgentContext
    {
        /// <summary>Registered tools by name.</summary>
        public Dictionary<string, IAgentTool> Tools { get; init; } = new();

        public IAgentPlanner? Planner { get; init; }
        public IAgentPermissionService? Permissions { get; init; }
        public IAgentSecurityService? Security { get; init; }
        public IAgentMemory? Memory { get; init; }
        public IAgentKnowledge? Knowledge { get; init; }
        public IAgentTeachingService? Teaching { get; init; }
        public IAgentReasoningProvider? Reasoning { get; init; }
        public IAgentAuditService? Audit { get; init; }
    }
}
