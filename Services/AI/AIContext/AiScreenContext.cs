// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIContext/AiScreenContext.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Context — global screen awareness (v3 JARVIS pass)
//
// JARVIS knows WHERE you are. This tiny static tracker records which
// module/screen the user is currently viewing (updated by
// MainViewModel on every navigation) and lets the assistant:
//   • answer "help me with this screen" without being told the module
//   • tailor suggestions to the active module
//   • reason about what the user is looking at right now
//
// ADDITIVE: nothing existing reads or writes this unless the AI
// features opt in. Thread-safe enough for UI-thread writes + reads.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Services.AI.AIContext
{
    /// <summary>
    /// Tracks the currently visible module/screen so the AI assistant
    /// is aware of the user's context across the whole application.
    /// </summary>
    public static class AiScreenContext
    {
        /// <summary>Friendly name of the active module (e.g. "Sheet Store").</summary>
        public static string CurrentModule { get; private set; } = "";

        /// <summary>Optional detail (e.g. "editing PI-2026-014", "History tab").</summary>
        public static string CurrentDetail { get; private set; } = "";

        /// <summary>Called by MainViewModel whenever navigation occurs.</summary>
        public static void Set(string module, string detail = "")
        {
            CurrentModule = module ?? string.Empty;
            CurrentDetail = detail ?? string.Empty;
        }

        /// <summary>Modules can enrich the detail (e.g. selected record).</summary>
        public static void SetDetail(string detail) =>
            CurrentDetail = detail ?? string.Empty;

        /// <summary>
        /// One-line description for prompt injection. Empty when the
        /// tracker has not been set yet (e.g. on the splash screen).
        /// </summary>
        public static string Describe()
        {
            if (string.IsNullOrWhiteSpace(CurrentModule))
                return string.Empty;

            return string.IsNullOrWhiteSpace(CurrentDetail)
                ? $"CURRENT SCREEN: the user is viewing the \"{CurrentModule}\" module right now."
                : $"CURRENT SCREEN: the user is viewing \"{CurrentModule}\" ({CurrentDetail}) right now.";
        }
    }
}
