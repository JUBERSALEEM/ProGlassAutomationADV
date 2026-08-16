// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Interfaces/IAgentServices.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// Every collaborator the agent core depends on, expressed as an interface
// so the core has NO concrete dependency on storage, network or WPF.
// One file because these are small contracts read together.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Interfaces
{
    // ══════════════════════════════════════════════════════════════════
    //  REASONING
    // ══════════════════════════════════════════════════════════════════

    /// <summary>A source of natural-language answers.</summary>
    public interface IAgentReasoningProvider
    {
        /// <summary>Display name ("Local knowledge", "Gemini", ...).</summary>
        string ProviderName { get; }

        /// <summary>
        /// True when the provider runs entirely on this machine. The
        /// security service refuses non-local providers in Local-Only mode.
        /// </summary>
        bool IsLocal { get; }

        /// <summary>
        /// Answers a question. Must never throw — return a truthful
        /// "I could not answer" string instead.
        /// </summary>
        Task<string> ReasonAsync(string prompt);
    }

    // ══════════════════════════════════════════════════════════════════
    //  MEMORY
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Conversation + approved long-term memory.</summary>
    public interface IAgentMemory
    {
        /// <summary>Adds a turn to the in-memory, session-only history.</summary>
        void AddTurn(string role, string text);

        /// <summary>
        /// Persists a fact the user explicitly approved. Session chatter is
        /// never persisted — only what the user asked to remember.
        /// </summary>
        Task AddApprovedAsync(string kind, string content);

        /// <summary>Merged recall: session turns + persisted facts.</summary>
        Task<IReadOnlyList<string>> RecallAsync(int max = 20);

        /// <summary>Clears the session conversation only.</summary>
        void ClearConversation();

        /// <summary>Clears persisted memory only.</summary>
        Task ClearPersistedAsync();
    }

    // ══════════════════════════════════════════════════════════════════
    //  KNOWLEDGE
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Curated domain facts the local provider answers from.</summary>
    public interface IAgentKnowledge
    {
        Task AddAsync(string category, string title, string content);

        /// <summary>Substring retrieval, capped.</summary>
        Task<IReadOnlyList<string>> RetrieveAsync(string query, int max = 8);

        /// <summary>Serializes the knowledge base to JSON.</summary>
        Task<string> ExportJsonAsync();

        /// <summary>Imports validated JSON. Returns how many rows landed.</summary>
        Task<int> ImportJsonAsync(string json);

        Task ClearAsync();

        /// <summary>Seeds the built-in glass-industry facts. Idempotent.</summary>
        Task EnsureSeededAsync();
    }

    // ══════════════════════════════════════════════════════════════════
    //  TEACHING
    // ══════════════════════════════════════════════════════════════════

    /// <summary>One user-taught trigger → action rule.</summary>
    public sealed class TeachingRuleView
    {
        public int Id { get; init; }
        public string Trigger { get; init; } = "";
        public string Action { get; init; } = "";
        public bool IsApproved { get; init; }
        public string ApprovedBy { get; init; } = "";
    }

    /// <summary>
    /// "Whenever I say X, do Y". Proposals are INERT until approved —
    /// teaching can never silently grant the agent a new behaviour.
    /// </summary>
    public interface IAgentTeachingService
    {
        /// <summary>Stores an unapproved rule; returns its id.</summary>
        Task<int> ProposeAsync(string trigger, string action);

        Task<bool> ApproveAsync(int id, string approvedBy);
        Task<bool> RevokeAsync(int id);

        /// <summary>Approved rules only — the ones the matcher may use.</summary>
        Task<IReadOnlyList<TeachingRuleView>> ActiveRulesAsync();

        /// <summary>Every rule, approved or not (for the Settings list).</summary>
        Task<IReadOnlyList<TeachingRuleView>> AllRulesAsync();

        Task ClearTeachingAsync();
    }

    // ══════════════════════════════════════════════════════════════════
    //  PERMISSIONS
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Outcome of a permission / confirmation check.</summary>
    public sealed class PermissionDecision
    {
        public bool Allowed { get; init; }

        /// <summary>Truthful reason shown to the user when denied.</summary>
        public string Reason { get; init; } = "";

        public static PermissionDecision Allow() => new() { Allowed = true };
        public static PermissionDecision Deny(string reason) =>
            new() { Allowed = false, Reason = reason };
    }

    /// <summary>Gate in front of every tool execution.</summary>
    public interface IAgentPermissionService
    {
        /// <summary>Role/permission check for the acting user.</summary>
        PermissionDecision Check(ToolDefinition tool, string username);

        /// <summary>
        /// Confirmation gate. Returns Deny when the user declined or when
        /// no prompt is wired (fail-closed).
        /// </summary>
        Task<PermissionDecision> ConfirmAsync(ToolDefinition tool, string argument);
    }

    // ══════════════════════════════════════════════════════════════════
    //  SECURITY
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Boundary between the agent and anything external.</summary>
    public interface IAgentSecurityService
    {
        /// <summary>True when the agent may not talk to the internet.</summary>
        bool LocalOnlyMode { get; }

        /// <summary>
        /// Neutralizes untrusted text (DB rows, transcripts) before it is
        /// used as reasoning input: strips control characters and known
        /// prompt-injection phrases, then caps the length.
        /// </summary>
        string SanitizeUntrusted(string text);

        /// <summary>Masks secrets (API keys, bearer tokens, passwords).</summary>
        string Redact(string text);

        /// <summary>
        /// Decides whether a specific outbound payload may leave the
        /// machine. Ordered checks; the first failure wins.
        /// </summary>
        Task<PermissionDecision> AllowExternalAsync(string host, string payload);
    }

    // ══════════════════════════════════════════════════════════════════
    //  PLANNING
    // ══════════════════════════════════════════════════════════════════

    /// <summary>A single decided step.</summary>
    public sealed class AgentPlanStep
    {
        /// <summary>"tool", "navigate", "answer", "teach", "approve".</summary>
        public string Kind { get; init; } = "answer";

        public string Target { get; init; } = "";
        public string Argument { get; init; } = "";
    }

    /// <summary>Turns a detected intent into an ordered step list.</summary>
    public interface IAgentPlanner
    {
        IReadOnlyList<AgentPlanStep> Plan(string intent, string originalText);
    }

    // ══════════════════════════════════════════════════════════════════
    //  AUDIT
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Every agent decision is recorded through this.</summary>
    public interface IAgentAuditService
    {
        void Request(string username, string text, bool fromVoice);
        void Intent(string username, string intent);
        void Tool(string username, string tool, bool allowed, string detail);
        void Teaching(string username, string action, string detail);
        void Security(string username, string action, string detail);
        void AgentError(string username, string detail);
    }
}
