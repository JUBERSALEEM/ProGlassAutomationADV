using ProGlassAutomation.Models.Production;

namespace ProGlassAutomation.ViewModels.Production
{
    /// <summary>
    /// Pairing stage queue — work items distributed from released Job Orders
    /// (confirmed PIs — drafts never reach the floor). See
    /// docs/PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md.
    /// </summary>
    public class PairingViewModel : ProductionStageViewModelBase
    {
        public override ProductionStage Stage => ProductionStage.Pairing;
        public override string ModuleName => "Pairing";
        public override string Description => "Multi-pane builds only: ready panes are matched and paired into units — pairing quantity is bounded by the slowest pane of the unit.";
        public override string IndustryFocus => "Match & pair — multi-pane units only · bounded by the slowest pane · DGU / LAM / DGU+LAM.";
        public override string Icon => "\uE71B"; // Segoe MDL2 (matches AboutView production stages)
    }
}
