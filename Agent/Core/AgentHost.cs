// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Core/AgentHost.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// The composition root — the single place where the agent is assembled.
//
// It is LAZY and OPT-IN: nothing here runs unless something asks for
// Instance. The ERP therefore starts exactly as it did before this phase;
// agent.db is not created, no seed is written and no service is
// constructed until the user opens Settings → AI & Agent or invokes the
// agent.
//
// Build() is also non-throwing: if any part of the agent fails to
// construct, the property returns null and every caller treats that as
// "the agent is unavailable" rather than crashing the shell.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Agent.Knowledge;
using ProGlassAutomation.Agent.Memory;
using ProGlassAutomation.Agent.Permissions;
using ProGlassAutomation.Agent.Planning;
using ProGlassAutomation.Agent.Providers;
using ProGlassAutomation.Agent.Security;
using ProGlassAutomation.Agent.Teaching;
using ProGlassAutomation.Agent.Tools.Registry;
using ProGlassAutomation.Agent.Voice;
using ProGlassAutomation.Agent.Voice.SpeechToText.Whisper;
using ProGlassAutomation.Agent.AI;
using ProGlassAutomation.Agent.Voice.TextToSpeech;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Core
{
    /// <summary>Lazily builds and owns the agent and its collaborators.</summary>
    public static class AgentHost
    {
        private static readonly object Gate = new();
        private static bool _built;

        // ── Collaborators (null until built) ─────────────────────────────
        public static ProGlassAgent? Instance { get; private set; }
        public static IAgentTeachingService? Teaching { get; private set; }
        public static IAgentKnowledge? Knowledge { get; private set; }
        public static IAgentMemory? Memory { get; private set; }
        public static IAgentSecurityService? Security { get; private set; }
        public static IAgentAuditService? Audit { get; private set; }
        public static AgentVoicePipeline? Voice { get; private set; }
        /// <summary>Central unified AI orchestrator (Phase 6 integration).</summary>
        public static IAIOrchestrator? AI { get; private set; }
        /// <summary>Unified command router integrating typed, voice and agent tasks (Phase 7).</summary>
        public static AIUnifiedCommandRouter? CommandRouter { get; private set; }
        /// <summary>Multi-step agent task adapter connecting task engine to agent core (Phase 9).</summary>
        public static AgentTaskAdapter? TaskAdapter { get; private set; }

        /// <summary>
        /// Navigation callback, set once by the shell. Assigning it before
        /// Build() lets the agent's navigate tool work; leaving it null
        /// simply means the agent truthfully reports it cannot move.
        /// </summary>
        public static Action<string>? Navigate { get; set; }

        /// <summary>
        /// Confirmation prompt, set by the UI layer. Null means every
        /// Write/Destructive tool is denied (fail-closed).
        /// </summary>
        public static ConfirmationPrompt? Confirm { get; set; }

        /// <summary>True once the agent has been assembled successfully.</summary>
        public static bool IsBuilt => _built && Instance != null;

        /// <summary>
        /// Builds the agent if it has not been built yet. Safe to call
        /// repeatedly and from any thread. Never throws.
        /// </summary>
        public static ProGlassAgent? Ensure()
        {
            lock (Gate)
            {
                if (_built) return Instance;
                _built = true;

                try
                {
                    var audit = new AgentAuditService();
                    var security = new AgentSecurityService(audit);
                    var knowledge = new AgentKnowledgeService(audit);
                    var memory = new AgentMemoryService(audit);
                    var teaching = new AgentTeachingService(audit);

                    var permissions = new AgentPermissionService(Confirm, audit);

                    var reasoning = AgentProviderSelector.Select(
                        knowledge, security, App.Ai);

                    var context = new AgentContext
                    {
                        Tools = AgentToolRegistry.Build(Navigate),
                        Planner = new AgentPlanner(),
                        Permissions = permissions,
                        Security = security,
                        Memory = memory,
                        Knowledge = knowledge,
                        Teaching = teaching,
                        Reasoning = reasoning,
                        Audit = audit
                    };

                    Audit = audit;
                    Security = security;
                    Knowledge = knowledge;
                    Memory = memory;
                    Teaching = teaching;
                    Instance = new ProGlassAgent(context);

                    Voice = new AgentVoicePipeline(
                        Instance,
                        new AgentWhisperBridge(App.SpeechToText),
                        new AgentSpeechAdapter(App.TextToSpeech));

                    // Phase 6 — unified AI orchestrator integration
                    try
                    {
                        var router = new AIModelRouter(AIProviderBridge.BuildProviders());
                        AI = new AIOrchestrator(router);
                    }
                    catch (Exception aiEx)
                    {
                        AppLogService.Debug($"[AgentHost] AI orchestrator init failed: {aiEx.Message}");
                        AI = null;
                    }

                    // Phase 7 — unified command router
                    try
                    {
                        CommandRouter = AI != null ? new AIUnifiedCommandRouter(AI) : null;
                    }
                    catch (Exception cmdEx)
                    {
                        AppLogService.Debug($"[AgentHost] command router init failed: {cmdEx.Message}");
                        CommandRouter = null;
                    }

                    // Phase 9 — multi-step agent task adapter
                    try
                    {
                        TaskAdapter = new AgentTaskAdapter();
                    }
                    catch (Exception taskEx)
                    {
                        AppLogService.Debug($"[AgentHost] task adapter init failed: {taskEx.Message}");
                        TaskAdapter = null;
                    }

                    return Instance;
                }
                catch (Exception ex)
                {
                    // The ERP must survive an agent that cannot start.
                    AppLogService.Debug($"[AgentHost] build failed: {ex.Message}");
                    Instance = null;
                    return null;
                }
            }
        }

        /// <summary>
        /// Seeds the knowledge base in the background. Idempotent and
        /// non-throwing; safe to call from a settings page load.
        /// </summary>
        public static async Task SeedKnowledgeAsync()
        {
            try
            {
                Ensure();
                if (Knowledge != null)
                    await Knowledge.EnsureSeededAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentHost] seed failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Rebuilds on the next Ensure() — used after the provider or the
        /// privacy settings change so the new posture takes effect.
        /// </summary>
        public static void Invalidate()
        {
            lock (Gate)
            {
                _built = false;
                // Proper resource cleanup before clearing references.
                try { Voice?.StopSpeaking(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[AgentHost] Invalidate: best-effort: " + ex.Message); }
                AI = null;
                CommandRouter = null;
                TaskAdapter = null;
                Instance = null;
                Voice = null;
            }
        }
    }
}
