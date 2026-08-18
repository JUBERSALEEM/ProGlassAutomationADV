// ═══════════════════════════════════════════════════════════════════════
//  Services/Statement/Workflow/UserWarningDecision.cs
//
//  Outcome of a missing-invoice warning prompt. Extracted from
//  StatementWorkflowEngine (Phase 11/12 split) — unchanged.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Services.Statement.Workflow
{
    /// <summary>How the user resolved a missing-invoice warning.</summary>
    public enum UserWarningDecision
    {
        CreateInvoice = 0,
        SkipAndContinue = 1,
        Abort = 2
    }
}
