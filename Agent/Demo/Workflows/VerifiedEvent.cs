using System;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// One action that actually succeeded. Narration is generated
    /// only from these — planned-but-unrun steps never become speech.
    /// </summary>
    public sealed class VerifiedEvent
    {
        public string Id { get; init; } = "";
        public string Title { get; init; } = "";
        public string Narration { get; init; } = "";
        public string Evidence { get; init; } = "";
        public DateTime Utc { get; init; } = DateTime.UtcNow;
        /// <summary>How long live speech actually took on this beat.</summary>
        public TimeSpan SpokenDuration { get; init; }
    }
}
