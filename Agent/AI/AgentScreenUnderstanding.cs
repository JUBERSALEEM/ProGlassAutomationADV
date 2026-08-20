using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Event-aware screen understanding that avoids continuous scraping.</summary>
    public sealed class AgentScreenUnderstanding
    {
        /// <summary>Captures important workflow events, not static UI elements.</summary>
        public IReadOnlyList<string> ExtractSemanticEvents(string windowTitle, string viewState)
        {
            var events = new List<string>();
            if (string.IsNullOrWhiteSpace(windowTitle) && string.IsNullOrWhiteSpace(viewState))
                return events;

            // Prioritize workflow transitions over static elements.
            if (!string.IsNullOrWhiteSpace(windowTitle))
            {
                events.Add($"Window: {windowTitle}");
            }

            if (!string.IsNullOrWhiteSpace(viewState) && viewState.Contains("Wizard"))
            {
                events.Add("Workflow: Wizard active (multi-step process)");
            }

            return events.AsReadOnly();
        }

        /// <summary>Generates concise professional narration based on events.</summary>
        public string GenerateNarration(IReadOnlyList<string> events)
        {
            if (events.Count == 0) return "No significant workflow events detected.";

            return string.Join(". ", events.Select(e =>
            {
                if (e.Contains("Window")) return $"The user is working in: {e.Split(':').Last().Trim()}";
                if (e.Contains("Wizard")) return "A multi-step wizard is guiding the user through a structured process.";
                return e;
            }));
        }
    }
}
