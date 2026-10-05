using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Regression verification checklist for AI platform phases.</summary>
    public static class AIRegressionChecklist
    {
        public static IReadOnlyList<string> GetPhaseVerificationItems(int phaseNumber)
        {
            var list = new List<string>();
            if (phaseNumber == 1) list.AddRange(new[] { "Audit complete", "Architecture mapped", "No broken existing AI components" });
            else if (phaseNumber == 2) list.AddRange(new[] { "Architecture map accurate", "Dependencies traced", "Duplication identified" });
            else if (phaseNumber == 3) list.AddRange(new[] { "Target design validated", "Security preserved", "Fallback strategy defined" });
            else if (phaseNumber == 4) list.AddRange(new[] { "Component classification complete", "Migration strategy defined" });
            else if (phaseNumber == 5) list.AddRange(new[] { "Provider abstraction compiles", "Router selects appropriate provider" });
            else if (phaseNumber == 6) list.AddRange(new[] { "Orchestrator integrated with AgentHost", "Lazy init preserved", "Non-throwing" });
            else if (phaseNumber == 7) list.AddRange(new[] { "Command router routes typed/voice/agent tasks", "No broken agent entry points" });
            else if (phaseNumber == 8) list.AddRange(new[] { "Typed parameters defined", "Validation rules work", "Timeout/cancellation functional" });
            else if (phaseNumber == 9) list.AddRange(new[] { "Multi-step engine runs", "Plan-observe-verify-correct loop works", "Audit log produced" });
            else list.Add("Verification required");
            return list.AsReadOnly();
        }
    }
}
