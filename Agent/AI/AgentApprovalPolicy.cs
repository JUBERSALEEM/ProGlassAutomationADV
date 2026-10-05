using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Risk level classification for approval policy.</summary>
    public enum RiskLevel
    {
        Read,
        Navigation,
        NonDestructiveUI,
        CreateRecord,
        EditRecord,
        Delete,
        FinancialOperation,
        SystemOperation
    }

    /// <summary>Approval policy configuration for agent operations.</summary>
    public sealed class ApprovalPolicy
    {
        public bool AutoConfirmLowRisk { get; set; } = true;
        public bool RequireConfirmationWrite { get; set; } = true;
        public bool RequireConfirmationDelete { get; set; } = true;
        public bool RequireConfirmationFinancial { get; set; } = true;
        public bool RequireConfirmationSystem { get; set; } = true;
    }

    /// <summary>Extended approval gate for agent actions.</summary>
    public interface IAgentApprovalService
    {
        Task<bool> ShouldConfirmAsync(RiskLevel risk, string argument);
        string GetConfirmationPrompt(RiskLevel risk, string argument);
    }
}
