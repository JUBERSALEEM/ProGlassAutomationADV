using ProGlassAutomation.Models.Production;

namespace ProGlassAutomation.ViewModels.Production
{
    /// <summary>
    /// Cutting stage queue — work items distributed from released Job Orders
    /// (confirmed PIs — drafts never reach the floor). See
    /// docs/PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md.
    /// </summary>
    public class CuttingViewModel : ProductionStageViewModelBase
    {
        public override ProductionStage Stage => ProductionStage.Cutting;
        public override string ModuleName => "Cutting";
        public override string Description => "First station of the chain: every pane is cut to size — W1×H1, plus W2×H2 for overlap glass (blank W2/H2 cuts equal-size lites). Cut pieces feed Holes, Polish, Mitring and Tempering.";
        public override string IndustryFocus => "Cut to size — W1×H1 outer, W2×H2 overlap inner · every pane enters the factory here (IN = Qty).";
        public override string Icon => "\uE8C6"; // Segoe MDL2 (matches AboutView production stages)
    }
}
