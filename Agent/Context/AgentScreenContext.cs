// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Context/AgentScreenContext.cs
// PROJECT: ProGlassAutomation — ProGlass Agent
//
// MASTER PROMPT §14 (context layer), §15 (screen awareness),
//                §27 (memory safety), §45/§46 (no screenshots, no hidden
//                recording), §4/§51 (reuse, do not duplicate)
//
// ─────────────────────────────────────────────────────────────────────
//  WHAT "THE AGENT LEARNS WHAT I AM DOING" SAFELY MEANS
// ─────────────────────────────────────────────────────────────────────
//  The request was for the agent to know which view is open and to learn
//  in the background — while staying secure and local. Those two pull in
//  opposite directions, so this class draws the line explicitly.
//
//  WHAT IT OBSERVES
//    • the name of the module on screen        ("Sheet Store")
//    • an optional short detail a module opts into ("editing PI-2026-014")
//    • when that last changed
//    • an in-session trail of the last few modules visited
//
//  WHAT IT DOES NOT OBSERVE, BY DESIGN
//    • no screenshots, no OCR            (§45 — structured data or nothing)
//    • no keystrokes, no field contents  (a quoted price is not context)
//    • no microphone, no continuous capture (§46)
//    • no customer names, balances or prices
//    • NOTHING is written to disk. This is RAM only, and it dies with the
//      process. Background observation that silently accumulates a
//      permanent file is surveillance, not assistance.
//
//  WHY IT IS NOT MEMORY (§27)
//    "The user looked at Sheet Store at 14:32" is not a durable fact worth
//    remembering; it is transient state. Agent memory persists only what
//    the user explicitly approves. Mixing the two would turn every glance
//    at a screen into a permanent record.
//
//  REUSE, NOT DUPLICATION (§4)
//    Services/AI/AIContext/AiScreenContext already tracks the active module
//    and is already updated by MainViewModel on every navigation. This
//    class READS that existing tracker rather than adding a second hook
//    into navigation, so the two can never disagree and the ERP is not
//    modified at all.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIContext;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProGlassAutomation.Agent.Context
{
    /// <summary>One observed screen visit. In-memory only.</summary>
    public sealed class ScreenVisit
    {
        public string Module { get; init; } = "";
        public DateTime AtLocal { get; init; } = DateTime.Now;
    }

    /// <summary>
    /// The agent's read-only window onto "what is the user looking at".
    /// Holds no ERP data and writes nothing to disk.
    /// </summary>
    public static class AgentScreenContext
    {
        /// <summary>
        /// Trail cap. Small on purpose: this exists to answer "what was I
        /// just doing", not to build a usage history. A large buffer would
        /// be a behavioural log in all but name.
        /// </summary>
        public const int MaxTrail = 10;

        private static readonly object Gate = new();
        private static readonly List<ScreenVisit> Trail = new();
        private static string _lastSeenModule = "";

        /// <summary>The module currently on screen, or empty.</summary>
        public static string CurrentModule
        {
            get
            {
                try { return AiScreenContext.CurrentModule ?? ""; }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[AgentScreenContext] {ex.Message}");
                    return "";
                }
            }
        }

        /// <summary>
        /// Supplies the live view-model so detail can be re-resolved at read
        /// time. Set once by the shell; null simply means no detail.
        ///
        /// WHY A PULL AND NOT A PUSH: AiScreenContext.CurrentDetail is only
        /// written when navigation happens. Select a different sheet without
        /// leaving Sheet Store and that stored string goes stale, so the
        /// agent would confidently name the PREVIOUS record. Asking the live
        /// view-model at the moment of the question cannot go stale.
        /// </summary>
        public static Func<object?>? CurrentViewModelProvider { get; set; }

        /// <summary>
        /// Optional short detail a module chose to publish, e.g.
        /// "editing PI-2026-014". Modules opt in via IAgentContextAware;
        /// nothing is scraped and nothing is guessed.
        /// </summary>
        public static string CurrentDetail
        {
            get
            {
                try
                {
                    // Live value first - see the staleness note above.
                    var vm = CurrentViewModelProvider?.Invoke();

                    if (vm is IAgentContextAware aware)
                    {
                        string live = aware.AgentContextDetail ?? "";
                        if (live.Length > 0) return live;
                    }

                    // Fall back to whatever navigation last recorded.
                    return AiScreenContext.CurrentDetail ?? "";
                }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[AgentScreenContext] {ex.Message}");
                    return "";
                }
            }
        }

        /// <summary>
        /// Records the current module into the in-session trail if it has
        /// changed. Called opportunistically when the agent is used — there
        /// is deliberately NO timer and NO navigation hook, so nothing is
        /// observed while the agent is idle.
        /// </summary>
        public static void Observe()
        {
            try
            {
                string module = CurrentModule;
                if (string.IsNullOrWhiteSpace(module)) return;

                lock (Gate)
                {
                    if (string.Equals(module, _lastSeenModule,
                            StringComparison.OrdinalIgnoreCase))
                        return;

                    _lastSeenModule = module;
                    Trail.Add(new ScreenVisit { Module = module });

                    while (Trail.Count > MaxTrail) Trail.RemoveAt(0);
                }
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentScreenContext] observe: {ex.Message}");
            }
        }

        /// <summary>Most recent first. Copy, so callers cannot mutate it.</summary>
        public static IReadOnlyList<ScreenVisit> RecentScreens()
        {
            lock (Gate) return Trail.AsEnumerable().Reverse().ToList();
        }

        /// <summary>
        /// One line the agent can reason with, e.g.
        /// "You are on Sheet Store (editing PI-2026-014)."
        /// Empty when nothing is known — an empty string is honest, a
        /// guessed module is not.
        /// </summary>
        public static string Describe()
        {
            string module = CurrentModule;
            if (string.IsNullOrWhiteSpace(module)) return "";

            string detail = CurrentDetail;

            return string.IsNullOrWhiteSpace(detail)
                ? $"You are on {module}."
                : $"You are on {module} ({detail}).";
        }

        /// <summary>
        /// Clears the trail. Wired to the agent's memory reset so "forget
        /// what I was doing" actually forgets it.
        /// </summary>
        public static void Clear()
        {
            lock (Gate)
            {
                Trail.Clear();
                _lastSeenModule = "";
            }
        }
    }
}
