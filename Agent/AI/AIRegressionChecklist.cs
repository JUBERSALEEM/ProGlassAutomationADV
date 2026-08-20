using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Regression verification checklist for AI platform phases.</summary>
    public static class AIRegressionChecklist
    {
        public static IReadOnlyList<string> GetPhaseVerificationItems(int phaseNumber) => phaseNumber switch
        {
            1 => new[] { "Audit complete", "Architecture mapped", "No broken existing AI components" },
            2 => new[] { "Architecture map accurate", "Dependencies traced", "Duplication identified" },
            3 => new[] { "Target design validated", "Security preserved", "Fallback strategy defined" },
            4 => new[] { "Component classification complete", "Migration strategy defined" },
            5 => new[] { "Provider abstraction compiles", "Router selects appropriate provider" },
            6 => new[] { "Orchestrator integrated with AgentHost", "Lazy init preserved", "Non-throwing" },
            7 => new[] { "Command router routes typed/voice/agent tasks", "No broken agent entry points" },
            8 => new[] { "Typed parameters defined", "Validation rules work", "Timeout/cancellation functional" },
            9 => new[] { "Multi-step engine runs", "Plan-observe-verify-correct loop works", "Audit log produced" },
            _ => new[] { "Verification required" }
        }.AsReadOnly();
    }
}
