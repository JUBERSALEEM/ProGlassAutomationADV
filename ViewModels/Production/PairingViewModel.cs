using ProGlassAutomation.Models.Production;

namespace ProGlassAutomation.ViewModels.Production
{
    /// <summary>
    /// Pairing stage queue — work items distributed from released Job Orders
    /// and Approved Proforma Invoices. See
    /// docs/PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md.
    /// </summary>
    public class PairingViewModel : ProductionStageViewModelBase
    {
        public override ProductionStage Stage => ProductionStage.Pairing;
        public override string ModuleName => "Pairing";
        public override string Description => "Pair glass pieces with stands for tracking and identification across production floor.";
        public override string Icon => "\uE71B"; // Segoe MDL2 (matches AboutView production stages)
    }
}
